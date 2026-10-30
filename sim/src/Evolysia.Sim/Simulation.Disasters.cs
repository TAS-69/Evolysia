using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Evolysia.Sim.Psych;
using Evolysia.Sim.Society;
using Evolysia.Sim.World;

namespace Evolysia.Sim;

public sealed partial class Simulation
{
    int[]? _volcanoes;

    void WorldDisasters()
    {
        var w = World;
        float rate = Config.DisasterRate;
        _volcanoes ??= Enumerable.Range(0, w.Count).Where(i => w.Volcano[i]).ToArray();
        foreach (int v in _volcanoes)
            if (Rng.Chance(0.000006f * rate)) Eruption(v);

        foreach (var band in Bands.Values.ToArray())
        {
            if (band.Disbanded || band.Members.Count == 0) continue;
            int c = band.Cell;
            float t = w.Temperature(c, Day), wet = w.Wetness(c, Day);
            var season = w.SeasonOf(c, Day);
            var biome = w.Biome[c];

            bool flammable = biome is Biome.BorealForest or Biome.TemperateForest or Biome.Savanna or Biome.Shrubland
                or Biome.TemperateGrassland or Biome.TropicalSeasonalForest;
            if (flammable && t > 22f && wet < 0.7f && Rng.Chance(0.0007f * rate * (1.2f - wet)))
            {
                w.PlantStock[c] *= 0.25f; w.PreyStock[c] *= 0.6f; w.Ash[c] = MathF.Min(1f, w.Ash[c] + 0.25f);
                band.Shelter *= 0.5f; band.FoodPool *= 0.7f;
                Strike(band, "wildfire", Stimulus.Wildfire, 0.75f, 0.012f, DeathCause.Disaster);
            }
            if (w.Flow[c] > WorldGen.RiverFlow * 2f && wet > 1.35f && Rng.Chance(0.0015f * rate))
            {
                band.FoodPool *= 0.5f; band.Shelter *= 0.4f;
                Strike(band, "flood", Stimulus.Flood, 0.65f, 0.01f, DeathCause.Accident);
            }
            if (season == Season.Winter && t < -12f && Rng.Chance(0.01f * rate))
            {
                foreach (int id in band.Members) if (Living.TryGetValue(id, out var m)) m.Health -= 0.15f * (1f - band.Shelter * 0.7f);
                Strike(band, "blizzard", Stimulus.Blizzard, 0.45f, 0.004f, DeathCause.Cold);
            }
            if (season is Season.Summer or Season.Wet && t > 18f && Rng.Chance(0.00008f * rate))
                Strike(band, "lightning strike", Stimulus.Lightning, 0.8f, 0f, DeathCause.Disaster, lightning: true);
            if (w.Fault[c] > 0.55f && Rng.Chance(0.00006f * rate * w.Fault[c]))
            {
                band.Shelter *= 0.3f;
                Strike(band, "earthquake", Stimulus.Earthquake, 0.85f, 0.02f, DeathCause.Disaster);
            }
            if (biome is Biome.Savanna or Biome.TemperateGrassland && season is Season.Summer or Season.Dry && Rng.Chance(0.0002f * rate))
            {
                w.PlantStock[c] *= 0.3f;
                Chronicle.Add(new WorldEvent(Day, EventCategory.Loss, "locusts", $"Locusts strip the land around the {band.Name}.", 0.35f, -1, band.Id));
                MaybeMyth(band, StimulusCategory.Sky, 0.5f, "locust swarm");
            }
            Plague(band);
        }
    }

