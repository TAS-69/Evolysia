using Evolysia.Sim;
using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Godot;
using Evolysia.Sim.Persistence;

namespace Evolysia.Game;

public partial class Main : Node2D
{
    public const int CellPx = 8;

    SimHost _host = null!;
    WorldView _view = null!;
    TileView _tiles = null!;
    BandLayer _bands = null!;
    CameraRig _camera = null!;
    Hud _hud = null!;
    NarrationHost _voices = null!;
    TargetOverlay _target = null!;
    Power? _armed;
    readonly Dictionary<int, ulong> _holdSince = new();
    bool _following;
    int _voicesShown = -1;
    double _shotWait;
    GameSettings _settings = null!;
    SaveRepository _saves = null!;
    int _nextAutosave = int.MaxValue;

    int _lastMapDay = int.MinValue, _lastBandDay = int.MinValue, _seenEvents, _feedEvents = -1;
    bool _mapDirty = true;
    double _uiTimer, _mapTimer;
    BandLabels _labels = null!;
    enum Selection { None, Band, Agent, Cell }
    Selection _sel;
    int _selId = -1;

    readonly Dictionary<string, string> _args = new();
    string? _screenshot;
    int _shotCountdown = -1;

    public override void _Ready()
    {
        var user = OS.GetCmdlineUserArgs();
        for (int i = 0; i < user.Length; i++)
            if (user[i].StartsWith("--")) _args[user[i][2..]] = i + 1 < user.Length && !user[i + 1].StartsWith("--") ? user[++i] : "true";

        AddChild(_host = new SimHost());
        AddChild(_view = new WorldView());
        AddChild(_tiles = new TileView());
        AddChild(_bands = new BandLayer { Tiles = _tiles });
        AddChild(_target = new TargetOverlay());
        AddChild(_camera = new CameraRig());
        AddChild(_hud = new Hud());
        AddChild(_voices = new NarrationHost());
        _labels = new BandLabels { Bands = _bands };
        _hud.AddChild(_labels);
        _hud.MoveChild(_labels, 0);
        RenderingServer.SetDefaultClearColor(Palette.Bg);
        _settings = GameSettings.Load(); _saves = new SaveRepository(ProjectSettings.GlobalizePath("user://saves")); ApplySettings();

        _hud.SpeedChosen += SetSpeed;
        _hud.OverlayChosen += o => { _view.Overlay = o; _mapDirty = true; };
        _hud.NewWorldRequested += () => ShowSetup(canCancel: true);
        _hud.LinkClicked += OnLink;
        _hud.InspectorClosed += ClearSelection;
        _hud.StartModelsRequested += () => _voices.StartModels();
        _hud.SaveRequested += SaveWorld;
        _hud.LoadRequested += LoadWorld;
        _hud.ConnectionRequested += ShowConnection;
        _hud.OutfitRequested += ShowOutfit;
        _hud.FollowRequested += ToggleFollow;
        _hud.SettingsRequested += ShowSettings;
        _hud.SavesRequested += ShowSaves;
        _hud.PowerPicked += OnPowerPicked;
        _host.ShouldHold = HoldForMinds;
        _camera.UserPanned += () => _following = false;
        _hud.DreamSent += (kind, words) =>
        {
            if (_sel == Selection.Agent && _host.Sim!.Living.TryGetValue(_selId, out var a))
                Unleash(new GodAction(Power.Dream, a.BandId >= 0 && _host.Sim.Bands.TryGetValue(a.BandId, out var b) ? b.Cell : 0,
                                      TargetAgentId: a.Id, Dream: kind, Words: words));
            Disarm();
        };
        _hud.MessageSent += (words, medium) =>
        {
            if (_messageCell >= 0) Unleash(new GodAction(Power.Message, _messageCell, Words: words, Medium: medium));
            _messageCell = -1;
            Disarm();
        };
        _voices.Spoke += n => { if (_host.Sim!.Bands.ContainsKey(n.Moment.BandId)) _labels.Say(n.Moment.BandId, n.Quote); };
        _voices.IsBandVisible = id =>
        {
            if (!_host.Sim!.Bands.TryGetValue(id, out var b)) return false;
            var view = new Rect2(_camera.Position - GetViewportRect().Size / (2f * _camera.Zoom.X), GetViewportRect().Size / _camera.Zoom.X);
            return view.HasPoint(_bands.NearestCopy(_bands.BandPosition(b), _camera.Position));
        };
        _camera.Changed += () =>
        {
            _target.CameraZoom = _camera.Zoom.X;
            var viewSize = GetViewportRect().Size / _camera.Zoom.X;
            _tiles.SetView(_camera.Position, viewSize, _camera.Zoom.X);
            _bands.View = new Rect2(_camera.Position - viewSize / 2f, viewSize);
            if (!Mathf.IsEqualApprox(_bands.CameraZoom, _camera.Zoom.X)) { _bands.CameraZoom = _camera.Zoom.X; _bands.QueueRedraw(); }
        };

        ulong seed = _args.TryGetValue("seed", out var s) && ulong.TryParse(s, out var parsed) ? parsed : 42;
        var config = new SimConfig { Seed = seed };
        if (_args.GetValueOrDefault("demo") == "message") config.FirstMessageYear = 0;
        if (_args.TryGetValue("planet", out var pl) && Enum.TryParse<PlanetType>(pl, true, out var planet)) config.Planet = planet;
        if (_args.TryGetValue("tribes", out var tr) && int.TryParse(tr, out var tribes)) config.Tribes = Math.Clamp(tribes, 1, 6);
        NewWorld(config);

        if (_args.Count == 0 || _args.ContainsKey("setup")) ShowSetup(canCancel: false);
        if (_args.TryGetValue("overlay", out var ov) && Enum.TryParse<Overlay>(ov, true, out var overlay))
        {
            _view.Overlay = overlay;
            _hud.OverlayPick.Select((int)overlay);
        }
        if (_args.TryGetValue("days", out var d) && int.TryParse(d, out var days))
        {
            _host.FastForward(days);
            _voices.Attach(_host.Sim!);
        }
        if (_args.ContainsKey("load")) LoadWorld();
        if (_args.TryGetValue("select", out var what) && what is "band" or "agent") SelectForDemo(what);
        if (_args.ContainsKey("possess") && _sel == Selection.Agent) OnPowerPicked(Power.Possess);
        if (_args.ContainsKey("outfit")) ShowOutfit();
        if (_args.ContainsKey("saves")) ShowSaves();
        if (_args.ContainsKey("settings")) ShowSettings();
        if (_args.GetValueOrDefault("demo") == "gods") DemoGods();
        if (_args.GetValueOrDefault("demo") == "message") DemoMessage();
        if (_args.GetValueOrDefault("demo") == "village") DemoVillage();
        if (_args.TryGetValue("zoom", out var zs) && float.TryParse(zs, System.Globalization.CultureInfo.InvariantCulture, out var zoom))
            _camera.Focus(_camera.Position, zoom);
        if (_args.TryGetValue("focus", out var fs) && fs.Split(',') is [var fx, var fy] && int.TryParse(fx, out var cx) && int.TryParse(fy, out var cy))
            _camera.Focus(new Vector2(cx + 0.5f, cy + 0.5f) * CellPx);
        if (_args.TryGetValue("fold", out var folds)) foreach (var f in folds.Split(',')) _hud.Fold(f);
        if (_args.TryGetValue("screenshot", out var shot)) { _screenshot = shot; _shotCountdown = 10; SetSpeed(0); }
        if (_args.TryGetValue("speed", out var sp) && int.TryParse(sp, out var speed)) SetSpeed(speed);
        _seenEvents = _host.Sim!.Chronicle.Events.Count;
    }

