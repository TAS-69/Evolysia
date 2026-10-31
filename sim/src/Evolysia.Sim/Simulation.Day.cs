using Evolysia.Sim.Agents;
using Evolysia.Sim.Economy;
using Evolysia.Sim.Core;
using Evolysia.Sim.Psych;
using Evolysia.Sim.Society;
using Evolysia.Sim.World;

namespace Evolysia.Sim;

public sealed partial class Simulation
{
    /// <summary>Band-level conditions shared by everyone in it today.</summary>
    struct DayContext
    {
        public float Temp, Growth, Wet, Shortage, PlantAvail, PreyAvail, FishAvail, Predators, Water;
        public int Range, PreySite, FishSite;   // tiles: where the game is today, the best fishing water (-1: none)
        public int Fields, Herd;                 // the band's fields and tame animals (not counting dogs)
        public float CraftNeed;                  // 0..1: how many members lack gear the band could make
        public TechSet Tech;
        public Season Season;
        public bool ColdAhead;
    }

    static readonly int ActivityCount = Enum.GetValues<Activity>().Length;
    // Per-thread scratch space (band days run in parallel).
    [ThreadStatic] static List<Agent>? t_members;
    [ThreadStatic] static List<Agent>? t_hunters;
    [ThreadStatic] static float[]? t_scores;
    static List<Agent> _members => t_members ??= new List<Agent>();
    static List<Agent> _hunters => t_hunters ??= new List<Agent>();
    static float[] _scores => t_scores ??= new float[ActivityCount];
    [ThreadStatic] static List<int>? t_land;
    static List<int> _land => t_land ??= new List<int>();   // today's surveyed land tiles in the band's range

    /// <summary>How far (cells) a band's people go out to work: about a fifth of an original cell's width each way.</summary>
    internal int RangeOf(Band band) => (int)(World.Scale * 0.8f);

    /// <summary>A tile within <paramref name="r"/> cells of <paramref name="cell"/> (wrapping, clamped at the poles).</summary>
    int Around(int cell, int r)
    {
        if (r <= 0) return cell;
        int y = Math.Clamp(World.Y(cell) + Rng.Range(-r, r + 1), 0, World.H - 1);
        return World.Index(World.X(cell) + Rng.Range(-r, r + 1), y);
    }

    void BandDay(Band band)
    {
        var w = World;
        int cell = band.Cell;
        _members.Clear();
        foreach (int id in band.Members)
            if (Living.TryGetValue(id, out var m) && m.Alive) _members.Add(m);
        if (_members.Count == 0) return;

        var ctx = new DayContext
        {
            Temp = w.Temperature(cell, Day),
            Growth = w.Growth(cell, Day),
            Wet = w.Wetness(cell, Day),
            Season = w.SeasonOf(cell, Day),
            Range = RangeOf(band),
        };
        Survey(band, ref ctx);
        ctx.Water = MathF.Max(w.WaterAccess[cell] * (0.5f + 0.5f * MathF.Min(ctx.Wet, 1f)), band.WaterBonus);
        band.BuildJobs = 0;
        foreach (int sid in band.Structures)
            if (Structures.TryGetValue(sid, out var st))
            {
                if (st.State is Economy.StructureState.Planned or Economy.StructureState.Building or Economy.StructureState.Damaged) band.BuildJobs++;
                if (st.Standing && st.Kind.Role == Economy.StructureRole.Field) ctx.Fields++;
            }
        foreach (var (species, head) in band.Herds) if (species != "dog") ctx.Herd += (int)head;
        float stored = band.FoodPool;
        foreach (var m in _members) { ctx.Tech |= m.Knows; stored += m.Food; }
        int lacking = 0, looked = 0;
        for (int k = 0; k < Math.Min(4, _members.Count); k++)
        {
            var m = _members[Rng.Next(_members.Count)];
            if (m.IsChild(Day)) continue;
            looked++;
            if (WantedItem(m, band, ctx.Tech, band) is not null) lacking++;
        }
        ctx.CraftNeed = looked == 0 ? 0f : lacking / (float)looked;
        ctx.Shortage = Math.Clamp(1f - stored / (_members.Count * 4f), 0f, 1f);
        float hungerNow = 0f;
        foreach (var m in _members) hungerNow += m.NeedOf(Need.Hunger);
        band.Stress = band.Stress * 0.97f + 0.03f * (hungerNow / _members.Count + 0.5f * ctx.Shortage);
        ctx.ColdAhead = ctx.Season == Season.Autumn || (w.TempMean[cell] - w.TempAmp[cell] < 2f && ctx.Season != Season.Summer);

        // 1. Everyone decides what to do today.
        _hunters.Clear();
        foreach (var a in _members)
        {
            a.Today = Choose(a, band, ctx);
            if (a.IsAdult(Day)) a.ActivityCounts[(int)a.Today]++;
            if (a.Today == Activity.Hunt) _hunters.Add(a);
        }

        // 2. Group hunt, then individual activities.
        if (_hunters.Count > 0) Hunt(band, ctx);
        foreach (var a in _members)
            if (a.Alive && a.Today != Activity.Hunt) Resolve(a, band, ctx);

        // 3. Sharing, eating, bodies, babies, minds.
        ShareAndEat(band, ctx);
        foreach (var a in _members)
        {
            if (!a.Alive) continue;
            Body(a, band, ctx);
            if (!a.Alive) continue;
            Reproduce(a, band, ctx);
            Psychology.DailyUpdate(a, Day);
            if (a.AgeDays(Day) == 12 * SimConfig.DaysPerYear) ComeOfAge(a, band);
        }
    }

