using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Evolysia.Sim.Psych;
using Evolysia.Sim.Society;
using Evolysia.Sim.World;

namespace Evolysia.Sim;

public sealed partial class Simulation
{
    public readonly List<(int Day, GodAction Action)> ActionLog = new();
    public readonly List<Prayer> Prayers = new();
    int _nextPrayer = 1;

    public void Act(GodAction action)
    {
        ActionLog.Add((Day, action));
        Apply(action);
        FlushDeathsAndBirths();
        if (Config.RequireModel)
            foreach (var band in BandsNear(action.Cell, Math.Max(World.Cells(2), action.Radius >= 0 ? action.Radius : Powers.DefaultRadius(action.Power, World.Scale)))) Reconsider(band.Id);
    }

    public IEnumerable<Prayer> OpenPrayers => Prayers.Where(p => p.Open);

    public Meme? PlayerGodOf(int cultureId) =>
        Memes.Values.FirstOrDefault(m => m.IsPlayerGod && m.CultureId == cultureId && m.Kind == MemeKind.SpiritBelief);

    public float FaithInPlayer(Band band) => PlayerGodOf(band.CultureId) is { } m ? Prevalence(band, m.Id) : 0f;

    List<Band> BandsNear(int cell, int radius) =>
        Bands.Values.Where(b => !b.Disbanded && World.Distance(b.Cell, cell) <= radius).ToList();

    IEnumerable<int> CellsNear(int cell, int radius)
    {
        int x0 = World.X(cell), y0 = World.Y(cell);
        for (int dy = -radius; dy <= radius; dy++)
        {
            int y = y0 + dy;
            if (y < 0 || y >= World.H) continue;
            for (int dx = -radius; dx <= radius; dx++) yield return World.Index(x0 + dx, y);
        }
    }

