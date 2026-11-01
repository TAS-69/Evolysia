using Evolysia.Sim;
using Evolysia.Sim.Persistence;
using Godot;

namespace Evolysia.Game;

public partial class SaveBrowser : ConfirmationDialog
{
    public SaveRepository Repository = null!;
    public Simulation Current = null!;
    public string LegacyPath = "";
    public event Action<Simulation>? Loaded;
    public event Action<string>? Notice;
    SaveSlot[] _slots = Array.Empty<SaveSlot>();
    ItemList _list = null!;
    LineEdit _title = null!;
    Label _status = null!;

    public override void _Ready()
    {
        Theme = Ui.Build(); Title = "World saves"; MinSize = new Vector2I(700, 470); OkButtonText = "Load selected";
        var box = new VBoxContainer(); AddChild(box);
        box.AddChild(new Label { Text = "Loading replaces this session. Save the current world first if you want to keep it." });
        _list = new ItemList { CustomMinimumSize = new Vector2(670, 260), SizeFlagsVertical = Control.SizeFlags.ExpandFill }; box.AddChild(_list);
        _title = new LineEdit { PlaceholderText = "Name this world", MaxLength = 80, Text = $"Seed {Current.Config.Seed} · year {Current.Year}" }; box.AddChild(_title);
        var row = new HBoxContainer(); box.AddChild(row);
        var save = new Button { Text = "Save new slot" }; row.AddChild(save); save.Pressed += SaveNew;
        var delete = new Button { Text = "Delete selected…" }; row.AddChild(delete); delete.Pressed += DeleteSelected;
        if (System.IO.File.Exists(LegacyPath))
        {
            var legacy = new Button { Text = "Load previous quick save" }; row.AddChild(legacy);
            legacy.Pressed += () => Attempt(() => { Loaded?.Invoke(WorldSave.Load(LegacyPath)); Hide(); });
        }
        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(670, 40) }; box.AddChild(_status);
        DialogHideOnOk = false;
        Confirmed += () =>
        {
            var selected = _list.GetSelectedItems(); if (selected.Length == 0) return;
            Attempt(() => { Loaded?.Invoke(Repository.Load(_slots[selected[0]].Id)); Hide(); });
        };
        VisibilityChanged += () => { if (!Visible) QueueFree(); };
        Refresh();
    }

    void Refresh()
    {
        _slots = Repository.List().ToArray(); _list.Clear();
        foreach (var slot in _slots) _list.AddItem($"{slot.Title}  ·  {slot.Population} people  ·  {slot.SavedUtc.ToLocalTime():g}");
        if (_slots.Length > 0) _list.Select(0);
        _status.Text = _slots.Length == 0 ? "No saved slots yet." : $"{_slots.Length} saved worlds. Autosaves rotate separately from named saves.";
    }

    void Attempt(Action operation)
    {
        try { operation(); }
        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { _status.Text = e.Message; Notice?.Invoke(e.Message); }
    }

    void SaveNew() => Attempt(() => { Repository.SaveNew(Current, _title.Text); Refresh(); _status.Text = "World saved."; });

    void DeleteSelected()
    {
        var selected = _list.GetSelectedItems(); if (selected.Length == 0) return;
        var slot = _slots[selected[0]];
        var confirm = new ConfirmationDialog { Title = "Delete saved world?", DialogText = $"Delete “{slot.Title}”? This cannot be undone.", Theme = Ui.Build() };
        AddChild(confirm);
        confirm.Confirmed += () => Attempt(() => { Repository.Delete(slot.Id); Refresh(); });
        confirm.VisibilityChanged += () => { if (!confirm.Visible) confirm.QueueFree(); }; confirm.PopupCentered();
    }
}
