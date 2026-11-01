using Evolysia.Sim.Core;
using Evolysia.Sim.World;

namespace Evolysia.Game;

public static class TileBake
{
    public const int Px = 16;

    public sealed record Input(WorldMap World, IReadOnlyDictionary<int, (string crop, float growth, bool orchard)> Fields,
                               IReadOnlySet<int> Occupied, byte[] Trees, byte[] Rocks, IReadOnlyDictionary<int, byte> Roads);

    public static string Material(Input input, int i)
    {
        var w = input.World;
        if (w.Volcano[i]) return "lava";
        if (w.IsLand(i) && w.Ash[i] > 0.3f) return "ash";
        if (input.Fields.TryGetValue(i, out var f) && !f.orchard) return "tilled_soil";
        float n = Patch(w, i);
        float t = w.TempMean[i], rain = w.Precip[i];
        if (Beach(w, i))
            return t < 4f ? "gravel" : n > 0.8f ? "shell_beach" : t > 22f ? (n > 0.55f ? "coral_shore" : "fine_sand") : "sand";
        return w.Biome[i] switch
        {
            Biome.DeepOcean => "deep_water",
            Biome.Ocean or Biome.Lake => "water",
            Biome.Coast => "shallow_water",
            Biome.IceSheet => "ice",
            Biome.Tundra => t < -9f ? "permafrost" : n > 0.66f ? "lichen_rock" : n < 0.2f ? "frozen_mud" : "tundra",
            Biome.BorealForest => rain > 700f && n > 0.55f ? "moss_floor" : "pine_needles",
            Biome.TemperateForest => n > 0.5f ? "leaf_litter" : "forest_floor",
            Biome.TemperateRainforest => n > 0.4f ? "moss_floor" : "forest_floor",
            Biome.TropicalSeasonalForest => n > 0.72f ? "red_clay" : "forest_floor",
            Biome.TropicalRainforest => "jungle_floor",
            Biome.ColdDesert => n > 0.78f ? "salt_flat" : n > 0.45f ? "gravel" : "scree",
            Biome.TemperateGrassland => rain < 450f ? "steppe" : n > 0.62f ? "meadow" : "grass",
            Biome.Shrubland => t < 12f && rain > 500f ? "heathland" : n > 0.7f ? "steppe" : "dry_grass",
            Biome.Savanna => n > 0.82f ? "red_sand" : "dry_grass",
            Biome.HotDesert => n > 0.6f ? "dune" : n < 0.12f ? "red_sand" : "sand",
            Biome.Wetland => t > 22f && Shore(w, i) ? "mangrove_floor" : t < 5f ? "bog" : n > 0.7f ? "peat" : "marsh",
            Biome.Alpine => w.Elevation[i] > 3200f ? (n > 0.6f ? "granite" : n < 0.25f ? "slate" : "rock") : "scree",
            Biome.Volcanic => n > 0.75f ? "obsidian" : "basalt",
            _ => "dirt",
        };
    }

    static bool Beach(WorldMap w, int i) =>
        w.IsLand(i) && w.Elevation[i] < 40f && w.Biome[i] is not (Biome.Lake or Biome.Wetland or Biome.IceSheet or Biome.Alpine or Biome.Volcanic)
        && Shore(w, i);

