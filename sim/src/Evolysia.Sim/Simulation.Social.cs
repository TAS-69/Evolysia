using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Evolysia.Sim.Psych;
using Evolysia.Sim.Society;
using Evolysia.Sim.World;

namespace Evolysia.Sim;

public sealed partial class Simulation
{
    // ------------------------------------------------------------------ memes

    // Beliefs change slowly, so what each person holds and how widespread each meme is in a band
    // are cached and refreshed monthly (RefreshMemeCaches) rather than recounted on every chat.

    /// <summary>Strongest conviction this agent holds in a meme of this kind (0 if none).</summary>
    internal float Held(Agent a, MemeKind kind) => a.HeldKind[(int)kind];

    internal float Taboo(Agent a, StimulusCategory target) => a.TabooOf[(int)target];

    internal float Prevalence(Band band, int memeId) => band.MemePrevalence.GetValueOrDefault(memeId);

    internal bool BandHolds(Band band, MemeKind kind, float minShare)
    {
        foreach (var (id, share) in band.MemePrevalence)
            if (share >= minShare && Memes[id].Kind == kind) return true;
        return false;
    }

    readonly HashSet<int> _heldMemes = new();

    void RefreshMemeCaches()
    {
        _heldMemes.Clear();
        foreach (var band in Bands.Values)
        {
            band.MemePrevalence.Clear();
            int adults = 0;
            foreach (int id in band.Members)
            {
                if (!Living.TryGetValue(id, out var a)) continue;
                FadeBeliefs(a);
                RefreshMind(a);
                foreach (var meme in a.Memes.Keys) _heldMemes.Add(meme);
                if (!a.IsAdult(Day)) continue;
                adults++;
                foreach (var (meme, conv) in a.Memes)
                    if (conv > 0.35f) band.MemePrevalence[meme] = band.MemePrevalence.GetValueOrDefault(meme) + 1f;
            }
            foreach (var id in band.MemePrevalence.Keys.ToArray()) band.MemePrevalence[id] /= Math.Max(1, adults);
        }
        PruneForgottenMemes();
    }

    /// <summary>
    /// A myth no living person remembers is gone. Faiths in the player are kept, so a people that
    /// forgot you can rediscover the same name.
    /// </summary>
    void PruneForgottenMemes()
    {
        var keep = new HashSet<int>(Memes.Values.Where(m => m.IsPlayerGod).SelectMany(m => new[] { m.Id, m.PairedMemeId }));
        foreach (var id in Memes.Keys.ToArray())
            if (!_heldMemes.Contains(id) && !keep.Contains(id)) Memes.Remove(id);
    }

    /// <summary>
    /// Monthly: beliefs fade unless renewed by ritual, retelling or events (~18% a year), so faiths
    /// can rise and die out rather than accumulating forever.
    /// </summary>
    void FadeBeliefs(Agent a)
    {
        if (a.Memes.Count == 0) return;
        foreach (var id in a.Memes.Keys.ToArray())
        {
            float c = a.Memes[id] - 0.015f;
            if (c < 0.05f) a.Memes.Remove(id);
            else a.Memes[id] = c;
        }
    }

    internal void RefreshMind(Agent a)
    {
        Array.Clear(a.HeldKind);
        Array.Clear(a.TabooOf);
        List<int>? stale = null;
        foreach (var (id, conv) in a.Memes)
        {
            if (!Memes.TryGetValue(id, out var m)) { (stale ??= new()).Add(id); continue; }
            a.HeldKind[(int)m.Kind] = MathF.Max(a.HeldKind[(int)m.Kind], conv);
            if (m.Kind == MemeKind.Taboo) a.TabooOf[(int)m.Target] = MathF.Max(a.TabooOf[(int)m.Target], conv);
        }
        if (stale != null) foreach (var id in stale) a.Memes.Remove(id);
    }

    /// <summary>
    /// Taboos and norms mean the same thing wherever they arise, so they are one shared idea that
    /// spreads and fades. Myths stay unique: two peoples' storm gods are different gods.
    /// </summary>
    internal Meme SharedMeme(MemeKind kind, StimulusCategory target, string name, Agent origin, Band band)
    {
        foreach (var m in Memes.Values)
            if (m.Kind == kind && (kind != MemeKind.Taboo || m.Target == target))
            {
                origin.Memes[m.Id] = 0.85f;
                return m;
            }
        return NewMeme(kind, target, name, origin, band);
    }

