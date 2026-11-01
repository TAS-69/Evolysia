using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;
using Evolysia.Sim.Economy;
using Evolysia.Sim.Psych;
using Evolysia.Sim.Society;

namespace Evolysia.Sim;

public enum MessageMedium : byte { Ground, Stone, Vision }

public enum MessageReading : byte
{
    Scripture,
    Vision,
    Command,
    Dismissed,
    Dread,
}

public sealed class Inscription
{
    public int Id, Cell, Day, StructureId = -1;
    public string Text = "";
    public MessageMedium Medium;
    public int FadesDay;
    public readonly List<(int Day, int BandId, MessageReading Reading, bool Understood)> Readings = new();
    public bool Visible(int day) => Medium != MessageMedium.Vision && day < FadesDay;
}

public sealed partial class Simulation
{
    public const int MessageCooldownDays = 250 * SimConfig.DaysPerYear;
    public readonly List<Inscription> Inscriptions = new();

    public int NextMessageDay => _nextMessageDay ?? Config.FirstMessageYear * SimConfig.DaysPerYear;
    int? _nextMessageDay;
    public bool CanMessage => Day >= NextMessageDay;
    int _nextInscription = 1;

    void LeaveMessage(GodAction a)
    {
        if (!CanMessage || string.IsNullOrWhiteSpace(a.Words)) return;
        _nextMessageDay = Day + MessageCooldownDays;
        var w = World;
        string text = a.Words.Trim();
        if (text.Length > 280) text = text[..280];
        var ins = new Inscription
        {
            Id = _nextInscription++, Cell = a.Cell, Day = Day, Text = text, Medium = a.Medium,
            FadesDay = a.Medium == MessageMedium.Ground ? Day + 60 * SimConfig.DaysPerYear : int.MaxValue,
        };
        Inscriptions.Add(ins);
        if (a.Medium == MessageMedium.Stone && Structures_Get("standing_stone") is { } stone && w.IsDryLand(a.Cell))
        {

            if (w.StructureAt[a.Cell] != 0 && Structures.TryGetValue(w.StructureAt[a.Cell], out var old)) Remove(old);
            var s = new Structure { Id = _nextStructure++, Def = stone.Index, Cell = a.Cell, BandId = -1, State = StructureState.Complete, BuiltDay = Day, PlannedDay = Day };
            Structures[s.Id] = s;
            w.StructureAt[a.Cell] = s.Id;
            ins.StructureId = s.Id;
        }
        Chronicle.Add(new WorldEvent(Day, EventCategory.Spiritual, "message",
            $"You leave words {(a.Medium == MessageMedium.Vision ? "in a vision" : a.Medium == MessageMedium.Stone ? "carved in stone" : "written on the ground")} at {w.X(a.Cell)},{w.Y(a.Cell)}: \"{text}\"", 0.9f));
        if (a.Medium == MessageMedium.Vision)
            foreach (var band in BandsNear(a.Cell, World.Cells(1.2f))) ReadMessage(band, ins, vision: true);
        else ReadersNearby(ins);
    }

    static StructureDef? Structures_Get(string id) => Economy.Structures.Get(id);

    void MessageUpkeep()
    {
        foreach (var ins in Inscriptions)
        {
            if (!ins.Visible(Day)) continue;
            if (ins.StructureId >= 0 && !Structures.ContainsKey(ins.StructureId)) { ins.FadesDay = Day; continue; }
            ReadersNearby(ins);
        }
    }

    void ReadersNearby(Inscription ins)
    {
        foreach (var band in BandsNear(ins.Cell, World.Cells(0.8f)))
            if (!ins.Readings.Any(r => r.BandId == band.Id)) ReadMessage(band, ins, vision: false);
    }