    void Apply(GodAction a)
    {
        var w = World;
        int r = a.Radius >= 0 ? a.Radius : Powers.DefaultRadius(a.Power, World.Scale);
        var bands = BandsNear(a.Cell, Math.Max(r, a.Power is Power.Lightning or Power.Plague ? 1 : r));
        if (a.Power is Power.Lightning or Power.Plague && bands.Count > 1)
            bands = bands.OrderBy(b => w.Distance(b.Cell, a.Cell)).Take(1).ToList();
        Stats.Year.GodActs++;

        switch (a.Power)
        {
            case Power.SpawnPlant:
                foreach (int cell in CellsNear(a.Cell, r).Where(w.IsDryLand))
                {
                    w.PlantStock[cell] = w.PlantK[cell];
                    w.Trees[cell] = w.TreesK[cell];
                }
                foreach (var b in bands) Witness(b, a.Power, true, 0.5f, "green shoots rising from bare earth");
                break;
            case Power.RaiseLand or Power.LowerLand:
                ShapeLand(a.Cell, r, a.Power == Power.RaiseLand ? 200f : -200f);
                foreach (var b in bands.Where(b => !b.Disbanded)) Witness(b, a.Power, a.Power == Power.RaiseLand, 0.8f, "the shape of the world changing beneath them");
                break;
            case Power.Possess:
                Possess(a.TargetAgentId);
                break;
            case Power.Rain:
                foreach (int c in CellsNear(a.Cell, r))
                {
                    w.YearRain[c] = MathF.Max(w.YearRain[c], 1.3f);
                    w.RainBias[c] = 1.3f; w.RainBiasUntil[c] = Day + SimConfig.DaysPerYear;
                    w.PlantStock[c] = MathF.Min(w.PlantK[c], w.PlantStock[c] + 0.3f * w.PlantK[c]);
                }
                foreach (var b in bands) Witness(b, a.Power, true, 0.5f, "rain falling from a clear sky");
                break;
            case Power.Drought:
                foreach (int c in CellsNear(a.Cell, r))
                {
                    w.YearRain[c] *= 0.35f; w.PlantStock[c] *= 0.6f;
                    w.RainBias[c] = 0.4f; w.RainBiasUntil[c] = Day + SimConfig.DaysPerYear;
                }
                foreach (var b in bands) { b.PlaceDread[(int)StimulusCategory.Sky] += 0.5f; Witness(b, a.Power, false, 0.4f, "the rains failing"); }
                break;
            case Power.Bless:
                foreach (int c in CellsNear(a.Cell, r)) { w.PlantStock[c] = w.PlantK[c]; w.PreyStock[c] = w.PreyK[c]; }
                foreach (var b in bands) { b.Stores.Add("food", b.Members.Count * 10f); Witness(b, a.Power, true, 0.7f, "the land overflowing with food"); }
                break;
            case Power.Fertility:
                foreach (var b in bands) { b.FertileBlessingUntil = Day + SimConfig.DaysPerYear; Witness(b, a.Power, true, 0.4f, "a season of plenty in the wombs"); }
                break;
            case Power.Heal:
                foreach (var b in bands)
                {
                    b.PlagueUntil = Day - 1;
                    foreach (int id in b.Members)
                        if (Living.TryGetValue(id, out var m)) { m.Sick = false; m.Health = 1f; }
                    Witness(b, a.Power, true, 0.8f, "the sick rising from their beds");
                }
                break;
            case Power.Game:
                foreach (int c in CellsNear(a.Cell, r)) { w.PreyK[c] *= 1.3f; w.PreyStock[c] = w.PreyK[c]; }
                foreach (var b in bands) Witness(b, a.Power, true, 0.4f, "herds pouring out of nowhere");
                break;
            case Power.Lightning:
                foreach (var b in bands) { Strike(b, "lightning strike", Stimulus.Lightning, 0.9f, 0f, DeathCause.Disaster, lightning: true); Witness(b, a.Power, false, 0.9f, "lightning from a clear sky"); }
                if (w.Biome[a.Cell] is Biome.BorealForest or Biome.TemperateForest or Biome.Savanna or Biome.Shrubland && Rng.Chance(0.4f))
                    w.PlantStock[a.Cell] *= 0.3f;
                break;
            case Power.Wildfire:
                foreach (int c in CellsNear(a.Cell, r)) { w.PlantStock[c] *= 0.2f; w.PreyStock[c] *= 0.6f; w.Ash[c] = MathF.Min(1f, w.Ash[c] + 0.3f); }
                DamageStructures(a.Cell, r, 0.7f, fire: true);
                foreach (var b in bands) { b.Stores.Scale(0.6f); Strike(b, "wildfire", Stimulus.Wildfire, 0.8f, 0.02f, DeathCause.Disaster); Witness(b, a.Power, false, 0.7f, "fire springing from nothing"); }
                break;
            case Power.Flood:
                DamageStructures(a.Cell, r, 0.6f, fire: false);
                foreach (var b in bands) { b.Stores.Scale(0.4f); Strike(b, "flood", Stimulus.Flood, 0.8f, 0.02f, DeathCause.Accident); Witness(b, a.Power, false, 0.7f, "water rising out of season"); }
                break;
            case Power.Earthquake:
                DamageStructures(a.Cell, r, 0.8f, fire: false);
                foreach (var b in bands) { Strike(b, "earthquake", Stimulus.Earthquake, 0.95f, 0.03f, DeathCause.Disaster); Witness(b, a.Power, false, 1f, "the ground tearing open"); }
                break;
            case Power.Blizzard:
                foreach (var b in bands)
                {
                    foreach (int id in b.Members) if (Living.TryGetValue(id, out var m)) m.Health -= 0.2f * (1f - 0.7f * b.Shelter);
                    Strike(b, "blizzard", Stimulus.Blizzard, 0.6f, 0.01f, DeathCause.Cold);
                    Witness(b, a.Power, false, 0.6f, "snow out of season");
                }
                break;
            case Power.Plague:
                foreach (var b in bands) { StartPlague(b); b.PlagueSeverity = MathF.Max(b.PlagueSeverity, 0.8f); Witness(b, a.Power, false, 0.5f, "a sickness with no cause"); }
                break;
            case Power.Meteor:
                foreach (int c in CellsNear(a.Cell, r))
                {
                    int d = w.Distance(a.Cell, c);
                    w.Ash[c] = MathF.Min(1f, w.Ash[c] + 1f / (1 + d));
                    w.PlantStock[c] *= 0.1f + 0.3f * d;
                }
                _volcanicCooling += 0.4f;
                foreach (var b in BandsNear(a.Cell, 6))
                {
                    int d = w.Distance(a.Cell, b.Cell);
                    if (d <= 1) Strike(b, "falling star", Stimulus.Eruption, 1f, d == 0 ? 0.4f : 0.1f, DeathCause.Disaster);
                    Witness(b, a.Power, false, d <= 1 ? 1f : 0.6f, "a star falling from the sky");
                }
                bands = BandsNear(a.Cell, 6);
                break;
            case Power.Beasts:
                foreach (int c in CellsNear(a.Cell, r)) { w.PredatorBase[c] = MathF.Min(1f, w.PredatorBase[c] * 1.6f + 0.2f); w.Predators[c] = w.PredatorBase[c]; }
                foreach (var b in bands) { foreach (int id in b.Members) if (Living.TryGetValue(id, out var m)) Fear.Scare(m, BeastHere(b.Cell), 0.5f, Day); Witness(b, a.Power, false, 0.4f, "beasts gathering at the edge of camp"); }
                break;
            case Power.Curse:
                if (Living.TryGetValue(a.TargetAgentId, out var cursed)) Curse(cursed);
                return;
            case Power.Message:
                LeaveMessage(a);
                return;
            case Power.Dream:
                if (Living.TryGetValue(a.TargetAgentId, out var dreamer)) Dream(dreamer, a.Dream, a.Words);
                return;
        }

        foreach (var b in bands) AnswerPrayers(b, a.Power);
        if (!Powers.IsKind(a.Power))
            foreach (var b in bands) AnswerProtection(b, a.Power);
        Chronicle.Add(new WorldEvent(Day, Powers.IsKind(a.Power) ? EventCategory.Spiritual : EventCategory.Danger, "god_act",
            $"By your hand: {Ids.Snake(a.Power).Replace('_', ' ')} at {w.X(a.Cell)},{w.Y(a.Cell)}" +
            (bands.Count > 0 ? $", over the {string.Join(", ", bands.Take(3).Select(b => b.Name))}." : "."), 0.45f, -1, bands.FirstOrDefault()?.Id ?? -1));
    }

