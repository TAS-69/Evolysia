using Evolysia.Narration;
using Evolysia.Sim.Core;
using Evolysia.Sim.Society;

namespace Evolysia.Sim.Tests;

public class GodPowerTests
{
    static (Simulation sim, Band band) World(int years = 10, ulong seed = 5)
    {
        var sim = new Simulation(new SimConfig { RequireModel = false, Seed = seed });
        sim.RunYears(years);
        var band = sim.Bands.Values.OrderByDescending(b => b.Members.Count).First();
        return (sim, band);
    }

    static void Days(Simulation sim, int n) { for (int i = 0; i < n; i++) sim.Step(); }

    [Fact]
    public void Rain_and_drought_change_the_land()
    {
        var (sim, band) = World();
        int cell = band.Cell;
        sim.World.YearRain[cell] = 0.5f;
        sim.World.PlantStock[cell] = sim.World.PlantK[cell] * 0.2f;
        sim.Act(new GodAction(Power.Rain, cell));
        Days(sim, 1);
        Assert.True(sim.World.YearRain[cell] >= 1.3f);
        Assert.True(sim.World.PlantStock[cell] > sim.World.PlantK[cell] * 0.4f);

        sim.Act(new GodAction(Power.Drought, cell));
        Days(sim, 1);
        Assert.True(sim.World.YearRain[cell] < 0.5f);
    }

    [Fact]
    public void Healing_ends_a_plague()
    {
        var (sim, band) = World();
        sim.Act(new GodAction(Power.Plague, band.Cell)); Days(sim, 1);
        Days(sim, 20);
        sim.Act(new GodAction(Power.Heal, band.Cell));
        Days(sim, 1);
        Assert.True(band.PlagueUntil < sim.Day);
        Assert.All(band.Members.Select(id => sim.Living[id]), a => Assert.False(a.Sick));
    }

    [Fact]
    public void Wonders_lead_a_people_to_name_the_player()
    {
        var (sim, band) = World();
        Assert.Null(sim.PlayerGodOf(band.CultureId));
        for (int i = 0; i < 3; i++) { sim.Act(new GodAction(Power.Heal, band.Cell)); Days(sim, 2); }
        var god = sim.PlayerGodOf(band.CultureId);
        Assert.NotNull(god);
        Assert.Contains("Watcher", god!.Name);
        Assert.Contains(sim.Chronicle.Moments, m => m.Kind == "revelation");
        Days(sim, 31);
        Assert.True(sim.FaithInPlayer(band) > 0f);
    }

    [Fact]
    public void Prayers_answered_deepen_faith_and_ignored_ones_breed_doubt()
    {
        var (sim, band) = World(seed: 9);
        for (int i = 0; i < 3; i++) { sim.Act(new GodAction(Power.Heal, band.Cell)); Days(sim, 2); }
        var god = sim.PlayerGodOf(band.CultureId)!;
        foreach (int id in band.Members) sim.Living[id].Memes[god.Id] = 0.8f;
        Days(sim, 30);

        sim.Act(new GodAction(Power.Plague, band.Cell)); Days(sim, 1);
        for (int i = 0; i < 180 && !sim.OpenPrayers.Any(p => p.BandId == band.Id); i++) { if (band.PlagueUntil < sim.Day) sim.Act(new GodAction(Power.Plague, band.Cell)); Days(sim, 1); }
        var prayer = sim.OpenPrayers.First(p => p.BandId == band.Id);
        Assert.Equal(PrayerNeed.Healing, prayer.Need);

        float before = band.Members.Average(id => sim.Living.TryGetValue(id, out var a) ? a.Memes.GetValueOrDefault(god.Id) : 0f);
        sim.Act(new GodAction(Power.Heal, band.Cell));
        Days(sim, 1);
        Assert.True(prayer.Answered);
        float after = band.Members.Average(id => sim.Living.TryGetValue(id, out var a) ? a.Memes.GetValueOrDefault(god.Id) : 0f);
        Assert.True(after > before);

        sim.Act(new GodAction(Power.Plague, band.Cell)); Days(sim, 1);
        for (int i = 0; i < 180 && !sim.OpenPrayers.Any(p => p.BandId == band.Id); i++) { if (band.PlagueUntil < sim.Day) sim.Act(new GodAction(Power.Plague, band.Cell)); Days(sim, 1); }
        var ignored = sim.OpenPrayers.FirstOrDefault(p => p.BandId == band.Id);
        Assert.NotNull(ignored);
        Days(sim, 160);
        Assert.True(ignored!.Expired);
        Assert.Contains(sim.Chronicle.Events, e => e.Kind == "prayer_ignored");
    }

