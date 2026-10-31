using Evolysia.Sim.Core;

namespace Evolysia.Sim.World;

public sealed class WorldMap
{
    public readonly int W, H;
    public readonly float AxialTilt;

    public readonly float[] Elevation;
    public readonly float[] TempMean;
    public readonly float[] TempAmp;
    public readonly float[] Precip;
    public readonly float[] Seasonality;
    public readonly float[] Flow;
    public readonly float[] WaterAccess;
    public readonly float[] Fault;
    public readonly bool[] Volcano;
    public readonly Biome[] Biome;
    public readonly float[] RowLat;
    public readonly int[] Landmass;
    public int[] LandmassSize = Array.Empty<int>();

    public readonly float[] PlantK, PlantStock;
    public readonly float[] PreyK, PreyStock;
    public readonly float[] FishK, FishStock;
    public readonly float[] Predators;
    public readonly float[] PredatorBase;
    public readonly float[] Ash;
    public readonly float[] YearRain;
    public readonly float[] HumanPressure;
    public readonly float[] RainBias;
    public readonly int[] RainBiasUntil;

    public readonly float[] Trees, TreesK;
    public readonly float[] Rock, RockK;
    public readonly float[] Ore, OreK;
    public readonly ushort[] OreGood;
    public readonly int[] StructureAt;

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
        Trees = new float[n]; TreesK = new float[n]; Rock = new float[n]; RockK = new float[n];
        Ore = new float[n]; OreK = new float[n]; OreGood = new ushort[n]; StructureAt = new int[n];
        for (int i = 0; i < n; i++) { YearRain[i] = 1f; RainBias[i] = 1f; }
    }

    public float Scale => W / 160f;

    public float CellArea => 1f / (Scale * Scale);

    public int Cells(float original) => Math.Max(1, (int)MathF.Round(original * Scale));

    public int Count => W * H;
    public int Index(int x, int y) => y * W + Wrap(x);
    public int Wrap(int x) => ((x % W) + W) % W;
    public int X(int i) => i % W;
    public int Y(int i) => i / W;
    public bool IsLand(int i) => Elevation[i] >= 0f;
    public bool IsWater(int i) => !IsLand(i) || Biome[i] == Core.Biome.Lake;

    public bool IsDryLand(int i) => IsLand(i) && Biome[i] != Core.Biome.Lake;

    public int Distance(int a, int b)
    {
        int dx = Math.Abs(X(a) - X(b));
        dx = Math.Min(dx, W - dx);
        return Math.Max(dx, Math.Abs(Y(a) - Y(b)));
    }

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

    public float Temperature(int i, int day)
    {
        float weather = HashNoise(i / 4, day) * 4f;
        return TempMean[i] + TempAmp[i] * SeasonPhase(i, day) + GlobalTempAnomaly + weather;
    }

    public float Wetness(int i, int day)
    {
        float swing = Seasonality[i] * SeasonPhase(i, day);
        if (MathF.Abs(RowLat[Y(i)]) is > 30f and < 45f) swing = -swing;
        return MathF.Max(0f, (1f + swing) * YearRain[i]);
    }

    public float Growth(int i, int day)
    {
        float t = Temperature(i, day);
        float warm = Math.Clamp((t - 2f) / 14f, 0f, 1f) * (1f - Math.Clamp((t - 34f) / 10f, 0f, 1f));
        float water = Math.Clamp(Precip[i] / 900f * Wetness(i, day) + 0.15f, 0f, 1.2f);
        return warm * water;
    }

    static float HashNoise(int a, int day)
    {

        int p = day / 5;
        float t = (day % 5) / 5f;
        float v0 = Hash01(a, p), v1 = Hash01(a, p + 1);
        return (v0 + (v1 - v0) * t) * 2f - 1f;
    }

    static float Hash01(int a, int b) => (Rng.Mix(((ulong)(uint)a << 32) | (uint)b) >> 40) / 16777216f;
}
