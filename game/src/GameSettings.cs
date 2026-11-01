using Godot;

namespace Evolysia.Game;

public sealed class GameSettings
{
    public int AutosaveDays = 30;
    public float UiScale = 1f;
    public bool ReducedMotion, Fullscreen;
    public bool Shadows = true, WorldLabels = true;

    public static GameSettings Load()
    {
        var file = new ConfigFile(); file.Load("user://settings.cfg");
        return new GameSettings { AutosaveDays = Math.Clamp((int)file.GetValue("world", "autosave_days", 30), 0, 360),
            UiScale = Math.Clamp((float)file.GetValue("display", "ui_scale", 1f), 0.75f, 2f),
            ReducedMotion = (bool)file.GetValue("display", "reduced_motion", false), Fullscreen = (bool)file.GetValue("display", "fullscreen", false),
            Shadows = (bool)file.GetValue("display", "shadows", true), WorldLabels = (bool)file.GetValue("display", "world_labels", true) };
    }

    public void Save()
    {
        var file = new ConfigFile(); file.SetValue("world", "autosave_days", AutosaveDays); file.SetValue("display", "ui_scale", UiScale);
        file.SetValue("display", "reduced_motion", ReducedMotion); file.SetValue("display", "fullscreen", Fullscreen);
        file.SetValue("display", "shadows", Shadows); file.SetValue("display", "world_labels", WorldLabels); file.Save("user://settings.cfg");
    }
}

public partial class SettingsDialog : ConfirmationDialog
{
    public GameSettings Settings = null!;
    public event Action? Applied;
    public override void _Ready()
    {
        Title = "Settings"; Theme = Ui.Build(); MinSize = new Vector2I(480, 360);
        var box = new VBoxContainer(); AddChild(box);
        box.AddChild(new Label { Text = "Autosave interval in simulated days (0 disables it)" });
        var autosave = new SpinBox { MinValue = 0, MaxValue = 360, Step = 5, Value = Settings.AutosaveDays }; box.AddChild(autosave);
        box.AddChild(new Label { Text = "Interface scale" });
        var scale = new SpinBox { MinValue = 0.75, MaxValue = 2, Step = 0.25, Value = Settings.UiScale }; box.AddChild(scale);
        CheckBox Toggle(string text, bool value) { var item = new CheckBox { Text = text, ButtonPressed = value }; box.AddChild(item); return item; }
        var motion = Toggle("Reduced motion", Settings.ReducedMotion); var fullscreen = Toggle("Fullscreen", Settings.Fullscreen);
        var shadows = Toggle("Ground shadows", Settings.Shadows); var labels = Toggle("World labels", Settings.WorldLabels);
        Confirmed += () => { Settings.AutosaveDays = (int)autosave.Value; Settings.UiScale = (float)scale.Value; Settings.ReducedMotion = motion.ButtonPressed;
            Settings.Fullscreen = fullscreen.ButtonPressed; Settings.Shadows = shadows.ButtonPressed; Settings.WorldLabels = labels.ButtonPressed; Settings.Save(); Applied?.Invoke(); };
        VisibilityChanged += () => { if (!Visible) QueueFree(); };
    }
}
