using Evolysia.Sim;
using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Evolysia.Sim.Society;

namespace Evolysia.Narration;

public static class CharacterSheet
{
    static readonly Dictionary<Trait, (string high, string low)> Phrases = new()
    {
        [Trait.NoveltySeeking] = ("restless and drawn to anything new or risky", "prefers the familiar and distrusts anything new"),
        [Trait.HarmAvoidance] = ("fearful and quick to sense danger", "fearless and rarely worried about danger"),
        [Trait.RewardDependence] = ("craves approval and is deeply hurt by scorn", "indifferent to praise or scorn"),
        [Trait.Persistence] = ("stubborn, never gives up once set on something", "gives up easily when things go badly"),
        [Trait.SelfDirectedness] = ("self-reliant, trusts their own judgment over the group's", "unsure of themself, lets others decide"),
        [Trait.Cooperativeness] = ("generous, puts the group first", "selfish and distrustful, puts themself first"),
        [Trait.SelfTranscendence] = ("spiritual, sees spirits and omens everywhere", "practical, scoffs at spirits and omens"),
    };

    public static string Personality(Agent a)
    {
        var parts = Enum.GetValues<Trait>()
            .Select(t => (t, c: (a.T(t) - 0.5f) * 2f))
            .Where(x => MathF.Abs(x.c) >= 0.35f)
            .OrderByDescending(x => MathF.Abs(x.c))
            .Select(x => (MathF.Abs(x.c) >= 0.7f ? "extremely " : "") + (x.c > 0 ? Phrases[x.t].high : Phrases[x.t].low))
            .ToList();
        return parts.Count == 0 ? "fairly ordinary in temperament" : string.Join("; ", parts);
    }

    public static string Role(Agent a, int day) => a.IsChild(day) ? "child" : a.MainActivity() switch
    {
        Activity.Forage => "gatherer", Activity.Hunt => "hunter", Activity.Fish => "fisher", Activity.Farm => "farmer",
        Activity.Herd => "herder", Activity.Craft => "crafter", Activity.Build => "builder", Activity.Ritual => "shaman",
        Activity.Teach => "teacher", Activity.Experiment => "tinkerer", Activity.Explore => "scout",
        Activity.CareChild => "caregiver", _ => "storyteller",
    };

    public static string Build(Simulation sim, Agent a)
    {
        int day = a.Alive ? sim.Day : a.DeathDay;
        var band = sim.Bands.GetValueOrDefault(a.BandId);
        string culture = sim.Cultures.TryGetValue(a.CultureId, out var c) ? c.Name : "unknown";
        var lines = new List<string>
        {
            $"{a.Name}, {(a.Sex == Sex.Female ? "woman" : "man")} of {a.Age(day):0}, {Role(a, day)} of the {band?.Name ?? "wanderers"} ({culture} people).",
            $"Personality: {Personality(a)}.",
        };

        var quirks = Genome.AllQuirks.Where(a.Genome.Has).Select(q => Ids.Snake(q).Replace('_', ' ')).ToList();
        if (quirks.Count > 0) lines.Add($"Peculiar: {string.Join(", ", quirks)}.");

        var feelings = Enum.GetValues<Emotion>().Where(e => e != Emotion.Calm && a.Emo(e) > 0.15f)
            .OrderByDescending(a.Emo).Take(3).Select(e => $"{Ids.Snake(e)} ({a.Emo(e):0.0})").ToList();
        lines.Add($"Feeling right now: {(feelings.Count == 0 ? "calm" : string.Join(", ", feelings))}.");

        var fears = Enumerable.Range(0, Ids.StimulusCount).Where(s => a.Fear[s] > 0.4f).OrderByDescending(s => a.Fear[s])
            .Take(2).Select(s => Ids.Snake((Stimulus)s).Replace('_', ' ')).ToList();
        if (fears.Count > 0) lines.Add($"Afraid of: {string.Join(", ", fears)}.");

        if (a.PartnerId >= 0 && sim.Find(a.PartnerId) is { } p) lines.Add($"Partner: {p.Name}{(p.Alive ? "" : " (dead)")}.");
        int kids = a.Children.Count(id => sim.Living.ContainsKey(id));
        if (kids > 0) lines.Add($"Living children: {kids}.");

        var beliefs = a.Memes.Where(kv => kv.Value > 0.4f && sim.Memes.ContainsKey(kv.Key)).OrderByDescending(kv => kv.Value).Take(2)
            .Select(kv => sim.Memes[kv.Key]).Select(m => m.Kind switch
            {
                MemeKind.SpiritBelief => $"worships {m.Name}",
                MemeKind.Taboo => $"keeps {m.Name}",
                MemeKind.Ritual => $"makes {m.Name}",
                _ => $"values {m.Name}",
            }).ToList();
        if (beliefs.Count > 0) lines.Add($"Faith and custom: {string.Join("; ", beliefs)}.");

        var memories = a.Memory.OrderByDescending(m => m.Importance).Take(3)
            .Select(m => $"year {m.Day / SimConfig.DaysPerYear}: {MemoryText(sim, m)}").ToList();
        if (memories.Count > 0) lines.Add($"Memories that shaped them: {string.Join("; ", memories)}.");
        return string.Join("\n", lines);
    }

    static string MemoryText(Simulation sim, LifeEvent m)
    {
        string other = m.OtherId >= 0 && sim.Find(m.OtherId) is { } o ? o.Name : m.Detail;
        return m.Kind switch
        {
            "bereaved" => $"lost {other}",
            "child_born" => $"{other} was born",
            "quarrel" => $"quarrelled with {other}",
            "assaulted" => $"was attacked by {other}",
            "rejected" => $"was turned down by {other}",
            "mauled" => $"was mauled by a {m.Detail.Replace('_', ' ')}",
            "raided" => $"survived a raid by the {m.Detail}",
            _ when m.Kind.EndsWith("_rejected") => $"their {m.Detail} was scorned",
            _ => (m.Detail.Length > 0 ? m.Detail : m.Kind).Replace('_', ' '),
        };
    }
}
