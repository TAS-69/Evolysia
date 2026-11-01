using Evolysia.Sim;
using Evolysia.Sim.World;
using Godot;

namespace Evolysia.Game;

public partial class SetupScreen : Control
{
    public event Action<SimConfig>? Begin;
    public event Action? Cancelled;
    public event Action? StartModels;
    public Func<(bool ready, string status)>? ModelStatus;
    Label _modelStatus = null!;

    LineEdit _seed = null!;
    HSlider _tribes = null!;
    Label _tribesLabel = null!, _status = null!, _placeHint = null!;
    Button _scatter = null!, _place = null!, _begin = null!, _cancel = null!;
    readonly List<Button> _planetButtons = new();
    TextureRect _map = null!;
    SetupMarkers _markers = null!;
    PlanetType _planet = PlanetType.Terra;
    WorldMap? _preview;
    Task<WorldMap>? _generating;
    (ulong seed, PlanetType planet) _wanted, _shown;
    readonly List<int> _placed = new();

    static readonly (PlanetType type, string name, string blurb, Color colour)[] Planets =
    {
        (PlanetType.Terra, "Terra", "Balanced continents, every climate", new Color("8fb36a")),
        (PlanetType.Ignis, "Ignis", "Hot, dry and volcanic", new Color("d9784e")),
        (PlanetType.Cryos, "Cryos", "Cold, ice-bound, short summers", new Color("9fc8e0")),
        (PlanetType.Thalassia, "Thalassia", "An ocean world of small islands", new Color("5fa6c9")),
    };

    public bool CanCancel { set => _cancel.Visible = value; }

    public PlanetType Planet { set => _planet = value; }