    void Witness(Band band, Power power, bool kind, float intensity, string what)
    {
        foreach (int id in band.Members)
            if (Living.TryGetValue(id, out var m))
                Psychology.Appraise(m, new Appraisal(EventCategory.Spiritual, "witnessed_" + Ids.Snake(power), kind ? 1f : -1f, intensity,
                    Agency.Unknown, Threat: !kind, Detail: what), Day);
        band.Awe += intensity * Powers.Spectacle(power);
        band.DivineFavor += kind ? intensity : -intensity;

        var god = PlayerGodOf(band.CultureId);
        if (god != null && Prevalence(band, god.Id) > 0.1f)
        {

            foreach (int id in band.Members)
                if (Living.TryGetValue(id, out var m) && m.Memes.TryGetValue(god.Id, out var conv))
                {
                    m.Memes[god.Id] = MathF.Min(1f, conv + 0.12f * intensity);
                    if (kind) m.Feel(Emotion.Love, 0.15f * intensity); else m.Feel(Emotion.Fear, 0.2f * intensity);
                }
            return;
        }
        if (band.Awe >= 1.5f) Revelation(band, null, what);
    }

    void Revelation(Band band, Agent? prophet, string what)
    {
        prophet ??= band.Members.Select(id => Living.GetValueOrDefault(id)).Where(a => a != null && a.IsAdult(Day))
            .OrderByDescending(a => a!.T(Trait.SelfTranscendence)).FirstOrDefault();
        if (prophet == null) return;
        var culture = Cultures[band.CultureId];
        var god = PlayerGodOf(band.CultureId);
        bool first = god == null;
        if (god == null)
        {
            string epithet = band.DivineFavor >= 0f ? "the Watcher Above" : "the Hand in the Sky";
            god = NewMeme(MemeKind.SpiritBelief, StimulusCategory.Sky, $"{Names.Make(culture.Syllables, Rng, 2, 2)} {epithet}", prophet, band);
            god.IsPlayerGod = true;
            god.CultureId = band.CultureId;
            var ritual = NewMeme(MemeKind.Ritual, StimulusCategory.Sky, $"prayers to {god.Name.Split(' ')[0]}", prophet, band);
            ritual.PairedMemeId = god.Id; god.PairedMemeId = ritual.Id;
        }
        prophet.Memes[god.Id] = 0.9f;
        if (god.PairedMemeId >= 0) prophet.Memes[god.PairedMemeId] = 0.8f;
        band.Awe = 0f;
        Stats.Year.Revelations++;

        var (approvers, _) = Judge(band, prophet, j => 0.55f * j.T(Trait.SelfTranscendence) + 0.2f * j.AffinityTo(prophet.Id) + 0.15f);
        foreach (var j in approvers)
        {
            j.Memes[god.Id] = MathF.Max(j.Memes.GetValueOrDefault(god.Id), 0.6f);
            if (god.PairedMemeId >= 0) j.Memes[god.PairedMemeId] = MathF.Max(j.Memes.GetValueOrDefault(god.PairedMemeId), 0.5f);
        }
        Psychology.Praise(prophet, Domain.Spiritual, 0.8f, Day);
        float share = approvers.Count / (float)Math.Max(1, AdultCount(band) - 1);
        Chronicle.Add(new WorldEvent(Day, EventCategory.Spiritual, "revelation",
            $"{prophet.Name} of the {band.Name} proclaims you {god.Name}{(first ? $", the first of the {culture.Name} to name you" : "")}. " +
            (share >= 0.5f ? "Most believe." : share < 0.25f ? "Few believe." : "Some believe."), first ? 0.85f : 0.55f, prophet.Id, band.Id));
        Chronicle.Moment(new LlmMoment(Day, "revelation", prophet.Id, band.Id, new()
        {
            ["god"] = god.Name, ["wonder"] = what, ["kind"] = (band.DivineFavor >= 0f).ToString(), ["approval"] = share.ToString("0.00"),
        }));
    }

