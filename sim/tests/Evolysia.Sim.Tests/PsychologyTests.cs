using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Evolysia.Sim.Psych;

namespace Evolysia.Sim.Tests;

public class PsychologyTests
{
    static Agent Person(float ns = 0.5f, float ha = 0.5f, float rd = 0.5f, float ps = 0.5f, float sd = 0.5f, float co = 0.5f, float st = 0.5f,
                        float ageYears = 30f, int day = 100 * SimConfig.DaysPerYear)
    {
        var g = new Genome();
        Array.Fill(g.Genes, 0.5f);
        float[] t = { ns, ha, rd, ps, sd, co, st };
        for (int i = 0; i < t.Length; i++) g.Genes[i] = t[i];
        return new Agent { Genome = g, BirthDay = day - (int)(ageYears * SimConfig.DaysPerYear) };
    }

    const int Day = 100 * SimConfig.DaysPerYear;

    [Fact]
    public void Scare_raises_fear_and_generalises_more_for_the_anxious()
    {
        var calm = Person(ha: 0.1f);
        var anxious = Person(ha: 0.9f);
        Fear.Scare(calm, Stimulus.Wolf, 0.8f, Day);
        Fear.Scare(anxious, Stimulus.Wolf, 0.8f, Day);

        Assert.True(anxious.Fear[(int)Stimulus.Wolf] > calm.Fear[(int)Stimulus.Wolf]);
        Assert.True(anxious.CategoryFear[(int)StimulusCategory.Beasts] > calm.CategoryFear[(int)StimulusCategory.Beasts] * 2f);
        Assert.True(anxious.EffectiveFear(Stimulus.Bear) > 0.2f, "fear of wolves should spill over to other beasts");
    }

    [Fact]
    public void Avoiding_keeps_fear_overcoming_reduces_it_and_leaves_thrill()
    {
        var avoider = Person(ha: 0.7f);
        var confronter = Person(ha: 0.7f);
        foreach (var a in new[] { avoider, confronter }) Fear.Scare(a, Stimulus.Bear, 0.9f, Day);
        float before = confronter.EffectiveFear(Stimulus.Bear);
        float nsBefore = confronter.T(Trait.NoveltySeeking);
        float effBefore = confronter.Efficacy[(int)Domain.Hunting];

        Fear.Avoided(avoider, witnessed: false, Day);
        bool triumph = Fear.Overcame(confronter, Stimulus.Bear, Domain.Hunting, Day);

        Assert.Equal(before, avoider.EffectiveFear(Stimulus.Bear), 3);
        Assert.True(triumph);
        Assert.True(confronter.EffectiveFear(Stimulus.Bear) < before * 0.6f);
        Assert.True(confronter.Mastered[(int)Stimulus.Bear] > 0f);
        Assert.True(confronter.T(Trait.NoveltySeeking) > nsBefore, "overcoming fear adds a taste for thrill");
        Assert.True(confronter.Efficacy[(int)Domain.Hunting] > effBefore);

        var naive = Person(ha: 0.7f);
        Fear.Scare(naive, Stimulus.Bear, 0.9f, Day);
        Fear.Scare(confronter, Stimulus.Bear, 0.9f, Day);
        Assert.True(confronter.Fear[(int)Stimulus.Bear] < naive.Fear[(int)Stimulus.Bear]);
    }

    [Fact]
    public void Defeat_sensitises()
    {
        var a = Person(ha: 0.5f);
        Fear.Scare(a, Stimulus.BigCat, 0.4f, Day);
        float before = a.Fear[(int)Stimulus.BigCat];
        Fear.Defeated(a, Stimulus.BigCat, Domain.Hunting, witnessed: true, Day);
        Assert.True(a.Fear[(int)Stimulus.BigCat] > before);
    }

    [Fact]
    public void Brave_person_with_allies_confronts_more_than_fearful_loner()
    {
        var rng = new Rng(7);
        var brave = Person(ns: 0.8f, ha: 0.1f, ps: 0.8f);
        var fearful = Person(ns: 0.2f, ha: 0.9f, ps: 0.2f);
        Fear.Scare(fearful, Stimulus.Wolf, 1f, Day);
        int braveYes = 0, fearfulYes = 0;
        for (int i = 0; i < 2000; i++)
        {
            if (Fear.Decide(brave, Stimulus.Wolf, 0.2f, 3, 0.5f, rng) == FearResponse.Confront) braveYes++;
            if (Fear.Decide(fearful, Stimulus.Wolf, 0.2f, 0, 0.3f, rng) == FearResponse.Confront) fearfulYes++;
        }
        Assert.True(braveYes > 1800, $"brave confronted {braveYes}/2000");
        Assert.True(fearfulYes < 200, $"fearful confronted {fearfulYes}/2000");
    }

