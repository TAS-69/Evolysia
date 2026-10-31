using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Evolysia.Narration;

public interface INarrationBackend
{

    Task<string> CompleteAsync(NarrationRequest request, CancellationToken ct);
    Task<bool> HealthyAsync(CancellationToken ct);
    string Describe();
}

public sealed class LlamaServerBackend : INarrationBackend, IDisposable
{
    readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(90) };
    public Uri Main { get; }
    public Uri? Helper { get; }
    bool _helperUp;

    public LlamaServerBackend(string mainUrl = "http://127.0.0.1:8081", string? helperUrl = "http://127.0.0.1:8082")
    {
        Main = new Uri(mainUrl);
        Helper = helperUrl == null ? null : new Uri(helperUrl);
    }

    public string Describe() => $"main {Main.Authority}" + (_helperUp ? $", helper {Helper!.Authority}" : "");

    public async Task<bool> HealthyAsync(CancellationToken ct)
    {
        bool main = await Ping(Main, ct);
        _helperUp = Helper != null && await Ping(Helper, ct);
        return main;
    }

    async Task<bool> Ping(Uri baseUri, CancellationToken ct)
    {
        try
        {
            using var r = await _http.GetAsync(new Uri(baseUri, "/health"), ct);
            return r.IsSuccessStatusCode;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { return false; }
    }

    public async Task<string> CompleteAsync(NarrationRequest r, CancellationToken ct)
    {
        var target = r.Role == ModelRole.Helper && _helperUp ? Helper! : Main;
        var body = new JsonObject
        {
            ["model"] = r.Role == ModelRole.Helper ? "helper" : "main",
            ["messages"] = new JsonArray(
                new JsonObject { ["role"] = "system", ["content"] = r.System },
                new JsonObject { ["role"] = "user", ["content"] = r.User }),
            ["temperature"] = r.Temperature,
            ["max_tokens"] = r.MaxTokens,
            ["chat_template_kwargs"] = new JsonObject { ["enable_thinking"] = false },
            ["response_format"] = new JsonObject
            {
                ["type"] = "json_schema",
                ["json_schema"] = new JsonObject { ["name"] = "out", ["strict"] = true, ["schema"] = r.Schema.DeepClone() },
            },
        };
        using var response = await _http.PostAsJsonAsync(new Uri(target, "/v1/chat/completions"), body, ct);
        response.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
    }

    public void Dispose() => _http.Dispose();
}
