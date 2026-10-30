using Evolysia.Sim.Core;

namespace Evolysia.Sim.Society;

/// <summary>
/// Each culture gets its own small sound palette; names, bands and gods are built from it.
/// Daughter cultures inherit the palette with a couple of sounds changed, so related peoples
/// sound related.
/// </summary>
public static class Names
{
    static readonly string[] Onsets = { "k", "t", "m", "n", "r", "s", "l", "v", "d", "g", "b", "h", "sh", "th", "z", "y", "w", "kh", "tz", "q", "p", "f" };
    static readonly string[] Vowels = { "a", "e", "i", "o", "u", "a", "o", "ai", "ei", "ou" };
    static readonly string[] Codas = { "", "", "", "n", "r", "l", "k", "s", "m" };

    public static string[] NewPhonology(Rng rng)
    {
        int n = 7 + rng.Next(4);
        var set = new HashSet<string>();
        // A culture favours a subset of consonants, which gives it a recognisable "accent".
        var onsets = Enumerable.Range(0, 7).Select(_ => rng.Pick(Onsets)).Distinct().ToArray();
        while (set.Count < n) set.Add(rng.Pick(onsets) + rng.Pick(Vowels) + rng.Pick(Codas));
        return set.ToArray();
    }

    public static string[] Mutate(string[] syllables, Rng rng)
    {
        var s = (string[])syllables.Clone();
        for (int k = 0; k < 2; k++) s[rng.Next(s.Length)] = rng.Pick(Onsets) + rng.Pick(Vowels) + rng.Pick(Codas);
        return s.Distinct().ToArray();
    }

    public static string Make(string[] syllables, Rng rng, int min = 2, int max = 3)
    {
        int count = rng.Range(min, max + 1);
        var sb = new System.Text.StringBuilder();
        for (int k = 0; k < count; k++) sb.Append(rng.Pick(syllables));
        var name = sb.ToString();
        if (name.Length > 10) name = name[..10];
        return char.ToUpperInvariant(name[0]) + name[1..];
    }

    static readonly Dictionary<StimulusCategory, string[]> Epithets = new()
    {
        [StimulusCategory.Beasts] = new[] { "the Great Hunter", "the Wolf Mother", "Who Walks With Claws" },
        [StimulusCategory.Water] = new[] { "the River Mother", "the Deep One", "Who Drinks the Drowned" },
        [StimulusCategory.Fire] = new[] { "the Burning One", "Ember Father", "the Red Hunger" },
        [StimulusCategory.Sky] = new[] { "the Thunder Father", "Storm Eye", "the Cold Wind" },
        [StimulusCategory.Earth] = new[] { "the Shaker Below", "Stone Heart", "the Sleeping Mountain" },
        [StimulusCategory.Outsiders] = new[] { "the Watcher at the Edge", "the Stranger God" },
        [StimulusCategory.Sickness] = new[] { "the Pale Walker", "the Fever Mother", "Who Takes the Breath" },
    };

    public static string Deity(string[] syllables, StimulusCategory c, Rng rng) =>
        $"{Make(syllables, rng, 2, 2)} {rng.Pick(Epithets[c])}";
}