    internal Meme NewMeme(MemeKind kind, StimulusCategory target, string name, Agent origin, Band band)
    {
        var m = new Meme { Id = _nextMeme++, Kind = kind, Target = target, Name = name, OriginAgentId = origin.Id, OriginBandId = band.Id, CreatedDay = Day };
        Memes[m.Id] = m;
        origin.Memes[m.Id] = 0.85f;
        return m;
    }

    /// <summary>How well a meme fits a listener's temperament.</summary>
    float Fit(Agent a, Meme m) => m.Kind switch
    {
        MemeKind.SpiritBelief or MemeKind.Ritual => a.T(Trait.SelfTranscendence),
        MemeKind.Taboo => 0.3f + 0.7f * a.T(Trait.HarmAvoidance),
        MemeKind.SharingNorm or MemeKind.Hospitality => a.T(Trait.Cooperativeness),
        MemeKind.Xenophobia => 0.5f * a.T(Trait.HarmAvoidance) + 0.5f * (1f - a.T(Trait.Cooperativeness)),
        MemeKind.ElderRule or MemeKind.InnovationTaboo => 0.5f * a.T(Trait.RewardDependence) + 0.5f * (1f - a.T(Trait.NoveltySeeking)),
        _ => 0.5f,
    };

    // ------------------------------------------------------------------ talking

    void Socialize(Agent a, Band band)
    {
        var other = PickSomeone(a, band, adultsOnly: false);
        a.Needs[(int)Need.Social] = MathF.Max(0f, a.NeedOf(Need.Social) - 0.6f);
        a.Belonging = MathF.Min(1f, a.Belonging + 0.01f);
        if (other == null) return;
        other.Needs[(int)Need.Social] = MathF.Max(0f, other.NeedOf(Need.Social) - 0.4f);

        var ra = a.Rel(other.Id);
        var rb = other.Rel(a.Id);
        float compat = 1f - 0.5f * MathF.Abs(a.T(Trait.Cooperativeness) - other.T(Trait.Cooperativeness))
                          - 0.3f * MathF.Abs(a.T(Trait.SelfTranscendence) - other.T(Trait.SelfTranscendence))
                          + 0.3f * MathF.Min(a.T(Trait.Cooperativeness), other.T(Trait.Cooperativeness));
        float delta = 0.06f * (compat - 0.55f) + Rng.Normal(0f, 0.04f);
        ra.Affinity = Math.Clamp(ra.Affinity + delta, -1f, 1f);
        rb.Affinity = Math.Clamp(rb.Affinity + delta, -1f, 1f);

        // Quarrels: likelier between the uncooperative, the angry and the aggrieved.
        float quarrel = 0.035f * (1.2f - a.T(Trait.Cooperativeness)) * (1.2f - other.T(Trait.Cooperativeness))
                        * (1f + a.Emo(Emotion.Anger) + other.Emo(Emotion.Anger)) * (ra.Grudge || rb.Grudge ? 3f : 1f);
        if (Rng.Chance(quarrel))
        {
            ra.Affinity = MathF.Max(-1f, ra.Affinity - 0.2f);
            rb.Affinity = MathF.Max(-1f, rb.Affinity - 0.2f);
            Psychology.Appraise(a, new Appraisal(EventCategory.Conflict, "quarrel", -0.6f, 0.4f, Agency.Other, OtherId: other.Id), Day);
            Psychology.Appraise(other, new Appraisal(EventCategory.Conflict, "quarrel", -0.6f, 0.4f, Agency.Other, OtherId: a.Id), Day);
            Interlocked.Increment(ref Stats.Year.Quarrels);
            if (a.Emo(Emotion.Anger) > 0.6f && a.T(Trait.Cooperativeness) < 0.25f && ra.Grudge && Rng.Chance(0.15f)) Violence(a, other, band);
            return;
        }

        // Ideas spread: conformity bias (what most people around believe) and prestige bias.
        foreach (var (memeId, conv) in a.Memes)
        {
            if (conv < 0.4f || !Memes.TryGetValue(memeId, out var meme)) continue;
            float prevalence = Prevalence(band, memeId);
            float p = 0.12f * conv * (0.4f + 0.6f * other.T(Trait.RewardDependence) * prevalence + 0.3f * MathF.Min(a.Prestige, 1f))
                      * Fit(other, meme) * (0.5f + MathF.Max(0f, rb.Affinity));
            if (Rng.Chance(p))
            {
                other.Memes[memeId] = MathF.Min(1f, other.Memes.GetValueOrDefault(memeId) + 0.25f);
            }
        }

        // Stories of danger travel too (observational fear at a distance).
        int topFear = -1; float topValue = 0.5f;
        for (int s = 0; s < Ids.StimulusCount; s++) if (a.Fear[s] > topValue) { topValue = a.Fear[s]; topFear = s; }
        if (topFear >= 0 && Rng.Chance(0.15f)) Fear.Observe(other, (Stimulus)topFear, topValue * 0.35f, Day);

        // Techniques pass on by watching and talking.
        if (Rng.Chance(0.04f * a.T(Trait.Cooperativeness)))
        {
            var teachable = a.Knows & ~other.Knows;
            if (!teachable.IsEmpty) LearnOne(other, teachable);
        }

        if (a.T(Trait.Cooperativeness) < 0.4f && other.Prestige > a.Prestige + 0.5f) Psychology.Envy(a, 0.2f);
    }

