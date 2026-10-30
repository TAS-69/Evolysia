namespace Evolysia.Sim;

/// <summary>The player's powers. IDs match the god-power icons in docs/art/ART_DIRECTION.md section 11.3.</summary>
public enum Power : byte
{
    Rain, Drought, Bless, Fertility, Heal, Game,                       // kindness
    Lightning, Wildfire, Flood, Earthquake, Blizzard, Plague, Meteor, Beasts, Curse,   // wrath
    Dream,                                                              // a word in someone's sleep
}

/// <summary>What a dream urges. The player's own words are flavour for the narrator; the kind decides the effect.</summary>
public enum DreamKind : byte { Worship, Leave, Peace, Seek, Courage, Love }

public enum PrayerNeed : byte { Rain, Food, Healing, Children, Protection }

/// <summary>One act of the player, applied between days and logged for replay.</summary>
public sealed record GodAction(Power Power, int Cell, int Radius = -1, int TargetAgentId = -1,
                               DreamKind Dream = DreamKind.Worship, string Words = "");

public sealed class Prayer
{
    public int Id, Day, BandId, AgentId;
    public PrayerNeed Need;
    public bool Answered, Expired;
    public int ResolvedDay = -1;
    public bool Open => !Answered && !Expired;
}

public static class Powers
{
    public static bool IsKind(Power p) => p is Power.Rain or Power.Bless or Power.Fertility or Power.Heal or Power.Game;
    public static bool NeedsPerson(Power p) => p is Power.Dream or Power.Curse;

    /// <summary>Spectacle: how much an act makes people wonder who did it.</summary>
    public static float Spectacle(Power p) => p switch
    {
        Power.Meteor => 2f, Power.Lightning or Power.Heal or Power.Earthquake => 1f,
        Power.Rain or Power.Bless or Power.Flood or Power.Wildfire or Power.Blizzard => 0.7f,
        _ => 0.4f,
    };

    public static int DefaultRadius(Power p) => p switch
    {
        Power.Rain or Power.Drought => 3,
        Power.Meteor or Power.Earthquake or Power.Beasts or Power.Game => 2,
        Power.Bless or Power.Fertility or Power.Heal or Power.Flood or Power.Wildfire or Power.Blizzard => 1,
        _ => 0,
    };

    public static bool Answers(Power p, PrayerNeed need) => need switch
    {
        PrayerNeed.Rain => p == Power.Rain,
        PrayerNeed.Food => p is Power.Bless or Power.Rain or Power.Game,
        PrayerNeed.Healing => p == Power.Heal,
        PrayerNeed.Children => p == Power.Fertility,
        _ => false,   // Protection is answered by striking the band's enemies (see Simulation.Gods)
    };

    public static string Verb(PrayerNeed n) => n switch
    {
        PrayerNeed.Rain => "rain", PrayerNeed.Food => "food", PrayerNeed.Healing => "healing",
        PrayerNeed.Children => "children", _ => "protection from their enemies",
    };
}
