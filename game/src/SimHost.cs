using Evolysia.Sim;
using Godot;

namespace Evolysia.Game;

/// <summary>
/// Owns the simulation and advances it in real time. Days are stepped on the main thread within
/// a per-frame time budget, so rendering stays smooth; if the world grows too big for the chosen
/// speed, the backlog is dropped rather than letting the game stall.
/// </summary>
public partial class SimHost : Node
{
    public Simulation? Sim { get; private set; }
    public static readonly float[] Speeds = { 0f, 2f, 10f, 60f, 360f };
    public int SpeedIndex { get; set; } = 2;
    public bool Paused => SpeedIndex == 0;
    public float DaysPerSecond => Speeds[SpeedIndex];

    /// <summary>Wall time per simulated day (ms), smoothed, for the HUD.</summary>
    public double MsPerDay { get; private set; }

    const long FrameBudgetUsec = 20000;

    /// <summary>Asked before each day: should time wait (e.g. for a mind to finish deciding)?</summary>
    public Func<bool>? ShouldHold;
    double _backlog;

    public void NewWorld(ulong seed)
    {
        // Each day runs across cores internally (identical results for any thread count); leave
        // two cores for rendering and the UI.
        Sim = new Simulation(new SimConfig { Seed = seed, Threads = Math.Max(1, System.Environment.ProcessorCount - 2) });
        _backlog = 0;
    }

    public override void _Process(double delta)
    {
        if (Sim == null || Paused || Sim.Living.Count == 0) return;
        _backlog = Math.Min(_backlog + delta * DaysPerSecond, Math.Max(2.0, DaysPerSecond * 0.25));
        ulong start = Time.GetTicksUsec();
        int steps = 0;
        while (_backlog >= 1.0 && (long)(Time.GetTicksUsec() - start) < FrameBudgetUsec)
        {
            if (ShouldHold?.Invoke() == true) { _backlog = Math.Min(_backlog, 1.0); break; }
            Sim.Step();
            _backlog -= 1.0;
            steps++;
        }
        if (steps > 0) MsPerDay = MsPerDay * 0.9 + 0.1 * ((Time.GetTicksUsec() - start) / 1000.0 / steps);
    }

    /// <summary>Run ahead synchronously (used for --days and screenshot mode).</summary>
    public void FastForward(int days)
    {
        if (Sim == null) return;
        for (int d = 0; d < days && Sim.Living.Count > 0; d++) Sim.Step();
    }
}
