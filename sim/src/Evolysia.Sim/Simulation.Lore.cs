using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Evolysia.Sim.Economy;
using Evolysia.Sim.Persistence;
using Evolysia.Sim.Society;

namespace Evolysia.Sim;

/// <summary>One thing a band has learned: from tradition, its own work, what it built or made, a technique, a clue or a dead end.</summary>
public sealed class LoreEntry
{
    public string Key = "", Text = "";
    public int Day, Count;
    public float Confidence;
}

/// <summary>What a band has seen its people's work bring, until it is well known.</summary>
public sealed class WorkLedger
{
    public int Days;
    public float Energy, Social, Hunger;
    public readonly Dictionary<ushort, float> Gains = new(), Uses = new();
}

/// <summary>An experiment the band's minds chose: an action on things they can work with, in their own words.</summary>
public sealed class ExperimentJob
{
    public int Id, Started;
    public Method Method;
    public string[] Subjects = Array.Empty<string>();
    public string Idea = "";
    public float Effort;
    public readonly List<int> Contributors = new();
}

/// <summary>A band's knowledge of how the world answers them, and the experiments under way.</summary>
public sealed class BandLore
{
    public readonly Dictionary<string, LoreEntry> Entries = new();
    public readonly Dictionary<string, WorkLedger> Ledgers = new();
    public readonly List<ExperimentJob> Experiments = new();
    /// <summary>How often the right things were worked the wrong way, by way in.</summary>
    public readonly Dictionary<string, int> WrongWays = new();
}

/// <summary>
/// Discovery and lore. With minds in charge, nobody is handed a manual: a band knows only its
/// traditions and what it has seen for itself (what a day's work actually brought, what a building
/// did once it stood, what a recipe turned out). New techniques are found only by experiments the
/// minds propose (an action on things they can perceive), judged against hidden ways in
/// (<see cref="Discovery"/>); results, near misses and dead ends come back as lore.
/// </summary>
public sealed partial class Simulation
{
    [SaveOptional] public readonly Dictionary<int, BandLore> Lore = new();
    [SaveOptional] int _nextExperiment;

    const int LoreCap = 160, WellKnownDays = 6, MaxExperiments = 3;

    /// <summary>The band's lore, created on first need (sequential code only): a new band inherits its parent's.</summary>
    public BandLore LoreOf(Band band)
    {
        if (Lore.TryGetValue(band.Id, out var lore)) return lore;
        lore = Lore[band.Id] = new BandLore();
        if (band.ParentBandId >= 0 && Lore.TryGetValue(band.ParentBandId, out var parent))
            foreach (var (key, e) in parent.Entries)
                lore.Entries[key] = new LoreEntry { Key = key, Text = e.Text, Day = e.Day, Count = e.Count, Confidence = e.Confidence };
        else
        {
            // The founders' own know-how, each tied to the work it is about.
            Learn(lore, "tradition:forage", "Forage: our people have always found roots, nuts and fruit to eat this way.", 0.6f);
            Learn(lore, "tradition:hunt", "Hunt: our hunters chase game for meat and hides.", 0.6f);
            Learn(lore, "tradition:rest", "Rest: it restores the tired and the sick.", 0.8f);
            Learn(lore, "tradition:socialize", "Socialize: time spent together keeps loneliness away.", 0.6f);
        }
        SyncTechLore(band, lore);
        return lore;
    }

    /// <summary>Every band has its lore before the parallel day (which only reads the outer table).</summary>
    void EnsureLore()
    {
        if (!Config.RequireModel) return;
        foreach (var band in Bands.Values) if (!band.Disbanded) LoreOf(band);
    }

    static void Learn(BandLore lore, string key, string text, float confidence, int day = 0)
    {
        if (lore.Entries.TryGetValue(key, out var e)) { e.Text = text; e.Count++; e.Confidence = MathF.Max(e.Confidence, confidence); e.Day = day; return; }
        lore.Entries[key] = new LoreEntry { Key = key, Text = text, Day = day, Count = 1, Confidence = confidence };
        if (lore.Entries.Count <= LoreCap) return;
        // Forget the least of it: old clues and dead ends first, then the least certain.
        var forget = lore.Entries.Values.OrderBy(x => x.Key.StartsWith("clue:") || x.Key.StartsWith("failed:") ? 0 : 1)
            .ThenBy(x => x.Confidence).ThenBy(x => x.Day).First();
        lore.Entries.Remove(forget.Key);
    }

