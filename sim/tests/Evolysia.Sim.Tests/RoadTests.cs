using Evolysia.Sim.Agents;
using Evolysia.Sim.Economy;
using Evolysia.Sim.Persistence;

namespace Evolysia.Sim.Tests;

public class RoadTests
{
    static (Simulation sim, int worker, Structure source, Structure target, int[] path) Freight()
    {
        var (sim, worker, band) = IndustryTests.Fixture(); var b = sim.Bands[band];
        int end = Enumerable.Range(0, sim.World.Count).First(c => sim.Walkable(c) && sim.World.Distance(c, b.Cell) == 4 && sim.Route(b.Cell, c) is { Length: 5 });
        var source = sim.Place(b, Structures.Need("chest"), b.Cell); source.State = StructureState.Complete;
        var target = sim.Place(b, Structures.Need("crate"), end); target.State = StructureState.Complete;
        b.Stores.Attach(source.Contents); b.Stores.Attach(target.Contents); source.Contents.Add("iron_ingot", 8f); sim.Living[worker].Site = source.Cell;
        return (sim, worker, source, target, sim.Route(source.Cell, target.Cell)!);
    }

    [Fact]
    public void Completed_stone_roads_double_freight_movement_without_creating_cargo()
    {
        var (fast, worker, source, target, path) = Freight(); var (slow, slowWorker, slowSource, slowTarget, _) = Freight();
        foreach (int cell in path) fast.Roads[cell] = 3;
        Assert.True(fast.QueueShipment(source.BandId, new(worker, source.Id, target.Id, "iron_ingot", 8f), out _));
        Assert.True(slow.QueueShipment(slowSource.BandId, new(slowWorker, slowSource.Id, slowTarget.Id, "iron_ingot", 8f), out _));
        fast.Step(); slow.Step(); fast.Step(); slow.Step();
        Assert.Equal(2, fast.Hauls.Values.Single().RouteIndex); Assert.Equal(1, slow.Hauls.Values.Single().RouteIndex);
        for (int i = 0; i < 12; i++) { fast.Step(); slow.Step(); }
        Assert.Equal(8f, target.Contents.Get("iron_ingot"), 4); Assert.Equal(8f, slowTarget.Contents.Get("iron_ingot"), 4);
    }

    [Fact]
    public void Carriers_recheck_blocked_routes_and_never_walk_through_a_new_wall()
    {
        var (sim, worker, source, target, path) = Freight();
        Assert.True(sim.QueueShipment(source.BandId, new(worker, source.Id, target.Id, "iron_ingot", 8f), out _)); sim.Step();
        var wall = sim.Place(sim.Bands[source.BandId], Structures.Need("palisade"), path[1]); wall.State = StructureState.Complete;
        for (int i = 0; i < 20; i++) { sim.Step(); Assert.NotEqual(wall.Cell, sim.Hauls.Values.Single().Cell); }
        Assert.Equal(8f, source.Contents.Get("iron_ingot") + target.Contents.Get("iron_ingot") + sim.Living[worker].Pack.Get("iron_ingot"), 4);
        Assert.Equal(HaulState.Delivered, sim.Hauls.Values.Single().State);
    }

    [Fact]
    public void Builders_construct_a_connected_road_and_consume_the_exact_surface_material()
    {
        var (sim, worker, band) = IndustryTests.Fixture(); var b = sim.Bands[band];
        sim.Living[worker].Knows.Add(TechId.Roads); var end = IndustryTests.Built(sim, band, "chest");
        if (end.Cell == b.Cell) end = IndustryTests.Built(sim, band, "crate");
        var path = sim.Route(b.Cell, end.Cell, cardinal: true)!;
        foreach (int cell in path) { sim.World.Trees[cell] = 0f; sim.World.Rock[cell] = 0f; }
        b.Stores.Add("stone_blocks", path.Length);
        Assert.True(sim.QueueRoad(band, new(worker, -band, end.Id, "stone"), out var error), error);
        for (int i = 0; i < 40 && sim.RoadFor(worker) != null; i++) sim.Step();
        Assert.All(path, cell => Assert.Equal((byte)3, sim.Roads[cell])); Assert.Equal(0f, b.Stores.Get("stone_blocks"), 4);
        Assert.False(sim.RoadProjects.Values.Single().Active);
    }

    [Fact]
    public void Road_orders_reject_foreign_endpoints_unknown_materials_and_double_booked_workers()
    {
        var (sim, worker, band) = IndustryTests.Fixture(); var end = IndustryTests.Built(sim, band, "chest");
        Assert.False(sim.QueueRoad(band, new(worker, -band, end.Id, "invented_surface"), out _));
        Assert.False(sim.QueueRoad(band, new(worker, -band, end.Id, "stone"), out _));
        Assert.False(sim.QueueRoad(band, new(worker, -band, int.MaxValue, "path"), out _));
        Assert.Empty(sim.RoadProjects);
        sim.Living[worker].Knows.Add(TechId.Cordage); sim.QueueProduction(band, new(worker, "recipe/twist_rope"), out _);
        Assert.False(sim.QueueRoad(band, new(worker, -band, end.Id, "path"), out _));
    }

    [Fact]
    public void Road_progress_and_completed_cells_survive_a_save()
    {
        var (sim, worker, band) = IndustryTests.Fixture(); var b = sim.Bands[band]; var end = IndustryTests.Built(sim, band, "chest");
        if (end.Cell == b.Cell) end = IndustryTests.Built(sim, band, "crate");
        Assert.True(sim.QueueRoad(band, new(worker, -band, end.Id, "path"), out _)); sim.Step();
        string file = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".evo");
        try
        {
            WorldSave.Save(sim, file); var loaded = WorldSave.Load(file);
            for (int i = 0; i < 15; i++) { sim.Step(); loaded.Step(); }
            Assert.Equal(sim.Roads.OrderBy(p => p.Key), loaded.Roads.OrderBy(p => p.Key));
            Assert.Equal(sim.RoadProjects.Values.Single().Index, loaded.RoadProjects.Values.Single().Index);
        }
        finally { File.Delete(file); }
    }
}
