using Evolysia.Sim;
using Evolysia.Sim.Core;
using Evolysia.Sim.World;
using Godot;

namespace Evolysia.Game;

/// <summary>
/// The planet in Codex's 16 px tile art (assets/terrain, overlays, flora), drawn over the
/// procedural <see cref="WorldView"/> when zoomed in. The art is baked once into slot indices
/// (<see cref="TileBake"/>); every redraw only refreshes small per-cell textures (season row,
/// snow, shade, tint) that tiles.gdshader turns into colour.
/// </summary>
public partial class TileView : Sprite2D
{
    WorldMap? _world;
    ShaderMaterial? _material;
    Image? _cellA, _cellB, _cellC;
    ImageTexture? _texA, _texB, _texC, _texWorld;
    byte[] _a = Array.Empty<byte>(), _b = Array.Empty<byte>(), _c = Array.Empty<byte>();
    int[] _rows = Array.Empty<int>();       // per biome x season: lut_nature row
    long _bakedAshSignature;
    int _bakedDay = int.MinValue;

    public bool Active => _material != null;

    public void Bind(WorldMap world)
    {
        _world = world;
        foreach (var child in GetChildren()) child.QueueFree();
        if (!Art.Available) { Visible = false; return; }
        _material ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/tiles.gdshader") };
        Material = _material;
        Centered = false;
        Scale = Vector2.One * (Main.CellPx / (float)TileBake.Px);
        TextureFilter = TextureFilterEnum.Nearest;
        BuildRows();

        (_cellA, _texA, _a) = CellTexture(world);
        (_cellB, _texB, _b) = CellTexture(world);
        (_cellC, _texC, _c) = CellTexture(world);
        _material.SetShaderParameter("lut", ImageTexture.CreateFromImage(Art.Nature.Image));
        _material.SetShaderParameter("cell_a", _texA);
        _material.SetShaderParameter("cell_b", _texB);
        _material.SetShaderParameter("cell_c", _texC);
        _material.SetShaderParameter("snow_tile", TileSlots("terrain.snow_plain"));
        _material.SetShaderParameter("ice_tile", TileSlots("terrain.ice_plain"));
        Bake(0);

        // The planet wraps east-west: a copy on each side, sharing texture and material.
        int width = world.W * TileBake.Px;
        foreach (int side in new[] { -1, 1 })
            AddChild(new Sprite2D { Texture = _texWorld, Centered = false, Position = new Vector2(side * width, 0), UseParentMaterial = true });
    }

    void Bake(int day)
    {
        var w = _world!;
        var started = Time.GetTicksMsec();
        var px = TileBake.Bake(w);
        var image = Image.CreateFromData(w.W * TileBake.Px, w.H * TileBake.Px, false, Image.Format.Rgba8, px);
        if (_texWorld == null) _texWorld = ImageTexture.CreateFromImage(image);
        else _texWorld.Update(image);
        Texture = _texWorld;
        _material!.SetShaderParameter("world", _texWorld);
        _bakedAshSignature = AshSignature(w);
        _bakedDay = day;
        GD.Print($"[tiles] baked {w.W}x{w.H} cells in {Time.GetTicksMsec() - started} ms");
    }

    /// <summary>Show the art when zoomed in, fading to the smooth procedural map as you zoom out.</summary>
    public void SetZoom(float zoom) => _material?.SetShaderParameter("fade", Math.Clamp((zoom - 1.1f) / 0.5f, 0f, 1f));

    public void Redraw(Simulation sim, Overlay overlay)
    {
        if (_material == null || _world == null) return;
        Visible = overlay is Overlay.Biome or Overlay.Cultures;
        if (!Visible) return;
        var w = _world;
        int day = sim.Day;
        // Eruptions and fires change the ground (ash, burned trees): rebake, at most monthly.
        if (day - _bakedDay >= 30 && AshSignature(w) != _bakedAshSignature) Bake(day);

        var territory = overlay == Overlay.Cultures ? WorldView.Territory(sim) : null;
        _material.SetShaderParameter("desaturate", territory != null);
        int doy = WorldMap.DayOfYear(day);
        for (int i = 0; i < w.Count; i++)
        {
            int o = i * 4;
            var biome = w.Biome[i];
            bool off;
            if (HasWetDry(biome))
            {
                float phase = w.SeasonPhase(i, day);   // +1 wettest
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
                _a[o + 2] = (byte)(Math.Clamp((progress - 0.75f) / 0.25f, 0f, 1f) * 255f);   // ease into the next season
                off = season == (int)Season.Winter;
            }
            float t = w.Temperature(i, day);
            float cover = w.IsLand(i) && w.Biome[i] != Biome.Lake
                ? Math.Clamp((-1f - t) / 7f, 0f, 0.95f)
                : w.Biome[i] != Biome.DeepOcean ? Math.Clamp((-6f - t) / 8f, 0f, 0.95f) : 0f;   // snow / sea and lake ice
            _a[o + 3] = (byte)(cover * 255f);

            if (territory != null && territory.TryGetValue(i, out var tint))
            { _b[o] = (byte)(tint.R * 255f); _b[o + 1] = (byte)(tint.G * 255f); _b[o + 2] = (byte)(tint.B * 255f); _b[o + 3] = 255; }
            else { _b[o] = _b[o + 1] = _b[o + 2] = _b[o + 3] = 0; }   // premultiplied, so edges blend cleanly

            _c[o] = (byte)(WorldView.Shade(w, i) * 128f);
            _c[o + 1] = (byte)((off ? 1 : 0) | (w.IsWater(i) || w.Biome[i] == Biome.Lake ? 2 : 0));
        }
        Upload(_cellA!, _texA!, _a);
        Upload(_cellB!, _texB!, _b);
        Upload(_cellC!, _texC!, _c);
    }

    // ---- helpers --------------------------------------------------------------------------------

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
                if (row < 0) row = Art.Nature.Row("temperate_forest.summer");   // the reference colouring
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

    /// <summary>The first 16 x 16 frame of a sheet as slot indices in R (for snow and ice cover).</summary>
    static ImageTexture TileSlots(string id)
    {
        var sheet = Art.Need(id);
        var bytes = new byte[TileBake.Px * TileBake.Px * 4];
        for (int y = 0; y < TileBake.Px; y++)
            for (int x = 0; x < TileBake.Px; x++)
            {
                byte slot = sheet.Slot(0, 0, x, y);
                bytes[(y * TileBake.Px + x) * 4] = slot;
                bytes[(y * TileBake.Px + x) * 4 + 3] = 255;
            }
        return ImageTexture.CreateFromImage(Image.CreateFromData(TileBake.Px, TileBake.Px, false, Image.Format.Rgba8, bytes));
    }

    static long AshSignature(WorldMap w)
    {
        long s = 0;
        for (int i = 0; i < w.Count; i++)
            if (w.Volcano[i] || (w.IsLand(i) && w.Ash[i] > 0.25f)) s = s * 31 + i + (w.Ash[i] > 0.3f ? 7 : 0);
        return s;
    }
}