    static float Avail(float stock, float cap) => cap <= 0f ? 0f : stock / (stock + 0.25f * cap);

    /// <summary>
    /// Looks over the home range: a handful of sampled tiles stand for what is out there today.
    /// Sets plant availability (average), the best hunting ground and the best fishing water.
    /// </summary>
    void Survey(Band band, ref DayContext c)
    {
        var w = World;
        _land.Clear();
        float plants = 0f, danger = 0f, bestPrey = -1f, bestFish = -1f;
        c.PreySite = band.Cell; c.FishSite = -1;
        int samples = c.Range == 0 ? 1 : 10;
        for (int k = 0; k < samples; k++)
        {
            int t = k == 0 ? band.Cell : Around(band.Cell, c.Range);
            danger += w.Predators[t];
            if (w.FishK[t] > 0f)
            {
                float f = Avail(Left(2, t), w.FishK[t]) * MathF.Min(1f, w.FishK[t] / (60f * w.CellArea));
                if (f > bestFish) { bestFish = f; c.FishSite = t; }
            }
            if (!w.IsDryLand(t)) continue;
            _land.Add(t);
            plants += Avail(Left(0, t), w.PlantK[t]);
            float p = Avail(Left(1, t), w.PreyK[t]);
            if (p > bestPrey) { bestPrey = p; c.PreySite = t; }
        }
        if (_land.Count == 0) _land.Add(band.Cell);
        c.PlantAvail = plants / _land.Count;
        c.PreyAvail = MathF.Max(0f, bestPrey);
        c.FishAvail = MathF.Max(0f, bestFish);
        c.Predators = danger / samples * Config.PredatorDanger;
    }

    // ------------------------------------------------------------------ shared harvests
    //
    // Ranges overlap, and bands in different cells run on different threads. Every write to a
    // shared stock is deferred to the ordered commit, so during the parallel phase the stocks hold
    // still: each cell group reads them as they stood at the start of the day, keeps a running
    // tally of what it took, and its deductions are committed in group order.

    [ThreadStatic] static Dictionary<long, float>? t_taken;

    // Shared tile stocks: 0 plants, 1 game, 2 fish, 3 standing trees, 4 loose stone and boulders, 5 ore.
    internal const int Plants = 0, Game = 1, FishStock = 2, Timber = 3, StoneStock = 4, OreStock = 5;
    float[] Stock(int kind) => kind switch
    {
        0 => World.PlantStock, 1 => World.PreyStock, 2 => World.FishStock, 3 => World.Trees, 4 => World.Rock, _ => World.Ore,
    };
    float[] Cap(int kind) => kind switch
    {
        0 => World.PlantK, 1 => World.PreyK, 2 => World.FishK, 3 => World.TreesK, 4 => World.RockK, _ => World.OreK,
    };

    /// <summary>What is left of a tile's stock for this group today.</summary>
    float Left(int kind, int tile)
    {
        float s = Stock(kind)[tile];
        if (t_taken == null) return s;
        float taken = t_taken.TryGetValue((long)kind << 32 | (uint)tile, out var v) ? v : 0f;
        return MathF.Max(0f, s - taken);
    }

    /// <summary>Take up to <paramref name="want"/> from a tile's stock, with diminishing returns as it thins.</summary>
    float Take(int kind, int tile, float want)
    {
        if (t_taken == null) return Ecology.Harvest(Stock(kind), Cap(kind), tile, want);   // outside the parallel phase
        float k = MathF.Max(Cap(kind)[tile], 1e-3f);
        float s = Left(kind, tile);
        float got = MathF.Min(want * s / (s + 0.25f * k), s);
        long key = (long)kind << 32 | (uint)tile;
        t_taken[key] = t_taken.GetValueOrDefault(key) + got;
        return got;
    }

    /// <summary>Commits a group's harvests (inside its deferred buffer, so in group order).</summary>
    void CommitTaken()
    {
        if (t_taken == null || t_taken.Count == 0) return;
        var taken = t_taken.ToArray();
        t_taken.Clear();
        Later(() =>
        {
            foreach (var (key, amount) in taken)
            {
                var stock = Stock((int)(key >> 32));
                int tile = (int)(uint)key;
                stock[tile] = MathF.Max(0f, stock[tile] - amount);
            }
        });
    }

