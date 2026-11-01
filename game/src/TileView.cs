using Evolysia.Sim;
using Evolysia.Sim.Core;
using Evolysia.Sim.World;
using Godot;

namespace Evolysia.Game;

public partial class TileView : Node2D
{

    static int ChunkCells(int density) => 512 / density;
    static readonly string[] Animated = { "deep_water", "water", "shallow_water", "lava" };

    WorldMap? _world;
    TileBake.Input? _input;
    Image? _cellA, _cellB, _cellC;
    ImageTexture? _texA, _texB, _texC, _lut;
    byte[] _a = Array.Empty<byte>(), _b = Array.Empty<byte>(), _c = Array.Empty<byte>();
    byte[] _kind = Array.Empty<byte>();
    int[] _rows = Array.Empty<int>();
    int _inputDay = int.MinValue / 2;
    bool _desaturate;
    float _zoom = 1f;
    Vector2 _viewCentre, _viewSize;
    readonly Level[] _levels = new Level[3];

    sealed class Level
    {
        public int D;
        public ImageTexture Snow = null!, Ice = null!, Anim = null!, AnimSlots = null!;
        public readonly Dictionary<(int, int), Chunk> Chunks = new();
        public float Fade;
        public Node2D Node = null!;
    }

    sealed class Chunk
    {
        public int Cx, Cy, Cw, Ch;
        public bool Baked, Stale;
        public long Signature;
        public Sprite2D? Sprite;
        public ShaderMaterial? Material;
        public ImageTexture? Texture, Roots;
        public Task<TileBake.Baked>? Task;
        public ulong LastSeen;
    }

    public bool Active => _world != null && Art.Available;

    public TileBake.Input? Input => _input;

    public (int rowA, int rowB, float blend, bool off) SeasonOf(int cell)
    {
        if (_a.Length <= cell * 4) return (Art.ReferenceRow, Art.ReferenceRow, 0f, false);
        int o = cell * 4;
        return (_a[o], _a[o + 1], _a[o + 2] / 255f, (_c[o + 1] & 1) != 0);
    }

    public bool Busy { get; private set; }

    public int Density => _zoom >= 6f ? 64 : _zoom >= 3f ? 32 : 16;

    public void Bind(WorldMap world, TileBake.Input input)
    {
        foreach (var level in _levels.Where(l => l != null)) level.Node.QueueFree();
        _world = world;
        _input = input;
        if (!Art.Available) { Visible = false; return; }
        _ = Art.SlotNames;
        BuildRows();
        (_cellA, _texA, _a) = CellTexture(world);
        (_cellB, _texB, _b) = CellTexture(world);
        (_cellC, _texC, _c) = CellTexture(world);
        _lut = ImageTexture.CreateFromImage(Art.Nature.Image);
        for (int k = 0; k < 3; k++) _levels[k] = NewLevel(Art.Densities[k]);
        RefreshKinds();

    }

    Level NewLevel(int d)
    {
        var level = new Level { D = d, Node = new Node2D { Name = $"Density{d}" } };
        AddChild(level.Node);
        level.Snow = TileSlots(Art.Lod("landscape.snow_plain", d), d);
        level.Ice = TileSlots(Art.Lod("landscape.ice_plain", d), d);

        var anim = new byte[4 * d * Animated.Length * 4 * d * 4];
        var owned = new byte[256 * 4];
        for (int k = 0; k < Animated.Length; k++)
        {
            if (Art.Lod($"landscape.{Animated[k]}_plain", d) is not { } sheet) continue;
            for (int v = 0; v < 4; v++)
                for (int f = 0; f < 4; f++)
                    for (int y = 0; y < d; y++)
                        for (int x = 0; x < d; x++)
                        {
                            byte slot = sheet.Slot(f % sheet.Frames, v % sheet.Rows, x, y);
                            if (slot == Art.Empty) continue;
                            int o = (((k * 4 + v) * d + y) * 4 * d + f * d + x) * 4;
                            anim[o] = slot; anim[o + 3] = 255;
                            owned[slot * 4] |= (byte)(1 << k);
                            owned[slot * 4 + 3] = 255;
                        }
        }
        level.Anim = ImageTexture.CreateFromImage(Image.CreateFromData(4 * d, Animated.Length * 4 * d, false, Image.Format.Rgba8, anim));
        level.AnimSlots = ImageTexture.CreateFromImage(Image.CreateFromData(256, 1, false, Image.Format.Rgba8, owned));
        return level;
    }