    /// <summary>Techniques the band knows (found, taught or carried in) appear in its lore with what they bring to mind.</summary>
    void SyncTechLore(Band band, BandLore lore)
    {
        foreach (var t in BandKnowledge(band).Ids())
        {
            string key = "tech:" + Ids.Snake(t);
            if (lore.Entries.ContainsKey(key)) continue;
            Learn(lore, key, $"We know how to {Discovery.Key(t).Use}.{Ideas(t)}", 1f, Day);
        }
    }

    /// <summary>What a technique brings to mind: things to build, make and do (names only; what they do is found by trying).</summary>
    static string Ideas(TechId t)
    {
        var ideas = Economy.Structures.All.Where(s => s.Techs.Contains(t)).Select(s => s.Id)
            .Concat(Crafts.Items.Where(i => i.Techs.Contains(t)).Select(i => i.Id))
            .Concat(Crafts.Recipes.Where(r => r.Tech == t).Select(r => r.Id))
            .Distinct().Take(8).Select(id => id.Replace('_', ' ')).ToArray();
        return ideas.Length == 0 ? "" : $" It brings to mind: {string.Join(", ", ideas)}.";
    }

    // ------------------------------------------------------------------ seeing what work brings

    /// <summary>Before and after one person's day of work, while that work is not yet well known.</summary>
    sealed class WorkWatch
    {
        public readonly float[] Before = new float[Goods.Count];
        public float Energy, Social, Hunger;
        public bool Active;
    }

    [ThreadStatic] static WorkWatch? t_watch;
    [ThreadStatic] static Agent[]? t_single;

    static Agent[] Single(Agent a) { var one = t_single ??= new Agent[1]; one[0] = a; return one; }

    WorkWatch? WatchWork(Band band, IEnumerable<Agent> workers, Activity work)
    {
        if (!Config.RequireModel || work is Activity.Experiment or Activity.Court || !Lore.TryGetValue(band.Id, out var lore)) return null;   // (those finish after the day)
        if (lore.Ledgers.TryGetValue("work:" + Ids.Snake(work), out var ledger) && ledger.Days >= WellKnownDays) return null;
        var watch = t_watch ??= new WorkWatch();
        Holdings(band, workers, watch.Before);
        watch.Energy = watch.Social = watch.Hunger = 0f;
        foreach (var a in workers) { watch.Energy += a.NeedOf(Need.Energy); watch.Social += a.NeedOf(Need.Social); watch.Hunger += a.NeedOf(Need.Hunger); }
        watch.Active = true;
        return watch;
    }

    static void Holdings(Band band, IEnumerable<Agent> workers, float[] into)
    {
        for (ushort g = 0; g < into.Length; g++) into[g] = band.Stores[g];
        foreach (var a in workers) for (ushort g = 0; g < into.Length; g++) into[g] += a.Pack[g];
    }

    /// <summary>After the work: what came in, what was used up, and how it felt, folded into the band's lore.</summary>
    void SawWork(Band band, IReadOnlyCollection<Agent> workers, Activity work, WorkWatch watch)
    {
        watch.Active = false;
        var lore = Lore[band.Id];
        string key = "work:" + Ids.Snake(work);
        if (!lore.Ledgers.TryGetValue(key, out var ledger)) lore.Ledgers[key] = ledger = new WorkLedger();
        Span<float> after = stackalloc float[Goods.Count];
        for (ushort g = 0; g < after.Length; g++) after[g] = band.Stores[g];
        foreach (var a in workers) for (ushort g = 0; g < after.Length; g++) after[g] += a.Pack[g];
        for (ushort g = 0; g < after.Length; g++)
        {
            float d = after[g] - watch.Before[g];
            if (d > 0.01f) ledger.Gains[g] = ledger.Gains.GetValueOrDefault(g) + d;
            else if (d < -0.01f) ledger.Uses[g] = ledger.Uses.GetValueOrDefault(g) - d;
        }
        float energy = 0f, social = 0f, hunger = 0f;
        foreach (var a in workers) { energy += a.NeedOf(Need.Energy); social += a.NeedOf(Need.Social); hunger += a.NeedOf(Need.Hunger); }
        ledger.Energy += energy - watch.Energy; ledger.Social += social - watch.Social; ledger.Hunger += hunger - watch.Hunger;
        ledger.Days += workers.Count;
        Learn(lore, key, WorkText(work, ledger), MathF.Min(1f, ledger.Days / (float)WellKnownDays), Day);
    }

