using Godot;

namespace Evolysia.Game;

/// <summary>
/// One piece of art standing in the world: drawn with its pivot (texture pixels) on its foot
/// (world units), each texture pixel <see cref="PixelSize"/> world units wide, optionally mirrored.
/// Sprites are y-sorted before drawing so nearer things overlap farther ones.
/// </summary>
public readonly record struct WorldSprite(Vector2 Foot, Texture2D Texture, Rect2 Source, Vector2 Pivot, float PixelSize, Color Modulate, float Shadow = 0f, bool Flip = false);

/// <summary>Textures for sheets: reference colouring or LUT recolours, enlarged by whole factors when finer art is missing.</summary>
public static class Sprites
{
    static readonly Dictionary<(string, int, int, int), ImageTexture> Cache = new();

    /// <summary>World units per art pixel at a density.</summary>
    public static float PixelSize(int density) => Main.CellPx / (float)density;

    /// <summary>
    /// A sheet's texture, enlarged <paramref name="scale"/> times, coloured by its reference PNG or,
    /// with a culture / hair row, with culture_* or hair_* slots taken from that LUT row.
    /// </summary>
    public static ImageTexture Texture(Sheet sheet, int scale = 1, int cultureRow = -1, int hairRow = -1)
    {
        var key = (sheet.Id, scale, cultureRow, hairRow);
        if (Cache.TryGetValue(key, out var t)) return t;
        Image image;
        if (cultureRow < 0 && hairRow < 0) image = (Image)sheet.Reference.Duplicate();
        else
        {
            var names = Art.SlotNames;
            var rgba = sheet.Reference.GetData();
            for (int i = 0; i < sheet.Slots.Length; i++)
            {
                byte s = sheet.Slots[i];
                if (s == Art.Empty) continue;
                Color32? c = cultureRow >= 0 && names[s].StartsWith("culture_") ? Art.Culture.At(s, cultureRow)
                           : hairRow >= 0 && names[s].StartsWith("hair_") ? Art.Hair.At(s, hairRow) : null;
                if (c is { } col) { rgba[i * 4] = col.R; rgba[i * 4 + 1] = col.G; rgba[i * 4 + 2] = col.B; }
            }
            image = Image.CreateFromData(sheet.Width, sheet.Height, false, Image.Format.Rgba8, rgba);
        }
        if (scale > 1) image.Resize(sheet.Width * scale, sheet.Height * scale, Image.Interpolation.Nearest);
        return Cache[key] = ImageTexture.CreateFromImage(image);
    }

    /// <summary>
    /// A sprite for a multi-density asset (civilisation id) or, failing that, a 16 px base sheet
    /// enlarged to the density. Null when neither exists.
    /// </summary>
    public static WorldSprite? Make(string? lodId, string? baseId, int density, Vector2 foot, Color modulate, int frame = 0, int row = 0,
                                    float shadow = -1f, bool flip = false, int cultureRow = -1, int hairRow = -1)
    {
        if (Art.Resolve(lodId, baseId, density) is not { } resolved) return null;
        var (sheet, scale) = resolved;
        var texture = Texture(sheet, scale, cultureRow, hairRow);
        var source = new Rect2(frame % sheet.Frames * sheet.FrameW * scale, row % sheet.Rows * sheet.FrameH * scale, sheet.FrameW * scale, sheet.FrameH * scale);
        float px = PixelSize(density);
        if (shadow < 0f) shadow = sheet.FrameW * scale * px * 0.42f;
        return new WorldSprite(foot, texture, source, new Vector2(sheet.PivotX, sheet.PivotY + 1) * scale, px, modulate, shadow, flip);
    }
}
