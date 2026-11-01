using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Evolysia.Sim.Economy;
using Evolysia.Sim.Persistence;

namespace Evolysia.Sim.Tests;

public class IndustryTests
{
    internal static (Simulation sim, int worker, int band) Fixture()
    {
        var sim = CognitionTests.World(); sim.Config.PlanDays = 90;
        var request = Assert.Single(sim.MindRequests()); var band = sim.Bands[request.BandId];
        band.Stores.Add("food", 3000f);
        Assert.True(sim.AcceptPlan(CognitionTests.Plan(request, "rest"), out _));
        return (sim, request.Agents.First(id => sim.Living[id].IsAdult(0)), band.Id);
    }

    internal static Structure Built(Simulation sim, int band, string kind)
    {
        var def = Structures.Need(kind); var b = sim.Bands[band];
        int cell = Enumerable.Range(0, sim.World.Count).Where(c => sim.World.Y(c) + def.H <= sim.World.H && sim.World.X(c) + def.W <= sim.World.W)
            .OrderBy(c => sim.World.Distance(c, b.Cell)).First(c => Enumerable.Range(0, def.W * def.H).All(i =>
                sim.World.IsDryLand(c + i / def.W * sim.World.W + i % def.W) && sim.World.StructureAt[c + i / def.W * sim.World.W + i % def.W] == 0));
        var result = sim.Place(b, def, cell); result.State = StructureState.Complete; return result;
    }

    [Fact]
    public void Manufacturing_completes_the_ordered_recipe_and_rejects_a_second_job_for_the_worker()
    {
        var (sim, worker, band) = Fixture(); sim.Living[worker].Knows.Add(TechId.Cordage); sim.Bands[band].Stores.Add("fiber", 20f);
        Assert.True(sim.QueueProduction(band, new(worker, "recipe/twist_rope", 3), out var error), error);
        Assert.False(sim.QueueProduction(band, new(worker, "recipe/twist_rope"), out _));
        for (int i = 0; i < 20 && sim.ProductionFor(worker) != null; i++) sim.Step();
        var job = Assert.Single(sim.Production.Values);
        Assert.Equal(ProductionState.Complete, job.State); Assert.Equal(3, job.Completed); Assert.Equal(3f, sim.Bands[band].Stores.Get("rope"), 4);
        Assert.Empty(job.Inputs.Stacks); Assert.True(sim.Bands[band].Stores.Get("fiber") < 14.01f);
    }

    [Fact]
    public void Powered_work_waits_without_fuel_and_consumes_actual_supplies_after_generation_is_enabled()
    {
        var (sim, worker, band) = Fixture(); var b = sim.Bands[band]; sim.Living[worker].Knows.Add(TechId.Electronics);
        Built(sim, band, "workbench"); foreach (string g in new[] { "components", "glass", "copper_ingot" }) b.Stores.Add(g, 10f);
        Assert.True(sim.QueueProduction(band, new(worker, "recipe/electronics"), out var error), error);
        sim.Step(); Assert.Equal(ProductionState.WaitingPower, sim.ProductionFor(worker)!.State); Assert.Equal(0f, b.Stores.Get("electronics"));
        Built(sim, band, "generator"); b.Stores.Add("coal", 10f); sim.Reconsider(band);
        var request = Assert.Single(sim.MindRequests()); Assert.True(sim.AcceptPlan(CognitionTests.Plan(request, "rest") with { Utilities = new(true, 0f) }, out error), error);
        for (int i = 0; i < 20 && sim.ProductionFor(worker) != null; i++) sim.Step();
        Assert.Equal(1f, b.Stores.Get("electronics"), 4); Assert.True(b.Stores.Get("coal") < 10f);
        Assert.True(sim.Energy[band].Produced > 0f); Assert.True(sim.Production.Values.Single().Completed == 1);
    }

    [Fact]
    public void Cancelling_work_preserves_staged_inputs_at_the_work_site_without_creating_equipment()
    {
        var (sim, worker, band) = Fixture(); sim.Living[worker].Knows.Add(TechId.Bow);
        foreach (string g in new[] { "wood", "fiber", "flint" }) sim.Bands[band].Stores.Add(g, 10f);
        Assert.True(sim.QueueProduction(band, new(worker, "gear/tool/bow", RecipientId: worker), out _));
        sim.Step(); var job = Assert.Single(sim.Production.Values); Assert.NotEmpty(job.Inputs.Stacks);
        var staged = job.Inputs.Held().ToDictionary(x => x.good.Id, x => x.qty);
        Assert.True(sim.CancelProduction(job.Id)); Assert.Empty(job.Inputs.Stacks);
        foreach (var (good, quantity) in staged) Assert.Equal(quantity, sim.GroundAt(job.Cell).Get(good), 4);
        Assert.DoesNotContain(sim.Living[worker].Gear, i => i.Kind.Id == "bow");
    }