    /// <summary>People working a tile push its predators back (deferred: tiles are shared across threads).</summary>
    void Press(int tile, float amount) => Later(() => World.HumanPressure[tile] = MathF.Min(1f, World.HumanPressure[tile] + amount));

    // ------------------------------------------------------------------ choosing

    Activity Choose(Agent a, Band band, in DayContext c)
    {
        float age = a.Age(Day);
        if (age < 4f) return Activity.Rest;
        var s = _scores;
        Array.Fill(s, float.NegativeInfinity);
        float hunger = a.NeedOf(Need.Hunger), energy = a.NeedOf(Need.Energy);
        float food = Math.Clamp(hunger * 1.2f + c.Shortage, 0f, 1.6f);

        if (age < 14f)
        {
            s[(int)Activity.Play] = 0.6f;
            s[(int)Activity.Socialize] = 0.25f + a.NeedOf(Need.Social) * 0.4f;
            if (age >= 8f) s[(int)Activity.Forage] = 0.2f + food * 0.7f;
            s[(int)Activity.Rest] = energy;
            return (Activity)Rng.Softmax(s, 0.25f);
        }

        float ns = a.T(Trait.NoveltySeeking), ha = a.T(Trait.HarmAvoidance), rd = a.T(Trait.RewardDependence);
        float co = a.T(Trait.Cooperativeness), st = a.T(Trait.SelfTranscendence), sd = a.T(Trait.SelfDirectedness);
        bool elder = age > 62f;
        float beastFear = a.CategoryFear[(int)StimulusCategory.Beasts] * 0.6f + MaxBeastFear(a) * 0.6f;

        s[(int)Activity.Rest] = 0.1f + energy * 1.2f + (a.Sick ? 1.2f : 0f) + (a.Health < 0.5f ? 0.6f : 0f) + (elder ? 0.35f : 0f);
        s[(int)Activity.Forage] = 0.35f + food * (0.5f + c.PlantAvail) * MathF.Max(c.Growth, 0.35f) + 0.3f * a.Efficacy[(int)Domain.Foraging];

        if (!elder)
            s[(int)Activity.Hunt] = 0.15f + food * 0.9f * c.PreyAvail + 0.4f * a.Efficacy[(int)Domain.Hunting] + 0.2f * ns
                                    + 0.15f * a.Gene(Gene.Strength) - beastFear * (0.3f + ha) - Taboo(a, StimulusCategory.Beasts) * 0.6f
                                    + (Techs.Has(c.Tech, TechId.Spear) ? 0.1f : 0f) + (Techs.Has(c.Tech, TechId.Bow) ? 0.1f : 0f);

        if (c.FishSite >= 0)
            s[(int)Activity.Fish] = 0.05f + food * 0.8f * c.FishAvail * (Techs.Has(c.Tech, TechId.Fishing) ? 1f : 0.35f)
                                    + 0.3f * a.Efficacy[(int)Domain.Fishing]
                                    - a.EffectiveFear(Stimulus.River) * (0.4f + ha) - Taboo(a, StimulusCategory.Water) * 0.7f;

        if (c.Fields > 0 && c.Growth > 0.12f)
            s[(int)Activity.Farm] = 0.5f + food * 0.6f + 0.3f * a.Efficacy[(int)Domain.Farming] + MathF.Min(0.4f, c.Fields / (float)_members.Count);
        if (c.Herd > 0)
            s[(int)Activity.Herd] = 0.2f + food * 0.3f + 0.2f * a.Efficacy[(int)Domain.Hunting] + MathF.Min(0.4f, c.Herd / 20f);
        // Materials: wood, stone, ore and the rest, by how short the band is of them.
        float strong = 0.1f * a.Gene(Gene.Strength);
        // (A background chore: enough to keep stocks up, not enough to crowd out food and family.)
        s[(int)Activity.Chop] = -0.3f + 0.55f * ShortageOf(band, GLog, GWood) + strong + (Techs.Has(c.Tech, TechId.Woodcutting) ? 0.05f : -0.15f);
        if (Techs.Has(c.Tech, TechId.StoneTools))
            s[(int)Activity.Quarry] = -0.35f + 0.5f * ShortageOf(band, GStone, GFlint) + strong;
        if (Techs.Has(c.Tech, TechId.Copper) || Techs.Has(c.Tech, TechId.Mining))
            s[(int)Activity.Mine] = -0.4f + 0.55f * ShortageOf(band, Goods.Id("copper_ore"), Goods.Id("tin_ore"), Goods.Id("iron_ore"), Goods.Id("coal"), Goods.Id("gold_ore")) + strong;
        s[(int)Activity.Gather] = -0.3f + 0.5f * ShortageOf(band, GClay, GReed, GSand, GFiber, GSalt, GBamboo, GHerbs);

        float social = a.NeedOf(Need.Social);
        s[(int)Activity.Socialize] = 0.05f + social * (0.6f + 0.6f * rd) - (a.WithdrawnUntil > Day ? 1.2f : 0f);
        if (a.PartnerId < 0 && age is >= 16f and < 46f)
            s[(int)Activity.Court] = 0.35f + 0.25f * ns + 0.15f * a.Gene(Gene.Charisma) - food * 0.4f - (a.WithdrawnUntil > Day ? 0.8f : 0f);
        if (HasYoungChild(a)) s[(int)Activity.CareChild] = 0.25f + 0.35f * co + 0.3f * a.Emo(Emotion.Love);

        if (band.BuildJobs > 0)
        {
            float shelterNeed = (1f - band.Shelter) * (c.ColdAhead ? 0.9f : 0.35f);
            s[(int)Activity.Build] = 0.2f + shelterNeed + 0.2f * a.Efficacy[(int)Domain.Building] + MathF.Min(0.3f, band.BuildJobs * 0.08f);
        }
        s[(int)Activity.Craft] = 0.05f + 0.25f * a.Efficacy[(int)Domain.Crafting] + 0.6f * c.CraftNeed + (elder ? 0.15f : 0f)
                                 + (c.Temp < 5f && c.CraftNeed > 0f ? 0.3f : 0f);

        float creative = (0.6f * ns + 0.35f * a.Gene(Gene.Intellect)) * (a.Genome.Has(Quirk.Dreamer) ? 1.3f : 1f);
        s[(int)Activity.Experiment] = creative + 0.25f * food - 0.55f + (a.InspiredUntil > Day ? 0.6f : 0f) - 0.4f * Held(a, MemeKind.InnovationTaboo) - 0.1f * a.IsSuppressedCount();
        s[(int)Activity.Ritual] = st * (0.25f + 0.5f * Held(a, MemeKind.Ritual)) + 0.3f * (a.Emo(Emotion.Fear) + a.Emo(Emotion.Sadness)) - 0.25f;
        s[(int)Activity.Explore] = ns * (1f - ha) * (0.5f + c.Shortage) - 0.25f - Taboo(a, StimulusCategory.Outsiders) * 0.5f;
        if (CanTeach(a, band)) s[(int)Activity.Teach] = 0.1f + 0.3f * co + 0.2f * MathF.Min(a.Prestige, 1f);
        if (a.Emo(Emotion.Sadness) > 0.5f) s[(int)Activity.Mourn] = a.Emo(Emotion.Sadness) * 0.9f - 0.2f;

        return (Activity)Rng.Softmax(s, 0.15f + 0.2f * ns);
    }

