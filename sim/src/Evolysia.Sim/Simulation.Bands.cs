using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Evolysia.Sim.Psych;
using Evolysia.Sim.Society;
using Evolysia.Sim.World;

namespace Evolysia.Sim;

public sealed partial class Simulation
{
    // ------------------------------------------------------------------ every 15 days

    void BandDecisions()
    {
        foreach (var band in Bands.Values.ToArray())
        {
            if (band.Disbanded || band.Members.Count == 0) continue;
            EconomyUpkeep(band);
            for (int c = 0; c < Ids.CategoryCount; c++) band.PlaceDread[c] *= 0.97f;

            if (!band.Sedentary && BandShare(band, TechId.Agriculture) >= 0.3f && World.Growth(band.Cell, Day) > 0f && OpenLand(band.Cell, band.Id)
                && World.Biome[band.Cell] is Biome.TemperateGrassland or Biome.TemperateForest or Biome.Savanna
                    or Biome.TropicalSeasonalForest or Biome.Shrubland or Biome.Wetland)
            {
                band.Sedentary = true;
                bool first = Chronicle.First("settled");
                Chronicle.Add(new WorldEvent(Day, EventCategory.Discovery, "settled",
                    $"The {band.Name} plant fields and stop wandering{(first ? " - the first village in the world" : "")}.", first ? 0.9f : 0.4f, -1, band.Id));
            }

            MaybeMigrate(band);
            MaybeSplit(band);
            if (!band.Disbanded) MaybeJoinNeighbour(band);
        }
    }

    // People per block of cells (a block is a home range wide), cached while no band moves, splits
    // or merges (CellScore asks a few dozen times per band).
    Dictionary<int, int>? _cellPeople;
    bool _cellPeopleValid;

    int BlockSize => Math.Max(1, (int)(World.Scale * 0.8f));
    int BlockOf(int cell) => World.Y(cell) / BlockSize * (World.W / BlockSize + 1) + World.X(cell) / BlockSize;

    /// <summary>People living close enough to share <paramref name="cell"/>'s home range.</summary>
    int PeopleNear(int cell)
    {
        if (!_cellPeopleValid || _cellPeople == null)
        {
            _cellPeople ??= new Dictionary<int, int>();
            _cellPeople.Clear();
            foreach (var b in Bands.Values) _cellPeople[BlockOf(b.Cell)] = _cellPeople.GetValueOrDefault(BlockOf(b.Cell)) + b.Members.Count;
            _cellPeopleValid = true;
        }
        int bs = BlockSize, reach = bs == 1 ? 0 : 1, sum = 0;
        int bx = World.X(cell) / bs, by = World.Y(cell) / bs, blocksX = World.W / bs + 1;
        for (int dy = -reach; dy <= reach; dy++)
            for (int dx = -reach; dx <= reach; dx++)
            {
                int x = ((bx + dx) % blocksX + blocksX) % blocksX, y = by + dy;
                if (y < 0) continue;
                sum += _cellPeople.GetValueOrDefault(y * blocksX + x);
            }
        return sum;
    }

