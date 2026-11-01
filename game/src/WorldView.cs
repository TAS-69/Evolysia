using Evolysia.Sim;
using Evolysia.Sim.Core;
using Evolysia.Sim.World;
using Godot;

namespace Evolysia.Game;

public enum Overlay { Biome, Elevation, Temperature, Rainfall, Food, Cultures }

public partial class WorldView : Sprite2D
{
    public const int SubPx = 6;
    public Overlay Overlay { get; set; } = Overlay.Biome;

    Image? _image;
    ImageTexture? _texture;
    byte[] _rgba = Array.Empty<byte>();
    int[] _downstream = Array.Empty<int>();
    Color[] _cellColor = Array.Empty<Color>();
    float[] _height = Array.Empty<float>();
    WorldMap? _world;

    public void Bind(WorldMap world)
    {
        _world = world;
        int w = world.W * SubPx, h = world.H * SubPx;
        _rgba = new byte[w * h * 4];
        _image = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        _texture = ImageTexture.CreateFromImage(_image);
        Texture = _texture;
        Centered = false;
        Scale = Vector2.One * (Main.CellPx / (float)SubPx);
        TextureFilter = TextureFilterEnum.Linear;
        TextureRepeat = TextureRepeatEnum.Enabled;

        foreach (var child in GetChildren()) child.QueueFree();
        foreach (int side in new[] { -1, 1 })
            AddChild(new Sprite2D
            {
                Texture = _texture, Centered = false, Position = new Vector2(side * w, 0),
                TextureFilter = TextureFilterEnum.Linear, TextureRepeat = TextureRepeatEnum.Enabled,
            });
        ComputeRivers(world);
    }

    void ComputeRivers(WorldMap w)
    {
        _downstream = new int[w.Count];
        Span<int> nb = stackalloc int[8];
        for (int i = 0; i < w.Count; i++)
        {
            _downstream[i] = -1;
            if (!w.IsLand(i) || w.Flow[i] < WorldGen.RiverFlow * 2f) continue;

            int k = w.Neighbors(i, nb);
            float best = w.Flow[i];
            for (int j = 0; j < k; j++)
                if (w.IsLand(nb[j]) && w.Biome[nb[j]] != Biome.Lake && w.Flow[nb[j]] > best) { best = w.Flow[nb[j]]; _downstream[i] = nb[j]; }
            if (_downstream[i] < 0)
                for (int j = 0; j < k; j++)
                    if (w.IsWater(nb[j])) { _downstream[i] = nb[j]; break; }
        }
    }

    public void Redraw(Simulation sim)
    {
        if (_world == null || _image == null) return;
        var w = _world;
        int day = sim.Day;
        int texW = w.W * SubPx;
        Dictionary<int, Color>? territory = Overlay == Overlay.Cultures ? Territory(sim) : null;

        if (_cellColor.Length != w.Count) { _cellColor = new Color[w.Count]; _height = new float[w.Count]; }
        for (int i = 0; i < w.Count; i++)
        {
            _cellColor[i] = CellColor(sim, i, day, territory) * Shade(w, i);
            _height[i] = w.Biome[i] == Biome.Lake ? -60f : w.Elevation[i];
        }

        int texH = w.H * SubPx;
        for (int py = 0; py < texH; py++)
        {
            float fy = (py + 0.5f) / SubPx - 0.5f;
            int y0 = (int)MathF.Floor(fy);
            float ty = fy - y0;
            int ya = Math.Clamp(y0, 0, w.H - 1), yb = Math.Clamp(y0 + 1, 0, w.H - 1);
            for (int px = 0; px < texW; px++)
            {
                float fx = (px + 0.5f) / SubPx - 0.5f;
                int x0 = (int)MathF.Floor(fx);
                float tx = fx - x0;
                int c00 = w.Index(x0, ya), c10 = w.Index(x0 + 1, ya), c01 = w.Index(x0, yb), c11 = w.Index(x0 + 1, yb);
                float w00 = (1 - tx) * (1 - ty), w10 = tx * (1 - ty), w01 = (1 - tx) * ty, w11 = tx * ty;
                float h = _height[c00] * w00 + _height[c10] * w10 + _height[c01] * w01 + _height[c11] * w11
                          + (Hash(px * 3, py * 7) - 0.5f) * 120f;
                bool land = h >= 0f;
                Color sum = new(0, 0, 0, 0);
                float total = 0f;
                Accumulate(c00, w00); Accumulate(c10, w10); Accumulate(c01, w01); Accumulate(c11, w11);
                var c = total > 0f ? sum / total : _cellColor[w.Index((int)MathF.Round(fx), Math.Clamp((int)MathF.Round(fy), 0, w.H - 1))];
                float jitter = 1f + (Hash(px, py) - 0.5f) * 0.06f;
                Put(px, py, texW, c * jitter);

                void Accumulate(int cell, float weight)
                {
                    if ((_height[cell] >= 0f) != land || weight <= 0f) return;
                    sum += _cellColor[cell] * weight;
                    total += weight;
                }
            }
        }
        if (Overlay is Overlay.Biome or Overlay.Cultures) DrawRivers(w, texW);

        _image.SetData(texW, w.H * SubPx, false, Image.Format.Rgba8, _rgba);
        _texture!.Update(_image);
    }