    float MaxBeastFear(Agent a) =>
        MathF.Max(MathF.Max(a.Fear[(int)Stimulus.Wolf], a.Fear[(int)Stimulus.Bear]), MathF.Max(a.Fear[(int)Stimulus.BigCat], a.Fear[(int)Stimulus.Crocodile]));

    bool HasYoungChild(Agent a)
    {
        foreach (int id in a.Children)
            if (Living.TryGetValue(id, out var c) && c.Age(Day) < 6f) return true;
        return false;
    }

    // ------------------------------------------------------------------ doing

    Stimulus BeastHere(int cell) => World.Biome[cell] switch
    {
        Biome.Savanna or Biome.TropicalSeasonalForest or Biome.TropicalRainforest => Rng.Chance(0.7f) ? Stimulus.BigCat : Stimulus.Wolf,
        Biome.Wetland => Rng.Chance(0.6f) ? Stimulus.Crocodile : Stimulus.BigCat,
        Biome.BorealForest or Biome.Alpine or Biome.TemperateRainforest => Rng.Chance(0.5f) ? Stimulus.Bear : Stimulus.Wolf,
        _ => Rng.Chance(0.75f) ? Stimulus.Wolf : Stimulus.Bear,
    };

    void Hunt(Band band, in DayContext c)
    {
        int n = _hunters.Count;
        foreach (var h in _hunters) h.Site = c.PreySite;
        float skill = 0f;
        foreach (var h in _hunters)
            skill += 0.4f * h.Efficacy[(int)Domain.Hunting] + 0.2f * h.Gene(Gene.Strength) + 0.2f * h.Gene(Gene.Dexterity) + 0.1f * h.Gene(Gene.Perception);
        skill /= n;
        // Weapons: a spear or bow in hand makes the party far more likely to bring something down; dogs help.
        float tools = 0f;
        foreach (var h in _hunters) tools += ToolBonus(h, Activity.Hunt) - 1f;
        float tech = tools / n * 0.55f + (band.Herds.GetValueOrDefault("dog") > 0f ? 0.06f : 0f);

        // Predators may contest the hunt; each hunter faces them or flees.
        int fled = 0;
        if (Rng.Chance(MathF.Min(0.4f, c.Predators * 0.06f)))
        {
            var beast = BeastHere(band.Cell);
            foreach (var h in _hunters.ToArray())
                if (!Encounter(h, band, beast, n - 1, Domain.Hunting, h.NeedOf(Need.Hunger) * 0.6f + c.Shortage * 0.5f)) fled++;
        }
        int stayed = n - fled;
        if (stayed <= 0) return;

        float p = Math.Clamp(0.15f + 0.08f * MathF.Min(stayed, 6) + skill * 0.5f + tech, 0f, 0.92f) * MathF.Min(1f, c.PreyAvail * 1.6f);
        bool success = Rng.Chance(p);
        Agent? best = null;
        foreach (var h in _hunters)
        {
            if (!h.Alive) continue;
            Psychology.Practice(h, Domain.Hunting, success);
            h.Needs[(int)Need.Energy] += 0.2f;
            if (best == null || h.Efficacy[(int)Domain.Hunting] > best.Efficacy[(int)Domain.Hunting]) best = h;
        }
        Press(c.PreySite, 0.004f * stayed);
        if (!success) return;

        float meat = Take(1, c.PreySite, 3.2f * stayed + 2f);
        HuntYield(_hunters, c.PreySite, meat);
        MaybeCapture(band, c.PreySite);
        if (best != null && meat > 12f && Rng.Chance(0.25f))
        {
            Psychology.Praise(best, Domain.Hunting, 0.6f, Day);
            Interlocked.Increment(ref Stats.Year.Praises);
        }
    }

