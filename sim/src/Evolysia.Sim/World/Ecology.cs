using Evolysia.Sim.Core;

namespace Evolysia.Sim.World;

public static class Ecology
{

    static readonly Dictionary<Biome, (float plant, float game, float danger)> Table = new()
    {
        [Biome.IceSheet] = (0f, 1f, 0.15f),
        [Biome.Tundra] = (4f, 9f, 0.35f),
        [Biome.BorealForest] = (10f, 9f, 0.6f),
        [Biome.ColdDesert] = (3f, 3f, 0.2f),
        [Biome.TemperateGrassland] = (20f, 15f, 0.45f),
        [Biome.TemperateForest] = (26f, 11f, 0.55f),
        [Biome.TemperateRainforest] = (22f, 9f, 0.5f),
        [Biome.Shrubland] = (13f, 7f, 0.3f),
        [Biome.HotDesert] = (2.5f, 2f, 0.25f),
        [Biome.Savanna] = (24f, 18f, 0.75f),
        [Biome.TropicalSeasonalForest] = (30f, 10f, 0.6f),
        [Biome.TropicalRainforest] = (30f, 7f, 0.65f),
        [Biome.Wetland] = (26f, 9f, 0.7f),
        [Biome.Alpine] = (2.5f, 4f, 0.35f),
        [Biome.Volcanic] = (8f, 3f, 0.2f),
        [Biome.Lake] = (6f, 4f, 0.3f),
    };

    public const float PlantGrowth = 0.10f, GameGrowth = 0.06f, FishGrowth = 0.08f;

    public static void InitCapacities(WorldMap w, Noise noise, float capacityScale = 1f)
    {
        Span<int> nb = stackalloc int[8];
        float area = w.CellArea * capacityScale;
        int reach = Math.Max(1, (int)MathF.Round(w.Scale * 0.4f));
        for (int i = 0; i < w.Count; i++)
        {
            if (!w.IsLand(i) && w.Biome[i] != Biome.Coast) continue;
            if (w.Biome[i] == Biome.Coast) { w.FishK[i] = 700f * area; w.FishStock[i] = w.FishK[i]; continue; }

            var (plant, game, danger) = Table[w.Biome[i]];
            float u = (float)w.X(i) / w.W, v = (float)w.Y(i) / w.H;
            float lushness = 1f + 0.25f * noise.Cylinder(u, v, 18f, 2, 7.7f);
            float river = w.Flow[i] > WorldGen.RiverFlow ? 1.3f : 1f;

            w.PlantK[i] = plant * lushness * river * 4f / PlantGrowth * area;
            w.PreyK[i] = game * lushness * 4f / GameGrowth * area;
            w.PredatorBase[i] = danger * Math.Clamp(lushness, 0.7f, 1.2f);

            float fish = w.Biome[i] == Biome.Lake ? 500f : Math.Clamp(w.Flow[i] / WorldGen.RiverFlow, 0f, 4f) * 60f;
            if (NearCoast(w, i, reach)) fish += 250f;
            w.FishK[i] = fish * area;

            w.PlantStock[i] = w.PlantK[i] * 0.7f;
            w.PreyStock[i] = w.PreyK[i] * 0.8f;
            w.FishStock[i] = w.FishK[i];
            w.Predators[i] = w.PredatorBase[i];
        }
        InitMaterials(w);
    }

    public static float TreeDensity(Biome b) => b switch
    {
        Biome.BorealForest or Biome.TemperateForest or Biome.TemperateRainforest or Biome.TropicalRainforest => 1f,
        Biome.TropicalSeasonalForest => 0.85f,
        Biome.Savanna => 0.3f,
        Biome.Wetland => 0.3f,
        Biome.Shrubland => 0.2f,
        Biome.TemperateGrassland => 0.1f,
        Biome.Alpine => 0.15f,
        Biome.Tundra => 0.05f,
        _ => 0f,
    };

    static void InitMaterials(WorldMap w)
    {
        string[] metals = { "copper_ore", "copper_ore", "tin_ore", "iron_ore", "iron_ore", "iron_ore", "gold_ore", "silver_ore", "coal", "coal" };
        for (int i = 0; i < w.Count; i++)
        {
            if (!w.IsDryLand(i)) continue;
            var b = w.Biome[i];
            w.TreesK[i] = TreeDensity(b) * 12f;
            w.Trees[i] = w.TreesK[i];
            bool rocky = b is Biome.Alpine or Biome.Volcanic or Biome.ColdDesert || w.Elevation[i] > 1500f;
            ulong h = Rng.Mix((ulong)i * 0x9E3779B97F4A7C15UL);
            w.RockK[i] = rocky ? 30f : (h & 7) == 0 ? 8f : 1.5f;
            w.Rock[i] = w.RockK[i];
            float fault = w.Fault[i];
            if (fault > 0.3f && (h >> 8 & 0xFF) < 22 + fault * 40f)
            {
                string metal = b == Biome.Volcanic ? "sulfur" : metals[(int)((h >> 16) % (ulong)metals.Length)];
                w.OreGood[i] = Economy.Goods.Id(metal);
                w.OreK[i] = 20f + (h >> 24 & 31);
                w.Ore[i] = w.OreK[i];
            }
        }
    }