    static string WorkText(Activity work, WorkLedger l)
    {
        static string Amounts(Dictionary<ushort, float> d, float days) => string.Join(", ", d.OrderByDescending(x => x.Value).Take(4)
            .Select(x => $"{x.Value / days:0.#} {Goods.All[x.Key].Id.Replace('_', ' ')}"));
        float days = MathF.Max(1, l.Days);
        var parts = new List<string>();
        if (l.Gains.Count > 0)
        {
            // Whether it can be eaten is the first thing anyone notices.
            float food = l.Gains.Sum(x => x.Value * Goods.All[x.Key].Food) / days;
            parts.Add("a day of it brought about " + Amounts(l.Gains, days) + (food >= 0.05f ? $" (enough to feed about {food:0.#} for a day)" : " (none of it food)"));
        }
        if (l.Uses.Count > 0) parts.Add("used up about " + Amounts(l.Uses, days));
        if (l.Energy / days < -0.1f) parts.Add("it restored strength");
        else if (l.Energy / days > 0.12f) parts.Add("it was tiring");
        if (l.Social / days < -0.1f) parts.Add("it eased loneliness");
        if (parts.Count == 0) parts.Add("it brought nothing we could hold");
        string seen = l.Days >= WellKnownDays ? "" : $" (seen {l.Days} time{(l.Days == 1 ? "" : "s")})";
        string name = ActivityName(work).Replace('_', ' ');
        return $"{char.ToUpper(name[0]) + name[1..]}: {string.Join("; ", parts)}{seen}.";
    }

    /// <summary>The first time a band raises a kind of building, it sees what it is good for.</summary>
    void SawBuilt(Structure s)
    {
        if (!Config.RequireModel || !Lore.TryGetValue(s.BandId, out var lore)) return;
        string key = "built:" + s.Kind.Id;
        if (lore.Entries.ContainsKey(key)) return;
        var k = s.Kind;
        var does = new List<string>();
        if (k.Housing > 0) does.Add($"it sleeps about {k.Housing}");
        if (k.Warmth > 0f) does.Add("it keeps out the cold");
        if (k.Capacity > 0f) does.Add($"it holds about {k.Capacity:0} of goods and keeps them longer");
        does.Add(k.Role switch
        {
            StructureRole.Field => "seeds sown and tended in it grow into a harvest",
            StructureRole.Pen => "tame animals stay and breed in it",
            StructureRole.Station => "work done at it can make: " + string.Join(", ", Crafts.Recipes.Where(r => r.Station == k.Id).Select(r => r.Id)
                .Concat(Crafts.Items.Where(i => i.Station == k.Id).Select(i => i.Id)).Take(6).Select(x => x.Replace('_', ' ')).DefaultIfEmpty("nothing yet")),
            StructureRole.Hearth => "a fire kept in it warms and cooks",
            StructureRole.Shrine => "people gather at it to honour what they believe",
            StructureRole.Defence => "it stands between us and attackers",
            StructureRole.Water => "it gives clean water",
            StructureRole.Vessel => "it carries people and goods",
            StructureRole.Power => "it gives power to machines",
            StructureRole.Civic => "people meet in it to serve the community",
            _ => "",
        });
        Learn(lore, key, $"We built a {k.Id.Replace('_', ' ')}: {string.Join("; ", does.Where(d => d.Length > 0))}.", 1f, Day);
    }

    /// <summary>The first time a band makes something, it sees what went in and what came out.</summary>
    void SawMade(Band band, ProductionSpec spec)
    {
        if (!Config.RequireModel || !Lore.TryGetValue(band.Id, out var lore)) return;
        string key = "made:" + spec.Id;
        if (lore.Entries.ContainsKey(key)) return;
        static string List((ushort good, float qty)[] goods) => string.Join(", ", goods.Select(g => $"{g.qty:0.#} {Goods.All[g.good].Id.Replace('_', ' ')}"));
        string made = spec.ItemIndex >= 0 ? "a " + Crafts.Items[spec.ItemIndex].Id.Replace('_', ' ') : List(spec.Outputs);
        Learn(lore, key, $"Making {spec.Id.Replace('/', ' ').Replace('_', ' ')}: {List(spec.Inputs)} became {made} after about {spec.Labour:0.#} days of work" +
                         (spec.Power > 0f ? ", with power" : "") + ".", 1f, Day);
    }