    SetupScreen? _setup;

    void ShowSetup(bool canCancel)
    {
        if (_setup != null) return;
        _voices.PlanningEnabled = false;
        _setup = new SetupScreen
        {
            Planet = _host.Sim?.Config.Planet ?? PlanetType.Terra,
            ModelStatus = () => (_voices.Director.Ready, _voices.Director.Status),
        };
        _setup.StartModels += _voices.StartModels;
        _hud.AddChild(_setup);
        _setup.CanCancel = canCancel;
        int resume = _host.SpeedIndex;
        SetSpeed(0);
        _setup.Begin += cfg => { CloseSetup(); NewWorld(cfg); SetSpeed(1); };
        _setup.Cancelled += () => { CloseSetup(); SetSpeed(resume); };
    }

    void CloseSetup()
    {
        _setup?.QueueFree();
        _setup = null;
        _voices.PlanningEnabled = true;
    }

    void NewWorld(SimConfig config)
    {
        _host.NewWorld(config);
        BindWorld();
    }

    void BindWorld()
    {
        _voices.PlanningEnabled = true;
        var sim = _host.Sim!;
        _nextAutosave = _settings.AutosaveDays > 0 ? sim.Day + _settings.AutosaveDays : int.MaxValue;
        _view.Bind(sim.World);
        _tiles.Bind(sim.World, Settlements.BakeInput(sim));
        _bands.Sim = sim;
        _target.Sim = sim;
        _target.WrapWidth = _bands.WrapWidth;
        _voices.Attach(sim);
        _voicesShown = -1;
        _camera.WorldSize = new Vector2(sim.World.W, sim.World.H) * CellPx;
        var center = sim.Bands.Values.Aggregate(Vector2.Zero, (acc, b) => acc + _bands.BandPosition(b)) / Math.Max(1, sim.Bands.Count);
        _camera.Focus(center, 2.5f);
        _seenEvents = 0; _feedEvents = -1; _mapDirty = true;
        ClearSelection();
        SetSpeed(_host.SpeedIndex == 0 ? 1 : _host.SpeedIndex);
    }