    [Fact]
    public void Starvation_can_override_fear()
    {
        var rng = new Rng(3);
        var fearful = Person(ha: 0.8f);
        Fear.Scare(fearful, Stimulus.Wolf, 0.7f, Day);
        int calmDay = 0, starving = 0;
        for (int i = 0; i < 2000; i++)
        {
            if (Fear.Decide(fearful, Stimulus.Wolf, 0.0f, 1, 0.3f, rng) == FearResponse.Confront) calmDay++;
            if (Fear.Decide(fearful, Stimulus.Wolf, 1.2f, 1, 0.3f, rng) == FearResponse.Confront) starving++;
        }
        Assert.True(starving > calmDay * 3, $"starving {starving} vs fed {calmDay}");
    }

    [Theory]
    [InlineData(0.9f, 0.1f, 0.2f, PivotChoice.Conform)]
    [InlineData(0.1f, 0.9f, 0.9f, PivotChoice.DoubleDown)]
    public void Pivot_follows_personality(float rd, float sd, float ps, PivotChoice expected)
    {
        var rng = new Rng(11);
        var a = Person(rd: rd, sd: sd, ps: ps, ns: 0.3f, co: 0.6f);
        var ctx = new PivotContext(0.7f, 1, CanLeave: true, "invention");
        var counts = new int[5];
        for (int i = 0; i < 3000; i++) counts[(int)Pivot.Resolve(a, ctx, rng)]++;
        int top = Array.IndexOf(counts, counts.Max());
        Assert.Equal(expected, (PivotChoice)top);
        Assert.True(counts.Count(c => c > 0) >= 3, "choices are tendencies, not certainties");
    }

    [Fact]
    public void Leave_requires_allies_or_restlessness()
    {
        var homebody = Person(ns: 0.3f, sd: 0.9f, rd: 0.1f);
        var scores = Pivot.Scores(homebody, new PivotContext(0.8f, 0, CanLeave: true, "belief"));
        Assert.True(float.IsNegativeInfinity(scores[(int)PivotChoice.Leave]));
        var withAllies = Pivot.Scores(homebody, new PivotContext(0.8f, 3, CanLeave: true, "belief"));
        Assert.False(float.IsNegativeInfinity(withAllies[(int)PivotChoice.Leave]));
    }

    [Fact]
    public void Conforming_suppresses_the_domain_and_shifts_personality()
    {
        var a = Person(rd: 0.6f, sd: 0.4f, ageYears: 20f);
        float rd = a.T(Trait.RewardDependence), sd = a.T(Trait.SelfDirectedness);
        Pivot.ApplyInner(a, PivotChoice.Conform, Domain.Crafting, new PivotContext(1f, 0, false, "invention"), Day);
        Assert.True(a.IsSuppressed(Domain.Crafting));
        Assert.True(a.T(Trait.RewardDependence) > rd);
        Assert.True(a.T(Trait.SelfDirectedness) < sd);
    }

    [Fact]
    public void Experience_shapes_the_young_more_than_the_old()
    {
        var child = Person(ageYears: 8f);
        var elder = Person(ageYears: 70f);
        child.Shift(Trait.HarmAvoidance, 0.1f, Day);
        elder.Shift(Trait.HarmAvoidance, 0.1f, Day);
        Assert.True(child.TraitOffset[(int)Trait.HarmAvoidance] > elder.TraitOffset[(int)Trait.HarmAvoidance] * 3f);
    }

    [Fact]
    public void An_ordinary_life_does_not_push_personality_to_extremes()
    {
        var a = Person(ageYears: 1f);
        a.Belonging = 0.6f;
        for (int d = 0; d < 60 * SimConfig.DaysPerYear; d++)
        {
            if (d % 30 == 0) { a.Feel(Emotion.Joy, 0.2f); a.Feel(Emotion.Love, 0.1f); }
            if (d % 90 == 0) a.Feel(Emotion.Fear, 0.2f);
            Psychology.DailyUpdate(a, Day - SimConfig.DaysPerYear + d);
        }
        foreach (var t in Enum.GetValues<Trait>())
            Assert.InRange(a.T(t), 0.25f, 0.75f);
    }
}