    /// <summary>Face a predator or flee. Returns false if the agent fled.</summary>
    bool Encounter(Agent a, Band band, Stimulus beast, int allies, Domain domain, float urgency)
    {
        var response = Fear.Decide(a, beast, urgency, allies, a.Efficacy[(int)domain], Rng);
        if (response == FearResponse.Avoid)
        {
            Fear.Avoided(a, allies > 0, Day);
            return false;
        }
        Interlocked.Increment(ref Stats.Year.Confrontations);
        float threat = beast switch { Stimulus.Bear => 0.22f, Stimulus.BigCat => 0.18f, Stimulus.Crocodile => 0.12f, _ => 0.05f };
        float power = 0.35f + 0.25f * a.Gene(Gene.Strength) + 0.15f * a.Gene(Gene.Dexterity) + 0.25f * a.Efficacy[(int)domain]
                      + 0.07f * MathF.Min(allies, 4) - threat
                      + WeaponPower(a)
                      + (a.Genome.Has(Quirk.Lucky) ? 0.05f : 0f);
        if (Rng.Chance(Math.Clamp(power, 0.1f, 0.95f)))
        {
            float fearBefore = a.EffectiveFear(beast);
            bool triumph = Fear.Overcame(a, beast, domain, Day);
            a.Prestige += 0.12f;
            Press(a.Site, 0.03f);
            if (triumph) Later(() =>
            {
                Stats.Year.FearsOvercome++;
                Chronicle.Add(new WorldEvent(Day, EventCategory.Danger, "fear_overcome",
                    $"{a.Name} of the {band.Name}, long afraid of the {Ids.Snake(beast).Replace('_', ' ')}, stood and drove one off.", fearBefore > 0.6f ? 0.4f : 0.28f, a.Id, band.Id));
                Chronicle.Moment(new LlmMoment(Day, "fear_overcome", a.Id, band.Id, new()
                {
                    ["stimulus"] = Ids.Snake(beast), ["fear_before"] = fearBefore.ToString("0.00"),
                    ["fear_after"] = a.EffectiveFear(beast).ToString("0.00"), ["allies"] = allies.ToString(),
                }));
            });
            return true;
        }

        // Lost the fight.
        a.Health -= Rng.Range(0.2f, 0.65f) * (1.2f - a.Gene(Gene.Constitution) * 0.4f) * (1f - Protection(a));
        a.Scars++;
        Fear.Defeated(a, beast, domain, allies > 0, Day);
        Psychology.Appraise(a, new Appraisal(EventCategory.Danger, "mauled", -1f, 0.9f, Agency.Nature, Threat: true, Detail: Ids.Snake(beast)), Day);
        foreach (int id in band.Members)
            if (id != a.Id && Living.TryGetValue(id, out var m) && m.Today == a.Today && Rng.Chance(0.6f))
                Fear.Observe(m, beast, 0.7f, Day);
        if (a.Health <= 0f) Kill(a, DeathCause.Predator, beast, $", killed by a {Ids.Snake(beast).Replace('_', ' ')}");
        return true;
    }

