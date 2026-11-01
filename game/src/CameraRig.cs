using Godot;

namespace Evolysia.Game;

public partial class CameraRig : Camera2D
{
    public Vector2 WorldSize;
    public const float MinZoom = 0.2f, MaxZoom = 16f;
    bool _dragging;
    public bool KeyboardEnabled = true;

    public event Action? Changed;
    public event Action? UserPanned;

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
                Position += before - GetGlobalMousePosition();
                ClampPosition();
                GetViewport().SetInputAsHandled();
            }
        }
        else if (e is InputEventMouseMotion mm && _dragging)
        {
            UserPanned?.Invoke();
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
        if (KeyboardEnabled && GetViewport().GuiGetFocusOwner() is not (LineEdit or TextEdit) && dir != Vector2.Zero)
        {
            UserPanned?.Invoke();
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

    void ClampPosition()
    {
        var half = GetViewportRect().Size / (2f * Zoom.X);
        float x = WorldSize.X > 0f ? ((Position.X % WorldSize.X) + WorldSize.X) % WorldSize.X : Position.X;
        float y = WorldSize.Y <= 2f * half.Y ? WorldSize.Y * 0.5f : Math.Clamp(Position.Y, half.Y, WorldSize.Y - half.Y);
        Position = new Vector2(x, y);
    }
}