    public bool PreviewReady => _preview != null && _generating == null && _wanted == _shown;
    bool Placing => _place.ButtonPressed;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Theme = Ui.Build();
        MouseFilter = MouseFilterEnum.Stop;
        var dim = new ColorRect { Color = new Color(Palette.Bg, 0.82f), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(dim);
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var centre = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(centre);
        centre.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(1060, 600) };
        centre.AddChild(panel);
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 22);
        panel.AddChild(columns);

        var left = new VBoxContainer { CustomMinimumSize = new Vector2(330, 0) };
        left.AddThemeConstantOverride("separation", 10);
        columns.AddChild(left);
        var logo = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://branding/evolysia-logo.png"),
            CustomMinimumSize = new Vector2(330, 109),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = TextureFilterEnum.Linear,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        left.AddChild(logo);
        var title = new Label { Text = "A new world" };
        title.AddThemeFontSizeOverride("font_size", 24);
        title.AddThemeColorOverride("font_color", Palette.Divine);
        left.AddChild(title);
        var sub = new Label { Text = "Shape it, seed it with people, and watch.", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        sub.AddThemeColorOverride("font_color", Ui.Muted);
        left.AddChild(sub);

        left.AddChild(Ui.Heading("Seed"));
        var seedRow = new HBoxContainer();
        _seed = new LineEdit { Text = ((ulong)GD.Randi()).ToString(), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _seed.TextChanged += _ => Regenerate();
        seedRow.AddChild(_seed);
        var dice = new Button { Text = "Roll", TooltipText = "A random seed", FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(36, 0) };
        dice.Pressed += () => { _seed.Text = ((ulong)GD.Randi()).ToString(); Regenerate(); };
        seedRow.AddChild(dice);
        left.AddChild(seedRow);

        left.AddChild(Ui.Heading("Planet"));
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 8);
        var group = new ButtonGroup();
        foreach (var (type, name, blurb, colour) in Planets)
        {
            var b = new Button
            {
                Text = $"{name}\n{blurb}", ToggleMode = true, ButtonGroup = group, ButtonPressed = type == _planet, FocusMode = FocusModeEnum.None,
                CustomMinimumSize = new Vector2(158, 58), AutowrapMode = TextServer.AutowrapMode.WordSmart, Alignment = HorizontalAlignment.Left,
            };
            b.AddThemeColorOverride("font_pressed_color", colour);
            b.AddThemeColorOverride("font_hover_pressed_color", colour);
            b.AddThemeFontSizeOverride("font_size", 12);
            var accent = (StyleBoxFlat)Ui.Card(colour);
            accent.BgColor = new Color(colour, 0.16f);
            b.AddThemeStyleboxOverride("pressed", accent);
            b.AddThemeStyleboxOverride("hover_pressed", accent);
            b.Pressed += () => { _planet = type; _placed.Clear(); Regenerate(); };
            _planetButtons.Add(b);
            grid.AddChild(b);
        }
        left.AddChild(grid);

        left.AddChild(Ui.Heading("Peoples"));
        var tribeRow = new HBoxContainer();
        _tribes = new HSlider { MinValue = 1, MaxValue = 6, Step = 1, Value = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill, FocusMode = FocusModeEnum.None };
        _tribes.ValueChanged += _ => { TrimPlaced(); Refresh(); };
        tribeRow.AddChild(_tribes);
        _tribesLabel = new Label { CustomMinimumSize = new Vector2(96, 0), HorizontalAlignment = HorizontalAlignment.Right };
        tribeRow.AddChild(_tribesLabel);
        left.AddChild(tribeRow);
        var hint = new Label { Text = "Each is its own people, with its own tongue and ways.", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        hint.AddThemeColorOverride("font_color", Ui.Muted);
        hint.AddThemeFontSizeOverride("font_size", 12);
        left.AddChild(hint);

        var placeRow = new HBoxContainer();
        var placing = new ButtonGroup();
        _scatter = new Button { Text = "Scatter at random", ToggleMode = true, ButtonGroup = placing, ButtonPressed = true, FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _place = new Button { Text = "Place on the map", ToggleMode = true, ButtonGroup = placing, FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _scatter.Pressed += Refresh;
        _place.Pressed += Refresh;
        placeRow.AddChild(_scatter);
        placeRow.AddChild(_place);
        left.AddChild(placeRow);

        left.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        _cancel = new Button { Text = "Back", FocusMode = FocusModeEnum.None, Visible = false };
        _cancel.Pressed += () => Cancelled?.Invoke();
        actions.AddChild(_cancel);
        actions.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _begin = new Button { Text = "Begin", FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(140, 38) };
        _begin.AddThemeFontSizeOverride("font_size", 16);
        _begin.AddThemeColorOverride("font_color", Palette.Divine);
        _begin.Pressed += OnBegin;
        actions.AddChild(_begin);
        left.AddChild(actions);

        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 8);
        columns.AddChild(right);
        _map = new TextureRect
        {
            CustomMinimumSize = new Vector2(680, 340), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale, TextureFilter = TextureFilterEnum.Nearest, MouseFilter = MouseFilterEnum.Stop,
        };
        _map.GuiInput += OnMapInput;
        right.AddChild(_map);
        _markers = new SetupMarkers { AnchorRight = 1, AnchorBottom = 1, MouseFilter = MouseFilterEnum.Ignore, Source = () => (_preview, _placed, Placing) };
        _map.AddChild(_markers);
        _placeHint = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _placeHint.AddThemeColorOverride("font_color", Ui.Muted);
        right.AddChild(_placeHint);
        _status = new Label();
        _status.AddThemeColorOverride("font_color", Ui.Muted);
        _status.AddThemeFontSizeOverride("font_size", 12);
        right.AddChild(_status);
        right.AddChild(Ui.Heading("Living minds"));
        _modelStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(680, 46) };
        right.AddChild(_modelStatus);
        var modelStart = new Button { Text = "Set up / start local model", TooltipText = "Installs the runtime if needed, then starts your configured model. The first start downloads model weights." };
        modelStart.Pressed += () => StartModels?.Invoke();
        right.AddChild(modelStart);

        Regenerate();
        Refresh();
    }

    static ulong StableSeed(string text)
    {
        ulong seed = 14695981039346656037UL;
        foreach (byte value in System.Text.Encoding.UTF8.GetBytes(text)) { seed ^= value; seed = unchecked(seed * 1099511628211UL); }
        return seed;
    }

    ulong Seed => ulong.TryParse(_seed.Text.Trim(), out var s) ? s : StableSeed(_seed.Text.Trim());

    void Regenerate()
    {
        _wanted = (Seed, _planet);
        _status.Text = "Shaping the planet…";
    }

    public override void _Process(double delta)
    {
        var connection = ModelStatus?.Invoke() ?? (false, "A model connection is required.");
        _modelStatus.Text = connection.Item2;
        _begin.Disabled = !connection.Item1 || Placing && _placed.Count < (int)_tribes.Value;

        if (_generating == null && _wanted != _shown)
        {
            var (seed, planet) = _wanted;
            _generating = Task.Run(() => WorldGen.Generate(new SimConfig { Seed = seed, Planet = planet }));
            _shown = _wanted;
        }
        if (_generating is { IsCompleted: true } done)
        {
            _generating = null;
            if (done.IsCompletedSuccessfully && _shown == _wanted)
            {
                _preview = done.Result;
                _map.Texture = Paint(_preview);
                if (_placed.Any(c => c >= _preview.Count || !Liveable(c))) _placed.Clear();
                int land = Enumerable.Range(0, _preview.Count).Count(_preview.IsDryLand);
                _status.Text = $"{_preview.W} x {_preview.H} cells · {land * 100 / _preview.Count}% land · {_preview.LandmassSize.Length} landmasses";
                _markers.QueueRedraw();
            }
        }
    }

    static ImageTexture Paint(WorldMap w)
    {
        var image = Image.CreateEmpty(w.W, w.H, false, Image.Format.Rgb8);
        for (int y = 0; y < w.H; y++)
            for (int x = 0; x < w.W; x++)
            {
                int i = w.Index(x, y);
                var c = Palette.Biome(w.Biome[i]);
                if (w.IsLand(i)) c = c.Darkened(Math.Clamp(-(w.Elevation[w.Index(x + 1, y)] - w.Elevation[i]) / 600f, -0.15f, 0.25f));
                if (w.Volcano[i]) c = new Color("e0552d");
                image.SetPixel(x, y, c);
            }
        return ImageTexture.CreateFromImage(image);
    }

    bool Liveable(int cell) => _preview != null && _preview.IsDryLand(cell) && _preview.PlantK[cell] > 0f;

    void OnMapInput(InputEvent e)
    {
        if (!Placing || _preview == null || e is not InputEventMouseButton { Pressed: true } mb) return;
        var size = _map.Size;
        int x = (int)(mb.Position.X / size.X * _preview.W), y = (int)(mb.Position.Y / size.Y * _preview.H);
        if (x < 0 || y < 0 || x >= _preview.W || y >= _preview.H) return;
        int cell = _preview.Index(x, y);
        if (mb.ButtonIndex == MouseButton.Right)
        {
            if (_placed.Count > 0) _placed.RemoveAt(_placed.Select((c, k) => (d: _preview.Distance(c, cell), k)).MinBy(t => t.d).k);
        }
        else if (mb.ButtonIndex == MouseButton.Left)
        {
            if (!Liveable(cell)) { _placeHint.Text = "No one could live there - choose land with something to eat."; return; }
            if (_placed.Count >= (int)_tribes.Value) _placed.RemoveAt(0);
            _placed.Add(cell);
        }
        Refresh();
    }

    void TrimPlaced()
    {
        while (_placed.Count > (int)_tribes.Value) _placed.RemoveAt(_placed.Count - 1);
    }

    void Refresh()
    {
        int n = (int)_tribes.Value;
        _tribesLabel.Text = n == 1 ? "1 people" : $"{n} peoples";
        _placeHint.Text = Placing
            ? $"Click the map to place each people ({_placed.Count} of {n} placed). Right-click removes one."
            : "They will wake in good land, well apart from one another.";
        _begin.Disabled = Placing && _placed.Count < n;
        _markers.QueueRedraw();
    }

    void OnBegin()
    {
        if (ModelStatus?.Invoke().ready != true) return;
        var cfg = new SimConfig
        {
            Seed = Seed, Planet = _planet, Tribes = (int)_tribes.Value,
            TribeCells = Placing ? _placed.ToArray() : Array.Empty<int>(),
            Threads = Math.Max(1, System.Environment.ProcessorCount - 2),
        };
        Begin?.Invoke(cfg);
    }
}

public partial class SetupMarkers : Control
{
    public Func<(WorldMap? map, IReadOnlyList<int> placed, bool show)>? Source;

    public override void _Draw()
    {
        if (Source?.Invoke() is not var (w, placed, show) || w == null || !show) return;
        var font = ThemeDB.FallbackFont;
        for (int k = 0; k < placed.Count; k++)
        {
            int c = placed[k];
            var p = new Vector2((w.X(c) + 0.5f) / w.W * Size.X, (w.Y(c) + 0.5f) / w.H * Size.Y);
            var colour = Palette.Culture(k * 5 % 12);
            DrawCircle(p, 9f, new Color(Palette.Bg, 0.8f));
            DrawArc(p, 9f, 0, Mathf.Tau, 24, colour, 2.5f, true);
            DrawString(font, p + new Vector2(-4f, 5f), (k + 1).ToString(), HorizontalAlignment.Left, -1, 13, Palette.Bone);
        }
    }
}
