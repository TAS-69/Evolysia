using Evolysia.Sim.Core;

namespace Evolysia.Sim.Agents;

public sealed class Relationship
{
    public float Affinity;
    public float Trust;
    public bool Grudge;
}

public sealed record LifeEvent(int Day, EventCategory Category, string Kind, float Importance, int OtherId = -1, string Detail = "");

public sealed class Agent
{
    public int Id;
    public string Name = "";
    public Sex Sex;
    public int BirthDay;
    public int MotherId = -1, FatherId = -1, PartnerId = -1;
    public readonly List<int> Children = new();
    public Genome Genome = null!;
    public int Generation;

    public readonly float[] TraitOffset = new float[Ids.TraitCount];
    public readonly float[] Emotions = new float[Ids.EmotionCount];
    public readonly float[] Efficacy = new float[Ids.DomainCount];
    public readonly float[] Fear = new float[Ids.StimulusCount];
    public readonly float[] CategoryFear = new float[Ids.CategoryCount];
    public readonly float[] Mastered = new float[Ids.StimulusCount];
    public uint SuppressedDomains;
    public float SelfEsteem = 0.5f;
    public float Belonging = 0.6f;
    public float Prestige;
    public int WithdrawnUntil;
    public int Scars;
    public int InspiredUntil = -1;

    public readonly float[] Needs = new float[Ids.NeedCount];
    public float Health = 1f;
    public bool Sick;
    public int SickUntil;
    Economy.Inventory? _pack;

    public Economy.Inventory Pack => _pack ??= new Economy.Inventory();

    public float Food => _pack?.FoodValue ?? 0f;
    internal void DropPack() => _pack = null;

    public readonly List<Economy.Item> Gear = new();
    public bool Pregnant;
    public int DueDay, PregnancyFatherId = -1, LastBirthDay = -100000;

    public int BandId = -1;
    public int CultureId = -1;
    public TechSet Knows;
    public readonly Dictionary<int, float> Memes = new();
    public readonly float[] HeldKind = new float[8];
    public readonly float[] TabooOf = new float[Ids.CategoryCount];
    public readonly Dictionary<int, Relationship> Relations = new();
    public readonly List<LifeEvent> Memory = new();
    public readonly int[] ActivityCounts = new int[Enum.GetValues<Activity>().Length];
    public Activity Today;
    public int Site = -1;

    public bool Alive = true;
    public DeathCause DeathCause;
    public int DeathDay = -1;

    public int AgeDays(int day) => day - BirthDay;
    public float Age(int day) => (day - BirthDay) / (float)SimConfig.DaysPerYear;
    public bool IsChild(int day) => Age(day) < 14f;
    public bool IsAdult(int day) => Age(day) >= 14f;

    public float T(Trait t) => Math.Clamp(Genome.Genes[(int)t] + TraitOffset[(int)t], 0f, 1f);
    public float Gene(Gene g) => Genome.Genes[(int)g];
    public float Emo(Emotion e) => Emotions[(int)e];
    public float NeedOf(Need n) => Needs[(int)n];

    public float Plasticity(int day)
    {
        float age = Age(day);
        return age < 14f ? 1f : Math.Clamp(1f - (age - 14f) / 55f, 0.12f, 1f);
    }

    public void Shift(Trait t, float delta, int day, bool scaleByPlasticity = true)
    {
        float d = scaleByPlasticity ? delta * Plasticity(day) : delta;
        TraitOffset[(int)t] = Math.Clamp(TraitOffset[(int)t] + d, -0.5f, 0.5f);
    }

    public void Feel(Emotion e, float amount) =>
        Emotions[(int)e] = Math.Clamp(Emotions[(int)e] + amount, 0f, 1f);

    public float EffectiveFear(Stimulus s) =>
        MathF.Max(Fear[(int)s], CategoryFear[(int)Ids.CategoryOf(s)] * 0.8f);

    public bool IsSuppressed(Domain d) => (SuppressedDomains & (1u << (int)d)) != 0;

    public Relationship Rel(int otherId)
    {
        if (!Relations.TryGetValue(otherId, out var r)) Relations[otherId] = r = new Relationship();
        return r;
    }

    public float AffinityTo(int otherId) => Relations.TryGetValue(otherId, out var r) ? r.Affinity : 0f;

    public void Remember(LifeEvent e)
    {
        Memory.Add(e);
        if (Memory.Count > 40)
        {
            int weakest = 0;
            for (int i = 1; i < Memory.Count; i++) if (Memory[i].Importance < Memory[weakest].Importance) weakest = i;
            Memory.RemoveAt(weakest);
        }
    }

    public Activity MainActivity()
    {
        int best = (int)Activity.Forage;
        for (int i = 0; i < ActivityCounts.Length; i++)
            if (i != (int)Activity.Rest && i != (int)Activity.Play && ActivityCounts[i] > ActivityCounts[best]) best = i;
        return (Activity)best;
    }
}