    Color CellColor(Simulation sim, int i, int day, Dictionary<int, Color>? territory)
    {
        var w = _world!;
        float t = w.Temperature(i, day);
        switch (Overlay)
        {
            case Overlay.Elevation:
                return w.IsLand(i)
                    ? new Color(0.35f, 0.5f, 0.3f).Lerp(new Color(0.95f, 0.92f, 0.88f), Math.Clamp(w.Elevation[i] / 4000f, 0f, 1f))
                    : new Color(0.1f, 0.2f, 0.45f).Lerp(new Color(0.25f, 0.45f, 0.7f), Math.Clamp(1f + w.Elevation[i] / 4500f, 0f, 1f));
            case Overlay.Temperature:
                return Gradient(Math.Clamp((t + 25f) / 60f, 0f, 1f), new Color("3b4cc0"), new Color("dddddd"), new Color("b40426"));
            case Overlay.Rainfall:
                if (!w.IsLand(i)) return new Color(0.12f, 0.15f, 0.22f);
                return Gradient(Math.Clamp(w.Precip[i] / 2500f, 0f, 1f), new Color("d9b26f"), new Color("8fc27a"), new Color("2c6fa8"));
            case Overlay.Food:
                if (!w.IsLand(i) || w.PlantK[i] <= 0f) return w.IsLand(i) ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.12f, 0.15f, 0.22f);
                return Gradient(Math.Clamp(w.PlantStock[i] / w.PlantK[i], 0f, 1f), new Color("6b3b2a"), new Color("c9b45a"), new Color("2f8f3a"));
        }