    void ReadMessage(Band band, Inscription ins, bool vision)
    {
        bool literate = vision || BandKnows(band, TechId.Writing);
        var counts = new int[5];
        foreach (int id in band.Members)
        {
            if (!Living.TryGetValue(id, out var m) || m.IsChild(Day)) continue;
            var r = Interpret(m, band, literate);
            counts[(int)r]++;
            Psychology.Appraise(m, new Appraisal(EventCategory.Spiritual, "read_message", r is MessageReading.Dread ? -0.5f : 0.6f, 0.8f,
                Agency.Unknown, Detail: literate ? ins.Text : "strange marks"), Day);
            m.Remember(new LifeEvent(Day, EventCategory.Spiritual, "message", 0.9f, -1, literate ? ins.Text : "marks no one could read"));
            if (r == MessageReading.Scripture) m.Feel(Emotion.Surprise, 0.6f);
            if (r == MessageReading.Dread) Fear.Scare(m, Stimulus.Lightning, 0.3f, Day);
        }
        band.Awe += vision ? 1.5f : 0.8f;
        var leader = Living.GetValueOrDefault(band.LeaderId);
        var instinct = leader != null ? Interpret(leader, band, literate) : (MessageReading)Array.IndexOf(counts, counts.Max());
        if ((DeferDecisions || Config.RequireModel) && leader != null)
        {
            var options = new[] { instinct }.Concat(Enum.GetValues<MessageReading>().Where(r => r != instinct)
                .OrderByDescending(r => counts[(int)r])).Where(r => literate || r != MessageReading.Command).Take(Config.RequireModel ? 5 : 3).Select(r => Ids.Snake(r)).ToArray();
            var d = new Decision
            {
                Id = _nextDecision++, Day = Day, Deadline = Day + DecisionWindowDays, AgentId = leader.Id, BandId = band.Id,
                Kind = DecisionKind.Message, Options = options, Descriptions = options.Select(o => DecisionText.Message(o, literate)).ToArray(),
                InstinctFavourite = Ids.Snake(instinct), MessageId = ins.Id,
            };
            Decisions.Add(d);
            Chronicle.Moment(new LlmMoment(Day, "decision", leader.Id, band.Id, DecisionFacts(d, new()
            {
                ["trigger"] = "message", ["message"] = literate ? ins.Text : "", ["medium"] = Ids.Snake(ins.Medium),
                ["readers"] = string.Join(",", Enum.GetValues<MessageReading>().Select(r => $"{Ids.Snake(r)}:{counts[(int)r]}")),
            })));
            ins.Readings.Add((Day, band.Id, instinct, literate));
            return;
        }
        ActOnMessage(band, ins, instinct, literate);
    }

    MessageReading Interpret(Agent a, Band band, bool literate)
    {
        float st = a.T(Trait.SelfTranscendence), rd = a.T(Trait.RewardDependence), ha = a.T(Trait.HarmAvoidance);
        float sd = a.T(Trait.SelfDirectedness), ns = a.T(Trait.NoveltySeeking), co = a.T(Trait.Cooperativeness);
        float faith = FaithInPlayer(band);
        Span<float> s = stackalloc float[5];
        s[(int)MessageReading.Scripture] = 1.2f * st + 0.4f * rd + faith + 0.4f * Held(a, MemeKind.SpiritBelief);
        s[(int)MessageReading.Vision] = 0.6f * ns + 0.4f * st + 0.2f * band.Awe;
        s[(int)MessageReading.Command] = literate ? 0.6f * rd + 0.4f * co + 0.4f * (1f - sd) + 0.6f * faith : float.NegativeInfinity;
        s[(int)MessageReading.Dismissed] = 0.7f * sd + 0.8f * (1f - st) - 0.6f * faith - 0.3f;
        s[(int)MessageReading.Dread] = 0.9f * ha + 0.3f * a.Emo(Emotion.Fear) - 0.4f * faith - 0.2f;
        return (MessageReading)Rng.Softmax(s, 0.25f);
    }

    internal void ActOnMessage(Band band, Inscription ins, MessageReading reading, bool literate)
    {
        var w = World;
        var leader = Living.GetValueOrDefault(band.LeaderId);
        int i = ins.Readings.FindIndex(r => r.BandId == band.Id);
        if (i >= 0) ins.Readings[i] = (Day, band.Id, reading, literate); else ins.Readings.Add((Day, band.Id, reading, literate));
        string words = literate ? $"\"{ins.Text}\"" : "the marks";
        string what = reading switch
        {
            MessageReading.Scripture => $"The {band.Name} take {words} as sacred and begin to retell them.",
            MessageReading.Vision => $"The {band.Name} marvel at {words}, a sign from somewhere beyond.",
            MessageReading.Command => $"The {band.Name} take {words} as a command.",
            MessageReading.Dismissed => $"The {band.Name} laugh off {words}.",
            _ => $"The {band.Name} fear {words} and shun the place.",
        };
        switch (reading)
        {
            case MessageReading.Scripture when leader != null:
                Scripture(band, leader, literate ? $"the Words: {Short(ins.Text)}" : "the Marks");
                band.DivineFavor += 1f;
                break;
            case MessageReading.Vision:
                band.Awe += 1f;
                foreach (int id in band.Members) if (Living.TryGetValue(id, out var m)) m.Feel(Emotion.Joy, 0.3f);
                break;
            case MessageReading.Command:
                if (!Config.RequireModel) what += " " + Obey(band, ins);
                break;
            case MessageReading.Dismissed:
                band.DivineFavor -= 0.5f;
                break;
            case MessageReading.Dread:
                band.PlaceDread[(int)StimulusCategory.Sky] += 1.5f;
                if (w.Distance(band.Cell, ins.Cell) <= World.Cells(1)) band.LastMoveDay = Day - 1000;
                break;
        }
        Chronicle.Add(new WorldEvent(Day, EventCategory.Spiritual, "message_read", what, 0.7f, band.LeaderId, band.Id));
        if (Config.RequireModel) Reconsider(band.Id);
        if (leader != null)
            Chronicle.Moment(new LlmMoment(Day, "message_read", leader.Id, band.Id, new()
            {
                ["message"] = literate ? ins.Text : "", ["reading"] = Ids.Snake(reading), ["medium"] = Ids.Snake(ins.Medium),
            }));
    }

