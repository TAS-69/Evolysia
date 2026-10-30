using Evolysia.Sim;
using Evolysia.Sim.Agents;
using Evolysia.Sim.Society;
using Godot;

namespace Evolysia.Game;

/// <summary>
/// Draws every band at its cell. Zoomed out: wandering bands as circles, settled villages as squares,
/// coloured by culture and sized by population. Zoomed in: the band's camp or village and its
/// people going about their day (<see cref="Camps"/>), and the wildlife around them
/// (<see cref="Wildlife"/>), in Codex's art at the density the zoom calls for (16, 32 or 64 px per
/// cell). Notable events flash as expanding rings so the eye finds where history is happening.
/// </summary>
public partial class BandLayer : Node2D
{
    public Simulation? Sim;
    public int SelectedBandId = -1;
    public float CameraZoom = 1f;

    /// <summary>Zoom from which camps are drawn as art rather than markers.</summary>
    public const float CampZoom = 1.8f;
    /// <summary>The camera's view in world units (centre copy), for culling.</summary>
    public Rect2 View;

    readonly List<(Vector2 pos, Color color, double born)> _pings = new();
    readonly Dictionary<int, ulong> _bandKnows = new();
    readonly List<WorldSprite> _sprites = new();
    readonly Dictionary<int, Vector2> _spread = new();
    int _knowsDay = int.MinValue, _spreadDay = int.MinValue, _spreadCount;
    static readonly Font Font = ThemeDB.FallbackFont;

    bool ShowCamps => CameraZoom >= CampZoom && Art.Available;

    public override void _Ready()
    {
        // Crisp art at any zoom (see pixel.gdshader); markers and lines draw unaffected.
        Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/pixel.gdshader") };
        TextureFilter = TextureFilterEnum.Linear;
    }

    public Vector2 BandPosition(Band band)
    {
        var w = Sim!.World;
        if (Sim.Day != _spreadDay || Sim.Bands.Count != _spreadCount) Spread();
        // A little stable jitter, so bands don't sit on a grid.
        float jx = ((band.Id * 7919) % 100) / 100f - 0.5f, jy = ((band.Id * 104729) % 100) / 100f - 0.5f;
        var centre = new Vector2((w.X(band.Cell) + 0.5f + jx * 0.4f) * Main.CellPx, (w.Y(band.Cell) + 0.5f + jy * 0.4f) * Main.CellPx);
        return centre + _spread.GetValueOrDefault(band.Id);
    }

    /// <summary>Bands sharing a cell stand apart in a ring, so their camps don't pile up.</summary>
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

    /// <summary>How far from its position a band can be clicked: its marker, or its camp when zoomed in.</summary>
    float HitRadius(Band band) => ShowCamps ? (band.Sedentary ? 18f : 8f) : Radius(band);

    public void Ping(Vector2 pos, Color color) => _pings.Add((pos, color, Time.GetTicksMsec() / 1000.0));

    public override void _Process(double delta)
    {
        if (_pings.Count > 0) QueueRedraw();
        if (ShowCamps) QueueRedraw();   // people walk and animals graze every frame
    }

    /// <summary>Width of the planet in world units; everything repeats every WrapWidth east-west.</summary>
    public float WrapWidth => Sim == null ? 0f : Sim.World.W * Main.CellPx;

    /// <summary>The copy of a world position nearest to <paramref name="near"/> (e.g. the camera).</summary>
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
        if (ShowCamps && Sim.Day != _knowsDay) RefreshKnows();
        if (ShowCamps) DrawCamps(outline, now);   // placed at the copy nearest the view already
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

    // ---- camps ----------------------------------------------------------------------------------

    void RefreshKnows()
    {
        _knowsDay = Sim!.Day;
        _bandKnows.Clear();
        foreach (var band in Sim.Bands.Values) _bandKnows[band.Id] = Settlements.Knows(Sim, band);
    }

