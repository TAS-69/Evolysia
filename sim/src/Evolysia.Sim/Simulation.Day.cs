using Evolysia.Sim.Agents;
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
        public ulong Tech;
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
            PlantAvail = Avail(w.PlantStock[cell], w.PlantK[cell]),
            PreyAvail = Avail(w.PreyStock[cell], w.PreyK[cell]),
            FishAvail = Avail(w.FishStock[cell], w.FishK[cell]),
            Predators = w.Predators[cell] * Config.PredatorDanger,
        };
        ctx.Water = w.WaterAccess[cell] * (0.5f + 0.5f * MathF.Min(ctx.Wet, 1f));
        float stored = band.FoodPool;
        foreach (var m in _members) { ctx.Tech |= m.Knows; stored += m.Food; }
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

        if (World.FishK[band.Cell] > 0f)
            s[(int)Activity.Fish] = 0.05f + food * 0.8f * c.FishAvail * (Techs.Has(c.Tech, TechId.Fishing) ? 1f : 0.35f)
                                    + 0.3f * a.Efficacy[(int)Domain.Fishing]
                                    - a.EffectiveFear(Stimulus.River) * (0.4f + ha) - Taboo(a, StimulusCategory.Water) * 0.7f;

        if (band.Sedentary && Techs.Has(c.Tech, TechId.Agriculture) && c.Growth > 0.15f)
            s[(int)Activity.Farm] = 0.5f + food * 0.6f + 0.3f * a.Efficacy[(int)Domain.Farming];
        if (Techs.Has(c.Tech, TechId.Domestication))
            s[(int)Activity.Herd] = 0.3f + food * 0.4f + 0.2f * a.Efficacy[(int)Domain.Hunting];

        float social = a.NeedOf(Need.Social);
        s[(int)Activity.Socialize] = 0.05f + social * (0.6f + 0.6f * rd) - (a.WithdrawnUntil > Day ? 1.2f : 0f);
        if (a.PartnerId < 0 && age is >= 16f and < 46f)
            s[(int)Activity.Court] = 0.35f + 0.25f * ns + 0.15f * a.Gene(Gene.Charisma) - food * 0.4f - (a.WithdrawnUntil > Day ? 0.8f : 0f);
        if (HasYoungChild(a)) s[(int)Activity.CareChild] = 0.25f + 0.35f * co + 0.3f * a.Emo(Emotion.Love);

        float shelterNeed = (1f - band.Shelter) * (c.ColdAhead ? 0.9f : 0.25f);
        s[(int)Activity.Build] = shelterNeed * (Techs.Has(c.Tech, TechId.Shelter) ? 1f : 0.5f) + 0.2f * a.Efficacy[(int)Domain.Building] - 0.1f;
        s[(int)Activity.Craft] = 0.05f + 0.25f * a.Efficacy[(int)Domain.Crafting] + (c.Temp < 5f && !Techs.Has(c.Tech, TechId.HideClothing) ? 0.3f : 0f);

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
        float skill = 0f;
        foreach (var h in _hunters)
            skill += 0.4f * h.Efficacy[(int)Domain.Hunting] + 0.2f * h.Gene(Gene.Strength) + 0.2f * h.Gene(Gene.Dexterity) + 0.1f * h.Gene(Gene.Perception);
        skill /= n;
        float tech = (Techs.Has(c.Tech, TechId.Spear) ? 0.12f : 0f) + (Techs.Has(c.Tech, TechId.Bow) ? 0.15f : 0f);

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
        World.HumanPressure[band.Cell] = MathF.Min(1f, World.HumanPressure[band.Cell] + 0.004f * stayed);
        if (!success) return;

        float meat = Ecology.Harvest(World.PreyStock, World.PreyK, band.Cell, 3.2f * stayed + 2f);
        foreach (var h in _hunters) if (h.Alive) h.Food += meat / stayed;
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
                      + (Techs.Has(a.Knows, TechId.Spear) ? 0.12f : 0f) + (Techs.Has(a.Knows, TechId.Bow) ? 0.08f : 0f)
                      + (a.Genome.Has(Quirk.Lucky) ? 0.05f : 0f);
        if (Rng.Chance(Math.Clamp(power, 0.1f, 0.95f)))
        {
            float fearBefore = a.EffectiveFear(beast);
            bool triumph = Fear.Overcame(a, beast, domain, Day);
            a.Prestige += 0.12f;
            World.HumanPressure[band.Cell] = MathF.Min(1f, World.HumanPressure[band.Cell] + 0.03f);
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
        a.Health -= Rng.Range(0.2f, 0.65f) * (1.2f - a.Gene(Gene.Constitution) * 0.4f);
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
        float tired = a.NeedOf(Need.Energy) > 0.8f ? 0.7f : 1f;
        float childFactor = a.IsChild(Day) ? 0.4f : 1f;
        switch (a.Today)
        {
            case Activity.Forage:
            {
                float want = 3.0f * (0.6f + 0.4f * a.Efficacy[(int)Domain.Foraging]) * (0.8f + 0.4f * a.Gene(Gene.Perception))
                             * (Techs.Has(c.Tech, TechId.Baskets) ? 1.15f : 1f) * tired * childFactor * MathF.Max(0.3f, c.Growth + 0.2f);
                float got = Ecology.Harvest(w.PlantStock, w.PlantK, cell, want);
                a.Food += got;
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
                             * (Techs.Has(c.Tech, TechId.Boats) ? 1.3f : 1f) * tired;
                float got = Ecology.Harvest(w.FishStock, w.FishK, cell, want);
                a.Food += got;
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
            case Activity.Farm:
            {
                float got = 2.3f * (0.6f + 0.4f * a.Efficacy[(int)Domain.Farming]) * MathF.Min(1.2f, c.Growth + 0.2f) * MathF.Min(w.YearRain[cell], 1.1f) * tired;
                band.FoodPool += got * 0.6f;
                a.Food += got * 0.4f;
                Psychology.Practice(a, Domain.Farming, got > 1.5f, 0.5f);
                a.Needs[(int)Need.Energy] += 0.16f;
                break;
            }
            case Activity.Herd:
                a.Food += 1.3f * (0.7f + 0.3f * a.Efficacy[(int)Domain.Hunting]) * tired;
                a.Needs[(int)Need.Energy] += 0.1f;
                break;
            case Activity.Build:
                band.Shelter = MathF.Min(Techs.Has(c.Tech, TechId.Shelter) ? 1f : 0.35f,
                    band.Shelter + 0.012f * (0.5f + a.Efficacy[(int)Domain.Building]));
                Psychology.Practice(a, Domain.Building, true, 0.5f);
                a.Needs[(int)Need.Energy] += 0.18f;
                break;
            case Activity.Craft:
                Psychology.Practice(a, Domain.Crafting, Rng.Chance(0.6f), 0.5f);
                a.Needs[(int)Need.Energy] += 0.08f;
                break;
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
                a.Food += Ecology.Harvest(w.PlantStock, w.PlantK, cell, 0.6f);
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
            if (a.Food > keep)
            {
                float give = (a.Food - keep) * Math.Clamp(0.4f + 0.6f * co, 0f, 1f);
                a.Food -= give; band.FoodPool += give;
            }
            sharingNormBand |= co > 0.8f;
        }

        float spoil = c.Temp > 20f ? 0.025f : c.Temp > 5f ? 0.01f : 0.003f;
        if (Techs.Has(c.Tech, TechId.FoodStorage)) spoil *= 0.35f;
        if (Techs.Has(c.Tech, TechId.Pottery)) spoil *= 0.7f;
        band.FoodPool *= 1f - spoil;
        float efficiency = Techs.Has(c.Tech, TechId.Cooking) ? 1.15f : 1f;

        // Children and the pregnant eat first from the common pool, then everyone else.
        for (int pass = 0; pass < 2; pass++)
            foreach (var a in _members)
            {
                if (!a.Alive) continue;
                bool priority = a.IsChild(Day) || a.Pregnant;
                if ((pass == 0) != priority) continue;
                a.Food *= 1f - spoil;
                float age = a.Age(Day);
                bool nursed = age < 1f && Living.TryGetValue(a.MotherId, out var mom) && mom.BandId == a.BandId;
                if (nursed) { a.Needs[(int)Need.Hunger] = MathF.Max(0f, a.NeedOf(Need.Hunger) - 0.3f); continue; }
                float need = (age < 4f ? 0.4f : a.IsChild(Day) ? 0.7f : 1f) * (a.Pregnant ? 1.15f : 1f)
                             * (NursingInfant(a) ? 1.25f : 1f) * (a.Genome.Has(Quirk.Glutton) ? 1.15f : 1f) / efficiency;
                float eaten = MathF.Min(a.Food, need);
                a.Food -= eaten;
                if (eaten < need) { float t = MathF.Min(band.FoodPool, need - eaten); band.FoodPool -= t; eaten += t; }
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
            k.Food -= t; got += t;
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
        float comfort = 10f - (Techs.Has(c.Tech, TechId.HideClothing) ? 10f : 0f) - (Techs.Has(c.Tech, TechId.Weaving) ? 3f : 0f)
                        - (Techs.Has(c.Tech, TechId.ControlledFire) ? 7f : 0f) - band.Shelter * 10f - (a.Genome.Has(Quirk.Hardy) ? 5f : 0f);
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
        float crowding = 0.75f + _members.Count / 45f;
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
        if (age < 16f || age > 45f || Day - a.LastBirthDay < 3.0f * SimConfig.DaysPerYear) return;
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
            if (share >= 0.5f ? Rng.Chance(0.85f) : share >= 0.15f && Rng.Chance(0.3f)) a.Knows |= Techs.Bit(def.Id);
        }
    }
}
