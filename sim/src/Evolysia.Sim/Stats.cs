using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Evolysia.Sim.Psych;

namespace Evolysia.Sim;

public sealed class YearRecord
{
    public int Year;
    public int Population, Bands, Cultures, Memes, Children, Elders, Sedentary;
    public int Births, Pairings, Inventions, Myths, Taboos, Norms, Schisms, Splits, Migrations, NewCultures;
    public int Raids, Trades, FirstContacts, Quarrels, Assaults, Disasters, Plagues, Drownings;
    public int Confrontations, FearsOvercome, Praises, Creations;
    public int GodActs, Revelations, Dreams, PrayersMade, PrayersAnswered;
    public int MindChoices, InstinctChoices, MindAgreedWithInstinct;
    public int ShoresSighted, Voyages, LostAtSea, Landmasses;
    public readonly int[] Deaths = new int[Enum.GetValues<DeathCause>().Length];
    public readonly int[] Pivots = new int[Enum.GetValues<PivotChoice>().Length];
    public readonly float[] TraitMean = new float[Ids.TraitCount];
    public readonly float[] TraitSd = new float[Ids.TraitCount];
    public readonly float[] CategoryFearMean = new float[Ids.CategoryCount];
    public int FertileWomen, FertilePaired, Pregnant, SingleAdults;
    public float MeanAge, MeanBandSize, TempAnomaly, MeanHunger, TechsKnownWorld, TechsMeanPerBand, MeanGeneration;
}

public sealed class Stats
{
    public YearRecord Year = new();
    public readonly List<YearRecord> History = new();

    internal void CloseYear(Simulation sim)
    {
        var r = Year;
        r.Year = sim.Year - 1;
        var living = sim.Living.Values.Where(a => a.Alive).ToList();
        r.Population = living.Count;
        r.Bands = sim.Bands.Count;
        r.Cultures = sim.Bands.Values.Select(b => b.CultureId).Distinct().Count();
        r.Memes = sim.Memes.Count;
        r.Sedentary = sim.Bands.Values.Count(b => b.Sedentary);
        r.TempAnomaly = sim.World.GlobalTempAnomaly;
        if (living.Count > 0)
        {
            int day = sim.Day;
            r.Children = living.Count(a => a.IsChild(day));
            r.Elders = living.Count(a => a.Age(day) >= 60f);
            r.MeanAge = living.Average(a => a.Age(day));
            r.MeanHunger = living.Average(a => a.NeedOf(Need.Hunger));
            r.MeanGeneration = (float)living.Average(a => a.Generation);
            r.MeanBandSize = (float)sim.Bands.Values.Average(b => b.Members.Count);
            var fertile = living.Where(a => a.Sex == Sex.Female && a.Age(day) is >= 16f and <= 45f).ToList();
            r.FertileWomen = fertile.Count;
            r.FertilePaired = fertile.Count(a => a.PartnerId >= 0 && sim.Living.TryGetValue(a.PartnerId, out var p) && p.BandId == a.BandId);
            r.Pregnant = fertile.Count(a => a.Pregnant);
            r.SingleAdults = living.Count(a => a.IsAdult(day) && a.PartnerId < 0 && a.Age(day) < 50f);
            var adults = living.Where(a => a.IsAdult(day)).ToList();
            if (adults.Count > 0)
            {
                for (int t = 0; t < Ids.TraitCount; t++)
                {
                    var values = adults.Select(a => a.T((Trait)t)).ToArray();
                    float mean = values.Average();
                    r.TraitMean[t] = mean;
                    r.TraitSd[t] = MathF.Sqrt(values.Average(v => (v - mean) * (v - mean)));
                }
                for (int c = 0; c < Ids.CategoryCount; c++)
                    r.CategoryFearMean[c] = adults.Average(a => a.CategoryFear[c]);
            }
            TechSet world = default;
            float perBand = 0f;
            foreach (var b in sim.Bands.Values)
            {
                TechSet k = default;
                foreach (int id in b.Members) if (sim.Living.TryGetValue(id, out var a)) k |= a.Knows;
                world |= k;
                perBand += Techs.CountKnown(k);
            }
            r.TechsKnownWorld = Techs.CountKnown(world);
            r.TechsMeanPerBand = sim.Bands.Count == 0 ? 0f : perBand / sim.Bands.Count;
        }
        History.Add(r);
        Year = new YearRecord();
    }
}
