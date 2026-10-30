using Evolysia.Sim;
using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Evolysia.Sim.Society;
using Evolysia.Sim.World;
using Godot;

namespace Evolysia.Game;

/// <summary>
/// A band's home in art, laid out in logical units (16 per cell) around the band's position, so
/// the same camp stands in the same place at every density. Wandering bands camp in the open:
/// shelters by what they know (windbreak, lean-to, hide tent), a hearth, knapping block, baskets,
/// drying racks, a totem where they keep rituals. Farming villages build houses in their region's
/// style (regional_styles.json) with a granary, kiln and loom. A selection of the band's real
/// people go about their day: out to forage, hunt, fish or farm and back to the fire, or crafting,
/// talking, praying and playing in camp.
/// </summary>
public static class Camps
{
    const float Unit = Main.CellPx / 16f;   // world units per logical unit

    static Vector2 L(float x, float y) => new Vector2(x, y) * Unit;

    static readonly Vector2[] ShelterSpots = { L(-10, -5), L(10, -5), L(-13, 7), L(13, 7) };
    static readonly Vector2[] HouseSpots = { L(-28, -14), L(28, -14), L(-30, 24), L(30, 24), L(0, -30), L(0, 38) };

    /// <summary>What the layer needs to know about a band this frame.</summary>
    public sealed record Context(Simulation Sim, Band Band, Vector2 Pos, ulong Knows, int Density, double Now, bool Selected);

    public static void Add(Context c, List<WorldSprite> into)
    {
        var sim = c.Sim;
        var band = c.Band;
        int d = c.Density;
        bool K(TechId t) => (c.Knows & Techs.Bit(t)) != 0;
        var tint = band.PlagueUntil > sim.Day ? new Color(0.85f, 1f, 0.7f) : Colors.White;
        int n = band.Members.Count;
        int hue = sim.Cultures[band.CultureId].Hue % 12;
        var pos = c.Pos;
        var w = sim.World;
        int animFrame = (int)(c.Now * 6.0);

        void Put(string? lod, string? baseId, Vector2 at, int frame = 0, float shadow = -1f, int culture = -1)
        {
            if (Sprites.Make(lod != null ? "civilisation." + lod : null, baseId, d, pos + at, tint, frame, shadow: shadow, cultureRow: culture) is { } s) into.Add(s);
        }

        if (band.Sedentary)
        {
            var style = Settlements.StyleOf(w.Biome[band.Cell]);
            if (style.Roof == "tile" && !K(TechId.Pottery)) style = style with { Roof = "thatch" };
            int houses = Math.Clamp(1 + n / 8, 2, HouseSpots.Length);
            for (int k = 0; k < houses; k++)
                if (House(style, 3, 2, d, roofed: !(c.Selected && d >= 64)) is { } house)
                    into.Add(new WorldSprite(pos + HouseSpots[k], house.Texture, new Rect2(Vector2.Zero, house.Texture.GetSize()), house.Pivot,
                                             Sprites.PixelSize(d), tint, 3f * 16 * Unit * 0.42f));
            if (K(TechId.FoodStorage))
            {
                if (K(TechId.Agriculture)) Container("grain_bin", "grain", Fill(band), L(-8, -12));
                else Container("crate", "dried_meat", Fill(band), L(-8, -12));
            }
            if (K(TechId.Pottery)) Put("kiln", null, L(14, 6), animFrame);
            if (K(TechId.Weaving)) Put("loom", null, L(-14, 6));
            if (K(TechId.Domestication)) Put("feed_trough", null, L(44, 4));
        }
        else
        {
            // Shelters from the multi-density pieces: a reed windbreak, a wattle lean-to, a hide-roofed hut.
            var shelter = K(TechId.Shelter) ? House(new Settlements.Style("hide", "hide", "earth"), 2, 2, d, roofed: true)
                        : Fence(K(TechId.HideClothing) ? "wattle" : "reed", K(TechId.HideClothing) ? 2 : 1, d);
            int shelters = Math.Clamp(1 + n / 10, 1, ShelterSpots.Length);
            for (int k = 0; shelter != null && k < shelters; k++)
                into.Add(new WorldSprite(pos + ShelterSpots[k], shelter.Texture, new Rect2(Vector2.Zero, shelter.Texture.GetSize()), shelter.Pivot,
                                         Sprites.PixelSize(d), tint, shelter.Texture.GetWidth() * Sprites.PixelSize(d) * 0.42f));
            if (K(TechId.FoodStorage)) Container("crate", K(TechId.Fishing) ? "dried_fish" : "dried_meat", Fill(band), L(0, -10));
            if (K(TechId.Baskets)) Container("basket", K(TechId.Fishing) ? "fish" : "berries", Fill(band), L(-5, -6));
            if (K(TechId.Fishing) && NearWater(w, band.Cell) != null) Put("fish_drying_rack", null, L(10, 1));
            if (K(TechId.Weaving)) Put("loom", null, L(-10, 2));
        }
        if (K(TechId.ControlledFire)) Put("hearth", null, L(0, 1), animFrame, shadow: 0f);
        if (K(TechId.StoneTools)) Put("knapping_block", null, L(-6, 4));
        if (K(TechId.Cooking) && K(TechId.Pottery)) Put("cooking_pot", null, L(5, 3));
        if (K(TechId.Pottery)) Put("pot", null, L(7, -4));
        if (K(TechId.Copper)) Put("anvil", null, L(-16, -2));
        if (K(TechId.Boats) && NearWater(w, band.Cell) is { } shore) Put("canoe", null, Rel(c, shore) * 0.85f);
        if (HoldsRitual(sim, band, out bool god))
        {
            if (god) Put("altar", null, L(0, -15));
            else Put(null, "structures.totem_complete", L(0, -15), culture: hue);
        }
        if (c.Selected && band.Sedentary && d >= 64) Put("bed", null, L(-28, -20));

        AddPeople(c, into, tint);

        void Container(string profile, string resource, int filled, Vector2 at)
        {
            if (Stocked(profile, resource, filled, d) is { } s)
                into.Add(new WorldSprite(pos + at, s.Texture, new Rect2(Vector2.Zero, s.Texture.GetSize()), s.Pivot, Sprites.PixelSize(d), tint, 5f * Unit));
        }
    }

