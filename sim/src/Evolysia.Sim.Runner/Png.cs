using System.IO.Compression;

namespace Evolysia.Sim.Runner;

/// <summary>Minimal RGB PNG encoder (no dependencies), for world map snapshots.</summary>
static class Png
{
    public static void Write(string path, int width, int height, byte[] rgb, int scale = 1)
    {
        int w = width * scale, h = height * scale;
        using var raw = new MemoryStream();
        for (int y = 0; y < h; y++)
        {
            raw.WriteByte(0);   // filter: none
            int sy = y / scale;
            for (int x = 0; x < w; x++)
            {
                int i = (sy * width + x / scale) * 3;
                raw.Write(rgb, i, 3);
            }
        }
        using var file = File.Create(path);
        file.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var ihdr = new byte[13];
        BE(ihdr, 0, w); BE(ihdr, 4, h);
        ihdr[8] = 8; ihdr[9] = 2;   // 8-bit RGB
        Chunk(file, "IHDR", ihdr);
        using (var z = new MemoryStream())
        {
            using (var zs = new ZLibStream(z, CompressionLevel.Optimal, leaveOpen: true)) raw.WriteTo(zs);
            Chunk(file, "IDAT", z.ToArray());
        }
        Chunk(file, "IEND", Array.Empty<byte>());
    }

    static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4]; BE(len, 0, data.Length);
        s.Write(len);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(data);
        uint crc = Crc(typeBytes, 0xFFFFFFFFu);
        crc = Crc(data, crc) ^ 0xFFFFFFFFu;
        var c = new byte[4]; BE(c, 0, (int)crc);
        s.Write(c);
    }

    static readonly uint[] Table = Enumerable.Range(0, 256).Select(n =>
    {
        uint c = (uint)n;
        for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    static uint Crc(byte[] data, uint crc)
    {
        foreach (byte b in data) crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }

    static void BE(byte[] b, int o, int v)
    {
        b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v;
    }
}
