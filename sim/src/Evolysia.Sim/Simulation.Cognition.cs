using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Evolysia.Sim.Economy;
using Evolysia.Sim.Society;

namespace Evolysia.Sim;

public sealed partial class Simulation
{
    public readonly Dictionary<int, AcceptedPlan> BandPlans = new();
    public readonly Dictionary<int, PersonalMind> Minds = new();
    public readonly List<AcceptedPlan> PlanLog = new();
    readonly Dictionary<int, MindRequest> _mindRequests = new();
    readonly Dictionary<int, int> _mindCursors = new();
    int _nextMindRequest = 1;

    public void Reconsider(int bandId)
    {
        _mindRequests.Remove(bandId);
        foreach (int id in Services.Where(s => s.Value.BandId == bandId).Select(s => s.Key).ToArray()) Services[id] = Services[id] with { Until = Day };
        if (BandPlans.TryGetValue(bandId, out var plan)) BandPlans[bandId] = plan with { Until = Day };
        if (Bands.TryGetValue(bandId, out var band))
            foreach (int id in band.Members) if (Minds.TryGetValue(id, out var mind)) mind.Until = Day;
    }

    public bool IsCurrentRequest(int id) => _mindRequests.Values.Any(r => r.Id == id);

    public bool WaitingForMinds => Bands.Values.Any(b => !b.Disbanded && b.Members.Count > 0 &&
        (!BandPlans.TryGetValue(b.Id, out var p) || p.Until <= Day)) || PendingDecisions.Any(d => d.Deadline <= Day);

