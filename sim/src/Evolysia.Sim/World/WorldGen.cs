using Evolysia.Sim.Core;

namespace Evolysia.Sim.World;

public static class WorldGen
{
    public static WorldMap Generate(SimConfig cfg)
    {
        var rng = Rng.Derive(cfg.Seed, 0x574F524C44UL);
        var noise = new Noise(rng);
        var w = new WorldMap(cfg.Width, cfg.Height, cfg.AxialTiltDegrees);

        for (int y = 0; y < w.H; y++) w.RowLat[y] = 90f - (y + 0.5f) / w.H * 180f;

        Terrain(w, noise, cfg);
        var distToSea = DistanceToSea(w);
        Temperature(w, distToSea, cfg.Planet);
        Rain(w, noise, cfg.Planet);
        Rivers(w);
        Biomes(w, noise, rng, cfg.Planet);
        Landmasses(w);
        Ecology.InitCapacities(w, noise, cfg.CapacityScale);
        return w;
    }

    static void Terrain(WorldMap w, Noise noise, SimConfig cfg)
    {
        int n = w.Count;
        var raw = new float[n];
        var ridge = new float[n];
        for (int i = 0; i < n; i++)
        {
            float u = (float)w.X(i) / w.W, v = (float)w.Y(i) / w.H;
            float continent = noise.Cylinder(u, v, 3.2f, 5);
            float detail = noise.Cylinder(u, v, 11f, 4, 17.3f);
            float fine = w.Scale > 1.5f ? noise.Cylinder(u, v, 48f, 3, 29.9f) : 0f;
            float r = 1f - MathF.Abs(noise.Cylinder(u, v, 5.5f, 3, 41.7f));
            ridge[i] = r * r * r;

            float pole = MathF.Abs(v - 0.5f) * 2f;
            raw[i] = cfg.Planet == PlanetType.Thalassia
                ? 0.35f * continent + 0.6f * detail + 0.3f * fine
                : continent + 0.25f * detail + 0.07f * fine - 0.35f * MathF.Max(0f, pole - 0.75f);
        }

        var sorted = (float[])raw.Clone();
        Array.Sort(sorted);
        float sea = sorted[(int)((1f - cfg.Land) * (n - 1))];
        float max = sorted[^1], min = sorted[0];

        for (int i = 0; i < n; i++)
        {
            if (raw[i] >= sea)
            {
                float h = (raw[i] - sea) / MathF.Max(max - sea, 1e-4f);
                float mountains = ridge[i] * MathF.Min(1f, h * 3f) * 3200f;
                w.Elevation[i] = MathF.Pow(h, 1.3f) * 1800f + mountains;
                w.Fault[i] = Math.Clamp(ridge[i] * 1.2f, 0f, 1f);
            }
            else
            {
                float d = (sea - raw[i]) / MathF.Max(sea - min, 1e-4f);
                w.Elevation[i] = -10f - d * 4500f;
                w.Fault[i] = ridge[i] * 0.5f;
            }
        }
    }

    static int[] DistanceToSea(WorldMap w)
    {
        int n = w.Count;
        var dist = new int[n];
        var queue = new Queue<int>();
        for (int i = 0; i < n; i++)
        {
            dist[i] = w.IsLand(i) ? int.MaxValue : 0;
            if (!w.IsLand(i)) queue.Enqueue(i);
        }
        Span<int> nb = stackalloc int[8];
        while (queue.Count > 0)
        {
            int c = queue.Dequeue();
            int k = w.Neighbors(c, nb);
            for (int j = 0; j < k; j++)
                if (dist[nb[j]] == int.MaxValue) { dist[nb[j]] = dist[c] + 1; queue.Enqueue(nb[j]); }
        }
        return dist;
    }

    static float Warming(PlanetType p) => p switch { PlanetType.Ignis => 15f, PlanetType.Cryos => -16f, PlanetType.Thalassia => 2f, _ => 0f };

    static void Temperature(WorldMap w, int[] distToSea, PlanetType planet)
    {
        float tilt = w.AxialTilt / 23.5f;
        for (int i = 0; i < w.Count; i++)
        {
            float lat = w.RowLat[w.Y(i)] * MathF.PI / 180f;
            float t = -15f + 42f * MathF.Cos(lat) + Warming(planet);
            if (w.IsLand(i)) t -= w.Elevation[i] * 0.0065f;
            float continental = Math.Clamp(distToSea[i] / (8f * w.Scale), 0f, 1f);
            w.TempMean[i] = t;
            w.TempAmp[i] = (1.5f + 16f * MathF.Abs(MathF.Sin(lat)) * tilt) * (0.55f + 0.45f * continental);
        }
    }

    static readonly (float lat, float rain)[] RainBands =
        { (0, 1.0f), (10, 0.9f), (20, 0.45f), (28, 0.22f), (38, 0.55f), (50, 0.8f), (62, 0.55f), (75, 0.3f), (90, 0.15f) };

