using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Evolysia.Sim.Society;

namespace Evolysia.Sim;

public sealed partial class Simulation
{
    HashSet<int> DepartingFamily(CommunityIntent intent)
    {
        var moving = (intent.Followers ?? Array.Empty<int>()).Append(intent.LeaderId).ToHashSet();
        foreach (int id in moving.ToArray())
            if (Living.TryGetValue(id, out var person))
            {
                if (person.PartnerId >= 0) moving.Add(person.PartnerId);
                foreach (int child in person.Children) if (Living.TryGetValue(child, out var c) && c.IsChild(Day)) moving.Add(child);
            }
        return moving;
    }

    bool ValidateCommunity(Band band, MindRequest request, MindPlan plan, out string error)
    {
        error = "Community action must be keep, elect, split or join, using living adult members and neighbouring bands. Splits need four departing people and three remaining; joining needs mutual consent.";
        var intent = plan.Community;
        if (intent == null || intent.Action == "keep") return true;
        if (intent.Action == "join") return request.Neighbours.Contains(intent.OtherBandId) && Bands.TryGetValue(intent.OtherBandId, out var other) &&
            !other.Disbanded && World.Distance(band.Cell, other.Cell) <= World.Cells(3) && (plan.Cargo?.Length ?? 0) == 0;
        if (intent.Action is not ("elect" or "split") || !Living.TryGetValue(intent.LeaderId, out var leader) || leader.BandId != band.Id || !leader.IsAdult(Day)) return false;
        if (intent.Action == "elect") return true;
        if ((plan.Cargo?.Length ?? 0) > 0 || plan.Destination == band.Cell || !request.Destinations.Contains(plan.Destination) || intent.Followers == null || intent.Followers.Length > 16 || intent.Followers.Distinct().Count() != intent.Followers.Length ||
            intent.Followers.Any(id => !Living.TryGetValue(id, out var a) || a.BandId != band.Id || !a.IsAdult(Day))) return false;
        var moving = DepartingFamily(intent).Where(id => band.Members.Contains(id)).ToArray();
        return moving.Length >= 4 && band.Members.Count - moving.Length >= 3;
    }

    void ApplyCommunity(Band band, MindPlan plan)
    {
        var intent = plan.Community;
        if (intent == null || intent.Action == "keep") return;
        if (intent.Action == "elect")
        {
            if (band.LeaderId == intent.LeaderId) return;
            band.LeaderId = intent.LeaderId;
            Chronicle.Add(new WorldEvent(Day, EventCategory.Social, "election", $"The {band.Name} choose {Living[intent.LeaderId].Name} to lead them.", 0.5f, intent.LeaderId, band.Id));
        }
        else if (intent.Action == "split")
        {
            foreach (var haul in Hauls.Values.Where(h => h.BandId == band.Id && h.Active)) CancelHaul(haul, "The community divided.");
            foreach (int id in Production.Values.Where(j => j.BandId == band.Id && j.Active).Select(j => j.Id).ToArray()) CancelProduction(id);
            foreach (var road in RoadProjects.Values.Where(r => r.BandId == band.Id && r.Active)) road.Cancelled = true;
            var followers = intent.Followers!.Select(id => Living[id]).ToList();
            var founded = Split(band, Living[intent.LeaderId], followers, band.CultureId, plan.Destination);
            Stats.Year.Splits++; Reconsider(band.Id);
            Chronicle.Add(new WorldEvent(Day, EventCategory.Social, "band_split", $"The {band.Name} divide by council agreement; {founded.Members.Count} people found the {founded.Name}.", 0.5f, intent.LeaderId, founded.Id));
        }
        else if (Bands.TryGetValue(intent.OtherBandId, out var other) && BandPlans.TryGetValue(other.Id, out var consent) && consent.Until > Day &&
            consent.Plan.Community is { Action: "join" } reciprocal && reciprocal.OtherBandId == band.Id) Merge(band, other);
    }
}
