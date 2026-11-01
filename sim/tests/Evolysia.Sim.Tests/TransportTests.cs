using Evolysia.Sim.Core;
using Evolysia.Sim.Economy;
using Evolysia.Sim.Persistence;

namespace Evolysia.Sim.Tests;

public class TransportTests
{
    static (Simulation sim, int worker, Structure source, Structure target) Fixture()
    {
        var sim = CognitionTests.World(); sim.Config.PlanDays = 90;
        var band = sim.Bands.Values.First();
        int targetCell = Enumerable.Range(0, sim.World.Count).First(c => c != band.Cell && sim.World.IsDryLand(c) && sim.World.Distance(c, band.Cell) == 1);
        var source = sim.Place(band, Structures.Need("chest"), band.Cell); source.State = StructureState.Complete;
        var target = sim.Place(band, Structures.Need("crate"), targetCell); target.State = StructureState.Complete;
        band.Stores.Attach(source.Contents); band.Stores.Attach(target.Contents);
        source.Contents.Add("iron_ingot", 8f);
        var request = Assert.Single(sim.MindRequests());
        Assert.True(sim.AcceptPlan(CognitionTests.Plan(request, "rest"), out _));
        return (sim, request.Agents.First(id => sim.Living[id].IsAdult(0)), source, target);
    }

    [Fact]
    public void Hauling_moves_one_physical_stack_without_creating_goods()
    {
        var (sim, worker, source, target) = Fixture();
        string identity = Assert.Single(source.Contents.Stacks).Id;
        Assert.True(sim.QueueShipment(source.BandId, new ShipmentOrder(worker, source.Id, target.Id, "iron_ingot", 8f), out _));
        Assert.Equal(0f, source.Contents.Take("iron_ingot", 8f));
        for (int day = 0; day < 10 && sim.Hauls.Values.Any(h => h.State != HaulState.Delivered); day++) sim.Step();
        var job = Assert.Single(sim.Hauls.Values);
        Assert.Equal(HaulState.Delivered, job.State);
        Assert.Equal(8f, job.Delivered, 4); Assert.Equal(8f, target.Contents.Get("iron_ingot"), 4);
        Assert.Equal(0f, source.Contents.Get("iron_ingot"), 4);
        Assert.Equal(identity, Assert.Single(target.Contents.Stacks, s => s.Good == Goods.Id("iron_ingot")).Id);
        Assert.Equal(8f, sim.Bands[source.BandId].Stores.Get("iron_ingot"), 4);
    }

    [Fact]
    public void Reservations_prevent_double_booking_and_reject_over_capacity_or_foreign_cargo()
    {
        var (sim, worker, source, target) = Fixture();
        Assert.False(sim.QueueShipment(source.BandId, new ShipmentOrder(worker, source.Id, target.Id, "iron_ingot", 1000f), out _));
        Assert.False(sim.QueueShipment(source.BandId + 10, new ShipmentOrder(worker, source.Id, target.Id, "iron_ingot", 1f), out _));
        Assert.Empty(sim.Hauls);
        Assert.True(sim.QueueShipment(source.BandId, new ShipmentOrder(worker, source.Id, target.Id, "iron_ingot", 8f), out _));
        Assert.False(sim.QueueShipment(source.BandId, new ShipmentOrder(worker, source.Id, target.Id, "iron_ingot", 8f), out _));
        Assert.Single(sim.Hauls);
    }

    [Fact]
    public void Saving_in_transit_preserves_reservations_stack_identity_and_delivery()
    {
        var (sim, worker, source, target) = Fixture();
        sim.QueueShipment(source.BandId, new ShipmentOrder(worker, source.Id, target.Id, "iron_ingot", 8f), out _);
        for (int i = 0; i < 5 && sim.Hauls.Values.First().State != HaulState.Carrying; i++) sim.Step();
        Assert.Equal(HaulState.Carrying, sim.Hauls.Values.First().State);
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".evo");
        try
        {
            WorldSave.Save(sim, path); var loaded = WorldSave.Load(path);
            for (int i = 0; i < 7; i++) { sim.Step(); loaded.Step(); }
            Assert.Equal(HaulState.Delivered, loaded.Hauls.Values.First().State);
            Assert.Equal(sim.Structures[target.Id].Contents.Get("iron_ingot"), loaded.Structures[target.Id].Contents.Get("iron_ingot"));
            Assert.Equal(sim.Structures[target.Id].Contents.Stacks.Select(s => s.Id), loaded.Structures[target.Id].Contents.Stacks.Select(s => s.Id));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Inventory_splits_preserve_lineage_and_invalid_amounts_cannot_mint_goods()
    {
        var source = new Inventory("granary", 100f); var target = new Inventory("cart", 4f);
        source.Add("iron_ingot", 10f); string original = source.Stacks[0].Id;
        float moved = source.TransferTo(target, Goods.Id("iron_ingot"), 20f);
        Assert.True(moved > 0f); Assert.True(target.Bulk <= target.Capacity + 1e-4f);
        Assert.Equal(10f, source.Get("iron_ingot") + target.Get("iron_ingot"), 4);
        Assert.Equal(original, target.Stacks[0].ParentId);
        Assert.Equal(0f, source.Take("iron_ingot", -100f));
        source.Add("iron_ingot", float.PositiveInfinity);
        Assert.Equal(10f, source.Get("iron_ingot") + target.Get("iron_ingot"), 4);
    }

    [Fact]
    public void Reserved_food_can_still_spoil_and_containers_have_one_location()
    {
        var food = new Inventory("chest", 100f); food.Add("meat_raw", 10f); Assert.True(food.Reserve(1, Goods.Id("meat_raw"), 10f));
        food.Spoil(1f); Assert.True(food.Get("meat_raw") < 10f);
        var root = new Inventory("camp"); root.Attach(food);
        Assert.Throws<InvalidOperationException>(() => new Inventory("other").Attach(food));
        Assert.Throws<InvalidOperationException>(() => food.Attach(root));
        Assert.Equal(food.Get("meat_raw"), root.Get("meat_raw"));
        food.Detach(); Assert.Equal(0f, root.Get("meat_raw"));
    }

    [Fact]
    public void Carriers_collect_the_vehicle_before_loading_and_do_not_leave_a_ghost_footprint()
    {
        var (sim, worker, source, target) = Fixture(); var band = sim.Bands[source.BandId];
        int garage = Enumerable.Range(0, sim.World.Count).First(c => sim.World.IsDryLand(c) && sim.World.StructureAt[c] == 0 && sim.World.Distance(c, source.Cell) == 1);
        var cart = sim.Place(band, Structures.Need("handcart"), garage); cart.State = StructureState.Complete; band.Stores.Attach(cart.Contents);
        sim.Living[worker].Site = source.Cell;
        Assert.True(sim.QueueShipment(band.Id, new ShipmentOrder(worker, source.Id, target.Id, "iron_ingot", 8f, cart.Id), out var error), error);
        var job = Assert.Single(sim.Hauls.Values);
        Assert.Equal(HaulState.GoingToVehicle, job.State); Assert.Equal(garage, cart.Cell);
        sim.Step(); Assert.Equal(garage, job.Cell); Assert.Equal(garage, cart.Cell);
        for (int i = 0; i < 20 && job.Active; i++) sim.Step();
        Assert.Equal(HaulState.Delivered, job.State); Assert.Equal(target.Cell, cart.Cell); Assert.Equal(target.Cell, cart.TransitCell);
        Assert.NotEqual(cart.Id, sim.World.StructureAt[garage]); Assert.Equal(8f, target.Contents.Get("iron_ingot"), 4);
    }
}