    static float LatRain(float absLat)
    {
        for (int k = 1; k < RainBands.Length; k++)
            if (absLat <= RainBands[k].lat)
            {
                var (l0, r0) = RainBands[k - 1];
                var (l1, r1) = RainBands[k];
                return r0 + (r1 - r0) * (absLat - l0) / (l1 - l0);
            }
        return RainBands[^1].rain;
    }

    static void Rain(WorldMap w, Noise noise, PlanetType planet)
    {
        float wetness = planet switch { PlanetType.Ignis => 0.35f, PlanetType.Cryos => 0.7f, PlanetType.Thalassia => 1.15f, _ => 1f };

        float drain = MathF.Pow(0.955f, 1f / w.Scale), refill = 0.3f / w.Scale;
        for (int y = 0; y < w.H; y++)
        {
            float absLat = MathF.Abs(w.RowLat[y]);
            int dir = absLat is >= 30f and < 60f ? +1 : -1;
            float baseRain = LatRain(absLat) * wetness;
            float moisture = 1f;

            for (int pass = 0; pass < 2; pass++)
                for (int s = 0; s < w.W; s++)
                {
                    int x = dir > 0 ? s : w.W - 1 - s;
                    int i = w.Index(x, y), prev = w.Index(x - dir, y);
                    if (!w.IsLand(i)) { moisture = MathF.Min(1f, moisture + refill); w.Precip[i] = 2000f * baseRain; continue; }
                    float rise = MathF.Max(0f, w.Elevation[i] - MathF.Max(0f, w.Elevation[prev]));
                    float orographic = MathF.Min(moisture, rise / 2500f);
                    moisture = MathF.Max(0.05f, moisture * drain - orographic);
                    if (pass == 1)
                    {
                        float u = (float)x / w.W, v = (float)y / w.H;
                        float jitter = 1f + 0.25f * noise.Cylinder(u, v, 8f, 3, 91.1f);
                        w.Precip[i] = MathF.Max(20f, (2300f * baseRain * (0.2f + 0.8f * moisture) + 1800f * orographic) * jitter);
                    }
                }

            float season = absLat < 25f ? Math.Clamp((absLat - 3f) / 12f, 0f, 0.85f) : absLat < 45f ? 0.45f : 0.25f;
            for (int x = 0; x < w.W; x++) w.Seasonality[w.Index(x, y)] = season;
        }
    }

    static void Rivers(WorldMap w)
    {
        int n = w.Count;

        var filled = new float[n];
        var done = new bool[n];
        var pq = new PriorityQueue<int, float>();
        for (int i = 0; i < n; i++)
        {
            filled[i] = w.Elevation[i];
            if (!w.IsLand(i)) { done[i] = true; pq.Enqueue(i, w.Elevation[i]); }
        }
        Span<int> nb = stackalloc int[8];
        while (pq.TryDequeue(out int c, out _))
        {
            int k = w.Neighbors(c, nb);
            for (int j = 0; j < k; j++)
            {
                int m = nb[j];
                if (done[m]) continue;
                done[m] = true;
                filled[m] = MathF.Max(filled[m], filled[c] + 0.01f);
                pq.Enqueue(m, filled[m]);
            }
        }

        var order = new int[n];
        for (int i = 0; i < n; i++) order[i] = i;
        Array.Sort(order, (a, b) => filled[b].CompareTo(filled[a]));
        foreach (int i in order)
        {
            if (!w.IsLand(i)) continue;
            w.Flow[i] += MathF.Max(0f, w.Precip[i] - 250f) / 1000f * w.CellArea;
            int k = w.Neighbors(i, nb), best = -1;
            float lowest = filled[i];
            for (int j = 0; j < k; j++)
                if (filled[nb[j]] < lowest) { lowest = filled[nb[j]]; best = nb[j]; }
            if (best >= 0 && w.IsLand(best)) w.Flow[best] += w.Flow[i];
        }

        for (int i = 0; i < n; i++)
            if (w.IsLand(i) && filled[i] - w.Elevation[i] > 25f && w.Precip[i] > 350f)
                w.Biome[i] = Biome.Lake;
    }

    static int[] DistanceToLand(WorldMap w)
    {
        var dist = new int[w.Count];
        var queue = new Queue<int>();
        for (int i = 0; i < w.Count; i++)
        {
            dist[i] = w.IsLand(i) ? 0 : int.MaxValue;
            if (w.IsLand(i)) queue.Enqueue(i);
        }
        Span<int> nb = stackalloc int[8];
        while (queue.Count > 0)
        {
            int c = queue.Dequeue();
            int k = w.Neighbors(c, nb);
            for (int j = 0; j < k; j++)
                if (dist[nb[j]] == int.MaxValue) { dist[nb[j]] = dist[c] + 1; queue.Enqueue(nb[j]); }
        }
        return dist;
    }

