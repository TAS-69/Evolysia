using Evolysia.Narration;
using Evolysia.Sim;
using Godot;

namespace Evolysia.Game;

/// <summary>
/// Feeds the simulation's narratable moments to the local model (tools/llm_lab, `lab.py serve`)
/// and hands finished voices to the UI. Moments involving whoever the player is looking at jump
/// the queue; at high speeds most moments are skipped, which is the point: the model voices what
/// matters and what is watched.
/// </summary>
public partial class NarrationHost : Node
{
    public Narrator Narrator { get; private set; } = null!;
    public readonly List<Narration.Narration> Recent = new();
    public event Action<Narration.Narration>? Spoke;

    /// <summary>Set by Main each frame: who/what the player is focused on, and what's on screen.</summary>
    public int FocusAgentId = -1, FocusBandId = -1;
    public Func<int, bool>? IsBandVisible;

    LlamaServerBackend _backend = null!;
    Simulation? _sim;
    int _seen;

    public override void _Ready()
    {
        _backend = new LlamaServerBackend();
        Narrator = new Narrator(_backend);
    }

    /// <summary>New world (or fast-forward): start voicing from the latest few moments.</summary>
    public void Attach(Simulation sim, int catchUp = 10)
    {
        _sim = sim;
        Narrator = new Narrator(_backend);
        Recent.Clear();
        _seen = Math.Max(0, sim.Chronicle.Moments.Count - catchUp);
    }

    public override void _Process(double delta)
    {
        if (_sim == null) return;
        var moments = _sim.Chronicle.Moments;
        // At very high speed thousands of moments can arrive between frames; only the newest matter.
        if (moments.Count - _seen > 200) _seen = moments.Count - 200;
        for (; _seen < moments.Count; _seen++)
        {
            var m = moments[_seen];
            var request = Prompts.Build(_sim, _seen, m);
            if (request == null) continue;
            float bonus = (m.AgentId == FocusAgentId ? 4f : 0f) + (m.BandId == FocusBandId ? 2f : 0f)
                          + (IsBandVisible?.Invoke(m.BandId) == true ? 1f : 0f);
            Narrator.Offer(request, bonus);
        }
        Narrator.Pump();
        while (Narrator.TryTake(out var n))
        {
            if (Prompts.DecisionChoice(n) is { } choice && !_sim.Decide(choice.DecisionId, choice.Choice))
                continue;   // instinct already decided; a voice for the road not taken would contradict history
            if (n.Moment.Kind == "decision" && Prompts.DecisionChoice(n) == null) continue;   // invalid choice: instinct will decide
            Recent.Add(n);
            if (Recent.Count > 40) Recent.RemoveAt(0);
            Spoke?.Invoke(n);
        }
    }

    /// <summary>Launch the model servers via the LLM lab (same config the player chose there).</summary>
    public void StartModels()
    {
        string root = ProjectSettings.GlobalizePath("res://").TrimEnd('/', '\\').GetBaseDir();
        string lab = System.IO.Path.Combine(root, "tools", "llm_lab", "lab.py");
        int pid = OS.CreateProcess("python", new[] { lab, "serve" });
        GD.Print(pid > 0 ? $"Starting local models (pid {pid}) via {lab}" : $"Could not start python for {lab}");
    }
}