    static bool NearCoast(WorldMap w, int i, int reach)
    {
        int x = w.X(i), y = w.Y(i);
        for (int dy = -reach; dy <= reach; dy++)
        {
            int ny = y + dy;
            if (ny < 0 || ny >= w.H) continue;
            for (int dx = -reach; dx <= reach; dx++)
                if (w.Biome[w.Index(x + dx, ny)] == Biome.Coast) return true;
        }
        return false;
    }

    public static void NewYear(WorldMap w, Noise noise, int year)
    {
        for (int i = 0; i < w.Count; i++)
        {
            float u = (float)w.X(i) / w.W, v = (float)w.Y(i) / w.H;
            float n = noise.Fbm(MathF.Cos(u * 6.2832f) * 1.2f, MathF.Sin(u * 6.2832f) * 1.2f + v * 3f, year * 0.37f, 3);
            w.YearRain[i] = Math.Clamp(1f + 0.9f * n, 0.25f, 1.6f);
            if (w.RainBiasUntil[i] > year * SimConfig.DaysPerYear)
                w.YearRain[i] = w.RainBias[i] > 1f ? MathF.Max(w.YearRain[i], w.RainBias[i]) : MathF.Min(w.YearRain[i], w.RainBias[i]);
        }
    }

    public static void Step(WorldMap w, int day, int dt)
    {

        Parallel.For(0, w.H, y =>
        {
            for (int i = y * w.W; i < (y + 1) * w.W; i++) StepCell(w, i, day, dt);
        });
    }

    static void StepCell(WorldMap w, int i, int day, int dt)
    {
        if (w.PlantK[i] <= 0f && w.FishK[i] <= 0f) return;
        float growth = w.Growth(i, day);
        float rain = MathF.Min(w.YearRain[i], 1.2f);
        float health = 1f - w.Ash[i];

        float k = w.PlantK[i] * (0.4f + 0.6f * rain) * health;
        w.PlantStock[i] = Logistic(w.PlantStock[i], k, PlantGrowth * growth * dt, w.PlantK[i]);
        if (growth < 0.05f) w.PlantStock[i] *= 1f - 0.002f * dt;

        float gk = w.PreyK[i] * (0.6f + 0.4f * rain) * health;
        w.PreyStock[i] = Logistic(w.PreyStock[i], gk, GameGrowth * MathF.Max(growth, 0.3f) * dt, w.PreyK[i]);
        w.FishStock[i] = Logistic(w.FishStock[i], w.FishK[i], FishGrowth * dt, w.FishK[i]);

        w.HumanPressure[i] *= MathF.Pow(0.97f, dt);

        if (w.TreesK[i] > 0f)
        {
            if (w.Ash[i] > 0.3f) w.Trees[i] *= 1f - 0.02f * dt;
            else w.Trees[i] = MathF.Min(w.TreesK[i], w.Trees[i] + w.TreesK[i] * 0.00012f * dt + 0.0005f * dt * MathF.Min(1f, w.Trees[i]));
        }
        w.Predators[i] = w.PredatorBase[i] * Math.Clamp(1f - w.HumanPressure[i], 0.15f, 1f)
                         * (0.5f + 0.5f * Math.Clamp(w.PreyStock[i] / MathF.Max(w.PreyK[i], 1f), 0f, 1f));
        if (w.Ash[i] > 0f) w.Ash[i] = MathF.Max(0f, w.Ash[i] - 0.0015f * dt);
    }

    static float Logistic(float s, float k, float rate, float baseK)
    {
        if (k <= 0f) return s * 0.98f;
        s = MathF.Max(s, baseK * 0.02f);
        return MathF.Max(0f, s + rate * s * (1f - s / k));
    }

    public static float Harvest(float[] stock, float[] cap, int i, float want)
    {
        float k = MathF.Max(cap[i], 1f);
        float got = want * stock[i] / (stock[i] + 0.25f * k);
        got = MathF.Min(got, stock[i]);
        stock[i] -= got;
        return got;
    }
}
