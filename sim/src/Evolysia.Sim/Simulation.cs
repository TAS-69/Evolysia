using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Evolysia.Sim.Psych;
using Evolysia.Sim.Society;
using Evolysia.Sim.World;

namespace Evolysia.Sim;

/// <summary>
/// The whole world, stepped one day at a time. Engine-agnostic: a renderer reads the public
/// state; nothing here knows about Godot or the LLM.
/// </summary>
public sealed partial class Simulation
{
    public readonly SimConfig Config;
    public readonly WorldMap World;
    readonly Rng _mainRng;
    [ThreadStatic] static Rng? t_rng;               // a band's own stream while it lives its day
    [ThreadStatic] static DayBuffer? t_buffer;      // global effects waiting for the ordered commit

    /// <summary>
    /// The random stream for whatever is running now: a band's own stream (seed, band, day) inside
    /// band days, the world stream everywhere else. That is what makes results independent of
    /// thread count and scheduling.
    /// </summary>
    public Rng Rng => t_rng ?? _mainRng;

    sealed class DayBuffer { public readonly List<Action> Deferred = new(); }

    /// <summary>Run now, or, inside a parallel band day, after all bands finish (in band order).</summary>
    internal void Later(Action action)
    {
        if (t_buffer is { } b) b.Deferred.Add(action);
        else action();
    }
    readonly Noise _climateNoise;

    public int Day { get; private set; }
    public int Year => Day / SimConfig.DaysPerYear;

    public readonly Dictionary<int, Agent> Living = new();
    public readonly Dictionary<int, Agent> Dead = new();
    public readonly SortedDictionary<int, Band> Bands = new();
    public readonly Dictionary<int, Culture> Cultures = new();
    public readonly Dictionary<int, Meme> Memes = new();
    public readonly Chronicle Chronicle = new();
    public readonly Stats Stats = new();

    int _nextAgent = 1, _nextBand = 1, _nextCulture = 1, _nextMeme = 1;
    readonly List<Agent> _newborns = new();
    readonly List<Agent> _dying = new();
    float _volcanicCooling;

    public Simulation(SimConfig config)
    {
        Config = config;
        World = WorldGen.Generate(config);
        _mainRng = Rng.Derive(config.Seed, 0x4C494645UL); // "LIFE"
        _climateNoise = new Noise(Rng.Derive(config.Seed, 0x434C494DUL));
        Ecology.NewYear(World, _climateNoise, 0);
        SeedPopulation();
    }

    public Agent? Find(int id) => Living.TryGetValue(id, out var a) ? a : Dead.TryGetValue(id, out var d) ? d : null;
    public Band? BandOf(Agent a) => Bands.TryGetValue(a.BandId, out var b) ? b : null;

    // ------------------------------------------------------------------ setup