    Agent? PickSomeone(Agent a, Band band, bool adultsOnly)
    {
        if (band.Members.Count < 2) return null;
        Agent? best = null;
        float bestScore = float.MinValue;
        for (int k = 0; k < 4; k++)
        {
            int id = band.Members[Rng.Next(band.Members.Count)];
            if (id == a.Id || !Living.TryGetValue(id, out var o) || !o.Alive) continue;
            if (adultsOnly && !o.IsAdult(Day)) continue;
            float s = a.AffinityTo(id) + Rng.NextFloat() * 0.5f;
            if (s > bestScore) { bestScore = s; best = o; }
        }
        return best;
    }

    /// <summary>Picks up one of <paramref name="options"/> - only one the learner has the grounding for.</summary>
    void LearnOne(Agent learner, TechSet options)
    {
        int count = 0;
        foreach (var t in options.Ids()) if (Techs.CanLearn(learner.Knows, t)) count++;
        if (count == 0) return;
        int pick = Rng.Next(count);
        foreach (var t in options.Ids())
            if (Techs.CanLearn(learner.Knows, t) && pick-- == 0)
            {
                // People who once gave in about a domain are reluctant to pick it up again.
                if (learner.IsSuppressed(Techs.Get(t).Domain) && Rng.Chance(0.8f)) return;
                learner.Knows |= Techs.Bit(t);
                return;
            }
    }

    void Violence(Agent attacker, Agent victim, Band band)
    {
        victim.Health -= Rng.Range(0.2f, 0.7f);
        victim.Scars++;
        Interlocked.Increment(ref Stats.Year.Assaults);
        Psychology.Appraise(victim, new Appraisal(EventCategory.Conflict, "assaulted", -1f, 0.9f, Agency.Other, Threat: true, OtherId: attacker.Id), Day);
        victim.Rel(attacker.Id).Grudge = true;
        attacker.Prestige -= 0.3f;
        foreach (int id in band.Members)
            if (Living.TryGetValue(id, out var m) && m != attacker && m != victim) m.Rel(attacker.Id).Affinity -= 0.1f;
        if (victim.Health <= 0f)
        {
            Kill(victim, DeathCause.Violence, null, $", struck down by {attacker.Name}");
            Later(() => Chronicle.Moment(new LlmMoment(Day, "killing", attacker.Id, band.Id, new() { ["victim"] = victim.Name, ["grudge"] = "true" })));
        }
    }

    // ------------------------------------------------------------------ love

    void Court(Agent a, Band band)
    {
        Agent? target = null;
        float best = float.MinValue;
        void Consider(Band b, int samples, float penalty)
        {
            for (int k = 0; k < samples && b.Members.Count > 0; k++)
            {
                int id = b.Members[Rng.Next(b.Members.Count)];
                if (!Living.TryGetValue(id, out var o) || o.Sex == a.Sex || o.PartnerId >= 0 || !o.IsAdult(Day) || o.Age(Day) > 50f) continue;
                if (Related(a, o)) continue;
                float s = Attraction(a, o) + Rng.NextFloat() * 0.3f - penalty;
                if (s > best) { best = s; target = o; }
            }
        }
        Consider(band, 6, 0f);
        // Most foragers marry out: look to friendly neighbouring bands too, especially from a small band.
        if (target == null || band.Members.Count < 25)
            foreach (var other in Bands.Values)
                if (other.Id != band.Id && World.Distance(other.Cell, band.Cell) <= World.Cells(2) && band.RelationTo(other.Id) > -0.3f)
                    Consider(other, 3, 0.1f + (other.CultureId != band.CultureId ? 0.1f : 0f));
        if (target == null) return;

        float accept = Attraction(target, a) + 0.2f * target.T(Trait.NoveltySeeking) - 0.12f;
        if (Rng.Chance(Math.Clamp(accept, 0.05f, 0.9f)))
        {
            Bond(a, target, Day);
            Stats.Year.Pairings++;
            if (target.BandId != a.BandId)
            {
                var mover = Rng.Chance(0.5f) ? a : target;
                MoveToBand(mover, mover == a ? target.BandId : a.BandId);
            }
            return;
        }

        // Rejected. Small stakes most of the time; a public, hoped-for refusal can change someone.
        float stake = MathF.Max(0f, a.AffinityTo(target.Id)) + a.Emo(Emotion.Love) * 0.5f;
        a.Rel(target.Id).Affinity -= 0.05f;
        if (stake < 0.4f || !Rng.Chance(0.25f))
        {
            Psychology.Appraise(a, new Appraisal(EventCategory.Social, "rejected", -0.5f, 0.3f, Agency.Other, OtherId: target.Id), Day);
            return;
        }
        var ctx = new PivotContext(Math.Clamp(stake, 0f, 1f), 0, false, "courtship");
        BeginPivot(a, band, new PivotCase(a.Id, band.Id, Domain.Social, ctx, "courtship", target.Name, Array.Empty<int>(), -1, null, target.Id));
    }