    static int Fill(Band band) => Math.Clamp((int)MathF.Ceiling(band.FoodPool / Math.Max(1, band.Members.Count) / 3f), 0, 9);

    static bool HoldsRitual(Simulation sim, Band band, out bool god)
    {
        god = false;
        bool ritual = false;
        foreach (var (id, share) in band.MemePrevalence)
        {
            if (share < 0.3f || !sim.Memes.TryGetValue(id, out var m)) continue;
            if (m.IsPlayerGod) god = true;
            if (m.Kind is MemeKind.Ritual or MemeKind.SpiritBelief) ritual = true;
        }
        return ritual || god;
    }

    static int? NearWater(WorldMap w, int cell)
    {
        int x = w.X(cell), y = w.Y(cell);
        for (int r = 1; r <= 2; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int ny = y + dy;
                    if (ny < 0 || ny >= w.H) continue;
                    int i = w.Index(x + dx, ny);
                    if (w.IsWater(i) || TileBake.IsRiver(w, i)) return i;
                }
        return null;
    }

    /// <summary>A cell's centre relative to the band's position, the short way round the planet.</summary>
    static Vector2 Rel(Context c, int cell)
    {
        var w = c.Sim.World;
        var v = new Vector2(w.X(cell) + 0.5f, w.Y(cell) + 0.5f) * Main.CellPx - c.Pos;
        float width = w.W * Main.CellPx;
        v.X -= MathF.Round(v.X / width) * width;
        return v;
    }

    // ---- people ---------------------------------------------------------------------------------