    // ------------------------------------------------------------------ experiments

    /// <summary>The things a band can lay hands on or see around it, to name in experiments.</summary>
    public string[] Perceivable(Band band)
    {
        var w = World;
        var known = BandKnowledge(band);
        bool Has(params string[] goods) => goods.Any(g => Goods.TryId(g, out var id) && band.Stores[id] >= 0.5f);
        bool game = w.PreyK[band.Cell] > 0f;
        bool Sees(string s) => s switch
        {
            "wood" => Affords(band, Affordance.Forest) > 0f || Has("log", "wood"),
            "stone" or "plants" or "earth" or "the_dead" or "sky" or "people" or "fiber" => true,
            "flint" => Affords(band, Affordance.Rock) > 0f || Has("flint"),
            "clay" => Affords(band, Affordance.Clay) > 0f || Has("clay"),
            "sand" => Affords(band, Affordance.Sand) > 0f || Affords(band, Affordance.Coast) > 0f || Has("sand"),
            "reeds" => Affords(band, Affordance.Water) > 0f || Has("reed"),
            "hide" => game || Has("hide"),
            "bone" => game || Has("bone"),
            "meat" => game || Has("meat"),
            "fish" => w.FishK[band.Cell] > 0f || Affords(band, Affordance.Water) > 0f || Has("fish"),
            "seeds" => Affords(band, Affordance.Grass) > 0f || Has("grain", "millet", "wheat", "barley", "rice", "maize"),
            "animals" => game,
            "herd" => band.Herds.Any(h => h.Key != "dog" && h.Value >= 1f),
            "water" => w.WaterAccess[band.Cell] > 0f || Affords(band, Affordance.Water) > 0f,
            "fire" => known.Has(TechId.ControlledFire),
            "ore" => Affords(band, Affordance.Ore) > 0f || Has("copper_ore", "tin_ore", "iron_ore"),
            "metal" => known.Has(TechId.Copper) || Has("copper", "bronze", "iron", "steel"),
            "charcoal" => known.Has(TechId.Charcoal) || Has("charcoal", "coal"),
            "glass" => known.Has(TechId.Glass) || Has("glass"),
            "tools" => band.Members.Any(id => Living.TryGetValue(id, out var a) && a.Gear.Count > 0),
            "machines" => known.Has(TechId.Machinery),
            "power" => known.Has(TechId.SteamPower),
            _ => false,
        };
        return Discovery.Subjects.Where(Sees).ToArray();
    }

    public static bool TryMethod(string? text, out Method method)
    {
        method = default;
        foreach (var m in Enum.GetValues<Method>()) if (Discovery.Verb(m) == text) { method = m; return true; }
        return false;
    }

    bool ValidateExperiments(MindRequest request, MindPlan plan, out string error)
    {
        error = "Experiments need a listed action, one to three distinct things from can_work_with, and a short idea.";
        var list = plan.Experiments ?? Array.Empty<ExperimentIntent>();
        if (list.Length > 2) return false;
        var subjects = request.Subjects ?? Array.Empty<string>();
        foreach (var x in list)
            if (x == null || !TryMethod(x.Action, out _) || x.Subjects == null || x.Subjects.Length is < 1 or > 3 ||
                x.Subjects.Distinct().Count() != x.Subjects.Length || x.Subjects.Any(s => !subjects.Contains(s)) || !ValidText(x.Idea, 160)) return false;
        error = "";
        return true;
    }

    /// <summary>The council's experiments join those under way (the same action on the same things continues).</summary>
    void TakeUpExperiments(Band band, ExperimentIntent[]? intents)
    {
        if (intents == null || intents.Length == 0) return;
        var lore = LoreOf(band);
        foreach (var x in intents)
        {
            TryMethod(x.Action, out var method);
            var same = lore.Experiments.FirstOrDefault(j => j.Method == method && j.Subjects.OrderBy(s => s).SequenceEqual(x.Subjects.OrderBy(s => s)));
            if (same != null) { same.Idea = x.Idea; continue; }
            lore.Experiments.Add(new ExperimentJob { Id = ++_nextExperiment, Started = Day, Method = method, Subjects = x.Subjects.ToArray(), Idea = x.Idea });
            if (lore.Experiments.Count > MaxExperiments) lore.Experiments.Remove(lore.Experiments.OrderBy(j => j.Effort).First());
        }
    }

