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
    public string ModelName { get; }
    bool _helperUp;

    public LlamaServerBackend(string mainUrl = "http://127.0.0.1:8081", string? helperUrl = "http://127.0.0.1:8082", string model = "main")
    {
        Main = new Uri(mainUrl);
        Helper = helperUrl == null ? null : new Uri(helperUrl);
        ModelName = model;
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
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(90)); ct = timeout.Token;
        var target = r.Role == ModelRole.Helper && _helperUp ? Helper! : Main;
        var body = new JsonObject
        {
            ["model"] = r.Role == ModelRole.Helper ? "helper" : ModelName,
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
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(target, "/v1/chat/completions")) { Content = JsonContent.Create(body) };
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[16384];
        int count;
        while ((count = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + count > 1048576) throw new JsonException("The model response exceeded the reply limit.");
            buffer.Write(chunk, 0, count);
        }
        using var doc = JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
        if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0 ||
            choices[0].ValueKind != JsonValueKind.Object || !choices[0].TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object ||
            !message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.String) throw new JsonException("The endpoint did not return a chat completion.");
        return content.GetString() ?? "";
    }

    public void Dispose() => _http.Dispose();
}