    static void AddPeople(Context c, List<WorldSprite> into, Color tint)
    {
        var sim = c.Sim;
        var band = c.Band;
        var w = sim.World;
        int n = band.Members.Count;
        int shown = Math.Min(n, Math.Min(18, 4 + n / 3));
        int hue = sim.Cultures[band.CultureId].Hue;
        float temp = w.Temperature(band.Cell, sim.Day);
        int placed = 0;
        var water = NearWater(w, band.Cell);
        foreach (int id in band.Members.Where(m => m == band.LeaderId).Concat(band.Members.Where(m => m != band.LeaderId)))
        {
            if (placed >= shown) break;
            if (!sim.Living.TryGetValue(id, out var agent)) continue;
            bool child = agent.IsChild(sim.Day);
            var activity = child ? (agent.Today == Activity.Play || id % 2 == 0 ? Activity.Play : Activity.Rest) : agent.Today;
            float ring = (band.Sedentary ? 12f : 9f) + shown * 0.35f;
            float angle = band.Id * 1.7f + placed * Mathf.Tau / shown;
            var home = L(MathF.Cos(angle) * ring, MathF.Sin(angle) * ring * 0.7f + 2f);
            var spot = Ashore(c, Spot(c, activity, id, angle, home, water));
            uint h = TileBake.Hash(id, 9);

            // A day's rhythm in a short loop: out to the work spot, work, back, rest by the fire.
            double period = 18.0 + h % 7;
            double phase = (c.Now / period + (h & 0xFF) / 255.0) % 1.0;
            Vector2 at, velocity = Vector2.Zero;
            bool away = spot.DistanceSquaredTo(home) > 4f;
            bool working = true;
            if (!away || agent.Sick) at = agent.Sick ? home : spot;
            else if (phase < 0.12) { at = home.Lerp(spot, (float)(phase / 0.12)); velocity = spot - home; }
            else if (phase < 0.75) at = spot;
            else if (phase < 0.87) { at = spot.Lerp(home, (float)((phase - 0.75) / 0.12)); velocity = home - spot; }
            else { at = home; working = false; }
            if (child && activity == Activity.Play)   // children chase each other round the fire
            {
                float a = (float)(c.Now * 0.9 + (h & 0xFF) / 40.0);
                at = L(MathF.Cos(a) * 14f, MathF.Sin(a) * 9f + 4f);
                velocity = new Vector2(-MathF.Sin(a), MathF.Cos(a) * 0.6f);
            }
            bool moving = velocity != Vector2.Zero && !(child && activity == Activity.Play);
            string anim = People.Animation(working ? activity : Activity.Rest, moving, child, agent.Sick);
            if (child && activity == Activity.Play) anim = "run";
            string dir = "down";
            bool flip = false;
            if (velocity != Vector2.Zero)
            {
                if (MathF.Abs(velocity.X) > MathF.Abs(velocity.Y) * 0.8f) { dir = "side"; flip = velocity.X < 0; }
                else dir = velocity.Y > 0 ? "down" : "up";
            }
            else if (anim is "craft" or "talk" or "attack" or "sleep") { dir = "side"; flip = at.X > 0; }   // face the fire

            var look = People.LookOf(agent, sim.Day, hue, temp, id == band.LeaderId, working ? activity : Activity.Rest);
            if (People.Get(look, anim, dir, c.Density) is not { } strip) return;
            int frame = (int)(c.Now * strip.Fps + (h & 0xF) * 0.37) % strip.Frames;
            var pivot = strip.Pivot + new Vector2(0, 1);
            into.Add(new WorldSprite(c.Pos + at, strip.Texture, new Rect2(frame * strip.FrameW, 0, strip.FrameW, strip.FrameH), pivot,
                                     Sprites.PixelSize(c.Density), tint, 3.5f * Unit, flip));
            placed++;
        }
    }

    /// <summary>Pulls a spot back towards camp until it is on land.</summary>
    static Vector2 Ashore(Context c, Vector2 spot)
    {
        var w = c.Sim.World;
        for (int k = 0; k < 6 && spot.LengthSquared() > 4f; k++)
        {
            var p = c.Pos + spot;
            int y = (int)MathF.Floor(p.Y / Main.CellPx);
            if (y < 0 || y >= w.H) { spot *= 0.6f; continue; }
            if (!w.IsWater(w.Index((int)MathF.Floor(p.X / Main.CellPx), y))) break;
            spot *= 0.6f;
        }
        return spot;
    }

    /// <summary>Where an activity takes someone, relative to the band's position.</summary>
    static Vector2 Spot(Context c, Activity activity, int id, float angle, Vector2 home, int? water)
    {
        var w = c.Sim.World;
        uint h = TileBake.Hash(id, c.Sim.Day / 5);
        float a = (h & 0xFFFF) / 65535f * Mathf.Tau;
        Vector2 Out(float r) => L(MathF.Cos(a) * r, MathF.Sin(a) * r * 0.8f);
        switch (activity)
        {
            case Activity.Forage: return Out(20f + h % 10);
            case Activity.Hunt: return Out(34f + h % 10);
            case Activity.Explore: return Out(44f);
            case Activity.Herd: return L(40 + h % 8, 8 + h % 10);
            case Activity.Fish when water is { } cell: return Rel(c, cell) * 0.4f + L((h % 5) - 2f, (h >> 8) % 5 - 2f);   // at the water's edge
            case Activity.Fish: return Out(24f);
            case Activity.Farm when c.Band.Sedentary: return Out(32f);   // the field ring two cells out
            case Activity.Build: return (c.Band.Sedentary ? HouseSpots[id % 4] : ShelterSpots[id % 2]) + L(6, 2);
            case Activity.Craft or Activity.Experiment: return L(-8 + (h % 3) * 2, 6);
            case Activity.Socialize or Activity.Court or Activity.Teach: return home * 0.6f;
            case Activity.Ritual: return L(MathF.Cos(angle) * 6f, -11f + MathF.Sin(angle) * 3f);
            case Activity.Mourn: return L(18f, -16f);
            default: return home;
        }
    }

