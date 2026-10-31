using Evolysia.Sim;
using Evolysia.Sim.Agents;
using Evolysia.Sim.Society;
using Godot;

namespace Evolysia.Game;

public partial class BandLayer : Node2D
{
    public Simulation? Sim;
    public int SelectedBandId = -1;
    public float CameraZoom = 1f;

    public const float CampZoom = 1.8f;

    public Rect2 View;

    public TileView? Tiles;

    readonly List<(Vector2 pos, Color color, double born)> _pings = new();
    readonly List<WorldSprite> _sprites = new();
    readonly Dictionary<int, Vector2> _spread = new();
    int _knowsDay = int.MinValue, _spreadDay = int.MinValue, _spreadCount;
    static readonly Font Font = ThemeDB.FallbackFont;

    bool ShowCamps => CameraZoom >= CampZoom && Art.Available;

    public override void _Ready()
    {

        Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/pixel.gdshader") };
        TextureFilter = TextureFilterEnum.Linear;
    }

    public Vector2 BandPosition(Band band)
    {
        var w = Sim!.World;
        if (Sim.Day != _spreadDay || Sim.Bands.Count != _spreadCount) Spread();

        float jx = ((band.Id * 7919) % 100) / 100f - 0.5f, jy = ((band.Id * 104729) % 100) / 100f - 0.5f;
        var centre = new Vector2((w.X(band.Cell) + 0.5f + jx * 0.4f) * Main.CellPx, (w.Y(band.Cell) + 0.5f + jy * 0.4f) * Main.CellPx);
        return centre + _spread.GetValueOrDefault(band.Id);
    }

    void Spread()
    {
        _spreadDay = Sim!.Day;
        _spreadCount = Sim.Bands.Count;
        _spread.Clear();
        foreach (var group in Sim.Bands.Values.GroupBy(b => b.Cell))
        {
            var bands = group.OrderBy(b => b.Id).ToList();
            if (bands.Count < 2) continue;
            for (int k = 0; k < bands.Count; k++)
            {
                float a = k * Mathf.Tau / bands.Count + group.Key * 0.9f;
                _spread[bands[k].Id] = new Vector2(MathF.Cos(a), MathF.Sin(a) * 0.8f) * (10f + bands.Count * 3f);
            }
        }
    }

    public static float Radius(Band band) => 0.9f + MathF.Sqrt(band.Members.Count) * 0.28f;

    float HitRadius(Band band) => ShowCamps ? (band.Sedentary ? 18f : 8f) : Radius(band);

    public void Ping(Vector2 pos, Color color) => _pings.Add((pos, color, Time.GetTicksMsec() / 1000.0));

    public override void _Process(double delta)
    {
        if (_pings.Count > 0) QueueRedraw();
        if (ShowCamps) QueueRedraw();
    }

    public float WrapWidth => Sim == null ? 0f : Sim.World.W * Main.CellPx;

    public Vector2 NearestCopy(Vector2 pos, Vector2 near)
    {
        float w = WrapWidth;
        if (w <= 0f) return pos;
        pos.X += MathF.Round((near.X - pos.X) / w) * w;
        return pos;
    }

    public override void _Draw()
    {
        if (Sim == null) return;
        float outline = 1f / MathF.Max(CameraZoom, 0.5f);
        double now = Time.GetTicksMsec() / 1000.0;
        _pings.RemoveAll(p => now - p.born > 2.5);
        if (ShowCamps) DrawCamps(outline, now);
        foreach (float offset in new[] { -WrapWidth, 0f, WrapWidth })
        {
            DrawSetTransform(new Vector2(offset, 0f));
            DrawWorld(outline, now);
        }
        DrawSetTransform(Vector2.Zero);
    }

    void DrawWorld(float outline, double now)
    {
        if (!ShowCamps) DrawMarkers(outline);

        DrawVoyages(outline);
        if (SelectedBandId >= 0 && Sim!.Bands.TryGetValue(SelectedBandId, out var selected)) DrawKnownShores(selected, outline);

        foreach (var (pos, color, born) in _pings)
        {
            float t = (float)((now - born) / 2.5);
            DrawArc(pos, 3f + t * 18f, 0, Mathf.Tau, 32, new Color(color, 1f - t), outline * 2f);
        }
    }

    void DrawMarkers(float outline)
    {
        foreach (var band in Sim!.Bands.Values)
        {
            var pos = BandPosition(band);
            float r = Radius(band);
            var col = Palette.Culture(Sim.Cultures[band.CultureId].Hue);
            if (band.PlagueUntil > Sim.Day) col = col.Lerp(new Color("b8c46a"), 0.5f);
            if (band.Sedentary)
            {
                var rect = new Rect2(pos - Vector2.One * r, Vector2.One * r * 2f);
                DrawRect(rect, col);
                DrawRect(rect, new Color(0, 0, 0, 0.8f), false, outline);
            }
            else
            {
                DrawCircle(pos, r, col);
                DrawArc(pos, r, 0, Mathf.Tau, 16, new Color(0, 0, 0, 0.8f), outline);
            }
            if (band.Id == SelectedBandId) DrawArc(pos, r + 2.5f, 0, Mathf.Tau, 24, Palette.Bone, outline * 1.5f);
        }
    }

