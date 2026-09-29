namespace BrightSpring.Referral.Agents;

/// <summary>
/// Connection settings for <see cref="ClaudeChatClient"/>.
///
/// Populated from configuration, never hardcoded. Locally that means
/// `dotnet user-secrets` (see README.md "Configuration"); in a real
/// deployment it means a secrets manager (Azure Key Vault, AWS Secrets
/// Manager) or a value injected by CI/CD into an environment variable —
/// the same personal-vs-production split used on the aloan.ai demo this
/// project is modeled after.
/// </summary>
public sealed class ClaudeChatClientOptions
{
    public const string SectionName = "Claude";

    /// <summary>Anthropic API key. Required.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Model identifier to call, e.g. "claude-sonnet-5". Kept
    /// configurable rather than constant so swapping models is a config
    /// change, not a code change.
    /// </summary>
    public string Model { get; set; } = "claude-sonnet-5";

    /// <summary>Anthropic Messages API base address.</summary>
    public string BaseAddress { get; set; } = "https://api.anthropic.com/v1/";

    /// <summary>Anthropic API version header value.</summary>
    public string ApiVersion { get; set; } = "2023-06-01";

    /// <summary>Upper bound on tokens generated per call.</summary>
    public int MaxOutputTokens { get; set; } = 4096;
}