    static bool Shore(WorldMap w, int i)
    {
        int x = w.X(i), y = w.Y(i);
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int ny = y + dy;
                if (ny < 0 || ny >= w.H || (dx == 0 && dy == 0)) continue;
                int n = w.Index(x + dx, ny);
                if (!w.IsLand(n)) return true;
            }
        return false;
    }

    static float Patch(WorldMap w, int i)
    {
        float fx = w.X(i) / 3.5f, fy = w.Y(i) / 3.5f;
        int x0 = (int)MathF.Floor(fx), y0 = (int)MathF.Floor(fy);
        float tx = fx - x0, ty = fy - y0;
        tx = tx * tx * (3 - 2 * tx); ty = ty * ty * (3 - 2 * ty);
        int period = (int)MathF.Ceiling(w.W / 3.5f);
        float H(int a, int b) => (Hash(((a % period) + period) % period, b + 911) & 0xFFFF) / 65535f;
        float v = (H(x0, y0) * (1 - tx) + H(x0 + 1, y0) * tx) * (1 - ty) + (H(x0, y0 + 1) * (1 - tx) + H(x0 + 1, y0 + 1) * tx) * ty;
        return Math.Clamp((v - 0.5f) * 1.6f + 0.5f + ((Hash(i, 5) & 0xFF) / 255f - 0.5f) * 0.15f, 0f, 1f);
    }

    public static Baked Bake(Input input, int density, int cx0, int cy0, int cw, int ch)
    {
        int pixels = cw * density * ch * density;
        var t = new Target(new byte[pixels * 4], new byte[pixels * 2], cx0 * density, cy0 * density, cw * density, ch * density,
                           input.World.W * density, input.World.H * density, density);
        Ground(input, t, cx0, cy0, cw, ch);
        Cliffs(input, t, cx0, cy0, cw, ch);
        Rivers(input.World, t, cx0, cy0, cw, ch);
        RoadSurface(input, t, cx0, cy0, cw, ch);
        Flora(input, t, cx0, cy0, cw, ch);
        return new Baked(t.Px, t.Anchor);
    }

    public sealed record Baked(byte[] Px, byte[] Anchor);

    static void Cliffs(Input input, Target target, int x0, int y0, int width, int height)
    {
        var w = input.World; int d = target.D;
        Span<int> cells = stackalloc int[4]; Span<int> levels = stackalloc int[4];
        for (int y = Math.Max(1, y0); y <= Math.Min(y0 + height, w.H - 1); y++)
            for (int x = x0; x <= x0 + width; x++)
            {
                cells[0] = w.Index(x - 1, y - 1); cells[1] = w.Index(x, y - 1); cells[2] = w.Index(x, y); cells[3] = w.Index(x - 1, y);
                int high = int.MinValue, low = int.MaxValue; float top = float.MinValue, bottom = float.MaxValue;
                for (int k = 0; k < 4; k++) { levels[k] = (int)MathF.Floor(w.Elevation[cells[k]] / 250f); high = Math.Max(high, levels[k]); low = Math.Min(low, levels[k]); top = MathF.Max(top, w.Elevation[cells[k]]); bottom = MathF.Min(bottom, w.Elevation[cells[k]]); }
                if (high == low || high < 1 || top - bottom < 150f) continue;
                int mask = 0; for (int k = 0; k < 4; k++) if (levels[k] == high) mask |= 1 << k;
                int index = cells[0];
                string material = w.Biome[index] == Biome.Volcanic ? "basalt" : w.TempMean[index] < -8f ? "ice" : w.Biome[index] == Biome.HotDesert ? "sandstone" : w.Elevation[index] > 3000f ? "granite" : "rock";
                var sheet = Art.Lod("landscape." + material + "_cliff_corners", d) ?? Art.Get("terrain.rock_cliff_corners");
                if (sheet != null) Blit(target, sheet, 0, mask, x * d - d / 2, y * d - d / 2, Math.Max(1, d / sheet.Density), 0);
            }
    }

    static void RoadSurface(Input input, Target target, int x0, int y0, int width, int height)
    {
        var w = input.World; int d = target.D;
        for (int y = Math.Max(0, y0); y < Math.Min(y0 + height, w.H); y++) for (int x = x0; x < x0 + width; x++)
        {
            int cell = w.Index(x, y); byte kind = input.Roads.GetValueOrDefault(cell);
            if (kind == 0 || !w.IsDryLand(cell)) continue;
            int mask = 0;
            if (y > 0 && input.Roads.ContainsKey(w.Index(x, y - 1))) mask |= 1;
            if (input.Roads.ContainsKey(w.Index(x + 1, y))) mask |= 2;
            if (y + 1 < w.H && input.Roads.ContainsKey(w.Index(x, y + 1))) mask |= 4;
            if (input.Roads.ContainsKey(w.Index(x - 1, y))) mask |= 8;
            string name = kind >= 3 ? "stone_road" : kind == 2 ? "packed_dirt_road" : "trampled_path";
            if (Art.Get("overlays.road_" + name) is { } road) Blit(target, road, 0, mask, x * d, y * d, d / Px, 0);
        }
    }

    readonly record struct Target(byte[] Px, byte[] Anchor, int X0, int Y0, int W, int H, int WorldW, int WorldH, int D)
    {

        public void Put(int gx, int gy, byte slot, int layer, int ax = 0, int ay = 0)
        {
            int ry = gy - Y0;
            if (ry < 0 || ry >= H) return;
            int rx = (((gx - X0) % WorldW) + WorldW) % WorldW;
            if (rx >= W) return;
            int o = (ry * W + rx) * 4;
            if (layer == 0) { Px[o] = slot; return; }
            Px[o + layer] = slot;
            Px[o + 3] |= (byte)layer;
            int dx = (int)MathF.Floor(gx / (float)D) - ax, dy = ay - gy / D;
            dx = -dx;
            Anchor[(ry * W + rx) * 2 + layer - 1] = dx is >= -2 and <= 2 && dy is >= 0 and <= 4 ? (byte)(1 + dx + 2 + 5 * dy) : (byte)0;
        }
    }

    sealed record Mat(string Id, int Priority, Sheet Plain, Sheet Corners, int Variants);

    static readonly Dictionary<(string, int), Mat?> Mats = new();

    static Mat? MatOf(string id, int density)
    {
        lock (Mats)
        {
            if (Mats.TryGetValue((id, density), out var m)) return m;
            var plain = Art.Lod($"landscape.{id}_plain", density);
            var corners = Art.Lod($"landscape.{id}_corners", density);
            m = plain == null || corners == null ? null
                : new Mat(id, plain.Int("layer_priority") * 1000 + plain.Int("priority_offset"), plain, corners,
                          Math.Clamp(plain.Int("variants_per_orientation", plain.Rows), 1, plain.Rows));
            return Mats[(id, density)] = m;
        }
    }

    static void Ground(Input input, Target t, int cx0, int cy0, int cw, int ch)
    {
        var w = input.World;
        int d = t.D;
        var fallback = MatOf("dirt", d)!;
        var mats = new Dictionary<int, Mat>();
        Mat MatAt(int i) => mats.TryGetValue(i, out var m) ? m : mats[i] = MatOf(Material(input, i), d) ?? fallback;
        Span<int> corner = stackalloc int[4];
        var present = new List<Mat>(4);
        for (int vy = cy0; vy <= Math.Min(cy0 + ch, w.H); vy++)
            for (int vx = cx0; vx <= cx0 + cw; vx++)
            {
                int top = Math.Max(vy - 1, 0), bottom = Math.Min(vy, w.H - 1);
                corner[0] = w.Index(vx - 1, top); corner[1] = w.Index(vx, top);
                corner[2] = w.Index(vx, bottom); corner[3] = w.Index(vx - 1, bottom);
                present.Clear();
                for (int k = 0; k < 4; k++) if (!present.Contains(MatAt(corner[k]))) present.Add(MatAt(corner[k]));
                present.Sort((a, b) => a.Priority.CompareTo(b.Priority));
                int ox = vx * d - d / 2, oy = vy * d - d / 2;
                uint h = Hash(w.Wrap(vx), vy);
                Blit(t, present[0].Plain, 0, (int)(h % (uint)present[0].Variants), ox, oy, 1, 0);
                for (int m = 1; m < present.Count; m++)
                {
                    int mask = 0;
                    for (int k = 0; k < 4; k++) if (MatAt(corner[k]).Priority >= present[m].Priority) mask |= 1 << k;
                    Blit(t, present[m].Corners, 0, mask, ox, oy, 1, 0);
                }
            }
    }

    static int[]? _riverMask;
    static WorldMap? _riverWorld;
    public static void InvalidateRivers() { lock (Mats) { _riverWorld = null; _riverMask = null; } }

    static int[] RiverMasks(WorldMap w)
    {
        lock (Mats)
        {
            if (_riverWorld == w && _riverMask != null) return _riverMask;
            var mask = new int[w.Count];
            int[] dx = { 0, 1, 0, -1 }, dy = { -1, 0, 1, 0 };
            for (int i = 0; i < w.Count; i++)
            {
                if (!IsRiver(w, i)) continue;
                int x = w.X(i), y = w.Y(i), best = -1, bestDir = -1;
                float bestFlow = w.Flow[i];
                for (int dir = 0; dir < 4; dir++)
                {
                    int ny = y + dy[dir];
                    if (ny < 0 || ny >= w.H) continue;
                    int n = w.Index(x + dx[dir], ny);
                    if (w.IsWater(n)) { if (best < 0 || w.IsLand(best)) { best = n; bestDir = dir; bestFlow = float.MaxValue; } }
                    else if (w.Flow[n] > bestFlow) { best = n; bestDir = dir; bestFlow = w.Flow[n]; }
                }
                if (best < 0) continue;
                mask[i] |= 1 << bestDir;
                if (IsRiver(w, best)) mask[best] |= 1 << ((bestDir + 2) % 4);
            }
            _riverWorld = w;
            return _riverMask = mask;
        }
    }

    public static bool IsRiver(WorldMap w, int i) => w.IsLand(i) && w.Biome[i] != Biome.Lake && w.Flow[i] >= WorldGen.RiverFlow * 2f;

    static void Rivers(WorldMap w, Target t, int cx0, int cy0, int cw, int ch)
    {
        var stream = Art.Get("overlays.river_stream");
        var river = Art.Get("overlays.river_river");
        var great = Art.Get("overlays.river_great_river");
        if (stream == null || river == null || great == null) return;
        var mask = RiverMasks(w);
        int scale = t.D / Px;
        for (int y = Math.Max(cy0, 0); y < Math.Min(cy0 + ch, w.H); y++)
            for (int x = cx0; x < cx0 + cw; x++)
            {
                int i = w.Index(x, y);
                if (mask[i] == 0) continue;
                var sheet = w.Flow[i] > WorldGen.RiverFlow * 16f ? great : w.Flow[i] > WorldGen.RiverFlow * 6f ? river : stream;
                Blit(t, sheet, 0, mask[i], x * t.D, y * t.D, scale, 0);
            }
    }

    readonly record struct Kind(string? Flora, string? Detail);

    static readonly Dictionary<string, Kind> Kinds = new()
    {
        ["conifer_mature"] = new("conifer_mature", "tree_conifer"),
        ["conifer_old"] = new("conifer_old", "tree_spruce"),
        ["conifer_young"] = new("conifer_young", "tree_fir"),
        ["conifer_sapling"] = new("conifer_sapling", "nature_fern"),
        ["pine"] = new("conifer_mature", "tree_pine"),
        ["cedar"] = new("conifer_old", "tree_cedar"),
        ["juniper"] = new(null, "tree_juniper"),
        ["broadleaf_mature"] = new("broadleaf_mature", "tree_broadleaf"),
        ["broadleaf_old"] = new("broadleaf_old", "tree_oak"),
        ["broadleaf_young"] = new("broadleaf_young", "tree_broadleaf"),
        ["birch"] = new("broadleaf_young", "tree_birch"),
        ["willow"] = new(null, "tree_willow"),
        ["fruit_tree"] = new(null, "tree_fruit_tree"),
        ["acacia_mature"] = new("acacia_mature", "tree_acacia"),
        ["acacia_young"] = new("acacia_young", "tree_acacia"),
        ["baobab"] = new(null, "tree_baobab"),
        ["palm_mature"] = new("palm_mature", "tree_palm"),
        ["jungle_tree_mature"] = new("jungle_tree_mature", "tree_jungle"),
        ["jungle_tree_young"] = new("jungle_tree_young", "tree_jungle"),
        ["rainforest_giant_mature"] = new("rainforest_giant_mature", "tree_jungle"),
        ["mangrove"] = new(null, "tree_mangrove"),
        ["shrub"] = new("shrub", "nature_flowering_shrub"),
        ["shrub_dry"] = new("shrub_dry", "nature_dry_shrub"),
        ["thorn_shrub"] = new(null, "nature_thorn_shrub"),
        ["berry_bush_fruiting"] = new("berry_bush_fruiting", "nature_berry_shrub"),
        ["berry_bush_flowering"] = new("berry_bush_flowering", "nature_berry_shrub"),
        ["tall_grass"] = new("tall_grass", "nature_meadow_grass"),
        ["dry_grass"] = new("tall_grass", "nature_dry_grass"),
        ["wild_grain_ripe"] = new("wild_grain_ripe", null),
        ["flowers"] = new("flowers", "nature_wildflowers"),
        ["clover"] = new(null, "nature_clover"),
        ["sedge"] = new(null, "nature_sedge"),
        ["weeds"] = new(null, "nature_weeds"),
        ["fern"] = new(null, "nature_fern"),
        ["heath"] = new(null, "nature_heath"),
        ["moss"] = new(null, "nature_moss"),
        ["reeds"] = new("reeds", "nature_reeds"),
        ["cactus"] = new("cactus", "nature_cactus"),
        ["cactus_tall"] = new("cactus_tall", "nature_cactus"),
        ["cactus_flowering"] = new("cactus_flowering", "nature_cactus"),
        ["succulent"] = new(null, "nature_succulent"),
        ["mushrooms"] = new("mushrooms", "nature_mushroom_patch"),
        ["fallen_log"] = new("fallen_log", "nature_fallen_log"),
        ["stump"] = new("stump", "nature_tree_stump"),
        ["conifer_dead"] = new("conifer_dead", null),
        ["boulder"] = new(null, "boulder_detail"),
        ["scree"] = new(null, "nature_scree"),
        ["granite_outcrop"] = new(null, "nature_granite_outcrop"),
        ["sandstone_outcrop"] = new(null, "nature_sandstone_outcrop"),
        ["basalt_outcrop"] = new(null, "nature_basalt_outcrop"),
        ["ore_outcrop"] = new(null, "nature_ore_outcrop"),
        ["clay_deposit"] = new(null, "nature_clay_deposit"),
    };

    static bool IsRock(string kind) => kind is "boulder" or "scree" or "granite_outcrop" or "sandstone_outcrop" or "basalt_outcrop" or "ore_outcrop";

    static bool IsTree(string kind) =>
        Kinds.TryGetValue(kind, out var k) ? k.Detail?.StartsWith("tree_") == true || k.Flora?.Contains("_mature") == true || k.Flora?.Contains("_old") == true
                                           : kind.Contains("_burned") || kind == "conifer_dead";

    static ((string id, float weight)[] kinds, int count) FloraOf(Biome b) => b switch
    {
        Biome.BorealForest => (new[] { ("conifer_mature", 4f), ("conifer_old", 2f), ("conifer_young", 2f), ("pine", 1f), ("birch", 0.6f), ("conifer_sapling", 0.6f), ("shrub", 0.5f), ("moss", 0.4f), ("fallen_log", 0.2f) }, 2),
        Biome.TemperateForest => (new[] { ("broadleaf_mature", 4f), ("broadleaf_old", 1.2f), ("broadleaf_young", 2f), ("birch", 0.6f), ("conifer_mature", 0.8f), ("shrub", 1f), ("fern", 0.6f), ("berry_bush_fruiting", 0.4f), ("mushrooms", 0.3f), ("fallen_log", 0.3f) }, 2),
        Biome.TemperateRainforest => (new[] { ("conifer_old", 3f), ("cedar", 1.5f), ("conifer_mature", 3f), ("broadleaf_old", 1f), ("fern", 1.2f), ("moss", 0.6f), ("fallen_log", 0.5f), ("mushrooms", 0.5f) }, 3),
        Biome.TemperateGrassland => (new[] { ("tall_grass", 4f), ("flowers", 1.5f), ("clover", 0.8f), ("shrub", 0.8f), ("broadleaf_young", 0.3f), ("wild_grain_ripe", 0.5f), ("boulder", 0.1f) }, 2),
        Biome.Shrubland => (new[] { ("shrub", 3f), ("shrub_dry", 2f), ("heath", 1.2f), ("juniper", 0.5f), ("berry_bush_flowering", 0.6f), ("dry_grass", 1f), ("boulder", 0.2f) }, 2),
        Biome.HotDesert => (new[] { ("cactus", 2f), ("cactus_tall", 1f), ("cactus_flowering", 0.4f), ("succulent", 0.8f), ("shrub_dry", 1f), ("sandstone_outcrop", 0.3f) }, 1),
        Biome.ColdDesert => (new[] { ("shrub_dry", 3f), ("dry_grass", 0.5f), ("scree", 0.8f), ("juniper", 0.3f), ("boulder", 0.4f) }, 1),
        Biome.Savanna => (new[] { ("dry_grass", 4f), ("acacia_mature", 1.2f), ("acacia_young", 0.6f), ("baobab", 0.2f), ("thorn_shrub", 0.8f), ("shrub_dry", 0.6f) }, 2),
        Biome.TropicalSeasonalForest => (new[] { ("jungle_tree_mature", 3f), ("broadleaf_mature", 2f), ("palm_mature", 1f), ("shrub", 1f), ("fern", 0.6f) }, 2),
        Biome.TropicalRainforest => (new[] { ("rainforest_giant_mature", 1.5f), ("jungle_tree_mature", 3f), ("jungle_tree_young", 1f), ("palm_mature", 1f), ("fern", 1f) }, 3),
        Biome.Wetland => (new[] { ("reeds", 4f), ("sedge", 2f), ("tall_grass", 1f), ("willow", 0.5f), ("broadleaf_young", 0.3f) }, 2),
        Biome.Tundra => (new[] { ("shrub_dry", 1f), ("sedge", 0.8f), ("moss", 0.8f), ("tall_grass", 0.4f), ("mushrooms", 0.2f), ("boulder", 0.4f) }, 1),
        Biome.Alpine => (new[] { ("conifer_young", 1f), ("juniper", 0.4f), ("shrub_dry", 1f), ("scree", 0.8f), ("granite_outcrop", 0.5f), ("ore_outcrop", 0.15f) }, 1),
        Biome.Volcanic => (new[] { ("conifer_dead", 1f), ("stump", 1f), ("basalt_outcrop", 1f) }, 1),
        _ => (Array.Empty<(string, float)>(), 0),
    };

    static string? Special(WorldMap w, int i, uint h)
    {
        if (w.Biome[i] == Biome.Wetland && w.TempMean[i] > 22f && Shore(w, i) && (h & 3) == 0) return "mangrove";
        if (IsRiver(w, i) && w.Biome[i] is Biome.TemperateForest or Biome.TemperateGrassland or Biome.Shrubland && (h & 7) == 0) return "willow";
        if (IsRiver(w, i) && (h & 15) == 1) return "clay_deposit";
        return null;
    }

    public readonly record struct Plant(int X, int Y, string Kind, int Variant, bool Snowy)
    {

        public (int x, int y) Cell => ((int)MathF.Floor(X / (float)Px), Y / Px);
    }

    static string? CropStage(float growth) => growth <= 0.01f ? null : growth < 0.3f ? "seedling" : growth < 0.8f ? "growing" : "ripe";

    public static string CropArt(string crop) => crop switch
    {
        "wheat" or "barley" or "oats" or "rye" or "rice" or "millet" or "maize" or "grain" => "grain",
        "potato" or "beet" or "carrot" or "onion" or "roots" or "cabbage" or "lettuce" => "tuber",
        "pumpkin" or "squash" or "melon" or "beans" or "peas" or "lentils" or "tomato" or "pepper" => "gourd",
        _ => "fiber",
    };

    public sealed record PlantArt(Sheet Summer, Sheet? Off, Sheet? Cap, int Scale, int Row);

    static readonly Dictionary<(string, int, bool), (Sheet, Sheet?, Sheet?, int)?> ArtCache = new();

    public static PlantArt? ArtOf(Plant p, int d)
    {
        (Sheet, Sheet?, Sheet?, int)? art;
        lock (ArtCache)
            if (!ArtCache.TryGetValue((p.Kind, d, p.Snowy), out art)) ArtCache[(p.Kind, d, p.Snowy)] = art = Resolve(p.Kind, d, p.Snowy);
        if (art is not var (summer, off, cap, scale)) return null;
        return new PlantArt(summer, off, cap, scale, p.Variant % summer.Rows);
    }

    static (Sheet, Sheet?, Sheet?, int)? Resolve(string kindId, int d, bool snowy)
    {
        int scale = d / Px;
        if (kindId.StartsWith("crop:"))
        {

            string art = kindId[5..];
            if (art.StartsWith("grain_") && !art.EndsWith("seedling") && d > Px && Art.Lod("civilisation.crop_grain_detail", d) is { } detail) return (detail, detail, null, 1);
            if (Art.Get($"flora.crop_{art}") is not { } crop) return null;
            return (crop, crop, null, scale);
        }
        var kind = Kinds.GetValueOrDefault(kindId, new Kind(kindId, null));
        if ((d == Px || kind.Detail == null) && kind.Flora != null && Art.Get("flora." + kind.Flora) is { } f)
        {
            Sheet off = f;
            if (f.Meta.TryGetProperty("seasonal_states", out var states))
                foreach (var st in states.EnumerateObject()) off = Art.Get(st.Value.GetString()!) ?? f;
            Sheet? cap = snowy && d == Px && kind.Flora.StartsWith("conifer_") && kind.Flora is not ("conifer_sapling" or "conifer_dead" or "conifer_burned")
                ? Art.Get("flora.conifer_snow_capped") : null;
            return (f, off, cap, scale);
        }
        if (kind.Detail != null && Art.Lod("civilisation." + kind.Detail, d) is { } detailArt) return (detailArt, detailArt, null, 1);
        return null;
    }

    public static void PlantsOf(Input input, int x, int y, List<Plant> into)
    {
        var w = input.World;
        if (y < 0 || y >= w.H) return;
        int i = w.Index(x, y);
        if (input.Fields.TryGetValue(i, out var field))
        {
            if (field.orchard) into.Add(new Plant(x * Px + 8, y * Px + 14, "fruit_tree", 0, false));
            else if (CropStage(field.growth) is { } stage) into.Add(new Plant(x * Px + 8, y * Px + 15, $"crop:{CropArt(field.crop)}_{stage}", 0, false));
            return;
        }
        if (input.Occupied.Contains(i)) return;
        if (!w.IsLand(i) || w.Volcano[i] || w.Biome[i] == Biome.Lake || Beach(w, i)) return;
        var (kinds, count) = FloraOf(w.Biome[i]);
        if (count == 0) return;
        bool burned = w.Ash[i] > 0.25f;
        bool snowy = w.TempMean[i] - w.TempAmp[i] < -3f;
        float total = 0f;
        foreach (var k in kinds) total += k.weight;
        for (int n = 0; n < count; n++)
        {
            uint h = Hash(i, n * 7 + 3);
            if ((h & 0xFF) < 40) continue;
            float pick = (h >> 8 & 0xFFFF) / 65536f * total;
            string id = kinds[^1].id;
            foreach (var (kid, weight) in kinds) { if (pick < weight) { id = kid; break; } pick -= weight; }
            if (n == 0 && Special(w, i, h >> 24) is { } special) id = special;
            if (burned) id = BurnedOf(id);

            if (IsTree(id) && (Hash(i, n * 3 + 101) & 0xFF) >= input.Trees[i]) continue;
            if (IsRock(id) && (Hash(i, n * 3 + 103) & 0xFF) >= input.Rocks[i]) continue;
            int px = x * Px + 2 + (int)(Hash(i, n * 2 + 11) % 12u);
            int py = y * Px + 3 + (int)(Hash(i, n * 5 + 17) % 13u);
            into.Add(new Plant(px, py, id, (int)(Hash(n, i * 13 + 1) & 0xFFFF), snowy));
        }
    }

    static void Flora(Input input, Target t, int cx0, int cy0, int cw, int ch)
    {
        var w = input.World;
        var plants = new List<Plant>();

        for (int y = Math.Max(cy0 - 1, 0); y < Math.Min(cy0 + ch + 3, w.H); y++)
            for (int x = cx0 - 2; x < cx0 + cw + 2; x++) PlantsOf(input, x, y, plants);
        plants.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
        int d = t.D, scale = d / Px;
        foreach (var p in plants)
        {
            if (ArtOf(p, d) is not { } art) continue;
            int gx = p.X * scale, gy = p.Y * scale;
            var (ax, ay) = p.Cell;
            Blit(t, art.Summer, 0, art.Row, gx - art.Summer.PivotX * art.Scale, gy - art.Summer.PivotY * art.Scale, art.Scale, 1, ax, ay);
            if (art.Off is { } off) Blit(t, off, 0, p.Variant % off.Rows, gx - off.PivotX * art.Scale, gy - off.PivotY * art.Scale, art.Scale, 2, ax, ay);
            if (art.Cap is { } cap) Blit(t, cap, 0, p.Variant % cap.Rows, gx - cap.PivotX * art.Scale, gy - cap.PivotY * art.Scale, art.Scale, 2, ax, ay);
        }
    }

    static string BurnedOf(string id)
    {
        foreach (var tree in new[] { "rainforest_giant", "jungle_tree", "broadleaf", "conifer", "acacia", "palm", "shrub" })
            if (id.StartsWith(tree)) return tree + "_burned";
        return id is "pine" or "cedar" or "juniper" ? "conifer_burned" : id is "birch" or "willow" or "baobab" or "fruit_tree" or "mangrove" ? "broadleaf_burned" : "stump";
    }

    static void Blit(Target t, Sheet sheet, int frame, int row, int ox, int oy, int scale, int layer, int ax = 0, int ay = 0)
    {
        if (row >= sheet.Rows || frame >= sheet.Frames) return;
        int fw = sheet.FrameW, fh = sheet.FrameH;

        if (oy + fh * scale <= t.Y0 || oy >= t.Y0 + t.H) return;
        for (int y = 0; y < fh; y++)
            for (int x = 0; x < fw; x++)
            {
                byte slot = sheet.Slot(frame, row, x, y);
                if (slot == Art.Empty) continue;
                for (int sy = 0; sy < scale; sy++)
                    for (int sx = 0; sx < scale; sx++)
                        t.Put(ox + x * scale + sx, oy + y * scale + sy, slot, layer, ax, ay);
            }
    }

    public static uint Hash(int a, int b)
    {
        uint h = (uint)(a * 374761393 + b * 668265263);
        h = (h ^ (h >> 13)) * 1274126177u;
        return h ^ (h >> 16);
    }
}
