using Evolysia.Sim;
using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Evolysia.Sim.Economy;
using Evolysia.Sim.Society;
using Godot;

namespace Evolysia.Game;

public static class Camps
{
    const float Unit = Main.CellPx / 16f;

    static Vector2 CellPos(Simulation sim, int cell, Vector2 near)
    {
        var w = sim.World;
        var p = new Vector2(w.X(cell), w.Y(cell)) * Main.CellPx;
        float width = w.W * Main.CellPx;
        p.X += MathF.Round((near.X - p.X) / width) * width;
        return p;
    }

    public static void AddStructures(Simulation sim, Rect2 view, int density, double now, int selectedBand, List<WorldSprite> into)
    {
        var grown = view.Grow(Main.CellPx * 4f);
        var centre = view.GetCenter();
        foreach (var s in sim.Structures.Values)
        {
            var k = s.Kind;
            if (k.Role == StructureRole.Field || s.State == StructureState.Planned) continue;
            var corner = CellPos(sim, s.Cell, centre);
            var foot = corner + new Vector2(k.W * 0.5f, k.H) * Main.CellPx;
            if (!grown.HasPoint(foot)) continue;
            sim.Bands.TryGetValue(s.BandId, out var band);
            if (Visual(sim, s, band, density, now, selectedBand) is not { } v) continue;
            var modulate = s.State switch
            {
                StructureState.Building => new Color(1f, 1f, 1f, 0.6f),
                StructureState.Damaged => new Color(0.85f, 0.8f, 0.75f),
                StructureState.Ruined => new Color(0.6f, 0.58f, 0.55f, 0.85f),
                StructureState.Burned => new Color(0.35f, 0.3f, 0.3f, 0.9f),
                _ => Colors.White,
            };
            if (band != null && band.PlagueUntil > sim.Day) modulate *= new Color(0.85f, 1f, 0.7f);
            into.Add(new WorldSprite(foot, v.Texture, v.Source, v.Pivot, Sprites.PixelSize(density), modulate, v.Shadow));
            if (s.State == StructureState.Building && Sprites.Make("civilisation.scaffold", null, density, foot + new Vector2(0, 0.01f), Colors.White, shadow: 0f) is { } scaffold)
                into.Add(scaffold);
        }
    }

    readonly record struct Look(Texture2D Texture, Rect2 Source, Vector2 Pivot, float Shadow);

    static Look Whole(Composite c, float shadow) => new(c.Texture, new Rect2(Vector2.Zero, c.Texture.GetSize()), c.Pivot, shadow);

    static Look? From(WorldSprite? s) => s is { } x ? new Look(x.Texture, x.Source, x.Pivot, x.Shadow) : null;