    // ---- composites -----------------------------------------------------------------------------

    public sealed record Composite(ImageTexture Texture, Vector2 Pivot);

    static readonly Dictionary<string, Composite?> Composites = new();

    /// <summary>
    /// A rectangular house of <paramref name="cw"/> x <paramref name="ch"/> cells from civilisation
    /// pieces: floor inside the wall line, a ring of connected walls (N=1 E=2 S=4 W=8), a door in
    /// the south wall, and a roof over it all with a shaded eave - or, roofless, a cut-away with the
    /// south wall removed (render_contract roof_occlusion) and the room's furniture showing.
    /// </summary>
    public static Composite? House(Settlements.Style style, int cw, int ch, int d, bool roofed)
    {
        string key = $"house|{style}|{cw}x{ch}|{d}|{roofed}";
        if (Composites.TryGetValue(key, out var cached)) return cached;
        Composite? result = null;
        string wall = Art.Lod($"civilisation.wall_{style.Wall}_00", d) != null ? style.Wall : "wattle";
        var floor = Art.Lod($"civilisation.floor_{style.Floor}", d) ?? Art.Lod("civilisation.floor_earth", d);
        var roof = Art.Lod($"civilisation.roof_{style.Roof}", d) ?? Art.Lod("civilisation.roof_thatch", d);
        if (floor != null && roof != null)
        {
            int width = cw * d, height = ch * d;
            var img = new byte[width * height * 4];
            void Stamp(Sheet s, int ox, int oy, Rect2I? clip = null, float shade = 1f)
            {
                for (int y = 0; y < s.FrameH; y++)
                    for (int x = 0; x < s.FrameW; x++)
                    {
                        int tx = ox + x, ty = oy + y;
                        if (tx < 0 || ty < 0 || tx >= width || ty >= height) continue;
                        if (clip is { } r && !r.HasPoint(new Vector2I(tx, ty))) continue;
                        var c = s.Reference.GetPixel(x, y);
                        if (c.A < 0.5f) continue;
                        int o = (ty * width + tx) * 4;
                        img[o] = (byte)(c.R * 255 * shade); img[o + 1] = (byte)(c.G * 255 * shade); img[o + 2] = (byte)(c.B * 255 * shade); img[o + 3] = 255;
                    }
            }
            var inside = new Rect2I(d / 2, d / 2, width - d, height - d);
            for (int y = 0; y < ch; y++) for (int x = 0; x < cw; x++) Stamp(floor, x * d, y * d, inside);
            for (int y = 0; y < ch; y++)
                for (int x = 0; x < cw; x++)
                {
                    bool side = x == 0 || x == cw - 1, end = y == 0 || y == ch - 1;
                    if (!side && !end) continue;
                    if (!roofed && y == ch - 1) continue;   // cut away the south wall
                    int mask = (side && y > 0 ? 1 : 0) | (end && x < cw - 1 ? 2 : 0) | (side && y < ch - 1 ? 4 : 0) | (end && x > 0 ? 8 : 0);
                    if (Art.Lod($"civilisation.wall_{wall}_{mask:00}", d) is { } piece) Stamp(piece, x * d, y * d);
                }
            if (roofed)
            {
                if (Art.Lod("civilisation.door_wood_closed", d) is { } door) Stamp(door, cw / 2 * d, (ch - 1) * d);
                // The roof covers the walls' tops down to the south wall line, its eave shaded.
                int bottom = (ch - 1) * d + d / 2, eave = Math.Max(1, d / 16);
                for (int y = 0; y < ch; y++)
                    for (int x = 0; x < cw; x++)
                        Stamp(roof, x * d, y * d, new Rect2I(eave, 0, width - 2 * eave, bottom));
                for (int y = 0; y < bottom; y++)
                    for (int x = eave; x < width - eave; x++)
                    {
                        bool edge = y >= bottom - eave * 2 || x < eave * 2 || x >= width - eave * 2;
                        if (!edge) continue;
                        int o = (y * width + x) * 4;
                        float k = y >= bottom - eave ? 0.55f : 0.75f;
                        img[o] = (byte)(img[o] * k); img[o + 1] = (byte)(img[o + 1] * k); img[o + 2] = (byte)(img[o + 2] * k);
                    }
            }
            else
            {
                if (Art.Lod("civilisation.hearth", d) is { } hearth) Stamp(hearth, 2 * d - d / 2, d / 2);
                if (Art.Lod("civilisation.pot", d) is { } pot) Stamp(pot, (cw - 1) * d - d / 4, d / 2);
            }
            var tex = ImageTexture.CreateFromImage(Image.CreateFromData(width, height, false, Image.Format.Rgba8, img));
            result = new Composite(tex, new Vector2(width / 2f, height));
        }
        Composites[key] = result;
        return result;
    }

