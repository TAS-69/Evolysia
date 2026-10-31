using Evolysia.Sim;
using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Evolysia.Sim.Society;
using Evolysia.Sim.World;
using Godot;

namespace Evolysia.Game;

/// <summary>
/// The animals on screen, read from the ecology: herds where a cell's game is plentiful, lone
/// predators where danger is high, fish schools in rich water, and the flocks and herds of bands
/// that have domesticated animals. At 16 px they are the animated fauna family (walking,
/// grazing); at 32 / 64 px the civilisation detail animals, standing still while they graze and
/// stepping when they move. Placement is a pure function of the cell and the clock, so nothing is
/// stored and every density shows the same animals in the same places.
/// </summary>
public static class Wildlife
{
    const float Unit = Main.CellPx / 16f;

    /// <summary>A species: its 16 px fauna form (e.g. "deer", "sheep_domestic") and its detail art id.</summary>
    readonly record struct Species(string Form, string Detail, bool Grazes = true);

    static Species? Prey(Biome b, uint h) => b switch
    {
        Biome.BorealForest => h % 3 == 0 ? new("elk", "animal_elk") : new("deer", "animal_deer"),
        Biome.TemperateForest or Biome.TemperateRainforest => h % 4 == 0 ? new("boar", "animal_pig") : h % 5 == 0 ? new("rabbit", "animal_rabbit") : new("deer", "animal_deer"),
        Biome.TemperateGrassland => h % 3 == 0 ? new("horse", "animal_horse") : h % 4 == 0 ? new("rabbit", "animal_rabbit") : new("bison", "animal_bison"),
        Biome.Shrubland => h % 2 == 0 ? new("goat", "animal_goat") : new("rabbit", "animal_rabbit"),
        Biome.Savanna => h % 3 == 0 ? new("horse", "animal_horse") : new("bison", "animal_bison"),
        Biome.HotDesert or Biome.ColdDesert => new("camel", "animal_camel"),
        Biome.Tundra => new("elk", "animal_elk"),
        Biome.Alpine => new("goat", "animal_goat"),
        Biome.TropicalSeasonalForest or Biome.TropicalRainforest => h % 2 == 0 ? new("boar", "animal_pig") : new("deer", "animal_deer"),
        Biome.Wetland => h % 2 == 0 ? new("fowl", "animal_duck") : new("deer", "animal_deer"),
        _ => null,
    };

    static Species? Predator(Biome b) => b switch
    {
        Biome.BorealForest or Biome.TemperateForest or Biome.TemperateRainforest or Biome.Alpine => new("bear", "animal_bear", false),
        Biome.Tundra or Biome.TemperateGrassland or Biome.Shrubland or Biome.ColdDesert => new("wolf", "animal_wolf", false),
        Biome.Savanna or Biome.TropicalSeasonalForest or Biome.TropicalRainforest => new("big_cat", "animal_big_cat", false),
        Biome.Wetland => new("crocodile", "animal_crocodile", false),
        Biome.HotDesert => new("snake", "animal_snake", false),
        _ => null,
    };