    void SetSpeed(int index)
    {
        _host.SpeedIndex = index;
        _hud.ShowSpeed(index);
    }

    public override void _Process(double delta)
    {
        var sim = _host.Sim;
        if (sim == null) return;
        _camera.KeyboardEnabled = sim.ControlledAgentId < 0;
        if (sim.Living.TryGetValue(sim.ControlledAgentId, out var controlled))
            _camera.Focus(new Vector2(sim.World.X(controlled.Site) + 0.5f, sim.World.Y(controlled.Site) + 0.5f) * CellPx);
        if (!_args.ContainsKey("screenshot") && !_args.ContainsKey("save-file") && _settings.AutosaveDays > 0 && sim.Day >= _nextAutosave && _setup == null)
        {
            _nextAutosave = sim.Day + _settings.AutosaveDays;
            try { _saves.Autosave(sim); _hud.Hint("World autosaved."); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException) { _hud.Hint("Autosave failed: " + e.Message); }
        }
        if (_following)
        {
            if (_sel == Selection.Agent && sim.Living.TryGetValue(_selId, out var person) && person.Site >= 0)
                _camera.Focus(new Vector2(sim.World.X(person.Site) + 0.5f, sim.World.Y(person.Site) + 0.5f) * CellPx);
            else if (_sel == Selection.Band && sim.Bands.TryGetValue(_selId, out var followed)) _camera.Focus(_bands.BandPosition(followed));
            else _following = false;
        }

        _mapTimer -= delta;
        if (_mapDirty || (sim.Day - _lastMapDay >= 5 && _mapTimer <= 0) || sim.Day < _lastMapDay)
        {
            _view.Redraw(sim);
            _tiles.Redraw(sim, _view.Overlay);
            _lastMapDay = sim.Day;
            _mapDirty = false;
            _mapTimer = 0.33;
        }
        _labels.QueueRedraw();
        if (sim.Day != _lastBandDay) { _bands.QueueRedraw(); _lastBandDay = sim.Day; }

        var events = sim.Chronicle.Events;
        for (int i = _seenEvents; i < events.Count; i++)
            if (events[i].Salience >= 0.4f && sim.Bands.TryGetValue(events[i].BandId, out var band))
                _bands.Ping(_bands.BandPosition(band), Describe.CategoryColor(events[i].Category));
        _seenEvents = events.Count;

        _uiTimer -= delta;
        if (_uiTimer <= 0)
        {
            _uiTimer = 0.25;
            RefreshHud(sim);
        }

        if (_sel == Selection.None && _voices.Recent.Count > 0 && _args.GetValueOrDefault("select") == "voice"
            && (!_args.ContainsKey("wait-choices") || _voices.Recent.Any(v => v.Moment.Kind == "decision")))
        {
            var n = _voices.Recent.LastOrDefault(v => v.Moment.Kind == "decision") ?? _voices.Recent[^1];
            Select(Selection.Agent, n.Moment.AgentId);
            if (sim.Bands.TryGetValue(n.Moment.BandId, out var vb)) _camera.Focus(_bands.BandPosition(vb), 4f);
            _labels.Say(n.Moment.BandId, n.Quote);
        }

        sim.DeferDecisions = true;

        _voices.FocusAgentId = _sel == Selection.Agent ? _selId : -1;
        _voices.FocusBandId = _bands.SelectedBandId;

        if (_shotCountdown > 0)
        {
            int want = _args.TryGetValue("wait-voices", out var wv) && int.TryParse(wv, out var n) ? n : 0;
            int wantChoices = _args.TryGetValue("wait-choices", out var wc) && int.TryParse(wc, out var k) ? k : 0;
            int wantMinds = _args.TryGetValue("wait-minds", out var wm) && int.TryParse(wm, out var minds) ? minds : 0;
            _shotWait += delta;
            bool ready = _voices.Director.Accepted >= wantMinds && _voices.Recent.Count >= want && _voices.Recent.Count(v => v.Moment.Kind == "decision") >= wantChoices && !_tiles.Busy && (_setup == null || _setup.PreviewReady);
            if (ready && wantChoices > 0 && _host.SpeedIndex != 0) SetSpeed(0);
            if (ready || _shotWait > 240) { if (--_shotCountdown == 0) TakeScreenshot(); }
        }
    }