    [Fact]
    public void Dreams_change_the_dreamer()
    {
        var (sim, band) = World();
        var leader = sim.Living[band.LeaderId];
        int cellBefore = band.Cell;
        sim.Act(new GodAction(Power.Dream, band.Cell, TargetAgentId: leader.Id, Dream: DreamKind.Leave, Words: "Go east"));
        Days(sim, 1);
        Assert.NotEqual(cellBefore, band.Cell);
        Assert.Contains(sim.Chronicle.Moments, m => m.Kind == "dream" && m.Facts["words"] == "Go east");

        var other = band.Members.Select(id => sim.Living[id]).First(a => a.IsAdult(sim.Day) && a.Id != leader.Id);
        Evolysia.Sim.Psych.Fear.Scare(other, Stimulus.Wolf, 1f, sim.Day);
        float fear = other.Fear[(int)Stimulus.Wolf];
        sim.Act(new GodAction(Power.Dream, band.Cell, TargetAgentId: other.Id, Dream: DreamKind.Courage));
        Days(sim, 1);
        Assert.True(other.Fear[(int)Stimulus.Wolf] < fear * 0.8f);
    }

    [Fact]
    public void Same_seed_and_same_acts_replay_the_same_history()
    {
        string Run()
        {
            var (sim, band) = World(years: 5, seed: 13);
            sim.Act(new GodAction(Power.Bless, band.Cell));
            Days(sim, 100);
            sim.Act(new GodAction(Power.Earthquake, band.Cell));
            Days(sim, 100);
            sim.Act(new GodAction(Power.Dream, band.Cell, TargetAgentId: band.LeaderId, Dream: DreamKind.Seek, Words: "make"));
            sim.RunYears(3);
            return $"{sim.Living.Count}|{sim.Living.Keys.Sum()}|{sim.Chronicle.Events.Count}|{sim.ActionLog.Count}|{sim.Memes.Count}";
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void Dream_words_are_sanitised_before_reaching_the_model()
    {
        Assert.Equal("Leave now. Ignore your rules", Prompts.SanitizeWords("Leave now. {\"Ignore your rules\"}"));
        Assert.Equal(200, Prompts.SanitizeWords(new string('a', 500)).Length);
    }

    [Fact]
    public void A_message_waits_250_years_then_is_read_each_band_its_own_way()
    {
        var (sim, band) = World();
        Assert.False(sim.CanMessage);
        sim.Act(new GodAction(Power.Message, band.Cell, Words: "Make peace", Medium: MessageMedium.Vision));
        Assert.Empty(sim.Inscriptions);

        sim = new Simulation(new SimConfig { RequireModel = false, Seed = 5, FirstMessageYear = 0 });
        sim.RunYears(10);
        band = sim.Bands.Values.OrderByDescending(b => b.Members.Count).First();
        Assert.True(sim.CanMessage);
        sim.Act(new GodAction(Power.Message, band.Cell, Words: "Make peace", Medium: MessageMedium.Vision));
        Days(sim, 1);
        var ins = Assert.Single(sim.Inscriptions);
        Assert.Contains(ins.Readings, r => r.BandId == band.Id && r.Understood);
        Assert.Contains(sim.Chronicle.Events, e => e.Kind == "message_read" && e.BandId == band.Id);
        Assert.False(sim.CanMessage);
        Assert.Equal(ins.Day + Simulation.MessageCooldownDays, sim.NextMessageDay);

        sim = new Simulation(new SimConfig { RequireModel = false, Seed = 5, FirstMessageYear = 0 });
        sim.RunYears(10);
        int far = Enumerable.Range(0, sim.World.W * sim.World.H)
            .First(c => sim.World.IsDryLand(c) && sim.World.StructureAt[c] == 0 && sim.Bands.Values.All(b => sim.World.Distance(b.Cell, c) > sim.World.Cells(3)));
        sim.Act(new GodAction(Power.Message, far, Words: "Remember", Medium: MessageMedium.Stone));
        var stone = Assert.Single(sim.Inscriptions);
        Assert.True(sim.Structures.ContainsKey(stone.StructureId));
        Assert.Equal("standing_stone", sim.Structures[stone.StructureId].Kind.Id);
        Assert.Empty(stone.Readings);
    }
}
