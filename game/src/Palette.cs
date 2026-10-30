using Evolysia.Sim.Core;
using Godot;

namespace Evolysia.Game;

/// <summary>
/// Placeholder colours until the Codex asset pipeline delivers the master palette
/// (docs/art/ART_DIRECTION.md section 2.1). UI colours are already the final ones from section 11.1.
/// </summary>
public static class Palette
{
    public static readonly Color Bg = new("1e1813");
    public static readonly Color Panel = new("2a211a");
    public static readonly Color Raised = new("3a2e24");
    public static readonly Color Border = new("5a4636");
    public static readonly Color Bone = new("f1e6d9");
    public static readonly Color Parchment = new("d4c3b1");
    public static readonly Color Ochre = new("c8913a");
    public static readonly Color Moss = new("6f8f4e");
    public static readonly Color Terracotta = new("b5553c");
    public static readonly Color River = new("4f86a8");
    public static readonly Color Divine = new("f2d27a");

    public static Color Biome(Biome b) => b switch
    {
        Sim.Core.Biome.DeepOcean => new("1b2f52"),
        Sim.Core.Biome.Ocean => new("24406b"),
        Sim.Core.Biome.Coast => new("33628a"),
        Sim.Core.Biome.Lake => new("3f75a0"),
        Sim.Core.Biome.IceSheet => new("e9edf0"),
        Sim.Core.Biome.Tundra => new("a7a88f"),
        Sim.Core.Biome.BorealForest => new("3f6650"),
        Sim.Core.Biome.ColdDesert => new("b4a88a"),
        Sim.Core.Biome.TemperateGrassland => new("9fb168"),
        Sim.Core.Biome.TemperateForest => new("4f7f45"),
        Sim.Core.Biome.TemperateRainforest => new("2f6a4a"),
        Sim.Core.Biome.Shrubland => new("a69a66"),
        Sim.Core.Biome.HotDesert => new("dcc28a"),
        Sim.Core.Biome.Savanna => new("bba45c"),
        Sim.Core.Biome.TropicalSeasonalForest => new("6f9443"),
        Sim.Core.Biome.TropicalRainforest => new("2e7a3c"),
        Sim.Core.Biome.Wetland => new("547a66"),
        Sim.Core.Biome.Alpine => new("8f857e"),
        Sim.Core.Biome.Volcanic => new("5a3a35"),
        _ => new("ff00ff"),
    };

    /// <summary>One of 12 culture hues (lut_culture rows in the art spec).</summary>
    public static Color Culture(int hue) => Color.FromHsv(hue / 12f, 0.62f, 0.95f);

    public static string Hex(Color c) => "#" + c.ToHtml(false);
}
