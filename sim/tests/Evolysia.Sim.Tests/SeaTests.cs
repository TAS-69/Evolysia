using Evolysia.Sim.Agents;
using Evolysia.Sim.World;

namespace Evolysia.Sim.Tests;

public class SeaTests
{
    [Fact]
    public void Land_is_divided_into_landmasses()
    {
        var w = WorldGen.Generate(new SimConfig { RequireModel = false, Seed = 3 });
        Assert.True(w.LandmassSize.Length >= 3, $"only {w.LandmassSize.Length} landmasses");
        for (int i = 0; i < w.Count; i++)
            Assert.Equal(w.IsDryLand(i), w.Landmass[i] >= 0);
        Assert.Equal(Enumerable.Range(0, w.Count).Count(w.IsDryLand), w.LandmassSize.Sum());
    }

    static (int from, int to, int distance) FindCrossing(Simulation sim, int range)
    {
        var w = sim.World;
        for (int i = 0; i < w.Count; i++)
        {
            if (!w.IsDryLand(i) || w.PlantK[i] <= 0f || !sim.IsCoastal(i)) continue;
            foreach (var (cell, d) in sim.ShoresWithin(i, range).OrderBy(kv => kv.Key))
                if (w.Landmass[cell] != w.Landmass[i] && w.PlantK[cell] > 0f) return (i, cell, d);
        }
        throw new InvalidOperationException("no crossing in this world");
    }

    [Fact]
    public void Sea_reach_respects_range_and_only_crosses_water()
    {
        var sim = new Simulation(new SimConfig { RequireModel = false, Seed = 3 });
        var (from, to, d) = FindCrossing(sim, 6);
        Assert.InRange(d, 1, 6);
        Assert.DoesNotContain(sim.ShoresWithin(from, d - 1), kv => kv.Key == to && kv.Value >= d);
        Assert.Empty(sim.ShoresWithin(from, 0));
    }

    [Fact]
    public void Pioneers_can_sail_to_a_new_land()
    {
        var sim = new Simulation(new SimConfig { RequireModel = false, Seed = 3 });
        sim.RunYears(5);
        var band = sim.Bands.Values.OrderByDescending(b => b.Members.Count).First();
        var (from, to, d) = FindCrossing(sim, 6);
        band.Cell = from;
        band.KnownShores[to] = d;
        foreach (int id in band.Members) sim.Living[id].Knows |= Techs.Bit(TechId.Boats) | Techs.Bit(TechId.Fishing);
        var leader = sim.Living[band.LeaderId];
        var followers = band.Members.Select(id => sim.Living[id]).Where(a => a != leader && a.IsAdult(sim.Day)).Take(6).ToList();

        var arrived = sim.Sail(band, leader, followers, to);
        var voyage = sim.Voyages.Last();
        Assert.Equal(to, voyage.ToCell);
        Assert.Equal(d, voyage.Distance);
        Assert.Contains(sim.Chronicle.Events, e => e.Kind is "voyage" or "lost_at_sea");
        if (arrived != null)
        {
            Assert.Equal(to, arrived.Cell);
            Assert.NotEqual(band.Id, arrived.Id);
            Assert.True(voyage.FirstLanding, "nobody had reached that land before");
            Assert.Contains(sim.Chronicle.Moments, m => m.Kind == "voyage");
        }
    }

    [Fact]
    public void Over_centuries_people_cross_the_sea()
    {
        var sim = new Simulation(new SimConfig { RequireModel = false, Seed = 7 });
        sim.RunYears(220);
        int settled = sim.Bands.Values.Select(b => sim.World.Landmass[b.Cell]).Distinct().Count();
        Assert.True(sim.Stats.History.Sum(r => r.Voyages) > 0, "an island people should take to the sea");
        Assert.True(settled >= 2, $"still on {settled} landmass");
    }
}
