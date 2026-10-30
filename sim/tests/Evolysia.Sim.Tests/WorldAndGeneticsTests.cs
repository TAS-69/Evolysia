using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Evolysia.Sim.World;

namespace Evolysia.Sim.Tests;

public class WorldAndGeneticsTests
{
    [Fact]
    public void Rng_is_deterministic_and_streams_differ()
    {
        var a = new Rng(42); var b = new Rng(42); var c = Rng.Derive(42, 1);
        for (int i = 0; i < 100; i++) Assert.Equal(a.NextUInt(), b.NextUInt());
        Assert.NotEqual(new Rng(42).NextUInt(), c.NextUInt());
    }

    [Fact]
    public void Beta22_is_centred_and_bounded()
    {
        var rng = new Rng(5);
        var v = Enumerable.Range(0, 20000).Select(_ => rng.Beta22()).ToArray();
        Assert.InRange(v.Average(), 0.48, 0.52);
        Assert.All(v, x => Assert.InRange(x, 0f, 1f));
    }

    [Fact]
    public void Children_blend_parents_with_one_to_three_divergences()
    {
        var rng = new Rng(9);
        var mom = new Genome(); var dad = new Genome();
        Array.Fill(mom.Genes, 0.2f); Array.Fill(dad.Genes, 0.8f);
        mom.Quirks = Quirk.Hardy;
        int hardyKids = 0;
        var firstGene = new List<float>();
        for (int k = 0; k < 400; k++)
        {
            var child = Genome.Inherit(mom, dad, rng, out var div);
            Assert.InRange(div.Count, 1, 3);
            firstGene.Add(child.Genes[0]);
            if (child.Has(Quirk.Hardy)) hardyKids++;
        }
        Assert.InRange(firstGene.Average(), 0.46f, 0.54f);   // centred on the parents' midpoint
        Assert.InRange(hardyKids, 150, 260);                 // ~50% inherit a parent's quirk (plus rare new ones)
    }

    [Fact]
    public void Random_mating_keeps_personality_variety_over_many_generations()
    {
        var rng = new Rng(4);
        var pop = Enumerable.Range(0, 400).Select(_ => Genome.Random(rng)).ToList();
        float Sd(List<Genome> p, int gene) { var v = p.Select(g => g.Genes[gene]).ToArray(); float m = v.Average(); return MathF.Sqrt(v.Average(x => (x - m) * (x - m))); }
        for (int gen = 0; gen < 20; gen++)
            pop = Enumerable.Range(0, 400).Select(i => Genome.Inherit(rng.Pick(pop), rng.Pick(pop), rng, out _)).ToList();
        for (int t = 0; t < Ids.TraitCount; t++)
            Assert.True(Sd(pop, t) > 0.18f, $"trait {(Trait)t} sd collapsed to {Sd(pop, t):0.00}");
    }

    [Fact]
    public void World_has_land_climate_gradient_and_variety()
    {
        var w = WorldGen.Generate(new SimConfig { Seed = 3 });
        int land = Enumerable.Range(0, w.Count).Count(w.IsLand);
        Assert.InRange(land / (float)w.Count, 0.28f, 0.40f);

        float equator = Enumerable.Range(0, w.W).Average(x => w.TempMean[w.Index(x, w.H / 2)]);
        float polar = Enumerable.Range(0, w.W).Average(x => w.TempMean[w.Index(x, 2)]);
        Assert.True(equator > polar + 20f);

        var biomes = Enumerable.Range(0, w.Count).Where(w.IsLand).Select(i => w.Biome[i]).Distinct().Count();
        Assert.True(biomes >= 10, $"only {biomes} land biomes");
        Assert.Contains(Enumerable.Range(0, w.Count), i => w.Flow[i] > WorldGen.RiverFlow);
    }

    [Fact]
    public void Same_seed_same_world()
    {
        var a = WorldGen.Generate(new SimConfig { Seed = 77 });
        var b = WorldGen.Generate(new SimConfig { Seed = 77 });
        Assert.Equal(a.Elevation, b.Elevation);
        Assert.Equal(a.Biome, b.Biome);
    }

    [Fact]
    public void Same_seed_same_history()
    {
        string Run()
        {
            var sim = new Simulation(new SimConfig { Seed = 21 });
            sim.RunYears(15);
            return string.Join("|", sim.Stats.History.Select(r => $"{r.Population},{r.Births},{r.Deaths.Sum()},{r.Memes},{r.Inventions}"))
                   + $"#{sim.Living.Keys.Sum()}#{sim.Chronicle.Events.Count}";
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void A_short_history_produces_life_events()
    {
        var sim = new Simulation(new SimConfig { Seed = 5 });
        sim.RunYears(30);
        var h = sim.Stats.History;
        Assert.Equal(30, h.Count);
        Assert.True(h.Sum(r => r.Births) > 10);
        Assert.True(sim.Living.Count > 0);
        Assert.Contains(sim.Living.Values, a => a.Memory.Count > 0);
        Assert.True(sim.Chronicle.Events.Count > 5);
    }
}
