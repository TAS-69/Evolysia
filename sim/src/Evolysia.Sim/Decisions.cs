using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Evolysia.Sim.Psych;

namespace Evolysia.Sim;

public enum DecisionKind : byte { Pivot, Meeting }

/// <summary>
/// A choice someone is brooding over. The simulation has already limited it to options their
/// personality makes plausible; a mind (the LLM) may pick one before the deadline, otherwise
/// instinct (the trait-weighted roll) decides. Either way the outcome is logged as an input.
/// </summary>
public sealed class Decision
{
    public int Id, Day, Deadline, AgentId, BandId, OtherBandId = -1;
    public DecisionKind Kind;
    public string[] Options = Array.Empty<string>();
    public string[] Descriptions = Array.Empty<string>();
    public string InstinctFavourite = "";     // what temperament alone most likely does
    public bool Resolved;
    public string? Chosen;
    public string Source = "";                // "mind" or "instinct"
    internal PivotCase? Pivot;
    internal string MeetingInstinct = "";
}

/// <summary>Everything needed to carry out a pivot later, whichever way it is decided.</summary>
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

    public static string Meeting(string option, string other) => option switch
    {
        "welcome" => $"Welcome the {other} with a feast and share what we know",
        "raid" => $"Attack the {other} and take their food before they take ours",
        _ => $"Keep a wary distance from the {other}, trade a little, trust nothing",
    };
}