    void SeedPopulation()
    {
        var w = World;
        var good = new List<int>();
        for (int i = 0; i < w.Count; i++)
            if (w.IsLand(i) && w.WaterAccess[i] >= 0.9f && w.Biome[i] is Biome.TemperateGrassland or Biome.TemperateForest
                    or Biome.Savanna or Biome.TropicalSeasonalForest or Biome.Shrubland)
                good.Add(i);
        if (good.Count == 0)
            for (int i = 0; i < w.Count; i++) if (w.IsLand(i) && w.PlantK[i] > 0f) good.Add(i);
        good.Sort((a, b) => (w.PlantK[b] + w.PreyK[b]).CompareTo(w.PlantK[a] + w.PreyK[a]));
        int home = good[Rng.Next(Math.Min(30, good.Count))];

        var culture = NewCulture(null);
        Chronicle.Add(new WorldEvent(0, EventCategory.Social, "dawn",
            $"The first people, the {culture.Name}, wake in the {Ids.Snake(w.Biome[home]).Replace('_', ' ')} at {w.X(home)},{w.Y(home)}.", 1f));

        for (int b = 0; b < Config.StartBands; b++)
        {
            int cell = b == 0 ? home : NearbyLand(home, 1 + b, 3 + b);
            var band = NewBand(cell, culture.Id, -1);
            bool fire = Rng.Chance(0.6f);
            var adults = new List<Agent>();
            int n = Config.StartBandSize;
            for (int k = 0; k < n * 0.65f; k++)
            {
                var a = NewAgent(Genome.Random(Rng), band, birthDay: -Rng.Range(16, 50) * SimConfig.DaysPerYear - Rng.Next(360));
                a.Knows = Techs.Bit(TechId.StoneTools) | (fire ? Techs.Bit(TechId.ControlledFire) : 0UL);
                adults.Add(a);
            }
            // Pair some adults and give them children.
            var women = adults.Where(a => a.Sex == Sex.Female).ToList();
            var men = adults.Where(a => a.Sex == Sex.Male).ToList();
            for (int k = 0; k < Math.Min(women.Count, men.Count) && k < 5; k++) Bond(women[k], men[k], 0);
            int kids = n - adults.Count;
            for (int k = 0; k < kids; k++)
            {
                var mom = women[k % Math.Max(1, Math.Min(women.Count, 5))];
                var dad = Find(mom.PartnerId) ?? men.FirstOrDefault() ?? mom;
                var g = Genome.Inherit(mom.Genome, dad.Genome, Rng, out _);
                var c = NewAgent(g, band, -Rng.Range(0, 13) * SimConfig.DaysPerYear - Rng.Next(360));
                c.MotherId = mom.Id; c.FatherId = dad.Id; c.Generation = 1;
                mom.Children.Add(c.Id); if (dad != mom) dad.Children.Add(c.Id);
            }
            ElectLeader(band);
            Chronicle.First($"landmass:{World.Landmass[band.Cell]}");
        }
    }

    int NearbyLand(int center, int minDist, int maxDist)
    {
        for (int tries = 0; tries < 200; tries++)
        {
            int x = World.X(center) + Rng.Range(-maxDist, maxDist + 1);
            int y = Math.Clamp(World.Y(center) + Rng.Range(-maxDist, maxDist + 1), 0, World.H - 1);
            int i = World.Index(x, y);
            int d = World.Distance(center, i);
            if (d >= minDist && d <= maxDist && World.IsLand(i) && World.Biome[i] != Biome.Lake && World.PlantK[i] > 0f) return i;
        }
        return center;
    }

    internal Culture NewCulture(Culture? parent)
    {
        var c = new Culture
        {
            Id = _nextCulture++,
            ParentId = parent?.Id ?? -1,
            FoundedDay = Day,
            Hue = Rng.Next(12),
            GlyphSeed = Rng.Mix((ulong)Rng.NextUInt() << 16 | (uint)_nextCulture),
            Syllables = parent is null ? Names.NewPhonology(Rng) : Names.Mutate(parent.Syllables, Rng),
        };
        c.Name = Names.Make(c.Syllables, Rng, 2, 3);
        Cultures[c.Id] = c;
        Stats.Year.NewCultures++;
        return c;
    }

    internal Band NewBand(int cell, int cultureId, int parentBandId)
    {
        var band = new Band
        {
            Id = _nextBand++, Cell = cell, CultureId = cultureId, FoundedDay = Day, ParentBandId = parentBandId,
            LastMoveDay = Day, Shelter = 0.1f,
        };
        band.Name = Names.Make(Cultures[cultureId].Syllables, Rng, 2, 3);
        Bands[band.Id] = band;
        BandsChanged();
        return band;
    }

    internal Agent NewAgent(Genome g, Band band, int birthDay)
    {
        var a = new Agent
        {
            Id = _nextAgent++, Genome = g, Sex = Rng.Chance(0.5f) ? Sex.Female : Sex.Male, BirthDay = birthDay,
            BandId = band.Id, CultureId = band.CultureId, Food = 3f,
        };
        a.Name = Names.Make(Cultures[band.CultureId].Syllables, Rng);
        for (int d = 0; d < Ids.DomainCount; d++) a.Efficacy[d] = Rng.Range(0.15f, 0.45f);
        Living[a.Id] = a;
        band.Members.Add(a.Id);
        return a;
    }

