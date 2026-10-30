using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Evolysia.Sim.Psych;
using Evolysia.Sim.Society;

namespace Evolysia.Sim;

public sealed partial class Simulation
{
    /// <summary>When true, pivotal choices wait (brood) for an outside mind before instinct decides.</summary>
    public bool DeferDecisions;
    public int DecisionWindowDays = 10;
    public readonly List<Decision> Decisions = new();
    public readonly List<(int Day, int DecisionId, string Choice, string Source)> DecisionLog = new();
    int _nextDecision = 1;

    public IEnumerable<Decision> PendingDecisions => Decisions.Where(d => !d.Resolved);

    /// <summary>
    /// A mind chooses for someone. Accepted only while the decision is pending and only among its
    /// sanctioned options. Applied between days, logged, so seed + logs replay exactly.
    /// </summary>
    public bool Decide(int decisionId, string option)
    {
        var d = Decisions.FirstOrDefault(x => x.Id == decisionId);
        if (d == null || d.Resolved || !d.Options.Contains(option)) return false;
        Resolve(d, option, "mind");
        FlushDeathsAndBirths();
        return true;
    }

    void ResolveDueDecisions()
    {
        foreach (var d in Decisions.Where(d => !d.Resolved && d.Deadline <= Day).ToList())
        {
            string choice = d.Kind == DecisionKind.Pivot && d.Pivot != null && Living.TryGetValue(d.AgentId, out var a)
                ? Ids.Snake(Psych.Pivot.Resolve(a, d.Pivot.Ctx, Rng))
                : d.MeetingInstinct;
            Resolve(d, choice, "instinct");
        }
        if (Decisions.Count > 400) Decisions.RemoveAll(d => d.Resolved && Day - d.Day > 3 * SimConfig.DaysPerYear);
    }

    void Resolve(Decision d, string choice, string source)
    {
        d.Resolved = true; d.Chosen = choice; d.Source = source;
        DecisionLog.Add((Day, d.Id, choice, source));
        if (source == "mind")
        {
            Stats.Year.MindChoices++;
            if (choice == d.InstinctFavourite) Stats.Year.MindAgreedWithInstinct++;
        }
        else Stats.Year.InstinctChoices++;

        if (d.Kind == DecisionKind.Pivot && d.Pivot != null)
            FinishPivot(d.Pivot, Enum.GetValues<PivotChoice>().First(c => Ids.Snake(c) == choice));
        else if (d.Kind == DecisionKind.Meeting && Bands.TryGetValue(d.BandId, out var a) && Bands.TryGetValue(d.OtherBandId, out var b))
            CarryOutMeeting(a, b, choice);
    }

    // ------------------------------------------------------------------ pivots

    internal void BeginPivot(Agent a, Band band, PivotCase pc)
    {
        if (!DeferDecisions)
        {
            FinishPivot(pc, Psych.Pivot.Resolve(a, pc.Ctx, Rng));
            return;
        }
        // Shortlist: the options this personality makes plausible (instinct's top three).
        var scores = Psych.Pivot.Scores(a, pc.Ctx);
        var ranked = Enumerable.Range(0, scores.Length).Where(i => !float.IsNegativeInfinity(scores[i]))
            .OrderByDescending(i => scores[i]).Take(3).Select(i => (PivotChoice)i).ToArray();
        var d = new Decision
        {
            Id = _nextDecision++, Day = Day, Deadline = Day + DecisionWindowDays, AgentId = a.Id, BandId = band.Id,
            Kind = DecisionKind.Pivot, Pivot = pc,
            Options = ranked.Select(c => Ids.Snake(c)).ToArray(),
            Descriptions = ranked.Select(c => DecisionText.Pivot(c, pc.Kind, pc.AllyIds.Length)).ToArray(),
            InstinctFavourite = Ids.Snake(ranked[0]),
        };
        Decisions.Add(d);
        Chronicle.Moment(new LlmMoment(Day, "decision", a.Id, band.Id, DecisionFacts(d, new()
        {
            ["trigger"] = pc.Kind, ["subject"] = pc.What, ["allies"] = pc.AllyIds.Length.ToString(),
        })));
    }

