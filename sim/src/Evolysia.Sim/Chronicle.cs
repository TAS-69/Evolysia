using Evolysia.Sim.Core;

namespace Evolysia.Sim;

public sealed record WorldEvent(int Day, EventCategory Category, string Kind, string Text, float Salience, int AgentId = -1, int BandId = -1);

public sealed record LlmMoment(int Day, string Kind, int AgentId, int BandId, Dictionary<string, string> Facts);

public sealed class Chronicle
{
    public readonly List<WorldEvent> Events = new();
    public readonly List<LlmMoment> Moments = new();
    public readonly Dictionary<string, int> Counts = new();
    public readonly HashSet<string> Firsts = new();

    public float MinSalienceToKeep = 0.25f;

    public void Add(WorldEvent e)
    {
        Counts[e.Kind] = Counts.TryGetValue(e.Kind, out var c) ? c + 1 : 1;
        if (e.Salience >= MinSalienceToKeep) Events.Add(e);
    }

    public bool First(string key) => Firsts.Add(key);

    public void Moment(LlmMoment m) => Moments.Add(m);
}
