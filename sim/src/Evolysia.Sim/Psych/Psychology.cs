using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;

namespace Evolysia.Sim.Psych;

public enum Agency : byte { Self, Other, Nature, Unknown }

public readonly record struct Appraisal(
    EventCategory Category,
    string Kind,
    float Valence,
    float Intensity,
    Agency Agency,
    bool Threat = false,
    bool Loss = false,
    bool Witnessed = false,
    int OtherId = -1,
    string Detail = "");

public static class Psychology
{
    static readonly float[] Decay =
    {
        0.85f,
        0.82f,
        0.80f,
        0.95f,
        0.93f,
        0.90f,
        0.80f,
        0.40f,
        0.985f,
        1.00f,
        0.92f,
    };

    public static void Appraise(Agent a, in Appraisal e, int day)
    {
        float ha = a.T(Trait.HarmAvoidance), co = a.T(Trait.Cooperativeness), rd = a.T(Trait.RewardDependence);
        float sd = a.T(Trait.SelfDirectedness);
        float i = e.Intensity;

        if (e.Valence < 0f)
        {
            float bad = -e.Valence * i;
            if (e.Threat) a.Feel(Emotion.Fear, bad * (0.4f + ha));
            if (e.Loss) a.Feel(Emotion.Sadness, bad * (0.6f + 0.3f * ha) * (a.Genome.Has(Quirk.Melancholic) ? 1.3f : 1f));
            if (e.Agency == Agency.Other)
                a.Feel(Emotion.Anger, bad * (0.4f + 0.5f * (1f - co)) * (a.Genome.Has(Quirk.HotTempered) ? 1.5f : 1f));
            if (e.Agency == Agency.Self)
                a.Feel(e.Witnessed ? Emotion.Shame : Emotion.Sadness, bad * (0.3f + (e.Witnessed ? rd : 0.3f)));
            if (e.Agency is Agency.Unknown or Agency.Nature) a.Feel(Emotion.Surprise, i * 0.6f);
            a.SelfEsteem = Math.Clamp(a.SelfEsteem - (e.Agency == Agency.Self ? bad * 0.08f : 0f), 0f, 1f);
        }
        else if (e.Valence > 0f)
        {
            float good = e.Valence * i;
            a.Feel(Emotion.Joy, good * 0.7f);
            if (e.Agency == Agency.Self)
            {
                a.Feel(Emotion.Pride, good * (0.4f + 0.4f * sd));
                a.SelfEsteem = Math.Clamp(a.SelfEsteem + good * 0.06f, 0f, 1f);
            }
            if (e.Agency == Agency.Other) a.Feel(Emotion.Love, good * 0.25f * co);
        }

        float importance = i * (0.5f + MathF.Abs(e.Valence));
        if (importance > 0.35f) a.Remember(new LifeEvent(day, e.Category, e.Kind, importance, e.OtherId, e.Detail));
    }

    public static void Envy(Agent a, float amount) =>
        a.Feel(Emotion.Envy, amount * (1f - a.T(Trait.Cooperativeness)) * (0.5f + a.T(Trait.RewardDependence)));

    public static void DailyUpdate(Agent a, int day)
    {
        bool terrors = a.Genome.Has(Quirk.NightTerrors), melancholic = a.Genome.Has(Quirk.Melancholic);
        for (int k = 0; k < Ids.EmotionCount; k++)
        {
            float d = Decay[k];
            if (k == (int)Emotion.Fear && terrors) d = 0.9f;
            if (k == (int)Emotion.Sadness && melancholic) d = 0.98f;
            a.Emotions[k] *= d;
        }
        float distress = a.Emo(Emotion.Fear) + a.Emo(Emotion.Anger) + a.Emo(Emotion.Sadness) + a.Emo(Emotion.Shame);
        a.Emotions[(int)Emotion.Calm] = Math.Clamp(1f - distress * 0.6f, 0f, 1f);

        const float KDrift = 0.00012f, ReturnRate = 0.0002f;
        a.Shift(Trait.HarmAvoidance, KDrift * (a.Emo(Emotion.Fear) - 0.08f) * 2f, day);
        a.Shift(Trait.RewardDependence, KDrift * (a.Belonging - 0.65f) * 2f, day);
        a.Shift(Trait.SelfDirectedness, KDrift * ((a.SelfEsteem - 0.5f) + (a.Emo(Emotion.Pride) - a.Emo(Emotion.Shame))), day);
        a.Shift(Trait.Cooperativeness, KDrift * (0.4f * (a.Emo(Emotion.Love) - 0.3f) + 0.3f * (a.Emo(Emotion.Joy) - 0.15f)
                                                 - a.Emo(Emotion.Anger) - a.Emo(Emotion.Envy)), day);
        a.Shift(Trait.NoveltySeeking, KDrift * 0.5f * (a.Emo(Emotion.Pride) - a.Emo(Emotion.Fear)), day);
        for (int t = 0; t < Ids.TraitCount; t++) a.TraitOffset[t] *= 1f - ReturnRate;

        a.Prestige *= 0.9995f;
        a.Belonging = Math.Clamp(a.Belonging + (0.6f - a.Belonging) * 0.01f, 0f, 1f);
        a.SelfEsteem += (0.5f - a.SelfEsteem) * 0.001f;
    }

    public static void Praise(Agent a, Domain d, float magnitude, int day)
    {
        a.Feel(Emotion.Pride, 0.4f * magnitude);
        a.Feel(Emotion.Joy, 0.3f * magnitude);
        a.Efficacy[(int)d] = Math.Clamp(a.Efficacy[(int)d] + 0.12f * magnitude, 0f, 1f);
        a.SelfEsteem = Math.Clamp(a.SelfEsteem + 0.08f * magnitude, 0f, 1f);
        a.Prestige += 0.3f * magnitude;
        a.Belonging = Math.Clamp(a.Belonging + 0.05f * magnitude, 0f, 1f);
        a.Shift(Trait.SelfDirectedness, 0.03f * magnitude, day);
        if (a.T(Trait.Cooperativeness) < 0.35f) a.Shift(Trait.Cooperativeness, -0.015f * magnitude, day);
    }

    public static void Practice(Agent a, Domain d, bool success, float weight = 1f)
    {
        ref float e = ref a.Efficacy[(int)d];
        if (success) e += 0.02f * weight * (1f - e);
        else e -= 0.012f * weight * (0.5f + a.T(Trait.HarmAvoidance)) * e;
        e = Math.Clamp(e, 0f, 1f);
    }
}
