using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Evolysia.Sim.Psych;
using Evolysia.Sim.Society;
using Evolysia.Sim.World;

namespace Evolysia.Sim;

public sealed record Voyage(int Day, int FromBandId, int BandId, int FromCell, int ToCell, int Distance, int Sailed, int Lost, bool FirstLanding);

public sealed partial class Simulation
{
    public readonly List<Voyage> Voyages = new();

    int SeaRange(TechSet tech) =>
        !Techs.Has(tech, TechId.Boats) ? 0
        : World.Cells(Techs.Has(tech, TechId.Shipbuilding) ? 60 : Techs.Has(tech, TechId.Sailing) ? 25 : Techs.Has(tech, TechId.Weaving) ? 10 : 6);

    public int SeaRange(Band band)
    {
        TechSet known = default;
        foreach (int id in band.Members) if (Living.TryGetValue(id, out var a)) known |= a.Knows;
        return SeaRange(known);
    }

    bool IsSea(int i) => !World.IsLand(i);

    internal bool IsCoastal(int cell)
    {
        int r = World.Cells(0.6f), x = World.X(cell), y = World.Y(cell);
        for (int dy = -r; dy <= r; dy++)
        {
            int ny = y + dy;
            if (ny < 0 || ny >= World.H) continue;
            for (int dx = -r; dx <= r; dx++)
                if ((dx != 0 || dy != 0) && IsSea(World.Index(x + dx, ny))) return true;
        }
        return false;
    }

