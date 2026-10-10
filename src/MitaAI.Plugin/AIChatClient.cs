using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MitaAI.Plugin;

public sealed record ChatTurn(string Role, string Content);

public sealed record AIChatConfig(
    AIProvider Provider,
    string EndpointUrl,
    string Model,
    string ApiKey,
    string SystemPrompt,
    int MaxTokens = 100,
    float Temperature = 0.7f,
    float TimeoutSeconds = 20f
);

public sealed record AIChatResult(
    bool Success,
    string Content,
    string? ErrorMessage = null,
    long DurationMs = 0
);

public sealed class AIChatClient
{
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(60) // High overall timeout; per-request token controls actual timeout
    };

    private readonly List<ChatTurn> promptHistory = new();
    private readonly List<ChatTurn> sessionHistory = new();
    private readonly object historyLock = new();
    private const int MaxPromptHistoryTurns = 6;
    private const int MaxSessionHistoryTurns = 100;

    public IReadOnlyList<ChatTurn> History
    {
        get
        {
            lock (historyLock)
            {
                return promptHistory.ToList();
            }
        }
    }

    public IReadOnlyList<ChatTurn> SessionHistory
    {
        get
        {
            lock (historyLock)
            {
                return sessionHistory.ToList();
            }
        }
    }

    public void ResetPromptHistory()
    {
        lock (historyLock)
        {
            promptHistory.Clear();
        }
    }

    public void ResetHistory(bool clearSessionHistory = true)
    {
        lock (historyLock)
        {
            promptHistory.Clear();
            if (clearSessionHistory)
            {
                sessionHistory.Clear();
            }
        }
    }

    public void AddTurn(string role, string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return;
        lock (historyLock)
        {
            var turn = new ChatTurn(role, content.Trim());
            promptHistory.Add(turn);
            while (promptHistory.Count > MaxPromptHistoryTurns)
            {
                promptHistory.RemoveAt(0);
            }
            sessionHistory.Add(turn);
            while (sessionHistory.Count > MaxSessionHistoryTurns)
            {
                sessionHistory.RemoveAt(0);
            }
        }
    }

    public static string SanitizeReply(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";

        string cleaned = raw.Trim();

        // Strip <think>...</think> reasoning blocks if present (DeepSeek, Qwen reasoning, etc.)
        int thinkStart = cleaned.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
        while (thinkStart >= 0)
        {
            int thinkEnd = cleaned.IndexOf("</think>", thinkStart, StringComparison.OrdinalIgnoreCase);
            if (thinkEnd >= 0)
            {
                cleaned = cleaned.Remove(thinkStart, (thinkEnd + 8) - thinkStart).Trim();
                thinkStart = cleaned.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                // Unclosed think tag: strip everything from <think> onwards
                cleaned = cleaned[..thinkStart].Trim();
                break;
            }
        }

        // Strip "Here's a thinking process..." reasoning leaks
        if (cleaned.StartsWith("Here's a thinking process", StringComparison.OrdinalIgnoreCase) ||
            cleaned.StartsWith("Here is a thinking process", StringComparison.OrdinalIgnoreCase))
        {
            // Find where actual dialogue starts (double newline or end of numbered steps)
            int dnl = cleaned.IndexOf("\n\n");
            if (dnl >= 0 && dnl + 2 < cleaned.Length)
            {
                cleaned = cleaned[(dnl + 2)..].Trim();
            }
            else
            {
                cleaned = "";
            }
        }

        // Strip enclosing double quotes if present
        if (cleaned.Length >= 2 && cleaned.StartsWith("\"") && cleaned.EndsWith("\""))
        {
            cleaned = cleaned[1..^1].Trim();
        }

        string textWithoutReasoning = cleaned;

        // Remove asterisks action formatting like *smiles* or *giggles*
        var sb = new StringBuilder(cleaned.Length);
        bool insideAsterisks = false;
        for (int i = 0; i < cleaned.Length; i++)
        {
            char c = cleaned[i];
            if (c == '*')
            {
                insideAsterisks = !insideAsterisks;
                continue;
            }
            if (!insideAsterisks)
            {
                sb.Append(c);
            }
        }
        cleaned = sb.ToString().Trim();

        // If stripping asterisks emptied the string (e.g. model output only "*waves*"),
        // use the text WITHOUT reasoning with asterisks removed (NOT raw, which might contain <think>!)
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            cleaned = textWithoutReasoning.Replace("*", "").Trim();
        }

        // Replace newlines, tabs, and multiple spaces with a single space
        var normalized = new StringBuilder();
        bool lastWasSpace = false;
        foreach (char c in cleaned)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace)
                {
                    normalized.Append(' ');
                    lastWasSpace = true;
                }
            }
            else
            {
                normalized.Append(c);
                lastWasSpace = false;
            }
        }
        cleaned = normalized.ToString().Trim();

        // Bounded length safe for subtitles (max 240 Unicode text elements)
        return SubtitleTurnState.DisplayExcerpt(cleaned);
    }

    public static string BuildRequestJson(string userMessage, AIChatConfig config, IReadOnlyList<ChatTurn>? existingHistory = null)
    {
        var messagesNode = new JsonArray();

        // System prompt
        string systemPrompt = !string.IsNullOrWhiteSpace(config.SystemPrompt)
            ? config.SystemPrompt.Trim()
            : AIProviderDefaults.DefaultSystemPrompt;

        messagesNode.Add(new JsonObject
        {
            ["role"] = "system",
            ["content"] = systemPrompt
        });

        // History
        if (existingHistory != null)
        {
            foreach (var turn in existingHistory)
            {
                if (!string.IsNullOrWhiteSpace(turn.Content))
                {
                    messagesNode.Add(new JsonObject
                    {
                        ["role"] = turn.Role,
                        ["content"] = turn.Content
                    });
                }
            }
        }

        // Current user message
        messagesNode.Add(new JsonObject
        {
            ["role"] = "user",
            ["content"] = userMessage.Trim()
        });

        string model = AIProviderDefaults.GetEffectiveModel(config.Provider, config.Model);

        var root = new JsonObject
        {
            ["model"] = model,
            ["messages"] = messagesNode,
            ["max_tokens"] = Math.Clamp(config.MaxTokens, 10, 500),
            ["temperature"] = Math.Clamp(config.Temperature, 0f, 2f)
        };

        // Some OpenRouter reasoning models (e.g. Nemotron) accept reasoning.effort = none to eliminate 10s latency
        if (config.Provider == AIProvider.OpenRouter && model.Contains("nemotron", StringComparison.OrdinalIgnoreCase))
        {
            root["reasoning"] = new JsonObject
            {
                ["effort"] = "none"
            };
        }

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    public static bool TryParseResponse(string responseJson, out string replyContent, out string? errorMessage)
    {
        replyContent = "";
        errorMessage = null;

        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var errorProp))
            {
                if (errorProp.ValueKind == JsonValueKind.Object && errorProp.TryGetProperty("message", out var msgProp))
                {
                    errorMessage = msgProp.GetString();
                }
                else
                {
                    errorMessage = errorProp.ToString();
                }
                return false;
            }

            if (root.TryGetProperty("choices", out var choicesProp) && choicesProp.GetArrayLength() > 0)
            {
                var firstChoice = choicesProp[0];
                if (firstChoice.TryGetProperty("message", out var msgProp) &&
                    msgProp.TryGetProperty("content", out var contentProp))
                {
                    replyContent = SanitizeReply(contentProp.GetString() ?? "");
                    return true;
                }
            }

            errorMessage = "No valid choices returned in response.";
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = $"JSON parse error: {ex.Message}";
            return false;
        }
    }

    public async Task<AIChatResult> SendMessageAsync(
        string userMessage,
        AIChatConfig config,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return new AIChatResult(false, "", "Message cannot be empty.", sw.ElapsedMilliseconds);
        }

        // Fixture provider bypasses network entirely
        if (config.Provider == AIProvider.Fixture)
        {
            string fixtureLine = "Hello! I'm right here.";
            return new AIChatResult(true, fixtureLine, null, sw.ElapsedMilliseconds);
        }

        string endpoint = AIProviderDefaults.GetEffectiveEndpoint(config.Provider, config.EndpointUrl);
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return new AIChatResult(false, "[Configuration error: endpoint URL is missing]",
                "Endpoint URL is empty.", sw.ElapsedMilliseconds);
        }

        if (config.Provider == AIProvider.OpenRouter && string.IsNullOrWhiteSpace(config.ApiKey))
        {
            return new AIChatResult(false,
                "[OpenRouter API key is missing. Set AI.ApiKey in BepInEx config]",
                "OpenRouter requires an API key.", sw.ElapsedMilliseconds);
        }

        IReadOnlyList<ChatTurn> currentHistory;
        lock (historyLock)
        {
            currentHistory = promptHistory.ToList();
        }

        string requestJson = BuildRequestJson(userMessage, config, currentHistory);

        float timeoutSec = Math.Clamp(config.TimeoutSeconds, 3f, 120f);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSec));

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            if (!string.IsNullOrWhiteSpace(config.ApiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey.Trim());
            }

            if (config.Provider == AIProvider.OpenRouter)
            {
                request.Headers.TryAddWithoutValidation("HTTP-Referer", "https://github.com/MitaAI");
                request.Headers.TryAddWithoutValidation("X-Title", "MitaAI");
            }

            using var response = await SharedHttpClient.SendAsync(request, cts.Token).ConfigureAwait(false);
            string responseBody = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);

            sw.Stop();

            if (!response.IsSuccessStatusCode)
            {
                string statusDesc = response.StatusCode switch
                {
                    System.Net.HttpStatusCode.Unauthorized => "Invalid API key (401). Check AI.ApiKey in config.",
                    System.Net.HttpStatusCode.NotFound => "Endpoint or model not found (404). Check EndpointUrl and Model.",
                    System.Net.HttpStatusCode.TooManyRequests => "Rate limited (429). Please wait before asking again.",
                    _ => $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}"
                };

                if (TryParseResponse(responseBody, out _, out string? apiError) && !string.IsNullOrWhiteSpace(apiError))
                {
                    statusDesc += $": {apiError}";
                }

                return new AIChatResult(false, $"[{statusDesc}]", statusDesc, sw.ElapsedMilliseconds);
            }

            if (TryParseResponse(responseBody, out string reply, out string? parseError))
            {
                if (string.IsNullOrWhiteSpace(reply))
                {
                    return new AIChatResult(false, "[Empty reply from AI]", "Model returned empty response.", sw.ElapsedMilliseconds);
                }

                return new AIChatResult(true, reply, null, sw.ElapsedMilliseconds);
            }
            else
            {
                string err = parseError ?? "Failed to parse model response.";
                return new AIChatResult(false, $"[Model Error: {err}]", err, sw.ElapsedMilliseconds);
            }
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            if (cancellationToken.IsCancellationRequested)
            {
                return new AIChatResult(false, "[Request Cancelled]", "Request was cancelled.", sw.ElapsedMilliseconds);
            }
            string timeoutMsg = $"Request timed out after {timeoutSec:0}s. Is the AI server responding?";
            return new AIChatResult(false, $"[{timeoutMsg}]", timeoutMsg, sw.ElapsedMilliseconds);
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            string hint = config.Provider switch
            {
                AIProvider.Ollama => "Could not reach Ollama. Make sure Ollama is running on localhost:11434.",
                AIProvider.LMStudio => "Could not reach LM Studio. Make sure local server is started on localhost:1234.",
                AIProvider.OpenRouter => "Could not connect to OpenRouter. Check your internet connection.",
                _ => $"Could not connect to {endpoint}: {ex.Message}"
            };
            return new AIChatResult(false, $"[{hint}]", ex.ToString(), sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new AIChatResult(false, $"[Unexpected error: {ex.Message}]", ex.ToString(), sw.ElapsedMilliseconds);
        }
    }
}
