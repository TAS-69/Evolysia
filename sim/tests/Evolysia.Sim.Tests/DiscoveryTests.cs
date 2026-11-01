using Evolysia.Narration;
using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;

namespace Evolysia.Sim.Tests;

/// <summary>With minds in charge, knowledge is found by experiment and learned from experience, never handed over.</summary>
public class DiscoveryTests
{
    /// <summary>A council plan; with experiments, half the people experiment and the rest forage so nobody starves.</summary>
    static MindPlan Plan(MindRequest r, string work, params ExperimentIntent[] experiments)
    {
        var plan = CognitionTests.Plan(r, work) with { Experiments = experiments };
        if (work != "experiment") return plan;
        return plan with
        {
            Labour = new[] { new WorkShare("forage", 2f), new WorkShare("experiment", 1f) },
            People = plan.People.Select((p, i) => p with { Activity = i % 2 == 0 ? "experiment" : "forage" }).ToArray(),
        };
    }

    /// <summary>Runs days, answering every council with the same plan (as a steady mind would).</summary>
    static void Live(Simulation sim, int days, Func<MindRequest, MindPlan> plan)
    {
        for (int d = 0; d < days && sim.Living.Count > 0; d++)
        {
            foreach (var r in sim.MindRequests()) Assert.True(sim.AcceptPlan(plan(r), out var error), error);
            foreach (var x in sim.PendingDecisions.ToArray()) sim.Decide(x.Id, x.Options.Contains("double_down") ? "double_down" : x.Options[0]);   // a mind that stands by its inventions
            sim.Step();
        }
    }

    [Fact]
    public void Every_technique_has_a_hidden_way_in_made_of_known_things()
    {
        foreach (var t in Techs.All)
        {
            Assert.True(Discovery.HasKey(t.Id), $"{t.Id} has no discovery key");
            var key = Discovery.Key(t.Id);
            Assert.NotEmpty(key.Subjects);
            Assert.All(key.Subjects, s => Assert.Contains(s, Discovery.Subjects));
            Assert.False(string.IsNullOrWhiteSpace(key.Sign));
        }
    }

    [Fact]
    public void Councils_get_no_research_menu_and_no_manual_only_lore_and_things_to_work_with()
    {
        var sim = CognitionTests.World();
        var r = Assert.Single(sim.MindRequests());
        Assert.Empty(r.Research);
        Assert.Contains("people", r.Subjects!);
        var prompt = CognitionPrompts.Plan(sim, r);
        Assert.DoesNotContain("forage gathers edible wild food", prompt.System);
        Assert.Contains("\"lore\"", prompt.User);
        Assert.Contains("\"can_work_with\"", prompt.User);
        Assert.Contains("tradition", prompt.User);
        Assert.DoesNotContain("\"research\":[\"", prompt.User);
    }

    [Fact]
    public void Experiments_must_name_a_listed_action_and_things_the_people_can_work_with()
    {
        var sim = CognitionTests.World();
        var r = Assert.Single(sim.MindRequests());
        Assert.False(sim.AcceptPlan(Plan(r, "forage", new ExperimentIntent("transmute", new[] { "stone" }, "gold")), out _));
        Assert.False(sim.AcceptPlan(Plan(r, "forage", new ExperimentIntent("heat", new[] { "unobtainium" }, "magic")), out _));
        Assert.False(sim.AcceptPlan(Plan(r, "forage", new ExperimentIntent("heat", new[] { "people", "people" }, "twice")), out _));
        Assert.True(sim.AcceptPlan(Plan(r, "forage", new ExperimentIntent("watch", new[] { "sky" }, "What do the lights mean?")), out var error), error);
        Assert.Single(sim.ExperimentsOf(sim.Bands[r.BandId]));
    }