    public IReadOnlyList<MindRequest> MindRequests()
    {
        foreach (int id in _mindRequests.Keys.ToArray())
            if (!Bands.TryGetValue(id, out var b) || b.Disbanded || _mindRequests[id].Agents.Any(a => !Living.TryGetValue(a, out var person) || person.BandId != id)) _mindRequests.Remove(id);
        foreach (var b in Bands.Values.Where(b => !b.Disbanded && b.Members.Count > 0))
        {
            if (BandPlans.TryGetValue(b.Id, out var p) && p.Until > Day || _mindRequests.ContainsKey(b.Id)) continue;
            var adults = b.Members.Where(id => Living.TryGetValue(id, out var a) && a.Age(Day) >= 4f).OrderBy(id => id).ToArray();
            int cursor = adults.Length == 0 ? 0 : _mindCursors.GetValueOrDefault(b.Id) % adults.Length;
            var roster = adults.Skip(cursor).Concat(adults.Take(cursor)).Take(6).ToArray();
            var known = BandKnowledge(b);
            var buildings = Economy.Structures.All.Concat(Designs.Values.Where(d => d.BandId == b.Id).Select(d => d.Definition))
                .Where(s => Economy.Structures.CanBuild(known, s) && s.Materials.All(m => CanObtain(known, m.good))).Select(s => s.Id).ToArray();
            // No list of techniques to research: new knowledge comes only from experiments on what they can work with.
            var research = Array.Empty<string>();
            var neighbours = Bands.Values.Where(n => n.Id != b.Id && !n.Disbanded && World.Distance(b.Cell, n.Cell) <= World.Cells(8)).Select(n => n.Id).ToArray();
            var destinations = new List<int> { b.Cell };
            for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++)
            {
                int cy = World.Y(b.Cell) + y * World.Cells(1);
                if (cy < 0 || cy >= World.H) continue;
                int c = World.Index(World.X(b.Cell) + x * World.Cells(1), cy);
                if (World.IsDryLand(c) && World.PlantK[c] > 0f && !destinations.Contains(c)) destinations.Add(c);
            }
            SyncTechLore(b, LoreOf(b));
            _mindRequests[b.Id] = new MindRequest(_nextMindRequest++, Day, b.Id, roster, buildings, research, neighbours, destinations.ToArray(), Perceivable(b));
        }
        return _mindRequests.Values.OrderBy(r => r.BandId).ToArray();
    }

    public TechSet BandKnowledge(Band band)
    {
        var known = default(TechSet);
        foreach (int id in band.Members) if (Living.TryGetValue(id, out var a)) known |= a.Knows;
        return known;
    }

    public bool AcceptPlan(MindPlan plan, out string error)
    {
        error = "The plan is stale or belongs to another world.";
        if (plan == null) return false;
        var request = _mindRequests.Values.FirstOrDefault(r => r.Id == plan.RequestId);
        if (request == null || !Bands.TryGetValue(request.BandId, out var band) || band.Disbanded) return false;
        if (!ValidateCommunity(band, request, plan, out error)) return false;
        error = "The plan contains invalid or unavailable actions.";
        if (!ValidateShipments(band.Id, plan.Cargo ?? Array.Empty<ShipmentOrder>(), out error)) return false;
        if (!ValidateWorkOrders(band, plan, out error)) return false;
        if (!ValidateExperiments(request, plan, out error)) return false;
        if (plan.Designs is { Length: > 1 }) { error = "Propose at most one new building per council."; return false; }
        foreach (var idea in plan.Designs ?? Array.Empty<BuildingIdea>()) if (!ValidateDesign(band, idea, out error)) return false;
        if (string.IsNullOrWhiteSpace(plan.Goal)) { error = "The shared goal must be a nonempty sentence."; return false; }
        error = "Check bounds, population count, sharing, destination and settlement prerequisites.";
        if (!ValidText(plan.Goal, 400) || plan.Goal.Length < 1 || !ValidText(plan.Institution, 80) || !ValidText(plan.Doctrine, 400) ||
            !float.IsFinite(plan.Sharing) || plan.Sharing is < 0f or > 1f || plan.Destination != -1 && !request.Destinations.Contains(plan.Destination) || plan.Settle && !BandKnowledge(band).Has(TechId.Agriculture) ||
            plan.Labour == null || plan.Labour.Length < 1 || plan.Labour.Length > ActivityCount || plan.Projects == null || plan.Projects.Length > 4 || plan.Research == null || plan.Research.Length > 4 ||
            plan.People == null || plan.People.Length != request.Agents.Length || plan.Diplomacy == null || plan.Diplomacy.Length > request.Neighbours.Length) return false;
        error = "Work activities must be unique with finite nonnegative relative weights, and projects/research must be available IDs.";
        if (plan.Labour.Any(w => w == null || !TryActivity(w.Activity, out _) || !float.IsFinite(w.Weight) || w.Weight < 0f) || plan.Labour.Sum(w => (double)w.Weight) <= 0f ||
            plan.Projects.Any(x => !request.Buildings.Contains(x)) || plan.Research.Any(x => !request.Research.Contains(x))) return false;
        error = "Every supplied person needs exactly one entry with a unique valid ID, brief text and a valid relationship target.";
        if (plan.People.Select(p => p?.AgentId ?? -1).Distinct().Count() != request.Agents.Length || plan.People.Any(p => p == null || !request.Agents.Contains(p.AgentId) ||
            !TryActivity(p.Activity, out _) || !ValidText(p.Goal, 240) || !ValidText(p.Thought, 320) || !ValidText(p.Speech, 240) || !ValidText(p.Memory, 320) ||
            !float.IsFinite(p.Affinity) || MathF.Abs(p.Affinity) > 0.1f || p.OtherId != -1 && (!Living.TryGetValue(p.OtherId, out var other) || other.BandId != band.Id))) return false;
        error = "Diplomacy needs distinct known neighbouring bands and a valid stance.";
        if (plan.Diplomacy.Any(d => d == null || !request.Neighbours.Contains(d.BandId) || d.Stance is not ("welcome" or "wary" or "raid")) ||
            plan.Diplomacy.Select(d => d.BandId).Distinct().Count() != plan.Diplomacy.Length) return false;
        double weightSum = plan.Labour.Sum(w => (double)w.Weight);
        var designs = (plan.Designs ?? Array.Empty<BuildingIdea>()).Select(idea => RegisterDesign(band, idea)).ToArray();
        plan = plan with { Labour = plan.Labour.GroupBy(w => w.Activity).Select(g => new WorkShare(g.Key, (float)(g.Sum(w => (double)w.Weight) / weightSum))).ToArray(), Projects = plan.Projects.Distinct().ToArray(), Research = plan.Research.Distinct().ToArray() };
        plan = plan with { Projects = plan.Projects.Concat(designs).ToArray() };
        var accepted = new AcceptedPlan(Day, Day + Math.Clamp(Config.PlanDays, 1, 90), band.Id, plan);
        BandPlans[band.Id] = accepted;
        PlanLog.Add(accepted);
        if (PlanLog.Count > 256) PlanLog.RemoveAt(0);
        foreach (var p in plan.People)
        {
            if (!Living.TryGetValue(p.AgentId, out var a) || a.BandId != band.Id) continue;
            if (!Minds.TryGetValue(a.Id, out var mind)) Minds[a.Id] = mind = new PersonalMind();
            mind.Goal = p.Goal; mind.Thought = p.Thought; mind.Speech = p.Speech;
            TryActivity(p.Activity, out mind.Work); mind.Until = accepted.Until;
            mind.Memories.Add(new MindMemory(Day, p.Goal, p.Thought, p.Speech, p.Memory));
            if (mind.Memories.Count > 12) mind.Memories.RemoveAt(0);
            if (p.OtherId >= 0 && p.OtherId != a.Id) a.Rel(p.OtherId).Affinity = Math.Clamp(a.Rel(p.OtherId).Affinity + p.Affinity, -1f, 1f);
            if (p.Speech.Length > 0) Chronicle.Moment(new LlmMoment(Day, "conversation", a.Id, band.Id, new() { ["says"] = p.Speech, ["thought"] = p.Thought, ["goal"] = p.Goal }));
        }
        _mindCursors[band.Id] = _mindCursors.GetValueOrDefault(band.Id) + request.Agents.Length;
        _mindRequests.Remove(band.Id);
        foreach (var order in plan.Cargo ?? Array.Empty<ShipmentOrder>()) AddShipment(band.Id, order);
        TakeUpExperiments(band, plan.Experiments);
        AssignWork(band, accepted);
        Chronicle.Add(new WorldEvent(Day, EventCategory.Social, "council", $"The {band.Name}: {plan.Goal}", 0.3f, band.LeaderId, band.Id));
        ApplyCommunity(band, plan);
        error = "";
        return true;
    }

    /// <summary>
    /// What minds call a kind of work: the intent, not its results (those are learned). "Gather"
    /// reads as gathering food, but it is going out for materials, so minds see "collect_materials".
    /// </summary>
    public static string ActivityName(Activity a) => a == Activity.Gather ? "collect_materials" : Ids.Snake(a);

    static bool ValidText(string? s, int max) => s != null && s.Length <= max && !s.Any(c => char.IsControl(c) && c != '\n');
    public static bool TryActivity(string? text, out Activity activity)
    {
        activity = default;
        foreach (var a in Enum.GetValues<Activity>()) if (ActivityName(a) == text || Ids.Snake(a) == text) { activity = a; return true; }
        return false;
    }

    Activity ModelActivity(Agent a, Band band, float[] feasible)
    {
        if (a.Sick || a.Health < 0.2f || a.NeedOf(Need.Energy) > 0.9f) return Activity.Rest;
        if (HaulFor(a.Id) != null) return Activity.Haul;
        if (Minds.TryGetValue(a.Id, out var personal) && personal.Until > Day && !float.IsNegativeInfinity(feasible[(int)personal.Work])) return personal.Work;
        if (!BandPlans.TryGetValue(band.Id, out var p)) return Activity.Rest;
        Span<float> weights = stackalloc float[ActivityCount];
        weights.Clear();
        weights.Clear();
        foreach (var share in p.Plan.Labour)
            if (TryActivity(share.Activity, out var work) && !float.IsNegativeInfinity(feasible[(int)work])) weights[(int)work] = share.Weight;
        if (weights.ToArray().Sum() <= 0f) return Activity.Rest;
        return (Activity)Rng.Weighted(weights);
    }

    bool PlanFromMind(Band band, TechSet known)
    {
        if (!BandPlans.TryGetValue(band.Id, out var plan)) return false;
        foreach (var id in plan.Plan.Projects.Distinct())
        {
            var def = BuildingDefinition(id);
            if (def == null || !Economy.Structures.CanBuild(known, def) || band.Structures.Any(s => Structures.TryGetValue(s, out var existing) && existing.Kind.Id == id &&
                (existing.State is StructureState.Planned or StructureState.Building || existing.Standing && def.Role is not (StructureRole.Dwelling or StructureRole.Field or StructureRole.Storage)))) continue;
            if (Site(band, def) is not { } cell) continue;
            Place(band, def, cell, design: Designs.GetValueOrDefault(id));
            return true;
        }
        return false;
    }

    void FollowMigrationPlan(Band band)
    {
        if (!BandPlans.TryGetValue(band.Id, out var p)) return;
        band.Sedentary = p.Plan.Settle && BandKnowledge(band).Has(TechId.Agriculture);
        int target = p.Plan.Destination;
        if (target < 0 || target == band.Cell || target >= World.Count || !World.IsDryLand(target) || World.Distance(band.Cell, target) > World.Cells(2)) return;
        LeaveCamp(band, target);
        band.Cell = target; band.LastMoveDay = Day; band.Shelter = 0f;
        Array.Clear(band.PlaceDread); BandsChanged(); ArrivedAt(band); Stats.Year.Migrations++;
    }
}