    static Look? Visual(Simulation sim, Structure s, Band? band, int d, double now, int selectedBand)
    {
        var k = s.Kind;
        var w = sim.World;
        int frame = (int)(now * 6.0);
        float shadow = k.W * Main.CellPx * 0.42f;
        var style = Settlements.StyleOf(w.Biome[s.Cell]);
        bool ruined = s.State is StructureState.Ruined or StructureState.Burned;
        bool roofed = !ruined && !(selectedBand >= 0 && s.BandId == selectedBand && d >= 64);
        Look? Building(Settlements.Style st, int cw, int ch) => House(st, cw, ch, d, roofed) is { } h ? Whole(h, shadow) : null;
        switch (k.Id)
        {
            case "windbreak": return Fence("reed", 1, d) is { } wb ? Whole(wb, shadow) : null;
            case "lean_to": return Fence("wattle", 2, d) is { } lt ? Whole(lt, shadow) : null;
            case "hide_tent": return Building(new Settlements.Style("hide", "hide", "earth"), 2, 2);
            case "hut" or "pit_house": return Building(style with { Roof = style.Roof is "reed" or "thatch" or "hide" or "bark" ? style.Roof : "thatch" }, 2, 2);
            case "house": return Building(style with { Roof = style.Roof == "tile" ? "wood_shingle" : style.Roof }, 3, 2);
            case "mudbrick_house": return Building(new Settlements.Style(w.TempMean[s.Cell] > 18f ? "adobe" : "mudbrick", style.Roof == "slate" ? "tile" : style.Roof, "packed_clay"), 3, 2);
            case "stone_house": return Building(new Settlements.Style(style.Wall is "granite" or "basalt" or "sandstone" or "limestone" ? style.Wall : "stone", "slate", "flagstone"), 3, 2);
            case "brick_house": return Building(new Settlements.Style("fired_brick", "tile", "ceramic_tile"), 3, 2);
            case "housing_block": return Building(new Settlements.Style("concrete", "metal", "concrete"), 4, 3);
            case "pressure_habitat": return Building(new Settlements.Style("composite", "glass", "metal_deck"), 3, 3);
            case "animal_pen": return Pen(d) is { } pen ? Whole(pen, 0f) : null;
            case "palisade": return Palisade(sim, s, d) is { } pal ? Whole(pal, 0f) : null;
            case "hearth":
                bool lit = band is { HasFireToday: true } && !ruined;
                return From(Sprites.Make(lit ? "civilisation.hearth" : "civilisation.hearth_unlit", null, d, Vector2.Zero, Colors.White, lit ? frame : 0, shadow: 0f));
            case "kiln" or "forge" or "oven":
                bool fired = band is { HasFireToday: true } && !ruined;
                return From(Sprites.Make($"civilisation.{k.Id}{(fired ? "" : "_unlit")}", null, d, Vector2.Zero, Colors.White, fired ? frame : 0));
            case "basket" or "crate" or "chest" or "grain_bin":
                if (band != null && Stocked(k.Id, StoredGood(band), Math.Clamp((int)MathF.Ceiling(band.FoodPool / Math.Max(1, band.Members.Count) / 3f), 0, 9), d) is { } store)
                    return Whole(store, Main.CellPx * 0.35f);
                break;
            case "totem":
                return From(Sprites.Make(null, $"structures.totem_{EarlyState(s)}", d, Vector2.Zero, Colors.White,
                                         cultureRow: band != null ? sim.Cultures[band.CultureId].Hue % 12 : 0));
        }

        if (Art.Get($"structures.{k.Id}_{EarlyState(s)}") != null)
            return From(Sprites.Make(null, $"structures.{k.Id}_{EarlyState(s)}", d, Vector2.Zero, Colors.White));
        return From(Sprites.Make("civilisation." + k.Id, null, d, Vector2.Zero, Colors.White, frame));
    }

    static string EarlyState(Structure s) => s.State switch
    {
        StructureState.Building => "building", StructureState.Damaged => "damaged", StructureState.Ruined => "ruined", StructureState.Burned => "burned", _ => "complete",
    };

    static string StoredGood(Band band)
    {
        string best = "food";
        float most = 0f;
        foreach (var (good, qty) in band.Stores.Held())
            if (good.Kind == GoodKind.Food && qty > most && Art.Lod($"civilisation.stored_{good.Id}", 16) != null) { most = qty; best = good.Id; }
        return best;
    }