    float Attraction(Agent viewer, Agent other)
    {
        float similarity = 0f;
        for (int t = 0; t < Ids.TraitCount; t++) similarity += 1f - MathF.Abs(viewer.T((Trait)t) - other.T((Trait)t));
        similarity /= Ids.TraitCount;
        return 0.3f * similarity + 0.3f * other.Gene(Gene.Charisma) + 0.2f * MathF.Min(other.Prestige, 1.5f)
               + 0.3f * viewer.AffinityTo(other.Id) + (other.Genome.Has(Quirk.Charming) ? 0.15f : 0f);
    }

    bool Related(Agent a, Agent b) =>
        a.MotherId == b.Id || a.FatherId == b.Id || b.MotherId == a.Id || b.FatherId == a.Id
        || (a.MotherId >= 0 && a.MotherId == b.MotherId) || (a.FatherId >= 0 && a.FatherId == b.FatherId);

    void CareForChild(Agent a, Band band)
    {
        Agent? child = null;
        foreach (int id in a.Children)
            if (Living.TryGetValue(id, out var c) && c.BandId == band.Id && (child == null || c.Age(Day) < child.Age(Day))) child = c;
        if (child == null) return;
        child.Needs[(int)Need.Social] = MathF.Max(0f, child.NeedOf(Need.Social) - 0.4f);
        child.Rel(a.Id).Affinity = MathF.Min(1f, child.AffinityTo(a.Id) + 0.03f);
        a.Feel(Emotion.Love, 0.03f);
        // Upbringing: character, beliefs and fears rub off.
        for (int t = (int)Trait.SelfDirectedness; t <= (int)Trait.SelfTranscendence; t++)
            child.Shift((Trait)t, (a.T((Trait)t) - child.T((Trait)t)) * 0.001f, Day);
        foreach (var (meme, conv) in a.Memes)
            if (conv > 0.5f && Rng.Chance(0.05f)) child.Memes[meme] = MathF.Max(child.Memes.GetValueOrDefault(meme), conv * 0.6f);
        for (int s = 0; s < Ids.StimulusCount; s++)
            if (a.Fear[s] > 0.5f && Rng.Chance(0.03f)) Fear.Observe(child, (Stimulus)s, a.Fear[s] * 0.5f, Day);
    }

    bool CanTeach(Agent a, Band band)
    {
        if (a.Knows.IsEmpty || a.IsChild(Day)) return false;
        for (int k = 0; k < 3; k++)
        {
            int id = band.Members[Rng.Next(band.Members.Count)];
            if (Living.TryGetValue(id, out var o) && !(a.Knows & ~o.Knows).IsEmpty) return true;
        }
        return false;
    }

    void Teach(Agent a, Band band)
    {
        var learner = PickSomeone(a, band, adultsOnly: false);
        if (learner == null) return;
        var teachable = a.Knows & ~learner.Knows;
        if (teachable.IsEmpty) return;
        if (Rng.Chance(0.25f * (0.5f + learner.Gene(Gene.Intellect)) * (0.5f + a.Efficacy[(int)Domain.Social])))
        {
            LearnOne(learner, teachable);
            a.Prestige += 0.03f;
            Psychology.Practice(a, Domain.Social, true, a.Genome.Has(Quirk.Stutter) ? 0.3f : 1f);
        }
    }

    // ------------------------------------------------------------------ faith

