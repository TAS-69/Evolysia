using System.Text.Json;
using Evolysia.Sim;
using Evolysia.Sim.Agents;
using Evolysia.Sim.Economy;
using Evolysia.Sim.Core;
using Godot;

namespace Evolysia.Game;

public static class People
{
    public static readonly string[] Layers =
        { "hair_back", "body", "legwear", "footwear", "top", "outerwear", "armour", "body_paint", "hair_front", "headwear", "accessory", "held_item" };

    public readonly record struct Piece(string? Part, int Main = 12, int Trim = 12);

    public sealed class Look
    {
        public int Age, Build, Skin, Hair, Culture;
        public readonly Piece[] Pieces = new Piece[Layers.Length];
        string? _key;
        public string Key => _key ??= $"{Age}{Build}{Skin}{Hair}{Culture}|" + string.Join(",", Pieces.Select(p => $"{p.Part}:{p.Main}:{p.Trim}"));
        public void Set(string layer, string? part, int main = 12, int trim = 12) { Pieces[Array.IndexOf(Layers, layer)] = new Piece(part, main, trim); _key = null; }
    }

    public sealed record Strip(ImageTexture Texture, int Frames, int FrameW, int FrameH, float Fps, Vector2 Pivot);

    static readonly string[] Ages = { "child", "adult", "elder" };
    static readonly string[] Builds = { "slim", "average", "stout" };
    static readonly string[] StylesA = { "short", "cropped", "topknot", "mane", "locks", "curly", "side_swept", "tied_back" };
    static readonly string[] StylesB = { "long", "braided", "twin_braids", "braid_crown", "high_tail", "bob", "coils", "locks" };
    static readonly Dictionary<string, Strip?> Cache = new();
    static Dictionary<string, (string? front, string? back)>? _hairStyles;

    public static string Animation(Activity activity, bool moving, bool child, bool sick)
    {
        if (sick) return "sleep";
        if (moving) return activity is Activity.Hunt ? "run" : "walk";
        if (child) return activity == Activity.Play ? "play" : "idle";
        return activity switch
        {
            Activity.Forage => "gather",
            Activity.Hunt => "attack",
            Activity.Farm => "dig",
            Activity.Craft or Activity.Experiment => "craft",
            Activity.Build => "build",
            Activity.Socialize or Activity.Court or Activity.Teach => "talk",
            Activity.Ritual => "pray",
            Activity.Mourn => "mourn",
            Activity.Chop => "chop",
            Activity.Quarry or Activity.Mine => "dig",
            Activity.Gather => "gather",
            Activity.Play => "play",
            _ => "idle",
        };
    }

    public static Look LookOf(Agent a, int day, int cultureHue, float temperature, bool leader, Activity activity)
    {
        var look = new Look();
        float years = a.AgeDays(day) / (float)Evolysia.Sim.SimConfig.DaysPerYear;
        look.Age = years < 14f ? 0 : years >= 55f ? 2 : 1;
        float strength = a.Genome[Gene.Strength];
        look.Build = strength < 0.35f ? 0 : strength > 0.65f ? 2 : 1;
        look.Skin = Math.Clamp((int)(a.Genome[Gene.Pigment] * 8f), 0, 7);
        look.Hair = look.Age == 2 ? 6 : Math.Clamp((int)(a.Genome[Gene.HairTone] * 8f), 0, 7);
        int hue = look.Culture = cultureHue % 12;
        bool child = look.Age == 0;

        var styles = (a.Sex == Sex.Female) ^ (hue % 3 == 0) ? StylesB : StylesA;
        string style = child ? "child_tuft" : styles[(hue * 3 + (a.Id % 5 == 0 ? a.Id : 0)) % styles.Length];
        if (look.Age == 2 && a.Sex == Sex.Male && a.Id % 3 == 0) style = "cropped";
        HairStyles().TryGetValue(style, out var hair);
        look.Set("hair_back", hair.back);
        look.Set("hair_front", hair.front);
        look.Set("body", "base");

        int dye = hue, trim = (hue + 5) % 12;
        foreach (var (layer, slot) in new[] { ("legwear", ItemSlot.Legwear), ("footwear", ItemSlot.Footwear), ("top", ItemSlot.Top),
                                              ("outerwear", ItemSlot.Outerwear), ("armour", ItemSlot.Armour), ("headwear", ItemSlot.Headwear), ("accessory", ItemSlot.Accessory) })
            if (Simulation.Worn(a, slot) is { } item)
            {
                bool dyed = item.Inputs.Any(i => Goods.All[i.good].Id is "cloth" or "fiber");
                look.Set(layer, item.Id, dyed ? dye : 12, dyed ? trim : 12);
            }

        if (!child && Techs.Has(a.Knows, TechId.Pigments))
            look.Set("body_paint", new[] { "cheek_dots", "brow_band", "arm_bands", "face_streak", "shoulder_dabs", "leg_marks" }[hue % 6], 12, 12);
        look.Set("held_item", child ? null : Simulation.HeldTool(a, activity)?.Id, 12, 12);
        return look;
    }