    static string Short(string text) => text.Length <= 40 ? text : text[..40].TrimEnd() + "...";

    string Obey(Band band, Inscription ins)
    {
        string t = ins.Text.ToLowerInvariant();
        bool Has(params string[] words) => words.Any(word => t.Contains(word));
        var leader = Living.GetValueOrDefault(band.LeaderId);
        if (Has("peace", "love", "forgive", "friend", "kind", "share", "help"))
        {
            foreach (var other in Bands.Values) if (other != band && World.Distance(other.Cell, band.Cell) <= World.Cells(3)) band.Relations[other.Id] = MathF.Min(1f, band.RelationTo(other.Id) + 0.5f);
            if (leader != null) NewMeme(MemeKind.SharingNorm, StimulusCategory.Outsiders, "sharing as the Words ask", leader, band);
            return "They seek peace with their neighbours and share what they have.";
        }
        if (Has("war", "fight", "kill", "destroy", "conquer", "attack"))
        {
            var target = Bands.Values.Where(o => o != band && World.Distance(o.Cell, band.Cell) <= World.Cells(3)).OrderBy(o => band.RelationTo(o.Id)).FirstOrDefault();
            if (target != null) { Raid(band, target, false); return $"They fall upon the {target.Name}."; }
            return "But there is no one near to fight.";
        }
        if (Has("stay", "settle", "farm", "plant", "home") && BandKnows(band, TechId.Agriculture)) { band.Sedentary = true; return "They settle where they are and plant fields."; }
        if (Has("go", "leave", "travel", "journey", "wander", "north", "south", "east", "west", "find"))
        {
            band.Sedentary = false;
            ForceMigrate(band, World.Cells(4));
            return "They pack up and set out.";
        }
        if (Has("sail", "sea", "cross", "boat", "ship") && leader != null && BestShore(band, leader) is { } shore) { Sail(band, leader, null, shore.cell); return "They take to the sea."; }
        if (Has("build", "temple", "altar", "stone", "shrine") && Economy.Structures.Get("altar") is { } altar && Site(band, altar) is { } cell)
        { Place(band, altar, cell); return "They begin raising an altar."; }
        if (Has("learn", "know", "think", "make", "invent", "teach"))
        {
            foreach (int id in band.Members) if (Living.TryGetValue(id, out var m)) m.InspiredUntil = Day + 2 * SimConfig.DaysPerYear;
            return "A restless curiosity takes hold of them.";
        }
        if (Has("pray", "worship", "praise", "honour", "honor") && leader != null)
        {
            NewMeme(MemeKind.Ritual, StimulusCategory.Sky, "the rite of the Words", leader, band);
            return "They make a rite of it.";
        }
        if (leader != null) Scripture(band, leader, $"the Words: {Short(ins.Text)}");
        return "Not knowing how to obey, they keep the words as holy.";
    }

    void Scripture(Band band, Agent leader, string name)
    {
        var god = PlayerGodOf(band.CultureId);
        var meme = NewMeme(MemeKind.SpiritBelief, StimulusCategory.Sky, name, leader, band);
        meme.CultureId = band.CultureId;
        meme.IsPlayerGod = god == null;
        foreach (int id in band.Members)
            if (Living.TryGetValue(id, out var m) && !m.IsChild(Day))
            {
                m.Memes[meme.Id] = MathF.Max(m.Memes.GetValueOrDefault(meme.Id), 0.6f);
                if (god != null) m.Memes[god.Id] = MathF.Min(1f, m.Memes.GetValueOrDefault(god.Id) + 0.2f);
            }
    }
}