    /// <summary>Techniques the band could find here and now: unknown, grounded in what it knows, possible where it lives.</summary>
    internal IEnumerable<TechDef> Findable(Band band)
    {
        var known = BandKnowledge(band);
        return Techs.All.Where(t => !known.Has(t.Id) && t.Requires.All(known.Has) && Affords(band, t.Needs) > 0f);
    }

    (TechDef? tech, float quality) BestMatch(Band band, ExperimentJob job)
    {
        TechDef? best = null; float quality = 0f;
        foreach (var t in Findable(band))
        {
            float q = Discovery.Match(Discovery.Key(t.Id), job.Method, job.Subjects);
            if (q > quality + 1e-4f || best != null && MathF.Abs(q - quality) <= 1e-4f && t.Difficulty < best.Difficulty) { best = t; quality = q; }
        }
        return (best, quality);
    }

    /// <summary>A day of experimenting (sequential: after the parallel band day). <paramref name="rate"/> is 1 for one person's day.</summary>
    void WorkExperiment(Agent a, Band band, float rate)
    {
        var lore = LoreOf(band);
        if (lore.Experiments.Count == 0) return;
        var job = lore.Experiments[(Day + a.Id) % lore.Experiments.Count];
        job.Effort += rate * (0.5f + a.Gene(Gene.Intellect)) * (0.6f + 0.6f * a.T(Trait.NoveltySeeking)) * (a.InspiredUntil > Day ? 1.5f : 1f);
        if (!job.Contributors.Contains(a.Id)) job.Contributors.Add(a.Id);
        var (tech, quality) = BestMatch(band, job);
        float needed = tech != null && quality >= 0.5f ? 3f + 1.2f * tech.Difficulty : 5f;
        if (job.Effort >= needed) Conclude(band, lore, job, tech, quality);
    }

    void Conclude(Band band, BandLore lore, ExperimentJob job, TechDef? tech, float quality)
    {
        string what = $"{Discovery.Gerund(job.Method)} {string.Join(" and ", job.Subjects.Select(Discovery.SubjectNote))}";
        var key = tech != null ? Discovery.Key(tech.Id) : null;
        float chance = quality >= 0.99f ? 0.9f : quality >= 0.74f ? 0.4f : 0f;
        if (tech != null && key != null && Rng.Chance(chance))
        {
            var finders = job.Contributors.Select(id => Living.GetValueOrDefault(id)).Where(p => p is { Alive: true } && p.BandId == band.Id && Techs.CanLearn(p.Knows, tech.Id)).ToList();
            if (finders.Count == 0 && band.Members.Select(id => Living.GetValueOrDefault(id)).FirstOrDefault(p => p != null && Techs.CanLearn(p.Knows, tech.Id)) is { } someone) finders.Add(someone);
            if (finders.Count > 0)
            {
                Discover(finders[0]!, band, tech);
                foreach (var p in finders.Skip(1)) p!.Knows.Add(tech.Id);
                Learn(lore, "tech:" + Ids.Snake(tech.Id), $"{key.Sign} We now know how to {key.Use}.{Ideas(tech.Id)}", 1f, Day);
                lore.Experiments.Remove(job);
                return;
            }
        }
        string text;
        if (quality >= 0.99f) { text = $"{what} almost worked; it is worth keeping at."; job.Effort *= 0.5f; Learn(lore, $"clue:{Discovery.Verb(job.Method)}:{string.Join("+", job.Subjects.OrderBy(s => s))}", text, 0.7f, Day); Told(band, text); return; }
        if (key != null && quality >= 0.5f)
        {
            var missing = key.Subjects.Where(s => !job.Subjects.Contains(s)).ToArray();
            var stray = job.Subjects.Where(s => !key.Subjects.Contains(s)).ToArray();
            string Things(IEnumerable<string> xs) => string.Join(" and ", xs.Select(Discovery.SubjectNote));
            text = job.Method == key.Method
                ? $"{char.ToUpper(what[0]) + what[1..]} changed something, but not enough." +
                  (missing.Length > 0 ? $" It seemed to want {Things(missing)}." : "") +
                  (stray.Length > 0 ? $" The {Things(stray)} only got in the way; try it without {(stray.Length == 1 ? "it" : "them")}." : "")
                : Secret(lore, key, job);
            Learn(lore, $"clue:{Discovery.Verb(job.Method)}:{string.Join("+", job.Subjects.OrderBy(s => s))}", text, 0.6f, Day);
        }
        else
        {
            text = $"{char.ToUpper(what[0]) + what[1..]} came to nothing.";
            Learn(lore, $"failed:{Discovery.Verb(job.Method)}:{string.Join("+", job.Subjects.OrderBy(s => s))}", text, 0.9f, Day);
        }
        lore.Experiments.Remove(job);
        Told(band, text);
    }

