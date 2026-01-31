namespace FluxGuard.Remote.Configuration;

/// <summary>
/// Remote guard (L3) configuration options
/// </summary>
public sealed class RemoteGuardOptions
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "FluxGuard:Remote";

    /// <summary>
    /// Whether L3 remote guards are enabled (default: true when configured)
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Request timeout in milliseconds (default: 5000)
    /// </summary>
    public int TimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Maximum retries on failure (default: 1)
    /// </summary>
    public int MaxRetries { get; set; } = 1;

    /// <summary>
    /// Whether to enable semantic caching (default: true)
    /// </summary>
    public bool EnableCache { get; set; } = true;

    /// <summary>
    /// Cache TTL in seconds (default: 3600 = 1 hour)
    /// </summary>
    public int CacheTtlSeconds { get; set; } = 3600;

    /// <summary>
    /// Maximum cache entries (default: 10000)
    /// </summary>
    public int MaxCacheEntries { get; set; } = 10000;

    /// <summary>
    /// LLM Judge options
    /// </summary>
    public LLMJudgeOptions Judge { get; set; } = new();

    /// <summary>
    /// OpenAI provider options
    /// </summary>
    public OpenAIProviderOptions OpenAI { get; set; } = new();
}

/// <summary>
/// LLM Judge configuration
/// </summary>
public sealed class LLMJudgeOptions
{
    /// <summary>
    /// Default model for judge (default: "gpt-4o-mini")
    /// </summary>
    public string Model { get; set; } = "gpt-4o-mini";

    /// <summary>
    /// Temperature for judge (default: 0.0 for consistency)
    /// </summary>
    public double Temperature { get; set; }

    /// <summary>
    /// Maximum tokens for response (default: 256)
    /// </summary>
    public int MaxTokens { get; set; } = 256;

    /// <summary>
    /// Confidence threshold for block decision (default: 0.8)
    /// </summary>
    public double BlockThreshold { get; set; } = 0.8;

    /// <summary>
    /// Confidence threshold for flag decision (default: 0.5)
    /// </summary>
    public double FlagThreshold { get; set; } = 0.5;
}

/// <summary>
/// OpenAI provider configuration
/// </summary>
public sealed class OpenAIProviderOptions
{
    /// <summary>
    /// OpenAI API key
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Organization ID (optional)
    /// </summary>
    public string? OrganizationId { get; set; }

    /// <summary>
    /// Base URL (for Azure OpenAI or custom endpoints)
    /// </summary>
    public Uri? BaseUrl { get; set; }

    /// <summary>
    /// Azure OpenAI deployment name (for Azure endpoints)
    /// </summary>
    public string? DeploymentName { get; set; }

    /// <summary>
    /// API version (for Azure OpenAI)
    /// </summary>
    public string? ApiVersion { get; set; }

    /// <summary>
    /// Whether to use Azure OpenAI
    /// </summary>
    public bool UseAzure { get; set; }
}
