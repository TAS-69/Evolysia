using Evolysia.Sim.Core;

namespace Evolysia.Sim.Agents;

public enum Gene : byte
{
    // Temperament (inherited part of personality)
    NoveltySeeking, HarmAvoidance, RewardDependence, Persistence,
    // Starting tilt for character traits; experience and upbringing take over from here
    SelfDirectedness, Cooperativeness, SelfTranscendence,
    // Aptitudes
    Strength, Dexterity, Intellect, Perception, Constitution, Fertility, Longevity, Charisma,
    // Appearance (drives portrait/sprite recolours)
    Pigment, HairTone,
}

/// <summary>
/// Heritable values 0..1. A child's genome blends both parents gene by gene, then 1-3 genes
/// diverge noticeably (or a quirk appears), so siblings differ and lineages drift.
/// </summary>
public sealed class Genome
{
    public const int Count = 17;
    public readonly float[] Genes = new float[Count];
    public Quirk Quirks;

    public float this[Gene g] => Genes[(int)g];
    public bool Has(Quirk q) => (Quirks & q) != 0;

    public static readonly Quirk[] AllQuirks = Enum.GetValues<Quirk>().Where(q => q != Quirk.None).ToArray();

    public static Genome Random(Rng rng)
    {
        var g = new Genome();
        for (int i = 0; i < Count; i++)
            g.Genes[i] = i < 7 ? rng.Spread(0.8f) : Math.Clamp(rng.Normal(0.5f, 0.17f), 0f, 1f);
        if (rng.Chance(0.35f)) g.Quirks |= rng.Pick(AllQuirks);
        return g;
    }

    /// <summary>
    /// Many genes shape each trait, so siblings scatter around their parents' midpoint. Without this
    /// noise, blending halves the population's variance each generation and everyone converges.
    /// With the Beta(2,2) blend (keeps 60% of variance), sd 0.14 holds the population sd near 0.25.
    /// </summary>
    public const float SegregationSd = 0.14f;

    /// <summary>Divergent genes are recorded so the chronicle and tests can see them.</summary>
    public static Genome Inherit(Genome mother, Genome father, Rng rng, out List<string> divergences)
    {
        var g = new Genome();
        for (int i = 0; i < Count; i++)
        {
            float t = rng.Beta22();
            g.Genes[i] = Math.Clamp(mother.Genes[i] * t + father.Genes[i] * (1f - t) + rng.Normal(0f, SegregationSd), 0f, 1f);
        }
        foreach (var q in AllQuirks)
        {
            if (mother.Has(q) && rng.Chance(0.5f)) g.Quirks |= q;
            if (father.Has(q) && rng.Chance(0.5f)) g.Quirks |= q;
        }

        divergences = new List<string>();
        int n = 1 + rng.Next(3);
        for (int k = 0; k < n; k++)
        {
            if (rng.Chance(0.2f))
            {
                var q = rng.Pick(AllQuirks);
                if (!g.Has(q)) { g.Quirks |= q; divergences.Add($"quirk:{Ids.Snake(q)}"); continue; }
            }
            int gi = rng.Next(Count);
            float before = g.Genes[gi];
            g.Genes[gi] = Math.Clamp(before + rng.Normal(0f, 0.2f), 0f, 1f);
            divergences.Add($"{Ids.Snake((Gene)gi)}:{before:0.00}->{g.Genes[gi]:0.00}");
        }
        return g;
    }
}
