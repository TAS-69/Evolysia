using System.Text.Json;
using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Godot;

namespace Evolysia.Game;

/// <summary>
/// People in Codex's layered 16 px art (assets/people, rig.json): hair-back, body, clothing and
/// hair-front sheets stacked and recoloured through the skin, hair, dye and culture LUTs. Each
/// distinct look is composited once into a small texture holding its idle frames side by side.
/// </summary>
public static class Figures
{
    public readonly record struct Look(int Age, int Build, int Skin, int Hair, string HairStyle, string Clothing, int Dye, int Culture);

    public const int Size = 16;
    public const int PivotX = 8, PivotY = 14;
    public const int IdleFrames = 2;
    public const float IdleFps = 3f;

    static readonly string[] Ages = { "child", "adult", "elder" };
    static readonly string[] Builds = { "slim", "average", "stout" };
    static readonly string[] Styles = { "short", "cropped", "long", "braided", "topknot", "mane", "tied_back", "locks", "shaved" };
    static readonly Dictionary<Look, ImageTexture?> Cache = new();
    static Dictionary<string, (string? front, string? back)>? _hairStyles;

    /// <summary>How an agent looks: age and build from life and genes, colours from genes and culture, dress from techniques.</summary>
    public static Look LookOf(Agent a, int day, int cultureHue, bool cold)
    {
        float years = a.AgeDays(day) / (float)Evolysia.Sim.SimConfig.DaysPerYear;
        int age = years < 14f ? 0 : years >= 55f ? 2 : 1;
        float strength = a.Genome[Gene.Strength];
        int build = strength < 0.35f ? 0 : strength > 0.65f ? 2 : 1;
        int skin = Math.Clamp((int)(a.Genome[Gene.Pigment] * 8f), 0, 7);
        int hair = age == 2 ? 6 : Math.Clamp((int)(a.Genome[Gene.HairTone] * 8f), 0, 7);   // rig.json: elders grey
        // Each culture favours a style; some people wear their own.
        string style = age == 0 ? "child_tuft" : Styles[(cultureHue * 5 + (a.Sex == Sex.Female ? 2 : 0) + (a.Id % 4 == 0 ? a.Id : 0)) % Styles.Length];
        bool weaves = (a.Knows & Techs.Bit(TechId.Weaving)) != 0;
        bool hides = (a.Knows & Techs.Bit(TechId.HideClothing)) != 0;
        string clothing = weaves ? (cold ? "fur_cloak" : "woven_tunic") : hides ? (cold ? "fur_cloak" : "hide_wrap") : "none";
        int dye = weaves ? cultureHue % 12 : 12;   // lut_cloth: 12 dyes, then undyed
        return new Look(age, build, skin, hair, style, clothing, dye, cultureHue % 12);
    }

    /// <summary>The look's idle frames side by side (frame f at x = f * 16), or null without the art.</summary>
    public static ImageTexture? Texture(Look look)
    {
        if (Cache.TryGetValue(look, out var cached)) return cached;
        if (Cache.Count > 4000) Cache.Clear();
        ImageTexture? texture = null;
        if (Art.Get("people.body_idle") != null)
        {
            var colours = Colours(look);
            var rgba = new byte[Size * IdleFrames * Size * 4];
            HairStyles().TryGetValue(look.HairStyle, out var hair);
            Layer("hair_back", hair.back);
            Layer("body", "base");
            Layer("clothing", look.Clothing);
            Layer("hair_front", hair.front);
            texture = ImageTexture.CreateFromImage(Image.CreateFromData(Size * IdleFrames, Size, false, Image.Format.Rgba8, rgba));

            void Layer(string layer, string? variant)
            {
                if (variant == null || Art.Get($"people.{layer}_idle") is not { } sheet) return;
                int v = Index(sheet.Meta.GetProperty("variants"), variant);
                int profile = -1, k = 0;
                foreach (var p in sheet.Meta.GetProperty("profiles").EnumerateArray())
                {
                    if (p[0].GetString() == Ages[look.Age] && p[1].GetString() == Builds[look.Build] && p[2].GetString() == "down") { profile = k; break; }
                    k++;
                }
                if (v < 0 || profile < 0) return;
                int row = sheet.Meta.GetProperty("row_lookup")[v][profile].GetInt32();
                for (int f = 0; f < IdleFrames; f++)
                    for (int y = 0; y < Size; y++)
                        for (int x = 0; x < Size; x++)
                        {
                            byte slot = sheet.Slot(f % sheet.Frames, row, x, y);
                            if (slot == Art.Empty || colours[slot].A == 0) continue;
                            var c = colours[slot];
                            int o = (y * Size * IdleFrames + f * Size + x) * 4;
                            rgba[o] = c.R; rgba[o + 1] = c.G; rgba[o + 2] = c.B; rgba[o + 3] = 255;
                        }
            }
        }
        Cache[look] = texture;
        return texture;
    }

    /// <summary>Slot -> colour: fixed colours from the nature LUT, then this person's skin, hair, dye and culture rows.</summary>
    static Color32[] Colours(Look look)
    {
        var names = Art.SlotNames;
        int reference = Math.Max(0, Art.Nature.Row("temperate_forest.summer"));
        var table = new Color32[256];
        for (int s = 0; s < 256; s++)
        {
            string n = names[s];
            table[s] = n.StartsWith("skin_") ? Art.Skin.At(s, look.Skin)
                     : n.StartsWith("hair_") ? Art.Hair.At(s, look.Hair)
                     : n.StartsWith("cloth_") ? Art.Cloth.At(s, look.Dye)
                     : n.StartsWith("culture_") ? Art.Culture.At(s, look.Culture)
                     : Art.Nature.At(s, reference);
        }
        return table;
    }

    static int Index(JsonElement array, string value)
    {
        int i = 0;
        foreach (var e in array.EnumerateArray()) { if (e.GetString() == value) return i; i++; }
        return -1;
    }

    static Dictionary<string, (string? front, string? back)> HairStyles()
    {
        if (_hairStyles != null) return _hairStyles;
        _hairStyles = new();
        var path = Path.Combine(Art.Root, "people", "rig.json");
        if (!File.Exists(path)) return _hairStyles;
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var style in doc.RootElement.GetProperty("hair_styles").EnumerateObject())
            _hairStyles[style.Name] = (Opt(style.Value, "front"), Opt(style.Value, "back"));
        return _hairStyles;

        static string? Opt(JsonElement e, string key) => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }
}
