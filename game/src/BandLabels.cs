using Godot;

namespace Evolysia.Game;

/// <summary>
/// Band names drawn in screen space (crisp at any zoom), biggest bands first, skipping any label
/// that would overlap one already placed.
/// </summary>
public partial class BandLabels : Control
{
    public BandLayer Bands = null!;
    readonly List<Rect2> _placed = new();
    readonly List<(int bandId, string text, ulong until)> _quotes = new();

    /// <summary>Float a line of speech above a band for a few seconds.</summary>
    public void Say(int bandId, string text)
    {
        if (text.Length == 0) return;
        if (text.Length > 150) text = text[..147].TrimEnd() + "…";
        _quotes.RemoveAll(q => q.bandId == bandId);
        _quotes.Add((bandId, "“" + text + "”", Time.GetTicksMsec() + 8000));
        if (_quotes.Count > 4) _quotes.RemoveAt(0);
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    public override void _Draw()
    {
        var sim = Bands.Sim;
        if (sim == null) return;
        float zoom = Bands.CameraZoom;
        var xf = GetViewport().GetCanvasTransform();
        var font = Bands.Labels;
        var size = GetViewportRect().Size;
        _placed.Clear();
        var cameraCentre = xf.AffineInverse() * (size / 2f);
        DrawQuotes(sim, xf, font, size, cameraCentre);
        if (zoom < 2.2f) return;
        foreach (var band in sim.Bands.Values.OrderByDescending(b => b.Id == Bands.SelectedBandId).ThenByDescending(b => b.Members.Count))
        {
            if (zoom < 4f && band.Members.Count < 25 && band.Id != Bands.SelectedBandId) continue;
            var world = Bands.NearestCopy(Bands.BandPosition(band), cameraCentre) + new Vector2(BandLayer.Radius(band) + 1f, 0);
            var at = xf * world;
            if (at.X < 0 || at.Y < 0 || at.X > size.X || at.Y > size.Y) continue;
            int fs = band.Members.Count >= 60 ? 14 : 12;
            var textSize = font.GetStringSize(band.Name, HorizontalAlignment.Left, -1, fs);
            var rect = new Rect2(at + new Vector2(0, -textSize.Y * 0.5f), textSize).Grow(2);
            if (_placed.Any(r => r.Intersects(rect))) continue;
            _placed.Add(rect);
            var baseline = at + new Vector2(0, textSize.Y * 0.3f);
            DrawString(font, baseline + new Vector2(1, 1), band.Name, HorizontalAlignment.Left, -1, fs, new Color(0, 0, 0, 0.85f));
            DrawString(font, baseline, band.Name, HorizontalAlignment.Left, -1, fs, band.Id == Bands.SelectedBandId ? Palette.Divine : Palette.Bone);
        }
    }

    void DrawQuotes(Sim.Simulation sim, Transform2D xf, Font font, Vector2 size, Vector2 cameraCentre)
    {
        ulong now = Time.GetTicksMsec();
        _quotes.RemoveAll(q => q.until < now || !sim.Bands.ContainsKey(q.bandId));
        const float width = 250f;
        const int fs = 13;
        foreach (var (bandId, text, until) in _quotes)
        {
            var anchor = xf * Bands.NearestCopy(Bands.BandPosition(sim.Bands[bandId]), cameraCentre);
            if (anchor.X < -50 || anchor.Y < 0 || anchor.X > size.X + 50 || anchor.Y > size.Y + 50) continue;
            var textSize = font.GetMultilineStringSize(text, HorizontalAlignment.Left, width, fs, 4);
            var box = new Rect2(anchor + new Vector2(-textSize.X * 0.5f - 8f, -textSize.Y - 26f), textSize + new Vector2(16f, 12f));
            float fade = Math.Clamp((until - now) / 1000f, 0f, 1f);   // fade out over the last second
            DrawRect(box, new Color(Palette.Panel, 0.93f * fade));
            DrawRect(box, new Color(Palette.Ochre, fade), false, 1f);
            DrawLine(anchor + new Vector2(0, -14f), new Vector2(anchor.X, box.End.Y), new Color(Palette.Ochre, fade), 1f);
            DrawMultilineString(font, box.Position + new Vector2(8f, 6f + fs), text, HorizontalAlignment.Left, width, fs, 4,
                new Color(Palette.Bone, fade));
            _placed.Add(box);   // keep band labels from drawing over speech
        }
    }
}
