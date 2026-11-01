using Godot;

namespace Evolysia.Game;

public partial class NaturePaletteCheck : Node2D
{
    readonly List<Resource> _owned = new();
    public override async void _Ready()
    {
        try
        {
            int cases = 0, failures = 0, worst = 0;
            var lut = Own(ImageTexture.CreateFromImage(Art.Nature.Image));
            foreach (string id in new[] { "flora.broadleaf_mature", "flora.conifer_mature", "flora.shrub", "civilisation.tree_oak_lod32", "civilisation.tree_oak_lod64", "civilisation.nature_granite_outcrop_lod64" })
            {
                var sheet = Art.Need(id);
                foreach (var seasons in new[] { ("savanna.wet", "savanna.dry"), ("temperate_forest.autumn", "temperate_forest.winter") })
                    foreach (byte blend in new byte[] { 19, 52, 128, 219 })
                    {
                        int rowA = Art.Nature.Row(seasons.Item1), rowB = Art.Nature.Row(seasons.Item2);
                        int width = sheet.FrameW, height = sheet.FrameH;
                        var bytes = new byte[width * height * 4];
                        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                        {
                            byte slot = sheet.Slot(0, 0, x, y); int i = (y * width + x) * 4;
                            bytes[i + 1] = slot == Art.Empty ? (byte)0 : slot; bytes[i + 3] = slot == Art.Empty ? (byte)0 : (byte)1;
                        }
                        using var image = Image.CreateFromData(width, height, false, Image.Format.Rgba8, bytes);
                        var texture = Own(ImageTexture.CreateFromImage(image));
                        using var cell = Image.CreateFromData(1, 1, false, Image.Format.Rgba8, new byte[] { (byte)rowA, (byte)rowB, blend, 0 });
                        var cellTexture = Own(ImageTexture.CreateFromImage(cell));
                        using var empty = Image.CreateFromData(1, 1, false, Image.Format.Rgba8, new byte[4]);
                        var emptyTexture = Own(ImageTexture.CreateFromImage(empty));
                        var material = Own(new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/tiles.gdshader") });
                        material.SetShaderParameter("world", texture); material.SetShaderParameter("lut", lut);
                        material.SetShaderParameter("cell_a", cellTexture); material.SetShaderParameter("cell_c", emptyTexture);
                        material.SetShaderParameter("roots", emptyTexture); material.SetShaderParameter("tile", 512);
                        var baseline = new Sprite2D { Centered = false, Texture = texture, Material = material, TextureFilter = TextureFilterEnum.Nearest, Position = new Vector2(16, 16) };
                        AddChild(baseline);
                        var foreground = new Sprite2D { Centered = false, Texture = Sprites.Nature(sheet, 1, rowA, rowB, blend / 255f),
                            RegionEnabled = true, RegionRect = sheet.Region(0, 0), TextureFilter = TextureFilterEnum.Linear,
                            Material = Own(new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/pixel.gdshader") }), Position = new Vector2(320, 16) };
                        AddChild(foreground);
                        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                        using var capture = GetViewport().GetTexture().GetImage();
                        int changed = 0, maximum = 0;
                        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                        {
                            if (sheet.Slot(0, 0, x, y) == Art.Empty) continue;
                            var a = capture.GetPixel(x + 16, y + 16); var b = capture.GetPixel(x + 320, y + 16);
                            int delta = (int)MathF.Round(MathF.Max(MathF.Abs(a.R - b.R), MathF.Max(MathF.Abs(a.G - b.G), MathF.Abs(a.B - b.B))) * 255f);
                            maximum = Math.Max(maximum, delta); if (delta > 1) changed++;
                        }
                        cases++; worst = Math.Max(worst, maximum);
                        if (changed > 0) { failures++; GD.Print($"Mismatch: {id}, {seasons}, blend {blend}: {changed} pixels, maximum {maximum}"); }
                        baseline.Free(); foreground.Free();
                    }
            }
            GD.Print($"Nature palette check: {cases} cases, {failures} failures, maximum channel difference {worst}.");
            GetTree().Quit(failures == 0 ? 0 : 1);
        }
        catch (Exception e) { GD.PrintErr(e); GetTree().Quit(2); }
    }

    T Own<T>(T resource) where T : Resource { _owned.Add(resource); return resource; }
    public override void _ExitTree() { foreach (var resource in _owned) resource.Dispose(); }
}