    /// <summary>What each band knows, refreshed daily while camps are shown.</summary>
    public IReadOnlyDictionary<int, ulong> BandKnows => _bandKnows;

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
            Camps.Add(new Camps.Context(Sim, band, pos, _bandKnows.GetValueOrDefault(band.Id), density, now, band.Id == SelectedBandId), _sprites);
        }
        Wildlife.Add(Sim, _bandKnows, View, density, now, _sprites);
        _sprites.Sort((a, b) => a.Foot.Y.CompareTo(b.Foot.Y));   // y-sort: nearer things overlap farther ones
        foreach (var s in _sprites)
            if (s.Shadow > 0f) DrawEllipse(s.Foot, s.Shadow, s.Shadow * 0.35f, new Color(0f, 0f, 0f, 0.16f));
        foreach (var s in _sprites)
        {
            var size = s.Source.Size * s.PixelSize;
            var pivot = s.Flip ? new Vector2(s.Source.Size.X - s.Pivot.X, s.Pivot.Y) : s.Pivot;   // mirror about the foot
            var topLeft = s.Foot - pivot * s.PixelSize;
            var rect = s.Flip ? new Rect2(topLeft.X + size.X, topLeft.Y, -size.X, size.Y) : new Rect2(topLeft, size);
            DrawTextureRectRegion(s.Texture, rect, s.Source, s.Modulate);
        }
        if (SelectedBandId >= 0 && Sim.Bands.TryGetValue(SelectedBandId, out var selected))
            DrawArc(NearestCopy(BandPosition(selected), centre), selected.Sedentary ? 24f : 10f, 0, Mathf.Tau, 48, Palette.Bone, outline * 1.5f);
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

    /// <summary>Destination as seen from the origin, taking the short way round the planet.</summary>
    Vector2 Unwrapped(Vector2 from, Vector2 to)
    {
        float width = Sim!.World.W * Main.CellPx;
        if (to.X - from.X > width / 2) to.X -= width;
        else if (from.X - to.X > width / 2) to.X += width;
        return to;
    }

    /// <summary>Sea routes: recent voyages bright, older ones fading into a map of where people went.</summary>
    void DrawVoyages(float line)
    {
        foreach (var v in Sim!.Voyages)
        {
            float years = (Sim.Day - v.Day) / (float)Evolysia.Sim.SimConfig.DaysPerYear;
            float alpha = years < 2f ? 0.9f : Math.Clamp(0.45f - years / 400f, 0.12f, 0.45f);
            bool lost = v.BandId < 0 || v.Sailed - v.Lost <= 0;
            var a = CellCenter(v.FromCell);
            var b = Unwrapped(a, CellCenter(v.ToCell));
            var mid = (a + b) / 2 + (b - a).Orthogonal() * 0.18f;   // a gentle arc
            var color = new Color(lost ? Palette.Terracotta : Palette.River.Lightened(0.35f), alpha);
            const int segments = 16;
            int drawn = lost ? segments / 2 : segments;              // lost voyages end at sea
            Vector2 prev = a;
            for (int i = 1; i <= drawn; i++)
            {
                float t = i / (float)segments;
                var p = a.Lerp(mid, t).Lerp(mid.Lerp(b, t), t);
                if (i % 2 == 1) DrawLine(prev, p, color, line * (years < 2f ? 1.6f : 1f));   // dashed
                prev = p;
            }
            if (lost) { DrawLine(prev + new Vector2(-1, -1), prev + new Vector2(1, 1), color, line * 1.5f); DrawLine(prev + new Vector2(-1, 1), prev + new Vector2(1, -1), color, line * 1.5f); }
            else if (v.FirstLanding) DrawArc(b, 2.2f, 0, Mathf.Tau, 12, new Color(Palette.Divine, alpha), line * 1.5f);
        }
    }

    /// <summary>For the selected band: the shores its scouts have sighted across the sea.</summary>
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