    void Finish(Level level, Chunk chunk, TileBake.Baked baked)
    {
        int d = level.D;
        var image = Image.CreateFromData(chunk.Cw * d, chunk.Ch * d, false, Image.Format.Rgba8, baked.Px);
        var roots = Image.CreateFromData(chunk.Cw * d, chunk.Ch * d, false, Image.Format.Rg8, baked.Anchor);
        if (chunk.Texture == null || chunk.Texture.GetWidth() != image.GetWidth())
        {
            chunk.Texture = ImageTexture.CreateFromImage(image);
            chunk.Roots = ImageTexture.CreateFromImage(roots);
        }
        else { chunk.Texture.Update(image); chunk.Roots!.Update(roots); }
        if (chunk.Sprite == null)
        {
            chunk.Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/tiles.gdshader") };
            chunk.Sprite = new Sprite2D
            {
                Centered = false, Material = chunk.Material, TextureFilter = TextureFilterEnum.Nearest,
                Scale = Vector2.One * (Main.CellPx / (float)d),
            };
            level.Node.AddChild(chunk.Sprite);
            var m = chunk.Material;
            m.SetShaderParameter("lut", _lut!);
            m.SetShaderParameter("cell_a", _texA!);
            m.SetShaderParameter("cell_b", _texB!);
            m.SetShaderParameter("cell_c", _texC!);
            m.SetShaderParameter("snow_tile", level.Snow);
            m.SetShaderParameter("ice_tile", level.Ice);
            m.SetShaderParameter("anim", level.Anim);
            m.SetShaderParameter("anim_slots", level.AnimSlots);
            m.SetShaderParameter("tile", d);
            m.SetShaderParameter("origin", new Vector2I(chunk.Cx, chunk.Cy));
            m.SetShaderParameter("cells", new Vector2I(_world!.W, _world.H));
            m.SetShaderParameter("wrap", false);
            m.SetShaderParameter("fade", level.Fade);
            m.SetShaderParameter("desaturate", _desaturate);
        }
        chunk.Sprite.Texture = chunk.Texture;
        chunk.Material!.SetShaderParameter("world", chunk.Texture);
        chunk.Material.SetShaderParameter("roots", chunk.Roots!);
        chunk.Baked = true;
        foreach (var copy in chunk.Sprite.GetChildren().OfType<Sprite2D>()) copy.Texture = chunk.Texture;
    }

    public void SetView(Vector2 centre, Vector2 size, float zoom)
    {
        _zoom = zoom;
        _viewCentre = centre;
        _viewSize = size;
        if (!Active) return;
        _levels[0].Fade = Math.Clamp((zoom - 1.1f) / 0.5f, 0f, 1f);
        _levels[1].Fade = Math.Clamp((zoom - 3f) / 0.35f, 0f, 1f);
        _levels[2].Fade = Math.Clamp((zoom - 6f) / 0.6f, 0f, 1f);
    }

    public override void _Process(double delta)
    {
        if (!Active || _world == null) return;
        ulong now = Time.GetTicksMsec();
        bool coveredAbove = false;
        bool busy = false;
        for (int k = 2; k >= 0; k--)
        {
            var level = _levels[k];
            bool wanted = level.Fade > 0f && Visible;
            bool complete = true;
            if (wanted) complete = UpdateChunks(level, now);
            busy |= !complete;
            foreach (var chunk in level.Chunks.Values)
            {
                if (chunk.Sprite == null) continue;
                bool show = wanted && !coveredAbove && chunk.LastSeen + 500 > now;
                chunk.Sprite.Visible = show;
                if (show) chunk.Material!.SetShaderParameter("fade", level.Fade);
            }

            if (wanted && level.Fade >= 1f && complete) coveredAbove = true;
        }
        Busy = busy;
    }

