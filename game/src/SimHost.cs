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
    /// <summary>Days per second: paused, then 1x, 2x, 3x, 5x, 10x and 25x of 12 days a second (25x is about a year every 1.2 s).</summary>
    public static readonly float[] Speeds = { 0f, 12f, 24f, 36f, 60f, 120f, 300f };
    public static readonly string[] SpeedNames = { "II", "1×", "2×", "3×", "5×", "10×", "25×" };
    public int SpeedIndex { get; set; } = 1;
    public bool Paused => SpeedIndex == 0;
    public float DaysPerSecond => Speeds[SpeedIndex];

    /// <summary>Wall time per simulated day (ms), smoothed, for the HUD.</summary>
    public double MsPerDay { get; private set; }
    /// <summary>Days actually simulated per second, smoothed (big worlds may not keep up with the chosen speed).</summary>
    public double ActualDaysPerSecond { get; private set; }

    // How much of each frame the simulation may take: at the top speeds, frames may slow so time can run.
    static readonly long[] FrameBudgetUsec = { 0, 20000, 20000, 22000, 28000, 36000, 50000 };

    /// <summary>Asked before each day: should time wait (e.g. for a mind to finish deciding)?</summary>
    public Func<bool>? ShouldHold;
    double _backlog;

    public void NewWorld(SimConfig config)
    {
        // Each day runs across cores internally (identical results for any thread count); leave
        // two cores for rendering and the UI.
        config.Threads = Math.Max(1, System.Environment.ProcessorCount - 2);
        Sim = new Simulation(config);
        _backlog = 0;
    }

    public override void _Process(double delta)
    {
        if (Sim == null || Paused || Sim.Living.Count == 0) { ActualDaysPerSecond = 0; return; }
        _backlog = Math.Min(_backlog + delta * DaysPerSecond, Math.Max(2.0, DaysPerSecond * 0.25));
        ulong start = Time.GetTicksUsec();
        int steps = 0;
        while (_backlog >= 1.0 && (long)(Time.GetTicksUsec() - start) < FrameBudgetUsec[SpeedIndex])
        {
            if (ShouldHold?.Invoke() == true) { _backlog = Math.Min(_backlog, 1.0); break; }
            Sim.Step();
            _backlog -= 1.0;
            steps++;
        }
        if (steps > 0) MsPerDay = MsPerDay * 0.9 + 0.1 * ((Time.GetTicksUsec() - start) / 1000.0 / steps);
        if (delta > 0) ActualDaysPerSecond = ActualDaysPerSecond * 0.85 + 0.15 * (steps / delta);
    }

    /// <summary>Run ahead synchronously (used for --days and screenshot mode).</summary>
    public void FastForward(int days)
    {
        if (Sim == null) return;
        for (int d = 0; d < days && Sim.Living.Count > 0; d++) Sim.Step();
    }
}
