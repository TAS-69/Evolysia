using Evolysia.Sim;
using Godot;

namespace Evolysia.Game;

public partial class SimHost : Node
{
    public Simulation? Sim { get; private set; }

    public static readonly float[] Speeds = { 0f, 12f, 24f, 36f, 60f, 120f, 300f };
    public static readonly string[] SpeedNames = { "II", "1×", "2×", "3×", "5×", "10×", "25×" };
    public int SpeedIndex { get; set; } = 1;
    public bool Paused => SpeedIndex == 0;
    public float DaysPerSecond => Speeds[SpeedIndex];

    public double MsPerDay { get; private set; }

    public double ActualDaysPerSecond { get; private set; }

    static readonly long[] FrameBudgetUsec = { 0, 20000, 20000, 22000, 28000, 36000, 50000 };

    public Func<bool>? ShouldHold;
    double _backlog;

    public void NewWorld(SimConfig config)
    {

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

    public void FastForward(int days)
    {
        if (Sim == null) return;
        for (int d = 0; d < days && Sim.Living.Count > 0; d++) Sim.Step();
    }
}