    internal Dictionary<int, int> ShoresWithin(int origin, int range)
    {
        var dist = new Dictionary<int, int>();
        var shores = new Dictionary<int, int>();
        var queue = new Queue<int>();
        if (range <= 0) return shores;
        Span<int> nb = stackalloc int[8];

        int r = World.Cells(0.6f), ox = World.X(origin), oy = World.Y(origin);
        for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                int y = oy + dy;
                if (y < 0 || y >= World.H) continue;
                int s = World.Index(ox + dx, y);
                if (IsSea(s) && dist.TryAdd(s, 1)) queue.Enqueue(s);
            }
        int k;
        while (queue.Count > 0)
        {
            int c = queue.Dequeue(), d = dist[c];
            k = World.Neighbors(c, nb);
            for (int j = 0; j < k; j++)
            {
                int n = nb[j];
                if (IsSea(n))
                {
                    if (d + 1 <= range && dist.TryAdd(n, d + 1)) queue.Enqueue(n);
                }
                else if (World.IsDryLand(n) && n != origin && !shores.ContainsKey(n)) shores[n] = d;
            }
        }
        return shores;
    }

    void SightShore(Agent scout, Band band, int range)
    {
        if (range <= 0 || band.KnownShores.Count >= 12 || !IsCoastal(band.Cell)) return;
        int home = World.Landmass[band.Cell];
        var candidates = ShoresWithin(band.Cell, range)
            .Where(kv => World.Landmass[kv.Key] != home && World.PlantK[kv.Key] > 0f && !band.KnownShores.ContainsKey(kv.Key))
            .OrderBy(kv => kv.Key).ToList();
        if (candidates.Count == 0) return;
        var (cell, distance) = candidates[Rng.Next(candidates.Count)];
        band.KnownShores[cell] = distance;
        Interlocked.Increment(ref Stats.Year.ShoresSighted);
        scout.Prestige += 0.2f;
        Later(() =>
        {
            bool firstSight = Chronicle.First($"sighted:{World.Landmass[cell]}");
            Chronicle.Add(new WorldEvent(Day, EventCategory.Discovery, "shore_sighted",
                $"{scout.Name} of the {band.Name} sights {LandName(World.Landmass[cell])} across the sea, {distance} days' paddle away.",
                firstSight ? 0.5f : 0.15f, scout.Id, band.Id));
        });
    }

    string LandName(int landmass)
    {
        int size = landmass >= 0 && landmass < World.LandmassSize.Length ? World.LandmassSize[landmass] : 0;
        return size < 40 ? "an island" : size < 300 ? "a strange land" : "a vast land";
    }

    (int cell, float score)? BestShore(Band band, Agent? judge)
    {
        if (band.KnownShores.Count == 0 || !IsCoastal(band.Cell) || SeaRange(band) == 0) return null;
        (int cell, float score)? best = null;
        float ns = judge?.T(Trait.NoveltySeeking) ?? 0.5f;
        foreach (var (cell, distance) in band.KnownShores.OrderBy(kv => kv.Key))
        {
            float s = CellScore(band, cell, judge) + (PeopleNear(cell) == 0 ? 0.15f : 0f) - 0.02f * distance / World.Scale + Rng.Normal(0f, 0.05f) * ns;
            if (best == null || s > best.Value.score) best = (cell, s);
        }
        return best;
    }

    internal Band? Sail(Band from, Agent leader, List<Agent>? followers, int target)
    {
        int distance = from.KnownShores.TryGetValue(target, out var d) ? d : 3;
        int fromCell = from.Cell;
        Band band;
        if (followers == null)
        {
            band = from;
            LeaveCamp(band, target);
            band.Cell = target;
            band.Sedentary = false;
            band.LastMoveDay = Day;
            band.Shelter = 0f;
            Array.Clear(band.PlaceDread);
            BandsChanged();
        }
        else band = Split(from, leader, followers, from.CultureId, target, announce: false);
        band.KnownShores.Clear();
        int sailed = band.Members.Count;

        bool winter = World.SeasonOf(fromCell, Day) == Season.Winter;
        bool sails = Techs.Has(leader.Knows, TechId.Weaving);
        float loss = (0.015f + 0.012f * distance) * (sails ? 0.7f : 1f) * (winter ? 1.5f : 1f);
        bool vanished = Rng.Chance(0.015f + 0.004f * distance);
        int lost = 0;
        foreach (int id in band.Members.ToArray())
        {
            if (!Living.TryGetValue(id, out var m)) continue;
            if (vanished || Rng.Chance(loss)) { Kill(m, DeathCause.Accident, Stimulus.Flood, ", lost at sea"); lost++; }
        }
        foreach (int id in band.Members)
        {
            if (!Living.TryGetValue(id, out var m) || !m.Alive) continue;
            if (lost > 0) Fear.Scare(m, Stimulus.Flood, 0.5f, Day);
            else if (m.EffectiveFear(Stimulus.River) > 0.35f) Fear.Overcame(m, Stimulus.River, Domain.Fishing, Day);
            Psychology.Appraise(m, new Appraisal(EventCategory.Discovery, "crossed_the_sea", lost == 0 ? 0.8f : 0.2f, 0.8f,
                Agency.Self, Detail: "crossed the sea"), Day);
        }

        Stats.Year.Voyages++;
        Stats.Year.LostAtSea += lost;
        bool first = !vanished && ArrivedAt(band, announce: false);
        Voyages.Add(new Voyage(Day, from.Id, band.Id, fromCell, target, distance, sailed, lost, first));
        if (Voyages.Count > 300) Voyages.RemoveAt(0);

        string text = vanished
            ? $"{leader.Name} sets out across the sea with {sailed - 1} of the {from.Name}. They are never seen again."
            : $"{leader.Name} leads {sailed} {(band == from ? $"of the {band.Name}" : $"of the {from.Name}, now the {band.Name},")} across " +
              $"{distance} days of open sea to {(first ? $"{LandName(World.Landmass[target])} no one has ever walked" : LandName(World.Landmass[target]))}. " +
              (lost == 0 ? "All arrive." : $"{lost} are lost to the waves.");
        Chronicle.Add(new WorldEvent(Day, EventCategory.Discovery, vanished ? "lost_at_sea" : "voyage", text, first ? 0.9f : vanished ? 0.6f : 0.5f, leader.Id, band.Id));
        if (!vanished && leader.Alive)
            Chronicle.Moment(new LlmMoment(Day, "voyage", leader.Id, band.Id, new()
            {
                ["distance"] = distance.ToString(), ["sailed"] = sailed.ToString(), ["lost"] = lost.ToString(),
                ["first_landing"] = first.ToString(), ["land"] = LandName(World.Landmass[target]), ["from_band"] = from.Name,
                ["winter"] = winter.ToString(),
            }));
        return vanished ? null : band;
    }

    internal bool ArrivedAt(Band band, bool announce = true)
    {
        int landmass = World.Landmass[band.Cell];
        if (landmass < 0 || !Chronicle.First($"landmass:{landmass}")) return false;
        Stats.Year.Landmasses++;
        if (announce)
            Chronicle.Add(new WorldEvent(Day, EventCategory.Discovery, "new_landmass",
                $"The {band.Name} are the first people to set foot on {LandName(landmass)}.", 0.8f, band.LeaderId, band.Id));
        return true;
    }
}
