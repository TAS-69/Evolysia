namespace Evolysia.Sim.Core;

/// <summary>
/// Seeded 3D value noise. The world samples it on a cylinder (x wraps around the planet),
/// so terrain has no seam at the date line.
/// </summary>
public sealed class Noise
{
    readonly int[] _perm = new int[512];
    readonly float[] _values = new float[256];

    public Noise(Rng rng)
    {
        var p = new int[256];
        for (int i = 0; i < 256; i++) { p[i] = i; _values[i] = rng.NextFloat() * 2f - 1f; }
        for (int i = 255; i > 0; i--) { int j = rng.Next(i + 1); (p[i], p[j]) = (p[j], p[i]); }
        for (int i = 0; i < 512; i++) _perm[i] = p[i & 255];
    }

    float Lattice(int x, int y, int z) => _values[_perm[_perm[_perm[x & 255] + (y & 255)] + (z & 255)]];

    static float Smooth(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

    public float Sample(float x, float y, float z)
    {
        int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y), zi = (int)MathF.Floor(z);
        float tx = Smooth(x - xi), ty = Smooth(y - yi), tz = Smooth(z - zi);
        float c000 = Lattice(xi, yi, zi), c100 = Lattice(xi + 1, yi, zi);
        float c010 = Lattice(xi, yi + 1, zi), c110 = Lattice(xi + 1, yi + 1, zi);
        float c001 = Lattice(xi, yi, zi + 1), c101 = Lattice(xi + 1, yi, zi + 1);
        float c011 = Lattice(xi, yi + 1, zi + 1), c111 = Lattice(xi + 1, yi + 1, zi + 1);
        float x00 = Lerp(c000, c100, tx), x10 = Lerp(c010, c110, tx);
        float x01 = Lerp(c001, c101, tx), x11 = Lerp(c011, c111, tx);
        return Lerp(Lerp(x00, x10, ty), Lerp(x01, x11, ty), tz);
    }

    /// <summary>Fractal sum, roughly in [-1, 1].</summary>
    public float Fbm(float x, float y, float z, int octaves, float lacunarity = 2f, float gain = 0.5f)
    {
        float sum = 0f, amp = 1f, norm = 0f;
        for (int o = 0; o < octaves; o++)
        {
            sum += amp * Sample(x, y, z);
            norm += amp;
            amp *= gain;
            x *= lacunarity; y *= lacunarity; z *= lacunarity;
        }
        return sum / norm;
    }

    /// <summary>Fbm on a cylinder: u in [0,1) wraps, v is the vertical coordinate.</summary>
    public float Cylinder(float u, float v, float scale, int octaves, float offset = 0f)
    {
        float a = u * 2f * MathF.PI;
        float r = scale / (2f * MathF.PI);
        return Fbm(MathF.Cos(a) * r + offset, MathF.Sin(a) * r + offset * 0.7f, v * scale + offset * 1.3f, octaves);
    }

    static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
