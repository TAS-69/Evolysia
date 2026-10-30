using Godot;

namespace Evolysia.Game;

/// <summary>Top bar (date, stats, speed, overlays, new world), chronicle feed, inspector.</summary>
public partial class Hud : CanvasLayer
{
    public Label Date = null!, Stats = null!, Perf = null!, VoiceStatus = null!;
    public RichTextLabel Feed = null!, Inspector = null!, Voices = null!;
    public Button StartModels = null!, Minds = null!;
    public event Action? StartModelsRequested;
    public PanelContainer InspectorPanel = null!;
    public OptionButton OverlayPick = null!;
    public LineEdit SeedEdit = null!;
    readonly List<Button> _speedButtons = new();

    public event Action<string>? LinkClicked;
    public event Action<int>? SpeedChosen;
    public event Action<Overlay>? OverlayChosen;
    public event Action<ulong>? NewWorldRequested;
    public event Action? InspectorClosed;

    // God powers
    public RichTextLabel PrayersList = null!;
    public Label PowerHint = null!;
    public PanelContainer DreamDialog = null!;
    public OptionButton DreamKindPick = null!;
    public LineEdit DreamWords = null!;
    readonly Dictionary<Evolysia.Sim.Power, Button> _powerButtons = new();
    public event Action<Evolysia.Sim.Power?>? PowerPicked;
    public event Action<Evolysia.Sim.DreamKind, string>? DreamSent;

