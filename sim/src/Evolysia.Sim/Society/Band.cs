using Evolysia.Sim.Core;
using Evolysia.Sim.Economy;
using Evolysia.Sim.Persistence;

namespace Evolysia.Sim.Society;

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

    public readonly Inventory Stores = new();

    public float FoodPool => Stores.FoodValue;
    public readonly List<int> Structures = new();
    public readonly SortedDictionary<string, float> Herds = new();
    public readonly List<Item> Spares = new();
    public float[] Targets = Array.Empty<float>();
    public float Shelter;
    public float ShelterWarmth;
    public bool HasFire;
    public bool HasFireToday = true;
    public bool HasColdStore;
    public float WaterBonus;
    public int BuildJobs;
    public int StorageRoom;
    public float Stress;
    public float CraftNeed;
    public int CraftNeedDay = int.MinValue / 2;
    public bool Sedentary;
    public int LastMoveDay;
    public int PlagueUntil = -1;
    public float PlagueSeverity;
    public float Awe;
    public float DivineFavor;
    public int FertileBlessingUntil = -1;
    public int LastRaidedDay = -100000;
    public readonly Dictionary<int, float> Relations = new();
    public readonly float[] PlaceDread = new float[Ids.CategoryCount];
    public readonly Dictionary<int, float> MemePrevalence = new();
    public readonly Dictionary<int, int> KnownShores = new();
    public bool Disbanded;

    public float RelationTo(int other) => Relations.TryGetValue(other, out var v) ? v : 0f;
}

public enum MemeKind : byte { SpiritBelief, Ritual, Taboo, SharingNorm, Hospitality, Xenophobia, ElderRule, InnovationTaboo }

public sealed class Meme
{
    [SaveOptional] public string Doctrine = "";
    public int Id;
    public MemeKind Kind;
    public StimulusCategory Target;
    public string Name = "";
    public int OriginBandId, OriginAgentId, CreatedDay;
    public int PairedMemeId = -1;
    public bool IsPlayerGod;
    public int CultureId = -1;
}

public sealed class Culture
{
    public int Id;
    public string Name = "";
    public int ParentId = -1;
    public int FoundedDay;
    public int Hue;
    public ulong GlyphSeed;
    public string[] Syllables = Array.Empty<string>();
}