    public static void AddPeople(Simulation sim, Band band, Vector2 camp, int density, double now, List<WorldSprite> into)
    {
        var w = sim.World;
        var tint = band.PlagueUntil > sim.Day ? new Color(0.85f, 1f, 0.7f) : Colors.White;
        int hue = sim.Cultures[band.CultureId].Hue;
        float temp = w.Temperature(band.Cell, sim.Day);
        var homes = new List<Vector2>();
        foreach (int sid in band.Structures)
            if (sim.Structures.TryGetValue(sid, out var s) && s.Standing && s.Kind.Housing > 0)
            {
                var corner = CellPos(sim, s.Cell, camp);
                for (int k = 0; k < Math.Max(1, s.Kind.Housing / 3); k++) homes.Add(corner + new Vector2(s.Kind.W * 0.5f, s.Kind.H + 0.3f) * Main.CellPx);
            }
        int placed = 0;
        foreach (int id in band.Members)
        {
            if (!sim.Living.TryGetValue(id, out var agent)) continue;
            uint h = TileBake.Hash(id, 9);
            bool child = agent.IsChild(sim.Day);
            float angle = (h & 0xFFFF) / 65535f * Mathf.Tau;
            var home = homes.Count > 0 ? homes[placed % homes.Count] + new Vector2((h >> 16) % 9 - 4f, (h >> 20) % 5) * Unit
                                       : camp + new Vector2(MathF.Cos(angle) * 12f, MathF.Sin(angle) * 8f + 2f) * Unit;
            placed++;
            var site = agent.Site >= 0 ? CellPos(sim, agent.Site, camp) + new Vector2(0.5f + ((h >> 4) % 7 - 3) / 16f, 0.75f) * Main.CellPx : home;
            var activity = agent.Today;
            double period = 18.0 + h % 7;
            double phase = (now / period + (h & 0xFF) / 255.0) % 1.0;
            Vector2 at, velocity = Vector2.Zero;
            bool working = true;
            bool away = site.DistanceSquaredTo(home) > 4f;
            if (agent.Sick) at = home;
            else if (!away) at = site;
            else if (phase < 0.12) { at = home.Lerp(site, (float)(phase / 0.12)); velocity = site - home; }
            else if (phase < 0.75) at = site;
            else if (phase < 0.87) { at = site.Lerp(home, (float)((phase - 0.75) / 0.12)); velocity = home - site; }
            else { at = home; working = false; }
            bool playing = child && activity == Activity.Play;
            if (playing)
            {
                float a = (float)(now * 0.9 + (h & 0xFF) / 40.0);
                at = camp + new Vector2(MathF.Cos(a) * 14f, MathF.Sin(a) * 9f + 4f) * Unit;
                velocity = new Vector2(-MathF.Sin(a), MathF.Cos(a) * 0.6f);
            }
            bool moving = velocity != Vector2.Zero && !playing;
            string anim = playing ? "run" : People.Animation(working ? activity : Activity.Rest, moving, child, agent.Sick);
            string dir = "down";
            bool flip = false;
            if (velocity != Vector2.Zero)
            {
                if (MathF.Abs(velocity.X) > MathF.Abs(velocity.Y) * 0.8f) { dir = "side"; flip = velocity.X < 0; }
                else dir = velocity.Y > 0 ? "down" : "up";
            }
            else if (anim is "craft" or "talk" or "attack" or "sleep" or "chop" or "dig" or "build") { dir = "side"; flip = (h & 1) == 0; }

            var look = People.LookOf(agent, sim.Day, hue, temp, id == band.LeaderId, working ? activity : Activity.Rest);
            if (People.Get(look, anim, dir, density) is not { } strip) return;
            int f = (int)(now * strip.Fps + (h & 0xF) * 0.37) % strip.Frames;
            into.Add(new WorldSprite(at, strip.Texture, new Rect2(f * strip.FrameW, 0, strip.FrameW, strip.FrameH), strip.Pivot + new Vector2(0, 1),
                                     Sprites.PixelSize(density), tint, 3.5f * Unit, flip));
        }
    }

    public sealed record Composite(ImageTexture Texture, Vector2 Pivot);