    public static void Add(Simulation sim, Rect2 view, int density, double now, List<WorldSprite> into)
    {
        var w = sim.World;
        if (density == 16 && view.Size.X > 120 * Main.CellPx) return;   // too far out to see animals
        int x0 = (int)MathF.Floor(view.Position.X / Main.CellPx) - 1, x1 = (int)MathF.Ceiling(view.End.X / Main.CellPx) + 1;
        int y0 = Math.Max(0, (int)MathF.Floor(view.Position.Y / Main.CellPx) - 1), y1 = Math.Min(w.H - 1, (int)MathF.Ceiling(view.End.Y / Main.CellPx) + 1);
        // Wild animals keep away from people: none within a camp's reach (a village's further).
        var peopled = new HashSet<int>();
        foreach (var band in sim.Bands.Values)
        {
            int r = band.Sedentary ? 7 : 3;
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                    if (w.Y(band.Cell) + dy is var ny && ny >= 0 && ny < w.H) peopled.Add(w.Index(w.X(band.Cell) + dx, ny));
        }
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int i = w.Index(x, y);
                var centre = new Vector2(x + 0.5f, y + 0.5f) * Main.CellPx;   // unwrapped: x may run off the planet's edge
                uint h = TileBake.Hash(i, 41);
                if (w.IsDryLand(i) && !peopled.Contains(i) && Inland(w, i, water: false))
                {
                    float game = w.PreyK[i] > 0f ? w.PreyStock[i] / w.PreyK[i] : 0f;
                    if ((h & 0xFF) < game * 22f && Prey(w.Biome[i], h >> 8) is { } prey)
                        Herd(prey, centre, i, 1 + (int)(h >> 16) % 3, density, now, into);
                    if (w.Predators[i] > 0.45f && (h >> 12 & 0xFF) < w.Predators[i] * 20f && Predator(w.Biome[i]) is { } predator)
                        Herd(predator, centre, i + 7919, 1, density, now, into);
                }
                else if (w.Biome[i] is Biome.Coast or Biome.Lake or Biome.Ocean && Inland(w, i, water: true)
                         && w.FishK[i] > 0f && (h & 0xFF) < w.FishStock[i] / w.FishK[i] * 25f)
                    Fish(centre, i, density, now, into);
            }
        foreach (var band in sim.Bands.Values)
        {
            if (band.Herds.Count == 0) continue;
            // Round the band's pen if it has one, else beside camp.
            int penCell = band.Cell;
            foreach (int sid in band.Structures)
                if (sim.Structures.TryGetValue(sid, out var s) && s.Standing && s.Kind.Role == Evolysia.Sim.Economy.StructureRole.Pen) { penCell = s.Cell; break; }
            var pos = new Vector2(w.X(penCell) + 1f, w.Y(penCell) + 1f) * Main.CellPx;
            pos.X += MathF.Round((view.GetCenter().X - pos.X) / (w.W * Main.CellPx)) * w.W * Main.CellPx;
            if (!view.Grow(Main.CellPx * 6).HasPoint(pos)) continue;
            Domestic(band, penCell == band.Cell ? pos + new Vector2(24f, 6f) * Unit : pos, density, now, into);
        }
    }

    /// <summary>
    /// Whether every neighbour of a cell is water (or dry land): animals wander beyond their own cell
    /// and the shore art blends across cell edges, so fish keep a cell off the shore and herds off the water.
    /// </summary>
    static bool Inland(WorldMap w, int i, bool water)
    {
        int x = w.X(i), y = w.Y(i);
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (y + dy < 0 || y + dy >= w.H) continue;
                int n = w.Index(x + dx, y + dy);
                if (w.IsWater(n) != water) return false;
            }
        return true;
    }

    /// <summary>Where a wandering animal is: stopped at a spot, or walking on to the next.</summary>
    readonly record struct Step(Vector2 At, bool Moving, bool Left);

    /// <summary>
    /// Wandering as animals do: stand (graze, rest) at a spot for a while, then walk at a steady pace,
    /// easing in and out, to the next spot - a new random point within <paramref name="radius"/>
    /// (world units) of <paramref name="home"/> each leg. Pure in the clock, so it is smooth frame to frame.
    /// </summary>
    static Step Wander(uint seed, Vector2 home, float radius, double now, double period, double walkShare)
    {
        double t = now / period + (seed & 0xFFFF) / 65536.0;
        long leg = (long)Math.Floor(t);
        double f = t - leg;
        Vector2 Spot(long n)
        {
            uint h = TileBake.Hash((int)seed, (int)n);
            float a = (h & 0xFFFF) / 65535f * Mathf.Tau, r = MathF.Sqrt((h >> 16) / 65535f) * radius;
            return home + new Vector2(MathF.Cos(a) * r, MathF.Sin(a) * r * 0.7f);
        }
        var from = Spot(leg);
        var to = Spot(leg + 1);
        double still = 1.0 - walkShare;
        if (f < still) return new Step(from, false, from.X < Spot(leg - 1).X);   // facing the way it came
        float u = (float)((f - still) / walkShare);
        u = u * u * (3f - 2f * u);
        return new Step(from.Lerp(to, u), true, to.X < from.X);
    }

    /// <summary>
    /// A small herd: the group wanders its cell together (each animal keeps its place in the group,
    /// with a little wander of its own), mostly grazing; predators spend longer on the move.
    /// </summary>
    static void Herd(Species s, Vector2 centre, int seed, int count, int density, double now, List<WorldSprite> into)
    {
        uint h = TileBake.Hash(seed, 3);
        var group = Wander(h, centre, 5f * Unit, now, s.Grazes ? 11.0 : 7.0, s.Grazes ? 0.3 : 0.55);
        for (int k = 0; k < count; k++)
        {
            uint a = TileBake.Hash(seed, k * 5 + 1);
            var place = new Vector2((a & 0xF) - 7.5f, (a >> 4 & 0xF) - 7.5f) * 0.6f * Unit;
            var own = Wander(a, Vector2.Zero, 1.5f * Unit, now, 6.0 + (a >> 8) % 4, 0.25);
            bool moving = group.Moving || own.Moving;
            Animal(s, group.At + place + own.At, moving, group.Moving ? group.Left : own.Left, density, now + (a & 0xFF) / 37.0, into, -1);
        }
    }

    static void Fish(Vector2 centre, int seed, int density, double now, List<WorldSprite> into)
    {
        uint h = TileBake.Hash(seed, 13);
        float a = (float)(now * 0.3 + (h & 0xFF) / 40.0);
        var at = centre + new Vector2(MathF.Cos(a), MathF.Sin(a) * 0.6f) * 4f * Unit;
        bool left = MathF.Sin(a) > 0;
        if (density == 16 || Art.Lod("civilisation.animal_fish", density) == null)
        {
            if (Sprites.Make(null, "fauna.fish_school_swim", density, at, Colors.White, (int)(now * 4) % 4, shadow: 0f, flip: left) is { } s) into.Add(s);
        }
        else if (Sprites.Make("civilisation.animal_fish", null, density, at + new Vector2(0, MathF.Sin((float)now * 2f) * 0.3f), new Color(1, 1, 1, 0.85f), shadow: 0f, flip: left) is { } s)
            into.Add(s);
    }

    /// <summary>A band's animals near home: dogs with every band that tames animals, then sheep, goats, cattle or horses and fowl.</summary>
    /// <summary>The fauna form and detail art of a domestic species (Band.Herds keys).</summary>
    static Species? Tame(string species) => species switch
    {
        "sheep" => new Species("sheep_domestic", "animal_sheep"),
        "goat" => new Species("goat_domestic", "animal_goat"),
        "cattle" => new Species("cattle", "animal_cattle"),
        "horse" => new Species("horse_domestic", "animal_horse"),
        "camel" => new Species("camel", "animal_camel"),
        "pig" => new Species("boar", "animal_pig"),
        "fowl" => new Species("fowl_domestic", "animal_fowl"),
        "dog" => new Species("dog", "animal_dog", false),
        _ => null,
    };

    /// <summary>A band's own animals, as many as it keeps (up to a dozen of each kind drawn), grazing round its pens or camp.</summary>
    static void Domestic(Band band, Vector2 pen, int density, double now, List<WorldSprite> into)
    {
        int coat = (int)(TileBake.Hash(band.Id, 2) % 8);
        int kind = 0;
        foreach (var (species, head) in band.Herds)
        {
            if (Tame(species) is not { } s) continue;
            int count = Math.Min(12, (int)MathF.Round(head));
            float roam = species == "dog" ? 12f : species == "fowl" ? 4f : 8f;
            var centre = pen + new Vector2(kind * 9f - 9f, kind % 2 * 6f) * Unit;
            for (int k = 0; k < count; k++)
            {
                uint a = TileBake.Hash(band.Id * 31 + kind, k * 11 + 4);
                var step = Wander(a, centre, roam * Unit, now, species == "dog" ? 5.0 : 9.0 + a % 5, species == "dog" ? 0.5 : 0.3);
                Animal(s, step.At, step.Moving, step.Left, density, now + k * 0.31, into, (coat + kind) % 8);
            }
            kind++;
        }
    }

    /// <summary>One animal: animated at 16 px (walk / eat / idle), the static detail art at 32 / 64 px.</summary>
    static void Animal(Species s, Vector2 at, bool moving, bool left, int density, double now, List<WorldSprite> into, int coat)
    {
        if (density > 16 && Art.Lod("civilisation." + s.Detail, density) != null)
        {
            // Detail animals have one pose: a gentle rise and fall with each stride while walking.
            float bob = moving ? -MathF.Abs(MathF.Sin((float)now * 9f)) * 0.35f * Unit : 0f;
            if (Sprites.Make("civilisation." + s.Detail, null, density, at + new Vector2(0, bob), Colors.White, flip: left) is { } d) into.Add(d);
            return;
        }
        string anim = moving ? (s.Form is "snake" ? "slither" : s.Form is "crocodile" ? "crawl" : "walk") : s.Grazes && Art.Get($"fauna.{s.Form}_eat") != null ? "eat" : "idle";
        if (Art.Get($"fauna.{s.Form}_{anim}") is not { } sheet) return;
        int frame = (int)(now * Math.Max(1, sheet.Int("fps", 4))) % Math.Max(1, sheet.Frames);
        if (Sprites.Make(null, $"fauna.{s.Form}_{anim}", density, at, Colors.White, frame, row: coat >= 0 ? coat % Math.Max(1, sheet.Rows) : 0, flip: left,
                         hairRow: coat >= 0 && sheet.Str("coat_lut") == "lut_hair" ? coat : -1) is { } sprite)
            into.Add(sprite);
    }
}