    bool UpdateChunks(Level level, ulong now)
    {
        var w = _world!;
        int size = ChunkCells(level.D);
        int chunksX = (w.W + size - 1) / size, chunksY = (w.H + size - 1) / size;
        float cell = Main.CellPx;
        var half = _viewSize / 2f + Vector2.One * cell;
        int ux0 = (int)MathF.Floor((_viewCentre.X - half.X) / cell / size), ux1 = (int)MathF.Floor((_viewCentre.X + half.X) / cell / size);
        int uy0 = Math.Max(0, (int)MathF.Floor((_viewCentre.Y - half.Y) / cell / size)), uy1 = Math.Min(chunksY - 1, (int)MathF.Floor((_viewCentre.Y + half.Y) / cell / size));
        bool complete = true;
        var needed = new List<(Chunk chunk, float distance)>();
        for (int uy = uy0; uy <= uy1; uy++)
            for (int ux = ux0; ux <= ux1; ux++)
            {
                int kx = ((ux % chunksX) + chunksX) % chunksX;
                if (!level.Chunks.TryGetValue((kx, uy), out var chunk))
                    level.Chunks[(kx, uy)] = chunk = new Chunk { Cx = kx * size, Cy = uy * size, Cw = Math.Min(size, w.W - kx * size), Ch = Math.Min(size, w.H - uy * size) };
                chunk.LastSeen = now;
                if (chunk.Sprite != null) chunk.Sprite.Position = new Vector2(ux * size, uy * size) * cell;
                if (!chunk.Baked || chunk.Stale) complete = false;
                if ((!chunk.Baked || chunk.Stale) && chunk.Task == null)
                {
                    var centre = (new Vector2(ux, uy) + Vector2.One * 0.5f) * size * cell;
                    needed.Add((chunk, centre.DistanceSquaredTo(_viewCentre)));
                }
            }

        int running = 0;
        foreach (var chunk in level.Chunks.Values)
        {
            if (chunk.Task == null) continue;
            if (chunk.Task.IsCompleted)
            {
                if (chunk.Task.IsCompletedSuccessfully) Finish(level, chunk, chunk.Task.Result);
                else GD.PrintErr($"[tiles] chunk bake failed: {chunk.Task.Exception?.GetBaseException().Message}");
                chunk.Task = null;
            }
            else running++;
        }
        foreach (var (chunk, _) in needed.OrderBy(n => n.distance))
        {
            if (running >= 4) break;
            var input = _input!;
            int d = level.D, cx = chunk.Cx, cy = chunk.Cy, cw = chunk.Cw, ch = chunk.Ch;
            chunk.Signature = ChunkSignature(chunk, input);
            chunk.Stale = false;
            chunk.Task = Task.Run(() => TileBake.Bake(input, d, cx, cy, cw, ch));
            running++;
        }
        Evict(level, now);
        return complete;
    }

    static void Evict(Level level, ulong now)
    {
        int keep = 72;
        if (level.Chunks.Count <= keep) return;
        foreach (var (key, chunk) in level.Chunks.Where(c => c.Value.Task == null && c.Value.LastSeen + 2000 < now)
                                                 .OrderBy(c => c.Value.LastSeen).Take(level.Chunks.Count - keep).ToList())
        {
            chunk.Sprite?.QueueFree();
            level.Chunks.Remove(key);
        }
    }

    public void InvalidateTerrain()
    {
        _inputDay = int.MinValue / 2;
        TileBake.InvalidateRivers();
    }

    public void Redraw(Simulation sim, Overlay overlay)
    {
        if (!Active || _world == null) return;
        Visible = overlay is Overlay.Biome or Overlay.Cultures;
        if (!Visible) return;
        var w = _world;
        int day = sim.Day;

        if (day - _inputDay >= 15 || day < _inputDay)
        {
            _inputDay = day;
            _input = Settlements.BakeInput(sim);
            RefreshKinds();
            foreach (var level in _levels)
                foreach (var chunk in level.Chunks.Values)
                    if (chunk.Baked && !chunk.Stale && ChunkSignature(chunk, _input) != chunk.Signature) chunk.Stale = true;
        }

        var territory = overlay == Overlay.Cultures ? WorldView.Territory(sim) : null;
        if (_desaturate != (territory != null))
        {
            _desaturate = territory != null;
            foreach (var level in _levels)
                foreach (var chunk in level.Chunks.Values) chunk.Material?.SetShaderParameter("desaturate", _desaturate);
        }
        int doy = WorldMap.DayOfYear(day);
        for (int i = 0; i < w.Count; i++)
        {
            int o = i * 4;
            var biome = w.Biome[i];
            bool off;
            if (HasWetDry(biome))
            {
                float phase = w.SeasonPhase(i, day);
                _a[o] = (byte)Row(biome, Season.Wet);
                _a[o + 1] = (byte)Row(biome, Season.Dry);
                float blend = Math.Clamp(0.5f - phase * 1.5f, 0f, 1f);
                _a[o + 2] = (byte)(blend * 255f);
                off = blend > 0.5f;
            }
            else
            {
                int d = (doy + (w.IsNorth(i) ? 0 : 180)) % SimConfig.DaysPerYear;
                int season = d / 90;
                float progress = d % 90 / 90f;
                _a[o] = (byte)Row(biome, (Season)season);
                _a[o + 1] = (byte)Row(biome, (Season)((season + 1) % 4));
                _a[o + 2] = (byte)(Math.Clamp((progress - 0.75f) / 0.25f, 0f, 1f) * 255f);
                off = season == (int)Season.Winter;
            }
            float t = w.Temperature(i, day);
            float cover = w.IsLand(i) && w.Biome[i] != Biome.Lake
                ? Math.Clamp((-1f - t) / 7f, 0f, 0.95f)
                : w.Biome[i] != Biome.DeepOcean ? Math.Clamp((-6f - t) / 8f, 0f, 0.95f) : 0f;
            _a[o + 3] = (byte)(cover * 255f);

            if (territory != null && territory.TryGetValue(i, out var tint))
            { _b[o] = (byte)(tint.R * 255f); _b[o + 1] = (byte)(tint.G * 255f); _b[o + 2] = (byte)(tint.B * 255f); _b[o + 3] = 255; }
            else { _b[o] = _b[o + 1] = _b[o + 2] = _b[o + 3] = 0; }

            _c[o] = (byte)(WorldView.Shade(w, i) * 128f);
            _c[o + 1] = (byte)((off ? 1 : 0) | (w.IsWater(i) ? 2 : 0));
            _c[o + 2] = cover > 0.5f ? (byte)0 : _kind[i];
        }
        Upload(_cellA!, _texA!, _a);
        Upload(_cellB!, _texB!, _b);
        Upload(_cellC!, _texC!, _c);
    }