    void Ritual(Agent a, Band band)
    {
        float conv = Held(a, MemeKind.Ritual);
        a.Feel(Emotion.Calm, 0.2f + 0.2f * conv);
        a.Emotions[(int)Emotion.Fear] *= 1f - 0.2f * (0.5f + conv);
        a.Emotions[(int)Emotion.Sadness] *= 0.9f;
        a.Belonging = MathF.Min(1f, a.Belonging + 0.02f * conv);
        band.Stores.TakeFood(0.2f * conv);   // offerings
        Psychology.Practice(a, Domain.Spiritual, true, 0.5f);
        a.Prestige += 0.01f * conv * a.T(Trait.SelfTranscendence);
        a.Shift(Trait.SelfTranscendence, 0.0008f * conv, Day);   // practice deepens faith, slowly

        // Superstition: if the feared thing hasn't struck lately, the ritual "works".
        foreach (var (id, c) in a.Memes.ToArray())
            if (Memes.TryGetValue(id, out var m) && m.Kind is MemeKind.Ritual or MemeKind.SpiritBelief)
                a.Memes[id] = MathF.Min(1f, c + 0.004f);
    }

    /// <summary>
    /// An unexplained blow (disaster, sudden death) moves a spiritual person to explain it.
    /// The explanation becomes a god and a ritual, offered to the band, which may embrace or mock it.
    /// </summary>
    internal void MaybeMyth(Band band, StimulusCategory cause, float intensity, string what)
    {
        Agent? seer = null;
        foreach (int id in band.Members)
            if (Living.TryGetValue(id, out var a) && a.IsAdult(Day) && a.T(Trait.SelfTranscendence) > 0.62f
                && (seer == null || a.T(Trait.SelfTranscendence) > seer.T(Trait.SelfTranscendence))) seer = a;
        if (seer == null) return;

        foreach (var (id, conv) in seer.Memes)
            if (conv > 0.4f && Memes.TryGetValue(id, out var existing) && existing.Kind == MemeKind.SpiritBelief && existing.Target == cause)
            {
                seer.Memes[id] = MathF.Min(1f, conv + 0.15f);   // "as foretold": faith deepens
                return;
            }
        if (!Rng.Chance(0.3f + 0.5f * intensity)) return;

        var culture = Cultures[band.CultureId];
        string god = Names.Deity(culture.Syllables, cause, Rng);
        var belief = NewMeme(MemeKind.SpiritBelief, cause, god, seer, band);
        var ritual = NewMeme(MemeKind.Ritual, cause, $"offerings to {god.Split(' ')[0]}", seer, band);
        belief.PairedMemeId = ritual.Id; ritual.PairedMemeId = belief.Id;
        seer.Memes[ritual.Id] = 0.8f;
        Stats.Year.Myths++;

        var (approvers, _) = Judge(band, seer, j => 0.6f * j.T(Trait.SelfTranscendence) + 0.25f * j.T(Trait.HarmAvoidance)
                                                   + 0.2f * j.AffinityTo(seer.Id) - 0.35f);
        float f = approvers.Count / (float)Math.Max(1, AdultCount(band) - 1);
        Chronicle.Add(new WorldEvent(Day, EventCategory.Spiritual, "myth",
            $"After the {what}, {seer.Name} of the {band.Name} speaks of {god}. {(f >= 0.5f ? "Many listen." : f < 0.25f ? "Most scoff." : "Some listen.")}",
            0.55f, seer.Id, band.Id));
        Chronicle.Moment(new LlmMoment(Day, "myth", seer.Id, band.Id, new()
        {
            ["deity"] = god, ["cause"] = Ids.Snake(cause), ["event"] = what, ["approval"] = f.ToString("0.00"),
        }));
        foreach (var j in approvers) { j.Memes[belief.Id] = 0.5f; j.Memes[ritual.Id] = 0.45f; }

        if (f >= 0.5f) Psychology.Praise(seer, Domain.Spiritual, 0.5f + f, Day);
        else if (f < 0.25f) SocialRejection(seer, band, Domain.Spiritual, approvers, "belief", god, belief.Id);
    }

    // ------------------------------------------------------------------ invention

    static readonly (string kind, Domain domain)[] CreationKinds =
    {
        ("song", Domain.Social), ("carving", Domain.Crafting), ("dance", Domain.Spiritual), ("new tool shape", Domain.Crafting),
        ("hunting trick", Domain.Hunting), ("recipe", Domain.Foraging), ("story", Domain.Spiritual), ("way of weaving reeds", Domain.Crafting),
    };

