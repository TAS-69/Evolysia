using Evolysia.Sim.Core;
using Evolysia.Sim.World;

namespace Evolysia.Game;

/// <summary>
/// Bakes the planet into one RGBA8 image of palette-slot indices, 16 px per cell:
/// R = ground (terrain materials blended on a dual grid, then rivers), G = flora as it looks in
/// season, B = flora off-season (bare in winter, dry-season states, snow-capped conifers),
/// A = bit 1 where G is drawn, bit 2 where B is drawn. Colour comes later, per cell, from the
/// nature LUT in tiles.gdshader, so seasons change without rebaking.
/// </summary>
public static class TileBake
{
    public const int Px = 16;

    /// <summary>The terrain material a cell is made of (art spec section 3.1).</summary>
    public static string Material(WorldMap w, int i)
    {
        if (w.Volcano[i]) return "lava";
        if (w.IsLand(i) && w.Ash[i] > 0.3f) return "ash";
        return w.Biome[i] switch
        {
            Biome.DeepOcean => "deep_water",
            Biome.Ocean or Biome.Lake => "water",
            Biome.Coast => "shallow_water",
            Biome.IceSheet => "ice",
            Biome.Tundra => "tundra",
            Biome.BorealForest or Biome.TemperateForest or Biome.TemperateRainforest or Biome.TropicalSeasonalForest => "forest_floor",
            Biome.TropicalRainforest => "jungle_floor",
            Biome.ColdDesert => "scree",
            Biome.TemperateGrassland => "grass",
            Biome.Shrubland or Biome.Savanna => "dry_grass",
            Biome.HotDesert => "sand",
            Biome.Wetland => "marsh",
            Biome.Alpine => w.Elevation[i] > 3200f ? "rock" : "scree",
            Biome.Volcanic => "basalt",
            _ => "dirt",
        };
    }

    public static byte[] Bake(WorldMap w)
    {
        int texW = w.W * Px, texH = w.H * Px;
        var px = new byte[texW * texH * 4];
        Ground(w, px, texW, texH);
        Rivers(w, px, texW, texH);
        Flora(w, px, texW, texH);
        return px;
    }

    // ---- ground ---------------------------------------------------------------------------------

    sealed record Mat(string Id, int Priority, Sheet Plain, Sheet Corners);

    static readonly Dictionary<string, Mat?> Mats = new();

    static Mat? MatOf(string id)
    {
        if (Mats.TryGetValue(id, out var m)) return m;
        var plain = Art.Get($"terrain.{id}_plain");
        var corners = Art.Get($"terrain.{id}_corners");
        m = plain == null || corners == null ? null
            : new Mat(id, plain.Meta.TryGetProperty("layer_priority", out var p) ? p.GetInt32() : 0, plain, corners);
        return Mats[id] = m;
    }

    /// <summary>
    /// Dual grid: one tile per cell corner, covering the quarters of the four cells that meet there.
    /// The lowest-priority material fills it; each higher one is laid over with the corner tile whose
    /// mask marks the quarters at or above its priority, so higher materials' edges overlap lower ones.
    /// </summary>
    static void Ground(WorldMap w, byte[] px, int texW, int texH)
    {
        var mats = new Mat[w.Count];
        for (int i = 0; i < w.Count; i++) mats[i] = MatOf(Material(w, i)) ?? MatOf("dirt")!;
        Span<int> corner = stackalloc int[4];   // TL, TR, BR, BL - the order of the mask bits 1, 2, 4, 8
        var present = new List<Mat>(4);
        for (int vy = 0; vy <= w.H; vy++)
            for (int vx = 0; vx < w.W; vx++)
            {
                int top = Math.Max(vy - 1, 0), bottom = Math.Min(vy, w.H - 1);
                corner[0] = w.Index(vx - 1, top); corner[1] = w.Index(vx, top);
                corner[2] = w.Index(vx, bottom); corner[3] = w.Index(vx - 1, bottom);
                present.Clear();
                for (int k = 0; k < 4; k++) if (!present.Contains(mats[corner[k]])) present.Add(mats[corner[k]]);
                present.Sort((a, b) => a.Priority.CompareTo(b.Priority));
                int ox = vx * Px - Px / 2, oy = vy * Px - Px / 2;
                uint h = Hash(vx, vy);
                Blit(present[0].Plain, 0, (int)(h % (uint)present[0].Plain.Rows), ox, oy, px, texW, texH, 0);
                for (int m = 1; m < present.Count; m++)
                {
                    int mask = 0;
                    for (int k = 0; k < 4; k++) if (mats[corner[k]].Priority >= present[m].Priority) mask |= 1 << k;
                    Blit(present[m].Corners, 0, mask, ox, oy, px, texW, texH, 0);
                }
            }
    }

