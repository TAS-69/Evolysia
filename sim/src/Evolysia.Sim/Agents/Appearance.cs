namespace Evolysia.Sim.Agents;

public sealed record GarmentStyle(string? Part, int Main, int Trim);
public sealed class Appearance
{
    [Persistence.SaveOptional(-1)] public int Build = -1;
    [Persistence.SaveOptional] public readonly Dictionary<string, int> Features = new();
    public string HairStyle = "";
    public int Skin = -1, Hair = -1;
    public readonly Dictionary<string, GarmentStyle> Garments = new();
}
