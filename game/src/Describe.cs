using System.Text;
using Evolysia.Sim;
using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Evolysia.Sim.Society;
using Activity = Evolysia.Sim.Core.Activity;
using Godot;

namespace Evolysia.Game;

/// <summary>Inspector and chronicle text (BBCode). Links: [url=agent:ID] and [url=band:ID].</summary>
public static class Describe
{
    static readonly string Muted = Palette.Hex(Palette.Parchment);

    public static Color CategoryColor(EventCategory c) => c switch
    {
        EventCategory.Danger => Palette.Terracotta,
        EventCategory.Loss => new Color("9a8f86"),
        EventCategory.Discovery => Palette.Ochre,
        EventCategory.Spiritual => Palette.Divine,
        EventCategory.Conflict => new Color("d06a4a"),
        EventCategory.Trade => Palette.River,
        EventCategory.Birth => Palette.Moss,
        _ => Palette.Parchment,
    };

    public static string Title(string s) => s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..].Replace('_', ' ');

    /// <summary>A thin bar drawn with background-coloured small spaces (works with any font).</summary>
    public static string Bar(float v, Color color, int width = 20)
    {
        int n = Math.Clamp((int)MathF.Round(v * width), 0, width);
        string on = new string((char)160, n * 2), off = new string((char)160, (width - n) * 2);
        return $"[font_size=7][bgcolor={Palette.Hex(color)}]{on}[/bgcolor][bgcolor={Palette.Hex(Palette.Raised)}]{off}[/bgcolor][/font_size]";
    }

    /// <summary>Rows of label | bar | note, aligned in a table.</summary>
    public static string Rows(IEnumerable<(string label, string bar, string note)> rows) =>
        "[table=3]" + string.Join("", rows.Select(r => $"[cell]{r.label}  [/cell][cell]{r.bar}[/cell][cell] {r.note}[/cell]")) + "[/table]\n";

    public static string Role(Activity a) => a switch
    {
        Activity.Forage => "gatherer", Activity.Hunt => "hunter", Activity.Fish => "fisher", Activity.Farm => "farmer",
        Activity.Herd => "herder", Activity.Craft => "crafter", Activity.Build => "builder", Activity.Ritual => "shaman",
        Activity.Teach => "teacher", Activity.Experiment => "tinkerer", Activity.Explore => "scout", Activity.CareChild => "caregiver",
        Activity.Socialize => "storyteller", Activity.Court => "suitor", Activity.Mourn => "mourner", _ => "wanderer",
    };

    public static string AgentLink(Simulation sim, int id)
    {
        var a = sim.Find(id);
        if (a == null) return "?";
        return a.Alive ? $"[url=agent:{id}]{a.Name}[/url]" : $"[color={Muted}][url=agent:{id}]{a.Name}[/url] †[/color]";
    }

    public static string BandLink(Simulation sim, int id) =>
        sim.Bands.TryGetValue(id, out var b) ? $"[url=band:{id}]the {b.Name}[/url]" : "[color=#9a8f86](gone)[/color]";

    public static string Band(Simulation sim, Band b)
    {
        var sb = new StringBuilder();
        var culture = sim.Cultures[b.CultureId];
        var members = b.Members.Select(id => sim.Living.GetValueOrDefault(id)).Where(a => a != null).Select(a => a!).ToList();
        int kids = members.Count(a => a.IsChild(sim.Day));
        sb.Append($"[font_size=20][b]The {b.Name}[/b][/font_size]\n");
        sb.Append($"[color={Palette.Hex(Palette.Culture(culture.Hue))}]■[/color] {culture.Name} people · {(b.Sedentary ? "village" : "wandering band")}\n");
        sb.Append($"{members.Count} people ({kids} children) · led by {(b.LeaderId >= 0 ? AgentLink(sim, b.LeaderId) : "no one")}\n");
        sb.Append($"Founded year {b.FoundedDay / SimConfig.DaysPerYear}{(b.ParentBandId >= 0 ? $", from {BandLink(sim, b.ParentBandId)}" : "")}\n\n");

        float food = b.FoodPool + members.Sum(a => a.Food);
        sb.Append(Rows(new[]
        {
            ("Food stores", Bar(Math.Clamp(food / MathF.Max(1, members.Count * 6f), 0f, 1f), Palette.Moss), $"{food / MathF.Max(1, members.Count):0.0} days each"),
            ("Shelter", Bar(b.Shelter, Palette.Ochre), ""),
            ("Hardship", Bar(Math.Clamp(b.Stress * 3f, 0f, 1f), Palette.Terracotta), ""),
        }));
        if (b.PlagueUntil > sim.Day) sb.Append("[color=#b8c46a]Sickness is spreading.[/color]\n");
        int seaRange = sim.SeaRange(b);
        if (seaRange > 0)
            sb.Append($"[color={Palette.Hex(Palette.River.Lightened(0.3f))}]Seafarers: up to {seaRange} days of open sea" +
                      (b.KnownShores.Count > 0 ? $"; they know of {b.KnownShores.Count} shore{(b.KnownShores.Count == 1 ? "" : "s")} across the water" : "") + "[/color]\n");
        if (sim.PlayerGodOf(b.CultureId) is { } god)
            sb.Append($"[color={Palette.Hex(Palette.Divine)}]Faith in you as {god.Name}: {sim.FaithInPlayer(b) * 100:0}%[/color]\n");
        else if (b.Awe > 0.2f)
            sb.Append($"[color={Palette.Hex(Palette.Divine)}]They have begun to wonder who is watching ({b.Awe:0.0} awe).[/color]\n");

        ulong known = 0; foreach (var a in members) known |= a.Knows;
        var techs = Techs.All.Where(t => Techs.Has(known, t.Id)).Select(t => Title(Techs.Name(t.Id)));
        sb.Append($"\n[b]Know-how[/b]\n{string.Join(", ", techs)}\n");

        var beliefs = b.MemePrevalence.Where(kv => kv.Value >= 0.1f).OrderByDescending(kv => kv.Value)
            .Select(kv => $"{Title(sim.Memes[kv.Key].Name)} [color={Muted}]({kv.Value * 100:0}%)[/color]").ToList();
        if (beliefs.Count > 0) sb.Append($"\n[b]Beliefs and customs[/b]\n{string.Join("\n", beliefs)}\n");

        var rel = b.Relations.Where(kv => sim.Bands.ContainsKey(kv.Key)).OrderBy(kv => kv.Value).ToList();
        if (rel.Count > 0)
        {
            sb.Append("\n[b]Neighbours[/b]\n");
            foreach (var (other, v) in rel.Take(2).Concat(rel.TakeLast(2)).Distinct())
                sb.Append($"{BandLink(sim, other)}: {(v > 0.3f ? "friends" : v < -0.3f ? "enemies" : "wary")}\n");
        }

        sb.Append($"\n[b]People[/b]\n");
        foreach (var a in members.OrderByDescending(a => a.Prestige).Take(60))
            sb.Append($"{AgentLink(sim, a.Id)} [color={Muted}]{(a.Sex == Sex.Female ? "♀" : "♂")}{a.Age(sim.Day):0}{(a.IsChild(sim.Day) ? "" : " " + Role(a.MainActivity()))}[/color]\n");
        return sb.ToString();
    }

    public static string Person(Simulation sim, Agent a, IReadOnlyList<Narration.Narration>? voices = null)
    {
        var sb = new StringBuilder();
        string inner = voices is { Count: > 0 }
            ? "\n[b]Inner voice[/b]\n" + string.Join("\n\n", voices.TakeLast(3).Reverse().Select(n =>
                $"[color={Muted}]Y{n.Moment.Day / SimConfig.DaysPerYear}, {VoiceTitle(n)}[/color]\n{VoiceBody(n)}")) + "\n"
            : "";
        int day = a.Alive ? sim.Day : a.DeathDay;
        sb.Append($"[font_size=20][b]{a.Name}[/b][/font_size]  [color={Muted}]{(a.Sex == Sex.Female ? "♀" : "♂")} {a.Age(day):0} years · gen {a.Generation}[/color]\n");
        if (!a.Alive) sb.Append($"[color=#9a8f86]Died year {a.DeathDay / SimConfig.DaysPerYear} ({Ids.Snake(a.DeathCause).Replace('_', ' ')})[/color]\n");
        else sb.Append($"{Title(a.IsChild(day) ? "child" : Role(a.MainActivity()))} of {BandLink(sim, a.BandId)} · today: {Ids.Snake(a.Today).Replace('_', ' ')}\n");
        if (a.Genome.Quirks != Quirk.None) sb.Append($"[color={Palette.Hex(Palette.Divine)}]{string.Join(", ", a.Genome.Quirks.ToString().Split(", ").Select(q => Title(Ids.Snake(Enum.Parse<Quirk>(q))).ToLower()))}[/color]\n");

        var family = new List<string>();
        if (a.PartnerId >= 0) family.Add("partner " + AgentLink(sim, a.PartnerId));
        if (a.MotherId >= 0) family.Add("mother " + AgentLink(sim, a.MotherId));
        if (a.FatherId >= 0) family.Add("father " + AgentLink(sim, a.FatherId));
        if (family.Count > 0) sb.Append(string.Join(" · ", family) + "\n");
        if (a.Children.Count > 0) sb.Append("children " + string.Join(", ", a.Children.Select(c => AgentLink(sim, c))) + "\n");
        sb.Append(inner);

        sb.Append("\n[b]Personality[/b]  [color=" + Muted + "](bar = now, number = change from birth)[/color]\n");
        sb.Append(Rows(Enum.GetValues<Trait>().Select(t =>
        {
            float off = a.T(t) - a.Genome.Genes[(int)t];
            string delta = MathF.Abs(off) < 0.03f ? "" : $"[color={(off > 0 ? "#8fc27a" : "#d06a4a")}]{off:+0.00;-0.00}[/color]";
            return (TraitName(t), Bar(a.T(t), Palette.Ochre), delta);
        })));

        if (a.Alive)
        {
            var feelings = Enum.GetValues<Emotion>().Where(e => e != Emotion.Calm && a.Emo(e) > 0.08f).OrderByDescending(a.Emo).Take(4).ToList();
            sb.Append("\n[b]Feeling[/b]\n");
            sb.Append(feelings.Count == 0 ? Rows(new[] { ("calm", Bar(a.Emo(Emotion.Calm), Palette.Moss), "") })
                                          : Rows(feelings.Select(e => (Ids.Snake(e), Bar(a.Emo(e), Palette.Divine), ""))));

            sb.Append("\n[b]Needs[/b]\n");
            sb.Append(Rows(Enum.GetValues<Need>().Select(n => (Ids.Snake(n), Bar(a.NeedOf(n), Palette.Terracotta), ""))
                .Append(("health", Bar(a.Health, Palette.Moss), (a.Sick ? "[color=#b8c46a]sick[/color] " : "") + (a.Pregnant ? "expecting" : "")))));
        }

        var fears = Enumerable.Range(0, Ids.StimulusCount).Where(s => a.Fear[s] > 0.12f).OrderByDescending(s => a.Fear[s]).Take(4).ToList();
        var mastered = Enumerable.Range(0, Ids.StimulusCount).Where(s => a.Mastered[s] > 0.2f).ToList();
        if (fears.Count + mastered.Count > 0)
        {
            sb.Append("\n[b]Fears[/b]\n");
            if (fears.Count > 0) sb.Append(Rows(fears.Select(s => (Ids.Snake((Stimulus)s).Replace('_', ' '), Bar(a.Fear[s], Palette.Terracotta), ""))));
            foreach (var s in mastered) sb.Append($"[color=#8fc27a]has faced down the {Ids.Snake((Stimulus)s).Replace('_', ' ')}[/color]\n");
        }

        var skills = Enumerable.Range(0, Ids.DomainCount).OrderByDescending(d => a.Efficacy[d]).Take(3);
        sb.Append("\n[b]Confident at[/b]  " + string.Join(", ", skills.Select(d => $"{Ids.Snake((Domain)d)} {a.Efficacy[d] * 100:0}%")) + "\n");
        var given = Enumerable.Range(0, Ids.DomainCount).Where(d => a.IsSuppressed((Domain)d)).ToList();
        if (given.Count > 0) sb.Append($"[color=#d06a4a]Gave up on {string.Join(", ", given.Select(d => Ids.Snake((Domain)d)))} after being shamed[/color]\n");

        var techs = Techs.All.Where(t => Techs.Has(a.Knows, t.Id)).Select(t => Techs.Name(t.Id).Replace('_', ' '));
        sb.Append($"Knows {string.Join(", ", techs)}\n");
        var beliefs = a.Memes.Where(kv => kv.Value > 0.3f).OrderByDescending(kv => kv.Value).Take(5)
            .Select(kv => $"{Title(sim.Memes[kv.Key].Name)} [color={Muted}]{kv.Value * 100:0}%[/color]").ToList();
        if (beliefs.Count > 0) sb.Append("\n[b]Believes in[/b]\n" + string.Join("\n", beliefs) + "\n");

        var friends = a.Relations.Where(kv => sim.Living.ContainsKey(kv.Key) && kv.Key != a.PartnerId)
            .OrderByDescending(kv => MathF.Abs(kv.Value.Affinity) + (kv.Value.Grudge ? 1f : 0f)).Take(5).ToList();
        if (friends.Count > 0)
        {
            sb.Append("\n[b]Relationships[/b]\n");
            foreach (var (id, r) in friends)
                sb.Append($"{AgentLink(sim, id)} [color={(r.Affinity >= 0 ? "#8fc27a" : "#d06a4a")}]{(r.Grudge ? "grudge" : r.Affinity > 0.4f ? "close" : r.Affinity > 0.1f ? "friendly" : r.Affinity < -0.3f ? "hostile" : "cool")}[/color]\n");
        }

        if (a.Memory.Count > 0)
        {
            sb.Append("\n[b]Remembers[/b]\n");
            foreach (var m in a.Memory.OrderByDescending(m => m.Day).Take(8))
                sb.Append($"[color={Muted}]Y{m.Day / SimConfig.DaysPerYear}[/color] {MemoryText(sim, m)}\n");
        }
        return sb.ToString();
    }

    static string TraitName(Trait t) => t switch
    {
        Trait.NoveltySeeking => "curious",
        Trait.HarmAvoidance => "fearful",
        Trait.RewardDependence => "needs approval",
        Trait.Persistence => "stubborn",
        Trait.SelfDirectedness => "self-reliant",
        Trait.Cooperativeness => "cooperative",
        _ => "spiritual",
    };

    static string MemoryText(Simulation sim, LifeEvent m)
    {
        string other = m.OtherId >= 0 ? AgentLink(sim, m.OtherId) : m.Detail;
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
            _ => m.Detail.Length > 0 ? $"{m.Detail.Replace('_', ' ')}" : m.Kind.Replace('_', ' '),
        };
    }

    // ---- voices (LLM narration) -------------------------------------------------------------

    static string Fact(Narration.Narration n, string key) => n.Moment.Facts.GetValueOrDefault(key, "").Replace('_', ' ');

    public static string VoiceTitle(Narration.Narration n) => n.Moment.Kind switch
    {
        "pivot" when Fact(n, "trigger") == "courtship" => $"turned down in love · chooses to {Fact(n, "choice")}",
        "pivot" => $"their {Fact(n, "subject")} is scorned · chooses to {Fact(n, "choice")}",
        "myth" => $"tells of {Fact(n, "deity")}",
        "invention" => $"discovers {Fact(n, "tech")}",
        "schism" => $"leaves to found the {Fact(n, "new_band")}",
        "fear_overcome" => $"faces down the {Fact(n, "stimulus")}",
        "notable_death" => "is laid to rest",
        "killing" => $"kills {Fact(n, "victim")}",
        "revelation" => $"names you {Fact(n, "god")}",
        "dream" => "dreams of you",
        "prayer" => $"prays to you for {Fact(n, "need")}",
        "prayer_answered" => "gives thanks",
        "voyage" => Fact(n, "first_landing") == "True" ? "lands where no one has walked" : "crosses the sea",
        "decision" => $"chooses: {ChosenText(n).ToLowerInvariant()}",
        _ => n.Moment.Kind,
    };

    static string ChosenText(Narration.Narration n)
    {
        var ids = n.Moment.Facts.GetValueOrDefault("options", "").Split(',');
        var text = n.Moment.Facts.GetValueOrDefault("descriptions", "").Split('|');
        int i = Array.IndexOf(ids, n.Get("choice"));
        return i >= 0 && i < text.Length ? text[i] : n.Get("choice");
    }

    /// <summary>Body text of a narration: inner voice in italics, speech in quotes, beliefs highlighted.</summary>
    public static string VoiceBody(Narration.Narration n)
    {
        var parts = new List<string>();
        void Line(string key, Func<string, string> fmt) { var v = n.Get(key); if (v.Length > 0) parts.Add(fmt(v)); }
        string parchment = Palette.Hex(Palette.Parchment), divine = Palette.Hex(Palette.Divine);
        Line("dream", v => $"[color={parchment}][i]{v}[/i][/color]");
        Line("thought", v => $"[color={parchment}][i]{v}[/i][/color]");
        Line("says", v => $"“{v}”");
        Line("speech", v => $"“{v}”");
        Line("myth", v => $"“{v}”");
        Line("ritual", v => $"[color={divine}]Ritual: {v}[/color]");
        Line("crowd", v => $"[color={parchment}]A voice from the crowd: “{v}”[/color]");
        Line("lasting_belief", v => $"[color={divine}]Now believes: {v}[/color]");
        Line("name_meaning", v => $"[color={parchment}]The {Fact(n, "new_band")}: {v}[/color]");
        Line("epitaph", v => $"[color={parchment}][i]“{v}”[/i][/color]");
        Line("proclamation", v => $"“{v}”");
        Line("commandment", v => $"[color={divine}]Your will, as they hear it: {v}[/color]");
        Line("prayer", v => $"[color={divine}]“{v}”[/color]");
        Line("offering", v => $"[color={parchment}]Offering: {v}[/color]");
        Line("thanks", v => $"“{v}”");
        return string.Join("\n", parts);
    }

    public static string Voices(Simulation sim, IReadOnlyList<Narration.Narration> recent, int max = 6)
    {
        if (recent.Count == 0) return $"[color={Muted}]Waiting for something worth telling…[/color]";
        var sb = new StringBuilder();
        for (int i = recent.Count - 1; i >= Math.Max(0, recent.Count - max); i--)
        {
            var n = recent[i];
            sb.Append($"[b]{AgentLink(sim, n.Moment.AgentId)}[/b] [color={Muted}]{VoiceTitle(n)} · Y{n.Moment.Day / SimConfig.DaysPerYear}[/color]\n");
            sb.Append(VoiceBody(n)).Append("\n\n");
        }
        return sb.ToString();
    }

    public static string Prayers(Simulation sim)
    {
        var open = sim.OpenPrayers.Where(p => sim.Bands.ContainsKey(p.BandId)).OrderBy(p => p.Day).Take(5).ToList();
        if (open.Count == 0) return "";
        var sb = new StringBuilder($"[color={Palette.Hex(Palette.Ochre)}]PRAYERS TO YOU[/color]\n");
        foreach (var p in open)
        {
            int left = 120 - (sim.Day - p.Day);
            sb.Append($"[color={Palette.Hex(Palette.Divine)}]✦[/color] {AgentLink(sim, p.AgentId)} of {BandLink(sim, p.BandId)} " +
                      $"asks for [b]{Evolysia.Sim.Powers.Verb(p.Need)}[/b] [color={Muted}]({left} days left)[/color]\n");
        }
        return sb.ToString();
    }

    public static string Feed(Simulation sim, int max = 80)
    {
        var sb = new StringBuilder();
        foreach (var e in sim.Chronicle.Events.Where(e => e.Salience >= 0.35f).TakeLast(max).Reverse())
        {
            string text = e.Text;
            string body = e.BandId >= 0 && sim.Bands.ContainsKey(e.BandId) ? $"[url=band:{e.BandId}]{text}[/url]"
                        : e.AgentId >= 0 ? $"[url=agent:{e.AgentId}]{text}[/url]" : text;
            sb.Append($"[color={Palette.Hex(CategoryColor(e.Category))}]●[/color] [color={Muted}]Y{e.Day / SimConfig.DaysPerYear}[/color] {body}\n");
        }
        return sb.ToString();
    }
}