    // ---- rivers ---------------------------------------------------------------------------------

    static void Rivers(WorldMap w, byte[] px, int texW, int texH)
    {
        var stream = Art.Get("overlays.river_stream");
        var river = Art.Get("overlays.river_river");
        var great = Art.Get("overlays.river_great_river");
        if (stream == null || river == null || great == null) return;
        // The art connects N/E/S/W only, so each river cell drains into its best 4-neighbour.
        var mask = new int[w.Count];
        int[] dx = { 0, 1, 0, -1 }, dy = { -1, 0, 1, 0 };   // N, E, S, W = bits 1, 2, 4, 8
        for (int i = 0; i < w.Count; i++)
        {
            if (!IsRiver(w, i)) continue;
            int x = w.X(i), y = w.Y(i), best = -1, bestDir = -1;
            float bestFlow = w.Flow[i];
            for (int d = 0; d < 4; d++)
            {
                int ny = y + dy[d];
                if (ny < 0 || ny >= w.H) continue;
                int n = w.Index(x + dx[d], ny);
                if (w.IsWater(n) || w.Biome[n] == Biome.Lake) { if (best < 0 || w.IsLand(best)) { best = n; bestDir = d; bestFlow = float.MaxValue; } }
                else if (w.Flow[n] > bestFlow) { best = n; bestDir = d; bestFlow = w.Flow[n]; }
            }
            if (best < 0) continue;
            mask[i] |= 1 << bestDir;
            if (IsRiver(w, best)) mask[best] |= 1 << ((bestDir + 2) % 4);
        }
        for (int i = 0; i < w.Count; i++)
        {
            if (mask[i] == 0) continue;
            var sheet = w.Flow[i] > WorldGen.RiverFlow * 16f ? great : w.Flow[i] > WorldGen.RiverFlow * 6f ? river : stream;
            Blit(sheet, 0, mask[i], w.X(i) * Px, w.Y(i) * Px, px, texW, texH, 0);
        }
    }

    static bool IsRiver(WorldMap w, int i) => w.IsLand(i) && w.Biome[i] != Biome.Lake && w.Flow[i] >= WorldGen.RiverFlow * 2f;

    // ---- flora ----------------------------------------------------------------------------------

    /// <summary>What grows in a biome, with relative weights, and how many plants a cell holds.</summary>
    static ((string id, float weight)[] kinds, int count) FloraOf(Biome b) => b switch
    {
        Biome.BorealForest => (new[] { ("conifer_mature", 4f), ("conifer_old", 2f), ("conifer_young", 2f), ("conifer_sapling", 0.6f), ("shrub", 0.8f) }, 6),
        Biome.TemperateForest => (new[] { ("broadleaf_mature", 4f), ("broadleaf_old", 1.2f), ("broadleaf_young", 2f), ("conifer_mature", 0.8f), ("shrub", 1f), ("berry_bush_fruiting", 0.4f), ("mushrooms", 0.3f), ("fallen_log", 0.3f) }, 6),
        Biome.TemperateRainforest => (new[] { ("conifer_old", 3f), ("conifer_mature", 3f), ("broadleaf_old", 1f), ("fallen_log", 0.5f), ("mushrooms", 0.5f) }, 7),
        Biome.TemperateGrassland => (new[] { ("tall_grass", 4f), ("flowers", 1.5f), ("shrub", 0.8f), ("broadleaf_young", 0.3f), ("wild_grain_ripe", 0.5f) }, 4),
        Biome.Shrubland => (new[] { ("shrub", 4f), ("shrub_dry", 2f), ("berry_bush_flowering", 0.6f), ("tall_grass", 1f) }, 4),
        Biome.HotDesert => (new[] { ("cactus", 2f), ("cactus_tall", 1f), ("cactus_flowering", 0.4f), ("shrub_dry", 1f) }, 1),
        Biome.ColdDesert => (new[] { ("shrub_dry", 3f), ("tall_grass", 0.5f) }, 1),
        Biome.Savanna => (new[] { ("tall_grass", 4f), ("acacia_mature", 1.2f), ("acacia_young", 0.6f), ("shrub_dry", 1f) }, 4),
        Biome.TropicalSeasonalForest => (new[] { ("jungle_tree_mature", 3f), ("broadleaf_mature", 2f), ("palm_mature", 1f), ("shrub", 1f) }, 6),
        Biome.TropicalRainforest => (new[] { ("rainforest_giant_mature", 1.5f), ("jungle_tree_mature", 3f), ("jungle_tree_young", 1f), ("palm_mature", 1f) }, 6),
        Biome.Wetland => (new[] { ("reeds", 4f), ("tall_grass", 2f), ("broadleaf_young", 0.4f) }, 4),
        Biome.Tundra => (new[] { ("shrub_dry", 1f), ("tall_grass", 0.6f), ("mushrooms", 0.3f) }, 1),
        Biome.Alpine => (new[] { ("conifer_young", 1f), ("shrub_dry", 1f) }, 1),
        Biome.Volcanic => (new[] { ("conifer_dead", 1f), ("stump", 1f) }, 1),
        _ => (Array.Empty<(string, float)>(), 0),
    };

