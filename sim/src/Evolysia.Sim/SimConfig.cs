namespace Evolysia.Sim;

/// <summary>Everything tunable about a run. Defaults aim for a lively 500-year history.</summary>
public sealed class SimConfig
{
    public ulong Seed = 42;

    // World
    public int Width = 160;
    public int Height = 80;
    public float LandFraction = 0.34f;
    public float AxialTiltDegrees = 23.5f;

    // Time
    public const int DaysPerYear = 360;
    public int EcologyStepDays = 5;

    // Starting population
    public int StartBands = 4;
    public int StartBandSize = 25;

    // Safety valve for headless runs: fertility falls off above this many living agents.
    public int SoftMaxAgents = 20000;

    // Worker threads for band days (results are identical for any value).
    public int Threads = Environment.ProcessorCount;

    // Tuning multipliers
    public float InventionRate = 1f;
    public float DisasterRate = 1f;
    public float PredatorDanger = 1f;
}
