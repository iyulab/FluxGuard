namespace FluxGuard.L2.Models;

/// <summary>
/// Model metadata information
/// </summary>
public record ModelInfo
{
    /// <summary>
    /// Unique identifier for the model
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Display name of the model
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Model version
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Path to the ONNX model file
    /// </summary>
    public required string ModelPath { get; init; }

    /// <summary>
    /// Path to the tokenizer files (vocabulary, config, etc.)
    /// </summary>
    public string? TokenizerPath { get; init; }

    /// <summary>
    /// Maximum sequence length supported by the model
    /// </summary>
    public int MaxSequenceLength { get; init; } = 512;

    /// <summary>
    /// Input tensor names
    /// </summary>
    public IReadOnlyList<string> InputNames { get; init; } = ["input_ids", "attention_mask"];

    /// <summary>
    /// Output tensor name
    /// </summary>
    public string OutputName { get; init; } = "logits";

    /// <summary>
    /// Label mapping for classification outputs
    /// </summary>
    public IReadOnlyDictionary<int, string> Labels { get; init; } = new Dictionary<int, string>();

    /// <summary>
    /// Whether the model is enabled
    /// </summary>
    public bool Enabled { get; init; } = true;
}
