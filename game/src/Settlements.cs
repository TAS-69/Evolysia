using Evolysia.Sim;
using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Evolysia.Sim.Society;
using Evolysia.Sim.World;

namespace Evolysia.Game;

/// <summary>
/// What a band's home looks like on the ground, derived from the simulation: which cells farming
/// villages till, what grows there, and the regional building style (civilisation
/// regional_styles.json) of the land a band lives on.
/// </summary>
public static class Settlements
{
    /// <summary>The bake input: the world plus every farmed cell and its crop.</summary>
    public static TileBake.Input BakeInput(Simulation sim, IReadOnlyDictionary<int, ulong>? knows = null)
    {
        var w = sim.World;
        var fields = new Dictionary<int, string>();
        foreach (var band in sim.Bands.Values.OrderBy(b => b.Id))
        {
            if (!band.Sedentary) continue;
            ulong k = knows?.GetValueOrDefault(band.Id) ?? Knows(sim, band);
            if ((k & Techs.Bit(TechId.Agriculture)) == 0) continue;
            int wanted = Math.Clamp(band.Members.Count / 3, 2, 16);
            int x0 = w.X(band.Cell), y0 = w.Y(band.Cell);
            // The ring two cells out: the village itself fills its cell and the one around it.
            foreach (var (dx, dy) in Ring(2).OrderBy(r => TileBake.Hash(band.Id, r.dx * 7 + r.dy * 31)))
            {
                if (wanted == 0) break;
                int y = y0 + dy;
                if (y < 0 || y >= w.H) continue;
                int i = w.Index(x0 + dx, y);
                if (!w.IsDryLand(i) || w.Volcano[i] || w.Biome[i] is Biome.IceSheet or Biome.Alpine or Biome.Wetland) continue;
                fields[i] = Crop(w, i, k);
                wanted--;
            }
        }
        return new TileBake.Input(w, fields);
    }

    /// <summary>Everything a band's living members know between them.</summary>
    public static ulong Knows(Simulation sim, Band band)
    {
        ulong k = 0;
        foreach (int id in band.Members) if (sim.Living.TryGetValue(id, out var a)) k |= a.Knows;
        return k;
    }

    static IEnumerable<(int dx, int dy)> Ring(int r)
    {
        for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == r) yield return (dx, dy);
    }

    /// <summary>What a field grows: grain in temperate lands, tubers and gourds in the tropics, some fibre where people weave.</summary>
    static string Crop(WorldMap w, int i, ulong knows)
    {
        uint h = TileBake.Hash(i, 77);
        if ((knows & Techs.Bit(TechId.Weaving)) != 0 && h % 5 == 0) return "fiber";
        return w.Biome[i] switch
        {
            Biome.TropicalRainforest => h % 3 == 0 ? "gourd" : "tuber",
            Biome.TropicalSeasonalForest or Biome.Savanna => h % 2 == 0 ? "gourd" : "grain",
            _ => "grain",
        };
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
