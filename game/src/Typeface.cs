using Godot;

namespace Evolysia.Game;

public static class Typeface
{
    static readonly Dictionary<string, Font> Cache = new();
    static readonly List<FontFile> Owned = new();
    public static Font Interface => Load("PixelifySans.ttf", true);
    public static Font Reading => Load("AlegreyaSans-Regular.ttf");
    public static Font ReadingBold => Load("AlegreyaSans-Bold.ttf");
    public static Font ReadingItalic => Load("AlegreyaSans-Italic.ttf");

    static Font Load(string name, bool pixel = false)
    {
        if (Cache.TryGetValue(name, out var cached)) return cached;
        string path = System.IO.Path.Combine(Art.Root, "fonts", name);
        if (!System.IO.File.Exists(path)) return Cache[name] = ThemeDB.FallbackFont;
        var font = new FontFile();
        if (font.LoadDynamicFont(path) != Error.Ok) { font.Dispose(); return Cache[name] = ThemeDB.FallbackFont; }
        font.Antialiasing = pixel ? TextServer.FontAntialiasing.None : TextServer.FontAntialiasing.Gray;
        font.Fallbacks = new Godot.Collections.Array<Font> { ThemeDB.FallbackFont };
        Owned.Add(font);
        return Cache[name] = font;
    }

    public static void Release()
    {
        foreach (var font in Owned) { font.Fallbacks = new Godot.Collections.Array<Font>(); font.Dispose(); }
        Owned.Clear(); Cache.Clear();
    }
}