    /// <summary>A short straight screen of wall pieces (E-W): a windbreak or lean-to.</summary>
    public static Composite? Fence(string wall, int cells, int d)
    {
        string key = $"fence|{wall}|{cells}|{d}";
        if (Composites.TryGetValue(key, out var cached)) return cached;
        Composite? result = null;
        var image = Image.CreateEmpty(cells * d, d, false, Image.Format.Rgba8);
        bool any = false;
        for (int x = 0; x < cells; x++)
        {
            int mask = (x < cells - 1 ? 2 : 0) | (x > 0 ? 8 : 0);
            if (cells == 1) mask = 10;
            if (Art.Lod($"civilisation.wall_{wall}_{mask:00}", d) is not { } piece) continue;
            image.BlendRect(piece.Reference, new Rect2I(0, 0, piece.FrameW, piece.FrameH), new Vector2I(x * d, 0));
            any = true;
        }
        if (any) result = new Composite(ImageTexture.CreateFromImage(image), new Vector2(cells * d / 2f, d));
        Composites[key] = result;
        return result;
    }

    /// <summary>
    /// A storage container with what it holds (storage_profiles.json): back, the visible slots
    /// filled back to front with the resource, then the front rim.
    /// </summary>
    public static Composite? Stocked(string profile, string resource, int filled, int d)
    {
        string key = $"store|{profile}|{resource}|{filled}|{d}";
        if (Composites.TryGetValue(key, out var cached)) return cached;
        Composite? result = null;
        var back = Art.Lod($"civilisation.{profile}_back", d);
        var front = Art.Lod($"civilisation.{profile}_front", d);
        var item = Art.Lod($"civilisation.stored_{resource}", d);
        if (back != null && front != null)
        {
            var image = (Image)back.Reference.GetRegion(new Rect2I(0, 0, back.FrameW, back.FrameH));
            var anchors = Anchors(profile);
            for (int k = 0; k < Math.Min(filled, anchors.Count) && item != null; k++)
            {
                var a = anchors[k] * d / 64f;
                image.BlendRect(item.Reference, new Rect2I(0, 0, item.FrameW, item.FrameH), new Vector2I((int)a.X - item.PivotX, (int)a.Y - item.PivotY));
            }
            image.BlendRect(front.Reference, new Rect2I(0, 0, front.FrameW, front.FrameH), Vector2I.Zero);
            result = new Composite(ImageTexture.CreateFromImage(image), new Vector2(back.PivotX, back.PivotY + 1));
        }
        Composites[key] = result;
        return result;
    }

    static Dictionary<string, List<Vector2>>? _anchors;

    static List<Vector2> Anchors(string profile)
    {
        if (_anchors == null)
        {
            _anchors = new();
            if (Art.Json("civilisation/storage_profiles.json") is { } json)
                foreach (var p in json.GetProperty("profiles").EnumerateObject())
                    _anchors[p.Name] = p.Value.GetProperty("slot_anchors_px64").EnumerateArray().Select(a => new Vector2(a[0].GetSingle(), a[1].GetSingle())).ToList();
        }
        return _anchors.GetValueOrDefault(profile) ?? new List<Vector2>();
    }
}