    void Resolve(Agent a, Band band, in DayContext c)
    {
        var w = World;
        int cell = band.Cell;
        a.Site = cell;
        float tired = a.NeedOf(Need.Energy) > 0.8f ? 0.7f : 1f;
        float childFactor = a.IsChild(Day) ? 0.4f : 1f;
        switch (a.Today)
        {
            case Activity.Forage:
            {
                float want = 3.0f * (0.6f + 0.4f * a.Efficacy[(int)Domain.Foraging]) * (0.8f + 0.4f * a.Gene(Gene.Perception))
                             * ToolBonus(a, Activity.Forage) * tired * childFactor * MathF.Max(0.3f, c.Growth + 0.2f);
                // The richest of a few places in the home range.
                int site = _land[Rng.Next(_land.Count)];
                for (int k = 0; k < 2; k++)
                {
                    int t = _land[Rng.Next(_land.Count)];
                    if (Left(0, t) / MathF.Max(1f, w.PlantK[t]) > Left(0, site) / MathF.Max(1f, w.PlantK[site])) site = t;
                }
                a.Site = site;
                float got = Take(0, site, want);
                ForageYield(a, site, got, c);
                Psychology.Practice(a, Domain.Foraging, got > 1.1f, 0.5f);
                a.Needs[(int)Need.Energy] += 0.12f;
                if (!a.IsChild(Day) && Rng.Chance(c.Predators * 0.006f))
                    Encounter(a, band, BeastHere(cell), 0, Domain.Foraging, a.NeedOf(Need.Hunger) * 0.5f);
                if (w.Flow[cell] > WorldGen.RiverFlow && a.Fear[(int)Stimulus.River] > 0f) Fear.SafeExposure(a, Stimulus.River);
                break;
            }
            case Activity.Fish:
            {
                float want = (Techs.Has(c.Tech, TechId.Fishing) ? 2f : 0.7f) * (0.6f + 0.4f * a.Efficacy[(int)Domain.Fishing])
                             * (Techs.Has(c.Tech, TechId.Boats) ? 1.3f : 1f) * ToolBonus(a, Activity.Fish) * tired;
                a.Site = c.FishSite;
                float got = Take(2, c.FishSite, want);
                FishYield(a, c.FishSite, got);
                Psychology.Practice(a, Domain.Fishing, got > 1f, 0.6f);
                a.Needs[(int)Need.Energy] += 0.1f;
                if (Rng.Chance(0.0004f * (1.2f - a.Efficacy[(int)Domain.Fishing])))
                {
                    Kill(a, DeathCause.Accident, Stimulus.River, ", drowned");
                    Interlocked.Increment(ref Stats.Year.Drownings);
                }
                else Fear.SafeExposure(a, Stimulus.River);
                break;
            }
            case Activity.Farm: FarmWork(a, band, c); break;
            case Activity.Herd: HerdWork(a, band, c); break;
            case Activity.Build:
                if (!BuildWork(a, band, c)) GatherMaterials(a, band, c);
                break;
            case Activity.Craft: CraftWork(a, band, c); break;
            case Activity.Chop: Chop(a, band, c); break;
            case Activity.Quarry: Quarry(a, band, c); break;
            case Activity.Mine: Mine(a, band, c); break;
            case Activity.Gather: GatherMaterials(a, band, c); break;
            case Activity.Socialize: Socialize(a, band); break;
            case Activity.Court: Later(() => { if (a.Alive) Court(a, band); }); break;   // may reach into other bands
            case Activity.CareChild: CareForChild(a, band); break;
            case Activity.Experiment:
            {
                var ctx = c;   // inventions can split bands and found peoples: global, so after the parallel phase
                Later(() => { if (a.Alive) Experiment(a, band, ctx); });
                break;
            }
            case Activity.Ritual: Ritual(a, band); break;
            case Activity.Teach: Teach(a, band); break;
            case Activity.Explore:
                a.Needs[(int)Need.Energy] += 0.18f;
                if (Rng.Chance(c.Predators * 0.02f)) Encounter(a, band, BeastHere(cell), 0, Domain.Hunting, 0.1f);
                a.Site = Around(cell, Math.Max(1, c.Range * 3));
                if (w.IsDryLand(a.Site)) ForageYield(a, a.Site, Take(0, a.Site, 0.6f), c);
                if (SeaRange(c.Tech) is > 0 and var range && Rng.Chance(0.015f * (0.5f + a.T(Trait.NoveltySeeking))))
                    SightShore(a, band, range);
                break;
            case Activity.Mourn:
                a.Emotions[(int)Emotion.Sadness] *= 0.95f;
                a.Needs[(int)Need.Social] -= 0.2f;
                break;
            case Activity.Play:
                a.Feel(Emotion.Joy, 0.1f);
                a.Needs[(int)Need.Social] -= 0.3f;
                Psychology.Practice(a, (Domain)Rng.Next(Ids.DomainCount), true, 0.3f);
                break;
            case Activity.Rest:
                a.Needs[(int)Need.Energy] -= 0.35f;
                break;
        }
    }

    // ------------------------------------------------------------------ food and bodies