    [Fact]
    public void Production_and_stored_energy_survive_save_and_resume()
    {
        var (sim, worker, band) = Fixture(); sim.Living[worker].Knows.Add(TechId.Bow);
        foreach (string g in new[] { "wood", "fiber", "flint" }) sim.Bands[band].Stores.Add(g, 10f);
        sim.QueueProduction(band, new(worker, "gear/tool/bow", RecipientId: worker), out _); sim.Step();
        string file = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".evo");
        try
        {
            WorldSave.Save(sim, file); var loaded = WorldSave.Load(file);
            for (int i = 0; i < 12; i++) { sim.Step(); loaded.Step(); }
            Assert.Equal(ProductionState.Complete, loaded.Production.Values.Single().State);
            Assert.Contains(loaded.Living[worker].Gear, i => i.Kind.Id == "bow");
            Assert.Equal(sim.Production.Values.Single().Completed, loaded.Production.Values.Single().Completed);
            Assert.Equal(sim.Bands[band].Stores.Get("wood"), loaded.Bands[band].Stores.Get("wood"));
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Staffed_care_consumes_medicine_and_shortens_illness()
    {
        var (sim, worker, band) = Fixture(); var b = sim.Bands[band]; var clinic = Built(sim, band, "medical_table");
        sim.Living[worker].Knows.Add(TechId.Herbalism); int target = b.Members.First(id => id != worker);
        var patient = sim.Living[target]; patient.Health = 0.4f; patient.Sick = true; patient.SickUntil = 20;
        b.Stores.Add("medicine", 2f); sim.Reconsider(band); var request = Assert.Single(sim.MindRequests());
        Assert.True(sim.AcceptPlan(CognitionTests.Plan(request, "rest") with { Services = new[] { new ServiceOrder(worker, clinic.Id, "care", new[] { target }) } }, out var error), error);
        sim.Step(); Assert.Equal(18, patient.SickUntil); Assert.True(patient.Health > 0.4f); Assert.True(b.Stores.Get("medicine") < 1.81f);
        Assert.Equal(1, sim.Civics[band].Patients); Assert.Equal(clinic.Cell, sim.Living[worker].Site);
    }

    [Fact]
    public void Schools_teach_the_chosen_known_subject_without_inventing_unrelated_knowledge()
    {
        var (sim, worker, band) = Fixture(); var library = Built(sim, band, "lectern"); var b = sim.Bands[band];
        sim.Living[worker].Knows.Add(TechId.Woodcutting); int pupilId = b.Members.First(id => id != worker); var pupil = sim.Living[pupilId];
        pupil.Knows = TechSet.Of(TechId.StoneTools); sim.Reconsider(band); var request = Assert.Single(sim.MindRequests());
        Assert.True(sim.AcceptPlan(CognitionTests.Plan(request, "rest") with { Services = new[] { new ServiceOrder(worker, library.Id, "teach", new[] { pupilId }, "woodcutting") } }, out var error), error);
        for (int i = 0; i < 35 && !pupil.Knows.Has(TechId.Woodcutting); i++) sim.Step();
        Assert.True(pupil.Knows.Has(TechId.Woodcutting)); Assert.Equal(2, pupil.Knows.Count); Assert.Equal(1, sim.Civics[band].Lessons);
    }

    [Fact]
    public void Staffed_settlements_combine_manufacturing_care_teaching_belief_and_defence()
    {
        var (sim, _, band) = Fixture(); var b = sim.Bands[band];
        foreach (int id in b.Members) foreach (var tech in Enum.GetValues<TechId>()) sim.Living[id].Knows.Add(tech);
        foreach (var good in Goods.All) b.Stores.Add(good.Index, 20f);
        var clinic = Built(sim, band, "medical_table"); var school = Built(sim, band, "lectern");
        var shrine = Built(sim, band, "altar"); var guard = Built(sim, band, "weapons_rack");
        Built(sim, band, "workbench"); Built(sim, band, "generator"); Built(sim, band, "battery_bank"); Built(sim, band, "solar_panel");
        int[] staff = b.Members.Where(id => sim.Living[id].IsAdult(0)).Take(6).ToArray();
        var pupil = sim.Living[b.Members.First(id => !staff.Contains(id))]; pupil.Knows = TechSet.Of(TechId.StoneTools); pupil.Sick = true; pupil.SickUntil = 50; pupil.Health = 0.5f;
        sim.Reconsider(band); var request = Assert.Single(sim.MindRequests());
        var plan = CognitionTests.Plan(request, "rest") with { Institution = "The Reed Archive", Doctrine = "Care for those who keep the stories alive.",
            Utilities = new(true, 0.2f), Production = new[] { new ProductionOrder(staff[0], "recipe/twist_rope", 1) },
            Services = new[] { new ServiceOrder(staff[1], clinic.Id, "care", new[] { pupil.Id }), new ServiceOrder(staff[2], school.Id, "teach", new[] { pupil.Id }, "woodcutting"),
                new ServiceOrder(staff[3], guard.Id, "guard", Array.Empty<int>()), new ServiceOrder(staff[4], shrine.Id, "worship", new[] { pupil.Id }) } };
        Assert.True(sim.AcceptPlan(plan, out var error), error);
        for (int i = 0; i < 5; i++) sim.Step();
        Assert.True(sim.Civics[band].Patients > 0); Assert.True(sim.Civics[band].Preparedness > 0f); Assert.True(sim.Civics[band].Ceremonies > 0);
        Assert.Contains(sim.Memes.Values, m => m.Name == "The Reed Archive" && m.Doctrine == plan.Doctrine);
        Assert.Equal(ProductionState.Complete, sim.Production.Values.Single().State); Assert.True(sim.Energy[band].Stored > 0f);
        string? path = Environment.GetEnvironmentVariable("EVOLYSIA_WORKSHOP_SAVE");
        if (!string.IsNullOrWhiteSpace(path)) { sim.Reconsider(band); WorldSave.Save(sim, path); }
    }
}