    /// <summary>
    /// Everyday creativity: a song, a carving, a trick. Shown to the band (or to a sweetheart), it
    /// earns praise or scorn. This is the commonest trigger for the conform/double-down/leave pivots.
    /// </summary>
    void Create(Agent a, Band band)
    {
        var (kind, domain) = CreationKinds[Rng.Next(CreationKinds.Length)];
        if (a.IsSuppressed(domain)) return;
        Stats.Year.Creations++;
        float quality = Math.Clamp(0.3f * a.Gene(Gene.Intellect) + 0.3f * a.Efficacy[(int)domain] + 0.2f * a.T(Trait.NoveltySeeking)
                                   + Rng.Normal(0.2f, 0.2f), 0f, 1f);

        // Sometimes it's made to impress someone in particular.
        Agent? crush = null;
        if (a.PartnerId < 0 && a.Age(Day) is >= 16f and < 45f && Rng.Chance(0.35f))
            foreach (int id in band.Members)
                if (Living.TryGetValue(id, out var o) && o.Sex != a.Sex && o.PartnerId < 0 && o.IsAdult(Day) && !Related(a, o)
                    && (crush == null || a.AffinityTo(o.Id) > a.AffinityTo(crush.Id))) crush = o;
        if (crush != null)
        {
            float liked = Attraction(crush, a) + 0.4f * quality + Rng.Normal(0f, 0.15f);
            if (liked > 0.55f) { Bond(a, crush, Day); Stats.Year.Pairings++; Psychology.Praise(a, domain, 0.4f, Day); return; }
            crush.Rel(a.Id).Affinity -= 0.05f;
            if (MathF.Max(0f, a.AffinityTo(crush.Id)) + a.Emo(Emotion.Love) < 0.3f) return;
            var ctx = new PivotContext(0.6f, 0, false, "courtship");
            BeginPivot(a, band, new PivotCase(a.Id, band.Id, domain, ctx, "courtship", $"{kind} made for {crush.Name}", Array.Empty<int>(), -1, null, crush.Id));
            return;
        }

        var (approvers, _) = Judge(band, a, j => 0.35f * j.T(Trait.NoveltySeeking) + 0.25f * j.T(Trait.Cooperativeness)
                                               + 0.35f * j.AffinityTo(a.Id) + 0.5f * (quality - 0.5f) - 0.25f
                                               - 0.2f * Held(j, MemeKind.InnovationTaboo));
        float f = approvers.Count / (float)Math.Max(1, AdultCount(band) - 1);
        if (f >= 0.55f)
        {
            Psychology.Praise(a, domain, 0.3f + 0.4f * f, Day);
            if (f > 0.8f && quality > 0.7f)
                Chronicle.Add(new WorldEvent(Day, EventCategory.Social, "creation_praised",
                    $"{a.Name}'s {kind} delights the {band.Name}.", 0.25f, a.Id, band.Id));
        }
        else if (f < 0.3f && Rng.Chance(0.5f))
            SocialRejection(a, band, domain, approvers, "creation", kind);
    }

    void Experiment(Agent a, Band band, in DayContext c)
    {
        a.Needs[(int)Need.Energy] += 0.08f;
        if (Rng.Chance(0.03f * (0.5f + a.T(Trait.NoveltySeeking)) * (a.Genome.Has(Quirk.Dreamer) ? 1.5f : 1f)))
        {
            Create(a, band);
            return;
        }
        var w = World;
        int cell = band.Cell;
        foreach (var def in Techs.All)
        {
            if (!Techs.CanDiscover(a.Knows, def.Id) || Techs.Has(c.Tech, def.Id)) continue;   // novel to the band
            if (a.IsSuppressed(def.Domain)) continue;
            float need = def.Id switch
            {
                TechId.HideClothing or TechId.Shelter => c.Temp < 8f ? 2.5f : 0.7f,
                TechId.ControlledFire => c.Temp < 10f ? 2f : 1f,
                TechId.FoodStorage or TechId.Agriculture or TechId.Domestication => 1f + 2f * c.Shortage,
                TechId.Herbalism => band.PlagueUntil > Day ? 3f : 1f,
                TechId.Fishing => w.FishK[cell] > 0f ? 1.5f : 0f,
                // Any coast will do; islanders and the hungry look to the sea hardest.
                TechId.Boats => !IsCoastal(cell) ? 0f
                    : (w.FishK[cell] > 300f ? 1.3f : 1f) * (w.LandmassSize[w.Landmass[cell]] < 300 ? 2f : 1f) * (1f + c.Shortage),
                TechId.Pottery => w.Flow[cell] > WorldGen.RiverFlow || w.Biome[cell] is Biome.Wetland ? 1.3f : 0.5f,
                TechId.Copper or TechId.Bronze => w.Fault[cell] > 0.3f ? 1f : 0.2f,
                _ => Affords(band, def.Needs),
            };
            if (def.Id == TechId.Agriculture && w.Biome[cell] is not (Biome.TemperateGrassland or Biome.TemperateForest or Biome.Savanna
                    or Biome.TropicalSeasonalForest or Biome.Shrubland or Biome.Wetland)) need = 0f;
            if (def.Id == TechId.Domestication && w.PreyK[cell] < 300f) need = 0f;

            float p = 0.00004f * Config.InventionRate * need / def.Difficulty
                      * (0.5f + a.Gene(Gene.Intellect)) * (0.4f + a.T(Trait.NoveltySeeking))
                      * (1f + a.Efficacy[(int)def.Domain]) * (a.Genome.Has(Quirk.Dreamer) ? 1.4f : 1f) * (a.InspiredUntil > Day ? 3f : 1f);
            if (!Rng.Chance(p)) continue;
            Discover(a, band, def);
            return;
        }
    }

