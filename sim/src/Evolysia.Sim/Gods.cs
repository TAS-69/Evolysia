namespace Evolysia.Sim;

public enum Power : byte
{
    Rain, Drought, Bless, Fertility, Heal, Game,
    Lightning, Wildfire, Flood, Earthquake, Blizzard, Plague, Meteor, Beasts, Curse,
    Dream,
    Message,
}

public enum DreamKind : byte { Worship, Leave, Peace, Seek, Courage, Love }

public enum PrayerNeed : byte { Rain, Food, Healing, Children, Protection }

public sealed record GodAction(Power Power, int Cell, int Radius = -1, int TargetAgentId = -1,
                               DreamKind Dream = DreamKind.Worship, string Words = "", MessageMedium Medium = MessageMedium.Ground);

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

    public static float Spectacle(Power p) => p switch
    {
        Power.Meteor => 2f, Power.Lightning or Power.Heal or Power.Earthquake => 1f,
        Power.Rain or Power.Bless or Power.Flood or Power.Wildfire or Power.Blizzard => 0.7f,
        _ => 0.4f,
    };

    public static int DefaultRadius(Power p, float scale = 1f) => (int)MathF.Round(scale * p switch
    {
        Power.Rain or Power.Drought => 3,
        Power.Meteor or Power.Earthquake or Power.Beasts or Power.Game => 2,
        Power.Bless or Power.Fertility or Power.Heal or Power.Flood or Power.Wildfire or Power.Blizzard => 1,
        _ => 0,
    });

    public static bool Answers(Power p, PrayerNeed need) => need switch
    {
        PrayerNeed.Rain => p == Power.Rain,
        PrayerNeed.Food => p is Power.Bless or Power.Rain or Power.Game,
        PrayerNeed.Healing => p == Power.Heal,
        PrayerNeed.Children => p == Power.Fertility,
        _ => false,
    };

    public static string Verb(PrayerNeed n) => n switch
    {
        PrayerNeed.Rain => "rain", PrayerNeed.Food => "food", PrayerNeed.Healing => "healing",
        PrayerNeed.Children => "children", _ => "protection from their enemies",
    };
}