    void RefreshHud(Simulation sim)
    {
        int year = sim.Year, doy = sim.Day % SimConfig.DaysPerYear;
        string[] seasons = { "spring", "summer", "autumn", "winter" };
        _hud.Date.Text = $"Year {year}, {seasons[doy / 90]} (day {doy + 1})";
        int cultures = sim.Bands.Values.Select(b => b.CultureId).Distinct().Count();
        TechSet known = default; foreach (var a in sim.Living.Values) known |= a.Knows;
        _hud.Stats.Text = sim.Living.Count == 0 ? "Humanity is gone."
            : $"{sim.Living.Count} people · {sim.Bands.Count} bands · {cultures} culture{(cultures == 1 ? "" : "s")} · {Evolysia.Sim.Agents.Techs.CountKnown(known)} techniques";
        _hud.ShowMessageCharge(sim.CanMessage ? 0 : (sim.NextMessageDay - sim.Day + SimConfig.DaysPerYear - 1) / SimConfig.DaysPerYear);
        _hud.Perf.Text = _host.Paused ? "paused" : $"{_host.ActualDaysPerSecond / SimHost.Speeds[1]:0.0}×";
        _hud.Perf.TooltipText = $"{_host.ActualDaysPerSecond:0} days a second ({_host.MsPerDay:0.0} ms a day); a big world may run slower than the chosen speed";

        int count = sim.Chronicle.Events.Count;
        if (count != _feedEvents)
        {
            _hud.Feed.Text = Describe.Feed(sim);
            _feedEvents = count;
        }
        _hud.VoiceStatus.Text = _voices.Director.Ready ? $"Minds · {_voices.Director.InFlight} thinking" : "Model required";
        _hud.VoiceStatus.AddThemeColorOverride("font_color", _voices.Director.Ready ? Palette.Moss.Lightened(0.25f) : Palette.Terracotta);
        _hud.VoiceStatus.TooltipText = _voices.Director.Status;
        _hud.StartModels.Visible = !_voices.Director.Ready;
        if (_voices.Recent.Count != _voicesShown || _voices.Recent.Count == 0)
        {
            _hud.Voices.Text = Describe.Voices(sim, _voices.Recent);
            _voicesShown = _voices.Recent.Count;
        }
        string prayers = Describe.Prayers(sim);
        _hud.PrayersList.Visible = prayers.Length > 0;
        if (_hud.PrayersList.Text != prayers) _hud.PrayersList.Text = prayers;
        RefreshInspector(sim);
    }

    bool HoldForMinds()
    {
        return _voices.Director.Hold(_host.Sim!);
    }

    string SavePath => _args.GetValueOrDefault("save-file") ?? ProjectSettings.GlobalizePath("user://world.evo");

