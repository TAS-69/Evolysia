using Evolysia.Sim.Core;

namespace Evolysia.Sim.World;

/// <summary>
/// The planet as a coarse grid (each cell is a region a band can live in). Stored as parallel
/// arrays for speed. x wraps around the planet; y runs from the north pole (0) to the south pole.
/// </summary>
public sealed class WorldMap
{
    public readonly int W, H;
    public readonly float AxialTilt;

    // Static (from generation)
    public readonly float[] Elevation;      // metres; < 0 is sea
    public readonly float[] TempMean;       // annual mean, deg C
    public readonly float[] TempAmp;        // seasonal half-swing, deg C
    public readonly float[] Precip;         // annual mm
    public readonly float[] Seasonality;    // 0..1, how wet/dry the seasons swing
    public readonly float[] Flow;           // river discharge (accumulated runoff)
    public readonly float[] WaterAccess;    // 0..1 fresh water availability
    public readonly float[] Fault;          // 0..1 earthquake proneness
    public readonly bool[] Volcano;
    public readonly Biome[] Biome;
    public readonly float[] RowLat;         // latitude per row, degrees
    public readonly int[] Landmass;         // connected land id (-1 for water and lakes)
    public int[] LandmassSize = Array.Empty<int>();

    // Ecology (dynamic)
    public readonly float[] PlantK, PlantStock;   // forageable food, person-days
    public readonly float[] PreyK, PreyStock;     // huntable game, person-days
    public readonly float[] FishK, FishStock;
    public readonly float[] Predators;            // 0..1 danger density
    public readonly float[] PredatorBase;
    public readonly float[] Ash;                  // 0..1 lingering ash/burn damage
    public readonly float[] YearRain;             // this year's rain multiplier (droughts < 0.6)
    public readonly float[] HumanPressure;        // recent hunting/presence, pushes predators away
    public readonly float[] RainBias;             // god-given rain (>1) or drought (<1); 1 = none
    public readonly int[] RainBiasUntil;          // day it ends

    public float GlobalTempAnomaly;

    public WorldMap(int w, int h, float axialTilt)
    {
        W = w; H = h; AxialTilt = axialTilt;
        int n = w * h;
        Elevation = new float[n]; TempMean = new float[n]; TempAmp = new float[n];
        Precip = new float[n]; Seasonality = new float[n]; Flow = new float[n];
        WaterAccess = new float[n]; Fault = new float[n]; Volcano = new bool[n];
        Biome = new Biome[n]; RowLat = new float[h]; Landmass = new int[n];
        PlantK = new float[n]; PlantStock = new float[n]; PreyK = new float[n]; PreyStock = new float[n];
        FishK = new float[n]; FishStock = new float[n]; Predators = new float[n]; PredatorBase = new float[n];
        Ash = new float[n]; YearRain = new float[n]; HumanPressure = new float[n];
        RainBias = new float[n]; RainBiasUntil = new int[n];
        for (int i = 0; i < n; i++) { YearRain[i] = 1f; RainBias[i] = 1f; }
    }

    public int Count => W * H;
    public int Index(int x, int y) => y * W + Wrap(x);
    public int Wrap(int x) => ((x % W) + W) % W;
    public int X(int i) => i % W;
    public int Y(int i) => i / W;
    public bool IsLand(int i) => Elevation[i] >= 0f;
    public bool IsWater(int i) => !IsLand(i) || Biome[i] == Core.Biome.Lake;

    /// <summary>Land a band could live on (not water, not a lake).</summary>
    public bool IsDryLand(int i) => IsLand(i) && Biome[i] != Core.Biome.Lake;

    /// <summary>Wrapped horizontal distance plus vertical distance (cells).</summary>
    public int Distance(int a, int b)
    {
        int dx = Math.Abs(X(a) - X(b));
        dx = Math.Min(dx, W - dx);
        return Math.Max(dx, Math.Abs(Y(a) - Y(b)));
    }

    /// <summary>The 8 neighbours (fewer at the poles).</summary>
    public int Neighbors(int i, Span<int> buffer)
    {
        int x = X(i), y = Y(i), n = 0;
        for (int dy = -1; dy <= 1; dy++)
        {
            int ny = y + dy;
            if (ny < 0 || ny >= H) continue;
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                buffer[n++] = Index(x + dx, ny);
            }
        }
        return n;
    }

    public static int DayOfYear(int day) => day % SimConfig.DaysPerYear;

    public bool IsTropical(int i) => MathF.Abs(RowLat[Y(i)]) < 23f;
    public bool IsNorth(int i) => RowLat[Y(i)] >= 0f;

    /// <summary>-1..1 seasonal phase: +1 at local mid-summer.</summary>
    public float SeasonPhase(int i, int day)
    {
        float s = MathF.Sin(2f * MathF.PI * (DayOfYear(day) - 45) / SimConfig.DaysPerYear);
        return IsNorth(i) ? s : -s;
    }

    public Season SeasonOf(int i, int day)
    {
        if (IsTropical(i)) return SeasonPhase(i, day) > 0f ? Season.Wet : Season.Dry;
        int d = (DayOfYear(day) + (IsNorth(i) ? 0 : 180)) % SimConfig.DaysPerYear;
        return (Season)(d / 90);
    }

    /// <summary>Today's temperature including regional weather noise and global anomalies.</summary>
    public float Temperature(int i, int day)
    {
        float weather = HashNoise(i / 4, day) * 4f;
        return TempMean[i] + TempAmp[i] * SeasonPhase(i, day) + GlobalTempAnomaly + weather;
    }

    /// <summary>0..1 relative wetness today (seasonal rain swing x this year's anomaly).</summary>
    public float Wetness(int i, int day)
    {
        float swing = Seasonality[i] * SeasonPhase(i, day);
        if (MathF.Abs(RowLat[Y(i)]) is > 30f and < 45f) swing = -swing; // dry summers (mediterranean)
        return MathF.Max(0f, (1f + swing) * YearRain[i]);
    }

    /// <summary>0..1 plant growth today: warmth x water.</summary>
    public float Growth(int i, int day)
    {
        float t = Temperature(i, day);
        float warm = Math.Clamp((t - 2f) / 14f, 0f, 1f) * (1f - Math.Clamp((t - 34f) / 10f, 0f, 1f));
        float water = Math.Clamp(Precip[i] / 900f * Wetness(i, day) + 0.15f, 0f, 1.2f);
        return warm * water;
    }

    static float HashNoise(int a, int day)
    {
        // Smooth-ish in time: blend two daily hashed values over 5-day periods.
        int p = day / 5;
        float t = (day % 5) / 5f;
        float v0 = Hash01(a, p), v1 = Hash01(a, p + 1);
        return (v0 + (v1 - v0) * t) * 2f - 1f;
    }

    static float Hash01(int a, int b) => (Rng.Mix(((ulong)(uint)a << 32) | (uint)b) >> 40) / 16777216f;
}
