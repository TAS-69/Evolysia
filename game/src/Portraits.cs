using System.Text.Json;
using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Godot;

namespace Evolysia.Game;

public static class Portraits
{
    static readonly Dictionary<string, ImageTexture> Cache = new();
    public static ImageTexture? Get(Agent person, int day, int culture)
    {
        if (!person.Alive) day = person.DeathDay;
        if (Art.Json("portraits/expressions.json") is not { } recipe) return null;
        var look = People.LookOf(person, day, culture, 10f, false, person.Today);
        string age = new[] { "child", "adult", "elder" }[look.Age];
        int Pick(Gene gene, int count) => Math.Clamp(person.Appearance.Features.TryGetValue(Ids.Snake(gene), out int chosen) && chosen >= 0 ? chosen : (int)(person.Genome[gene] * count), 0, count - 1);
        string shape = new[] { "oval", "round", "angular" }[Pick(Gene.FaceShape, 3)];
        string Feature(string sheet, Gene gene, int segment = -1)
        {
            var variants = Art.Get("portraits." + sheet)!.Meta.GetProperty("variants").EnumerateArray().Select(v => v.GetString()!)
                .Select(v => segment >= 0 ? v.Split('.')[segment] : v).Distinct().ToArray();
            return variants[Pick(gene, variants.Length)];
        }
        var feeling = Enum.GetValues<Emotion>().Where(e => e != Emotion.Calm).OrderByDescending(person.Emo).First();
        if (person.Emo(feeling) < 0.15f) feeling = Emotion.Calm;
        string key = $"{person.Id}/{look.Key}/{shape}/{string.Join(',', person.Genome.Genes.Skip((int)Gene.FaceShape))}/{string.Join(',', person.Appearance.Features.OrderBy(p => p.Key).Select(p => p.Key + '=' + p.Value))}/{feeling}/{person.Scars}";
        if (Cache.TryGetValue(key, out var cached)) return cached;
        var expression = recipe.GetProperty("expressions").GetProperty(Ids.Snake(feeling));
        string hair = person.Appearance.HairStyle;
        if (hair.Length == 0) hair = look.Pieces[Array.IndexOf(People.Layers, "hair_front")].Part ?? "shaved";
        var styles = recipe.GetProperty("hair_styles");
        if (hair.Length == 0 || !styles.TryGetProperty(hair, out _)) hair = recipe.GetProperty("defaults").GetProperty("hair").GetProperty(age).GetString()!;
        var style = styles.GetProperty(hair);
        string? Text(JsonElement e) => e.ValueKind == JsonValueKind.String ? e.GetString() : null;
        var selections = new Dictionary<string, string?>
        {
            ["bg"] = "vignette", ["head"] = "base", ["ears"] = "base", ["shoulders"] = "woven_tunic",
            ["eyes"] = $"{Feature("eyes", Gene.EyeShape, 0)}.{Feature("eyes", Gene.IrisTone, 1)}.{expression.GetProperty("eyes").GetString()}",
            ["brows"] = Feature("brows", Gene.BrowShape, 0) + "." + expression.GetProperty("brows").GetString(), ["nose"] = Feature("nose", Gene.NoseShape),
            ["mouth"] = expression.GetProperty("mouth").GetString(), ["facial_hair"] = look.Age > 0 && person.Appearance.Features.GetValueOrDefault("facial_hair", -1) != -2 && (person.Appearance.Features.GetValueOrDefault("facial_hair", -1) >= 0 || person.Sex == Sex.Male && person.Genome[Gene.FacialHair] > 0.4f) ? Feature("facial_hair", Gene.FacialHair) : null,
            ["hair_back"] = Text(style.GetProperty("back")), ["hair_front"] = Text(style.GetProperty("front")),
            ["scars"] = person.Scars > 2 ? "missing_ear" : person.Scars > 1 ? "brow" : person.Scars > 0 ? "cheek" : null,
            ["paint"] = look.Pieces[Array.IndexOf(People.Layers, "body_paint")].Part, ["accessory"] = null,
        };
        var top = Evolysia.Sim.Simulation.Worn(person, Sim.Economy.ItemSlot.Top)?.Id ?? "none";
        selections["shoulders"] = top.Contains("robe") ? "dyed_robe" : top.Contains("hide") ? "hide_wrap" : top == "none" ? "none" : "woven_tunic";
        var headwear = Evolysia.Sim.Simulation.Worn(person, Sim.Economy.ItemSlot.Headwear)?.Id;
        selections["accessory"] = headwear?.Contains("helmet") == true ? "helmet" : headwear == "feather_band" ? headwear : null;
        var slots = Enumerable.Repeat(Art.Empty, 48 * 48).ToArray();
        Vector2[] cutout = Array.Empty<Vector2>();
        if (selections["scars"] == "missing_ear") cutout = recipe.GetProperty("scar_operations").GetProperty("missing_ear").GetProperty("polygon_by_profile").GetProperty(age + "." + shape)
            .EnumerateArray().Select(p => new Vector2(p[0].GetSingle(), p[1].GetSingle())).ToArray();
        foreach (var declared in recipe.GetProperty("layer_order").EnumerateArray())
        {
            string layer = declared.GetString()!;
            foreach (var part in layer == "hair" ? new[] { "hair_back", "hair_front" } : new[] { layer })
            {
                if (!selections.TryGetValue(part, out var variant) || variant == null || Art.Get("portraits." + part) is not { } sheet) continue;
                int v = 0; foreach (var value in sheet.Meta.GetProperty("variants").EnumerateArray()) { if (value.GetString() == variant) break; v++; }
                if (v >= sheet.Meta.GetProperty("variants").GetArrayLength()) continue;
                int profile = 0;
                foreach (var p in sheet.Meta.GetProperty("profiles").EnumerateArray()) { if (p[0].GetString() == age && p[1].GetString() == shape) break; profile++; }
                int cell = sheet.Meta.GetProperty("row_lookup")[v][profile].GetInt32(), columns = sheet.Int("atlas_columns");
                int ox = cell % columns * 48, oy = cell / columns * 48;
                for (int y = 0; y < 48; y++) for (int x = 0; x < 48; x++)
                {
                    byte s = sheet.SlotAt(ox + x, oy + y); if (s == Art.Empty) continue;
                    if (part == "ears" && cutout.Length > 0 && Geometry2D.IsPointInPolygon(new Vector2(x + 0.5f, y + 0.5f), cutout)) continue;
                    if (part == "paint" && (slots[y * 48 + x] == Art.Empty || !Art.SlotNames[slots[y * 48 + x]].StartsWith("skin_"))) continue;
                    slots[y * 48 + x] = s;
                }
            }
        }
        var dye = person.Appearance.Garments.GetValueOrDefault("top");
        var table = People.Table(look, dye is { Main: >= 0 } ? dye.Main : culture % 12, dye is { Trim: >= 0 } ? dye.Trim : (culture + 5) % 12);
        var rgba = new byte[48 * 48 * 4];
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == Art.Empty) continue;
            var c = table[slots[i]]; rgba[i * 4] = c.R; rgba[i * 4 + 1] = c.G; rgba[i * 4 + 2] = c.B; rgba[i * 4 + 3] = 255;
        }
        using var image = Image.CreateFromData(48, 48, false, Image.Format.Rgba8, rgba);
        var texture = ImageTexture.CreateFromImage(image);
        if (Cache.Count > 256) Cache.Clear(); Cache[key] = texture; return texture;
    }
}