    void SaveWorld()
    {
        if (_host.Sim == null) return;
        int speed = _host.SpeedIndex; SetSpeed(0);
        try
        {
            if (_args.ContainsKey("save-file")) WorldSave.Save(_host.Sim, SavePath);
            else _saves.Save(_host.Sim, "quick", "Quick save");
            _hud.Hint("World saved.");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException) { _hud.Hint("Could not save: " + e.Message); }
        SetSpeed(speed);
    }

    void LoadWorld()
    {
        if (!_args.ContainsKey("save-file")) { ShowSaves(); return; }
        SetSpeed(0);
        try
        {
            var loaded = WorldSave.Load(SavePath);
            CloseSetup(); _host.Restore(loaded); BindWorld(); SetSpeed(0);
            _hud.Hint("World loaded. Connect the model, then resume.");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or EndOfStreamException)
        { _hud.Hint("Could not load: " + e.Message); }
    }

    void ShowSaves()
    {
        if (_host.Sim == null) return;
        SetSpeed(0);
        var dialog = new SaveBrowser { Repository = _saves, Current = _host.Sim, LegacyPath = ProjectSettings.GlobalizePath("user://world.evo") };
        dialog.Loaded += loaded => { CloseSetup(); _host.Restore(loaded); BindWorld(); SetSpeed(0); _hud.Hint("World loaded. Resume when ready."); };
        dialog.Notice += message => _hud.Hint(message); _hud.AddChild(dialog); dialog.PopupCentered();
    }

    void ShowSettings()
    {
        SetSpeed(0); var dialog = new SettingsDialog { Settings = _settings };
        dialog.Applied += ApplySettings; _hud.AddChild(dialog); dialog.PopupCentered();
    }

    void ApplySettings()
    {
        GetWindow().ContentScaleFactor = _settings.UiScale;
        GetWindow().Mode = _settings.Fullscreen ? Window.ModeEnum.Fullscreen : Window.ModeEnum.Windowed;
        _bands.ShowShadows = _settings.Shadows; _bands.ReducedMotion = _settings.ReducedMotion; _bands.QueueRedraw();
        _target.ReducedMotion = _settings.ReducedMotion; _labels.Visible = _settings.WorldLabels;
        _nextAutosave = _settings.AutosaveDays > 0 && _host.Sim != null ? _host.Sim.Day + _settings.AutosaveDays : int.MaxValue;
    }

    void ShowConnection()
    {
        var dialog = new ConfirmationDialog { Title = "Model connection", MinSize = new Vector2I(540, 200), OkButtonText = "Connect" };
        var box = new VBoxContainer(); dialog.AddChild(box);
        box.AddChild(new Label { Text = "Model address" });
        var address = new LineEdit { Text = _voices.Endpoint }; box.AddChild(address);
        box.AddChild(new Label { Text = "Model name" });
        var model = new LineEdit { Text = _voices.Model }; box.AddChild(model);
        dialog.Confirmed += () => _hud.Hint(_voices.Configure(address.Text, model.Text) ? "Connecting to the model…" : "Enter a valid model address and name.");
        dialog.VisibilityChanged += () => { if (!dialog.Visible) dialog.QueueFree(); };
        _hud.AddChild(dialog); dialog.PopupCentered();
    }

    void ShowOutfit()
    {
        if (_sel != Selection.Agent || _host.Sim?.Living.GetValueOrDefault(_selId) is not { } person)
        { _hud.Hint("Select a living person to change their appearance."); return; }
        var editor = new OutfitEditor { Person = person };
        editor.Applied += () => { _bands.QueueRedraw(); RefreshInspector(_host.Sim!); };
        _hud.AddChild(editor); editor.PopupCentered();
    }

    void ToggleFollow()
    {
        _following = !_following && _sel is Selection.Agent or Selection.Band;
        _hud.Hint(_following ? "Following the selection. F stops following." : "Camera released.");
    }

