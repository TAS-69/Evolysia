using Evolysia.Sim.Core;
using Evolysia.Sim.Economy;
using Godot;

namespace Evolysia.Game;

public static class Ui
{
    public static readonly Color Glass = new(Palette.Panel, 0.94f);
    public static readonly Color Line = new(Palette.Border, 0.75f);
    public static readonly Color Muted = new(Palette.Parchment, 0.62f);

    public static readonly Color Female = new("e39bb6"), Male = new("8fbbe3"), Child = new("e7c36f"), Elder = new("b9b2a8");
    public static readonly Color FoodWork = new("9cc06c"), MakeWork = new("d9a656"), SpiritWork = new("b49be0"),
                                 SocialWork = new("e09a82"), MindWork = new("6fc4b8");
    public static readonly Color FoodGood = new("9cc06c"), MaterialGood = new("c9a774"), FuelGood = new("d88d58"),
                                 MetalGood = new("aebccb"), MadeGood = new("bba2df"), MedicineGood = new("82cfae");

    public static Color Work(Activity a) => a switch
    {
        Activity.Forage or Activity.Hunt or Activity.Fish or Activity.Farm or Activity.Herd => FoodWork,
        Activity.Craft or Activity.Build or Activity.Chop or Activity.Quarry or Activity.Mine or Activity.Gather => MakeWork,
        Activity.Ritual or Activity.Mourn => SpiritWork,
        Activity.Socialize or Activity.Court or Activity.Teach or Activity.CareChild or Activity.Play => SocialWork,
        _ => MindWork,
    };

    public static Color Good(GoodKind k) => k switch
    {
        GoodKind.Food => FoodGood, GoodKind.Fuel => FuelGood, GoodKind.Metal => MetalGood,
        GoodKind.Manufactured => MadeGood, GoodKind.Medicine => MedicineGood, _ => MaterialGood,
    };

    public static Color Structure(StructureRole r) => r switch
    {
        StructureRole.Dwelling => new("d7b07a"), StructureRole.Field or StructureRole.Pen => FoodWork,
        StructureRole.Storage => new("c9b26a"), StructureRole.Station or StructureRole.Hearth or StructureRole.Power => FuelGood,
        StructureRole.Shrine => SpiritWork, StructureRole.Defence => new("d17a62"), StructureRole.Vessel => new("7fb2d6"),
        _ => MindWork,
    };

    public static string C(string text, Color c) => $"[color=#{c.ToHtml(false)}]{text}[/color]";

    public static string Tag(string text, Color c) =>
        $"[bgcolor=#{new Color(c, 0.18f).ToHtml(true)}][color=#{c.ToHtml(false)}] {text} [/color][/bgcolor]";

    public static Label Heading(string text)
    {
        var l = new Label { Text = Spaced(text.ToUpperInvariant()) };
        l.AddThemeColorOverride("font_color", Palette.Ochre);
        l.AddThemeFontSizeOverride("font_size", 11);
        return l;
    }

    public static string Spaced(string s) => string.Join(" ", s.ToCharArray());

    static StyleBoxFlat Box(Color bg, Color border, int radius, int margin, int borderWidth = 1, bool shadow = false)
    {
        var sb = new StyleBoxFlat { BgColor = bg, BorderColor = border, AntiAliasing = true };
        sb.SetBorderWidthAll(borderWidth);
        sb.SetCornerRadiusAll(radius);
        sb.SetContentMarginAll(margin);
        if (shadow) { sb.ShadowColor = new Color(0f, 0f, 0f, 0.35f); sb.ShadowSize = 8; sb.ShadowOffset = new Vector2(0, 2); }
        return sb;
    }

    public static StyleBoxFlat Card(Color? accent = null)
    {
        var sb = Box(new Color(Palette.Raised, 0.55f), new Color(accent ?? Palette.Border, accent == null ? 0.5f : 0.9f), 6, 10);
        if (accent != null) { sb.SetBorderWidthAll(0); sb.BorderWidthLeft = 3; }
        return sb;
    }

