using Evolysia.Sim;
using Godot;

namespace Evolysia.Game;

/// <summary>While a power is armed: a ring at the cursor showing what it will touch.</summary>
public partial class TargetOverlay : Node2D
{
    public Power? Armed;
    public float CameraZoom = 1f;
    public float WrapWidth;
    readonly List<(Vector2 pos, float radius, Color color, ulong born)> _bursts = new();

    public override void _Process(double delta)
    {
        if (Armed != null || _bursts.Count > 0) QueueRedraw();
    }

    public void Burst(Vector2 pos, float radius, bool kind) =>
        _bursts.Add((pos, radius, kind ? Palette.Divine : Palette.Terracotta, Time.GetTicksMsec()));

    public static float RadiusPx(Power p) => (Powers.DefaultRadius(p) + 0.5f) * Main.CellPx;

    public override void _Draw()
    {
        float line = 1.5f / MathF.Max(CameraZoom, 0.5f);
        if (Armed is { } p)
        {
            var pos = GetGlobalMousePosition();
            var col = Powers.IsKind(p) ? Palette.Divine : Palette.Terracotta;
            float r = RadiusPx(p);
            DrawCircle(pos, r, new Color(col, 0.12f));
            DrawArc(pos, r, 0, Mathf.Tau, 48, col, line);
            DrawArc(pos, r * 0.15f, 0, Mathf.Tau, 16, col, line);
        }
        ulong now = Time.GetTicksMsec();
        _bursts.RemoveAll(b => now - b.born > 1500);
        foreach (var (origin, radius, color, born) in _bursts)
            foreach (float offset in new[] { -WrapWidth, 0f, WrapWidth })
            {
                var pos = origin + new Vector2(offset, 0f);
                float t = (now - born) / 1500f;
                DrawCircle(pos, radius * (0.3f + t), new Color(color, 0.35f * (1f - t)));
                DrawArc(pos, radius * (0.3f + t * 1.4f), 0, Mathf.Tau, 48, new Color(color, 1f - t), line * 2f);
            }
    }
}
