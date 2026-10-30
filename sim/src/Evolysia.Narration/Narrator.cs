using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Evolysia.Sim;

namespace Evolysia.Narration;

/// <summary>A moment given a voice.</summary>
public sealed record Narration(int MomentIndex, LlmMoment Moment, string AgentName, IReadOnlyDictionary<string, string> Fields, double LatencyMs)
{
    public string Get(string key) => Fields.TryGetValue(key, out var v) ? v.Trim() : "";

    /// <summary>The one line worth floating above the map.</summary>
    public string Quote => new[] { Get("says"), Get("speech"), Get("epitaph"), Get("thought"), Get("myth") }
        .FirstOrDefault(s => s.Length > 0) ?? "";
}

public readonly record struct NarratorStatus(bool Online, string Backend, int Queued, int Running, int Done, int Dropped, int Failed, double AvgLatencyMs);

/// <summary>
/// The model can narrate a few moments a second, but a fast-forwarded world produces hundreds,
/// so the narrator works like a newsroom: a small bounded queue, highest priority first (kind of
/// moment + what the player is looking at), stale stories lose priority and the least important
/// are dropped. Nothing here touches simulation state, so narration never changes what happens.
/// </summary>
public sealed class Narrator
{
    readonly INarrationBackend _backend;
    readonly int _maxConcurrent, _maxQueue;
    readonly List<(NarrationRequest req, float priority, long enqueued)> _queue = new();
    readonly ConcurrentQueue<Narration> _results = new();
    readonly Dictionary<int, List<Narration>> _byAgent = new();
    readonly Stopwatch _clock = Stopwatch.StartNew();
    int _running, _done, _dropped, _failed;
    double _latencySum;
    long _lastHealthCheck = -1_000_000;   // "long ago" (MinValue would overflow the subtraction)
    bool _online, _checking;

    /// <summary>Priority lost per second of waiting: an old story is worth less than a fresh one.</summary>
    public float StalenessPerSecond = 0.5f;

    public Narrator(INarrationBackend backend, int maxConcurrent = 3, int maxQueue = 16)
    {
        _backend = backend;
        _maxConcurrent = maxConcurrent;
        _maxQueue = maxQueue;
    }

    public NarratorStatus Status => new(_online, _backend.Describe(), _queue.Count, _running, _done, _dropped, _failed,
        _done == 0 ? 0 : _latencySum / _done);

    /// <summary>Main thread: offer a moment. Bonus: e.g. +3 if the player has that person selected.</summary>
    public void Offer(NarrationRequest request, float bonus = 0f)
    {
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

    /// <summary>Main thread, every frame: start work if there is capacity; re-check the server when offline.</summary>
    public void Pump()
    {
        long now = _clock.ElapsedMilliseconds;
        if (!_checking && now - _lastHealthCheck > (_online ? 30000 : 5000))
        {
            _checking = true;
            _lastHealthCheck = now;
            _ = Task.Run(async () =>
            {
                try { _online = await _backend.HealthyAsync(CancellationToken.None); }
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
            var n = Parse(request, await _backend.CompleteAsync(request, CancellationToken.None), sw.Elapsed.TotalMilliseconds);
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

    /// <summary>Main thread: collect finished narrations (also indexed by agent).</summary>
    public bool TryTake(out Narration narration)
    {
        if (!_results.TryDequeue(out narration!)) return false;
        _done++;
        _latencySum += narration.LatencyMs;
        if (!_byAgent.TryGetValue(narration.Moment.AgentId, out var list)) _byAgent[narration.Moment.AgentId] = list = new List<Narration>();
        list.Add(narration);
        return true;
    }

    public IReadOnlyList<Narration> ForAgent(int agentId) =>
        _byAgent.TryGetValue(agentId, out var list) ? list : Array.Empty<Narration>();

    /// <summary>Synchronous path for offline use (the runner): narrate one request now.</summary>
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

    /// <summary>
    /// Small models occasionally slip into another script (Qwen: stray CJK) or run out of room
    /// mid-sentence. Drop non-Latin script and trim a cut-off tail back to the last full sentence.
    /// </summary>
    public static string Clean(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        foreach (char ch in text)
            if (!(ch is >= '⺀' and <= '鿿' or >= '가' and <= '힯' or >= '＀' and <= '￯')) sb.Append(ch);
        var s = sb.ToString().Trim();
        if (!s.Contains(' ')) return s;   // ids and single words (e.g. a chosen option) are not prose
        if (s.Length == 0 || ".!?\"'”’…)".Contains(s[^1])) return s;
        int end = s.LastIndexOfAny(new[] { '.', '!', '?' });
        return end > s.Length / 3 ? s[..(end + 1)] : s + "…";
    }
}
