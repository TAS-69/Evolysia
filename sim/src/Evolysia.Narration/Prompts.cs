using System.Text.Json.Nodes;
using Evolysia.Sim;
using Evolysia.Sim.Agents;
using Evolysia.Sim.Core;

namespace Evolysia.Narration;

public enum ModelRole { Main, Helper }

/// <summary>A prompt ready to send, snapshotted from simulation state at the moment it happened.</summary>
public sealed record NarrationRequest(
    int MomentIndex, LlmMoment Moment, string AgentName, ModelRole Role,
    string System, string User, JsonObject Schema, int MaxTokens, float Temperature, float BasePriority);

/// <summary>
/// One template per kind of moment. The simulation has already decided what happened; the model
/// only gives it a voice, so every prompt states the outcome as fact and asks for words, not choices.
/// </summary>
public static class Prompts
{
    // Same system prompt the LLM lab evaluated (tools/llm_lab/lablib/evals.py SYSTEM_MAIN), plus voice rules.
    public const string SystemMain =
        "You voice one person living in a harsh early-human world simulation. Stay fully in character. " +
        "People here can be selfish, cowardly, cruel, dishonest, stubborn, superstitious or foolish: play the " +
        "character exactly as described, not as a helpful or polite assistant. Their personality should drive " +
        "the words. They know nothing of writing, metal (unless stated), or any modern idea. " +
        "Use plain, concrete, earthy English. Finish every sentence. Reply only with the requested JSON.";

    // For choices: the exact framing tools/llm_lab evaluated (personality drives the choice).
    public const string SystemChoice =
        "You voice one person living in a harsh early-human world simulation. Stay fully in character. " +
        "People here can be selfish, cowardly, cruel, dishonest, stubborn, superstitious or foolish: play the " +
        "character exactly as described, not as a helpful or polite assistant. Their personality should drive " +
        "the choice. Use plain, concrete, earthy English. Reply only with the requested JSON.";

    public const string SystemHelper =
        "You write short lines for a world simulation's chronicle. Plain, earthy English; no modern ideas; finish every sentence. Reply only with the requested JSON.";

    static readonly Dictionary<string, float> KindPriority = new()
    {
        // A choice waiting on a mind is time-critical: instinct takes over at the deadline.
        ["decision"] = 11f,
        // Moments the player caused come first: they are watching for the response.
        ["dream"] = 12f, ["prayer_answered"] = 10f, ["revelation"] = 10f, ["prayer"] = 8f,
        ["schism"] = 9f, ["myth"] = 8f, ["voyage"] = 9f, ["killing"] = 7f, ["invention"] = 6f, ["pivot"] = 5f,
        ["notable_death"] = 4f, ["fear_overcome"] = 2f,
    };

    public static float Priority(string kind) => KindPriority.GetValueOrDefault(kind, 1f);

    static string F(LlmMoment m, string key) => m.Facts.GetValueOrDefault(key, "");

    static string ChoiceText(string choice) => choice switch
    {
        "conform" => "give it up and never try such things again",
        "double_down" => "keep at it and prove them all wrong",
        "withdraw" => "pull away from everyone and keep to themself",
        "resent" => "say nothing, but hold a grudge against those who laughed",
        "leave" => "leave the band for good",
        _ => choice,
    };

    static string UrgeText(string urge) => urge switch
    {
        "worship" => "worship this power above all others",
        "leave" => "leave this place",
        "peace" => "make peace and let go of anger",
        "seek" => "seek out new things and make what has never been made",
        "courage" => "fear nothing",
        _ => "love more deeply",
    };

    /// <summary>The player's own words go into a prompt: keep them short and unable to break the frame.</summary>
    public static string SanitizeWords(string words)
    {
        var clean = new string(words.Where(ch => !char.IsControl(ch) && ch is not ('{' or '}' or '"' or '`')).ToArray()).Trim();
        return clean.Length > 200 ? clean[..200] : clean;
    }

    /// <summary>If this narration answered a decision, which option did the mind pick?</summary>
    public static (int DecisionId, string Choice)? DecisionChoice(Narration n)
    {
        if (n.Moment.Kind != "decision" || !int.TryParse(n.Moment.Facts.GetValueOrDefault("decision_id"), out int id)) return null;
        string choice = n.Get("choice");
        var options = n.Moment.Facts.GetValueOrDefault("options", "").Split(',');
        return options.Contains(choice) ? (id, choice) : null;
    }