    static Dictionary<string, string> DecisionFacts(Decision d, Dictionary<string, string> extra)
    {
        extra["decision_id"] = d.Id.ToString();
        extra["decision_kind"] = Ids.Snake(d.Kind);
        extra["options"] = string.Join(",", d.Options);
        extra["descriptions"] = string.Join("|", d.Descriptions);
        extra["deadline"] = d.Deadline.ToString();
        return extra;
    }

    void FinishPivot(PivotCase pc, PivotChoice choice)
    {
        if (!Living.TryGetValue(pc.AgentId, out var a) || !Bands.TryGetValue(a.BandId, out var band)) return;
        var allies = pc.AllyIds.Select(id => Living.GetValueOrDefault(id)).Where(x => x != null && x.BandId == band.Id).Select(x => x!).ToList();
        if (choice == PivotChoice.Leave && (!pc.Ctx.CanLeave || band.Members.Count <= 6)) choice = PivotChoice.Withdraw;
        Psych.Pivot.ApplyInner(a, choice, pc.Domain, pc.Ctx, Day);
        switch (choice)
        {
            case PivotChoice.Conform:
                if (pc.Tech is { } t) a.Knows &= ~Techs.Bit(t);   // abandons it; nobody else may know it
                a.Memes.Remove(pc.MemeId);
                break;
            case PivotChoice.DoubleDown:
                foreach (var ally in allies)
                {
                    if (pc.Tech is { } t2 && Rng.Chance(0.4f)) ally.Knows |= Techs.Bit(t2);
                    if (Memes.ContainsKey(pc.MemeId)) ally.Memes[pc.MemeId] = MathF.Max(ally.Memes.GetValueOrDefault(pc.MemeId), 0.5f);
                }
                break;
            case PivotChoice.Resent:
                int target = pc.GrudgeTargetId >= 0 ? pc.GrudgeTargetId : band.LeaderId;
                if (target >= 0 && target != a.Id) a.Rel(target).Grudge = true;
                break;
            case PivotChoice.Leave:
                Schism(a, band, allies, pc.Kind, pc.What, pc.Tech, pc.MemeId);
                break;
        }
        RecordPivot(a, band, choice, pc.Kind, pc.What);
    }

    // ------------------------------------------------------------------ first contact between peoples

    /// <summary>Leader of <paramref name="a"/> meets a foreign people for the first time.</summary>
    void BeginMeeting(Band a, Band b, string instinct)
    {
        var leader = Living.GetValueOrDefault(a.LeaderId);
        if (!DeferDecisions || leader == null)
        {
            CarryOutMeeting(a, b, instinct);
            return;
        }
        a.Relations[b.Id] = a.RelationTo(b.Id);   // they have now seen each other
        b.Relations[a.Id] = b.RelationTo(a.Id);
        var options = new[] { "welcome", "wary", "raid" };
        var d = new Decision
        {
            Id = _nextDecision++, Day = Day, Deadline = Day + DecisionWindowDays, AgentId = leader.Id, BandId = a.Id, OtherBandId = b.Id,
            Kind = DecisionKind.Meeting, Options = options, Descriptions = options.Select(o => DecisionText.Meeting(o, b.Name)).ToArray(),
            InstinctFavourite = instinct, MeetingInstinct = instinct,
        };
        Decisions.Add(d);
        var mood = Mood(a);
        Chronicle.Moment(new LlmMoment(Day, "decision", leader.Id, a.Id, DecisionFacts(d, new()
        {
            ["trigger"] = "first_contact", ["other_band"] = b.Name, ["other_culture"] = Cultures[b.CultureId].Name,
            ["other_size"] = b.Members.Count.ToString(), ["own_size"] = a.Members.Count.ToString(),
            ["hungry"] = (mood.hunger > 0.3f).ToString(),
        })));
    }

    void CarryOutMeeting(Band a, Band b, string choice)
    {
        switch (choice)
        {
            case "raid": Raid(a, b, true); break;
            case "welcome": Feast(a, b, true); break;
            default:
                a.Relations[b.Id] = a.RelationTo(b.Id) + 0.02f;
                b.Relations[a.Id] = b.RelationTo(a.Id) + 0.02f;
                MeetStrangers(a, 0.3f); MeetStrangers(b, 0.3f);
                break;
        }
    }
}