    void DrawCamps(float outline, double now)
    {
        _sprites.Clear();
        int density = Art.DensityFor(CameraZoom);
        var view = View.Grow(Main.CellPx * 6f);
        var centre = View.GetCenter();
        foreach (var band in Sim!.Bands.Values)
        {
            var pos = NearestCopy(BandPosition(band), centre);
            if (!view.HasPoint(pos)) continue;
            Camps.AddPeople(Sim, band, pos, density, now, _sprites);
        }
        Camps.AddStructures(Sim, View, density, now, SelectedBandId, _sprites);
        Wildlife.Add(Sim, View, density, now, _sprites);
        AddOccluders(density);
        _sprites.Sort((a, b) => a.Foot.Y.CompareTo(b.Foot.Y));
        foreach (var s in _sprites)
            if (s.Shadow > 0f) DrawTextureRect(ShadowTexture, new Rect2(s.Foot - new Vector2(s.Shadow, s.Shadow * 0.35f), new Vector2(s.Shadow * 2f, s.Shadow * 0.7f)), false, new Color(0f, 0f, 0f, 0.16f));
        foreach (var s in _sprites)
        {
            var size = s.Source.Size * s.PixelSize;
            var pivot = s.Flip ? new Vector2(s.Source.Size.X - s.Pivot.X, s.Pivot.Y) : s.Pivot;
            var topLeft = s.Foot - pivot * s.PixelSize;
            var rect = s.Flip ? new Rect2(topLeft.X + size.X, topLeft.Y, -size.X, size.Y) : new Rect2(topLeft, size);
            DrawTextureRectRegion(s.Texture, rect, s.Source, s.Modulate);
        }
        if (SelectedBandId >= 0 && Sim.Bands.TryGetValue(SelectedBandId, out var selected))
            DrawArc(NearestCopy(BandPosition(selected), centre), selected.Sedentary ? 24f : 10f, 0, Mathf.Tau, 48, Palette.Bone, outline * 1.5f);
    }

    readonly Dictionary<(int, int), List<TileBake.Plant>> _plantsByCell = new();
    readonly HashSet<(int, int)> _occluding = new();

