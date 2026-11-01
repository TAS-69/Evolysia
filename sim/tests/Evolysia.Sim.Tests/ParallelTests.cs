namespace Evolysia.Sim.Tests;

public class ParallelTests
{
    static string Fingerprint(int threads, ulong seed, int years)
    {
        var sim = new Simulation(new SimConfig { RequireModel = false, Seed = seed, Threads = threads });
        sim.RunYears(years);
        var people = string.Join(";", sim.Living.Values.OrderBy(a => a.Id)
            .Select(a => $"{a.Id}:{a.BandId}:{a.Health:0.0000}:{a.Food:0.0000}:{string.Join(",", a.TraitOffset.Select(t => t.ToString("0.0000")))}"));
        var events = string.Join("|", sim.Chronicle.Events.Select(e => $"{e.Day}:{e.Kind}:{e.AgentId}"));
        var years_ = string.Join("|", sim.Stats.History.Select(r => $"{r.Population},{r.Births},{r.Deaths.Sum()},{r.Quarrels},{r.Confrontations},{r.Inventions}"));
        return $"{people}#{events}#{years_}#{sim.Memes.Count}#{sim.Bands.Count}";
    }

    [Theory]
    [InlineData(23UL)]
    [InlineData(42UL)]
    public void History_is_identical_for_any_thread_count(ulong seed)
    {
        string one = Fingerprint(1, seed, 60);
        Assert.Equal(one, Fingerprint(4, seed, 60));
        Assert.Equal(one, Fingerprint(16, seed, 60));
    }
}
