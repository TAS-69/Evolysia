using Evolysia.Narration;
using Evolysia.Sim;
using Godot;

namespace Evolysia.Game;

public partial class NarrationHost : Node
{
    public Narrator Narrator { get; private set; } = null!;
    public CognitionDirector Director { get; private set; } = null!;
    public readonly List<Narration.Narration> Recent = new();
    public event Action<Narration.Narration>? Spoke;

    public int FocusAgentId = -1, FocusBandId = -1;
    public Func<int, bool>? IsBandVisible;

    LlamaServerBackend _backend = null!;
    Simulation? _sim;
    int _seen;
    public string Endpoint => _backend.Main.ToString();
    public string Model => _backend.ModelName;
    public bool PlanningEnabled;

    public override void _Ready()
    {
        var settings = new ConfigFile(); settings.Load("user://models.cfg");
        _backend = new LlamaServerBackend((string)settings.GetValue("connection", "endpoint", "http://127.0.0.1:8081"),
            null, (string)settings.GetValue("connection", "model", "main"));
        Narrator = new Narrator(_backend);
        Director = new CognitionDirector(_backend);
    }

    public void Attach(Simulation sim, int catchUp = 10)
    {
        _sim = sim;
        Director?.Dispose();
        Narrator?.Dispose();
        Director = new CognitionDirector(_backend);
        Narrator = new Narrator(_backend);
        Recent.Clear();
        _seen = Math.Max(0, sim.Chronicle.Moments.Count - catchUp);
    }

    public override void _Process(double delta)
    {
        Director.Pump(PlanningEnabled ? _sim : null);
        if (_sim == null || !PlanningEnabled) return;
        var moments = _sim.Chronicle.Moments;

        if (moments.Count - _seen > 200) _seen = moments.Count - 200;
        for (; _seen < moments.Count; _seen++)
        {
            var m = moments[_seen];
            if (m.Kind == "decision") continue;
            if (m.Kind == "conversation")
            {
                var fields = m.Facts.ToDictionary(x => x.Key, x => x.Value);
                var voice = new Narration.Narration(_seen, m, _sim.Find(m.AgentId)?.Name ?? "", fields, 0);
                Recent.Add(voice); if (Recent.Count > 40) Recent.RemoveAt(0);
                Spoke?.Invoke(voice);
                continue;
            }
            var request = Prompts.Build(_sim, _seen, m);
            if (request == null) continue;
            float bonus = (m.AgentId == FocusAgentId ? 4f : 0f) + (m.BandId == FocusBandId ? 2f : 0f)
                          + (IsBandVisible?.Invoke(m.BandId) == true ? 1f : 0f);
            Narrator.Offer(request, bonus);
        }
        if (Director.Ready && Director.InFlight == 0 && !_sim.WaitingForMinds) Narrator.Pump();
        while (Narrator.TryTake(out var n))
        {
            if (Prompts.DecisionChoice(n) is { } choice && !_sim.Decide(choice.DecisionId, choice.Choice))
                continue;
            if (n.Moment.Kind == "decision" && Prompts.DecisionChoice(n) == null) continue;
            Recent.Add(n);
            if (Recent.Count > 40) Recent.RemoveAt(0);
            Spoke?.Invoke(n);
        }
    }

    public void StartModels()
    {
        string root = System.IO.Path.GetDirectoryName(Art.Root)!;
        string lab = System.IO.Path.Combine(root, "tools", "llm_lab", "lab.py");
        int pid = OS.CreateProcess("python", new[] { lab, "bootstrap", "--helper", "off", "--embed", "off" });
        GD.Print(pid > 0 ? $"Starting local models (pid {pid}) via {lab}" : $"Could not start python for {lab}");
    }

    public bool Configure(string endpoint, string model)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || string.IsNullOrWhiteSpace(model)) return false;
        Director.Dispose(); Narrator.Dispose(); _backend.Dispose();
        _backend = new LlamaServerBackend(uri.ToString(), null, model.Trim());
        Director = new CognitionDirector(_backend); Narrator = new Narrator(_backend);
        var settings = new ConfigFile(); settings.SetValue("connection", "endpoint", uri.ToString()); settings.SetValue("connection", "model", model.Trim());
        settings.Save("user://models.cfg");
        return true;
    }

    public override void _ExitTree()
    {
        Director.Dispose();
        Narrator.Dispose();
        _backend.Dispose();
    }
}
