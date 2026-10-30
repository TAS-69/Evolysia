namespace Evolysia.Sim.Core;

// These IDs are shared with docs/art/ART_DIRECTION.md (asset names) and the LLM lab.
// Renaming one is an art + code change.

public enum Biome : byte
{
    DeepOcean, Ocean, Coast, Lake,
    IceSheet, Tundra, BorealForest, ColdDesert, TemperateGrassland, TemperateForest,
    TemperateRainforest, Shrubland, HotDesert, Savanna, TropicalSeasonalForest,
    TropicalRainforest, Wetland, Alpine, Volcanic,
}

public enum Season : byte { Spring, Summer, Autumn, Winter, Wet, Dry }

/// <summary>Temperament is inherited; character develops with experience. Values are 0..1.</summary>
public enum Trait : byte
{
    NoveltySeeking, HarmAvoidance, RewardDependence, Persistence,   // temperament
    SelfDirectedness, Cooperativeness, SelfTranscendence,           // character
}

public enum Emotion : byte { Joy, Fear, Anger, Sadness, Shame, Pride, Disgust, Surprise, Love, Calm, Envy }

public enum Need : byte { Hunger, Thirst, Energy, Warmth, Safety, Social }

public enum EventCategory : byte { Danger, Social, Discovery, Loss, Spiritual, Conflict, Trade, Birth }

/// <summary>Skill domains with their own self-efficacy (confidence).</summary>
public enum Domain : byte { Foraging, Hunting, Fishing, Crafting, Building, Social, Healing, Spiritual, Farming }

/// <summary>Specific things an agent can learn to fear. Each belongs to a broader category.</summary>
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

/// <summary>Daily activity. Names map to animation ids in the art guide (section 6.2).</summary>
public enum Activity : byte
{
    Rest, Forage, Hunt, Fish, Farm, Herd, Craft, Build, Socialize, Court, CareChild,
    Teach, Experiment, Ritual, Explore, Mourn, Play,
}

[Flags]
public enum Quirk : ushort
{
    None = 0,
    Hardy = 1 << 0,        // tolerates cold
    Frail = 1 << 1,        // more vulnerable to disease
    Glutton = 1 << 2,      // gets hungry faster
    HotTempered = 1 << 3,  // anger flares stronger
    Melancholic = 1 << 4,  // sadness lingers
    Empath = 1 << 5,       // learns strongly from others' experiences
    Dreamer = 1 << 6,      // more creative and spiritual
    NightTerrors = 1 << 7, // fears fade slowly
    Stutter = 1 << 8,      // social efficacy grows slowly
    Lucky = 1 << 9,        // small edge in dangerous moments
    KeenEyed = 1 << 10,    // spots prey and danger early
    Charming = 1 << 11,    // attractive and persuasive
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

    /// <summary>snake_case id used by assets, logs and the LLM lab.</summary>
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