    static NarrationRequest? BuildDecision(Simulation sim, int index, LlmMoment m, Agent a, string sheet, string band)
    {
        string name = a.Name;
        var ids = F(m, "options").Split(',');
        var descriptions = F(m, "descriptions").Split('|');
        if (ids.Length != descriptions.Length || ids.Length < 2) return null;
        string situation = F(m, "trigger") switch
        {
            "courtship" => $"{name} offered {F(m, "subject")} hoping for love, and was turned down in front of others.",
            "first_contact" => $"{name} leads the {band} ({F(m, "own_size")} people{(F(m, "hungry") == "True" ? ", and they are hungry" : "")}). " +
                               $"For the first time they have met strangers: the {F(m, "other_band")} of the {F(m, "other_culture")} people, " +
                               $"{F(m, "other_size")} strong, speaking a tongue no one understands.",
            _ => $"{name} showed the {band} their {F(m, "subject")}. Most of them scorned it." +
                 (F(m, "allies") is var al && al != "0" ? $" {al} sided with {name}." : " No one sided with them."),
        };
        // Shuffle option order (stable per decision) so position is not mistaken for preference.
        var order = Enumerable.Range(0, ids.Length).OrderBy(i => (i * 7919 + index * 104729) % 97).ToArray();
        string options = string.Join("\n", order.Select(i => $"- {ids[i]}: {descriptions[i]}"));
        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["thought"] = new JsonObject { ["type"] = "string", ["maxLength"] = 400 },
                ["choice"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray(order.Select(i => (JsonNode)JsonValue.Create(ids[i])!).ToArray()) },
                ["says"] = new JsonObject { ["type"] = "string", ["maxLength"] = 260 },
            },
            ["required"] = new JsonArray("thought", "choice", "says"),
            ["additionalProperties"] = false,
        };
        string user = $"{sheet}\n\nSituation: {situation}\n\nOptions:\n{options}\n\n" +
                      $"Decide what {name} does, as {name} would, not as a sensible or kind person would. " +
                      $"JSON fields: thought ({name}'s inner voice weighing it, first person, 1-2 sentences, leading to the choice), " +
                      $"choice (one option id), says (what {name} says aloud; it must fit the choice; may be empty).";
        return new NarrationRequest(index, m, name, ModelRole.Main, SystemChoice, user, schema, 300,
            0.6f + 0.5f * a.T(Trait.NoveltySeeking), Priority("decision"));
    }

    public static NarrationRequest? Build(Simulation sim, int index, LlmMoment m)
    {
        var a = sim.Find(m.AgentId);
        if (a == null) return null;
        string sheet = CharacterSheet.Build(sim, a);
        string name = a.Name;
        float temperature = 0.6f + 0.5f * a.T(Trait.NoveltySeeking);
        string band = sim.Bands.TryGetValue(m.BandId, out var b) ? b.Name : "band";
        if (m.Kind == "decision") return BuildDecision(sim, index, m, a, sheet, band);

        (string situation, JsonObject schema, string fields, ModelRole role, int tokens) = m.Kind switch
        {
            "pivot" when F(m, "trigger") == "courtship" => (
                $"{name} hoped for love and offered their {F(m, "subject")}, but was turned down. " +
                $"{name} has decided to {ChoiceText(F(m, "choice"))}.",
                Obj(("thought", 400), ("says", 260), ("lasting_belief", 220)),
                "thought (inner voice, first person, 1-2 sentences), says (what they say aloud, may be empty), lasting_belief (what they now believe about themself or love, first person, one sentence)",
                ModelRole.Main, 300),
            "pivot" => (
                $"{name} showed the {band} their {F(m, "subject")}. Most of them scorned it. " +
                $"{name} has decided to {ChoiceText(F(m, "choice"))}.",
                Obj(("thought", 400), ("says", 260), ("lasting_belief", 220)),
                "thought (inner voice, first person, 1-2 sentences), says (what they say aloud, may be empty), lasting_belief (what they now believe about themself or their people, first person, one sentence)",
                ModelRole.Main, 300),
            "myth" => (
                $"After the {F(m, "event")}, {name} has a vision that explains it: it was the doing of {F(m, "deity")}. " +
                $"About {float.Parse(F(m, "approval"), System.Globalization.CultureInfo.InvariantCulture) * 100:0}% of the {band} believe them.",
                Obj(("myth", 700), ("ritual", 300)),
                $"myth (the story {name} tells the band about {F(m, "deity")}, in their own voice, 2-3 sentences), ritual (the offering or practice it demands, one sentence)",
                ModelRole.Main, 380),
            "invention" => (
                $"{name} has just worked out {F(m, "tech").Replace('_', ' ')}{(F(m, "world_first") == "True" ? ", something no one in the world has ever done" : "")}. " +
                $"When shown to the {band}, it was {F(m, "verdict")}.",
                Obj(("thought", 400), ("says", 260), ("crowd", 220)),
                "thought (inner voice, 1-2 sentences), says (how they present it aloud), crowd (one line someone in the band says back)",
                ModelRole.Main, 300),
            "schism" => (
                $"{name} is leaving the {F(m, "old_band")} over the {F(m, "subject")}, taking {F(m, "followers")} followers " +
                $"to found a new band, the {F(m, "new_band")}{(F(m, "new_culture") == "True" ? ", a new people" : "")}.",
                Obj(("speech", 500), ("name_meaning", 160)),
                $"speech (the parting words {name} speaks to those staying behind, 2 sentences), name_meaning (what the name {F(m, "new_band")} means, a few words)",
                ModelRole.Main, 300),
            "fear_overcome" => (
                $"{name} has long been afraid of the {F(m, "stimulus").Replace('_', ' ')}. Today one came at them and, " +
                $"{(F(m, "allies") == "0" ? "alone" : $"with {F(m, "allies")} others near")}, they stood their ground and drove it off.",
                Obj(("thought", 400), ("says", 240)),
                "thought (inner voice right after, 1-2 sentences), says (what they shout or say, may be empty)",
                ModelRole.Main, 260),
            "killing" => (
                $"In anger, {name} has struck down {F(m, "victim")}, whom they had long held a grudge against.",
                Obj(("thought", 400), ("says", 240)),
                "thought (inner voice, 1-2 sentences), says (what they say over the body, may be empty)",
                ModelRole.Main, 260),
            "revelation" => (
                $"{name} has seen {F(m, "wonder")} and proclaims it the work of a great unseen power they call {F(m, "god")}. " +
                $"{(F(m, "kind") == "True" ? "It has shown them kindness." : "It has shown them its wrath.")} " +
                $"About {float.Parse(F(m, "approval"), System.Globalization.CultureInfo.InvariantCulture) * 100:0}% of the {band} believe.",
                Obj(("proclamation", 500), ("commandment", 220)),
                $"proclamation (what {name} declares to the band about {F(m, "god")}, 2 sentences), commandment (what {F(m, "god")} demands of the people, one sentence)",
                ModelRole.Main, 320),
            "dream" => (
                $"Last night {name} dreamt of {F(m, "god")}. " +
                (SanitizeWords(F(m, "words")) is { Length: > 0 } w ? $"In the dream a voice said: '{w}'. " : "No words were spoken, but the meaning was clear. ") +
                $"{name} took it as a call to {UrgeText(F(m, "urge"))}, and {F(m, "effect")}.",
                Obj(("dream", 500), ("says", 260), ("lasting_belief", 220)),
                $"dream (how {name} recounts the dream, first person, 2 sentences), says (what they tell the band on waking), lasting_belief (what they now believe, first person, one sentence)",
                ModelRole.Main, 340),
            "prayer" => (
                $"Hardship has come: the {band} need {F(m, "need")}. {name} prays aloud to {F(m, "god")}.",
                Obj(("prayer", 500), ("offering", 220)),
                $"prayer (the words {name} speaks, 2-3 sentences, addressed to {F(m, "god")}), offering (what they offer in return, one sentence)",
                ModelRole.Main, 300),
            "prayer_answered" => (
                $"{name} prayed to {F(m, "god")} for {F(m, "need")}, and {F(m, "how")}.",
                Obj(("thanks", 400), ("lasting_belief", 220)),
                "thanks (what they cry out, 1-2 sentences), lasting_belief (what they now believe about the one above, first person, one sentence)",
                ModelRole.Main, 260),
            "voyage" => (
                $"{name} has led {F(m, "sailed")} people {(F(m, "from_band") is var fb && fb.Length > 0 ? $"of the {fb} " : "")}across " +
                $"{F(m, "distance")} days of open sea{(F(m, "winter") == "True" ? " in the cold season" : "")} to {F(m, "land")}" +
                $"{(F(m, "first_landing") == "True" ? " where no human has ever walked" : "")}. " +
                (F(m, "lost") == "0" ? "Everyone made it." : $"{F(m, "lost")} were lost to the waves on the way."),
                Obj(("thought", 400), ("says", 260)),
                $"thought ({name}'s inner voice as they step onto the shore, 1-2 sentences), says (what they tell the others there)",
                ModelRole.Main, 260),
            "notable_death" => (
                $"{name} has died, aged {F(m, "age")}, of {F(m, "cause").Replace('_', ' ')}.{(F(m, "was_leader") == "True" ? $" They led the {band}." : "")}",
                Obj(("epitaph", 360)),
                $"epitaph (the words the {band} say to remember {name}, one or two sentences, no numbers or years)",
                ModelRole.Helper, 160),
            _ => default,
        };
        if (schema == null) return null;

        string user = $"{sheet}\n\nWhat happens (decided, do not change it): {situation}\n\nJSON fields: {fields}.";
        return new NarrationRequest(index, m, name, role, role == ModelRole.Main ? SystemMain : SystemHelper, user, schema,
            tokens, role == ModelRole.Main ? temperature : 0.7f, Priority(m.Kind));
    }

    static JsonObject Obj(params (string name, int maxLength)[] fields)
    {
        var props = new JsonObject();
        foreach (var (n, len) in fields) props[n] = new JsonObject { ["type"] = "string", ["maxLength"] = len };
        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = props,
            ["required"] = new JsonArray(fields.Select(f => (JsonNode)JsonValue.Create(f.name)!).ToArray()),
            ["additionalProperties"] = false,
        };
    }
}
