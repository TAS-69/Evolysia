using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Evolysia.Sim.Persistence;
using Evolysia.Sim.Society;
using Evolysia.Sim.Psych;

namespace Evolysia.Sim;

public sealed partial class Simulation
{
    [SaveOptional(-1)] public int ControlledAgentId = -1;
    [SaveOptional(-1)] public int ControlTargetCell = -1;
    [SaveOptional] public Activity ControlledActivity = Activity.Rest;

    public bool Possess(int personId)
    {
        if (personId == ControlledAgentId || personId < 0) { ReleaseControl(); return true; }
        if (!Living.TryGetValue(personId, out var person) || !person.Alive || !person.IsAdult(Day)) return false;
        ReleaseControl();
        if (HaulFor(personId) is { } haul) CancelHaul(haul, "The carrier is under your direct control.");
        if (ProductionFor(personId) is { } production) CancelProduction(production.Id);
        if (RoadFor(personId) is { } road) road.Cancelled = true;
        Services.Remove(personId);
        ControlledAgentId = personId; ControlTargetCell = person.Site >= 0 && Walkable(person.Site) ? person.Site : Bands[person.BandId].Cell;
        person.Site = ControlTargetCell; ControlledActivity = Activity.Rest; Reconsider(person.BandId);
        return true;
    }

    public void ReleaseControl()
    {
        int band = Living.TryGetValue(ControlledAgentId, out var person) ? person.BandId : -1;
        ControlledAgentId = -1; ControlTargetCell = -1; ControlledActivity = Activity.Rest;
        if (band >= 0) Reconsider(band);
    }

    public bool CommandPerson(int cell, Activity activity)
    {
        if (!Living.TryGetValue(ControlledAgentId, out var person) || !person.Alive || cell < 0 || cell >= World.Count ||
            activity is not (Activity.Rest or Activity.Explore or Activity.Forage or Activity.Chop or Activity.Quarry) || !Walkable(cell)) return false;
        if (activity == Activity.Chop && !person.Knows.Has(TechId.StoneTools) || activity == Activity.Quarry && !person.Knows.Has(TechId.StoneTools)) return false;
        if (Route(person.Site, cell) == null) return false;
        ControlTargetCell = cell; ControlledActivity = activity; return true;
    }

    bool AtCamp(Agent person, Band band) => person.Id != ControlledAgentId || person.Site < 0 || World.Distance(person.Site, band.Cell) <= World.Cells(0.5f);

    Activity ControlChoice(Agent person)
    {
        if (person.Sick || person.Health < 0.2f || person.NeedOf(Need.Energy) > 0.9f) return Activity.Rest;
        return person.Site != ControlTargetCell ? Activity.Explore : ControlledActivity;
    }

    void ResolveControl(Agent person, Band band, in DayContext context)
    {
        if (!Walkable(person.Site))
        {
            person.Site = band.Cell; ControlTargetCell = band.Cell; ControlledActivity = Activity.Rest;
        }
        if (person.Today == Activity.Explore && person.Site != ControlTargetCell)
        {
            if (Route(person.Site, ControlTargetCell) is { Length: > 1 } path)
            { person.Site = path[1]; person.Needs[(int)Need.Energy] += 0.15f; }
            else { ControlTargetCell = person.Site; ControlledActivity = Activity.Rest; }
            return;
        }
        float tired = person.NeedOf(Need.Energy) > 0.8f ? 0.7f : 1f;
        int cell = person.Site;
        switch (person.Today)
        {
            case Activity.Forage:
                float gathered = Take(0, cell, 3f * (0.6f + 0.4f * person.Efficacy[(int)Domain.Foraging]) * ToolBonus(person, Activity.Forage) * tired * MathF.Max(0.3f, World.Growth(cell, Day) + 0.2f));
                var local = context; local.Season = World.SeasonOf(cell, Day);
                ForageYield(person, cell, gathered, local); Psychology.Practice(person, Domain.Foraging, gathered > 1f, 0.5f);
                person.Needs[(int)Need.Energy] += 0.12f; break;
            case Activity.Chop:
                float logs = Take(Timber, cell, (HeldTool(person, Activity.Chop) != null ? 1.2f : 0.35f) * ToolBonus(person, Activity.Chop) * tired);
                person.Pack.Add(GLog, logs); person.Pack.Add(GWood, logs * 0.5f); person.Needs[(int)Need.Energy] += 0.2f; break;
            case Activity.Quarry:
                float stone = Take(StoneStock, cell, ToolBonus(person, Activity.Quarry) * tired);
                person.Pack.Add(GStone, stone * 0.8f); person.Pack.Add(GFlint, stone * 0.25f); person.Needs[(int)Need.Energy] += 0.22f; break;
            default: person.Needs[(int)Need.Energy] -= 0.35f; break;
        }
    }
}
