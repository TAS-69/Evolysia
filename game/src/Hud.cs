using Evolysia.Sim;
using Godot;

namespace Evolysia.Game;

public partial class Hud : CanvasLayer
{
    public Label Date = null!, Stats = null!, Perf = null!, VoiceStatus = null!;
    public RichTextLabel Feed = null!, Inspector = null!, Voices = null!;
    public Button StartModels = null!, Minds = null!;
    public event Action? StartModelsRequested;
    public event Action? SaveRequested, LoadRequested, ConnectionRequested, OutfitRequested, FollowRequested, SettingsRequested, SavesRequested;
    public PanelContainer InspectorPanel = null!;
    public TextureRect Portrait = null!;
    public OptionButton OverlayPick = null!;
    readonly List<Button> _speedButtons = new();
    readonly Dictionary<Power, Button> _powerButtons = new();
    public event Action<string>? LinkClicked;
    public event Action<int>? SpeedChosen;
    public event Action<Overlay>? OverlayChosen;
    public event Action? NewWorldRequested;
    public event Action? InspectorClosed;

    public RichTextLabel PrayersList = null!;
    public Label PowerHint = null!;
    public PanelContainer DreamDialog = null!, MessageDialog = null!;
    public OptionButton DreamKindPick = null!;
    public LineEdit DreamWords = null!;
    public TextEdit MessageWords = null!;
    OptionButton _messageMedium = null!;
    Label _messageWhere = null!;

    public event Action<Power?>? PowerPicked;
    public event Action<DreamKind, string>? DreamSent;

    public event Action<string, MessageMedium>? MessageSent;

