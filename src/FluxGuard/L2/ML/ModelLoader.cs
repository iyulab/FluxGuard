using FluxGuard.L2.Models;

namespace FluxGuard.L2.ML;

/// <summary>
/// Utility class for loading and managing ML models
/// </summary>
public static class ModelLoader
{
    /// <summary>
    /// Default models directory relative to application base
    /// </summary>
    public const string DefaultModelsDirectory = "models";

    /// <summary>
    /// Prompt injection detection model ID
    /// </summary>
    public const string PromptInjectionModelId = "prompt-injection-v2";

    /// <summary>
    /// Toxicity detection model ID
    /// </summary>
    public const string ToxicityModelId = "toxicity-unbiased";

    /// <summary>
    /// Gets the default model info for prompt injection detection
    /// Uses DeBERTa-v3-base-prompt-injection-v2
    /// </summary>
    public static ModelInfo GetPromptInjectionModelInfo(string? basePath = null)
    {
        var modelsDir = GetModelsDirectory(basePath);

        return new ModelInfo
        {
            Id = PromptInjectionModelId,
            Name = "DeBERTa-v3 Prompt Injection Detector",
            Version = "2.0",
            ModelPath = Path.Combine(modelsDir, "prompt-injection", "model.onnx"),
            TokenizerPath = Path.Combine(modelsDir, "prompt-injection", "vocab.txt"),
            MaxSequenceLength = 512,
            InputNames = ["input_ids", "attention_mask"],
            OutputName = "logits",
            Labels = new Dictionary<int, string>
            {
                { 0, "safe" },
                { 1, "injection" }
            }
        };
    }

    /// <summary>
    /// Gets the default model info for toxicity detection
    /// Uses Detoxify unbiased model
    /// </summary>
    public static ModelInfo GetToxicityModelInfo(string? basePath = null)
    {
        var modelsDir = GetModelsDirectory(basePath);

        return new ModelInfo
        {
            Id = ToxicityModelId,
            Name = "Detoxify Unbiased Toxicity Detector",
            Version = "1.0",
            ModelPath = Path.Combine(modelsDir, "toxicity", "model.onnx"),
            TokenizerPath = Path.Combine(modelsDir, "toxicity", "vocab.txt"),
            MaxSequenceLength = 512,
            InputNames = ["input_ids", "attention_mask"],
            OutputName = "logits",
            Labels = new Dictionary<int, string>
            {
                { 0, "toxicity" },
                { 1, "severe_toxicity" },
                { 2, "obscene" },
                { 3, "threat" },
                { 4, "insult" },
                { 5, "identity_attack" },
                { 6, "sexual_explicit" }
            }
        };
    }

    /// <summary>
    /// Gets the models directory path
    /// </summary>
    public static string GetModelsDirectory(string? basePath = null)
    {
        basePath ??= AppContext.BaseDirectory;
        return Path.Combine(basePath, DefaultModelsDirectory);
    }

    /// <summary>
    /// Checks if a model file exists
    /// </summary>
    public static bool ModelExists(ModelInfo modelInfo)
    {
        return File.Exists(modelInfo.ModelPath);
    }

    /// <summary>
    /// Checks if all required models exist
    /// </summary>
    public static ModelAvailability CheckModelAvailability(string? basePath = null)
    {
        var promptInjection = GetPromptInjectionModelInfo(basePath);
        var toxicity = GetToxicityModelInfo(basePath);

        return new ModelAvailability
        {
            PromptInjectionAvailable = ModelExists(promptInjection),
            ToxicityAvailable = ModelExists(toxicity),
            ModelsDirectory = GetModelsDirectory(basePath)
        };
    }

    /// <summary>
    /// Downloads missing models from the default source
    /// </summary>
    public static async Task<bool> DownloadMissingModelsAsync(
        string? basePath = null,
        IProgress<ModelDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // This is a placeholder for model download functionality
        // In production, this would download from HuggingFace or a custom CDN
        await Task.CompletedTask;

        progress?.Report(new ModelDownloadProgress
        {
            ModelId = "all",
            Status = DownloadStatus.NotAvailable,
            Message = "Model download not yet implemented. Please manually place models in the models directory."
        });

        return false;
    }
}

/// <summary>
/// Model availability status
/// </summary>
public record ModelAvailability
{
    /// <summary>
    /// Whether prompt injection model is available
    /// </summary>
    public bool PromptInjectionAvailable { get; init; }

    /// <summary>
    /// Whether toxicity model is available
    /// </summary>
    public bool ToxicityAvailable { get; init; }

    /// <summary>
    /// Path to models directory
    /// </summary>
    public required string ModelsDirectory { get; init; }

    /// <summary>
    /// Whether all required models are available
    /// </summary>
    public bool AllModelsAvailable => PromptInjectionAvailable && ToxicityAvailable;
}

/// <summary>
/// Model download progress information
/// </summary>
public record ModelDownloadProgress
{
    /// <summary>
    /// Model being downloaded
    /// </summary>
    public required string ModelId { get; init; }

    /// <summary>
    /// Download status
    /// </summary>
    public DownloadStatus Status { get; init; }

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public int ProgressPercent { get; init; }

    /// <summary>
    /// Status message
    /// </summary>
    public string? Message { get; init; }
}

/// <summary>
/// Download status enum
/// </summary>
public enum DownloadStatus
{
    /// <summary>
    /// Download pending
    /// </summary>
    Pending,

    /// <summary>
    /// Currently downloading
    /// </summary>
    Downloading,

    /// <summary>
    /// Download completed
    /// </summary>
    Completed,

    /// <summary>
    /// Download failed
    /// </summary>
    Failed,

    /// <summary>
    /// Download not available
    /// </summary>
    NotAvailable
}