    void ShareAndEat(Band band, in DayContext c)
    {
        bool sharingNormBand = false;
        foreach (var a in _members)
        {
            if (!a.Alive || a.IsChild(Day)) continue;
            float co = a.T(Trait.Cooperativeness) + 0.2f * Held(a, MemeKind.SharingNorm);
            float keep = 1f + (1f - co) * 3f;
            // Materials always go to the stores; food beyond what they keep, as generous as they are.
            Deposit(a, band, keep + MathF.Max(0f, a.Food - keep) * (1f - Math.Clamp(0.4f + 0.6f * co, 0f, 1f)));
            sharingNormBand |= co > 0.8f;
        }
        foreach (var a in _members) if (a.Alive && a.IsChild(Day)) Deposit(a, band, 2f);

        StoresUpkeep(band, c);
        float heat = c.Temp > 25f ? 1.6f : c.Temp > 12f ? 1f : c.Temp > 2f ? 0.5f : 0.15f;
        float efficiency = Techs.Has(c.Tech, TechId.Cooking) && band.HasFireToday ? 1.15f : 1f;

        // Children and the pregnant eat first from the common pool, then everyone else.
        for (int pass = 0; pass < 2; pass++)
            foreach (var a in _members)
            {
                if (!a.Alive) continue;
                bool priority = a.IsChild(Day) || a.Pregnant;
                if ((pass == 0) != priority) continue;
                if (a.Food > 0f) a.Pack.Spoil(0.5f * heat);
                float age = a.Age(Day);
                bool nursed = age < 1f && Living.TryGetValue(a.MotherId, out var mom) && mom.BandId == a.BandId;
                if (nursed) { a.Needs[(int)Need.Hunger] = MathF.Max(0f, a.NeedOf(Need.Hunger) - 0.3f); continue; }
                float need = (age < 4f ? 0.4f : a.IsChild(Day) ? 0.7f : 1f) * (a.Pregnant ? 1.15f : 1f)
                             * (NursingInfant(a) ? 1.25f : 1f) * (a.Genome.Has(Quirk.Glutton) ? 1.15f : 1f) / efficiency;
                float eaten = a.Food > 0f ? a.Pack.TakeFood(need) : 0f;
                if (eaten < need) eaten += band.Stores.TakeFood(need - eaten);
                if (eaten < need) eaten += FamilyFeeds(a, need - eaten);
                float f = eaten / need;
                ref float hunger = ref a.Needs[(int)Need.Hunger];
                hunger = Math.Clamp(hunger + 0.12f * (1f - f) - 0.3f * f, 0f, 1f);
            }
    }

    bool NursingInfant(Agent mother)
    {
        if (mother.Sex != Sex.Female || Day - mother.LastBirthDay > SimConfig.DaysPerYear) return false;
        foreach (int id in mother.Children)
            if (Living.TryGetValue(id, out var c) && c.Age(Day) < 1f) return true;
        return false;
    }

    /// <summary>Parents give their children everything they carry; partners share only their surplus.</summary>
    float FamilyFeeds(Agent a, float lacking)
    {
        float got = 0f;
        Span<int> family = stackalloc int[3] { a.MotherId, a.FatherId, a.PartnerId };
        bool child = a.IsChild(Day);
        foreach (int id in family)
        {
            if (got >= lacking || id < 0 || !Living.TryGetValue(id, out var k) || k.BandId != a.BandId) continue;
            bool parent = id != a.PartnerId;
            if (parent && !child) continue;
            float spare = parent ? k.Food : MathF.Max(0f, k.Food - 1f);
            float t = MathF.Min(spare, lacking - got);
            if (t > 0f) got += k.Pack.TakeFood(t);
        }
        return got;
    }