    /// <summary>
    /// Food around a cell, per original-cell area: the home range sampled at its centre and eight
    /// points round it (the cell alone when ranges are a single cell).
    /// </summary>
    float RangeFood(int cell, bool fishing)
    {
        var w = World;
        int r = Math.Max(1, (int)(w.Scale * 0.8f));
        if (w.Scale < 1.5f) return CellFood(cell, fishing);
        float sum = 0f; int n = 0;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int y = Math.Clamp(w.Y(cell) + dy * r, 0, w.H - 1);
                sum += CellFood(w.Index(w.X(cell) + dx * r, y), fishing);
                n++;
            }
        return sum / n / w.CellArea;
    }

    float CellFood(int c, bool fishing)
    {
        var w = World;
        return w.PlantStock[c] * 0.6f + w.PlantK[c] * 0.4f * MathF.Max(w.Growth(c, Day + 30), 0.2f)
               + w.PreyStock[c] * 0.6f + w.FishStock[c] * (fishing ? 0.8f : 0.2f);
    }

    /// <summary>Candidate cells within <paramref name="radius"/>: all of them when few, else a fixed-size sample.</summary>
    IEnumerable<int> Candidates(int cell, int radius, int samples)
    {
        int x0 = World.X(cell), y0 = World.Y(cell);
        if ((2 * radius + 1) * (2 * radius + 1) <= samples + 1)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                int y = y0 + dy;
                if (y < 0 || y >= World.H) continue;
                for (int dx = -radius; dx <= radius; dx++) yield return World.Index(x0 + dx, y);
            }
            yield break;
        }
        for (int k = 0; k < samples; k++)
        {
            int y = y0 + Rng.Range(-radius, radius + 1);
            if (y < 0 || y >= World.H) continue;
            yield return World.Index(x0 + Rng.Range(-radius, radius + 1), y);
        }
    }

    internal void BandsChanged() => _cellPeopleValid = false;

    float CellScore(Band band, int cell, Agent? leader)
    {
        var w = World;
        if (!w.IsLand(cell) || w.Biome[cell] == Biome.Lake) return float.NegativeInfinity;
        bool fishing = BandKnows(band, TechId.Fishing);
        float food = RangeFood(cell, fishing);
        bool home = BlockOf(cell) == BlockOf(band.Cell) || w.Distance(cell, band.Cell) <= BlockSize;
        int crowd = band.Members.Count + PeopleNear(cell) - (home ? band.Members.Count : 0);
        float perCapita = food / MathF.Max(1f, crowd * 40f);
        float ha = leader?.T(Trait.HarmAvoidance) ?? 0.5f;
        float cold = MathF.Max(0f, 5f - (w.TempMean[cell] - w.TempAmp[cell])) / 20f;
        float dread = cell == band.Cell ? band.PlaceDread.Sum() * 0.5f : 0f;
        float dry = MathF.Max(0f, 0.45f - w.WaterAccess[cell]) * 3f;
        return perCapita + 0.4f * w.WaterAccess[cell] - dry - w.Predators[cell] * 0.3f * ha - cold - dread;
    }

    void MaybeMigrate(Band band)
    {
        if (band.Sedentary && band.Members.Count > 0)
        {
            // Farmers only abandon their village in a real crisis.
            if (AverageHunger(band) < 0.7f) return;
        }
        var leader = Living.GetValueOrDefault(band.LeaderId);
        float here = CellScore(band, band.Cell, leader);
        float hunger = AverageHunger(band);
        float thirst = 0f;
        foreach (int id in band.Members) if (Living.TryGetValue(id, out var m)) thirst = MathF.Max(thirst, m.NeedOf(Need.Thirst));
        float ns = leader?.T(Trait.NoveltySeeking) ?? 0.5f, ha = leader?.T(Trait.HarmAvoidance) ?? 0.5f;

        int radius = World.Cells(2), best = band.Cell;
        float bestScore = here;
        foreach (int c in Candidates(band.Cell, radius, 40))
        {
            if (c == band.Cell) continue;
            float s = CellScore(band, c, leader) - 0.05f * World.Distance(band.Cell, c) / World.Scale + Rng.Normal(0f, 0.05f) * ns;
            if (s > bestScore) { bestScore = s; best = c; }
        }
        float threshold = 1.12f + 0.3f * ha - 0.25f * ns;
        bool desperate = hunger > 0.5f || thirst > 0.5f;
        if (desperate && leader != null && BestShore(band, leader) is { } shore && shore.score > MathF.Max(bestScore, here) + 0.1f)
        {
            Sail(band, leader, null, shore.cell);   // the whole band takes to the sea
            return;
        }
        if (best == band.Cell) return;
        if (!desperate && bestScore < here * threshold + 0.05f) return;
        if (Day - band.LastMoveDay < 45 && !desperate) return;

        LeaveCamp(band, best);
        band.Cell = best;
        BandsChanged();
        band.LastMoveDay = Day;
        Array.Clear(band.PlaceDread);
        band.Shelter = 0f;
        Stats.Year.Migrations++;
        ArrivedAt(band);
        if (Chronicle.First($"biome:{World.Biome[best]}"))
            Chronicle.Add(new WorldEvent(Day, EventCategory.Discovery, "new_land",
                $"The {band.Name} are the first to enter the {Ids.Snake(World.Biome[best]).Replace('_', ' ')}.", 0.5f, -1, band.Id));
    }

    float AverageHunger(Band band)
    {
        float sum = 0f; int n = 0;
        foreach (int id in band.Members) if (Living.TryGetValue(id, out var m)) { sum += m.NeedOf(Need.Hunger); n++; }
        return n == 0 ? 0f : sum / n;
    }

    int Capacity(Band band) =>
        32 + (BandKnows(band, TechId.FoodStorage) ? 10 : 0) + (band.Sedentary ? 50 : 0) + (BandKnows(band, TechId.Pottery) ? 8 : 0);

    /// <summary>
    /// A hungry band sheds a family group to open new land, led by someone restless and unafraid.
    /// This is how people spread across the planet.
    /// </summary>
    bool MaybeBud(Band band)
    {
        // Wanderers bud when hungry. Villages bud when they outgrow their land: a crowded or hungry
        // village sends a party off to found a new village on good land nearby - how farming spreads.
        bool village = band.Sedentary;
        if (village ? band.Members.Count < 55 || (band.Stress < 0.05f && band.Members.Count < 75) || Day - band.FoundedDay < 3 * SimConfig.DaysPerYear
                    : band.Stress < 0.1f || band.Members.Count < 22 || Day - band.FoundedDay < 360) return false;
        Agent? pioneer = null;
        foreach (int id in band.Members)
            if (Living.TryGetValue(id, out var m) && m.IsAdult(Day) && m.Id != band.LeaderId && m.Age(Day) is > 18f and < 45f
                && (pioneer == null || Pioneering(m) > Pioneering(pioneer))) pioneer = m;
        if (pioneer == null || !Rng.Chance(0.25f + Pioneering(pioneer) * 0.5f)) return false;

        int target = -1; float bestScore = float.MinValue;
        foreach (int c in Candidates(band.Cell, World.Cells(village ? 4 : 3), 48))
        {
            if (World.Distance(c, band.Cell) < World.Cells(village ? 2 : 1)) continue;   // far enough to be new land (clear of the village's fields)
            if (village && (!FarmLand(c) || !OpenLand(c))) continue;
            float s = CellScore(band, c, pioneer) + Rng.Normal(0f, 0.05f);
            if (s > bestScore) { bestScore = s; target = c; }
        }
        var sea = village ? null : BestShore(band, pioneer);
        if ((target < 0 || float.IsNegativeInfinity(bestScore)) && sea == null) return false;

        var followers = new List<Agent>();
        foreach (int id in band.Members)
            if (Living.TryGetValue(id, out var m) && m.IsAdult(Day) && m != pioneer && m.Id != band.LeaderId
                && m.AffinityTo(pioneer.Id) + 0.3f * m.T(Trait.NoveltySeeking) + Rng.Normal(0f, 0.2f) > 0.35f
                && followers.Count < band.Members.Count / 3)
                followers.Add(m);
        if (followers.Count < 3) return false;
        if (sea is { } shore && (target < 0 || shore.score > bestScore + 0.1f))
        {
            Stats.Year.Splits++;
            Sail(band, pioneer, followers, shore.cell);
            return true;
        }
        if (target < 0) return false;
        var nb = Split(band, pioneer, followers, band.CultureId, target);
        Stats.Year.Splits++;
        if (village)
        {
            nb.Sedentary = true;   // they take seed, tools and know-how: a new village
            Chronicle.Add(new WorldEvent(Day, EventCategory.Discovery, "new_village",
                $"{pioneer.Name} leads {nb.Members.Count} of the {band.Name} to found a new village, the {nb.Name}.", 0.3f, pioneer.Id, nb.Id));
        }
        else
            Chronicle.Add(new WorldEvent(Day, EventCategory.Discovery, "budding",
                $"Hungry times: {pioneer.Name} leads {nb.Members.Count} of the {band.Name} to new land, and they become the {nb.Name}.", 0.35f, pioneer.Id, nb.Id));
        return true;
    }

    /// <summary>Land farmers would settle: the biomes agriculture takes to, dry, with water near.</summary>
    bool FarmLand(int c) =>
        World.IsDryLand(c) && World.WaterAccess[c] > 0.4f && World.Biome[c] is Biome.TemperateGrassland or Biome.TemperateForest or Biome.Savanna
            or Biome.TropicalSeasonalForest or Biome.Shrubland or Biome.Wetland or Biome.TemperateRainforest or Biome.BorealForest;

    static float Pioneering(Agent a) => a.T(Trait.NoveltySeeking) * (1.2f - a.T(Trait.HarmAvoidance)) + 0.2f * a.T(Trait.SelfDirectedness);

    void MaybeSplit(Band band)
    {
        if (MaybeBud(band)) return;
        // Too big to feed or too bitter to stay together: a rival faction leaves.
        float tension = 0f; int pairs = 0;
        var leader = Living.GetValueOrDefault(band.LeaderId);
        if (leader == null) return;
        foreach (int id in band.Members)
            if (Living.TryGetValue(id, out var m) && m.IsAdult(Day) && id != leader.Id)
            {
                float aff = m.AffinityTo(leader.Id);
                tension += MathF.Max(0f, -aff) + (m.Relations.TryGetValue(leader.Id, out var r) && r.Grudge ? 0.5f : 0f);
                pairs++;
            }
        tension = pairs == 0 ? 0f : tension / pairs;
        bool tooBig = band.Members.Count > Capacity(band);
        if (!tooBig && !(tension > 0.25f && band.Members.Count >= 12)) return;

        // The rival: most prestigious adult who isn't fond of the leader.
        Agent? rival = null;
        foreach (int id in band.Members)
            if (Living.TryGetValue(id, out var m) && m.IsAdult(Day) && m.Id != leader.Id && m.Age(Day) > 20f
                && (rival == null || m.Prestige - m.AffinityTo(leader.Id) > rival.Prestige - rival.AffinityTo(leader.Id)))
                rival = m;
        if (rival == null) return;

        var followers = new List<Agent>();
        foreach (int id in band.Members)
            if (Living.TryGetValue(id, out var m) && m.IsAdult(Day) && m != rival && m != leader
                && m.AffinityTo(rival.Id) + Rng.Normal(0f, 0.2f) > m.AffinityTo(leader.Id))
                followers.Add(m);
        if (followers.Count < 3) return;
        if (!tooBig && Rng.Chance(0.5f)) return;

        var newBand = Split(band, rival, followers, band.CultureId);
        Chronicle.Add(new WorldEvent(Day, EventCategory.Social, "band_split",
            $"{rival.Name} leads {newBand.Members.Count} people away from the {band.Name} to found the {newBand.Name}{(tooBig ? "" : ", tired of " + leader.Name)}.",
            0.4f, rival.Id, newBand.Id));
        Stats.Year.Splits++;
    }

    /// <summary>Moves founder + followers (and their partners and young children) to a new band nearby.</summary>
    Band Split(Band from, Agent founder, List<Agent> followers, int cultureId, int cell = -1, bool announce = true)
    {
        BandsChanged();
        if (cell < 0) cell = NearbyLand(from.Cell, World.Cells(1), World.Cells(2));
        var nb = NewBand(cell, cultureId, from.Id);
        var moving = new HashSet<int> { founder.Id };
        foreach (var f in followers) moving.Add(f.Id);
        foreach (var id in moving.ToArray())
        {
            if (!Living.TryGetValue(id, out var m)) continue;
            if (m.PartnerId >= 0) moving.Add(m.PartnerId);
            foreach (int c in m.Children) if (Living.TryGetValue(c, out var ch) && ch.IsChild(Day)) moving.Add(c);
        }
        foreach (int id in moving)
        {
            if (!Living.TryGetValue(id, out var m) || m.BandId != from.Id) continue;
            from.Members.Remove(id);
            AddMember(nb, id);
            m.BandId = nb.Id;
            m.CultureId = cultureId;
        }
        // The leavers take their share of the stores.
        from.Stores.MoveFraction(nb.Stores, nb.Members.Count / (float)Math.Max(1, nb.Members.Count + from.Members.Count));
        nb.LeaderId = founder.Id;
        nb.Shelter = 0.05f;
        from.Relations[nb.Id] = -0.2f; nb.Relations[from.Id] = -0.2f;
        if (from.LeaderId == -1 || !from.Members.Contains(from.LeaderId)) ElectLeader(from);
        if (from.Members.Count == 0) Disband(from);
        if (announce) ArrivedAt(nb);
        return nb;
    }

    /// <summary>A rejected inventor or prophet walks out with the people who believed in them.</summary>
    void Schism(Agent founder, Band band, List<Agent> allies, string kind, string what, TechId? tech, int memeId)
    {
        var parent = Cultures[band.CultureId];
        var culture = allies.Count >= 5 && Rng.Chance(0.5f) ? NewCulture(parent) : parent;
        var nb = Split(band, founder, allies.Where(x => x.Alive).ToList(), culture.Id);
        foreach (int id in nb.Members)
            if (Living.TryGetValue(id, out var m))
            {
                if (tech is { } t && (m == founder || Rng.Chance(0.6f))) m.Knows |= Techs.Bit(t);
                if (Memes.ContainsKey(memeId)) m.Memes[memeId] = MathF.Max(m.Memes.GetValueOrDefault(memeId), 0.6f);
                m.Belonging = 0.75f;
            }
        Stats.Year.Schisms++;
        Chronicle.Add(new WorldEvent(Day, EventCategory.Social, "schism",
            $"{founder.Name} leaves the {band.Name} over the {what}, taking {nb.Members.Count - 1} followers. They name themselves the {nb.Name}" +
            (culture != parent ? $", the first of the {culture.Name} people." : "."), 0.75f, founder.Id, nb.Id));
        Chronicle.Moment(new LlmMoment(Day, "schism", founder.Id, nb.Id, new()
        {
            ["trigger"] = kind, ["subject"] = what, ["followers"] = (nb.Members.Count - 1).ToString(),
            ["old_band"] = band.Name, ["new_band"] = nb.Name, ["new_culture"] = (culture != parent).ToString(),
        }));
    }

    // ------------------------------------------------------------------ every 30 days

    void Contacts()
    {
        // Each band meets at most one neighbour a month, so contact scales with bands, not pairs.
        var bands = Bands.Values.Where(b => !b.Disbanded && b.Members.Count > 0).ToArray();
        var near = new List<Band>();
        int contact = World.Cells(2);
        foreach (var a in bands)
        {
            if (a.Disbanded || !Rng.Chance(0.3f)) continue;
            near.Clear();
            foreach (var b in bands)
                if (b != a && !b.Disbanded && World.Distance(a.Cell, b.Cell) <= contact) near.Add(b);
            if (near.Count > 0) Meet(a, near[Rng.Next(near.Count)]);
        }
    }

    (float co, float hunger, int fighters, float xeno, float hosp) Mood(Band band)
    {
        float co = 0f, hunger = 0f; int n = 0, fighters = 0;
        foreach (int id in band.Members)
            if (Living.TryGetValue(id, out var m) && m.IsAdult(Day))
            {
                co += m.T(Trait.Cooperativeness); hunger += m.NeedOf(Need.Hunger); n++;
                if (m.Age(Day) < 50f) fighters++;
            }
        n = Math.Max(n, 1);
        return (co / n, hunger / n, fighters, BandHolds(band, MemeKind.Xenophobia, 0.3f) ? 1f : 0f, BandHolds(band, MemeKind.Hospitality, 0.3f) ? 1f : 0f);
    }

    void Meet(Band a, Band b)
    {
        bool first = !a.Relations.ContainsKey(b.Id);
        var ma = Mood(a); var mb = Mood(b);
        float rel = a.RelationTo(b.Id);
        float warmth = (ma.co + mb.co) * 0.5f + rel * 0.6f + 0.25f * (ma.hosp + mb.hosp) - 0.3f * (ma.xeno + mb.xeno)
                       - 0.5f * MathF.Abs(ma.hunger - mb.hunger) + Rng.Normal(0f, 0.15f);
        bool sameCulture = a.CultureId == b.CultureId;
        if (sameCulture) warmth += 0.15f;
        if (first) Stats.Year.FirstContacts++;
        if (a.PlagueUntil > Day && b.PlagueUntil < Day && Rng.Chance(0.2f)) StartPlague(b);
        else if (b.PlagueUntil > Day && a.PlagueUntil < Day && Rng.Chance(0.2f)) StartPlague(a);

        bool hostile = warmth < 0.32f && (ma.hunger > 0.35f || mb.hunger > 0.35f || ma.xeno + mb.xeno > 0f);
        if (first && !sameCulture)
        {
            // A meeting of peoples: the hungrier side's leader decides how to greet the strangers.
            var decider = ma.hunger >= mb.hunger ? a : b;
            BeginMeeting(decider, decider == a ? b : a, hostile ? "raid" : warmth > 0.45f ? "welcome" : "wary");
            return;
        }
        if (hostile)
            Raid(ma.hunger >= mb.hunger ? a : b, ma.hunger >= mb.hunger ? b : a, first);
        else if (warmth > 0.45f)
            Feast(a, b, first);
        else
        {
            a.Relations[b.Id] = rel + 0.02f; b.Relations[a.Id] = rel + 0.02f;
            if (first) foreach (var band in new[] { a, b }) MeetStrangers(band, 0.3f);
        }
    }

    void MeetStrangers(Band band, float intensity)
    {
        foreach (int id in band.Members)
            if (Living.TryGetValue(id, out var m) && m.IsAdult(Day))
            {
                if (m.T(Trait.HarmAvoidance) > 0.6f) Fear.Scare(m, Stimulus.Strangers, intensity, Day);
                else Fear.SafeExposure(m, Stimulus.Strangers);
            }
    }

    /// <summary>
    /// Friends in plenty help friends in want: at a feast the better-fed band gives a share of its
    /// surplus food (more freely for those who know trade and count it as fair exchange).
    /// </summary>
    void ShareFood(Band a, Band b)
    {
        float perA = a.FoodPool / Math.Max(1, a.Members.Count), perB = b.FoodPool / Math.Max(1, b.Members.Count);
        var (rich, poor, perRich, perPoor) = perA >= perB ? (a, b, perA, perB) : (b, a, perB, perA);
        if (perRich < 30f || perRich < perPoor * 2f + 5f || rich.RelationTo(poor.Id) < 0f) return;
        float generosity = (BandKnows(rich, TechId.Trade) ? 0.3f : 0.15f) * (0.5f + rich.RelationTo(poor.Id));
        float give = MathF.Min((perRich - 20f) * rich.Members.Count, (40f - perPoor) * poor.Members.Count) * generosity;
        if (give > 1f) rich.Stores.GiveFood(poor.Stores, give);
    }

    void Feast(Band a, Band b, bool first)
    {
        Stats.Year.Trades++;
        a.Relations[b.Id] = MathF.Min(1f, a.RelationTo(b.Id) + 0.15f);
        b.Relations[a.Id] = MathF.Min(1f, b.RelationTo(a.Id) + 0.15f);
        ExchangeIdeas(a, b); ExchangeIdeas(b, a);
        ShareFood(a, b);
        foreach (var band in new[] { a, b })
            foreach (int id in band.Members)
                if (Living.TryGetValue(id, out var m)) { Fear.SafeExposure(m, Stimulus.Strangers); m.Feel(Emotion.Joy, 0.1f); }

        // Marriage across bands.
        var single = SingleAdult(a); var other = SingleAdult(b);
        if (single != null && other != null && single.Sex != other.Sex && Rng.Chance(0.5f))
        {
            Bond(single, other, Day);
            var mover = Rng.Chance(0.5f) ? single : other;
            var to = mover == single ? b : a; var fromBand = mover == single ? a : b;
            fromBand.Members.Remove(mover.Id); AddMember(to, mover.Id); mover.BandId = to.Id;
            Stats.Year.Pairings++;
        }
        if (first)
            Chronicle.Add(new WorldEvent(Day, EventCategory.Trade, "first_contact",
                $"The {a.Name} and the {b.Name} meet for the first time and share a feast.",
                a.CultureId != b.CultureId ? 0.45f : 0.26f, -1, a.Id));   // only a meeting of peoples is big news
        if (a.Members.Count < 6 && a.RelationTo(b.Id) > 0.3f) Merge(a, b);
        else if (b.Members.Count < 6 && b.RelationTo(a.Id) > 0.3f) Merge(b, a);

        Agent? SingleAdult(Band x)
        {
            foreach (int id in x.Members)
                if (Living.TryGetValue(id, out var m) && m.PartnerId < 0 && m.Age(Day) is >= 16f and < 40f) return m;
            return null;
        }
    }

    void ExchangeIdeas(Band from, Band to)
    {
        TechSet known = default, theirs = default;
        foreach (int id in from.Members) if (Living.TryGetValue(id, out var m)) known |= m.Knows;
        foreach (int id in to.Members) if (Living.TryGetValue(id, out var m)) theirs |= m.Knows;
        var learners = to.Members.Select(id => Living.GetValueOrDefault(id)).Where(m => m != null && m.IsAdult(Day)).ToList();
        if (learners.Count == 0) return;
        foreach (var t in (known & ~theirs).Ids())
        {
            if (BandShare(from, t) < 0.3f || !Rng.Chance(0.35f)) continue;
            var learner = learners[Rng.Next(learners.Count)]!;
            if (!Techs.CanLearn(learner.Knows, t)) continue;   // it builds on things they don't know yet
            learner.Knows |= Techs.Bit(t);
            Chronicle.Add(new WorldEvent(Day, EventCategory.Trade, "tech_spread",
                $"The {to.Name} learn {Techs.Name(t).Replace('_', ' ')} from the {from.Name}.", 0.3f, learner.Id, to.Id));
        }
        foreach (var meme in Memes.Values)
            if (Prevalence(from, meme.Id) > 0.5f && Rng.Chance(0.3f))
            {
                var listener = learners[Rng.Next(learners.Count)]!;
                listener.Memes[meme.Id] = MathF.Max(listener.Memes.GetValueOrDefault(meme.Id), 0.3f * Fit(listener, meme) + 0.1f);
            }
    }

    internal void MoveToBand(Agent a, int bandId)
    {
        BandsChanged();
        if (!Bands.TryGetValue(a.BandId, out var from) || !Bands.TryGetValue(bandId, out var to) || from == to) return;
        from.Members.Remove(a.Id);
        AddMember(to, a.Id);
        a.BandId = to.Id;
        foreach (int c in a.Children)
            if (Living.TryGetValue(c, out var child) && child.Age(Day) < 6f && child.BandId == from.Id)
            {
                from.Members.Remove(c); AddMember(to, c); child.BandId = to.Id;
            }
        if (from.LeaderId == a.Id) ElectLeader(from);
        if (from.Members.Count == 0) Disband(from);
    }

    /// <summary>A band too small to survive alone joins the friendliest neighbour.</summary>
    void MaybeJoinNeighbour(Band band)
    {
        if (band.Members.Count >= 8) return;
        Band? best = null;
        foreach (var other in Bands.Values)
            if (other.Id != band.Id && !other.Disbanded && World.Distance(other.Cell, band.Cell) <= World.Cells(3) && band.RelationTo(other.Id) > -0.2f
                && (best == null || band.RelationTo(other.Id) + (other.CultureId == band.CultureId ? 0.3f : 0f)
                                    > band.RelationTo(best.Id) + (best.CultureId == band.CultureId ? 0.3f : 0f)))
                best = other;
        if (best != null && Rng.Chance(0.3f)) Merge(band, best);
    }

    void Merge(Band small, Band into)
    {
        BandsChanged();
        foreach (int id in small.Members.ToArray())
            if (Living.TryGetValue(id, out var m)) { AddMember(into, id); m.BandId = into.Id; }
        small.Stores.MoveAllTo(into.Stores);
        foreach (var (species, head) in small.Herds) into.Herds[species] = into.Herds.GetValueOrDefault(species) + head;
        foreach (int sid in small.Structures)
            if (Structures.TryGetValue(sid, out var s)) { s.BandId = World.Distance(s.Cell, into.Cell) <= 8 ? into.Id : -1; if (s.BandId == into.Id) into.Structures.Add(sid); }
        small.Structures.Clear();
        small.Members.Clear();
        Chronicle.Add(new WorldEvent(Day, EventCategory.Social, "merge", $"The last of the {small.Name} join the {into.Name}.", 0.3f, -1, into.Id));
        Disband(small);
    }

    void Raid(Band attacker, Band victim, bool first)
    {
        Stats.Year.Raids++;
        var ma = Mood(attacker); var mv = Mood(victim);
        float odds = (ma.fighters + 1f) / (ma.fighters + mv.fighters + 2f) + Rng.Normal(0f, 0.1f);
        bool won = odds > 0.5f;
        int casualtiesV = 0, casualtiesA = 0;
        foreach (var (band, losers, chance) in new[] { (victim, won, 0.06f), (attacker, !won, 0.04f) })
            foreach (int id in band.Members.ToArray())
                if (Living.TryGetValue(id, out var m) && m.IsAdult(Day) && Rng.Chance(losers ? chance * 1.6f : chance * 0.5f))
                {
                    m.Health -= Rng.Range(0.3f, 1f);
                    m.Scars++;
                    if (m.Health <= 0f) { Kill(m, DeathCause.Violence, Stimulus.Strangers, $", in a raid by the {attacker.Name}"); if (band == victim) casualtiesV++; else casualtiesA++; }
                }
        if (won)
        {
            victim.Stores.GiveFood(attacker.Stores, victim.FoodPool * 0.6f);
            DamageStructures(victim.Cell, 2, 0.3f, fire: Rng.Chance(0.4f));
        }
        victim.LastRaidedDay = Day;

        attacker.Relations[victim.Id] = MathF.Max(-1f, attacker.RelationTo(victim.Id) - 0.3f);
        victim.Relations[attacker.Id] = MathF.Max(-1f, victim.RelationTo(attacker.Id) - 0.5f);
        foreach (int id in victim.Members)
            if (Living.TryGetValue(id, out var m))
            {
                Fear.Scare(m, Stimulus.Strangers, won ? 0.7f : 0.4f, Day);
                Psychology.Appraise(m, new Appraisal(EventCategory.Conflict, "raided", -1f, 0.7f, Agency.Other, Threat: true, Detail: attacker.Name), Day);
            }
        foreach (int id in attacker.Members)
            if (Living.TryGetValue(id, out var m) && won && m.T(Trait.Cooperativeness) < 0.4f) m.Prestige += 0.1f;

        Chronicle.Add(new WorldEvent(Day, EventCategory.Conflict, "raid",
            $"The {attacker.Name} raid the {victim.Name}{(won ? " and carry off their food" : " but are driven off")}. {casualtiesV + casualtiesA} die.",
            (casualtiesV + casualtiesA == 0 ? (won ? 0.26f : 0.2f) : 0.35f + 0.08f * (casualtiesV + casualtiesA)), -1, attacker.Id));
    }

    /// <summary>Taboos from shared fear, norms from leaders, and new cultures from drift.</summary>
    void CulturalUpkeep()
    {
        foreach (var band in Bands.Values.ToArray())
        {
            if (band.Disbanded) continue;
            int adults = AdultCount(band);
            if (adults < 3) continue;

            // Shared fear hardens into taboo.
            for (int c = 0; c < Ids.CategoryCount; c++)
            {
                var cat = (StimulusCategory)c;
                int afraid = 0; Agent? voice = null;
                foreach (int id in band.Members)
                    if (Living.TryGetValue(id, out var m) && m.IsAdult(Day) && CategoryFearOf(m, cat) > 0.45f)
                    {
                        afraid++;
                        if (voice == null || m.T(Trait.RewardDependence) + CategoryFearOf(m, cat) > voice.T(Trait.RewardDependence) + CategoryFearOf(voice, cat)) voice = m;
                    }
                if (voice == null || afraid < adults * 0.4f || Taboo(voice, cat) > 0f || BandHolds(band, MemeKind.Taboo, 0.01f) && HasTaboo(band, cat)) continue;
                bool firstEver = Chronicle.First($"taboo:{cat}");
                var meme = SharedMeme(MemeKind.Taboo, cat, $"the {Ids.Snake(cat)} taboo", voice, band);
                foreach (int id in band.Members)
                    if (Living.TryGetValue(id, out var m) && CategoryFearOf(m, cat) > 0.3f) m.Memes[meme.Id] = 0.6f;
                Stats.Year.Taboos++;
                Chronicle.Add(new WorldEvent(Day, EventCategory.Spiritual, "taboo",
                    $"The {band.Name} begin to shun {TabooText(cat)}.", firstEver ? 0.5f : 0.3f, voice.Id, band.Id));
            }

            // Leaders' temperaments seed norms.
            if (Rng.Chance(0.02f) && Living.TryGetValue(band.LeaderId, out var leader))
            {
                MemeKind? kind =
                    leader.T(Trait.Cooperativeness) > 0.72f ? MemeKind.SharingNorm :
                    leader.T(Trait.Cooperativeness) < 0.3f && leader.T(Trait.HarmAvoidance) > 0.6f ? MemeKind.Xenophobia :
                    leader.T(Trait.Cooperativeness) > 0.6f && leader.T(Trait.NoveltySeeking) > 0.6f ? MemeKind.Hospitality :
                    leader.Age(Day) > 50f && leader.T(Trait.RewardDependence) > 0.6f ? MemeKind.ElderRule :
                    leader.T(Trait.NoveltySeeking) < 0.3f && leader.T(Trait.HarmAvoidance) > 0.55f ? MemeKind.InnovationTaboo : null;
                if (kind is { } k && !BandHolds(band, k, 0.2f))
                {
                    SharedMeme(k, StimulusCategory.Outsiders, Ids.Snake(k).Replace('_', ' '), leader, band);
                    Stats.Year.Norms++;
                    Chronicle.Add(new WorldEvent(Day, EventCategory.Social, "norm",
                        $"Under {leader.Name}, the {band.Name} come to value {NormText(k)}.", 0.35f, leader.Id, band.Id));
                }
            }
        }
    }

    bool HasTaboo(Band band, StimulusCategory cat)
    {
        foreach (var m in Memes.Values) if (m.Kind == MemeKind.Taboo && m.Target == cat && Prevalence(band, m.Id) > 0.2f) return true;
        return false;
    }

    static float CategoryFearOf(Agent a, StimulusCategory c)
    {
        float best = a.CategoryFear[(int)c];
        for (int s = 0; s < Ids.StimulusCount; s++) if (Ids.CategoryOf((Stimulus)s) == c) best = MathF.Max(best, a.Fear[s]);
        return best;
    }

    static string TabooText(StimulusCategory c) => c switch
    {
        StimulusCategory.Beasts => "the hunting grounds where the beasts walk",
        StimulusCategory.Water => "deep water",
        StimulusCategory.Fire => "the dry places where fire comes",
        StimulusCategory.Sky => "open ground under storms",
        StimulusCategory.Earth => "the mountains that shake",
        StimulusCategory.Outsiders => "strangers",
        _ => "the sick",
    };

    static string NormText(MemeKind k) => k switch
    {
        MemeKind.SharingNorm => "sharing every meal",
        MemeKind.Xenophobia => "keeping strangers away",
        MemeKind.Hospitality => "welcoming travellers",
        MemeKind.ElderRule => "the word of the eldest",
        MemeKind.InnovationTaboo => "the old ways over new tricks",
        _ => Ids.Snake(k),
    };

    /// <summary>Yearly: bands whose shared beliefs have drifted far from their culture's become a new people.</summary>
    internal void CultureDrift()
    {
        var byCulture = Bands.Values.Where(b => !b.Disbanded).GroupBy(b => b.CultureId).ToList();
        foreach (var group in byCulture)
        {
            if (group.Count() < 2) continue;
            var profiles = group.ToDictionary(b => b, b => Memes.Values.Where(m => Prevalence(b, m.Id) >= 0.4f).Select(m => m.Id).ToHashSet());
            var core = profiles.Values.SelectMany(s => s).GroupBy(x => x).Where(g => g.Count() * 2 >= profiles.Count).Select(g => g.Key).ToHashSet();
            foreach (var (band, profile) in profiles)
            {
                if (Day - band.FoundedDay < 40 * SimConfig.DaysPerYear) continue;
                int union = profile.Union(core).Count();
                if (union < 3) continue;
                float distance = 1f - profile.Intersect(core).Count() / (float)union;
                if (distance < 0.75f || !Rng.Chance(0.05f)) continue;
                var culture = NewCulture(Cultures[band.CultureId]);
                band.CultureId = culture.Id;
                foreach (int id in band.Members) if (Living.TryGetValue(id, out var m)) m.CultureId = culture.Id;
                Chronicle.Add(new WorldEvent(Day, EventCategory.Spiritual, "new_culture",
                    $"The {band.Name} no longer see themselves as {Cultures[culture.ParentId].Name}. They are the {culture.Name}.", 0.6f, -1, band.Id));
            }
        }
    }
}
