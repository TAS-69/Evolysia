using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Evolysia.Sim.Psych;

namespace Evolysia.Sim;

public enum DecisionKind : byte { Pivot, Meeting, Message }

public sealed class Decision
{
    public int Id, Day, Deadline, AgentId, BandId, OtherBandId = -1;
    public DecisionKind Kind;
    public string[] Options = Array.Empty<string>();
    public string[] Descriptions = Array.Empty<string>();
    public string InstinctFavourite = "";
    public bool Resolved;
    public string? Chosen;
    public string Source = "";
    internal PivotCase? Pivot;
    internal string MeetingInstinct = "";
    internal int MessageId = -1;
}

internal sealed record PivotCase(int AgentId, int BandId, Domain Domain, PivotContext Ctx, string Kind, string What,
                                 int[] AllyIds, int MemeId, TechId? Tech, int GrudgeTargetId);

public static class DecisionText
{
    public static string Pivot(PivotChoice c, string kind, int allies) => (kind, c) switch
    {
        ("courtship", PivotChoice.Conform) => "Give up on love and stop hoping",
        ("courtship", PivotChoice.DoubleDown) => "Try again, harder, and win them over",
        ("courtship", PivotChoice.Withdraw) => "Withdraw and nurse the hurt alone",
        ("courtship", PivotChoice.Resent) => "Nurse a grudge against the one who refused, and make them pay for it one day",
        (_, PivotChoice.Conform) => "Give it up and never try such things again",
        (_, PivotChoice.DoubleDown) => "Keep at it anyway and prove them wrong",
        (_, PivotChoice.Withdraw) => "Pull away from everyone and keep to themself",
        (_, PivotChoice.Resent) => "Smile and say nothing, but never forgive those who laughed, and wait for a chance to get even",
        _ => allies > 0 ? $"Leave the band for good, taking the {allies} who sided with them" : "Leave the band for good, alone",
    };

    public static string Message(string option, bool understood) => option switch
    {
        "scripture" => understood ? "Keep these words as sacred and teach them to everyone" : "Keep these marks as sacred, though no one can read them",
        "vision" => "Take it as a wonder, a sign from beyond, and marvel at it",
        "command" => "Take the words as a command and do what they say",
        "dismissed" => understood ? "Laugh it off: a trick, or a madman's scrawl" : "Ignore the scratches; they mean nothing",
        _ => "Fear it as a curse and keep away from that place",
    };

    public static string Meeting(string option, string other) => option switch
    {
        "welcome" => $"Welcome the {other} with a feast and share what we know",
        "raid" => $"Attack the {other} and take their food before they take ours",
        _ => $"Keep a wary distance from the {other}, trade a little, trust nothing",
    };
}
