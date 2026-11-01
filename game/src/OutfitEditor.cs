using Evolysia.Sim.Agents;
using Evolysia.Sim.Economy;
using Godot;

namespace Evolysia.Game;

public partial class OutfitEditor : ConfirmationDialog
{
    public Agent Person = null!;
    public event Action? Applied;
    readonly Dictionary<string, (OptionButton part, OptionButton main, OptionButton trim)> _pieces = new();
    OptionButton _hair = null!, _skin = null!, _tone = null!;
    readonly Dictionary<string, OptionButton> _features = new();

    public override void _Ready()
    {
        Theme = Ui.Build();
        Title = $"{Person.Name}'s appearance"; MinSize = new Vector2I(700, 560); OkButtonText = "Apply";
        var scroll = new ScrollContainer(); AddChild(scroll);
        var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; scroll.AddChild(box);
        box.AddChild(new Label { Text = "Requested clothes are made when knowledge, materials and a crafter are available.", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(660, 40) });
        var tones = new HBoxContainer(); box.AddChild(tones);
        _skin = TonePicker("Inherited skin", 8, Person.Appearance.Skin);
        _tone = TonePicker("Inherited hair colour", 8, Person.Appearance.Hair);
        tones.AddChild(_skin); tones.AddChild(_tone);
        _hair = new OptionButton(); _hair.AddItem("Inherited hairstyle");
        var data = Art.Json("wardrobe/customisation.json");
        if (data is { } json)
        {
            foreach (var style in json.GetProperty("hair_styles").EnumerateObject()) _hair.AddItem(style.Name);
            for (int i = 1; i < _hair.ItemCount; i++) if (_hair.GetItemText(i) == Person.Appearance.HairStyle) _hair.Select(i);
        }
        box.AddChild(_hair);
        var build = new OptionButton(); build.AddItem("Inherited build"); foreach (string name in new[] { "Slim", "Average", "Stout" }) build.AddItem(name);
        build.Select(Math.Clamp(Person.Appearance.Build + 1, 0, 3)); box.AddChild(build);
        void Feature(string name, string label, IEnumerable<string> choices)
        {
            var pick = new OptionButton(); pick.AddItem("Inherited " + label);
            if (name == "facial_hair") pick.AddItem("Clean shaven");
            foreach (string choice in choices) pick.AddItem(Describe.Title(choice));
            int chosen = Person.Appearance.Features.GetValueOrDefault(name, -1);
            pick.Select(Math.Clamp(name == "facial_hair" ? chosen == -2 ? 1 : chosen < 0 ? 0 : chosen + 2 : chosen + 1, 0, pick.ItemCount - 1));
            box.AddChild(pick); _features[name] = pick;
        }
        string[] Variants(string sheet, int segment = -1) => Art.Get("portraits." + sheet)!.Meta.GetProperty("variants").EnumerateArray()
            .Select(v => segment < 0 ? v.GetString()! : v.GetString()!.Split('.')[segment]).Distinct().ToArray();
        Feature("face_shape", "face", new[] { "oval", "round", "angular" }); Feature("eye_shape", "eyes", Variants("eyes", 0));
        Feature("iris_tone", "iris", Variants("eyes", 1)); Feature("brow_shape", "brows", Variants("brows", 0));
        Feature("nose_shape", "nose", Variants("nose")); Feature("facial_hair", "facial hair", Variants("facial_hair"));
        foreach (var (layer, slot) in new[] { ("legwear", ItemSlot.Legwear), ("footwear", ItemSlot.Footwear), ("top", ItemSlot.Top),
                     ("outerwear", ItemSlot.Outerwear), ("armour", ItemSlot.Armour), ("headwear", ItemSlot.Headwear), ("accessory", ItemSlot.Accessory) })
        {
            box.AddChild(new Label { Text = Describe.Title(layer) });
            var row = new HBoxContainer(); box.AddChild(row);
            var part = new OptionButton { CustomMinimumSize = new Vector2(230, 0) }; part.AddItem("Choose naturally");
            var preference = Person.Appearance.Garments.GetValueOrDefault(layer);
            foreach (var item in Crafts.Items.Where(i => i.Slot == slot).OrderBy(i => i.Id)) part.AddItem(item.Id);
            for (int i = 1; i < part.ItemCount; i++) if (part.GetItemText(i) == preference?.Part) part.Select(i);
            var main = DyePicker(preference?.Main ?? -1); var trim = DyePicker(preference?.Trim ?? -1);
            row.AddChild(part); row.AddChild(main); row.AddChild(trim); _pieces[layer] = (part, main, trim);
        }
        Confirmed += () =>
        {
            Person.Appearance.Skin = _skin.Selected - 1; Person.Appearance.Hair = _tone.Selected - 1;
            Person.Appearance.Build = build.Selected - 1;
            foreach (var (name, pick) in _features) Person.Appearance.Features[name] = name == "facial_hair" ? pick.Selected == 0 ? -1 : pick.Selected == 1 ? -2 : pick.Selected - 2 : pick.Selected - 1;
            Person.Appearance.HairStyle = _hair.Selected > 0 ? _hair.GetItemText(_hair.Selected) : "";
            foreach (var (layer, controls) in _pieces)
                Person.Appearance.Garments[layer] = new GarmentStyle(controls.part.Selected > 0 ? controls.part.GetItemText(controls.part.Selected) : null, controls.main.Selected - 1, controls.trim.Selected - 1);
            Applied?.Invoke();
        };
        VisibilityChanged += () => { if (!Visible) QueueFree(); };
    }

    static OptionButton TonePicker(string inherited, int count, int selected)
    {
        var pick = new OptionButton(); pick.AddItem(inherited);
        for (int i = 0; i < count; i++) pick.AddItem($"Tone {i + 1}");
        pick.Select(Math.Clamp(selected + 1, 0, count)); return pick;
    }
    static OptionButton DyePicker(int selected)
    {
        var pick = new OptionButton { CustomMinimumSize = new Vector2(170, 0) };
        pick.AddItem("Natural colour");
        foreach (var row in Art.Cloth.Rows) pick.AddItem(Describe.Title(row));
        pick.Select(Math.Clamp(selected + 1, 0, pick.ItemCount - 1)); return pick;
    }
}
