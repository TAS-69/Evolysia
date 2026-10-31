namespace Evolysia.Sim;

/// <summary>Everything tunable about a run. Defaults aim for a lively 500-year history.</summary>
public sealed class SimConfig
{
    public ulong Seed = 42;

    // World
    public int Width = 800;
    public int Height = 400;
    public float LandFraction = 0.34f;
    public float AxialTiltDegrees = 23.5f;

    // Time
    public const int DaysPerYear = 360;
    public int EcologyStepDays = 5;

    // Starting population
    public int StartBands = 8;
    public int StartBandSize = 25;

    // Safety valve for headless runs: fertility falls off above this many living agents.
    public int SoftMaxAgents = 200000;

    // Worker threads for band days (results are identical for any value).
    public int Threads = Environment.ProcessorCount;

    // Tuning multipliers
    public float InventionRate = 1f;
    public float DisasterRate = 1f;
    public float PredatorDanger = 1f;
    /// <summary>Food the land supports per area, relative to the original tuning (sets how crowded the planet gets).</summary>
    public float CapacityScale = 1f;
}