    record struct Plant(int X, int Y, Sheet Summer, int SummerRow, Sheet? Off, int OffRow, Sheet? Cap);

    static void Flora(WorldMap w, byte[] px, int texW, int texH)
    {
        var plants = new List<Plant>();
        for (int i = 0; i < w.Count; i++)
        {
            if (!w.IsLand(i) || w.Volcano[i] || w.Biome[i] == Biome.Lake) continue;
            var (kinds, count) = FloraOf(w.Biome[i]);
            if (count == 0) continue;
            bool burned = w.Ash[i] > 0.25f;
            bool snowyWinter = w.TempMean[i] - w.TempAmp[i] < -3f;
            float total = kinds.Sum(k => k.weight);
            for (int n = 0; n < count; n++)
            {
                uint h = Hash(i, n * 7 + 3);
                if ((h & 0xFF) < 40) continue;   // gaps, so cells don't read as a grid
                float pick = (h >> 8 & 0xFFFF) / 65536f * total;
                string id = kinds[^1].id;
                foreach (var (kid, weight) in kinds) { if (pick < weight) { id = kid; break; } pick -= weight; }
                if (burned) id = BurnedOf(id);
                var sheet = Art.Get("flora." + id);
                if (sheet == null) continue;
                Sheet? off = sheet, cap = null;
                if (sheet.Meta.TryGetProperty("seasonal_states", out var states))
                    foreach (var s in states.EnumerateObject()) off = Art.Get(s.Value.GetString()!) ?? sheet;
                if (snowyWinter && id.StartsWith("conifer_") && id is not ("conifer_sapling" or "conifer_dead" or "conifer_burned"))
                    cap = Art.Get("flora.conifer_snow_capped");
                uint v = Hash(n, i * 13 + 1);
                int x = w.X(i) * Px + 2 + (int)(Hash(i, n * 2 + 11) % 12u);
                int y = w.Y(i) * Px + 3 + (int)(Hash(i, n * 5 + 17) % 13u);
                plants.Add(new Plant(x, y, sheet, (int)(v % (uint)sheet.Rows), off, (int)(v % (uint)Math.Max(1, off?.Rows ?? 1)), cap));
            }
        }
        plants.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));   // y-sort: nearer plants overlap
        foreach (var p in plants)
        {
            Blit(p.Summer, 0, p.SummerRow, p.X - p.Summer.PivotX, p.Y - p.Summer.PivotY, px, texW, texH, 1);
            var off = p.Off ?? p.Summer;
            Blit(off, 0, p.OffRow % off.Rows, p.X - off.PivotX, p.Y - off.PivotY, px, texW, texH, 2);
            if (p.Cap != null) Blit(p.Cap, 0, p.SummerRow % p.Cap.Rows, p.X - p.Cap.PivotX, p.Y - p.Cap.PivotY, px, texW, texH, 2);
        }
    }

    static string BurnedOf(string id)
    {
        foreach (var tree in new[] { "rainforest_giant", "jungle_tree", "broadleaf", "conifer", "acacia", "palm", "shrub" })
            if (id.StartsWith(tree)) return tree + "_burned";
        return "stump";
    }

    // ---- helpers --------------------------------------------------------------------------------

    /// <summary>
    /// Copies one frame's slot indices into the world image (wrapping east-west). Layer 0 writes
    /// the ground channel (R); layer 1 / 2 write in-season (G) / off-season (B) flora and mark A.
    /// </summary>
    static void Blit(Sheet sheet, int frame, int row, int ox, int oy, byte[] px, int texW, int texH, int layer)
    {
        if (row >= sheet.Rows) return;
        for (int y = 0; y < sheet.FrameH; y++)
        {
            int ty = oy + y;
            if (ty < 0 || ty >= texH) continue;
            for (int x = 0; x < sheet.FrameW; x++)
            {
                byte slot = sheet.Slot(frame, row, x, y);
                if (slot == Art.Empty) continue;
                int tx = ((ox + x) % texW + texW) % texW;
                int o = (ty * texW + tx) * 4;
                if (layer == 0) px[o] = slot;
                else { px[o + layer] = slot; px[o + 3] |= (byte)layer; }
            }
        }
    }

    static uint Hash(int a, int b)
    {
        uint h = (uint)(a * 374761393 + b * 668265263);
        h = (h ^ (h >> 13)) * 1274126177u;
        return h ^ (h >> 16);
    }
}
