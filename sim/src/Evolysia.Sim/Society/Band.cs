using Evolysia.Sim.Core;

namespace Evolysia.Sim.Society;

/// <summary>A band/village: the "circle" an agent belongs to, shares food with and is judged by.</summary>
public sealed class Band
{
    public int Id;
    public string Name = "";
    public int Cell;
    public int CultureId;
    public int LeaderId = -1;
    public int FoundedDay;
    public int ParentBandId = -1;
    public readonly List<int> Members = new();
    public float FoodPool;
    public float Shelter;            // 0..1 quality of dwellings
    public float Stress;             // smoothed hunger/shortage; drives budding and migration
    public bool Sedentary;           // farming bands stop migrating
    public int LastMoveDay;
    public int PlagueUntil = -1;
    public float PlagueSeverity;
    public float Awe;                // accumulated wonder at acts no one can explain (the player's)
    public float DivineFavor;        // + for kindness witnessed, - for wrath
    public int FertileBlessingUntil = -1;
    public int LastRaidedDay = -100000;
    public readonly Dictionary<int, float> Relations = new();   // other band id -> -1..1
    public readonly float[] PlaceDread = new float[Ids.CategoryCount];   // collective unease about this place
    public readonly Dictionary<int, float> MemePrevalence = new();      // monthly cache: meme id -> share of adults
    public readonly Dictionary<int, int> KnownShores = new();           // sighted land across the sea -> water distance
    public bool Disbanded;

    public float RelationTo(int other) => Relations.TryGetValue(other, out var v) ? v : 0f;
}

public enum MemeKind : byte { SpiritBelief, Ritual, Taboo, SharingNorm, Hospitality, Xenophobia, ElderRule, InnovationTaboo }

/// <summary>
/// A transmissible idea: a myth, ritual, taboo or norm. Memes spread by talk with conformity
/// and prestige bias, so nearby bands converge and isolated ones drift apart.
/// </summary>
public sealed class Meme
{
    public int Id;
    public MemeKind Kind;
    public StimulusCategory Target;   // what a belief/ritual/taboo is about
    public string Name = "";          // e.g. "Oru the River Mother"
    public int OriginBandId, OriginAgentId, CreatedDay;
    public int PairedMemeId = -1;     // belief <-> its ritual
    public bool IsPlayerGod;          // this belief is about the player
    public int CultureId = -1;        // which people named the player this way
}

public sealed class Culture
{
    public int Id;
    public string Name = "";
    public int ParentId = -1;
    public int FoundedDay;
    public int Hue;                    // 0..11, picks lut_culture row
    public ulong GlyphSeed;
    public string[] Syllables = Array.Empty<string>();
}