    static readonly Dictionary<string, Composite?> Composites = new();

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
            void Stamp(Sheet s, int ox, int oy, Rect2I? clip = null)
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
                        img[o] = (byte)(c.R * 255); img[o + 1] = (byte)(c.G * 255); img[o + 2] = (byte)(c.B * 255); img[o + 3] = 255;
                    }
            }
            var inside = new Rect2I(d / 2, d / 2, width - d, height - d);
            for (int y = 0; y < ch; y++) for (int x = 0; x < cw; x++) Stamp(floor, x * d, y * d, inside);
            for (int y = 0; y < ch; y++)
                for (int x = 0; x < cw; x++)
                {
                    bool side = x == 0 || x == cw - 1, end = y == 0 || y == ch - 1;
                    if (!side && !end) continue;
                    if (!roofed && y == ch - 1) continue;
                    int mask = (side && y > 0 ? 1 : 0) | (end && x < cw - 1 ? 2 : 0) | (side && y < ch - 1 ? 4 : 0) | (end && x > 0 ? 8 : 0);
                    if (Art.Lod($"civilisation.wall_{wall}_{mask:00}", d) is { } piece) Stamp(piece, x * d, y * d);
                }
            if (roofed)
            {
                if (Art.Lod("civilisation.door_wood_closed", d) is { } door) Stamp(door, cw / 2 * d, (ch - 1) * d);

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
                if (Art.Lod("civilisation.hearth", d) is { } hearth) Stamp(hearth, d / 2, d / 2);
                if (Art.Lod("civilisation.bed", d) is { } bed && cw >= 3) Stamp(bed, (cw - 2) * d + d / 2, d / 2);
                if (Art.Lod("civilisation.pot", d) is { } pot) Stamp(pot, (cw - 1) * d - d / 2, d / 2);
            }
            var tex = ImageTexture.CreateFromImage(Image.CreateFromData(width, height, false, Image.Format.Rgba8, img));
            result = new Composite(tex, new Vector2(width / 2f, height));
        }
        Composites[key] = result;
        return result;
    }

    public static Composite? Fence(string wall, int cells, int d)
    {
        string key = $"fence|{wall}|{cells}|{d}";
        if (Composites.TryGetValue(key, out var cached)) return cached;
        Composite? result = null;
        var image = Image.CreateEmpty(cells * d, d, false, Image.Format.Rgba8);
        bool any = false;
        for (int x = 0; x < cells; x++)
        {
            int mask = cells == 1 ? 10 : (x < cells - 1 ? 2 : 0) | (x > 0 ? 8 : 0);
            if (Art.Lod($"civilisation.wall_{wall}_{mask:00}", d) is not { } piece) continue;
            image.BlendRect(piece.Reference, new Rect2I(0, 0, piece.FrameW, piece.FrameH), new Vector2I(x * d, 0));
            any = true;
        }
        if (any) result = new Composite(ImageTexture.CreateFromImage(image), new Vector2(cells * d / 2f, d));
        Composites[key] = result;
        return result;
    }

    static Composite? Pen(int d)
    {
        string key = $"pen|{d}";
        if (Composites.TryGetValue(key, out var cached)) return cached;
        var image = Image.CreateEmpty(2 * d, 2 * d, false, Image.Format.Rgba8);
        int[] masks = { 6, 12, 3, 9 };
        for (int k = 0; k < 4; k++)
            if (Art.Lod($"civilisation.wall_wattle_{masks[k]:00}", d) is { } piece)
                image.BlendRect(piece.Reference, new Rect2I(0, 0, piece.FrameW, piece.FrameH), new Vector2I(k % 2 * d, k / 2 * d));
        if (Art.Lod("civilisation.feed_trough", d) is { } trough)
            image.BlendRect(trough.Reference, new Rect2I(0, 0, trough.FrameW, trough.FrameH), new Vector2I(d / 2, d / 2));
        var result = new Composite(ImageTexture.CreateFromImage(image), new Vector2(d, 2 * d));
        Composites[key] = result;
        return result;
    }

    static Composite? Palisade(Simulation sim, Structure s, int d)
    {
        var w = sim.World;
        int mask = 0;
        int[] dx = { 0, 1, 0, -1 }, dy = { -1, 0, 1, 0 };
        for (int k = 0; k < 4; k++)
        {
            int y = w.Y(s.Cell) + dy[k];
            if (y < 0 || y >= w.H) continue;
            int id = w.StructureAt[w.Index(w.X(s.Cell) + dx[k], y)];
            if (id != 0 && sim.Structures.TryGetValue(id, out var n) && n.Kind.Id == "palisade") mask |= 1 << k;
        }
        string key = $"palisade|{mask}|{d}";
        if (Composites.TryGetValue(key, out var cached)) return cached;
        Composite? result = null;
        if (Art.Lod($"civilisation.wall_palisade_{mask:00}", d) is { } piece)
            result = new Composite(ImageTexture.CreateFromImage(piece.Reference.GetRegion(new Rect2I(0, 0, piece.FrameW, piece.FrameH))), new Vector2(piece.PivotX, piece.PivotY + 1));
        Composites[key] = result;
        return result;
    }

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