    static void Landmasses(WorldMap w)
    {
        Array.Fill(w.Landmass, -1);
        var sizes = new List<int>();
        var queue = new Queue<int>();
        Span<int> nb = stackalloc int[8];
        for (int start = 0; start < w.Count; start++)
        {
            if (!w.IsDryLand(start) || w.Landmass[start] >= 0) continue;
            int id = sizes.Count, size = 0;
            w.Landmass[start] = id;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int c = queue.Dequeue();
                size++;
                int k = w.Neighbors(c, nb);
                for (int j = 0; j < k; j++)
                    if (w.IsDryLand(nb[j]) && w.Landmass[nb[j]] < 0) { w.Landmass[nb[j]] = id; queue.Enqueue(nb[j]); }
            }
            sizes.Add(size);
        }
        w.LandmassSize = sizes.ToArray();
    }

    public const float RiverFlow = 6f;

    static void Biomes(WorldMap w, Noise noise, Rng rng, PlanetType planet)
    {
        Span<int> nb = stackalloc int[8];

        var shore = DistanceToLand(w);
        int coastWidth = Math.Max(1, (int)MathF.Round(w.Scale * 0.4f));
        for (int i = 0; i < w.Count; i++)
        {
            if (w.Biome[i] == Biome.Lake) continue;
            if (!w.IsLand(i))
            {
                w.Biome[i] = shore[i] <= coastWidth ? Biome.Coast : w.Elevation[i] > -1500f ? Biome.Ocean : Biome.DeepOcean;
                continue;
            }
            float t = w.TempMean[i], p = w.Precip[i], e = w.Elevation[i];
            float u = (float)w.X(i) / w.W, v = (float)w.Y(i) / w.H;
            float wetNoise = noise.Cylinder(u, v, 14f, 2, 63.3f);
            w.Biome[i] =
                t < -9f ? Biome.IceSheet :
                e > 3000f ? Biome.Alpine :
                t < -1f ? Biome.Tundra :
                e < 300f && p > 900f && w.Flow[i] > RiverFlow && wetNoise > 0.15f ? Biome.Wetland :
                t < 6f ? (p < 280f ? Biome.ColdDesert : Biome.BorealForest) :
                t < 19f ? (p < 250f ? Biome.ColdDesert : p < 500f ? (t > 13f ? Biome.Shrubland : Biome.TemperateGrassland)
                          : p < 750f ? Biome.TemperateGrassland : p < 1700f ? Biome.TemperateForest : Biome.TemperateRainforest)
                        : (p < 280f ? Biome.HotDesert : p < 950f ? Biome.Savanna : p < 1800f ? Biome.TropicalSeasonalForest : Biome.TropicalRainforest);
        }

        var candidates = new List<int>();
        for (int i = 0; i < w.Count; i++)
            if (w.IsLand(i) && w.Biome[i] != Biome.Lake && w.Fault[i] > (planet == PlanetType.Ignis ? 0.6f : 0.75f)) candidates.Add(i);

        int volcanoes = planet == PlanetType.Ignis ? Math.Clamp((int)(candidates.Count * w.CellArea / 12f), 20, 70)
                                                   : Math.Clamp((int)(candidates.Count * w.CellArea / 80f), 4, 14);
        for (int k = 0; k < volcanoes && candidates.Count > 0; k++)
        {
            int idx = rng.Next(candidates.Count);
            int c = candidates[idx];
            candidates.RemoveAt(idx);
            w.Volcano[c] = true;
            w.Biome[c] = Biome.Volcanic;

            int r = Math.Max(0, (int)(w.Scale * (planet == PlanetType.Ignis ? 1.2f : 0.5f)));
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int y = w.Y(c) + dy;
                    if (y < 0 || y >= w.H) continue;
                    int f = w.Index(w.X(c) + dx, y);
                    float ragged = 0.55f + 0.6f * ((uint)f * 2654435761u >> 24) / 255f;
                    if (dx * dx + dy * dy > (r + 0.5f) * (r + 0.5f) * ragged) continue;
                    if (w.IsLand(f) && w.Biome[f] != Biome.Lake) w.Biome[f] = Biome.Volcanic;
                }
        }

        for (int i = 0; i < w.Count; i++)
        {
            if (!w.IsLand(i)) { w.WaterAccess[i] = 1f; continue; }
            bool fresh = w.Flow[i] > RiverFlow || w.Biome[i] is Biome.Lake or Biome.Wetland;
            if (!fresh)
            {
                int k = w.Neighbors(i, nb);
                for (int j = 0; j < k; j++) fresh |= w.Biome[nb[j]] == Biome.Lake || w.Flow[nb[j]] > RiverFlow * 2f;
            }
            w.WaterAccess[i] = fresh ? 1f : Math.Clamp(w.Precip[i] / 700f, 0.05f, 0.9f);
        }
    }
}
