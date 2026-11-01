using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Evolysia.Sim.Persistence;

namespace Evolysia.Sim.Tests;

public class InterventionTests
{
    static void Renew(Simulation sim)
    {
        foreach (var request in sim.MindRequests()) Assert.True(sim.AcceptPlan(CognitionTests.Plan(request, "rest"), out var error), error);
    }

    [Fact]
    public void Direct_control_moves_one_cell_per_day_and_harvests_the_actual_local_stock()
    {
        var (sim, worker, band) = IndustryTests.Fixture(); var person = sim.Living[worker];
        Assert.True(sim.Possess(worker)); Renew(sim);
        int start = person.Site;
        int target = Enumerable.Range(0, sim.World.Count).First(c => sim.Walkable(c) && sim.World.Distance(c, start) == 2 && sim.Route(start, c) is { Length: 3 });
        Assert.True(sim.CommandPerson(target, Activity.Chop)); sim.Step();
        Assert.Equal(1, sim.World.Distance(start, person.Site)); Assert.NotEqual(target, person.Site);
        sim.Step(); Assert.Equal(target, person.Site);
        sim.World.Trees[target] = 20f; sim.World.TreesK[target] = 20f;
        float before = person.Pack.Get("log"); sim.Step();
        Assert.True(person.Pack.Get("log") > before); Assert.True(sim.World.Trees[target] < 20f);
        Assert.False(sim.QueueProduction(band, new(worker, "recipe/twist_rope"), out _));
        sim.ReleaseControl(); Assert.Equal(-1, sim.ControlledAgentId); Assert.NotEmpty(sim.MindRequests());
    }

    [Fact]
    public void Distant_controlled_people_cannot_eat_camp_food_and_control_still_requires_a_model_plan()
    {
        var (sim, worker, band) = IndustryTests.Fixture(); var person = sim.Living[worker];
        Assert.True(sim.Possess(worker)); int day = sim.Day; Assert.Throws<InvalidOperationException>(() => sim.Step()); Assert.Equal(day, sim.Day);
        Renew(sim);
        int remote = Enumerable.Range(0, sim.World.Count).First(c => sim.Walkable(c) && sim.World.Distance(c, sim.Bands[band].Cell) > sim.World.Cells(2));
        person.Site = remote; Assert.True(sim.CommandPerson(remote, Activity.Rest));
        person.Pack.TakeFood(person.Food); person.Needs[(int)Need.Hunger] = 0.5f;
        sim.Step(); Assert.True(person.NeedOf(Need.Hunger) > 0.5f); Assert.True(sim.Bands[band].Stores.FoodValue > 100f);
    }

    [Fact]
    public void Control_and_local_work_survive_a_save_and_cancelled_work_returns_its_inputs()
    {
        var (sim, worker, band) = IndustryTests.Fixture(); sim.Living[worker].Knows.Add(TechId.Cordage); sim.Bands[band].Stores.Add("fiber", 10f);
        Assert.True(sim.QueueProduction(band, new(worker, "recipe/twist_rope", 3), out _)); sim.Step();
        Assert.True(sim.Possess(worker)); Assert.Null(sim.ProductionFor(worker)); Assert.Equal(ProductionState.Cancelled, sim.Production.Values.Single().State);
        Renew(sim); Assert.True(sim.CommandPerson(sim.Living[worker].Site, Activity.Forage));
        string file = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".evo");
        try { WorldSave.Save(sim, file); var loaded = WorldSave.Load(file); sim.Step(); loaded.Step(); Assert.Equal(worker, loaded.ControlledAgentId); Assert.Equal(sim.Living[worker].Food, loaded.Living[worker].Food); }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Shaping_land_updates_water_habitats_roads_and_flooded_buildings_without_refilling_remote_resources()
    {
        var (sim, _, band) = IndustryTests.Fixture(); var building = IndustryTests.Built(sim, band, "chest");
        int cell = building.Cell; sim.World.Elevation[cell] = 50f; sim.Roads[cell] = 3;
        int remote = Enumerable.Range(0, sim.World.Count).First(c => sim.World.IsDryLand(c) && sim.World.Distance(c, cell) > 5 && sim.World.TreesK[c] > 1f);
        sim.World.Trees[remote] = 0f;
        sim.Act(new(Power.LowerLand, cell, Radius: 0));
        Assert.False(sim.World.IsDryLand(cell)); Assert.False(building.Standing); Assert.False(sim.Roads.ContainsKey(cell));
        Assert.Equal(0f, sim.World.PlantStock[cell]); Assert.Equal(0f, sim.World.Trees[cell]); Assert.Equal(0f, sim.World.Trees[remote]); Assert.Equal(1f, sim.World.WaterAccess[cell]);
        sim.Act(new(Power.RaiseLand, cell, Radius: 0)); Assert.True(sim.World.IsLand(cell)); Assert.True(sim.World.PlantK[cell] >= 0f);
        Assert.All(sim.Bands.Values, b => Assert.True(sim.World.IsDryLand(b.Cell)));
    }

    [Fact]
    public void Plant_growth_uses_existing_habitat_limits_and_does_not_plant_the_ocean()
    {
        var (sim, _, band) = IndustryTests.Fixture(); int cell = sim.Bands[band].Cell;
        sim.World.PlantStock[cell] = 0f; sim.World.Trees[cell] = 0f; sim.Act(new(Power.SpawnPlant, cell, Radius: 0));
        Assert.Equal(sim.World.PlantK[cell], sim.World.PlantStock[cell]); Assert.Equal(sim.World.TreesK[cell], sim.World.Trees[cell]);
        int sea = Enumerable.Range(0, sim.World.Count).First(c => !sim.World.IsLand(c)); sim.Act(new(Power.SpawnPlant, sea, Radius: 0)); Assert.Equal(0f, sim.World.Trees[sea]);
    }
}
