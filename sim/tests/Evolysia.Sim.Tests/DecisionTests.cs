using System.Text.Json.Nodes;
using Evolysia.Narration;

namespace Evolysia.Sim.Tests;

public class DecisionTests
{
    static Simulation Deferred(ulong seed = 5) => new(new SimConfig { Seed = seed }) { DeferDecisions = true };

    [Fact]
    public void Without_a_mind_instinct_decides_at_the_deadline()
    {
        var sim = Deferred();
        sim.RunYears(30);
        Assert.NotEmpty(sim.Decisions);
        Assert.All(sim.Decisions.Where(d => d.Deadline < sim.Day), d => Assert.True(d.Resolved));
        Assert.All(sim.Decisions.Where(d => d.Resolved), d =>
        {
            Assert.Equal("instinct", d.Source);
            Assert.True(d.Deadline - d.Day == sim.DecisionWindowDays);
        });
        Assert.True(sim.Stats.History.Sum(r => r.InstinctChoices) > 0);
        Assert.True(sim.Stats.History.Sum(r => r.Pivots.Sum()) > 0, "pivots still happen through deferred decisions");
    }

    [Fact]
    public void A_mind_chooses_only_among_sanctioned_options_while_pending()
    {
        var sim = Deferred();
        Decision? d = null;
        for (int i = 0; i < 30 * SimConfig.DaysPerYear && d == null; i++)
        {
            sim.Step();
            d = sim.PendingDecisions.FirstOrDefault();
        }
        Assert.NotNull(d);
        Assert.InRange(d!.Options.Length, 2, 3);
        Assert.False(sim.Decide(d.Id, "not_an_option"));
        string pick = d.Options[^1];
        Assert.True(sim.Decide(d.Id, pick));
        Assert.True(d.Resolved);
        Assert.Equal("mind", d.Source);
        Assert.Equal(pick, d.Chosen);
        Assert.False(sim.Decide(d.Id, d.Options[0]), "a decision can only be made once");
        Assert.Contains(sim.DecisionLog, e => e.DecisionId == d.Id && e.Choice == pick && e.Source == "mind");
    }

    [Fact]
    public void Same_seed_and_same_minds_replay_the_same_history()
    {
        string Run()
        {
            var sim = Deferred(seed: 17);
            int end = 25 * SimConfig.DaysPerYear;
            while (sim.Day < end && sim.Living.Count > 0)
            {
                sim.Step();
                foreach (var d in sim.PendingDecisions.ToList())
                    if (sim.Day - d.Day >= 3) sim.Decide(d.Id, d.Options[^1]);
            }
            return $"{sim.Living.Count}|{sim.Living.Keys.Sum()}|{sim.Chronicle.Events.Count}|{sim.DecisionLog.Count}|" +
                   string.Join(",", sim.DecisionLog.Select(e => e.Choice));
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void Decision_prompts_offer_exactly_the_shortlist_and_parse_the_choice()
    {
        var sim = Deferred();
        LlmMoment? m = null; int index = -1;
        for (int i = 0; i < 30 * SimConfig.DaysPerYear && m == null; i++)
        {
            sim.Step();
            index = sim.Chronicle.Moments.FindIndex(x => x.Kind == "decision");
            if (index >= 0) m = sim.Chronicle.Moments[index];
        }
        Assert.NotNull(m);
        var req = Prompts.Build(sim, index, m!)!;
        var options = m!.Facts["options"].Split(',');
        var allowed = req.Schema["properties"]!["choice"]!["enum"]!.AsArray().Select(n => n!.GetValue<string>()).ToArray();
        Assert.Equal(options.OrderBy(x => x), allowed.OrderBy(x => x));
        foreach (var o in options) Assert.Contains($"- {o}:", req.User);
        Assert.Contains("personality should drive the choice", req.System, StringComparison.OrdinalIgnoreCase);

        var good = Narrator.Parse(req, new JsonObject { ["thought"] = "t", ["choice"] = options[0], ["says"] = "s" }.ToJsonString(), 1)!;
        Assert.Equal((int.Parse(m.Facts["decision_id"]), options[0]), Prompts.DecisionChoice(good));
        var bad = Narrator.Parse(req, new JsonObject { ["thought"] = "t", ["choice"] = "fly_away", ["says"] = "s" }.ToJsonString(), 1)!;
        Assert.Null(Prompts.DecisionChoice(bad));
    }
}