    void Strike(Band band, string kind, Stimulus s, float intensity, float killChance, DeathCause cause, bool lightning = false)
    {
        Stats.Year.Disasters++;
        int deaths = 0;
        var members = band.Members.ToArray();
        Agent? struck = lightning && members.Length > 0 && Living.TryGetValue(members[Rng.Next(members.Length)], out var victim) ? victim : null;
        foreach (int id in members)
        {
            if (!Living.TryGetValue(id, out var m) || !m.Alive) continue;
            float risk = killChance * (m.Genome.Has(Quirk.Lucky) ? 0.7f : 1f) * (m.IsChild(Day) || m.Age(Day) > 60f ? 1.4f : 1f);
            if (m == struck) risk = 0.6f;
            if (Rng.Chance(risk)) { Kill(m, cause, s, $" in the {kind}"); deaths++; continue; }
            Fear.Scare(m, s, intensity, Day);
            Psychology.Appraise(m, new Appraisal(EventCategory.Danger, kind, -1f, intensity, Agency.Unknown, Threat: true, Detail: kind), Day);
        }
        band.PlaceDread[(int)Ids.CategoryOf(s)] += intensity * 0.5f + deaths * 0.2f;
        Chronicle.Add(new WorldEvent(Day, EventCategory.Danger, kind.Replace(' ', '_'),
            $"A {kind} strikes the {band.Name}{(deaths > 0 ? $"; {deaths} die" : "")}.", 0.3f + 0.1f * deaths + (lightning && deaths > 0 ? 0.2f : 0f), -1, band.Id));
        MaybeMyth(band, Ids.CategoryOf(s), MathF.Min(1f, intensity * 0.6f + deaths * 0.15f), kind);
    }

    void Eruption(int volcano)
    {
        var w = World;
        bool big = Rng.Chance(0.12f);
        int radius = big ? 5 : 3;
        for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                int y = w.Y(volcano) + dy;
                if (y < 0 || y >= w.H) continue;
                int c = w.Index(w.X(volcano) + dx, y);
                int d = w.Distance(volcano, c);
                w.Ash[c] = MathF.Min(1f, w.Ash[c] + (big ? 0.9f : 0.6f) / (1 + d));
                w.PlantStock[c] *= 0.3f + 0.1f * d;
            }
        if (big) _volcanicCooling += 1.4f;
        Stats.Year.Disasters++;
        Chronicle.Add(new WorldEvent(Day, EventCategory.Danger, "eruption",
            $"The mountain at {w.X(volcano)},{w.Y(volcano)} erupts{(big ? ", darkening the skies of the whole world" : "")}.", big ? 0.85f : 0.5f));

        foreach (var band in Bands.Values.ToArray())
        {
            int d = w.Distance(volcano, band.Cell);
            if (band.Disbanded || d > radius + 2) continue;
            if (d <= 1) Strike(band, "eruption", Stimulus.Eruption, 1f, d == 0 ? 0.35f : 0.1f, DeathCause.Disaster);
            else
                foreach (int id in band.Members)
                    if (Living.TryGetValue(id, out var m)) Fear.Scare(m, Stimulus.Eruption, 0.5f, Day);
            if (d > 1) MaybeMyth(band, StimulusCategory.Earth, 0.7f, "fire mountain");
        }
    }

    void Plague(Band band)
    {
        if (band.PlagueUntil < Day)
        {
            float p = 0.00002f * Config.DisasterRate * (band.Members.Count / 25f)
                      * (BandKnows(band, TechId.Domestication) ? 1.8f : 1f) * (band.Sedentary ? 1.5f : 1f);
            if (!Rng.Chance(p)) return;
            StartPlague(band);
        }
        foreach (int id in band.Members)
        {
            if (!Living.TryGetValue(id, out var m) || m.Sick) continue;
            if (Rng.Chance(0.03f * band.PlagueSeverity))
            {
                m.Sick = true;
                m.SickUntil = Day + Rng.Range(12, 26);
                Fear.Scare(m, Stimulus.Plague, 0.3f, Day);
            }
        }
        if (Day == band.PlagueUntil) MaybeMyth(band, StimulusCategory.Sickness, 0.6f + band.PlagueSeverity * 0.4f, "sickness");
    }

    internal void StartPlague(Band band)
    {
        band.PlagueSeverity = Rng.Range(0.3f, 0.9f);
        band.PlagueUntil = Day + Rng.Range(40, 100);
        Stats.Year.Plagues++;
        Chronicle.Add(new WorldEvent(Day, EventCategory.Danger, "plague",
            $"A wasting sickness spreads among the {band.Name}.", 0.35f + band.PlagueSeverity * 0.3f, -1, band.Id));
        band.PlaceDread[(int)StimulusCategory.Sickness] += 0.3f;
    }
}
