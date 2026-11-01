using System.Text.Json;
using Evolysia.Narration;
using Evolysia.Sim.Core;

namespace Evolysia.Sim.Tests;

public class CognitionTests
{
    internal static Simulation World() => new(new SimConfig { Width = 160, Height = 80, Tribes = 1, StartBands = 1, Threads = 1 });
    internal static MindPlan Plan(MindRequest r, string work = "forage") => new(r.Id, "Gather enough food for our families.",
        new[] { new WorkShare(work, 1f) }, Array.Empty<string>(), Array.Empty<string>(),
        r.Agents.Select(id => new PersonalIntent(id, work, "Feed my family.", "I remember our empty baskets.", "I will gather food.", "We agreed to gather before arguing.")).ToArray(),
        Array.Empty<DiplomaticIntent>(), "The basket circle", "No child should go hungry.");

    [Fact]
    public void Ordinary_worlds_require_model_plans_before_time_can_advance()
    {
        var sim = World();
        Assert.True(sim.Config.RequireModel);
        Assert.Throws<InvalidOperationException>(sim.Step);
        Assert.Equal(0, sim.Day);
        var request = Assert.Single(sim.MindRequests());
        Assert.True(sim.AcceptPlan(Plan(request), out _));
        sim.Step();
        Assert.Equal(1, sim.Day);
    }

    [Fact]
    public void Invalid_or_repeated_plans_cannot_mutate_world_state()
    {
        var sim = World(); var r = Assert.Single(sim.MindRequests()); var plan = Plan(r);
        Assert.False(sim.AcceptPlan(plan with { Projects = new[] { "unlimited_free_steel" } }, out _));
        Assert.False(sim.AcceptPlan(plan with { Sharing = float.NaN }, out _));
        Assert.False(sim.AcceptPlan(plan with { People = plan.People.Take(1).ToArray() }, out _));
        Assert.False(sim.AcceptPlan(plan with { Destination = sim.World.Count + 100 }, out _));
        Assert.Empty(sim.Minds); Assert.Empty(sim.PlanLog); Assert.Empty(sim.BandPlans);
        Assert.True(sim.AcceptPlan(plan, out _));
        Assert.False(sim.AcceptPlan(plan, out _));
        Assert.Single(sim.PlanLog);
    }

    [Fact]
    public void Model_intents_change_work_and_remembered_inner_life()
    {
        var gather = World(); var rest = World();
        var r = Assert.Single(gather.MindRequests()); var r2 = Assert.Single(rest.MindRequests());
        Assert.True(gather.AcceptPlan(Plan(r), out _)); Assert.True(rest.AcceptPlan(Plan(r2, "rest"), out _));
        gather.Step(); rest.Step();
        int id = r.Agents.First(i => gather.Living[i].IsAdult(0));
        Assert.Equal(Activity.Forage, gather.Living[id].Today);
        Assert.Equal(Activity.Rest, rest.Living[id].Today);
        Assert.Equal("Feed my family.", gather.Minds[id].Goal);
        Assert.Single(gather.Minds[id].Memories);
        Assert.Contains(gather.Chronicle.Moments, m => m.Kind == "conversation" && m.AgentId == id);
    }

    [Fact]
    public void Expired_plans_hold_all_speeds_without_instinct_fallback()
    {
        var sim = World(); sim.Config.PlanDays = 2;
        var r = Assert.Single(sim.MindRequests()); Assert.True(sim.AcceptPlan(Plan(r), out _));
        sim.Step(); sim.Step();
        Assert.True(sim.WaitingForMinds);
        Assert.Throws<InvalidOperationException>(sim.Step);
        Assert.Equal(2, sim.Day);
        Assert.Equal(0, sim.Stats.Year.InstinctChoices);
    }

    [Fact]
    public void Prompts_include_memories_and_do_not_offer_unknown_projects()
    {
        var sim = World(); var r = Assert.Single(sim.MindRequests()); sim.AcceptPlan(Plan(r), out _);
        for (int i = 0; i < sim.Config.PlanDays; i++) sim.Step();
        var next = Assert.Single(sim.MindRequests());
        var prompt = CognitionPrompts.Plan(sim, next);
        Assert.Contains("basket circle", prompt.User);
        Assert.Contains("labour", prompt.Schema.ToJsonString());
        Assert.Contains("EVERY supplied person", prompt.System);
        Assert.DoesNotContain("free_steel", prompt.User);
        var roundTrip = CognitionPrompts.Parse(JsonSerializer.Serialize(Plan(next), CognitionPrompts.Json));
        Assert.NotNull(roundTrip);
        Assert.True(sim.AcceptPlan(roundTrip!, out _));
    }

    sealed class OfflineBackend : INarrationBackend
    {
        public Task<string> CompleteAsync(NarrationRequest request, CancellationToken ct) => Task.FromException<string>(new HttpRequestException("offline"));
        public Task<bool> HealthyAsync(CancellationToken ct) => Task.FromResult(false);
        public string Describe() => "offline";
    }

