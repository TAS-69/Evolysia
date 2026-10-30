using Godot;

namespace Evolysia.Game;

/// <summary>Right/middle-drag or WASD to pan, wheel to zoom towards the cursor.</summary>
public partial class CameraRig : Camera2D
{
    public Vector2 WorldSize;
    public const float MinZoom = 0.7f, MaxZoom = 10f;   // at 0.7 the view stays within the three drawn copies
    bool _dragging;

    public event Action? Changed;

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex is MouseButton.Right or MouseButton.Middle) _dragging = mb.Pressed;
            if (mb.Pressed && mb.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                var before = GetGlobalMousePosition();
                float z = Math.Clamp(Zoom.X * (mb.ButtonIndex == MouseButton.WheelUp ? 1.15f : 1f / 1.15f), MinZoom, MaxZoom);
                Zoom = Vector2.One * z;
                Position += before - GetGlobalMousePosition();   // keep the point under the cursor fixed
                ClampPosition();
                GetViewport().SetInputAsHandled();
            }
        }
        else if (e is InputEventMouseMotion mm && _dragging)
        {
            Position -= mm.Relative / Zoom;
            ClampPosition();
        }
    }

    public override void _Process(double delta)
    {
        var dir = Vector2.Zero;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) dir.X -= 1;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) dir.X += 1;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) dir.Y -= 1;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) dir.Y += 1;
        if (dir != Vector2.Zero)
        {
            Position += dir.Normalized() * (float)delta * 600f / Zoom.X;
            ClampPosition();
        }
        Changed?.Invoke();
    }

    public void Focus(Vector2 worldPos, float? zoom = null)
    {
        if (zoom is { } z) Zoom = Vector2.One * Math.Clamp(z, MinZoom, MaxZoom);
        Position = worldPos;
        ClampPosition();
    }

    /// <summary>
    /// East-west the planet wraps: pan forever, with the position folded back into one planet-width
    /// (the copies drawn on each side make the jump invisible). North-south the view stays on the planet.
    /// </summary>
    void ClampPosition()
    {
        var half = GetViewportRect().Size / (2f * Zoom.X);
        float x = WorldSize.X > 0f ? ((Position.X % WorldSize.X) + WorldSize.X) % WorldSize.X : Position.X;
        float y = WorldSize.Y <= 2f * half.Y ? WorldSize.Y * 0.5f : Math.Clamp(Position.Y, half.Y, WorldSize.Y - half.Y);
        Position = new Vector2(x, y);
    }
}
