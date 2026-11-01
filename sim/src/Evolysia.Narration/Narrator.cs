using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Evolysia.Sim;

namespace Evolysia.Narration;

public sealed record Narration(int MomentIndex, LlmMoment Moment, string AgentName, IReadOnlyDictionary<string, string> Fields, double LatencyMs)
{
    public string Get(string key) => Fields.TryGetValue(key, out var v) ? v.Trim() : "";

    public string Quote => new[] { Get("says"), Get("speech"), Get("epitaph"), Get("thought"), Get("myth") }
        .FirstOrDefault(s => s.Length > 0) ?? "";
}

public readonly record struct NarratorStatus(bool Online, string Backend, int Queued, int Running, int Done, int Dropped, int Failed, double AvgLatencyMs);

public sealed class Narrator : IDisposable
{
    readonly INarrationBackend _backend;
    readonly int _maxConcurrent, _maxQueue;
    readonly List<(NarrationRequest req, float priority, long enqueued)> _queue = new();
    readonly ConcurrentQueue<Narration> _results = new();
    readonly Dictionary<int, List<Narration>> _byAgent = new();
    readonly Stopwatch _clock = Stopwatch.StartNew();
    readonly CancellationTokenSource _cancel = new();
    readonly CancellationToken _ct;
    int _running, _done, _dropped, _failed;
    double _latencySum;
    long _lastHealthCheck = -1_000_000;
    bool _online, _checking;

    public float StalenessPerSecond = 0.5f;

    public Narrator(INarrationBackend backend, int maxConcurrent = 3, int maxQueue = 16)
    {
        _backend = backend;
        _ct = _cancel.Token;
        _maxConcurrent = maxConcurrent;
        _maxQueue = maxQueue;
    }

    public NarratorStatus Status => new(_online, _backend.Describe(), _queue.Count, _running, _done, _dropped, _failed,
        _done == 0 ? 0 : _latencySum / _done);

    public void Offer(NarrationRequest request, float bonus = 0f)
    {
        if (_ct.IsCancellationRequested) return;
        _queue.Add((request, request.BasePriority + bonus, _clock.ElapsedMilliseconds));
        while (_queue.Count > _maxQueue)
        {
            int worst = 0;
            for (int i = 1; i < _queue.Count; i++) if (Effective(_queue[i]) < Effective(_queue[worst])) worst = i;
            _queue.RemoveAt(worst);
            _dropped++;
        }
    }

    float Effective((NarrationRequest req, float priority, long enqueued) e) =>
        e.priority - (_clock.ElapsedMilliseconds - e.enqueued) / 1000f * StalenessPerSecond;

    public void Pump()
    {
        if (_ct.IsCancellationRequested) return;
        long now = _clock.ElapsedMilliseconds;
        if (!_checking && now - _lastHealthCheck > (_online ? 30000 : 5000))
        {
            _checking = true;
            _lastHealthCheck = now;
            _ = Task.Run(async () =>
            {
                try { _online = await _backend.HealthyAsync(_ct); }
                catch (Exception e) when (e is HttpRequestException or OperationCanceledException or ObjectDisposedException) { _online = false; }
                finally { _checking = false; }
            });
        }
        while (_online && _running < _maxConcurrent && _queue.Count > 0)
        {
            int best = 0;
            for (int i = 1; i < _queue.Count; i++) if (Effective(_queue[i]) > Effective(_queue[best])) best = i;
            var request = _queue[best].req;
            _queue.RemoveAt(best);
            Interlocked.Increment(ref _running);
            _ = Task.Run(() => RunAsync(request));
        }
    }

    async Task RunAsync(NarrationRequest request)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var n = Parse(request, await _backend.CompleteAsync(request, _ct), sw.Elapsed.TotalMilliseconds);
            if (n != null) _results.Enqueue(n);
            else Interlocked.Increment(ref _failed);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            _online = false;
            Interlocked.Increment(ref _failed);
        }
        catch (Exception)
        {
            Interlocked.Increment(ref _failed);
        }
        finally { Interlocked.Decrement(ref _running); }
    }

    public bool TryTake(out Narration narration)
    {
        if (!_results.TryDequeue(out narration!)) return false;
        _done++;
        _latencySum += narration.LatencyMs;
        if (!_byAgent.TryGetValue(narration.Moment.AgentId, out var list)) _byAgent[narration.Moment.AgentId] = list = new List<Narration>();
        list.Add(narration);
        if (list.Count > 12) list.RemoveAt(0);
        return true;
    }

    public IReadOnlyList<Narration> ForAgent(int agentId) =>
        _byAgent.TryGetValue(agentId, out var list) ? list : Array.Empty<Narration>();

    public void Dispose() { _cancel.Cancel(); _queue.Clear(); _cancel.Dispose(); }

    public async Task<Narration?> NarrateNowAsync(NarrationRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var n = Parse(request, await _backend.CompleteAsync(request, ct), sw.Elapsed.TotalMilliseconds);
        if (n == null) _failed++; else { _done++; _latencySum += n.LatencyMs; }
        return n;
    }

    public static Narration? Parse(NarrationRequest request, string json, double latencyMs)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var fields = new Dictionary<string, string>();
            foreach (var p in doc.RootElement.EnumerateObject())
                if (p.Value.ValueKind == JsonValueKind.String) fields[p.Name] = Clean(p.Value.GetString()!);
            var required = request.Schema["required"]!.AsArray().Select(n => n!.GetValue<string>());
            return required.All(fields.ContainsKey) ? new Narration(request.MomentIndex, request.Moment, request.AgentName, fields, latencyMs) : null;
        }
        catch (JsonException) { return null; }
    }

    public static string Clean(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        foreach (char ch in text)
            if (!(ch is >= '⺀' and <= '鿿' or >= '가' and <= '힯' or >= '＀' and <= '￯')) sb.Append(ch);
        var s = sb.ToString().Trim();
        if (!s.Contains(' ')) return s;
        if (s.Length == 0 || ".!?\"'”’…)".Contains(s[^1])) return s;
        int end = s.LastIndexOfAny(new[] { '.', '!', '?' });
        return end > s.Length / 3 ? s[..(end + 1)] : s + "…";
    }
}
