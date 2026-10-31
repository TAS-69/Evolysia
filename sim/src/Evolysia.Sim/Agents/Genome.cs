using Evolysia.Sim.Core;

namespace Evolysia.Sim.Agents;

public enum Gene : byte
{

    NoveltySeeking, HarmAvoidance, RewardDependence, Persistence,

    SelfDirectedness, Cooperativeness, SelfTranscendence,

    Strength, Dexterity, Intellect, Perception, Constitution, Fertility, Longevity, Charisma,

    Pigment, HairTone,
}

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

    public const float SegregationSd = 0.14f;

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
