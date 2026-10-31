using System.Text.Json;
using Godot;

namespace Evolysia.Game;

public sealed class Sheet
{
    public required string Id;
    public required JsonElement Meta;
    public int FrameW, FrameH, Width, Height, PivotX, PivotY;

    public int Density = 16;
    public byte[] Slots = Array.Empty<byte>();
    public Image Reference = null!;
    ImageTexture? _texture;

    public int Frames => Width / FrameW;
    public int Rows => Height / FrameH;

    public ImageTexture Texture => _texture ??= ImageTexture.CreateFromImage(Reference);
    public byte Slot(int frame, int row, int x, int y) => Slots[(row * FrameH + y) * Width + frame * FrameW + x];
    public Rect2 Region(int frame, int row) => new(frame * FrameW, row * FrameH, FrameW, FrameH);
    public byte SlotAt(int x, int y) => Slots[y * Width + x];
    public int Int(string key, int fallback = 0) => Meta.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : fallback;

    public string? Str(string key) => Meta.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}

public sealed class Lut
{
    public required string[] Rows;
    public required Image Image;
    public required byte[] Rgba;

    public int Row(string name) => Array.IndexOf(Rows, name);
    public Color32 At(int slot, int row) { int o = (row * 256 + slot) * 4; return new Color32(Rgba[o], Rgba[o + 1], Rgba[o + 2], Rgba[o + 3]); }
}

public readonly record struct Color32(byte R, byte G, byte B, byte A);

public static class Art
{
    public const byte Empty = 255;
    public static readonly int[] Densities = { 16, 32, 64 };
    public static readonly string Root = FindRoot();
    public static readonly bool Available = File.Exists(Path.Combine(Root, "manifest.json"));

    static string FindRoot()
    {
        string besideGame = Path.Combine(Path.GetDirectoryName(OS.GetExecutablePath())!, "assets");
        string besideProject = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "assets"));
        return File.Exists(Path.Combine(besideGame, "manifest.json")) ? besideGame : besideProject;
    }

    static readonly Dictionary<string, Sheet?> Sheets = new();
    static string[]? _slotNames;
    static Lut? _nature, _skin, _hair, _cloth, _culture;

    public static string[] SlotNames => _slotNames ??= LoadSlotNames();
    public static Lut Nature => _nature ??= LoadLut("lut_nature");

    public static int ReferenceRow => Math.Max(0, Nature.Row("temperate_forest.summer"));
    public static Lut Skin => _skin ??= LoadLut("lut_skin");
    public static Lut Hair => _hair ??= LoadLut("lut_hair");
    public static Lut Cloth => _cloth ??= LoadLut("lut_cloth");
    public static Lut Culture => _culture ??= LoadLut("lut_culture");

    public static Sheet? Get(string id)
    {
        lock (Sheets) return GetLocked(id);
    }

    public static Sheet? Lod(string id, int density) => Get($"{id}_lod{density}");

    public static int DensityFor(float zoom)
    {
        float tile = Main.CellPx * zoom;
        return tile <= 24f ? 16 : tile <= 48f ? 32 : 64;
    }

    public static (Sheet sheet, int scale)? Resolve(string? lodId, string? baseId, int density)
    {
        if (lodId != null)
        {
            if (Lod(lodId, density) is { } fine) return (fine, 1);
            if (baseId == null && Lod(lodId, 16) is { } coarse) return (coarse, density / 16);
        }
        if (baseId != null && Get(baseId) is { } b) return (b, Math.Max(1, density / b.Density));
        return null;
    }

    static Sheet? GetLocked(string id)
    {
        if (Sheets.TryGetValue(id, out var cached)) return cached;
        Sheet? sheet = null;
        int dot = id.IndexOf('.');
        string json = Path.Combine(Root, id[..dot], id[(dot + 1)..] + ".json");
        if (Available && File.Exists(json))
        {
            var meta = JsonDocument.Parse(File.ReadAllText(json)).RootElement.Clone();
            var files = meta.GetProperty("files").EnumerateArray().Select(f => f.GetString()!).ToArray();
            var reference = LoadImage(files[0]);
            var idx = files.Length > 1 ? LoadImage(files[1]) : null;
            var frame = meta.GetProperty("frame");
            var pivot = meta.TryGetProperty("pivot", out var p) ? p : default;
            sheet = new Sheet
            {
                Id = id, Meta = meta, Reference = reference,
                FrameW = frame[0].GetInt32(), FrameH = frame[1].GetInt32(),
                Width = reference.GetWidth(), Height = reference.GetHeight(),
                PivotX = pivot.ValueKind == JsonValueKind.Array ? pivot[0].GetInt32() : 0,
                PivotY = pivot.ValueKind == JsonValueKind.Array ? pivot[1].GetInt32() : 0,
                Slots = idx != null ? SlotsOf(idx) : new byte[reference.GetWidth() * reference.GetHeight()],
                Density = meta.TryGetProperty("density", out var d) && d.ValueKind == JsonValueKind.Number ? d.GetInt32() : 16,
            };
        }
        Sheets[id] = sheet;
        return sheet;
    }

    public static Sheet Need(string id) => Get(id) ?? throw new FileNotFoundException($"asset {id} not found under {Root}");

    public static JsonElement? Json(string relative)
    {
        var path = Path.Combine(Root, relative);
        if (!File.Exists(path)) return null;
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();
    }

    static Image LoadImage(string relative)
    {
        var image = Image.LoadFromFile(Path.Combine(Root, relative));
        if (image.GetFormat() != Image.Format.Rgba8) image.Convert(Image.Format.Rgba8);
        return image;
    }

    static byte[] SlotsOf(Image idx)
    {
        var rgba = idx.GetData();
        var slots = new byte[rgba.Length / 4];
        for (int i = 0; i < slots.Length; i++) slots[i] = rgba[i * 4 + 3] == 0 ? Empty : rgba[i * 4];
        return slots;
    }

    static string[] LoadSlotNames()
    {
        var names = Enumerable.Repeat("", 256).ToArray();
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "palette", "palette.json")));
        foreach (var slot in doc.RootElement.GetProperty("slots").EnumerateArray())
            names[slot.GetProperty("index").GetInt32()] = slot.GetProperty("name").GetString()!;
        return names;
    }

    static Lut LoadLut(string name)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "palette", name + ".json")));
        var rows = doc.RootElement.TryGetProperty("rows", out var r) ? r : doc.RootElement.GetProperty("directions");
        var image = LoadImage(Path.Combine("palette", name + ".png"));
        return new Lut { Rows = rows.EnumerateArray().Select(x => x.GetString()!).ToArray(), Image = image, Rgba = image.GetData() };
    }
}
