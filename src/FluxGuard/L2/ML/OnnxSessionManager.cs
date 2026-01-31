using System.Collections.Concurrent;
using FluxGuard.L2.Models;
using Microsoft.ML.OnnxRuntime;

namespace FluxGuard.L2.ML;

/// <summary>
/// Thread-safe singleton manager for ONNX Runtime sessions
/// Uses Lazy initialization to ensure single instance per model
/// </summary>
public sealed class OnnxSessionManager : IDisposable
{
    private readonly ConcurrentDictionary<string, Lazy<InferenceSession>> _sessions = new();
    private readonly ConcurrentDictionary<string, ModelInfo> _modelInfos = new();
    private readonly SessionOptions _defaultOptions;
    private bool _disposed;

    /// <summary>
    /// Creates a new session manager with default options
    /// </summary>
    public OnnxSessionManager()
        : this(SessionOptionsFactory.CreateDefault())
    {
    }

    /// <summary>
    /// Creates a new session manager with custom options
    /// </summary>
    public OnnxSessionManager(SessionOptions options)
    {
        _defaultOptions = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Registers a model for lazy loading
    /// </summary>
    public void RegisterModel(ModelInfo modelInfo)
    {
        ArgumentNullException.ThrowIfNull(modelInfo);

        if (!_modelInfos.TryAdd(modelInfo.Id, modelInfo))
        {
            throw new InvalidOperationException($"Model '{modelInfo.Id}' is already registered");
        }

        _sessions.TryAdd(modelInfo.Id, new Lazy<InferenceSession>(
            () => CreateSession(modelInfo),
            LazyThreadSafetyMode.ExecutionAndPublication));
    }

    /// <summary>
    /// Gets or creates an inference session for the specified model
    /// </summary>
    public InferenceSession GetSession(string modelId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_sessions.TryGetValue(modelId, out var lazySession))
        {
            throw new KeyNotFoundException($"Model '{modelId}' is not registered. Call RegisterModel first.");
        }

        return lazySession.Value;
    }

    /// <summary>
    /// Gets model info for the specified model ID
    /// </summary>
    public ModelInfo? GetModelInfo(string modelId)
    {
        return _modelInfos.TryGetValue(modelId, out var info) ? info : null;
    }

    /// <summary>
    /// Checks if a model is registered
    /// </summary>
    public bool IsModelRegistered(string modelId)
    {
        return _modelInfos.ContainsKey(modelId);
    }

    /// <summary>
    /// Checks if a model session has been created (loaded into memory)
    /// </summary>
    public bool IsModelLoaded(string modelId)
    {
        return _sessions.TryGetValue(modelId, out var lazySession) && lazySession.IsValueCreated;
    }

    /// <summary>
    /// Preloads a model session (useful for warmup)
    /// </summary>
    public void PreloadModel(string modelId)
    {
        _ = GetSession(modelId);
    }

    /// <summary>
    /// Preloads all registered models
    /// </summary>
    public void PreloadAllModels()
    {
        foreach (var modelId in _modelInfos.Keys)
        {
            PreloadModel(modelId);
        }
    }

    /// <summary>
    /// Gets all registered model IDs
    /// </summary>
    public IEnumerable<string> GetRegisteredModelIds()
    {
        return _modelInfos.Keys.ToList();
    }

    /// <summary>
    /// Unloads and removes a model session
    /// </summary>
    public bool UnloadModel(string modelId)
    {
        if (_sessions.TryRemove(modelId, out var lazySession))
        {
            if (lazySession.IsValueCreated)
            {
                lazySession.Value.Dispose();
            }
            _modelInfos.TryRemove(modelId, out _);
            return true;
        }
        return false;
    }

    private InferenceSession CreateSession(ModelInfo modelInfo)
    {
        if (!File.Exists(modelInfo.ModelPath))
        {
            throw new FileNotFoundException(
                $"ONNX model file not found: {modelInfo.ModelPath}",
                modelInfo.ModelPath);
        }

        return new InferenceSession(modelInfo.ModelPath, _defaultOptions);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var lazySession in _sessions.Values)
        {
            if (lazySession.IsValueCreated)
            {
                lazySession.Value.Dispose();
            }
        }

        _sessions.Clear();
        _modelInfos.Clear();
        _defaultOptions.Dispose();
    }
}