    void Dream(Agent a, DreamKind kind, string words)
    {
        var band = BandOf(a);
        if (band == null) return;
        float receptive = 0.4f + 0.6f * a.T(Trait.SelfTranscendence);
        Psychology.Appraise(a, new Appraisal(EventCategory.Spiritual, "dreamt_of_you", 1f, 0.8f, Agency.Unknown, Detail: "a dream sent from above"), Day);
        a.Feel(Emotion.Surprise, 0.6f);
        Stats.Year.Dreams++;

        var god = PlayerGodOf(band.CultureId);
        if (god == null || kind == DreamKind.Worship && Prevalence(band, god.Id) < 0.25f) Revelation(band, a, "a dream");
        god = PlayerGodOf(band.CultureId)!;
        a.Memes[god.Id] = MathF.Min(1f, a.Memes.GetValueOrDefault(god.Id) + 0.4f * receptive);

        string effect;
        switch (kind)
        {
            case DreamKind.Worship:
                a.Prestige += 0.6f * receptive;
                effect = "becomes your prophet";
                break;
            case DreamKind.Leave:
                if (Config.RequireModel) { effect = "wakes with a desire to leave, to weigh against the needs of the band"; break; }
                if (band.LeaderId == a.Id || a.Prestige >= 1.5f) { ForceMigrate(band, 4); effect = "leads the band to new land"; }
                else
                {
                    var followers = band.Members.Select(id => Living.GetValueOrDefault(id))
                        .Where(m => m != null && m != a && m.IsAdult(Day) && (m.AffinityTo(a.Id) > 0.2f || m.Memes.GetValueOrDefault(god.Id) > 0.4f))
                        .Select(m => m!).ToList();
                    if (followers.Count >= 2) { Split(band, a, followers, band.CultureId, BestCellNear(band, 4, a)); effect = $"leaves with {followers.Count} believers"; }
                    else effect = "wants to leave, but no one will follow";
                }
                break;
            case DreamKind.Peace:
                a.Emotions[(int)Emotion.Anger] = 0f;
                foreach (var rel in a.Relations.Values) { rel.Grudge = false; rel.Affinity = MathF.Max(rel.Affinity, 0f); }
                a.Shift(Trait.Cooperativeness, 0.1f * receptive, Day, scaleByPlasticity: false);
                if (!Config.RequireModel && band.LeaderId == a.Id) foreach (var k in band.Relations.Keys.ToList()) band.Relations[k] = MathF.Max(band.Relations[k], 0.3f);
                effect = !Config.RequireModel && band.LeaderId == a.Id ? "makes peace with every neighbour" : "lets go of every grudge";
                break;
            case DreamKind.Seek:
                a.Shift(Trait.NoveltySeeking, 0.12f * receptive, Day, scaleByPlasticity: false);
                a.InspiredUntil = Day + 120;
                effect = "is seized by the urge to make new things";
                break;
            case DreamKind.Courage:
                for (int s = 0; s < Ids.StimulusCount; s++) { a.Fear[s] *= 1f - 0.7f * receptive; a.Mastered[s] = MathF.Min(1f, a.Mastered[s] + 0.3f * receptive); }
                for (int c = 0; c < Ids.CategoryCount; c++) a.CategoryFear[c] *= 1f - 0.7f * receptive;
                effect = "wakes unafraid";
                break;
            default:
                a.Feel(Emotion.Love, 0.6f);
                a.Emotions[(int)Emotion.Sadness] *= 0.3f;
                effect = "wakes full of love";
                break;
        }
        Chronicle.Add(new WorldEvent(Day, EventCategory.Spiritual, "dream",
            $"{a.Name} of the {band.Name} dreams of you, and {effect}.", 0.55f, a.Id, a.BandId));
        Chronicle.Moment(new LlmMoment(Day, "dream", a.Id, a.BandId, new()
        {
            ["god"] = god.Name, ["urge"] = Ids.Snake(kind), ["words"] = words, ["effect"] = effect, ["receptive"] = receptive.ToString("0.00"),
        }));
    }

