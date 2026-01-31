using System.Diagnostics;
using System.Globalization;
using FluxGuard.Abstractions;
using FluxGuard.Core;
using FluxGuard.L2.ML;
using FluxGuard.L2.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace FluxGuard.L2.Guards.Input;

/// <summary>
/// L2 Prompt Injection Guard using DeBERTa-v3 ML model
/// Provides higher accuracy than L1 regex patterns
/// </summary>
public sealed class L2PromptInjectionGuard : IInputGuard, IDisposable
{
    private readonly OnnxSessionManager _sessionManager;
    private readonly TokenizerWrapper _tokenizer;
    private readonly string _modelId;
    private readonly L2GuardOptions _options;

    /// <summary>
    /// Threshold for confirmed injection (blocks immediately)
    /// </summary>
    public double BlockThreshold => _options.BlockThreshold;

    /// <summary>
    /// Threshold for escalation to L3 (uncertain, needs further review)
    /// </summary>
    public double EscalationThreshold => _options.EscalationThreshold;

    /// <inheritdoc/>
    public string Name => "L2.PromptInjection";

    /// <inheritdoc/>
    public string Layer => "L2";

    /// <inheritdoc/>
    public int Order => 150; // After L1 (100-140), before L3

    /// <inheritdoc/>
    public bool IsEnabled { get; private set; } = true;

    /// <summary>
    /// Enable or disable this guard
    /// </summary>
    public void SetEnabled(bool enabled) => IsEnabled = enabled;

    /// <inheritdoc/>
    public void Dispose()
    {
        _tokenizer.Dispose();
    }

    /// <summary>
    /// Creates a new L2 Prompt Injection Guard
    /// </summary>
    public L2PromptInjectionGuard(
        OnnxSessionManager sessionManager,
        L2GuardOptions? options = null)
    {
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _options = options ?? new L2GuardOptions();
        _modelId = ModelLoader.PromptInjectionModelId;

        // Register model if not already registered
        if (!_sessionManager.IsModelRegistered(_modelId))
        {
            var modelInfo = ModelLoader.GetPromptInjectionModelInfo(_options.ModelsBasePath);
            if (ModelLoader.ModelExists(modelInfo))
            {
                _sessionManager.RegisterModel(modelInfo);
            }
        }

        // Initialize tokenizer
        var modelInfo2 = _sessionManager.GetModelInfo(_modelId);
        _tokenizer = new TokenizerWrapper(
            modelInfo2?.TokenizerPath ?? string.Empty,
            modelInfo2?.MaxSequenceLength ?? 512);
    }

    /// <inheritdoc/>
    public async ValueTask<GuardCheckResult> CheckAsync(GuardContext context)
    {
        if (!IsEnabled)
        {
            return GuardCheckResult.Safe;
        }

        var input = context.NormalizedInput ?? context.OriginalInput;
        if (string.IsNullOrWhiteSpace(input))
        {
            return GuardCheckResult.Safe;
        }

        // Check if model is available
        if (!_sessionManager.IsModelRegistered(_modelId))
        {
            // Model not available, skip L2 check
            return GuardCheckResult.Safe;
        }

        var sw = Stopwatch.StartNew();

        try
        {
            var result = await Task.Run(() => RunInference(input), context.CancellationToken);
            sw.Stop();

            if (!result.Success)
            {
                return GuardCheckResult.Safe;
            }

            var isInjection = result.Label == "injection";
            var score = isInjection ? result.Score : 0;

            // Determine decision based on thresholds
            if (isInjection && score >= BlockThreshold)
            {
                return new GuardCheckResult
                {
                    GuardName = Name,
                    Passed = false,
                    Score = score,
                    Severity = Severity.Critical,
                    Pattern = "L2.MLDetection",
                    Message = $"Prompt injection detected with {score:P0} confidence",
                    Details = $"ML model confidence: {score:P0}",
                    NeedsEscalation = false,
                    LatencyMs = sw.Elapsed.TotalMilliseconds
                };
            }

            if (isInjection && score >= EscalationThreshold)
            {
                return new GuardCheckResult
                {
                    GuardName = Name,
                    Passed = true, // Pass for now, but flag for escalation
                    Score = score,
                    Severity = Severity.Medium,
                    Pattern = "L2.MLDetection",
                    Message = $"Potential injection detected with {score:P0} confidence, escalating to L3",
                    Details = $"ML model confidence: {score:P0}",
                    NeedsEscalation = true,
                    LatencyMs = sw.Elapsed.TotalMilliseconds
                };
            }

            return GuardCheckResult.Safe;
        }
        catch (Exception)
        {
            // FailMode.Open - on error, pass through
            return GuardCheckResult.Safe;
        }
    }

    private InferenceResult RunInference(string text)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var session = _sessionManager.GetSession(_modelId);
            var modelInfo = _sessionManager.GetModelInfo(_modelId);

            if (modelInfo is null)
            {
                return InferenceResult.Failed("Model info not found", _modelId);
            }

            // Tokenize input
            var tokenized = _tokenizer.Tokenize(text);

            // Create input tensors
            var inputIds = new DenseTensor<long>(tokenized.InputIds, [1, tokenized.InputIds.Length]);
            var attentionMask = new DenseTensor<long>(tokenized.AttentionMask, [1, tokenized.AttentionMask.Length]);

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(modelInfo.InputNames[0], inputIds),
                NamedOnnxValue.CreateFromTensor(modelInfo.InputNames[1], attentionMask)
            };

            // Run inference
            using var outputs = session.Run(inputs);
            var outputsList = outputs.ToList();
            var logits = outputsList[0].AsTensor<float>();

            // Apply softmax to get probabilities
            var probabilities = Softmax([.. logits]);

            sw.Stop();

            // Find predicted class
            var predictedClass = Array.IndexOf(probabilities, probabilities.Max());
            var predictedLabel = modelInfo.Labels.TryGetValue(predictedClass, out var label)
                ? label
                : predictedClass.ToString(CultureInfo.InvariantCulture);

            return new InferenceResult
            {
                Label = predictedLabel,
                Score = probabilities[predictedClass],
                Probabilities = modelInfo.Labels.ToDictionary(
                    kvp => kvp.Value,
                    kvp => (double)probabilities[kvp.Key]),
                LatencyMs = sw.Elapsed.TotalMilliseconds,
                Success = true,
                ModelId = _modelId
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return InferenceResult.Failed(ex.Message, _modelId);
        }
    }

    private static float[] Softmax(float[] logits)
    {
        var maxLogit = logits.Max();
        var expValues = logits.Select(x => MathF.Exp(x - maxLogit)).ToArray();
        var sumExp = expValues.Sum();
        return expValues.Select(x => x / sumExp).ToArray();
    }
}

/// <summary>
/// Options for L2 guards
/// </summary>
public class L2GuardOptions
{
    /// <summary>
    /// Base path for model files
    /// </summary>
    public string? ModelsBasePath { get; set; }

    /// <summary>
    /// Threshold for blocking (0.85 default = high confidence)
    /// </summary>
    public double BlockThreshold { get; set; } = 0.85;

    /// <summary>
    /// Threshold for escalation to L3 (0.5 default = uncertain)
    /// </summary>
    public double EscalationThreshold { get; set; } = 0.5;

    /// <summary>
    /// Threshold for flagging (0.3 default = low suspicion)
    /// </summary>
    public double FlagThreshold { get; set; } = 0.3;

    /// <summary>
    /// Maximum inference timeout in milliseconds
    /// </summary>
    public int TimeoutMs { get; set; } = 5000;
}
