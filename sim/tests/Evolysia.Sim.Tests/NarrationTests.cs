using System.Text.Json.Nodes;
using Evolysia.Narration;

namespace Evolysia.Sim.Tests;

public class NarrationTests
{
    sealed class FakeBackend : INarrationBackend
    {
        public bool Up = true;
        public int Calls;
        public Task<bool> HealthyAsync(CancellationToken ct) => Task.FromResult(Up);
        public string Describe() => "fake";
        public Task<string> CompleteAsync(NarrationRequest r, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            var o = new JsonObject();
            foreach (var f in r.Schema["required"]!.AsArray()) o[f!.GetValue<string>()] = $"{f} of {r.AgentName}";
            return Task.FromResult(o.ToJsonString());
        }
    }

    static Simulation SimWithMoments()
    {
        var sim = new Simulation(new SimConfig { Seed = 5 });
        sim.RunYears(25);
        return sim;
    }

    [Fact]
    public void Every_moment_kind_builds_a_prompt_with_the_decided_facts()
    {
        var sim = SimWithMoments();
        Assert.NotEmpty(sim.Chronicle.Moments);
        var built = 0;
        for (int i = 0; i < sim.Chronicle.Moments.Count; i++)
        {
            var m = sim.Chronicle.Moments[i];
            var r = Prompts.Build(sim, i, m);
            if (r == null) continue;
            built++;
            Assert.Contains(r.AgentName, r.User);
            Assert.Contains("Personality:", r.User);
            Assert.Contains("decided, do not change it", r.User);
            Assert.NotEmpty(r.Schema["required"]!.AsArray());
            if (m.Kind == "pivot") Assert.Contains(m.Facts["subject"], r.User);
            if (m.Kind == "myth") Assert.Contains(m.Facts["deity"], r.User);
        }
        Assert.True(built > 0);
    }

    [Fact]
    public async Task Narrator_prefers_important_moments_and_drops_the_rest()
    {
        var sim = SimWithMoments();
        var requests = sim.Chronicle.Moments.Select((m, i) => Prompts.Build(sim, i, m)).Where(r => r != null).Select(r => r!).ToList();
        var backend = new FakeBackend();
        var narrator = new Narrator(backend, maxConcurrent: 2, maxQueue: 5);
        foreach (var r in requests) narrator.Offer(r);
        Assert.Equal(Math.Min(5, requests.Count), narrator.Status.Queued);
        Assert.Equal(Math.Max(0, requests.Count - 5), narrator.Status.Dropped);

        var results = new List<Narration.Narration>();
        for (int tick = 0; tick < 200 && results.Count < Math.Min(5, requests.Count); tick++)
        {
            narrator.Pump();
            await Task.Delay(5);
            while (narrator.TryTake(out var n)) results.Add(n);
        }
        Assert.Equal(Math.Min(5, requests.Count), results.Count);
        float lowestKept = results.Min(n => Prompts.Priority(n.Moment.Kind));
        float highestOffered = requests.Max(r => r.BasePriority);
        Assert.True(results.Any(n => Prompts.Priority(n.Moment.Kind) == highestOffered), "the most important story must survive");
        Assert.All(results, n => Assert.NotEmpty(n.Quote));
        var first = results[0];
        Assert.Contains(first, narrator.ForAgent(first.Moment.AgentId));
        Assert.True(lowestKept >= 1f);
    }

    [Fact]
    public void Malformed_or_incomplete_output_is_rejected()
    {
        var sim = SimWithMoments();
        var r = sim.Chronicle.Moments.Select((m, i) => Prompts.Build(sim, i, m)).First(x => x != null)!;
        Assert.Null(Narrator.Parse(r, "not json", 1));
        Assert.Null(Narrator.Parse(r, "{\"unrelated\":\"x\"}", 1));
    }

    [Theory]
    [InlineData("We walk at dawn. The river will not have us", "We walk at dawn.")]
    [InlineData("Hold hands to keep the dark退 away.", "Hold hands to keep the dark away.")]
    [InlineData("I will not stop", "I will not stop…")]
    [InlineData("\"Go.\"", "\"Go.\"")]
    public void Clean_trims_cut_off_sentences_and_foreign_script(string raw, string expected) =>
        Assert.Equal(expected, Narrator.Clean(raw));

    [Fact]
    public void Offline_backend_queues_without_calling()
    {
        var backend = new FakeBackend { Up = false };
        var narrator = new Narrator(backend);
        var sim = SimWithMoments();
        var r = sim.Chronicle.Moments.Select((m, i) => Prompts.Build(sim, i, m)).First(x => x != null)!;
        narrator.Offer(r);
        narrator.Pump();
        Thread.Sleep(50);
        narrator.Pump();
        Assert.Equal(0, backend.Calls);
        Assert.Equal(1, narrator.Status.Queued);
    }
}