    public override void _Ready()
    {
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = BuildTheme() };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        // ---- top bar
        var top = new PanelContainer();
        top.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        top.OffsetBottom = 44;
        root.AddChild(top);
        var bar = new HBoxContainer();
        bar.AddThemeConstantOverride("separation", 14);
        top.AddChild(bar);
        var title = new Label { Text = "EVOLYSIA" };
        title.AddThemeColorOverride("font_color", Palette.Parchment);
        title.AddThemeFontSizeOverride("font_size", 18);
        bar.AddChild(title);
        bar.AddChild(Date = new Label { CustomMinimumSize = new Vector2(190, 0) });
        bar.AddChild(Stats = new Label());
        bar.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });
        bar.AddChild(VoiceStatus = new Label { TooltipText = "Local language model that voices notable moments", MouseFilter = Control.MouseFilterEnum.Pass });
        StartModels = new Button { Text = "Start model", FocusMode = Control.FocusModeEnum.None, Visible = false,
                                   TooltipText = "Runs tools/llm_lab/lab.py serve (uses your lab profile)" };
        StartModels.Pressed += () => StartModelsRequested?.Invoke();
        bar.AddChild(StartModels);
        Minds = new Button { Text = "Minds", ToggleMode = true, ButtonPressed = true, FocusMode = Control.FocusModeEnum.None,
                             TooltipText = "When on, pivotal choices (rejections, first contacts) are made by the local model in character. " +
                                           "When off, by temperament alone." };
        Minds.AddThemeFontSizeOverride("font_size", 12);
        bar.AddChild(Minds);
        bar.AddChild(Perf = new Label());
        Perf.AddThemeColorOverride("font_color", Palette.Border.Lightened(0.3f));

        string[] labels = { "II", "1×", "2×", "3×", "4×" };
        string[] tips = { "Pause (Space)", "2 days/s", "10 days/s", "60 days/s", "360 days/s" };
        for (int i = 0; i < labels.Length; i++)
        {
            int idx = i;
            var b = new Button { Text = labels[i], ToggleMode = true, TooltipText = tips[i], CustomMinimumSize = new Vector2(36, 0), FocusMode = Control.FocusModeEnum.None };
            b.Pressed += () => SpeedChosen?.Invoke(idx);
            _speedButtons.Add(b);
            bar.AddChild(b);
        }

        OverlayPick = new OptionButton { FocusMode = Control.FocusModeEnum.None, TooltipText = "Map overlay (Tab)" };
        foreach (var o in Enum.GetValues<Overlay>()) OverlayPick.AddItem(o.ToString(), (int)o);
        OverlayPick.ItemSelected += i => OverlayChosen?.Invoke((Overlay)(int)i);
        bar.AddChild(OverlayPick);

        SeedEdit = new LineEdit { CustomMinimumSize = new Vector2(80, 0), PlaceholderText = "seed", TooltipText = "World seed (empty = random)" };
        bar.AddChild(SeedEdit);
        var newWorld = new Button { Text = "New world", FocusMode = Control.FocusModeEnum.None };
        newWorld.Pressed += () =>
        {
            if (!ulong.TryParse(SeedEdit.Text, out var seed)) seed = (ulong)GD.Randi();
            NewWorldRequested?.Invoke(seed);
        };
        bar.AddChild(newWorld);

        // ---- chronicle (right)
        var right = new PanelContainer();
        right.SetAnchorsPreset(Control.LayoutPreset.RightWide);
        right.OffsetLeft = -380; right.OffsetTop = 52; right.OffsetBottom = -8; right.OffsetRight = -8;
        root.AddChild(right);
        var rv = new VBoxContainer();
        right.AddChild(rv);
        PrayersList = new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false, Visible = false, FocusMode = Control.FocusModeEnum.None };
        PrayersList.MetaClicked += meta => LinkClicked?.Invoke(meta.AsString());
        rv.AddChild(PrayersList);
        rv.AddChild(Header("Chronicle"));
        Feed = Rich();
        rv.AddChild(Feed);

        // ---- inspector (left)
        InspectorPanel = new PanelContainer { Visible = false };
        InspectorPanel.SetAnchorsPreset(Control.LayoutPreset.LeftWide);
        InspectorPanel.OffsetLeft = 8; InspectorPanel.OffsetRight = 420; InspectorPanel.OffsetTop = 52; InspectorPanel.OffsetBottom = -8;
        root.AddChild(InspectorPanel);
        var lv = new VBoxContainer();
        InspectorPanel.AddChild(lv);
        var head = new HBoxContainer();
        head.AddChild(Header("Inspector"));
        head.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var close = new Button { Text = "✕", FocusMode = Control.FocusModeEnum.None, TooltipText = "Close (Esc)" };
        close.Pressed += () => InspectorClosed?.Invoke();
        head.AddChild(close);
        lv.AddChild(head);
        Inspector = Rich();
        lv.AddChild(Inspector);

        BuildPowers(root);

        // ---- voices (bottom centre)
        var voices = new PanelContainer();
        voices.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        voices.OffsetLeft = 428; voices.OffsetRight = -388; voices.OffsetTop = -236; voices.OffsetBottom = -32;
        root.AddChild(voices);
        var vv = new VBoxContainer();
        voices.AddChild(vv);
        vv.AddChild(Header("Voices"));
        Voices = Rich();
        vv.AddChild(Voices);

        var help = new Label
        {
            Text = "Right-drag / WASD: pan   Wheel: zoom   Click: inspect   Space: pause   1-4: speed   Tab: overlay",
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        help.AddThemeColorOverride("font_color", new Color(Palette.Bone, 0.55f));
        help.AddThemeFontSizeOverride("font_size", 12);
        help.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        help.OffsetLeft = 12; help.OffsetTop = -26;
        root.AddChild(help);
    }

    void BuildPowers(Control root)
    {
        var panel = new PanelContainer();
        panel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        panel.OffsetTop = 52;
        panel.GrowHorizontal = Control.GrowDirection.Both;
        root.AddChild(panel);
        var col = new VBoxContainer();
        panel.AddChild(col);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 3);
        col.AddChild(row);
        foreach (var p in Enum.GetValues<Evolysia.Sim.Power>())
        {
            var power = p;
            bool kind = Evolysia.Sim.Powers.IsKind(p);
            var b = new Button
            {
                Text = PowerLabel(p), ToggleMode = true, FocusMode = Control.FocusModeEnum.None,
                TooltipText = PowerTip(p),
            };
            b.AddThemeColorOverride("font_color", kind ? Palette.Divine : p == Evolysia.Sim.Power.Dream ? new Color("9fd3e6") : new Color("e08a6e"));
            b.AddThemeFontSizeOverride("font_size", 12);
            b.Toggled += on => PowerPicked?.Invoke(on ? power : null);
            _powerButtons[p] = b;
            row.AddChild(b);
        }
        PowerHint = new Label { HorizontalAlignment = HorizontalAlignment.Center, Visible = false };
        PowerHint.AddThemeColorOverride("font_color", Palette.Parchment);
        PowerHint.AddThemeFontSizeOverride("font_size", 12);
        col.AddChild(PowerHint);

        // Dream dialog
        DreamDialog = new PanelContainer { Visible = false };
        DreamDialog.SetAnchorsPreset(Control.LayoutPreset.Center);
        DreamDialog.GrowHorizontal = Control.GrowDirection.Both;
        DreamDialog.GrowVertical = Control.GrowDirection.Both;
        DreamDialog.CustomMinimumSize = new Vector2(440, 0);
        root.AddChild(DreamDialog);
        var dv = new VBoxContainer();
        dv.AddThemeConstantOverride("separation", 8);
        DreamDialog.AddChild(dv);
        dv.AddChild(Header("Send a dream"));
        DreamKindPick = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        foreach (var k in Enum.GetValues<Evolysia.Sim.DreamKind>()) DreamKindPick.AddItem(DreamLabel(k), (int)k);
        dv.AddChild(DreamKindPick);
        DreamWords = new LineEdit { PlaceholderText = "Words they hear in the dream (optional)", MaxLength = 200 };
        dv.AddChild(DreamWords);
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        var cancel = new Button { Text = "Cancel", FocusMode = Control.FocusModeEnum.None };
        cancel.Pressed += () => { DreamDialog.Visible = false; ShowPower(null); };
        var send = new Button { Text = "Send dream", FocusMode = Control.FocusModeEnum.None };
        send.Pressed += () =>
        {
            DreamDialog.Visible = false;
            DreamSent?.Invoke((Evolysia.Sim.DreamKind)DreamKindPick.GetSelectedId(), DreamWords.Text);
            DreamWords.Text = "";
        };
        buttons.AddChild(cancel);
        buttons.AddChild(send);
        dv.AddChild(buttons);
    }

    public void Hint(string text)
    {
        PowerHint.Text = text;
        PowerHint.Visible = text.Length > 0;
    }

    public void ShowPower(Evolysia.Sim.Power? active)
    {
        foreach (var (p, b) in _powerButtons) b.SetPressedNoSignal(p == active);
    }

    static string PowerLabel(Evolysia.Sim.Power p) => p switch
    {
        Evolysia.Sim.Power.Wildfire => "Fire",
        Evolysia.Sim.Power.Earthquake => "Quake",
        Evolysia.Sim.Power.Fertility => "Fertile",
        Evolysia.Sim.Power.Lightning => "Bolt",
        _ => p.ToString(),
    };

    static string PowerTip(Evolysia.Sim.Power p) => p switch
    {
        Evolysia.Sim.Power.Rain => "Rain for a year over a wide area: ends droughts, feeds the land",
        Evolysia.Sim.Power.Drought => "Withhold the rains for a year",
        Evolysia.Sim.Power.Bless => "Abundance: wild food and game fill the land; stores overflow",
        Evolysia.Sim.Power.Fertility => "A year in which many children are born",
        Evolysia.Sim.Power.Heal => "Cure every sickness in a band, end its plague",
        Evolysia.Sim.Power.Game => "Herds multiply, permanently richer hunting",
        Evolysia.Sim.Power.Lightning => "Strike one band from a clear sky",
        Evolysia.Sim.Power.Wildfire => "Set the land ablaze",
        Evolysia.Sim.Power.Flood => "Raise the waters out of season",
        Evolysia.Sim.Power.Earthquake => "Shake the ground; homes fall",
        Evolysia.Sim.Power.Blizzard => "Snow and killing cold",
        Evolysia.Sim.Power.Plague => "A wasting sickness in one band (it can spread)",
        Evolysia.Sim.Power.Meteor => "A falling star: devastation, ash, a cooler world; seen from far away",
        Evolysia.Sim.Power.Beasts => "Predators gather, permanently more dangerous land",
        Evolysia.Sim.Power.Curse => "Curse the selected person: sickness, shame, misfortune",
        _ => "Speak to the selected person in a dream",
    };

    static string DreamLabel(Evolysia.Sim.DreamKind k) => k switch
    {
        Evolysia.Sim.DreamKind.Worship => "Worship me (they become your prophet)",
        Evolysia.Sim.DreamKind.Leave => "Leave this place (leads or splits the band)",
        Evolysia.Sim.DreamKind.Peace => "Make peace (grudges and anger fall away)",
        Evolysia.Sim.DreamKind.Seek => "Seek new things (a burst of invention)",
        Evolysia.Sim.DreamKind.Courage => "Fear nothing (fears melt, courage grows)",
        _ => "Love (grief eases, love grows)",
    };

    RichTextLabel Rich()
    {
        var r = new RichTextLabel
        {
            BbcodeEnabled = true, SizeFlagsVertical = Control.SizeFlags.ExpandFill, ScrollActive = true,
            SelectionEnabled = false, FocusMode = Control.FocusModeEnum.None,
        };
        r.MetaClicked += meta => LinkClicked?.Invoke(meta.AsString());
        return r;
    }

    static Label Header(string text)
    {
        var l = new Label { Text = text.ToUpper() };
        l.AddThemeColorOverride("font_color", Palette.Ochre);
        l.AddThemeFontSizeOverride("font_size", 13);
        return l;
    }

    public void ShowSpeed(int index)
    {
        for (int i = 0; i < _speedButtons.Count; i++) _speedButtons[i].SetPressedNoSignal(i == index);
    }

    static Theme BuildTheme()
    {
        var theme = new Theme();
        StyleBoxFlat Box(Color bg, Color border, int margin = 8)
        {
            var sb = new StyleBoxFlat { BgColor = bg, BorderColor = border };
            sb.SetBorderWidthAll(1);
            sb.SetCornerRadiusAll(3);
            sb.SetContentMarginAll(margin);
            return sb;
        }
        theme.SetStylebox("panel", "PanelContainer", Box(Palette.Panel, Palette.Border, 10));
        theme.SetStylebox("normal", "Button", Box(Palette.Raised, Palette.Border, 5));
        theme.SetStylebox("hover", "Button", Box(Palette.Raised.Lightened(0.12f), Palette.Ochre, 5));
        theme.SetStylebox("pressed", "Button", Box(Palette.Ochre.Darkened(0.25f), Palette.Ochre, 5));
        theme.SetStylebox("hover_pressed", "Button", Box(Palette.Ochre.Darkened(0.15f), Palette.Ochre, 5));
        theme.SetStylebox("focus", "Button", new StyleBoxEmpty());
        theme.SetStylebox("normal", "LineEdit", Box(Palette.Bg, Palette.Border, 5));
        theme.SetStylebox("normal", "OptionButton", Box(Palette.Raised, Palette.Border, 5));
        theme.SetStylebox("hover", "OptionButton", Box(Palette.Raised.Lightened(0.12f), Palette.Ochre, 5));
        theme.SetStylebox("pressed", "OptionButton", Box(Palette.Raised, Palette.Ochre, 5));
        theme.SetStylebox("panel", "PopupMenu", Box(Palette.Panel, Palette.Border, 6));
        foreach (var type in new[] { "Label", "Button", "LineEdit", "OptionButton", "PopupMenu" })
        {
            theme.SetColor("font_color", type, Palette.Bone);
            theme.SetFontSize("font_size", type, 14);
        }
        theme.SetColor("font_hover_color", "Button", Palette.Bone);
        theme.SetColor("font_pressed_color", "Button", Palette.Bone);
        theme.SetColor("default_color", "RichTextLabel", Palette.Bone);
        theme.SetFontSize("normal_font_size", "RichTextLabel", 14);
        theme.SetFontSize("bold_font_size", "RichTextLabel", 14);
        theme.SetFontSize("italics_font_size", "RichTextLabel", 14);
        return theme;
    }
}