    void Curse(Agent a)
    {
        var band = BandOf(a);
        a.Health -= 0.4f;
        a.Sick = true;
        a.SickUntil = Day + 30;
        Psychology.Appraise(a, new Appraisal(EventCategory.Spiritual, "cursed", -1f, 0.9f, Agency.Unknown, Threat: true, Detail: "a curse from above"), Day);
        a.Feel(Emotion.Shame, 0.4f);
        if (band != null && PlayerGodOf(band.CultureId) is { } god && a.Memes.ContainsKey(god.Id))
            a.Memes[god.Id] = MathF.Min(1f, a.Memes[god.Id] + 0.2f);
        Stats.Year.GodActs++;
        Chronicle.Add(new WorldEvent(Day, EventCategory.Spiritual, "curse", $"You curse {a.Name}{(band != null ? $" of the {band.Name}" : "")}.", 0.45f, a.Id, a.BandId));
        if (a.Health <= 0f) Kill(a, DeathCause.Disease, null, ", withered by a curse");
    }

    int BestCellNear(Band band, int radius, Agent? judge)
    {
        int best = band.Cell; float bestScore = float.NegativeInfinity;
        foreach (int c in CellsNear(band.Cell, radius))
        {
            if (c == band.Cell) continue;
            float s = CellScore(band, c, judge);
            if (s > bestScore) { bestScore = s; best = c; }
        }
        return best;
    }

    void ForceMigrate(Band band, int radius)
    {
        int target = BestCellNear(band, radius, Living.GetValueOrDefault(band.LeaderId));
        LeaveCamp(band, target);
        band.Cell = target;
        BandsChanged();
        ArrivedAt(band);
        band.LastMoveDay = Day;
        band.Sedentary = false;
        band.Shelter = 0f;
        Array.Clear(band.PlaceDread);
        Stats.Year.Migrations++;
    }

