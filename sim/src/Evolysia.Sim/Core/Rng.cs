namespace Evolysia.Sim.Core;

/// <summary>
/// PCG32. Small, fast and bit-identical across platforms and .NET versions, which System.Random
/// does not promise. Every random decision in the simulation goes through one of these, so a world
/// seed reproduces a run exactly.
/// </summary>
public sealed class Rng
{
    ulong _state;
    readonly ulong _inc;

    public Rng(ulong seed, ulong stream = 0xDA3E39CB94B95BDBUL)
    {
        _inc = (stream << 1) | 1UL;
        NextUInt();
        _state += seed;
        NextUInt();
    }

    /// <summary>An independent stream derived from a seed and a key (e.g. "worldgen").</summary>
    public static Rng Derive(ulong seed, ulong key) => new(Mix(seed ^ Mix(key)), Mix(key + 1));

    public static ulong Mix(ulong z)
    {
        z += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    public uint NextUInt()
    {
        ulong old = _state;
        _state = unchecked(old * 6364136223846793005UL + _inc);
        uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
        int rot = (int)(old >> 59);
        return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
    }

    /// <summary>Uniform in [0, 1).</summary>
    public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

    /// <summary>Uniform integer in [0, max).</summary>
    public int Next(int max) => max <= 0 ? 0 : (int)(((ulong)NextUInt() * (ulong)max) >> 32);

    public int Range(int min, int maxExclusive) => min + Next(maxExclusive - min);
    public float Range(float min, float max) => min + (max - min) * NextFloat();
    public bool Chance(float p) => NextFloat() < p;

    public float Normal(float mean = 0f, float sd = 1f)
    {
        float u1 = MathF.Max(NextFloat(), 1e-7f), u2 = NextFloat();
        return mean + sd * MathF.Sqrt(-2f * MathF.Log(u1)) * MathF.Cos(2f * MathF.PI * u2);
    }

    /// <summary>Beta(2,2): the median of three uniforms. Used to blend parents' genes.</summary>
    public float Beta22()
    {
        float a = NextFloat(), b = NextFloat(), c = NextFloat();
        return MathF.Max(MathF.Min(a, b), MathF.Min(MathF.Max(a, b), c));
    }

    /// <summary>Beta(k,k) for k &lt; 1 via inverse-power trick; pushes values towards 0 and 1.</summary>
    public float Spread(float k)
    {
        float u = NextFloat();
        float a = MathF.Pow(u, 1f / k), b = MathF.Pow(NextFloat(), 1f / k);
        return a / MathF.Max(a + b, 1e-6f);
    }

    public T Pick<T>(IReadOnlyList<T> list) => list[Next(list.Count)];

    /// <summary>Index chosen with probability proportional to weights (all &gt;= 0).</summary>
    public int Weighted(ReadOnlySpan<float> weights)
    {
        float total = 0f;
        foreach (var w in weights) total += MathF.Max(w, 0f);
        if (total <= 0f) return Next(weights.Length);
        float r = NextFloat() * total;
        for (int i = 0; i < weights.Length; i++)
        {
            r -= MathF.Max(weights[i], 0f);
            if (r < 0f) return i;
        }
        return weights.Length - 1;
    }

    /// <summary>Softmax sample over scores at the given temperature.</summary>
    public int Softmax(ReadOnlySpan<float> scores, float temperature)
    {
        Span<float> w = stackalloc float[scores.Length];
        float max = float.MinValue;
        foreach (var s in scores) max = MathF.Max(max, s);
        float t = MathF.Max(temperature, 1e-3f);
        for (int i = 0; i < scores.Length; i++)
            w[i] = float.IsNegativeInfinity(scores[i]) ? 0f : MathF.Exp((scores[i] - max) / t);
        return Weighted(w);
    }
}