    public static Strip? Get(Look look, string anim, string dir, int density)
    {
        string key = $"{look.Key}|{anim}|{dir}|{density}";
        if (Cache.TryGetValue(key, out var cached)) return cached;
        if (Cache.Count > 6000) Cache.Clear();
        Strip? strip = null;
        var body = Art.Lod($"wardrobe.body_{anim}", density);
        if (body != null)
        {
            int frames = body.Int("frames", 1), fw = body.FrameW, fh = body.FrameH;
            int profile = Profile(body, look, ref dir);
            var rgba = new byte[fw * frames * fh * 4];
            var slots = new byte[fw * frames * fh];
            Array.Fill(slots, Art.Empty);
            var skinTable = Table(look, 12, 12);
            for (int l = 0; l < Layers.Length; l++)
            {
                var piece = look.Pieces[l];
                if (piece.Part == null || Art.Lod($"wardrobe.{Layers[l]}_{anim}", density) is not { } sheet) continue;
                int v = Index(sheet.Meta.GetProperty("variants"), piece.Part);
                int p = Profile(sheet, look, ref dir);
                if (v < 0 || p < 0) continue;
                int row = sheet.Meta.GetProperty("row_lookup")[v][p].GetInt32();
                int columns = sheet.Int("strip_columns", 1), sheetFrames = sheet.Int("frames", 1);
                var table = Layers[l] is "body" ? skinTable : Table(look, piece.Main, piece.Trim);
                bool paint = Layers[l] == "body_paint";
                for (int f = 0; f < frames; f++)
                {
                    int sx = (row % columns) * sheet.FrameW * sheetFrames + (f % sheetFrames) * sheet.FrameW, sy = row / columns * sheet.FrameH;
                    for (int y = 0; y < fh; y++)
                        for (int x = 0; x < fw; x++)
                        {
                            byte slot = sheet.SlotAt(sx + x, sy + y);
                            if (slot == Art.Empty) continue;
                            int o = y * fw * frames + f * fw + x;
                            if (paint && !Art.SlotNames[slots[o] == Art.Empty ? 0 : slots[o]].StartsWith("skin_")) continue;
                            var c = table[slot];
                            slots[o] = slot;
                            rgba[o * 4] = c.R; rgba[o * 4 + 1] = c.G; rgba[o * 4 + 2] = c.B; rgba[o * 4 + 3] = 255;
                        }
                }
            }
            var pivot = body.Meta.TryGetProperty("row_pivots", out var rp) && profile >= 0 && profile < rp.GetArrayLength()
                ? new Vector2(rp[profile][0].GetInt32(), rp[profile][1].GetInt32()) : new Vector2(body.PivotX, body.PivotY);
            strip = new Strip(ImageTexture.CreateFromImage(Image.CreateFromData(fw * frames, fh, false, Image.Format.Rgba8, rgba)),
                              frames, fw, fh, body.Meta.TryGetProperty("fps", out var fps) ? fps.GetSingle() : 4f, pivot);
        }
        Cache[key] = strip;
        return strip;
    }

    static int Profile(Sheet sheet, Look look, ref string dir)
    {
        for (int pass = 0; pass < 2; pass++)
        {
            int k = 0;
            foreach (var p in sheet.Meta.GetProperty("profiles").EnumerateArray())
            {
                if (p[0].GetString() == Ages[look.Age] && p[1].GetString() == Builds[look.Build] && p[2].GetString() == dir) return k;
                k++;
            }
            dir = "side";
        }
        return -1;
    }

    static Color32[] Table(Look look, int main, int trim)
    {
        var names = Art.SlotNames;
        int reference = Art.ReferenceRow;
        var table = new Color32[256];
        for (int s = 0; s < 256; s++)
        {
            string n = names[s];
            table[s] = n.StartsWith("skin_") ? Art.Skin.At(s, look.Skin)
                     : n.StartsWith("hair_") ? Art.Hair.At(s, look.Hair)
                     : n.StartsWith("cloth_a") ? Art.Cloth.At(s, main)
                     : n.StartsWith("cloth_b") ? Art.Cloth.At(s, trim)
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
        if (Art.Json("wardrobe/customisation.json") is not { } doc) return _hairStyles;
        foreach (var style in doc.GetProperty("hair_styles").EnumerateObject())
            _hairStyles[style.Name] = (Opt(style.Value, "front"), Opt(style.Value, "back"));
        return _hairStyles;

        static string? Opt(JsonElement e, string key) => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }
}
