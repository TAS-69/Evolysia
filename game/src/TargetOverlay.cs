using Evolysia.Sim;
using Godot;

namespace Evolysia.Game;

/// <summary>
/// While a power is armed: a ring at the cursor showing what it will touch. Also the player's words
/// written on the ground, as faint gold marks that weather away (carved stones are structures).
/// </summary>
public partial class TargetOverlay : Node2D
{
    public Power? Armed;
    public float CameraZoom = 1f;
    public float WrapWidth;
    public Simulation? Sim;
    int _marks;   // inscriptions drawn last frame (redraw while any show)
    readonly List<(Vector2 pos, float radius, Color color, ulong born)> _bursts = new();

    public override void _Process(double delta)
    {
        if (Armed != null || _bursts.Count > 0 || _marks > 0 || Sim?.Inscriptions.Count > 0) QueueRedraw();
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
        DrawInscriptions(line);
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

    /// <summary>Ground writing: a ring of short strokes, brightest when fresh, fading as it weathers.</summary>
    void DrawInscriptions(float line)
    {
        _marks = 0;
        if (Sim is not { } sim) return;
        float pulse = 0.85f + 0.15f * MathF.Sin(Time.GetTicksMsec() / 600f);
        foreach (var ins in sim.Inscriptions)
        {
            if (ins.Medium != MessageMedium.Ground || !ins.Visible(sim.Day)) continue;
            _marks++;
            float age = (sim.Day - ins.Day) / (float)Math.Max(1, ins.FadesDay - ins.Day);
            var col = new Color(Palette.Divine, (0.9f - 0.6f * age) * pulse);
            var centre = new Vector2(sim.World.X(ins.Cell) + 0.5f, sim.World.Y(ins.Cell) + 0.5f) * Main.CellPx;
            float r = Main.CellPx * 1.1f;
            foreach (float offset in new[] { -WrapWidth, 0f, WrapWidth })
            {
                var c = centre + new Vector2(offset, 0f);
                for (int k = 0; k < 9; k++)
                {
                    // One stroke per letter-ish, angled by the words themselves.
                    float a = k / 9f * Mathf.Tau;
                    float tilt = (ins.Text.Length > k ? ins.Text[k] % 7 : k) / 7f - 0.5f;
                    var p = c + new Vector2(MathF.Cos(a), MathF.Sin(a) * 0.6f) * r;
                    var d = new Vector2(MathF.Cos(a + 1.2f + tilt), MathF.Sin(a + 1.2f + tilt)) * Main.CellPx * 0.22f;
                    DrawLine(p - d, p + d, col, line * 1.6f);
                }
                DrawArc(c, r * 0.35f, 0, Mathf.Tau, 12, new Color(col, col.A * 0.6f), line);
            }
        }
    }
}