    /// <summary>
    /// How strongly a band's surroundings suggest a technique: materials at hand (forest, rock,
    /// ore, clay, sand), climate, water, a settled or large community, herds, a spiritual life.
    /// </summary>
    float Affords(Band band, Affordance need)
    {
        var w = World;
        int cell = band.Cell;
        bool forest = w.Biome[cell] is Biome.BorealForest or Biome.TemperateForest or Biome.TemperateRainforest
                      or Biome.TropicalSeasonalForest or Biome.TropicalRainforest;
        bool present = need switch
        {
            Affordance.None => true,
            Affordance.Water => w.FishK[cell] > 0f || w.Flow[cell] > WorldGen.RiverFlow || IsCoastal(cell),
            Affordance.Coast => IsCoastal(cell),
            Affordance.Forest => forest,
            Affordance.Rock => w.Elevation[cell] > 1200f || w.Fault[cell] > 0.35f || w.Biome[cell] is Biome.Alpine or Biome.ColdDesert or Biome.Volcanic,
            Affordance.Ore => w.Fault[cell] > 0.3f,
            Affordance.Clay => w.Flow[cell] > WorldGen.RiverFlow || w.Biome[cell] is Biome.Wetland,
            Affordance.Sand => w.Biome[cell] is Biome.HotDesert or Biome.ColdDesert || IsCoastal(cell),
            Affordance.Cold => w.TempMean[cell] - w.TempAmp[cell] < -2f,
            Affordance.Hot => w.TempMean[cell] > 20f,
            Affordance.Dry => w.Precip[cell] < 600f,
            Affordance.Grass => w.Biome[cell] is Biome.TemperateGrassland or Biome.Savanna or Biome.Shrubland,
            Affordance.Settled => band.Sedentary,
            Affordance.Many => band.Members.Count >= 45 || band.Sedentary,
            Affordance.Herds => BandKnows(band, TechId.Domestication),
            Affordance.Spirit => band.MemePrevalence.Count > 0,
            _ => true,
        };
        return present ? (need == Affordance.None ? 1f : 1.4f) : 0.12f;
    }

    void Discover(Agent a, Band band, TechDef def)
    {
        a.Knows |= Techs.Bit(def.Id);
        Stats.Year.Inventions++;
        if (def.Id == TechId.Domestication && band.Herds.Count == 0)
        {
            // The idea comes with the animals that gave it: a few young ones kept, and the dogs that hang about camp.
            band.Herds[HerdSpecies(band.Cell)] = 3f;
            band.Herds["dog"] = 2f;
        }
        bool worldFirst = Chronicle.First("tech:" + def.Id);
        string tech = Techs.Name(def.Id).Replace('_', ' ');

        // The band judges it: the novelty-seeking and the young are open; the cautious, the
        // approval-bound and the innovation-wary are not; friends of the inventor lean yes.
        float wary = BandHolds(band, MemeKind.InnovationTaboo, 0.3f) ? 0.35f : 0f;
        var (approvers, mockers) = Judge(band, a, j =>
            0.45f * j.T(Trait.NoveltySeeking) - 0.25f * j.T(Trait.HarmAvoidance) + 0.3f * j.AffinityTo(a.Id)
            + 0.3f * def.Value * j.Gene(Gene.Intellect) - (j.Age(Day) > 45f ? 0.12f : 0f) - wary - 0.05f);
        int judges = Math.Max(1, AdultCount(band) - 1);
        float f = approvers.Count / (float)judges;

        string verdict = f >= 0.5f ? "embraced" : f < 0.3f ? "mocked" : "met with shrugs";
        Chronicle.Add(new WorldEvent(Day, EventCategory.Discovery, "invention",
            $"{a.Name} of the {band.Name} discovers {tech}{(worldFirst ? " - the first in the world" : "")}. It is {verdict}.",
            worldFirst ? 0.8f : 0.4f, a.Id, band.Id));
        Chronicle.Moment(new LlmMoment(Day, "invention", a.Id, band.Id, new()
        {
            ["tech"] = Techs.Name(def.Id), ["approval"] = f.ToString("0.00"), ["world_first"] = worldFirst.ToString(), ["verdict"] = verdict,
        }));

        if (f >= 0.5f)
        {
            Psychology.Praise(a, def.Domain, 0.6f + f, Day);
            foreach (var j in approvers) if (Rng.Chance(0.5f)) j.Knows |= Techs.Bit(def.Id);
        }
        else if (f < 0.3f)
        {
            SocialRejection(a, band, def.Domain, approvers, "invention", tech, -1, def.Id);
        }
        else
        {
            a.Feel(Emotion.Pride, 0.2f);
            foreach (var j in approvers) if (Rng.Chance(0.3f)) j.Knows |= Techs.Bit(def.Id);
        }
    }