    void Body(Agent a, Band band, in DayContext c)
    {
        float age = a.Age(Day);
        float con = a.Gene(Gene.Constitution);
        DeathCause worst = DeathCause.None;
        float damage = 0f;

        // Starvation
        float hunger = a.NeedOf(Need.Hunger);
        if (hunger > 0.75f) { float d = 0.025f * (hunger - 0.6f) / 0.4f * (1.3f - con); damage += d; worst = DeathCause.Starvation; }

        // Thirst
        ref float thirst = ref a.Needs[(int)Need.Thirst];
        thirst = c.Water < 0.25f ? MathF.Min(1f, thirst + (0.25f - c.Water) * 0.6f) : MathF.Max(0f, thirst - 0.3f);
        if (c.Temp > 36f) thirst = MathF.Min(1f, thirst + 0.08f);
        if (thirst > 0.8f) { damage += 0.03f; worst = DeathCause.Thirst; }

        // Cold
        // Warmth: what they wear, the hearth (if there is wood for it) and their dwelling.
        float comfort = 10f - Warmth(a) - (band.HasFireToday && Techs.Has(c.Tech, TechId.ControlledFire) ? 7f : 0f)
                        - band.ShelterWarmth * band.Shelter - (a.Genome.Has(Quirk.Hardy) ? 5f : 0f);
        float cold = MathF.Max(0f, (comfort - c.Temp) / 12f) * (age < 4f || age > 65f ? 1.5f : 1f);
        a.Needs[(int)Need.Warmth] = Math.Clamp(cold, 0f, 1f);
        if (cold > 0.8f) { float d = 0.025f * (cold - 0.8f) * (1.3f - con); damage += d; if (d > 0.01f) worst = DeathCause.Cold; }

        // Disease
        if (a.Sick)
        {
            float d = 0.02f * band.PlagueSeverity * (a.Genome.Has(Quirk.Frail) ? 1.5f : 1f) * (BandKnowsCached(c, TechId.Herbalism) ? 0.6f : 1f) * (1.3f - con);
            damage += d; worst = DeathCause.Disease;
            if (Day >= a.SickUntil) a.Sick = false;
        }

        if (damage > 0f) a.Health -= damage;
        else if (hunger < 0.4f && thirst < 0.4f && cold < 0.5f)
            a.Health = MathF.Min(1f, a.Health + 0.01f + (BandKnowsCached(c, TechId.Herbalism) ? 0.005f : 0f));

        WearGear(a, a.Today);

        // Energy, social, safety needs
        ref float energy = ref a.Needs[(int)Need.Energy];
        energy = Math.Clamp(energy - 0.13f, 0f, 1f);
        ref float social = ref a.Needs[(int)Need.Social];
        social = Math.Clamp(social + 0.035f * (0.4f + a.T(Trait.RewardDependence)) - (a.PartnerId >= 0 ? 0.015f : 0f), 0f, 1f);
        a.Needs[(int)Need.Safety] = Math.Clamp(a.Emo(Emotion.Fear) * 0.7f + c.Predators * 0.2f * a.T(Trait.HarmAvoidance)
                                                + MathF.Max(0f, 6 - _members.Count) / 6f * 0.3f, 0f, 1f);
        if (hunger > 0.6f) { a.Feel(Emotion.Sadness, 0.02f); a.Feel(Emotion.Anger, 0.02f * (1f - a.T(Trait.Cooperativeness))); }

        if (a.Health <= 0f) { Kill(a, worst == DeathCause.None ? DeathCause.Disease : worst); return; }

        // Ageing (Gompertz) and childhood illness.
        // Forager-like: ~18% infant mortality, risky early childhood, adult hazard doubling every ~9 years.
        // Crowding breeds illness, so large bands lose more (density dependence keeps growth in check).
        // Roofs, wells, herbal lore, medicine and sanitation let a settled village grow without as much sickness.
        float care = 1f + band.Shelter * 0.5f + band.WaterBonus * 0.5f + (BandKnowsCached(c, TechId.Herbalism) ? 0.2f : 0f)
                     + (BandKnowsCached(c, TechId.Medicine) ? 0.4f : 0f) + (BandKnowsCached(c, TechId.Sanitation) ? 0.6f : 0f) + (BandKnowsCached(c, TechId.Hospitals) ? 0.8f : 0f);
        float crowding = 0.75f + _members.Count / (45f * care);
        float yearly = age < 1f ? 0.16f * (1.3f - con) * crowding
                     : age < 5f ? 0.04f * (1.3f - con) * crowding
                     : age < 15f ? 0.008f * crowding
                     : 0.008f * MathF.Exp(0.075f * (age - 30f)) * (1.6f - a.Gene(Gene.Longevity)) * (a.Genome.Has(Quirk.Frail) ? 1.3f : 1f)
                       + (age < 50f ? 0.004f * (crowding - 1f) : 0f);
        if (Rng.Chance(yearly / SimConfig.DaysPerYear)) Kill(a, age < 50f ? DeathCause.Disease : DeathCause.OldAge);
    }

    static bool BandKnowsCached(in DayContext c, TechId t) => Techs.Has(c.Tech, t);

    void Reproduce(Agent a, Band band, in DayContext c)
    {
        if (a.Pregnant)
        {
            if (Day >= a.DueDay) Later(() => GiveBirth(a, band));
            return;
        }
        if (a.Sex != Sex.Female || a.PartnerId < 0) return;
        float age = a.Age(Day);
        // Settled farmers wean earlier than foragers on the move, so births come closer together.
        float spacing = band.Sedentary && band.FoodPool > _members.Count * 5f ? 2.2f : 3.0f;
        if (age < 16f || age > 45f || Day - a.LastBirthDay < spacing * SimConfig.DaysPerYear) return;
        if (!Living.TryGetValue(a.PartnerId, out var partner) || partner.BandId != a.BandId) return;
        float nourished = 1f - a.NeedOf(Need.Hunger);
        float crowd = (Living.Count > Config.SoftMaxAgents ? 0.2f : 1f) * (band.FertileBlessingUntil > Day ? 2.5f : 1f);
        float p = 0.0034f * (0.5f + a.Gene(Gene.Fertility)) * (0.5f + partner.Gene(Gene.Fertility)) * nourished * nourished * crowd;
        if (Rng.Chance(p))
        {
            a.Pregnant = true;
            a.DueDay = Day + 270;
            a.PregnancyFatherId = partner.Id;
        }
    }

    void ComeOfAge(Agent a, Band band)
    {
        // Children pick up what the grown-ups around them do every day.
        foreach (var def in Techs.All)
        {
            float share = BandShare(band, def.Id);
            if (!Techs.CanLearn(a.Knows, def.Id)) continue;
            if (share >= 0.5f ? Rng.Chance(0.85f) : share >= 0.15f && Rng.Chance(0.3f)) a.Knows |= Techs.Bit(def.Id);
        }
    }
}
