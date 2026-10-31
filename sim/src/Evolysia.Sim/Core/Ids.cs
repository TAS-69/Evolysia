namespace Evolysia.Sim.Core;

public enum Biome : byte
{
    DeepOcean, Ocean, Coast, Lake,
    IceSheet, Tundra, BorealForest, ColdDesert, TemperateGrassland, TemperateForest,
    TemperateRainforest, Shrubland, HotDesert, Savanna, TropicalSeasonalForest,
    TropicalRainforest, Wetland, Alpine, Volcanic,
}

public enum Season : byte { Spring, Summer, Autumn, Winter, Wet, Dry }

public enum Trait : byte
{
    NoveltySeeking, HarmAvoidance, RewardDependence, Persistence,
    SelfDirectedness, Cooperativeness, SelfTranscendence,
}

public enum Emotion : byte { Joy, Fear, Anger, Sadness, Shame, Pride, Disgust, Surprise, Love, Calm, Envy }

public enum Need : byte { Hunger, Thirst, Energy, Warmth, Safety, Social }

public enum EventCategory : byte { Danger, Social, Discovery, Loss, Spiritual, Conflict, Trade, Birth }

public enum Domain : byte { Foraging, Hunting, Fishing, Crafting, Building, Social, Healing, Spiritual, Farming }

public enum Stimulus : byte
{
    Wolf, Bear, BigCat, Crocodile,
    River, Flood,
    Wildfire,
    Lightning, Blizzard,
    Earthquake, Eruption,
    Strangers,
    Plague,
}

public enum StimulusCategory : byte { Beasts, Water, Fire, Sky, Earth, Outsiders, Sickness }

public enum DeathCause : byte { None, OldAge, Starvation, Thirst, Cold, Predator, Violence, Disaster, Disease, Childbirth, Accident }

public enum Sex : byte { Female, Male }

public enum Activity : byte
{
    Rest, Forage, Hunt, Fish, Farm, Herd, Craft, Build, Socialize, Court, CareChild,
    Teach, Experiment, Ritual, Explore, Mourn, Play,
    Chop, Quarry, Mine, Gather,
}

[Flags]
public enum Quirk : ushort
{
    None = 0,
    Hardy = 1 << 0,
    Frail = 1 << 1,
    Glutton = 1 << 2,
    HotTempered = 1 << 3,
    Melancholic = 1 << 4,
    Empath = 1 << 5,
    Dreamer = 1 << 6,
    NightTerrors = 1 << 7,
    Stutter = 1 << 8,
    Lucky = 1 << 9,
    KeenEyed = 1 << 10,
    Charming = 1 << 11,
}

public static class Ids
{
    public const int TraitCount = 7, EmotionCount = 11, NeedCount = 6, DomainCount = 9;
    public const int StimulusCount = 13, CategoryCount = 7;

    public static StimulusCategory CategoryOf(Stimulus s) => s switch
    {
        Stimulus.Wolf or Stimulus.Bear or Stimulus.BigCat or Stimulus.Crocodile => StimulusCategory.Beasts,
        Stimulus.River or Stimulus.Flood => StimulusCategory.Water,
        Stimulus.Wildfire => StimulusCategory.Fire,
        Stimulus.Lightning or Stimulus.Blizzard => StimulusCategory.Sky,
        Stimulus.Earthquake or Stimulus.Eruption => StimulusCategory.Earth,
        Stimulus.Strangers => StimulusCategory.Outsiders,
        _ => StimulusCategory.Sickness,
    };

    public static string Snake<T>(T value) where T : Enum
    {
        var s = value.ToString();
        var sb = new System.Text.StringBuilder(s.Length + 4);
        for (int i = 0; i < s.Length; i++)
        {
            if (char.IsUpper(s[i]) && i > 0) sb.Append('_');
            sb.Append(char.ToLowerInvariant(s[i]));
        }
        return sb.ToString();
    }
}