    public override void _Ready()
    {
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = Ui.Build() };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        var top = new PanelContainer();
        top.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        top.OffsetLeft = 8; top.OffsetRight = -8; top.OffsetTop = 6; top.OffsetBottom = 46;
        var topStyle = (StyleBoxFlat)root.Theme.GetStylebox("panel", "PanelContainer").Duplicate();
        topStyle.SetContentMarginAll(6); topStyle.ContentMarginLeft = 14; topStyle.ContentMarginRight = 8;
        top.AddThemeStyleboxOverride("panel", topStyle);
        root.AddChild(top);
        var bar = new HBoxContainer();
        bar.AddThemeConstantOverride("separation", 12);
        top.AddChild(bar);
        var title = new Label { Text = Ui.Spaced("EVOLYSIA") };
        title.AddThemeColorOverride("font_color", Palette.Divine);
        title.AddThemeFontSizeOverride("font_size", 16);
        bar.AddChild(title);
        bar.AddChild(Divider());
        bar.AddChild(Date = new Label { CustomMinimumSize = new Vector2(170, 0) });
        Date.AddThemeColorOverride("font_color", Palette.Parchment);
        bar.AddChild(Stats = new Label());
        Stats.AddThemeColorOverride("font_color", Ui.Muted);
        bar.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });
        bar.AddChild(VoiceStatus = new Label { TooltipText = "Local language model that voices notable moments", MouseFilter = Control.MouseFilterEnum.Pass });
        VoiceStatus.AddThemeFontSizeOverride("font_size", 12);
        StartModels = new Button { Text = "Start model", FocusMode = Control.FocusModeEnum.None, Visible = false,
                                   TooltipText = "Runs tools/llm_lab/lab.py serve (uses your lab profile)" };
        StartModels.Pressed += () => StartModelsRequested?.Invoke();
        bar.AddChild(StartModels);
        Minds = new Button { Text = "Minds required", Disabled = true, ButtonPressed = true, FocusMode = Control.FocusModeEnum.None,
                             TooltipText = "A connected model generates work, goals, dialogue, construction, research and pivotal choices. The world waits if it cannot decide." };
        Minds.AddThemeFontSizeOverride("font_size", 12);
        Minds.Visible = false;
        bar.AddChild(Minds);
        bar.AddChild(Perf = new Label { CustomMinimumSize = new Vector2(64, 0), HorizontalAlignment = HorizontalAlignment.Right, MouseFilter = Control.MouseFilterEnum.Pass });
        Perf.AddThemeColorOverride("font_color", new Color(Palette.Parchment, 0.45f));
        Perf.AddThemeFontSizeOverride("font_size", 11);

        var speeds = new HBoxContainer();
        speeds.AddThemeConstantOverride("separation", 0);
        var group = new ButtonGroup();
        for (int i = 0; i < SimHost.SpeedNames.Length; i++)
        {
            int idx = i;
            var b = new Button
            {
                Text = SimHost.SpeedNames[i], ToggleMode = true, ButtonGroup = group, FocusMode = Control.FocusModeEnum.None,
                CustomMinimumSize = new Vector2(i == 0 ? 30 : 38, 0),
                TooltipText = i == 0 ? "Pause (Space)" : $"{SimHost.Speeds[i]:0} days a second (key {i})",
            };
            b.AddThemeFontSizeOverride("font_size", 12);
            Segment(b, root.Theme, i == 0, i == SimHost.SpeedNames.Length - 1);
            b.Pressed += () => SpeedChosen?.Invoke(idx);
            _speedButtons.Add(b);
            speeds.AddChild(b);
        }
        bar.AddChild(speeds);

        OverlayPick = new OptionButton { FocusMode = Control.FocusModeEnum.None, TooltipText = "Map overlay (Tab)" };
        foreach (var o in Enum.GetValues<Overlay>()) OverlayPick.AddItem(o.ToString(), (int)o);
        OverlayPick.ItemSelected += i => OverlayChosen?.Invoke((Overlay)(int)i);
        bar.AddChild(OverlayPick);
        var newWorld = new MenuButton { Text = "World", FocusMode = Control.FocusModeEnum.None };
        var menu = newWorld.GetPopup();
        menu.AddItem("New world…", 0); menu.AddItem("Save world (F5)", 1); menu.AddItem("Load world (F9)", 2);
        menu.AddItem("Model connection…", 3); menu.AddItem("Selected person's outfit…", 4); menu.AddItem("Follow selection (F)", 5);
        menu.AddItem("Settings…", 6); menu.AddItem("Saved worlds…", 7);
        menu.IdPressed += id =>
        {
            switch (id)
            {
                case 0: NewWorldRequested?.Invoke(); break;
                case 1: SaveRequested?.Invoke(); break;
                case 2: LoadRequested?.Invoke(); break;
                case 3: ConnectionRequested?.Invoke(); break;
                case 4: OutfitRequested?.Invoke(); break;
                case 5: FollowRequested?.Invoke(); break;
                case 6: SettingsRequested?.Invoke(); break;
                case 7: SavesRequested?.Invoke(); break;
            }
        };
        bar.AddChild(newWorld);

        var right = new PanelContainer();
        right.SetAnchorsPreset(Control.LayoutPreset.RightWide);
        right.OffsetLeft = -360; right.OffsetTop = 54; right.OffsetBottom = -8; right.OffsetRight = -8;
        root.AddChild(right);
        var rv = new VBoxContainer();
        rv.AddThemeConstantOverride("separation", 8);
        right.AddChild(rv);
        PrayersList = new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false, Visible = false, FocusMode = Control.FocusModeEnum.None };
        PrayersList.MetaClicked += meta => LinkClicked?.Invoke(meta.AsString());
        rv.AddChild(PrayersList);
        Feed = Rich();
        Feed.MetaUnderlined = false;
        rv.AddChild(HeadRow(right, "Chronicle", Feed));
        rv.AddChild(Feed);

        InspectorPanel = new PanelContainer { Visible = false };
        InspectorPanel.SetAnchorsPreset(Control.LayoutPreset.LeftWide);
        InspectorPanel.OffsetLeft = 8; InspectorPanel.OffsetRight = 412; InspectorPanel.OffsetTop = 54; InspectorPanel.OffsetBottom = -8;
        root.AddChild(InspectorPanel);
        var lv = new VBoxContainer();
        InspectorPanel.AddChild(lv);
        Inspector = Rich();
        var head = HeadRow(InspectorPanel, "Inspector", Inspector);
        var close = new Button { Text = "✕", Flat = true, FocusMode = Control.FocusModeEnum.None, TooltipText = "Close (Esc)" };
        close.Pressed += () => InspectorClosed?.Invoke();
        head.AddChild(close);
        lv.AddChild(head);
        Portrait = new TextureRect { CustomMinimumSize = new Vector2(144, 144), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, TextureFilter = Control.TextureFilterEnum.Nearest, Visible = false };
        lv.AddChild(Portrait);
        lv.AddChild(Inspector);

        BuildPowers(root);

        var voices = new PanelContainer();
        voices.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        voices.OffsetLeft = 420; voices.OffsetRight = -368; voices.OffsetTop = -150; voices.OffsetBottom = -30;
        root.AddChild(voices);
        var vv = new VBoxContainer();
        voices.AddChild(vv);
        Voices = Rich();
        vv.AddChild(HeadRow(voices, "Voices", Voices));
        vv.AddChild(Voices);

        var help = new Label
        {
            Text = "Right-drag / WASD pan  ·  wheel zoom  ·  click inspect  ·  Space pause  ·  1-6 speed  ·  Tab overlay",
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        help.AddThemeColorOverride("font_color", new Color(Palette.Bone, 0.4f));
        help.AddThemeFontSizeOverride("font_size", 11);
        help.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        help.OffsetLeft = 14; help.OffsetTop = -22;
        root.AddChild(help);
    }

    HBoxContainer HeadRow(PanelContainer panel, string title, Control body)
    {
        var row = new HBoxContainer();
        var fold = new Button { Text = "▾", Flat = true, FocusMode = Control.FocusModeEnum.None, TooltipText = "Collapse" };
        fold.AddThemeColorOverride("font_color", new Color(Palette.Ochre, 0.8f));
        fold.AddThemeFontSizeOverride("font_size", 12);
        row.AddChild(fold);
        var heading = Ui.Heading(title);
        heading.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        heading.MouseFilter = Control.MouseFilterEnum.Stop;
        heading.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) fold.EmitSignal(BaseButton.SignalName.Pressed); };
        row.AddChild(heading);
        float anchorTop = panel.AnchorTop, anchorBottom = panel.AnchorBottom, top = panel.OffsetTop, bottom = panel.OffsetBottom;
        bool fromBottom = anchorTop >= 1f;
        _folds[title.ToLowerInvariant()] = fold;
        fold.Pressed += () =>
        {
            bool collapse = body.Visible;
            body.Visible = !collapse;
            fold.Text = collapse ? "▸" : "▾";
            fold.TooltipText = collapse ? "Expand" : "Collapse";
            if (collapse && fromBottom) { panel.GrowVertical = Control.GrowDirection.Begin; panel.OffsetTop = bottom; }
            else if (collapse) { panel.GrowVertical = Control.GrowDirection.End; panel.AnchorBottom = anchorTop; panel.OffsetBottom = top; }
            else { panel.AnchorBottom = anchorBottom; panel.OffsetTop = top; panel.OffsetBottom = bottom; }
        };
        return row;
    }

    readonly Dictionary<string, Button> _folds = new();

    public void Fold(string panel)
    {
        if (_folds.TryGetValue(panel.ToLowerInvariant(), out var b)) b.EmitSignal(BaseButton.SignalName.Pressed);
    }

    static Control Divider()
    {
        var d = new ColorRect { Color = new Color(Palette.Border, 0.6f), CustomMinimumSize = new Vector2(1, 22), MouseFilter = Control.MouseFilterEnum.Ignore };
        d.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        return d;
    }

    static void Segment(Button b, Theme theme, bool first, bool last)
    {
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed" })
        {
            var sb = (StyleBoxFlat)theme.GetStylebox(state, "Button").Duplicate();
            if (!first) { sb.CornerRadiusTopLeft = 0; sb.CornerRadiusBottomLeft = 0; }
            if (!last) { sb.CornerRadiusTopRight = 0; sb.CornerRadiusBottomRight = 0; }
            b.AddThemeStyleboxOverride(state, sb);
        }
    }

    void BuildPowers(Control root)
    {
        var panel = new PanelContainer();
        panel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        panel.OffsetTop = 54;
        panel.GrowHorizontal = Control.GrowDirection.Both;
        var style = (StyleBoxFlat)root.Theme.GetStylebox("panel", "PanelContainer").Duplicate();
        style.SetContentMarginAll(6);
        panel.AddThemeStyleboxOverride("panel", style);
        root.AddChild(panel);
        var col = new VBoxContainer();
        panel.AddChild(col);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 1);
        col.AddChild(row);

        var groups = new[]
        {
            (Power[])[Power.Rain, Power.Drought, Power.Bless, Power.Fertility, Power.Heal, Power.Game, Power.SpawnPlant, Power.RaiseLand, Power.LowerLand],
            [Power.Lightning, Power.Wildfire, Power.Flood, Power.Earthquake, Power.Blizzard, Power.Plague, Power.Meteor, Power.Beasts, Power.Curse],
            [Power.Dream, Power.Message, Power.Possess],
        };
        for (int g = 0; g < groups.Length; g++)
        {
            if (g > 0) { row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; col.AddChild(row); }
            foreach (var p in groups[g])
            {
                var power = p;
                var colour = PowerColour(p);
                var b = new Button { Text = PowerLabel(p), ToggleMode = true, FocusMode = Control.FocusModeEnum.None, TooltipText = PowerTip(p) };
                b.AddThemeColorOverride("font_color", colour);
                b.AddThemeColorOverride("font_hover_color", colour.Lightened(0.25f));
                b.AddThemeColorOverride("font_pressed_color", Palette.Bone);
                b.AddThemeFontSizeOverride("font_size", 12);
                foreach (var state in new[] { "normal", "hover", "disabled" })
                {
                    var sb = (StyleBoxFlat)root.Theme.GetStylebox(state, "Button").Duplicate();
                    sb.ContentMarginLeft = sb.ContentMarginRight = 5;
                    if (state != "hover") { sb.BgColor = new Color(0, 0, 0, 0); sb.BorderColor = new Color(0, 0, 0, 0); }
                    b.AddThemeStyleboxOverride(state, sb);
                }
                var pressed = (StyleBoxFlat)Ui.Card(colour);
                pressed.BgColor = new Color(colour, 0.22f);
                pressed.SetContentMarginAll(6);
                pressed.ContentMarginLeft = pressed.ContentMarginRight = 5;
                b.AddThemeStyleboxOverride("pressed", pressed);
                b.AddThemeStyleboxOverride("hover_pressed", pressed);
                b.Toggled += on => PowerPicked?.Invoke(on ? power : null);
                _powerButtons[p] = b;
                row.AddChild(b);
            }
        }
        PowerHint = new Label { HorizontalAlignment = HorizontalAlignment.Center, Visible = false };
        PowerHint.AddThemeColorOverride("font_color", Palette.Parchment);
        PowerHint.AddThemeFontSizeOverride("font_size", 12);
        col.AddChild(PowerHint);

        DreamDialog = Dialog(root, "Send a dream", 440);
        var dv = (VBoxContainer)DreamDialog.GetChild(0);
        DreamKindPick = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        foreach (var k in Enum.GetValues<DreamKind>()) DreamKindPick.AddItem(DreamLabel(k), (int)k);
        dv.AddChild(DreamKindPick);
        DreamWords = new LineEdit { PlaceholderText = "Words they hear in the dream (optional)", MaxLength = 200 };
        dv.AddChild(DreamWords);
        dv.AddChild(Buttons(() => { DreamDialog.Visible = false; ShowPower(null); }, "Send dream", () =>
        {
            DreamDialog.Visible = false;
            DreamSent?.Invoke((DreamKind)DreamKindPick.GetSelectedId(), DreamWords.Text);
            DreamWords.Text = "";
        }));

        MessageDialog = Dialog(root, "Leave a message", 480);
        var mv = (VBoxContainer)MessageDialog.GetChild(0);
        _messageWhere = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _messageWhere.AddThemeColorOverride("font_color", Ui.Muted);
        mv.AddChild(_messageWhere);
        MessageWords = new TextEdit { PlaceholderText = "Your words, as they will appear", CustomMinimumSize = new Vector2(0, 96), WrapMode = TextEdit.LineWrappingMode.Boundary };
        mv.AddChild(MessageWords);
        _messageMedium = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        _messageMedium.AddItem("Written on the ground (fades in decades)", (int)MessageMedium.Ground);
        _messageMedium.AddItem("Carved in a standing stone (lasts while it stands)", (int)MessageMedium.Stone);
        _messageMedium.AddItem("Sent as a vision to everyone near", (int)MessageMedium.Vision);
        mv.AddChild(_messageMedium);
        var note = new Label { Text = "Each who reads it will take it their own way. You may speak like this once in 250 years.", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        note.AddThemeColorOverride("font_color", new Color(Palette.Divine, 0.75f));
        note.AddThemeFontSizeOverride("font_size", 12);
        mv.AddChild(note);
        mv.AddChild(Buttons(() => { MessageDialog.Visible = false; ShowPower(null); }, "Leave it", () =>
        {
            if (MessageWords.Text.Trim().Length == 0) return;
            MessageDialog.Visible = false;
            MessageSent?.Invoke(MessageWords.Text.Trim(), (MessageMedium)_messageMedium.GetSelectedId());
            MessageWords.Text = "";
        }));
    }

    static PanelContainer Dialog(Control root, string title, float width)
    {
        var d = new PanelContainer { Visible = false, CustomMinimumSize = new Vector2(width, 0) };
        d.SetAnchorsPreset(Control.LayoutPreset.Center);
        d.GrowHorizontal = Control.GrowDirection.Both;
        d.GrowVertical = Control.GrowDirection.Both;
        root.AddChild(d);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 10);
        d.AddChild(v);
        v.AddChild(Ui.Heading(title));
        return d;
    }

    static HBoxContainer Buttons(Action cancel, string ok, Action send)
    {
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        row.AddThemeConstantOverride("separation", 8);
        var c = new Button { Text = "Cancel", FocusMode = Control.FocusModeEnum.None };
        c.Pressed += cancel;
        var s = new Button { Text = ok, FocusMode = Control.FocusModeEnum.None };
        s.AddThemeColorOverride("font_color", Palette.Divine);
        s.Pressed += send;
        row.AddChild(c);
        row.AddChild(s);
        return row;
    }

    public void AskMessage(string where)
    {
        _messageWhere.Text = where;
        MessageDialog.Visible = true;
        MessageWords.GrabFocus();
    }

    public void ShowMessageCharge(int yearsLeft)
    {
        if (!_powerButtons.TryGetValue(Power.Message, out var b)) return;
        b.Text = yearsLeft > 0 ? $"Message · {yearsLeft}y" : "Message";
        b.TooltipText = yearsLeft > 0 ? $"Your words can be given again in {yearsLeft} years" : PowerTip(Power.Message);
        b.Disabled = yearsLeft > 0;
    }

    public void Hint(string text)
    {
        PowerHint.Text = text;
        PowerHint.Visible = text.Length > 0;
    }

    public void ShowPower(Power? active)
    {
        foreach (var (p, b) in _powerButtons) b.SetPressedNoSignal(p == active);
    }

    static Color PowerColour(Power p) =>
        Powers.IsKind(p) ? Palette.Divine : p is Power.Dream or Power.Message ? new Color("9fd3e6") : new Color("e08a6e");

    static string PowerLabel(Power p) => p switch
    {
        Power.Wildfire => "Fire",
        Power.Earthquake => "Quake",
        Power.Fertility => "Fertile",
        Power.Lightning => "Bolt",
        Power.SpawnPlant => "Grow",
        Power.RaiseLand => "Raise land",
        Power.LowerLand => "Lower land",
        _ => p.ToString(),
    };

    static string PowerTip(Power p) => p switch
    {
        Power.Rain => "Rain for a year over a wide area: ends droughts, feeds the land",
        Power.Drought => "Withhold the rains for a year",
        Power.Bless => "Abundance: wild food and game fill the land; stores overflow",
        Power.Fertility => "A year in which many children are born",
        Power.Heal => "Cure every sickness in a band, end its plague",
        Power.Game => "Herds multiply, permanently richer hunting",
        Power.Lightning => "Strike one band from a clear sky",
        Power.Wildfire => "Set the land ablaze",
        Power.Flood => "Raise the waters out of season",
        Power.Earthquake => "Shake the ground; homes fall",
        Power.Blizzard => "Snow and killing cold",
        Power.Plague => "A wasting sickness in one band (it can spread)",
        Power.Meteor => "A falling star: devastation, ash, a cooler world; seen from far away",
        Power.Beasts => "Predators gather, permanently more dangerous land",
        Power.Curse => "Curse the selected person: sickness, shame, misfortune",
        Power.SpawnPlant => "Restore local wild plants and trees to the land's carrying capacity",
        Power.RaiseLand => "Raise terrain by 200 metres, changing coasts, habitats and water access",
        Power.LowerLand => "Lower terrain by 200 metres; submerged buildings are ruined and camps must escape",
        Power.Possess => "Direct a selected adult. WASD/arrows move; E forage; C chop; Q quarry; R rest; X release. Needs, time and finite resources still apply.",
        Power.Message => "Your own words, written on the ground, carved in stone or sent as a vision. Whoever reads them takes them their own way. Once in 250 years.",
        _ => "Speak to the selected person in a dream",
    };

    static string DreamLabel(DreamKind k) => k switch
    {
        DreamKind.Worship => "Worship me (they become your prophet)",
        DreamKind.Leave => "Leave this place (leads or splits the band)",
        DreamKind.Peace => "Make peace (grudges and anger fall away)",
        DreamKind.Seek => "Seek new things (a burst of invention)",
        DreamKind.Courage => "Fear nothing (fears melt, courage grows)",
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

    public void ShowSpeed(int index)
    {
        for (int i = 0; i < _speedButtons.Count; i++) _speedButtons[i].SetPressedNoSignal(i == index);
    }
}
