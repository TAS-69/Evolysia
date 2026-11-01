using System.Diagnostics;
using System.Text.Json;
using Evolysia.Sim;

namespace Evolysia.Narration;

public sealed class CognitionDirector : IDisposable
{
    readonly INarrationBackend _backend;
    readonly CancellationTokenSource _cancel = new();
    readonly Stopwatch _clock = Stopwatch.StartNew();
    readonly Dictionary<int, (Task<string> task, CancellationTokenSource cancel)> _plans = new();
    readonly Dictionary<int, (NarrationRequest request, Task<string> task)> _choices = new();
    readonly Dictionary<int, long> _retry = new();
    readonly Dictionary<int, string> _corrections = new();
    Task<bool>? _probe;
    long _nextProbe;
    public bool Ready { get; private set; }
    public string Status { get; private set; } = "Connecting to the model…";
    public int Accepted { get; private set; }
    public int Rejected { get; private set; }
    public int InFlight => _plans.Count + _choices.Count;
    public string LastError { get; private set; } = "";
    public string LastRejectedReply { get; private set; } = "";

    public CognitionDirector(INarrationBackend backend) => _backend = backend;

    async Task<bool> ProbeAsync()
    {
        try
        {
            var response = await _backend.CompleteAsync(CognitionPrompts.Probe(), _cancel.Token);
            using var doc = JsonDocument.Parse(response);
            return doc.RootElement.GetProperty("status").GetString() == "ready";
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        { return false; }
    }

    public void Pump(Simulation? sim)
    {
        if (_cancel.IsCancellationRequested) return;
        long now = _clock.ElapsedMilliseconds;
        if (_probe is { IsCompleted: true })
        {
            Ready = _probe.IsCompletedSuccessfully && _probe.Result;
            _probe = null;
            _nextProbe = now + (Ready ? 15000 : 3000);
            Status = Ready ? "Model ready. Plans and personal memories will shape this world." : "Model unavailable. Start or reconnect the model; the world is held.";
        }
        if (_probe == null && now >= _nextProbe) _probe = ProbeAsync();
        if (sim == null) return;
        sim.MindRequests();
        foreach (int id in _retry.Keys.Where(id => id > 0 ? !sim.IsCurrentRequest(id) : !sim.PendingDecisions.Any(d => d.Id == -id)).ToArray())
        { _retry.Remove(id); _corrections.Remove(id); }
        foreach (var (id, job) in _plans.Where(p => !sim.IsCurrentRequest(p.Key)).ToArray())
        {
            _plans.Remove(id); job.cancel.Cancel(); job.cancel.Dispose(); Observe(job.task);
            _retry.Remove(id); _corrections.Remove(id);
        }
        foreach (var (id, job) in _plans.Where(p => p.Value.task.IsCompleted).ToArray())
        {
            _plans.Remove(id);
            job.cancel.Dispose();
            try
            {
                string reply = job.task.GetAwaiter().GetResult();
                LastRejectedReply = reply;
                var plan = CognitionPrompts.Parse(reply);
                if (plan == null || plan.RequestId != id) Fail(id, "Reply used the wrong request ID.");
                else if (!sim.AcceptPlan(plan, out var error)) Fail(id, error);
                else { Accepted++; _retry.Remove(id); _corrections.Remove(id); LastError = ""; LastRejectedReply = ""; }
            }
            catch (Exception e) when (e is HttpRequestException or OperationCanceledException or JsonException or NotSupportedException or ArgumentException or InvalidOperationException)
            { Fail(id, e is JsonException ? "Reply did not match the required plan structure." : "Model request failed; retrying without advancing time."); if (e is HttpRequestException or OperationCanceledException) Ready = false; }
        }
        foreach (var (id, job) in _choices.Where(p => p.Value.task.IsCompleted).ToArray())
        {
            _choices.Remove(id);
            try
            {
                var n = Narrator.Parse(job.request, job.task.GetAwaiter().GetResult(), 0);
                if (n == null || Prompts.DecisionChoice(n) is not { } choice || !sim.Decide(choice.DecisionId, choice.Choice)) Fail(-id, "Pivotal decision was invalid; it remains pending.");
                else { Accepted++; _retry.Remove(-id); }
            }
            catch (Exception e) when (e is HttpRequestException or OperationCanceledException or JsonException)
            { Fail(-id, "Pivotal decision could not be completed; retrying."); if (e is HttpRequestException or OperationCanceledException) Ready = false; }
        }
        if (!Ready) return;
        foreach (var d in sim.PendingDecisions.OrderBy(d => d.Id).ToArray())
        {
            if (InFlight >= 2) break;
            if (_choices.ContainsKey(d.Id) || _retry.GetValueOrDefault(-d.Id) > now) continue;
            int index = sim.Chronicle.Moments.FindIndex(m => m.Kind == "decision" && m.Facts.GetValueOrDefault("decision_id") == d.Id.ToString());
            if (index < 0 || Prompts.Build(sim, index, sim.Chronicle.Moments[index]) is not { } prompt) continue;
            _choices[d.Id] = (prompt, _backend.CompleteAsync(prompt, _cancel.Token));
        }
        foreach (var r in sim.MindRequests())
        {
            if (InFlight >= 2) break;
            if (_plans.ContainsKey(r.Id) || _retry.GetValueOrDefault(r.Id) > now) continue;
            var prompt = CognitionPrompts.Plan(sim, r, _corrections.GetValueOrDefault(r.Id, ""));
            var cancel = CancellationTokenSource.CreateLinkedTokenSource(_cancel.Token);
            _plans[r.Id] = (_backend.CompleteAsync(prompt, cancel.Token), cancel);
        }
        Status = sim.WaitingForMinds ? $"Thinking · {InFlight} requests · {Accepted} plans and decisions accepted" : $"Minds active · {Accepted} accepted";
        if (LastError.Length > 0) Status += " · " + LastError;
    }

    void Fail(int id, string error)
    {
        Rejected++; LastError = error;
        _retry[id] = _clock.ElapsedMilliseconds + 2000;
        _corrections[id] = error;
    }

    public bool Hold(Simulation sim) => !Ready || sim.WaitingForMinds;
    static void Observe(Task task) => _ = task.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    public void Dispose()
    {
        _cancel.Cancel();
        foreach (var job in _plans.Values) { Observe(job.task); job.cancel.Dispose(); }
        foreach (var job in _choices.Values) Observe(job.task);
        if (_probe != null) Observe(_probe);
        _cancel.Dispose();
    }
}