    [Fact]
    public void Offline_model_cannot_unlock_world_progression()
    {
        var sim = World(); using var director = new CognitionDirector(new OfflineBackend());
        for (int i = 0; i < 5; i++) director.Pump(sim);
        Assert.False(director.Ready); Assert.True(director.Hold(sim)); Assert.Equal(0, sim.Day);
    }

    [Fact]
    public void Interventions_discard_stale_plans_and_require_a_new_observation()
    {
        var sim = World(); var request = Assert.Single(sim.MindRequests());
        sim.Act(new GodAction(Power.Rain, sim.Bands[request.BandId].Cell));
        Assert.False(sim.AcceptPlan(Plan(request), out _));
        var fresh = Assert.Single(sim.MindRequests()); Assert.NotEqual(request.Id, fresh.Id);
        Assert.True(sim.AcceptPlan(Plan(fresh), out _));
        sim.Act(new GodAction(Power.Drought, sim.Bands[request.BandId].Cell));
        Assert.True(sim.WaitingForMinds); Assert.Throws<InvalidOperationException>(sim.Step);
        Assert.Contains("Feed my family.", sim.Minds.Values.First().Memories.Select(m => m.Goal));
    }

    [Fact]
    public void Unread_messages_do_not_leak_text_into_council_knowledge()
    {
        var sim = World(); var request = Assert.Single(sim.MindRequests()); var band = sim.Bands[request.BandId];
        var inscription = new Inscription { Id = 1, Cell = band.Cell, Text = "Secret words behind unread marks" };
        sim.Inscriptions.Add(inscription);
        Assert.DoesNotContain(inscription.Text, CognitionPrompts.Plan(sim, request).User);
        inscription.Readings.Add((0, band.Id, MessageReading.Dread, false));
        Assert.DoesNotContain(inscription.Text, CognitionPrompts.Plan(sim, request).User);
        inscription.Readings[0] = (0, band.Id, MessageReading.Command, true);
        Assert.Contains(inscription.Text, CognitionPrompts.Plan(sim, request).User);
    }

    [Fact]
    public void Councils_observe_actual_family_and_relationships_without_creating_new_ones()
    {
        var sim = World(); var request = Assert.Single(sim.MindRequests());
        var person = sim.Living[request.Agents[0]]; int partner = request.Agents[1];
        person.PartnerId = partner; person.Rel(partner).Grudge = true; person.Rel(partner).Trust = 0.73f;
        int before = sim.Living.Values.Sum(a => a.Relations.Count);
        using var observation = JsonDocument.Parse(CognitionPrompts.Plan(sim, request).User);
        var snapshot = observation.RootElement.GetProperty("people").EnumerateArray().Single(p => p.GetProperty("id").GetInt32() == person.Id);
        Assert.Equal(partner, snapshot.GetProperty("family").GetProperty("partner_id").GetInt32());
        var relation = snapshot.GetProperty("relationships").EnumerateArray().Single(r => r.GetProperty("id").GetInt32() == partner);
        Assert.True(relation.GetProperty("grudge").GetBoolean()); Assert.Equal(0.73f, relation.GetProperty("trust").GetSingle());
        Assert.Equal(before, sim.Living.Values.Sum(a => a.Relations.Count));
    }

    [Fact]
    public void Councils_can_elect_leaders_and_invalid_officeholders_are_rejected()
    {
        var sim = World(); var r = Assert.Single(sim.MindRequests()); var band = sim.Bands[r.BandId];
        var successor = band.Members.Select(id => sim.Living[id]).First(a => a.IsAdult(0) && a.Id != band.LeaderId);
        Assert.False(sim.AcceptPlan(Plan(r) with { Community = new("elect", int.MaxValue) }, out _));
        Assert.Empty(sim.PlanLog);
        Assert.True(sim.AcceptPlan(Plan(r) with { Community = new("elect", successor.Id) }, out _));
        Assert.Equal(successor.Id, band.LeaderId);
        Assert.Contains(sim.Chronicle.Events, e => e.Kind == "election");
    }

    [Fact]
    public void Joining_requires_reciprocal_model_consent_and_refreshes_the_combined_council()
    {
        var sim = new Simulation(new SimConfig { Width = 160, Height = 80, Tribes = 1, StartBands = 2, Threads = 1 });
        var bands = sim.Bands.Values.ToArray(); Assert.Equal(2, bands.Length); bands[1].Cell = bands[0].Cell;
        int total = sim.Living.Count; var requests = sim.MindRequests();
        var a = requests.Single(r => r.BandId == bands[0].Id); var b = requests.Single(r => r.BandId == bands[1].Id);
        Assert.True(sim.AcceptPlan(Plan(a) with { Community = new("join", OtherBandId: b.BandId) }, out var error), error);
        Assert.Equal(2, sim.Bands.Count);
        Assert.True(sim.AcceptPlan(Plan(b) with { Community = new("join", OtherBandId: a.BandId) }, out error), error);
        Assert.Single(sim.Bands); Assert.Equal(total, sim.Bands.Values.Single().Members.Count);
        Assert.True(sim.WaitingForMinds); Assert.Throws<InvalidOperationException>(sim.Step);
    }
}