    void PrayerUpkeep()
    {
        foreach (var p in Prayers.Where(p => p.Open && Day - p.Day > 120).ToList())
        {
            p.Expired = true; p.ResolvedDay = Day;
            if (!Bands.TryGetValue(p.BandId, out var band)) continue;
            ShakeFaith(band, -0.2f);
            Chronicle.Add(new WorldEvent(Day, EventCategory.Spiritual, "prayer_ignored",
                $"The {band.Name} prayed for {Powers.Verb(p.Need)} and heard nothing. Some begin to doubt.", 0.4f, p.AgentId, band.Id));
        }

        foreach (var band in Bands.Values)
        {
            if (band.Disbanded || FaithInPlayer(band) < 0.25f || Prayers.Any(p => p.Open && p.BandId == band.Id)) continue;
            PrayerNeed? need =
                band.PlagueUntil > Day ? PrayerNeed.Healing :
                Day - band.LastRaidedDay < 120 ? PrayerNeed.Protection :
                band.Stress > 0.15f ? (World.YearRain[band.Cell] < 0.75f ? PrayerNeed.Rain : PrayerNeed.Food) :
                band.Members.Count < 12 && !band.Members.Any(id => Living.TryGetValue(id, out var k) && k.Age(Day) < 3f) ? PrayerNeed.Children :
                null;
            if (need is not { } n || !Rng.Chance(0.5f)) continue;
            var god = PlayerGodOf(band.CultureId)!;
            var devout = band.Members.Select(id => Living.GetValueOrDefault(id)).Where(a => a != null && a.IsAdult(Day))
                .OrderByDescending(a => a!.Memes.GetValueOrDefault(god.Id) * (0.5f + a.T(Trait.SelfTranscendence))).First()!;
            var prayer = new Prayer { Id = _nextPrayer++, Day = Day, BandId = band.Id, AgentId = devout.Id, Need = n };
            Prayers.Add(prayer);
            Stats.Year.PrayersMade++;
            Chronicle.Add(new WorldEvent(Day, EventCategory.Spiritual, "prayer",
                $"{devout.Name} of the {band.Name} prays to {god.Name} for {Powers.Verb(n)}.", 0.5f, devout.Id, band.Id));
            Chronicle.Moment(new LlmMoment(Day, "prayer", devout.Id, band.Id, new() { ["god"] = god.Name, ["need"] = Powers.Verb(n) }));
        }
    }

    void AnswerPrayers(Band band, Power power)
    {
        foreach (var p in Prayers.Where(p => p.Open && p.BandId == band.Id && Powers.Answers(power, p.Need)).ToList())
            Answered(p, band, $"{Powers.Verb(p.Need)} came");
    }

    void AnswerProtection(Band struck, Power power)
    {
        foreach (var p in Prayers.Where(p => p.Open && p.Need == PrayerNeed.Protection).ToList())
            if (Bands.TryGetValue(p.BandId, out var praying) && praying.RelationTo(struck.Id) < -0.3f)
                Answered(p, praying, $"{Ids.Snake(power).Replace('_', ' ')} fell upon the {struck.Name}");
    }

    void Answered(Prayer p, Band band, string how)
    {
        p.Answered = true; p.ResolvedDay = Day;
        Stats.Year.PrayersAnswered++;
        ShakeFaith(band, +0.25f);
        if (Living.TryGetValue(p.AgentId, out var priest)) Psychology.Praise(priest, Domain.Spiritual, 1f, Day);
        var god = PlayerGodOf(band.CultureId);
        Chronicle.Add(new WorldEvent(Day, EventCategory.Spiritual, "prayer_answered",
            $"The {band.Name} rejoice: they prayed for {Powers.Verb(p.Need)}, and {how}.", 0.6f, p.AgentId, band.Id));
        Chronicle.Moment(new LlmMoment(Day, "prayer_answered", p.AgentId, band.Id, new()
        {
            ["god"] = god?.Name ?? "the one above", ["need"] = Powers.Verb(p.Need), ["how"] = how,
        }));
    }

    void ShakeFaith(Band band, float delta)
    {
        if (PlayerGodOf(band.CultureId) is not { } god) return;
        foreach (int id in band.Members)
            if (Living.TryGetValue(id, out var m) && m.Memes.TryGetValue(god.Id, out var c))
            {
                m.Memes[god.Id] = Math.Clamp(c + delta * (0.5f + m.T(Trait.SelfTranscendence)), 0.06f, 1f);
                if (delta > 0) m.Feel(Emotion.Joy, 0.4f); else m.Feel(Emotion.Sadness, 0.15f);
            }
    }
}