    [Fact]
    public void The_right_experiment_finds_a_technique_and_it_enters_their_lore()
    {
        var sim = CognitionTests.World();
        var band = sim.Bands.Values.Single();
        var r = sim.MindRequests().Single();
        var target = sim.Findable(band).Where(t => Discovery.Key(t.Id).Subjects.All(r.Subjects!.Contains)).OrderBy(t => t.Difficulty).First();
        var key = Discovery.Key(target.Id);
        var try_ = new ExperimentIntent(Discovery.Verb(key.Method), key.Subjects, "Try it the way the clue suggests.");
        Live(sim, 120, req => Plan(req, "experiment", try_));
        Assert.True(sim.BandKnowledge(band).Has(target.Id), $"{target.Id} was not found. Lore: {string.Join(" / ", sim.LoreFor(band).Select(l => l.about + ": " + l.text))}. Under way: {string.Join(", ", sim.ExperimentsOf(band).Select(x => x.Method + " " + x.Effort))}. Events: {string.Join(" / ", sim.Chronicle.Events.Where(e => e.Day > 0).TakeLast(12).Select(e => e.Day + " " + e.Kind + " " + e.Text))}");
        Assert.Contains(sim.LoreFor(band), l => l.about == "knowledge" && l.text.Contains(key.Use));
        Assert.Contains(sim.Chronicle.Events, e => e.Kind == "invention" && e.BandId == band.Id);
    }

    [Fact]
    public void A_pointless_experiment_becomes_a_dead_end_not_knowledge()
    {
        var sim = CognitionTests.World();
        var band = sim.Bands.Values.Single();
        var r = sim.MindRequests().Single();
        var findable = sim.Findable(band).ToArray();
        var known = sim.BandKnowledge(band);
        // Something that touches no way in: an action and one thing that match nothing findable.
        var (method, subject) = Enum.GetValues<Method>().SelectMany(m => r.Subjects!.Select(s => (m, s)))
            .First(x => findable.All(t => Discovery.Match(Discovery.Key(t.Id), x.m, new[] { x.s }) < 0.5f));
        Live(sim, 40, req => Plan(req, "experiment", new ExperimentIntent(Discovery.Verb(method), new[] { subject }, "Perhaps?")));
        Assert.True((sim.BandKnowledge(band) & ~known).IsEmpty, "a pointless experiment found something");
        Assert.Contains(sim.LoreFor(band), l => l.about == "dead end");
    }

    [Fact]
    public void People_learn_what_their_work_brings_by_doing_it()
    {
        var sim = CognitionTests.World();
        var band = sim.Bands.Values.Single();
        Assert.DoesNotContain(sim.LoreFor(band), l => l.about == "work");
        Live(sim, 6, r => CognitionTests.Plan(r, "forage"));
        var forage = Assert.Single(sim.LoreFor(band), l => l.about == "work" && l.text.StartsWith("Forage"));
        Assert.Contains("a day of it brought about", forage.text);
    }

    [Fact]
    public void Rules_only_worlds_keep_no_lore()
    {
        var sim = new Simulation(new SimConfig { RequireModel = false, Width = 160, Height = 80, Tribes = 1, StartBands = 1, Threads = 1 });
        sim.RunYears(1);
        Assert.Empty(sim.Lore);
    }

    [Fact]
    public void Model_worlds_with_lore_and_experiments_are_identical_for_any_thread_count()
    {
        string History(int threads)
        {
            var sim = new Simulation(new SimConfig { Width = 160, Height = 80, Tribes = 2, StartBands = 2, Threads = threads, Seed = 11 });
            Live(sim, 90, r => Plan(r, r.Id % 2 == 0 ? "experiment" : "forage",
                new ExperimentIntent("heat", r.Subjects!.Take(2).ToArray(), "See what changes."),
                new ExperimentIntent("tie", r.Subjects!.Skip(1).Take(1).ToArray(), "Bind it.")));
            return string.Join("|", sim.Living.Values.OrderBy(a => a.Id).Select(a => $"{a.Id}:{a.Health:0.0000}:{a.Knows.GetHashCode()}:{a.Today}")) + "#" +
                   string.Join("|", sim.Lore.OrderBy(l => l.Key).SelectMany(l => l.Value.Entries.Values.OrderBy(e => e.Key).Select(e => e.Key + "=" + e.Text)));
        }
        Assert.Equal(History(1), History(4));
    }
}