    /// <summary>The right things worked the wrong way; after a few such tries someone guesses the way.</summary>
    static string Secret(BandLore lore, DiscoveryKey key, ExperimentJob job)
    {
        var right = key.Subjects.Where(job.Subjects.Contains).Select(Discovery.SubjectNote).ToArray();
        string things = string.Join(" and ", right);
        string way = string.Join("+", key.Subjects.OrderBy(s => s));
        int tries = lore.WrongWays[way] = lore.WrongWays.GetValueOrDefault(way) + 1;
        return $"{char.ToUpper(things[0]) + things[1..]} seemed to hold a secret, but {Discovery.Gerund(job.Method)} them was not the way." +
               (tries >= 3 ? $" Perhaps they want {Discovery.Gerund(key.Method)}." : "");
    }

    void Told(Band band, string text) =>
        Chronicle.Add(new WorldEvent(Day, EventCategory.Discovery, "experiment", $"The {band.Name}'s experiment: {text}", 0.3f, band.LeaderId, band.Id));

    /// <summary>
    /// Every fortnight or so someone at their work may notice something: half of a way into a
    /// technique the band could find (two of its things, or the action and one thing).
    /// </summary>
    void Notice(Band band)
    {
        if (!Rng.Chance(0.12f)) return;
        var findable = Findable(band).ToArray();
        if (findable.Length == 0) return;
        var tech = findable[Rng.Next(findable.Length)];
        var key = Discovery.Key(tech.Id);
        var lore = LoreOf(band);
        var who = band.Members.Count == 0 ? null : Living.GetValueOrDefault(band.Members[Rng.Next(band.Members.Count)]);
        string name = who?.Name ?? "Someone";
        string first = Discovery.SubjectNote(key.Subjects[0]);
        string text = key.Subjects.Length > 1 && Rng.Chance(0.5f)
            ? $"{name} noticed that {first} and {Discovery.SubjectNote(key.Subjects[1])} behave strangely together."
            : $"{name} wondered what {Discovery.Gerund(key.Method)} {first} might do.";
        Learn(lore, "clue:notice:" + Ids.Snake(tech.Id), text, 0.4f, Day);
    }

    /// <summary>Lore as the minds see it: kinds in plain words, the freshest and surest first.</summary>
    public IEnumerable<(string about, string text)> LoreFor(Band band, int max = 48)
    {
        if (!Lore.TryGetValue(band.Id, out var lore)) return Array.Empty<(string, string)>();
        static string About(string key) => key[..key.IndexOf(':')] switch
        {
            "tradition" => "tradition", "work" => "work", "built" => "building", "made" => "making",
            "tech" => "knowledge", "clue" => "clue", "failed" => "dead end", _ => "lore",
        };
        int Rank(LoreEntry e) => About(e.Key) switch { "clue" => 0, "work" => 1, "tradition" => 2, "building" => 3, "making" => 4, "dead end" => 5, _ => 6 };
        return lore.Entries.Values.OrderBy(Rank).ThenByDescending(e => e.Day).Take(max).Select(e => (About(e.Key), e.Text));
    }

    /// <summary>The band's experiments under way (read-only).</summary>
    public IReadOnlyList<ExperimentJob> ExperimentsOf(Band band) => Lore.TryGetValue(band.Id, out var lore) ? lore.Experiments : Array.Empty<ExperimentJob>();

    /// <summary>Whether the band has seen what an activity, building or product does.</summary>
    public bool Knows(Band band, string key) => Lore.TryGetValue(band.Id, out var lore) && lore.Entries.ContainsKey(key);
}
