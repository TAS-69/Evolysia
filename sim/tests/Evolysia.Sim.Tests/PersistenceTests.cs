using Evolysia.Sim.Core;
using Evolysia.Sim.Persistence;

namespace Evolysia.Sim.Tests;

public class PersistenceTests
{
    static string Fingerprint(Simulation sim) => $"{sim.Day}|{sim.EverLived}|{sim.Structures.Count}|{sim.Memes.Count}|" +
        string.Join(";", sim.Living.Values.Select(a => $"{a.Id}:{a.BandId}:{a.Health:R}:{a.Food:R}:{a.Prestige:R}:{string.Join(',', a.Needs)}")) +
        string.Join(";", sim.Chronicle.Events.Select(e => $"{e.Day}:{e.Kind}:{e.AgentId}:{e.BandId}"));

    [Fact]
    public void Saves_preserve_rng_ecology_inventory_and_future_history()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".evo");
        try
        {
            var sim = new Simulation(new SimConfig { RequireModel = false, Width = 160, Height = 80, Threads = 1 });
            sim.RunYears(4);
            WorldSave.Save(sim, path);
            var loaded = WorldSave.Load(path);
            Assert.Equal(Fingerprint(sim), Fingerprint(loaded));
            sim.RunYears(2); loaded.RunYears(2);
            Assert.Equal(Fingerprint(sim), Fingerprint(loaded));
            Assert.Equal(sim.World.PlantStock, loaded.World.PlantStock);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Model_plans_and_personal_memories_survive_reload()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".evo");
        try
        {
            var sim = CognitionTests.World(); var r = Assert.Single(sim.MindRequests()); sim.AcceptPlan(CognitionTests.Plan(r), out _); sim.Step();
            WorldSave.Save(sim, path); var loaded = WorldSave.Load(path);
            Assert.True(loaded.Config.RequireModel);
            Assert.Equal(sim.BandPlans[r.BandId].Plan.Goal, loaded.BandPlans[r.BandId].Plan.Goal);
            Assert.Equal(sim.Minds[r.Agents[0]].Memories, loaded.Minds[r.Agents[0]].Memories);
            Assert.False(loaded.WaitingForMinds);
            sim.Step(); loaded.Step(); Assert.Equal(Fingerprint(sim), Fingerprint(loaded));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Corrupt_saves_are_rejected_without_replacing_existing_world()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".evo");
        try
        {
            var sim = CognitionTests.World(); WorldSave.Save(sim, path);
            var bytes = File.ReadAllBytes(path); bytes[35] ^= 42; File.WriteAllBytes(path, bytes);
            Assert.Throws<InvalidDataException>(() => WorldSave.Load(path));
            Assert.Equal(0, sim.Day);
        }
        finally { File.Delete(path); }
    }
}