    internal void Bond(Agent x, Agent y, int day)
    {
        x.PartnerId = y.Id; y.PartnerId = x.Id;
        x.Rel(y.Id).Affinity = MathF.Max(x.Rel(y.Id).Affinity, 0.6f);
        y.Rel(x.Id).Affinity = MathF.Max(y.Rel(x.Id).Affinity, 0.6f);
        x.Feel(Emotion.Love, 0.6f); y.Feel(Emotion.Love, 0.6f);
    }

    // ------------------------------------------------------------------ main loop

    /// <summary>Wall-clock seconds spent per phase (for tuning performance).</summary>
    public readonly Dictionary<string, double> Profile = new();
    readonly System.Diagnostics.Stopwatch _watch = new();

    void Timed(string phase, Action action)
    {
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        action();
        Profile[phase] = Profile.GetValueOrDefault(phase) + System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalSeconds;
    }

    public void Step()
    {
        if (WorldMap.DayOfYear(Day) == 0) Timed("new_year", NewYear);
        ResolveDueDecisions();
        if (Day % Config.EcologyStepDays == 0) Timed("ecology", () => Ecology.Step(World, Day, Config.EcologyStepDays));

        Timed("disasters", WorldDisasters);
        Timed("band_days", BandDays);

        FlushDeathsAndBirths();
        if (Day % 15 == 0) Timed("band_decisions", BandDecisions);
        if (Day % 30 == 0)
        {
            Timed("meme_caches", RefreshMemeCaches);
            Timed("contacts", Contacts);
            Timed("cultural_upkeep", CulturalUpkeep);
            PrayerUpkeep();
        }
        Day++;
        if (WorldMap.DayOfYear(Day) == 0) Timed("stats", () => Stats.CloseYear(this));
    }

    /// <summary>
    /// Every band lives its day. Bands sharing a cell share its food, so they run together in id
    /// order; different cells run in parallel. Each band draws from its own random stream and
    /// defers anything global (births, death bookkeeping, chronicle, cross-band acts), committed
    /// afterwards in a fixed order, so 1 thread and 16 threads give the identical history.
    /// </summary>
    void BandDays()
    {
        var groups = Bands.Values.Where(b => !b.Disbanded).GroupBy(b => b.Cell).Select(g => g.ToArray()).ToArray();
        var buffers = new DayBuffer[groups.Length];
        ulong daySeed = Rng.Mix(Config.Seed ^ ((ulong)(uint)Day << 20));
        void Live(int gi)
        {
            var buffer = buffers[gi] = new DayBuffer();
            t_buffer = buffer;
            try
            {
                foreach (var band in groups[gi])
                {
                    t_rng = Rng.Derive(daySeed, (ulong)band.Id);
                    BandDay(band);
                }
            }
            finally { t_rng = null; t_buffer = null; }
        }
        if (Config.Threads <= 1 || groups.Length < 4)
            for (int gi = 0; gi < groups.Length; gi++) Live(gi);
        else
        {
            // Heaviest groups first, handed out one at a time (big villages would otherwise leave
            // threads idle). Order of work doesn't matter: buffers are committed by group index.
            var heaviestFirst = Enumerable.Range(0, groups.Length).OrderByDescending(gi => groups[gi].Sum(b => b.Members.Count)).ToArray();
            Parallel.ForEach(System.Collections.Concurrent.Partitioner.Create(heaviestFirst, System.Collections.Concurrent.EnumerablePartitionerOptions.NoBuffering),
                new ParallelOptions { MaxDegreeOfParallelism = Config.Threads }, Live);
        }
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        foreach (var buffer in buffers)
            foreach (var action in buffer.Deferred) action();
        Profile["band_commit"] = Profile.GetValueOrDefault("band_commit") + System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalSeconds;
    }

