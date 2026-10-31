namespace Evolysia.Sim;

/// <summary>
/// The kind of planet: Terra (balanced, the default), Ignis (hot, dry and volcanic), Cryos (cold
/// and frozen), Thalassia (an ocean world of small islands).
/// </summary>
public enum PlanetType : byte { Terra, Ignis, Cryos, Thalassia }

/// <summary>Everything tunable about a run. Defaults aim for a lively 500-year history.</summary>
public sealed class SimConfig
{
    public ulong Seed = 42;

    // World
    public int Width = 800;
    public int Height = 400;
    public PlanetType Planet = PlanetType.Terra;
    /// <summary>Share of the surface that is land; NaN takes the planet type's own.</summary>
    public float LandFraction = float.NaN;
    public float AxialTiltDegrees = 23.5f;

    // Time
    public const int DaysPerYear = 360;
    public int EcologyStepDays = 5;

    // Starting population: separate peoples (each its own culture and language), each a few bands.
    public int Tribes = 3;
    public int StartBands = 2;            // bands per tribe
    public int StartBandSize = 25;
    /// <summary>The player's first message can be left only after this many years (then once every 250).</summary>
    public int FirstMessageYear = 250;
    /// <summary>Cells where the player placed the tribes (one per tribe); empty: spread them at random.</summary>
    public int[] TribeCells = Array.Empty<int>();

    /// <summary>The land share a planet type has unless overridden.</summary>
    public float Land => !float.IsNaN(LandFraction) ? LandFraction : Planet switch
    {
        PlanetType.Ignis => 0.42f,
        PlanetType.Cryos => 0.36f,
        PlanetType.Thalassia => 0.07f,
        _ => 0.34f,
    };

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
