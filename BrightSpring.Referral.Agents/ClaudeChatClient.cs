using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace BrightSpring.Referral.Agents;

/// <summary>
/// The one place in this entire solution that knows Claude exists.
///
/// Microsoft.Extensions.AI's <see cref="IChatClient"/> is a
/// backend-agnostic abstraction: nothing that calls it knows or cares
/// whether the request is served by Claude, Azure OpenAI, or a local
/// model. What makes that abstraction usable is a client that is
/// strongly typed to a specific backend's wire format — that's this
/// class. Microsoft.Agents.AI builds agents on top of IChatClient, so
/// swapping this class out for a different <see cref="IChatClient"/>
/// implementation (say, an Azure OpenAI one) requires no change anywhere
/// else in the Agents project: IntakeAgent and ReviewAgent are written
/// against the interface, not against Claude specifically.
///
/// This is a deliberately minimal implementation of the Anthropic
/// Messages API — enough to support the single-turn, non-streaming,
/// non-tool-calling requests this project makes. A production client
/// would add streaming, tool-use blocks, retries with backoff, and
/// proper rate-limit handling.
/// </summary>
public sealed class ClaudeChatClient : IChatClient
{
    // A single naming policy applied to every request/response DTO below,
    // rather than a [JsonPropertyName] attribute hand-placed on each
    // property. Anthropic's wire format is lowercase/snake_case
    // throughout ("model", "max_tokens", "stop_reason", ...); a policy
    // converts every C# property consistently (Model -> model, MaxTokens
    // -> max_tokens) and, critically, can't be forgotten on a new field
    // the way an attribute can — CaseInsensitive is added as well so a
    // future response field survives even if Anthropic's exact casing
    // shifts.
    private static readonly JsonSerializerOptions AnthropicJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,

        // When there's no system prompt (systemBlocks is null), this
        // omits "system" from the JSON entirely rather than serializing
        // it as a literal `null`. Anthropic's validation error was
        // "system: Input should be a valid array" — an explicit null is
        // not an array either, so it would fail the same check.
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;
    private readonly ClaudeChatClientOptions _options;

    public ClaudeChatClient(HttpClient httpClient, IOptions<ClaudeChatClientOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;

        _httpClient.BaseAddress ??= new Uri(_options.BaseAddress);
        _httpClient.DefaultRequestHeaders.Remove("x-api-key");
        _httpClient.DefaultRequestHeaders.Add("x-api-key", _options.ApiKey);
        _httpClient.DefaultRequestHeaders.Remove("anthropic-version");
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", _options.ApiVersion);
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <inheritdoc />
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Anthropic separates the system prompt from the message turns;
        // Microsoft.Extensions.AI's ChatMessage list mixes them by role,
        // so we split them out here rather than asking every caller to
        // know about that wire-format difference.
        string? systemPrompt = null;
        var turns = new List<AnthropicMessage>();

        foreach (var message in messages)
        {
            var text = string.Concat(message.Contents.OfType<TextContent>().Select(t => t.Text));
            if (message.Role == ChatRole.System)
            {
                systemPrompt = systemPrompt is null ? text : $"{systemPrompt}\n{text}";
                continue;
            }

            turns.Add(new AnthropicMessage(
                Role: message.Role == ChatRole.Assistant ? "assistant" : "user",
                Content: text));
        }

        // Anthropic's current Messages API requires "system" to be an
        // array of content blocks (the same shape user/assistant content
        // uses), not a bare string — sending a string produces exactly
        // the 400 this project hit: "system: Input should be a valid
        // array". A single-element array with one text block is the
        // equivalent of the old plain-string form.
        List<AnthropicContentBlock>? systemBlocks = systemPrompt is null
            ? null
            : new List<AnthropicContentBlock> { new("text", systemPrompt) };

        var request = new AnthropicMessagesRequest(
            Model: _options.Model,
            System: systemBlocks,
            Messages: turns,
            MaxTokens: options?.MaxOutputTokens ?? _options.MaxOutputTokens,
            Temperature: options?.Temperature);

        // Plain reflection-based System.Text.Json here, not a
        // source-generated JsonSerializerContext: the request/response
        // shapes are small and this is a demo project, so the extra
        // ceremony (and the "every containing type must be partial"
        // constraint that comes with it) isn't worth paying for. A
        // production client processing high request volume would
        // reasonably switch to a source-generated context for the
        // trimming/AOT and startup-time benefits.
        //
        // AnthropicJsonOptions is passed explicitly on both calls below —
        // PostAsJsonAsync/ReadFromJsonAsync fall back to
        // JsonSerializerOptions.Default (which preserves exact C#
        // casing) if no options are given, which is exactly the bug that
        // sent "Model"/"Messages"/"Role"/"Content" to Anthropic instead
        // of "model"/"messages"/"role"/"content" and got a 400 back.
        using var response = await _httpClient
            .PostAsJsonAsync("messages", request, AnthropicJsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            // Anthropic's error responses are themselves informative JSON
            // (e.g. {"error":{"type":"invalid_request_error","message":"..."}}).
            // EnsureSuccessStatusCode() would discard that body and only
            // report the status code, which is exactly what made this bug
            // hard to diagnose from the console log alone — surfacing the
            // actual message means the next mismatch (if any) is visible
            // immediately instead of requiring a debugger attach.
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException(
                $"Claude API request failed with {(int)response.StatusCode} {response.StatusCode}: {errorBody}");
        }

        var payload = await response.Content
            .ReadFromJsonAsync<AnthropicMessagesResponse>(AnthropicJsonOptions, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Claude returned an empty response body.");

        var responseText = string.Concat(payload.Content.Select(c => c.Text));

        var chatMessage = new ChatMessage(ChatRole.Assistant, responseText);
        return new ChatResponse(chatMessage)
        {
            ModelId = payload.Model,
            FinishReason = payload.StopReason == "max_tokens" ? ChatFinishReason.Length : ChatFinishReason.Stop,
        };
    }

    /// <summary>
    /// Streaming is not implemented for this demo — every call in this
    /// project is a single request/response extraction or drafting step,
    /// not an interactive chat, so RunAsync-style non-streaming calls are
    /// the only path exercised. A production client would implement this
    /// against Anthropic's server-sent-events streaming endpoint the same
    /// way RunStreamingAsync would on the agent side.
    /// </summary>
    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "Streaming is not implemented in this demo client. Use GetResponseAsync (RunAsync) instead.");
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() => _httpClient.Dispose();

    // ---- Anthropic wire-format DTOs -----------------------------------
    // Kept private to this file: nothing outside this class should ever
    // need to know Claude's request/response shape.

    // No [JsonPropertyName] attributes needed on any of these — every
    // property is converted by AnthropicJsonOptions' naming policy above
    // (Model -> model, MaxTokens -> max_tokens, StopReason -> stop_reason,
    // etc.), applied uniformly on both the request and response side.
    private sealed record AnthropicMessage(string Role, string Content);

    private sealed record AnthropicMessagesRequest(
        string Model,
        List<AnthropicContentBlock>? System,
        List<AnthropicMessage> Messages,
        int MaxTokens,
        double? Temperature);

    private sealed record AnthropicContentBlock(string Type, string Text);

    private sealed record AnthropicMessagesResponse(
        string Id,
        string Model,
        List<AnthropicContentBlock> Content,
        string? StopReason);
}
