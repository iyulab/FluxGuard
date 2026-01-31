using System.Diagnostics;
using FluxGuard.Abstractions;
using FluxGuard.Core;
using FluxGuard.L2.Guards.Input;
using FluxGuard.L2.ML;
using FluxGuard.L2.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace FluxGuard.L2.Guards.Output;

/// <summary>
/// L2 Toxicity Guard using Detoxify ML model
/// Detects toxic, offensive, and harmful content in LLM outputs
/// </summary>
public sealed class L2ToxicityGuard : IOutputGuard, IDisposable
{
    private readonly OnnxSessionManager _sessionManager;
    private readonly TokenizerWrapper _tokenizer;
    private readonly string _modelId;
    private readonly L2GuardOptions _options;

    /// <summary>
    /// Toxicity categories to check
    /// </summary>
    private static readonly string[] ToxicityCategories =
    [
        "toxicity",
        "severe_toxicity",
        "obscene",
        "threat",
        "insult",
        "identity_attack",
        "sexual_explicit"
    ];

    /// <inheritdoc/>
    public string Name => "L2.Toxicity";

    /// <inheritdoc/>
    public string Layer => "L2";

    /// <inheritdoc/>
    public int Order => 150; // After L1, before L3

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
    /// Creates a new L2 Toxicity Guard
    /// </summary>
    public L2ToxicityGuard(
        OnnxSessionManager sessionManager,
        L2GuardOptions? options = null)
    {
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _options = options ?? new L2GuardOptions();
        _modelId = ModelLoader.ToxicityModelId;

        // Register model if not already registered
        if (!_sessionManager.IsModelRegistered(_modelId))
        {
            var modelInfo = ModelLoader.GetToxicityModelInfo(_options.ModelsBasePath);
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
    public async ValueTask<GuardCheckResult> CheckAsync(GuardContext context, string output)
    {
        if (!IsEnabled)
        {
            return GuardCheckResult.Safe;
        }

        if (string.IsNullOrWhiteSpace(output))
        {
            return GuardCheckResult.Safe;
        }

        // Check if model is available
        if (!_sessionManager.IsModelRegistered(_modelId))
        {
            return GuardCheckResult.Safe;
        }

        var sw = Stopwatch.StartNew();

        try
        {
            var result = await Task.Run(() => RunInference(output), context.CancellationToken);
            sw.Stop();

            if (!result.Success)
            {
                return GuardCheckResult.Safe;
            }

            // Check for any toxic category above threshold
            var maxToxicScore = result.Probabilities.Values.Max();
            var maxToxicCategory = result.Probabilities
                .OrderByDescending(kvp => kvp.Value)
                .First();

            if (maxToxicScore >= _options.BlockThreshold)
            {
                return new GuardCheckResult
                {
                    GuardName = Name,
                    Passed = false,
                    Score = maxToxicScore,
                    Severity = Severity.Critical,
                    Pattern = $"L2.Toxicity.{maxToxicCategory.Key}",
                    Message = $"Toxic content detected: {maxToxicCategory.Key} ({maxToxicScore:P0} confidence)",
                    Details = $"Category: {maxToxicCategory.Key}, Score: {maxToxicScore:P0}",
                    NeedsEscalation = false,
                    LatencyMs = sw.Elapsed.TotalMilliseconds
                };
            }

            if (maxToxicScore >= _options.EscalationThreshold)
            {
                return new GuardCheckResult
                {
                    GuardName = Name,
                    Passed = true,
                    Score = maxToxicScore,
                    Severity = Severity.Medium,
                    Pattern = $"L2.Toxicity.{maxToxicCategory.Key}",
                    Message = $"Potential toxic content: {maxToxicCategory.Key} ({maxToxicScore:P0}), escalating to L3",
                    Details = $"Category: {maxToxicCategory.Key}, Score: {maxToxicScore:P0}",
                    NeedsEscalation = true,
                    LatencyMs = sw.Elapsed.TotalMilliseconds
                };
            }

            if (maxToxicScore >= _options.FlagThreshold)
            {
                return new GuardCheckResult
                {
                    GuardName = Name,
                    Passed = true,
                    Score = maxToxicScore,
                    Severity = Severity.Low,
                    Pattern = $"L2.Toxicity.{maxToxicCategory.Key}",
                    Message = $"Low toxicity signal: {maxToxicCategory.Key} ({maxToxicScore:P0})",
                    Details = $"Category: {maxToxicCategory.Key}, Score: {maxToxicScore:P0}",
                    NeedsEscalation = false,
                    LatencyMs = sw.Elapsed.TotalMilliseconds
                };
            }

            return GuardCheckResult.Safe;
        }
        catch (Exception)
        {
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

            // Apply sigmoid for multi-label classification
            var probabilities = logits.Select(x => 1.0f / (1.0f + MathF.Exp(-x))).ToArray();

            sw.Stop();

            // Build category probability map
            var categoryProbs = new Dictionary<string, double>();
            for (var i = 0; i < ToxicityCategories.Length && i < probabilities.Length; i++)
            {
                categoryProbs[ToxicityCategories[i]] = probabilities[i];
            }

            var maxCategory = categoryProbs.OrderByDescending(kvp => kvp.Value).First();

            return new InferenceResult
            {
                Label = maxCategory.Key,
                Score = maxCategory.Value,
                Probabilities = categoryProbs,
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
}