    void OnPowerPicked(Power? power)
    {
        if (power is not { } p) { Disarm(); return; }
        var sim = _host.Sim!;
        if (Powers.NeedsPerson(p))
        {
            if (_sel != Selection.Agent || !sim.Living.TryGetValue(_selId, out var person))
            {
                Disarm();
                _hud.Hint($"{p}: first select a living person (click a band, then a name).");
                return;
            }
            if (p == Power.Possess)
            {
                Disarm();
                Unleash(new GodAction(p, person.Site >= 0 ? person.Site : sim.Bands[person.BandId].Cell, TargetAgentId: person.Id));
                _following = false;
                _hud.Hint(sim.ControlledAgentId >= 0 ? "Possessing: WASD/arrows move; E forage; C chop; Q quarry; R rest; X release. Time must be running." : "Character released.");
            }
            else if (p == Power.Curse)
            {
                Unleash(new GodAction(Power.Curse, sim.Bands.TryGetValue(person.BandId, out var b) ? b.Cell : 0, TargetAgentId: person.Id));
                Disarm();
            }
            else
            {
                _armed = p;
                _hud.Hint($"Speak to {person.Name} in a dream.");
                _hud.DreamDialog.Visible = true;
            }
            return;
        }
        if (p == Power.Message && !sim.CanMessage)
        {
            Disarm();
            _hud.Hint($"Your words can be given again in year {sim.NextMessageDay / SimConfig.DaysPerYear}.");
            return;
        }
        _armed = p;
        _target.Armed = p;
        _hud.ShowPower(p);
        _hud.Hint(p == Power.Message ? "Message: click where your words should appear. Esc cancels."
                                     : $"{p}: click the map to unleash it (Shift-click to keep it ready). Esc cancels.");
    }

    void Disarm()
    {
        _armed = null;
        _target.Armed = null;
        _hud.ShowPower(null);
        _hud.Hint("");
        _hud.DreamDialog.Visible = false;
        _hud.MessageDialog.Visible = false;
        _messageCell = -1;
    }

    int _messageCell = -1;

    void Unleash(GodAction action)
    {
        var sim = _host.Sim!;
        sim.Act(action);
        if (action.Power is Power.RaiseLand or Power.LowerLand or Power.SpawnPlant or Power.Earthquake or Power.Meteor or Power.Wildfire) _tiles.InvalidateTerrain();
        var at = new Vector2(sim.World.X(action.Cell) + 0.5f, sim.World.Y(action.Cell) + 0.5f) * CellPx;
        _target.WrapWidth = _bands.WrapWidth;
        _target.Burst(at, TargetOverlay.RadiusPx(action.Power), Powers.IsKind(action.Power));
        _mapDirty = true;
        _bands.QueueRedraw();
        _uiTimer = 0;
    }

    void RefreshInspector(Simulation sim)
    {
        _hud.Portrait.Visible = _sel == Selection.Agent;
        if (_sel == Selection.Agent && sim.Find(_selId) is { } subject)
            _hud.Portrait.Texture = Portraits.Get(subject, sim.Day, sim.Cultures.GetValueOrDefault(subject.CultureId)?.Hue ?? 0);
        string? text = _sel switch
        {
            Selection.Band when sim.Bands.TryGetValue(_selId, out var b) => Describe.Band(sim, b),
            Selection.Agent when sim.Find(_selId) is { } a => Describe.Person(sim, a, _voices.Narrator.ForAgent(a.Id)),
            Selection.Cell => _view.DescribeCell(sim, _selId),
            _ => null,
        };
        if (text == null) { ClearSelection(); return; }

        var scrollbar = _hud.Inspector.GetVScrollBar();
        double scroll = scrollbar.Value;
        if (_hud.Inspector.Text == text) return;
        _hud.Inspector.Text = text;
        _hud.InspectorPanel.Visible = true;
        scrollbar.SetDeferred(Godot.Range.PropertyName.Value, scroll);
    }

    void Select(Selection kind, int id)
    {
        _sel = kind; _selId = id;
        var sim = _host.Sim!;
        _bands.SelectedBandId = kind switch
        {
            Selection.Band => id,
            Selection.Agent when sim.Find(id) is { Alive: true } a => a.BandId,
            _ => -1,
        };
        _bands.QueueRedraw();
        RefreshInspector(sim);
    }

    void ClearSelection()
    {
        _following = false;
        _sel = Selection.None; _selId = -1;
        _bands.SelectedBandId = -1;
        _bands.QueueRedraw();
        _hud.InspectorPanel.Visible = false;
    }