    public static Theme Build()
    {
        var theme = new Theme();
        theme.SetStylebox("panel", "PanelContainer", Box(Glass, Line, 10, 12, shadow: true));
        theme.SetStylebox("panel", "Panel", Box(Glass, Line, 10, 12, shadow: true));
        var flat = new Color(Palette.Raised, 0.85f);
        theme.SetStylebox("normal", "Button", Box(flat, new Color(Palette.Border, 0.5f), 6, 6));
        theme.SetStylebox("hover", "Button", Box(flat.Lightened(0.1f), new Color(Palette.Ochre, 0.7f), 6, 6));
        theme.SetStylebox("pressed", "Button", Box(new Color(Palette.Ochre, 0.32f), Palette.Ochre, 6, 6));
        theme.SetStylebox("hover_pressed", "Button", Box(new Color(Palette.Ochre, 0.42f), Palette.Ochre, 6, 6));
        theme.SetStylebox("disabled", "Button", Box(new Color(Palette.Raised, 0.4f), new Color(Palette.Border, 0.3f), 6, 6));
        theme.SetStylebox("focus", "Button", new StyleBoxEmpty());
        theme.SetStylebox("normal", "LineEdit", Box(new Color(Palette.Bg, 0.9f), new Color(Palette.Border, 0.6f), 6, 7));
        theme.SetStylebox("focus", "LineEdit", Box(new Color(Palette.Bg, 0.9f), Palette.Ochre, 6, 7));
        theme.SetStylebox("normal", "TextEdit", Box(new Color(Palette.Bg, 0.9f), new Color(Palette.Border, 0.6f), 6, 8));
        theme.SetStylebox("focus", "TextEdit", Box(new Color(Palette.Bg, 0.9f), Palette.Ochre, 6, 8));
        theme.SetStylebox("normal", "OptionButton", Box(flat, new Color(Palette.Border, 0.5f), 6, 6));
        theme.SetStylebox("hover", "OptionButton", Box(flat.Lightened(0.1f), new Color(Palette.Ochre, 0.7f), 6, 6));
        theme.SetStylebox("pressed", "OptionButton", Box(flat, Palette.Ochre, 6, 6));
        theme.SetStylebox("focus", "OptionButton", new StyleBoxEmpty());
        theme.SetStylebox("panel", "PopupMenu", Box(Palette.Panel, Line, 8, 6, shadow: true));
        theme.SetStylebox("hover", "PopupMenu", Box(new Color(Palette.Ochre, 0.3f), new Color(0, 0, 0, 0), 4, 4));
        theme.SetStylebox("panel", "TooltipPanel", Box(new Color(Palette.Bg, 0.96f), Line, 6, 8, shadow: true));
        var slider = Box(new Color(Palette.Bg, 0.9f), new Color(Palette.Border, 0.6f), 4, 2);
        theme.SetStylebox("slider", "HSlider", slider);
        theme.SetStylebox("grabber_area", "HSlider", Box(new Color(Palette.Ochre, 0.6f), new Color(0, 0, 0, 0), 4, 2));
        theme.SetStylebox("grabber_area_highlight", "HSlider", Box(new Color(Palette.Ochre, 0.8f), new Color(0, 0, 0, 0), 4, 2));
        foreach (var type in new[] { "Label", "Button", "LineEdit", "TextEdit", "OptionButton", "PopupMenu", "TooltipLabel", "CheckBox" })
        {
            theme.SetColor("font_color", type, Palette.Bone);
            theme.SetFontSize("font_size", type, 13);
        }
        theme.SetColor("font_hover_color", "Button", Palette.Bone);
        theme.SetColor("font_pressed_color", "Button", Palette.Divine);
        theme.SetColor("font_disabled_color", "Button", new Color(Palette.Parchment, 0.4f));
        theme.SetColor("font_placeholder_color", "LineEdit", new Color(Palette.Parchment, 0.45f));
        theme.SetColor("default_color", "RichTextLabel", Palette.Bone);
        foreach (var size in new[] { "normal_font_size", "bold_font_size", "italics_font_size" }) theme.SetFontSize(size, "RichTextLabel", 13);
        theme.SetConstant("line_separation", "RichTextLabel", 3);
        return theme;
    }
}
