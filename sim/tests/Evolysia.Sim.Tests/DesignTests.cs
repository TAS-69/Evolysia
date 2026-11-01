using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Evolysia.Sim.Economy;
using Evolysia.Sim.Persistence;

namespace Evolysia.Sim.Tests;

public class DesignTests
{
    [Fact]
    public void Generated_buildings_create_costed_projects_with_original_names_and_layouts()
    {
        var sim = CognitionTests.World(); var r = Assert.Single(sim.MindRequests());
        var idea = new BuildingIdea("Reedlight shelter", "windbreak", 5, 4, new[] { new RoomObject("bed", 1, 1) });
        Assert.True(sim.ValidateDesign(sim.Bands[r.BandId], idea, out var error), error);
        Assert.True(sim.AcceptPlan(CognitionTests.Plan(r) with { Designs = new[] { idea } }, out error), error);
        var design = Assert.Single(sim.Designs.Values);
        Assert.Equal(5, design.Definition.W); Assert.Equal(4, design.Definition.H);
        Assert.True(design.Definition.Materials.Sum(m => m.qty) > Structures.Need("windbreak").Materials.Sum(m => m.qty));
        sim.Step();
        var project = Assert.Single(sim.Structures.Values, s => s.CustomName == "Reedlight shelter");
        Assert.Equal(StructureState.Planned, project.State);
        Assert.NotNull(project.Layout); Assert.Equal("bed", Assert.Single(project.Layout!.Objects).Kind);
    }

    [Fact]
    public void Impossible_or_blocked_designs_are_rejected_atomically()
    {
        var sim = CognitionTests.World(); var r = Assert.Single(sim.MindRequests()); var band = sim.Bands[r.BandId];
        var bad = new BuildingIdea("Sealed chamber", "windbreak", 4, 5, new[] { new RoomObject("bed", 2, 3), new RoomObject("bed", 1, 1) });
        Assert.False(sim.ValidateDesign(band, bad, out _));
        Assert.False(sim.ValidateDesign(band, bad with { Width = 1000 }, out _));
        Assert.False(sim.ValidateDesign(band, bad with { Template = "future_fabricator" }, out _));
        Assert.False(sim.AcceptPlan(CognitionTests.Plan(r) with { Designs = new[] { bad } }, out _));
        Assert.Empty(sim.Designs); Assert.Empty(sim.PlanLog); Assert.Empty(sim.Minds);
    }

    [Fact]
    public void Specialist_rooms_build_independent_fixtures_and_preserve_their_inventory_ownership()
    {
        var sim = new Simulation(new SimConfig { RequireModel = false, Width = 160, Height = 80, Tribes = 1, StartBands = 1, Threads = 1 });
        var band = sim.Bands.Values.First();
        foreach (int id in band.Members) foreach (var tech in Enum.GetValues<TechId>()) sim.Living[id].Knows.Add(tech);
        foreach (var good in Goods.All) band.Stores.Add(good.Index, 500f);
        var definition = Structures.Need("smithy");
        int cell = Enumerable.Range(0, sim.World.Count).First(c => sim.World.Y(c) + definition.H < sim.World.H &&
            Enumerable.Range(0, definition.W * definition.H).All(i => sim.World.IsDryLand(sim.World.Index(sim.World.X(c) + i % definition.W, sim.World.Y(c) + i / definition.W))));
        var room = sim.Place(band, definition, cell); room.State = StructureState.Complete;
        sim.Step();
        Assert.Equal(7, room.Kind.W); Assert.Equal(6, room.Kind.H);
        Assert.Contains(sim.Structures.Values, s => s.ParentBuildingId == room.Id && s.Kind.Id == "anvil");
        Assert.All(sim.Structures.Values.Where(s => s.ParentBuildingId == room.Id), s => Assert.Equal(room.Id, sim.World.StructureAt[s.Cell]));
        string? fixture = Environment.GetEnvironmentVariable("EVOLYSIA_REVIEW_SAVE");
        if (!string.IsNullOrWhiteSpace(fixture)) WorldSave.Save(sim, fixture);
    }

    [Fact]
    public void Regional_material_substitutions_are_costed_once_per_good()
    {
        var sim = CognitionTests.World(); var band = sim.Bands.Values.First(); sim.World.Biome[band.Cell] = Biome.TropicalRainforest;
        var materials = sim.ConstructionMaterials(band, Structures.Need("house"));
        Assert.Equal(materials.Length, materials.Select(m => m.good).Distinct().Count());
        Assert.Contains(materials, m => m.good == Goods.Id("bamboo"));
        Assert.DoesNotContain(materials, m => m.good == Goods.Id("wood") || m.good == Goods.Id("log"));
        Assert.Equal(Structures.Need("house").Materials.Sum(m => m.qty), materials.Sum(m => m.qty));
    }
}
