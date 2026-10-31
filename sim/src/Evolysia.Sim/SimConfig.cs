namespace Evolysia.Sim;

public enum PlanetType : byte { Terra, Ignis, Cryos, Thalassia }

public sealed class SimConfig
{
    public ulong Seed = 42;

    public int Width = 800;
    public int Height = 400;
    public PlanetType Planet = PlanetType.Terra;

    public float LandFraction = float.NaN;
    public float AxialTiltDegrees = 23.5f;

    public const int DaysPerYear = 360;
    public int EcologyStepDays = 5;

    public int Tribes = 3;
    public int StartBands = 2;
    public int StartBandSize = 25;

    public int FirstMessageYear = 250;

    public int[] TribeCells = Array.Empty<int>();

    public float Land => !float.IsNaN(LandFraction) ? LandFraction : Planet switch
    {
        PlanetType.Ignis => 0.42f,
        PlanetType.Cryos => 0.36f,
        PlanetType.Thalassia => 0.07f,
        _ => 0.34f,
    };

    public int SoftMaxAgents = 200000;

    public int Threads = Environment.ProcessorCount;

    public float InventionRate = 1f;
    public float DisasterRate = 1f;
    public float PredatorDanger = 1f;

    public float CapacityScale = 1f;
}