    public void RunYears(int years, Action<Simulation>? onYear = null)
    {
        int end = Day + years * SimConfig.DaysPerYear;
        while (Day < end && Living.Count > 0)
        {
            Step();
            if (WorldMap.DayOfYear(Day) == 0) onYear?.Invoke(this);
        }
        if (Living.Count == 0) Chronicle.Add(new WorldEvent(Day, EventCategory.Loss, "extinction", "The last person has died. The world is quiet.", 1f));
    }

    void NewYear()
    {
        Ecology.NewYear(World, _climateNoise, Year);
        _volcanicCooling *= 0.55f;
        World.GlobalTempAnomaly = World.GlobalTempAnomaly * 0.7f + Rng.Normal(0f, 0.35f) - _volcanicCooling;
        foreach (var band in Bands.Values) if (!band.Disbanded) ElectLeader(band);
        CultureDrift();
    }

    // ------------------------------------------------------------------ life and death

    internal void Kill(Agent a, DeathCause cause, Stimulus? witnessedAs = null, string how = "")
    {
        if (!a.Alive) return;
        a.Alive = false; a.DeathCause = cause; a.DeathDay = Day;

        var band = BandOf(a);
        bool notable = a.Prestige > 3f || (band != null && band.LeaderId == a.Id);
        Later(() =>
        {
            _dying.Add(a);
            Stats.Year.Deaths[(int)cause]++;
            if (notable || cause is DeathCause.Violence or DeathCause.Disaster)
                Chronicle.Add(new WorldEvent(Day, EventCategory.Loss, "death",
                    $"{a.Name} of the {band?.Name} died ({Ids.Snake(cause).Replace('_', ' ')}{how}) aged {a.Age(Day):0}.",
                    notable ? 0.6f : 0.3f, a.Id, a.BandId));
            if (notable)
                Chronicle.Moment(new LlmMoment(Day, "notable_death", a.Id, a.BandId, new()
                {
                    ["cause"] = Ids.Snake(cause), ["age"] = a.Age(Day).ToString("0"), ["role"] = Ids.Snake(a.MainActivity()),
                    ["was_leader"] = (band?.LeaderId == a.Id).ToString(),
                }));
        });

        if (band == null) return;
        foreach (int mid in band.Members)
        {
            if (mid == a.Id || !Living.TryGetValue(mid, out var m) || !m.Alive) continue;
            float closeness = m.PartnerId == a.Id ? 1f
                : a.Children.Contains(m.Id) || m.Children.Contains(a.Id) ? 0.9f
                : MathF.Max(0f, m.AffinityTo(a.Id));
            if (closeness > 0.2f)
                Psychology.Appraise(m, new Appraisal(EventCategory.Loss, "bereaved", -1f, closeness, Agency.Unknown, Loss: true, OtherId: a.Id, Detail: a.Name), Day);
            if (witnessedAs is { } s && Rng.Chance(0.7f)) Fear.Observe(m, s, 0.8f, Day);
            if (m.PartnerId == a.Id) m.PartnerId = -1;
        }
    }

    void FlushDeathsAndBirths()
    {
        BandsChanged();
        foreach (var a in _dying)
        {
            Living.Remove(a.Id);
            Dead[a.Id] = a;
            a.Relations.Clear();      // keep the archive light
            a.Memes.Clear();
            if (Bands.TryGetValue(a.BandId, out var band))
            {
                band.Members.Remove(a.Id);
                if (band.LeaderId == a.Id) ElectLeader(band);
                if (band.Members.Count == 0) Disband(band);
            }
        }
        _dying.Clear();
        foreach (var c in _newborns) if (Bands.TryGetValue(c.BandId, out var b) && !b.Disbanded) b.Members.Add(c.Id);
        _newborns.Clear();
    }

    internal void Disband(Band band)
    {
        BandsChanged();
        band.Disbanded = true;
        Bands.Remove(band.Id);
    }

