using Evolysia.Sim;
using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
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

    public static void Add(Simulation sim, IReadOnlyDictionary<int, ulong> knows, Rect2 view, int density, double now, List<WorldSprite> into)
    {
        var w = sim.World;
        if (density == 16 && view.Size.X > 120 * Main.CellPx) return;   // too far out to see animals
        int x0 = (int)MathF.Floor(view.Position.X / Main.CellPx) - 1, x1 = (int)MathF.Ceiling(view.End.X / Main.CellPx) + 1;
        int y0 = Math.Max(0, (int)MathF.Floor(view.Position.Y / Main.CellPx) - 1), y1 = Math.Min(w.H - 1, (int)MathF.Ceiling(view.End.Y / Main.CellPx) + 1);
        // Wild animals keep away from people: no herds within a cell of a camp.
        var peopled = new HashSet<int>();
        foreach (var band in sim.Bands.Values)
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (w.Y(band.Cell) + dy is var ny && ny >= 0 && ny < w.H) peopled.Add(w.Index(w.X(band.Cell) + dx, ny));
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int i = w.Index(x, y);
                var centre = new Vector2(x + 0.5f, y + 0.5f) * Main.CellPx;   // unwrapped: x may run off the planet's edge
                uint h = TileBake.Hash(i, 41);
                if (w.IsDryLand(i) && !peopled.Contains(i))
                {
                    float game = w.PreyK[i] > 0f ? w.PreyStock[i] / w.PreyK[i] : 0f;
                    if ((h & 0xFF) < game * 22f && Prey(w.Biome[i], h >> 8) is { } prey)
                        Herd(prey, centre, i, 1 + (int)(h >> 16) % 3, density, now, into);
                    if (w.Predators[i] > 0.45f && (h >> 12 & 0xFF) < w.Predators[i] * 20f && Predator(w.Biome[i]) is { } predator)
                        Herd(predator, centre, i + 7919, 1, density, now, into);
                }
                else if (w.Biome[i] is Biome.Coast or Biome.Lake or Biome.Ocean && w.FishK[i] > 0f && (h & 0xFF) < w.FishStock[i] / w.FishK[i] * 25f)
                    Fish(centre, i, density, now, into);
            }
        foreach (var band in sim.Bands.Values)
        {
            ulong k = knows.GetValueOrDefault(band.Id);
            if ((k & Techs.Bit(TechId.Domestication)) == 0) continue;
            var pos = new Vector2(w.X(band.Cell) + 0.5f, w.Y(band.Cell) + 0.5f) * Main.CellPx;
            pos.X += MathF.Round((view.GetCenter().X - pos.X) / (w.W * Main.CellPx)) * w.W * Main.CellPx;
            if (!view.Grow(Main.CellPx * 4).HasPoint(pos)) continue;
            Domestic(band.Id, band.Members.Count, w.Biome[band.Cell], pos + new Vector2(22f, 4f) * Unit * (band.Sedentary ? 2f : 1f), density, now, into);
        }
    }

    /// <summary>
    /// A small herd wandering in its cell: the group drifts slowly round a loop, each animal
    /// grazing or walking after it.
    /// </summary>
    static void Herd(Species s, Vector2 centre, int seed, int count, int density, double now, List<WorldSprite> into)
    {
        uint h = TileBake.Hash(seed, 3);
        double t = now * 0.05 + (h & 0xFFF) / 300.0;
        var drift = new Vector2(MathF.Cos((float)t), MathF.Sin((float)(t * 1.3))) * 4.5f * Unit;
        var velocity = new Vector2(-MathF.Sin((float)t), 1.3f * MathF.Cos((float)(t * 1.3)));
        bool moving = MathF.Sin((float)(t * 2.1)) > (s.Grazes ? 0.35f : -0.2f);   // herds mostly graze; predators prowl
        for (int k = 0; k < count; k++)
        {
            uint a = TileBake.Hash(seed, k * 5 + 1);
            var at = centre + drift + new Vector2((a & 0xF) - 7.5f, (a >> 4 & 0xF) - 7.5f) * 0.7f * Unit;
            Animal(s, at, moving, velocity.X < 0, density, now + (a & 0xFF) / 37.0, into, -1);
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
    static void Domestic(int bandId, int people, Biome biome, Vector2 pen, int density, double now, List<WorldSprite> into)
    {
        var herd = biome switch
        {
            Biome.Alpine or Biome.Shrubland => new Species("goat_domestic", "animal_goat"),
            Biome.TemperateGrassland or Biome.Savanna => new Species("cattle", "animal_cattle"),
            Biome.HotDesert or Biome.ColdDesert => new Species("camel", "animal_camel"),
            Biome.Tundra or Biome.BorealForest => new Species("horse_domestic", "animal_horse"),
            _ => new Species("sheep_domestic", "animal_sheep"),
        };
        int coat = (int)(TileBake.Hash(bandId, 2) % 8);
        int count = Math.Clamp(people / 6, 2, 6);
        for (int k = 0; k < count; k++)
        {
            uint a = TileBake.Hash(bandId, k * 11 + 4);
            float t = (float)(now * 0.12 + (a & 0xFF) / 30.0);
            var at = pen + new Vector2(MathF.Cos(t) * 5f + (a & 7) - 3.5f, MathF.Sin(t * 0.7f) * 3f + (a >> 3 & 7) - 3.5f) * Unit;
            Animal(herd, at, MathF.Sin(t * 1.7f) > 0.5f, MathF.Sin(t) > 0, density, now + k * 0.31, into, coat);
        }
        Animal(new Species("dog", "animal_dog", false), pen + new Vector2(-14f, 6f) * Unit, false, false, density, now, into, (coat + 3) % 8);
        Animal(new Species("fowl_domestic", "animal_fowl"), pen + new Vector2(-10f, -6f) * Unit, false, true, density, now, into, coat);
    }

    /// <summary>One animal: animated at 16 px (walk / eat / idle), the static detail art at 32 / 64 px.</summary>
    static void Animal(Species s, Vector2 at, bool moving, bool left, int density, double now, List<WorldSprite> into, int coat)
    {
        if (density > 16 && Art.Lod("civilisation." + s.Detail, density) != null)
        {
            // Detail animals have one pose: a small step up and down while walking.
            float bob = moving && (int)(now * 6) % 2 == 0 ? -1f * Sprites.PixelSize(density) * density / 32f : 0f;
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