        Color c = Palette.Biome(w.Biome[i]);
        if (!w.IsLand(i))
        {
            if (t < -6f && w.Biome[i] != Biome.DeepOcean) c = c.Lerp(new Color("dfe8ee"), Math.Clamp((-6f - t) / 8f, 0f, 0.9f));
        }
        else
        {
            bool vegetated = w.PlantK[i] > 0f && w.Biome[i] is not (Biome.HotDesert or Biome.ColdDesert or Biome.IceSheet or Biome.Alpine);
            if (vegetated)
            {
                float growth = Math.Clamp(w.Growth(i, day), 0f, 1.2f);
                var season = w.SeasonOf(i, day);
                if (season == Season.Autumn && w.Biome[i] is Biome.TemperateForest or Biome.TemperateGrassland or Biome.Shrubland)
                    c = c.Lerp(new Color("b5793a"), 0.35f);
                else if (growth < 0.5f) c = c.Lerp(t > 5f ? new Color("c2ad6e") : new Color("7d7464"), (0.5f - growth) * 0.9f);
                else c = c.Lerp(new Color("6fae4a"), (growth - 0.5f) * 0.25f);
            }
            if (t < -1f) c = c.Lerp(new Color("f3f5f7"), Math.Clamp((-1f - t) / 7f, 0f, 0.92f));
            if (w.Ash[i] > 0.05f) c = c.Lerp(new Color("4a4440"), Math.Clamp(w.Ash[i], 0f, 0.8f));
            if (w.Volcano[i]) c = VolcanoPeak(w, i) ? new Color("e0552d") : c.Lerp(new Color("3b3230"), 0.7f);
        }
        if (territory != null)
        {
            c = c.Lerp(new Color(c.Luminance, c.Luminance, c.Luminance), 0.55f);
            if (territory.TryGetValue(i, out var tint)) c = c.Lerp(tint, 0.45f);
        }
        return c;
    }

    static bool VolcanoPeak(WorldMap w, int i)
    {
        int x = w.X(i), y = w.Y(i);
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                if ((dx != 0 || dy != 0) && y + dy >= 0 && y + dy < w.H && w.Elevation[w.Index(x + dx, y + dy)] > w.Elevation[i]) return false;
        return true;
    }

    internal static Dictionary<int, Color> Territory(Simulation sim)
    {
        var map = new Dictionary<int, Color>();
        var w = sim.World;
        foreach (var band in sim.Bands.Values)
        {
            var col = Palette.Culture(sim.Cultures[band.CultureId].Hue);
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int y = w.Y(band.Cell) + dy;
                    if (y < 0 || y >= w.H) continue;
                    int c = w.Index(w.X(band.Cell) + dx, y);
                    if (w.IsLand(c) && !map.ContainsKey(c)) map[c] = col;
                }
        }
        return map;
    }

    internal static float Shade(WorldMap w, int i)
    {
        if (!w.IsLand(i)) return 1f;
        int x = w.X(i), y = w.Y(i);
        int nw = w.Index(x - 1, Math.Max(0, y - 1));
        float slope = (w.Elevation[i] - MathF.Max(0f, w.Elevation[nw])) / 1200f;
        return Math.Clamp(1f + slope * 0.6f, 0.7f, 1.25f);
    }

    void DrawRivers(WorldMap w, int texW)
    {
        var water = new Color("4f86a8");
        for (int i = 0; i < w.Count; i++)
        {
            int d = _downstream[i];
            if (d < 0) continue;
            int x0 = w.X(i) * SubPx + SubPx / 2, y0 = w.Y(i) * SubPx + SubPx / 2;
            int dx = w.X(d) - w.X(i);
            if (dx > 1) dx -= w.W; else if (dx < -1) dx += w.W;
            int x1 = x0 + dx * SubPx, y1 = y0 + (w.Y(d) - w.Y(i)) * SubPx;
            bool big = w.Flow[i] > WorldGen.RiverFlow * 8f;
            for (int s = 0; s <= SubPx; s++)
            {
                int px = x0 + (x1 - x0) * s / SubPx, py = y0 + (y1 - y0) * s / SubPx;
                Put(px, py, texW, water);
                if (big) Put(px + 1, py, texW, water);
            }
        }
    }

    void Put(int px, int py, int texW, Color c)
    {
        int texH = _rgba.Length / 4 / texW;
        px = ((px % texW) + texW) % texW;
        if (py < 0 || py >= texH) return;
        int o = (py * texW + px) * 4;
        _rgba[o] = (byte)Math.Clamp(c.R * 255f, 0f, 255f);
        _rgba[o + 1] = (byte)Math.Clamp(c.G * 255f, 0f, 255f);
        _rgba[o + 2] = (byte)Math.Clamp(c.B * 255f, 0f, 255f);
        _rgba[o + 3] = 255;
    }

    static Color Gradient(float t, Color a, Color b, Color c) => t < 0.5f ? a.Lerp(b, t * 2f) : b.Lerp(c, (t - 0.5f) * 2f);

    static float Hash(int x, int y)
    {
        uint h = (uint)(x * 374761393 + y * 668265263);
        h = (h ^ (h >> 13)) * 1274126177u;
        return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
    }

    public string DescribeCell(Simulation sim, int i)
    {
        var w = _world!;
        int day = sim.Day;
        string name = Ids.Snake(w.Biome[i]).Replace('_', ' ');
        var s = $"[b]{char.ToUpper(name[0]) + name[1..]}[/b]  ({w.X(i)}, {w.Y(i)})\n";
        s += $"Elevation {w.Elevation[i]:0} m · {w.SeasonOf(i, day).ToString().ToLower()}\n";
        s += $"Today {w.Temperature(i, day):0} °C (mean {w.TempMean[i]:0}, ±{w.TempAmp[i]:0})\n";
        if (w.IsLand(i))
        {
            s += $"Rain {w.Precip[i]:0} mm/yr · this year ×{w.YearRain[i]:0.00}\n";
            s += $"Fresh water {w.WaterAccess[i] * 100:0}%{(w.Flow[i] > WorldGen.RiverFlow ? " · river" : "")}\n";
            if (w.PlantK[i] > 0) s += $"Plants {w.PlantStock[i] / w.PlantK[i] * 100:0}% · game {w.PreyStock[i] / MathF.Max(1f, w.PreyK[i]) * 100:0}% · predators {w.Predators[i] * 100:0}%\n";
            if (w.FishK[i] > 0) s += $"Fish {w.FishStock[i] / w.FishK[i] * 100:0}%\n";
            if (w.Volcano[i]) s += "[color=#b5553c]Volcano[/color]\n";
            if (w.Fault[i] > 0.6f) s += "Fault line: earthquakes possible\n";
        }

        var works = sim.Structures.Values.Where(st => st.Cell == i || st.Id == w.StructureAt[i]).Take(6).ToArray();
        foreach (var work in works)
        {
            s += $"\n[b]{Ui.Plain(work.CustomName.Length > 0 ? work.CustomName : Describe.Title(work.Kind.Id))}[/b] · {work.State}\n";
            foreach (var (good, quantity) in work.Contents.Held().Take(8)) s += $"{Describe.Title(good.Id)} {quantity:0.0}\n";
        }
        if (sim.GroundStock.TryGetValue(i, out var loose))
        {
            s += "\n[b]Loose stock[/b]\n";
            foreach (var (good, quantity) in loose.Held().Take(12)) s += $"{Describe.Title(good.Id)} {quantity:0.0}\n";
        }
        foreach (var ins in sim.Inscriptions)
        {
            if (ins.Medium == MessageMedium.Vision || w.Distance(ins.Cell, i) > 2) continue;
            string medium = ins.Medium == MessageMedium.Stone ? "carved in stone" : ins.Visible(day) ? "written on the ground" : "once written here, now weathered away";
            s += $"\n[color={Palette.Hex(Palette.Divine)}]Your words, {medium} (year {ins.Day / SimConfig.DaysPerYear}):[/color]\n[i]“{Ui.Plain(ins.Text)}”[/i]\n";
            foreach (var (rday, bandId, reading, understood) in ins.Readings)
            {
                string who = sim.Bands.TryGetValue(bandId, out var b) ? $"[url=band:{bandId}]the {b.Name}[/url]" : "a band now gone";
                s += $"· {who}: {Ids.Snake(reading)}{(understood ? "" : " (could not read it)")}, year {rday / SimConfig.DaysPerYear}\n";
            }
        }
        return s;
    }
}
