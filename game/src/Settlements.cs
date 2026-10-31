using Evolysia.Sim;
using Evolysia.Sim.Core;
using Evolysia.Sim.Economy;

namespace Evolysia.Game;

/// <summary>
/// What the simulation has done to the land, for the tile art: fields and their crops, the cells
/// structures stand on, felled woods and quarried boulders. Also the regional building styles
/// (civilisation regional_styles.json) that dwellings are drawn in.
/// </summary>
public static class Settlements
{
    /// <summary>
    /// The bake input, snapshotted from the simulation (bakes run on other threads while it
    /// steps): every field (crop, growth, orchard), every cell a structure stands on, and what is
    /// left of each cell's timber and stone (0..255).
    /// </summary>
    public static TileBake.Input BakeInput(Simulation sim)
    {
        var w = sim.World;
        var fields = new Dictionary<int, (string, float, bool)>();
        var occupied = new HashSet<int>();
        foreach (var s in sim.Structures.Values)
        {
            var k = s.Kind;
            if (k.Role == StructureRole.Field)
            {
                if (s.State != StructureState.Planned) fields[s.Cell] = (s.Crop, s.Growth, k.Id == "orchard");
                continue;
            }
            for (int y = 0; y < k.H; y++)
                for (int x = 0; x < k.W; x++)
                    if (w.Y(s.Cell) + y < w.H) occupied.Add(w.Index(w.X(s.Cell) + x, w.Y(s.Cell) + y));
        }
        var trees = new byte[w.Count];
        var rocks = new byte[w.Count];
        for (int i = 0; i < w.Count; i++)
        {
            trees[i] = w.TreesK[i] <= 0f ? (byte)255 : (byte)Math.Clamp(w.Trees[i] / w.TreesK[i] * 255f + 0.5f, 0f, 255f);
            rocks[i] = w.RockK[i] <= 0f ? (byte)255 : (byte)Math.Clamp(w.Rock[i] / w.RockK[i] * 255f + 0.5f, 0f, 255f);
        }
        return new TileBake.Input(w, fields, occupied, trees, rocks);
    }

    /// <summary>A regional building style: wall, roof and floor materials (civilisation asset ids).</summary>
    public readonly record struct Style(string Wall, string Roof, string Floor);

    static Dictionary<string, Style>? _styles;

    public static Style StyleOf(Biome biome)
    {
        _styles ??= LoadStyles();
        return _styles.TryGetValue(Ids.Snake(biome), out var s) ? s : new Style("wattle", "thatch", "earth");
    }

    static Dictionary<string, Style> LoadStyles()
    {
        var styles = new Dictionary<string, Style>();
        if (Art.Json("civilisation/regional_styles.json") is not { } json) return styles;
        foreach (var style in json.GetProperty("styles").EnumerateObject())
        {
            var s = new Style(style.Value.GetProperty("wall").GetString()!, style.Value.GetProperty("roof").GetString()!, style.Value.GetProperty("floor").GetString()!);
            foreach (var b in style.Value.GetProperty("biomes").EnumerateArray()) styles[b.GetString()!] = s;
        }
        return styles;
    }
}
