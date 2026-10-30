using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;

namespace Evolysia.Sim.Psych;

/// <summary>How someone responds when their circle rejects something they cared about.</summary>
public enum PivotChoice : byte
{
    Conform,    // give in: abandon it, never try that kind of thing again
    DoubleDown, // persist and try to prove them wrong
    Withdraw,   // retreat from people, keep to themself
    Resent,     // hold a grudge against those who mocked
    Leave,      // walk away from the circle (with followers if any)
}

public readonly record struct PivotContext(
    float Severity,        // 0..1 how public/harsh the rejection was
    int Allies,            // people who sided with them
    bool CanLeave,         // is there anywhere / anyone to go with
    string Kind);          // "invention", "belief", "courtship", ...

/// <summary>
/// Trait loadings match the LLM lab's "invention_mocked" scenario, so the numeric sim and the
/// language model agree on what each personality tends to do. The LLM narrates the choice; it
/// doesn't make it.
/// </summary>
public static class Pivot
{
    static readonly (PivotChoice choice, (Trait t, float w)[] load)[] Options =
    {
        (PivotChoice.Conform,    new[] { (Trait.RewardDependence, 1.0f), (Trait.SelfDirectedness, -0.8f), (Trait.Persistence, -0.6f) }),
        (PivotChoice.DoubleDown, new[] { (Trait.Persistence, 1.0f), (Trait.SelfDirectedness, 0.8f), (Trait.RewardDependence, -0.5f) }),
        (PivotChoice.Withdraw,   new[] { (Trait.HarmAvoidance, 0.7f), (Trait.RewardDependence, 0.3f), (Trait.SelfDirectedness, -0.3f), (Trait.Cooperativeness, -0.2f) }),
        (PivotChoice.Resent,     new[] { (Trait.Cooperativeness, -1.0f), (Trait.Persistence, 0.3f) }),
        (PivotChoice.Leave,      new[] { (Trait.NoveltySeeking, 0.8f), (Trait.SelfDirectedness, 0.7f), (Trait.RewardDependence, -0.7f) }),
    };

    public static float[] Scores(Agent a, in PivotContext ctx)
    {
        var scores = new float[Options.Length];
        for (int k = 0; k < Options.Length; k++)
        {
            float s = 0f;
            foreach (var (t, w) in Options[k].load) s += w * (a.T(t) - 0.5f) * 2f;
            scores[k] = s;
        }
        float allyShare = MathF.Min(1f, ctx.Allies / 3f);
        scores[(int)PivotChoice.Conform] += 1.2f * (a.Belonging - 0.5f) - 0.6f * allyShare;
        scores[(int)PivotChoice.DoubleDown] += 0.9f * allyShare + 0.5f * a.SelfEsteem;
        scores[(int)PivotChoice.Withdraw] += 0.8f * a.Emo(Emotion.Shame) + 0.3f * ctx.Severity;
        scores[(int)PivotChoice.Resent] += 0.8f * a.Emo(Emotion.Anger) + (a.Genome.Has(Quirk.HotTempered) ? 0.4f : 0f);
        scores[(int)PivotChoice.Leave] = ctx.CanLeave && (ctx.Allies >= 2 || a.T(Trait.NoveltySeeking) > 0.65f)
            ? scores[(int)PivotChoice.Leave] + 0.6f * allyShare - 0.4f
            : float.NegativeInfinity;
        return scores;
    }

    public static PivotChoice Resolve(Agent a, in PivotContext ctx, Rng rng) =>
        (PivotChoice)rng.Softmax(Scores(a, ctx), 0.55f);

    /// <summary>The personal (psychological) side of each choice. Social consequences live in the simulation.</summary>
    public static void ApplyInner(Agent a, PivotChoice c, Domain d, in PivotContext ctx, int day)
    {
        float s = 0.5f + 0.5f * ctx.Severity;
        a.Feel(Emotion.Shame, 0.3f * s * a.T(Trait.RewardDependence));
        a.Feel(Emotion.Sadness, 0.2f * s);
        switch (c)
        {
            case PivotChoice.Conform:
                a.SuppressedDomains |= 1u << (int)d;
                a.Efficacy[(int)d] = Math.Clamp(a.Efficacy[(int)d] - 0.2f * s, 0f, 1f);
                a.Shift(Trait.RewardDependence, 0.06f * s, day);
                a.Shift(Trait.SelfDirectedness, -0.06f * s, day);
                a.Shift(Trait.Persistence, -0.04f * s, day);
                a.Shift(Trait.NoveltySeeking, -0.04f * s, day);
                a.Belonging = Math.Clamp(a.Belonging + 0.05f, 0f, 1f);
                break;
            case PivotChoice.DoubleDown:
                a.Shift(Trait.Persistence, 0.05f * s, day);
                a.Shift(Trait.SelfDirectedness, 0.06f * s, day);
                a.Shift(Trait.RewardDependence, -0.05f * s, day);
                a.Feel(Emotion.Pride, 0.2f);
                a.Feel(Emotion.Anger, 0.2f * s);
                a.Belonging = Math.Clamp(a.Belonging - 0.08f * s, 0f, 1f);
                break;
            case PivotChoice.Withdraw:
                a.WithdrawnUntil = day + 60 + (int)(120 * s);
                a.Shift(Trait.HarmAvoidance, 0.05f * s, day);
                a.Shift(Trait.SelfDirectedness, -0.03f * s, day);
                a.Feel(Emotion.Sadness, 0.3f * s);
                a.Belonging = Math.Clamp(a.Belonging - 0.12f * s, 0f, 1f);
                break;
            case PivotChoice.Resent:
                a.Shift(Trait.Cooperativeness, -0.08f * s, day);
                a.Feel(Emotion.Anger, 0.5f * s);
                a.Belonging = Math.Clamp(a.Belonging - 0.1f * s, 0f, 1f);
                break;
            case PivotChoice.Leave:
                a.Shift(Trait.SelfDirectedness, 0.07f * s, day);
                a.Shift(Trait.RewardDependence, -0.06f * s, day);
                a.Shift(Trait.NoveltySeeking, 0.03f * s, day);
                a.Belonging = 0.7f;   // the new circle
                break;
        }
    }
}