    void OnLink(string link)
    {
        var parts = link.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[1], out int id)) return;
        var sim = _host.Sim!;
        if (parts[0] == "band" && sim.Bands.TryGetValue(id, out var band))
        {
            Select(Selection.Band, id);
            _camera.Focus(_bands.BandPosition(band));
        }
        else if (parts[0] == "agent") Select(Selection.Agent, id);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click && _armed is { } armed && !Powers.NeedsPerson(armed))
        {
            var sim = _host.Sim!;
            var at = GetGlobalMousePosition();
            int cx = sim.World.Wrap((int)MathF.Floor(at.X / CellPx)), cy = Math.Clamp((int)MathF.Floor(at.Y / CellPx), 0, sim.World.H - 1);
            if (armed == Power.Message)
            {
                _messageCell = sim.World.Index(cx, cy);
                _target.Armed = null;
                var nearby = sim.Bands.Values.Count(b => sim.World.Distance(b.Cell, _messageCell) <= sim.World.Cells(0.8f));
                _hud.AskMessage($"At {cx}, {cy}: {sim.World.Biome[_messageCell]}. " +
                                (nearby == 0 ? "No one lives near yet; it will wait for whoever comes." : $"{nearby} band{(nearby == 1 ? "" : "s")} near enough to find it."));
            }
            else
            {
                Unleash(new GodAction(armed, sim.World.Index(cx, cy)));
                if (!click.ShiftPressed) Disarm();
            }
            GetViewport().SetInputAsHandled();
            return;
        }
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            var sim = _host.Sim!;
            var pos = GetGlobalMousePosition();
            var band = _bands.BandAt(pos, 3f / _camera.Zoom.X + 1f);
            if (band != null) Select(Selection.Band, band.Id);
            else
            {
                int cx = sim.World.Wrap((int)MathF.Floor(pos.X / CellPx)), cy = (int)MathF.Floor(pos.Y / CellPx);
                if (cy >= 0 && cy < sim.World.H && cx >= 0 && cx < sim.World.W) Select(Selection.Cell, sim.World.Index(cx, cy));
            }
        }
        else if (e is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (ControlKey(key.Keycode)) { GetViewport().SetInputAsHandled(); return; }
            switch (key.Keycode)
            {
                case Key.Space: SetSpeed(_host.Paused ? 1 : 0); break;
                case Key.F5: SaveWorld(); break;
                case Key.F9: LoadWorld(); break;
                case Key.F: ToggleFollow(); break;
                case >= Key.Key1 and <= Key.Key6: SetSpeed((int)(key.Keycode - Key.Key1) + 1); break;
                case Key.Escape: if (_armed != null) Disarm(); else ClearSelection(); break;
                case Key.Tab:
                    _view.Overlay = (Overlay)(((int)_view.Overlay + 1) % Enum.GetValues<Overlay>().Length);
                    _hud.OverlayPick.Select((int)_view.Overlay);
                    _mapDirty = true;
                    break;
                default: return;
            }
            GetViewport().SetInputAsHandled();
        }
    }

    bool ControlKey(Key key)
    {
        var sim = _host.Sim!;
        if (!sim.Living.TryGetValue(sim.ControlledAgentId, out var person)) return false;
        if (key == Key.X) { sim.ReleaseControl(); _hud.Hint("Character released."); return true; }
        int dx = key is Key.A or Key.Left ? -1 : key is Key.D or Key.Right ? 1 : 0;
        int dy = key is Key.W or Key.Up ? -1 : key is Key.S or Key.Down ? 1 : 0;
        Activity? work = key switch { Key.E => Activity.Forage, Key.C => Activity.Chop, Key.Q => Activity.Quarry, Key.R => Activity.Rest, _ => null };
        if (dx == 0 && dy == 0 && work == null) return false;
        int y = sim.World.Y(person.Site) + dy;
        bool accepted = y >= 0 && y < sim.World.H && sim.CommandPerson(sim.World.Index(sim.World.X(person.Site) + dx, y), work ?? Activity.Explore);
        _hud.Hint(accepted ? "Possessing: WASD/arrows move; E forage; C chop; Q quarry; R rest; X release. Time must be running." : "That route or activity is unavailable.");
        return true;
    }

    void SelectForDemo(string what)
    {
        var sim = _host.Sim!;
        var band = sim.Bands.Values.OrderByDescending(b => b.Members.Count).FirstOrDefault();
        if (band == null) return;
        _camera.Focus(_bands.BandPosition(band), 4f);
        if (what == "agent")
        {

            var person = band.Members.Select(id => sim.Living[id]).Where(a => a.IsAdult(sim.Day))
                .OrderByDescending(a => a.Memory.Count + a.Scars * 2 + (a.SuppressedDomains != 0 ? 3 : 0)).First();
            Select(Selection.Agent, person.Id);
        }
        else Select(Selection.Band, band.Id);
    }

    void DemoVillage()
    {
        var sim = _host.Sim!;
        if (_sel != Selection.Band || !sim.Bands.TryGetValue(_selId, out var band)) return;

        TechSet all = default;
        foreach (var t in Techs.All)
            if (t.Stage is "foraging" or "stonecraft" or "agriculture" or "metallurgy") all |= Techs.Bit(t.Id);
        foreach (int id in band.Members) if (sim.Living.TryGetValue(id, out var a)) a.Knows |= all;
        band.Sedentary = true;
        foreach (var (good, qty) in new[] { ("log", 60f), ("wood", 60f), ("fiber", 60f), ("stone", 40f), ("clay", 30f), ("reed", 30f), ("hide", 20f), ("rope", 10f), ("grain", 200f) })
            band.Stores.Add(good, qty);
        band.Herds["sheep"] = 8f; band.Herds["dog"] = 2f; band.Herds["fowl"] = 6f;
        _host.FastForward(2 * Evolysia.Sim.SimConfig.DaysPerYear);
        _tiles.Redraw(sim, _view.Overlay);
        _mapDirty = true;
    }

    void DemoGods()
    {
        var sim = _host.Sim!;
        var band = sim.Bands.Values.OrderByDescending(b => b.Members.Count).FirstOrDefault();
        if (band == null) return;
        for (int i = 0; i < 2; i++) { sim.Act(new GodAction(Power.Heal, band.Cell)); _host.FastForward(3); }
        sim.Act(new GodAction(Power.Bless, band.Cell));
        _host.FastForward(31);
        if (sim.Living.ContainsKey(band.LeaderId))
            sim.Act(new GodAction(Power.Dream, band.Cell, TargetAgentId: band.LeaderId, Dream: DreamKind.Peace,
                                  Words: "Put down your spears. The ones beyond the river are your kin."));
        _voices.Attach(sim, 6);
        _camera.Focus(_bands.BandPosition(band), 4f);
        if (sim.Living.ContainsKey(band.LeaderId)) Select(Selection.Agent, band.LeaderId); else Select(Selection.Band, band.Id);
        _target.Armed = Power.Rain;
        _target.QueueRedraw();
    }

    void DemoMessage()
    {
        var sim = _host.Sim!;
        var band = sim.Bands.Values.OrderByDescending(b => b.Members.Count).FirstOrDefault();
        if (band == null) return;
        int cell = sim.World.Index(sim.World.X(band.Cell) + 2, sim.World.Y(band.Cell) + 1);
        sim.Act(new GodAction(Power.Message, cell, Words: "Share the hunt with the strangers by the river", Medium: MessageMedium.Ground));
        _host.FastForward(40);
        _camera.Focus(new Vector2(sim.World.X(cell) + 0.5f, sim.World.Y(cell) - 9.5f) * CellPx, 3f);
        Select(Selection.Cell, cell);
        _hud.AskMessage("At the river bend: temperate grassland. 2 bands near enough to find it.");
    }

    void TakeScreenshot()
    {
        if (_args.ContainsKey("save-on-shot")) SaveWorld();
        using var image = GetViewport().GetTexture().GetImage();
        string path = _screenshot!;
        DirAccess.MakeDirRecursiveAbsolute(path.GetBaseDir());
        var err = image.SavePng(path);
        GD.Print($"Screenshot {(err == Error.Ok ? "saved" : "FAILED: " + err)}: {path}");
        GetTree().Quit();
    }

    public override void _ExitTree() { BandLayer.ReleaseShared(); Typeface.Release(); }
}