    (List<Agent> approvers, List<Agent> mockers) Judge(Band band, Agent subject, Func<Agent, float> score)
    {
        var yes = new List<Agent>();
        var no = new List<Agent>();
        foreach (int id in band.Members)
        {
            if (id == subject.Id || !Living.TryGetValue(id, out var j) || !j.IsAdult(Day)) continue;
            float s = score(j) + Rng.Normal(0f, 0.15f);
            if (band.LeaderId == j.Id) s *= 1.5f;   // the leader's view carries
            (s > 0f ? yes : no).Add(j);
        }
        return (yes, no);
    }

    int AdultCount(Band band)
    {
        int n = 0;
        foreach (int id in band.Members) if (Living.TryGetValue(id, out var a) && a.IsAdult(Day)) n++;
        return n;
    }

    /// <summary>
    /// The circle shunned something this person made or believed. They conform (and may abandon
    /// it, so it can be lost), double down, withdraw, resent, or leave and found their own band.
    /// </summary>
    void SocialRejection(Agent a, Band band, Domain domain, List<Agent> allies, string kind, string what, int memeId = -1, TechId? tech = null)
    {
        float severity = Math.Clamp(1f - allies.Count / (float)Math.Max(1, AdultCount(band) - 1) * 2f, 0.2f, 1f);
        bool canLeave = band.Members.Count > 6;
        var ctx = new PivotContext(severity, allies.Count, canLeave, kind);
        Psychology.Appraise(a, new Appraisal(EventCategory.Social, $"{kind}_rejected", -0.8f, 0.5f + 0.5f * severity, Agency.Other, Witnessed: true, Detail: what), Day);
        BeginPivot(a, band, new PivotCase(a.Id, band.Id, domain, ctx, kind, what, allies.Select(x => x.Id).ToArray(), memeId, tech, -1));
    }

    void RecordPivot(Agent a, Band band, PivotChoice choice, string kind, string what)
    {
        Stats.Year.Pivots[(int)choice]++;
        string text = choice switch
        {
            PivotChoice.Conform => $"{a.Name} gives in and abandons the {what}, and will not try such things again.",
            PivotChoice.DoubleDown => $"{a.Name} refuses to give up the {what}, set on proving the {band.Name} wrong.",
            PivotChoice.Withdraw => $"{a.Name} withdraws from the {band.Name} after the {what} is scorned.",
            PivotChoice.Resent => $"{a.Name} says little, but does not forget who laughed at the {what}.",
            _ => $"{a.Name} turns away from the {band.Name} over the {what}.",
        };
        Chronicle.Add(new WorldEvent(Day, EventCategory.Social, "pivot_" + Ids.Snake(choice), text, kind == "courtship" ? 0.3f : 0.45f, a.Id, band.Id));
        Chronicle.Moment(new LlmMoment(Day, "pivot", a.Id, band.Id, new()
        {
            ["trigger"] = kind, ["subject"] = what, ["choice"] = Ids.Snake(choice),
            ["traits"] = string.Join(",", Enum.GetValues<Trait>().Select(t => $"{Ids.Snake(t)}={a.T(t):0.00}")),
        }));
    }
}

static class AgentExtensions
{
    public static int IsSuppressedCount(this Agent a) => System.Numerics.BitOperations.PopCount(a.SuppressedDomains);
}