    internal void GiveBirth(Agent mother, Band band)
    {
        mother.Pregnant = false;
        mother.LastBirthDay = Day;
        var father = Find(mother.PregnancyFatherId) ?? mother;
        var g = Genome.Inherit(mother.Genome, father.Genome, Rng, out var divergences);
        var child = new Agent
        {
            Id = _nextAgent++, Genome = g, Sex = Rng.Chance(0.5f) ? Sex.Female : Sex.Male, BirthDay = Day,
            BandId = band.Id, CultureId = band.CultureId, MotherId = mother.Id, FatherId = father.Id,
            Generation = Math.Max(mother.Generation, father.Generation) + 1,
        };
        child.Name = Names.Make(Cultures[band.CultureId].Syllables, Rng);
        for (int d = 0; d < Ids.DomainCount; d++) child.Efficacy[d] = 0.1f;
        // Upbringing: character starts tilted towards the parents' lived character, beliefs too.
        for (int t = (int)Trait.SelfDirectedness; t <= (int)Trait.SelfTranscendence; t++)
        {
            float parents = (mother.T((Trait)t) + father.T((Trait)t)) * 0.5f;
            child.TraitOffset[t] = Math.Clamp((parents - child.Genome.Genes[t]) * 0.3f, -0.5f, 0.5f);
        }
        foreach (var (meme, conv) in mother.Memes) child.Memes[meme] = conv * 0.5f;
        foreach (var (meme, conv) in father.Memes) child.Memes[meme] = MathF.Max(child.Memes.GetValueOrDefault(meme), conv * 0.5f);

        Living[child.Id] = child;
        _newborns.Add(child);
        mother.Children.Add(child.Id);
        if (father != mother) father.Children.Add(child.Id);
        Stats.Year.Births++;
        foreach (var p in new[] { mother, father })
            Psychology.Appraise(p, new Appraisal(EventCategory.Birth, "child_born", 1f, 0.7f, Agency.Other, OtherId: child.Id, Detail: child.Name), Day);
        if (divergences.Count >= 3 && Rng.Chance(0.1f))
            Chronicle.Add(new WorldEvent(Day, EventCategory.Birth, "divergent_birth",
                $"{child.Name} is born to {mother.Name} and {father.Name}, unlike either ({string.Join(", ", divergences)}).", 0.25f, child.Id, band.Id));

        float risk = 0.012f * (1.3f - mother.Gene(Agents.Gene.Constitution)) * (BandKnows(band, TechId.Herbalism) ? 0.6f : 1f);
        if (Rng.Chance(risk)) Kill(mother, DeathCause.Childbirth);
    }

    internal bool BandKnows(Band band, TechId tech)
    {
        foreach (int id in band.Members)
            if (Living.TryGetValue(id, out var a) && Techs.Has(a.Knows, tech)) return true;
        return false;
    }

    internal float BandShare(Band band, TechId tech)
    {
        int n = 0, k = 0;
        foreach (int id in band.Members)
            if (Living.TryGetValue(id, out var a) && a.IsAdult(Day)) { n++; if (Techs.Has(a.Knows, tech)) k++; }
        return n == 0 ? 0f : (float)k / n;
    }

    internal void ElectLeader(Band band)
    {
        bool elderRule = BandHolds(band, MemeKind.ElderRule, 0.4f);
        Agent? best = null;
        float bestScore = float.MinValue;
        foreach (int id in band.Members)
        {
            if (!Living.TryGetValue(id, out var a) || !a.IsAdult(Day)) continue;
            float age = a.Age(Day);
            float s = a.Prestige + 0.6f * a.T(Trait.SelfDirectedness) + 0.2f * a.Gene(Agents.Gene.Charisma)
                      + (elderRule ? age / 40f : age is > 25f and < 60f ? 0.3f : 0f);
            if (s > bestScore) { bestScore = s; best = a; }
        }
        int before = band.LeaderId;
        band.LeaderId = best?.Id ?? -1;
        if (best != null && before != -1 && before != best.Id)
            Chronicle.Add(new WorldEvent(Day, EventCategory.Social, "new_leader", $"{best.Name} now leads the {band.Name}.", 0.3f, best.Id, band.Id));
    }
}