    void RefreshKinds()
    {
        var w = _world!;
        if (_kind.Length != w.Count) _kind = new byte[w.Count];
        for (int i = 0; i < w.Count; i++) _kind[i] = (byte)(Array.IndexOf(Animated, TileBake.Material(_input!, i)) + 1);
    }

    static long ChunkSignature(Chunk chunk, TileBake.Input input)
    {
        var w = input.World;
        long s = 17;
        for (int y = Math.Max(0, chunk.Cy - 1); y < Math.Min(w.H, chunk.Cy + chunk.Ch + 3); y++)
            for (int x = chunk.Cx - 2; x < chunk.Cx + chunk.Cw + 2; x++)
            {
                int i = w.Index(x, y);
                long v = (long)(input.Trees[i] >> 5) + ((long)(input.Rocks[i] >> 5) << 3) + (w.Ash[i] > 0.25f ? 1L << 6 : 0L) + (w.Ash[i] > 0.3f ? 1L << 7 : 0L)
                         + (input.Occupied.Contains(i) ? 1L << 8 : 0L);
                if (input.Fields.TryGetValue(i, out var f)) v += ((long)(f.growth <= 0.01f ? 1 : f.growth < 0.3f ? 2 : f.growth < 0.8f ? 3 : 4) << 9) + ((long)f.crop.Length << 12);
                  v += ((long)input.Roads.GetValueOrDefault(i) << 20) + ((long)(int)(w.Elevation[i] / 10f) << 23) + ((long)(int)w.Biome[i] << 45);
                s = s * 1000003 + v;
            }
        return s;
    }

    static bool HasWetDry(Biome b) => b is Biome.Savanna or Biome.TropicalSeasonalForest or Biome.TropicalRainforest;

    void BuildRows()
    {
        var biomes = Enum.GetValues<Biome>();
        _rows = new int[(biomes.Max(b => (int)b) + 1) * 6];
        foreach (var b in biomes)
            foreach (var s in Enum.GetValues<Season>())
            {
                int row = Art.Nature.Row($"{Ids.Snake(b)}.{Ids.Snake(s)}");
                if (row < 0) row = Art.Nature.Row($"{Ids.Snake(b)}.summer");
                if (row < 0) row = Art.ReferenceRow;
                _rows[(int)b * 6 + (int)s] = Math.Max(row, 0);
            }
    }

    int Row(Biome b, Season s) => _rows[(int)b * 6 + (int)s];

    static (Image, ImageTexture, byte[]) CellTexture(WorldMap w)
    {
        var bytes = new byte[w.Count * 4];
        var image = Image.CreateFromData(w.W, w.H, false, Image.Format.Rgba8, bytes);
        return (image, ImageTexture.CreateFromImage(image), bytes);
    }

    static void Upload(Image image, ImageTexture texture, byte[] bytes)
    {
        image.SetData(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgba8, bytes);
        texture.Update(image);
    }

    static ImageTexture TileSlots(Sheet? sheet, int d)
    {
        var bytes = new byte[d * d * 4];
        if (sheet != null)
            for (int y = 0; y < d; y++)
                for (int x = 0; x < d; x++)
                {
                    bytes[(y * d + x) * 4] = sheet.Slot(0, 0, x % sheet.FrameW, y % sheet.FrameH);
                    bytes[(y * d + x) * 4 + 3] = 255;
                }
        return ImageTexture.CreateFromImage(Image.CreateFromData(d, d, false, Image.Format.Rgba8, bytes));
    }
}