    void AddOccluders(int density)
    {
        if (Tiles?.Input is not { } input) return;
        _plantsByCell.Clear();
        _occluding.Clear();
        float cell = Main.CellPx, unit = cell / TileBake.Px, px = Sprites.PixelSize(density);
        var w = Sim!.World;
        int count = _sprites.Count;
        for (int k = 0; k < count; k++)
        {
            var s = _sprites[k];
            var size = s.Source.Size * s.PixelSize;
            var rect = new Rect2(s.Foot - s.Pivot * s.PixelSize, size);
            int x0 = (int)MathF.Floor(rect.Position.X / cell) - 1, x1 = (int)MathF.Floor(rect.End.X / cell) + 1;
            int y0 = (int)MathF.Floor(s.Foot.Y / cell), y1 = y0 + 2;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    if (!_plantsByCell.TryGetValue((x, y), out var plants))
                    {
                        plants = new List<TileBake.Plant>();
                        TileBake.PlantsOf(input, x, y, plants);
                        _plantsByCell[(x, y)] = plants;
                    }
                    foreach (var p in plants)
                    {
                        var foot = new Vector2(p.X, p.Y) * unit;
                        if (foot.Y <= s.Foot.Y || _occluding.Contains((p.X, p.Y))) continue;
                        if (TileBake.ArtOf(p, density) is not { } art) continue;
                        var (cx, cy) = p.Cell;
                        var (rowA, rowB, blend, off) = Tiles.SeasonOf(w.Index(cx, cy));
                        var sheet = off ? art.Off : art.Summer;
                        if (sheet == null) continue;
                        int row = (off ? p.Variant : art.Row) % sheet.Rows;
                        var pivot = new Vector2(sheet.PivotX, sheet.PivotY) * art.Scale;
                        var plantRect = new Rect2(foot - pivot * px, new Vector2(sheet.FrameW, sheet.FrameH) * art.Scale * px);
                        if (!plantRect.Intersects(rect)) continue;
                        _occluding.Add((p.X, p.Y));
                        var source = new Rect2(0, row * sheet.FrameH * art.Scale, sheet.FrameW * art.Scale, sheet.FrameH * art.Scale);
                        _sprites.Add(new WorldSprite(foot, Sprites.Nature(sheet, art.Scale, rowA, rowB, blend), source, pivot, px, Colors.White));
                        if (off && art.Cap is { } cap)
                            _sprites.Add(new WorldSprite(foot + new Vector2(0, 0.001f), Sprites.Nature(cap, art.Scale, rowA, rowB, blend),
                                                         new Rect2(0, p.Variant % cap.Rows * cap.FrameH * art.Scale, cap.FrameW * art.Scale, cap.FrameH * art.Scale),
                                                         new Vector2(cap.PivotX, cap.PivotY) * art.Scale, px, Colors.White));
                    }
                }
        }
    }

    static ImageTexture? _shadow;

    static ImageTexture ShadowTexture
    {
        get
        {
            if (_shadow != null) return _shadow;
            const int w = 32, h = 12;
            var image = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + 0.5f) / w * 2f - 1f, dy = (y + 0.5f) / h * 2f - 1f;
                    float r = MathF.Sqrt(dx * dx + dy * dy);
                    image.SetPixel(x, y, new Color(1f, 1f, 1f, Math.Clamp((1f - r) * 3f, 0f, 1f)));
                }
            return _shadow = ImageTexture.CreateFromImage(image);
        }
    }

    void DrawEllipse(Vector2 centre, float rx, float ry, Color color)
    {
        var points = new Vector2[12];
        for (int i = 0; i < points.Length; i++)
        {
            float a = i * Mathf.Tau / points.Length;
            points[i] = centre + new Vector2(MathF.Cos(a) * rx, MathF.Sin(a) * ry);
        }
        DrawColoredPolygon(points, color);
    }

    public Font Labels => Font;

    Vector2 CellCenter(int cell) => new Vector2(Sim!.World.X(cell) + 0.5f, Sim.World.Y(cell) + 0.5f) * Main.CellPx;

    Vector2 Unwrapped(Vector2 from, Vector2 to)
    {
        float width = Sim!.World.W * Main.CellPx;
        if (to.X - from.X > width / 2) to.X -= width;
        else if (from.X - to.X > width / 2) to.X += width;
        return to;
    }

    void DrawVoyages(float line)
    {
        foreach (var v in Sim!.Voyages)
        {
            float years = (Sim.Day - v.Day) / (float)Evolysia.Sim.SimConfig.DaysPerYear;
            float alpha = years < 2f ? 0.9f : Math.Clamp(0.45f - years / 400f, 0.12f, 0.45f);
            bool lost = v.BandId < 0 || v.Sailed - v.Lost <= 0;
            var a = CellCenter(v.FromCell);
            var b = Unwrapped(a, CellCenter(v.ToCell));
            var mid = (a + b) / 2 + (b - a).Orthogonal() * 0.18f;
            var color = new Color(lost ? Palette.Terracotta : Palette.River.Lightened(0.35f), alpha);
            const int segments = 16;
            int drawn = lost ? segments / 2 : segments;
            Vector2 prev = a;
            for (int i = 1; i <= drawn; i++)
            {
                float t = i / (float)segments;
                var p = a.Lerp(mid, t).Lerp(mid.Lerp(b, t), t);
                if (i % 2 == 1) DrawLine(prev, p, color, line * (years < 2f ? 1.6f : 1f));
                prev = p;
            }
            if (lost) { DrawLine(prev + new Vector2(-1, -1), prev + new Vector2(1, 1), color, line * 1.5f); DrawLine(prev + new Vector2(-1, 1), prev + new Vector2(1, -1), color, line * 1.5f); }
            else if (v.FirstLanding) DrawArc(b, 2.2f, 0, Mathf.Tau, 12, new Color(Palette.Divine, alpha), line * 1.5f);
        }
    }

    void DrawKnownShores(Band band, float line)
    {
        var from = BandPosition(band);
        foreach (var cell in band.KnownShores.Keys)
        {
            var to = Unwrapped(from, CellCenter(cell));
            var color = new Color(Palette.Divine, 0.55f);
            var dir = (to - from).Normalized();
            for (float t = 0; t < from.DistanceTo(to); t += 3f) DrawLine(from + dir * t, from + dir * MathF.Min(t + 1f, from.DistanceTo(to)), color, line);
            DrawRect(new Rect2(to - Vector2.One * 1.2f, Vector2.One * 2.4f), color, false, line * 1.5f);
        }
    }

    public Band? BandAt(Vector2 worldPos, float maxDistance)
    {
        Band? best = null;
        float bestD = maxDistance;
        foreach (var band in Sim!.Bands.Values)
        {
            float d = NearestCopy(BandPosition(band), worldPos).DistanceTo(worldPos) - HitRadius(band);
            if (d < bestD) { bestD = d; best = band; }
        }
        return best;
    }
}
