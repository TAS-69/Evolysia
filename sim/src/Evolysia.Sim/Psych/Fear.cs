using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;

namespace Evolysia.Sim.Psych;

public enum FearResponse : byte { Avoid, Confront }

public static class Fear
{
    public static void Scare(Agent a, Stimulus s, float intensity, int day)
    {
        float ha = a.T(Trait.HarmAvoidance);
        float target = intensity * (0.5f + ha) * (1f - 0.8f * a.Mastered[(int)s]);
        if (a.Genome.Has(Quirk.NightTerrors)) target *= 1.2f;
        target = MathF.Min(target, 1f);

        ref float f = ref a.Fear[(int)s];
        if (target > f) f += 0.6f * (target - f);

        ref float c = ref a.CategoryFear[(int)Ids.CategoryOf(s)];
        float general = target * ha * 0.7f;
        if (general > c) c += 0.5f * (general - c);

        a.Feel(Emotion.Fear, intensity * (0.4f + ha));
        a.Shift(Trait.HarmAvoidance, 0.008f * intensity, day);
    }

    public static void Observe(Agent a, Stimulus s, float intensity, int day)
    {
        float k = a.Genome.Has(Quirk.Empath) ? 0.7f : 0.45f;
        Scare(a, s, intensity * k, day);
    }

    public static void SafeExposure(Agent a, Stimulus s)
    {
        float rate = 0.012f * (1.1f - a.T(Trait.HarmAvoidance)) * (a.Genome.Has(Quirk.NightTerrors) ? 0.5f : 1f);
        a.Fear[(int)s] = MathF.Max(0f, a.Fear[(int)s] - rate);
        int c = (int)Ids.CategoryOf(s);
        a.CategoryFear[c] = MathF.Max(0f, a.CategoryFear[c] - rate * 0.4f);
    }

    public static FearResponse Decide(Agent a, Stimulus s, float needUrgency, int allies, float efficacy, Rng rng)
    {
        float ha = a.T(Trait.HarmAvoidance);
        float courage = needUrgency
                        + 0.5f * efficacy
                        + MathF.Min(0.5f, 0.12f * allies)
                        + 0.25f * a.T(Trait.Persistence)
                        + 0.25f * a.T(Trait.NoveltySeeking)
                        + 0.3f * a.Mastered[(int)s]
                        + (a.Genome.Has(Quirk.Lucky) ? 0.05f : 0f);
        float fear = a.EffectiveFear(s) * (0.5f + ha) + 0.25f * ha + 0.2f;
        return courage + rng.Normal(0f, 0.15f) > fear ? FearResponse.Confront : FearResponse.Avoid;
    }

    public static bool Overcame(Agent a, Stimulus s, Domain d, int day)
    {
        float before = a.EffectiveFear(s);
        a.Fear[(int)s] *= 0.35f;
        a.CategoryFear[(int)Ids.CategoryOf(s)] *= 0.7f;
        a.Mastered[(int)s] = MathF.Min(1f, a.Mastered[(int)s] + 0.8f * before);
        a.Efficacy[(int)d] = Math.Clamp(a.Efficacy[(int)d] + 0.05f + 0.1f * before, 0f, 1f);
        a.SelfEsteem = Math.Clamp(a.SelfEsteem + 0.05f * (0.5f + before), 0f, 1f);
        a.Feel(Emotion.Pride, 0.3f + 0.4f * before);
        a.Feel(Emotion.Joy, 0.2f + 0.4f * before);
        a.Shift(Trait.NoveltySeeking, 0.03f * before, day);
        a.Shift(Trait.HarmAvoidance, -0.04f * before, day);
        return before > 0.35f;
    }

    public static void Defeated(Agent a, Stimulus s, Domain d, bool witnessed, int day)
    {
        Scare(a, s, 0.9f, day);
        a.CategoryFear[(int)Ids.CategoryOf(s)] = MathF.Min(1f, a.CategoryFear[(int)Ids.CategoryOf(s)] + 0.15f * a.T(Trait.HarmAvoidance));
        a.Efficacy[(int)d] = Math.Clamp(a.Efficacy[(int)d] - 0.08f, 0f, 1f);
        if (witnessed) a.Feel(Emotion.Shame, 0.2f * a.T(Trait.RewardDependence));
    }

    public static void Avoided(Agent a, bool witnessed, int day)
    {
        a.Shift(Trait.HarmAvoidance, 0.003f, day);
        if (witnessed) a.Feel(Emotion.Shame, 0.08f * a.T(Trait.RewardDependence));
    }
}
