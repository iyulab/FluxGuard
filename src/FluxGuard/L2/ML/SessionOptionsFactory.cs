using Microsoft.ML.OnnxRuntime;

namespace FluxGuard.L2.ML;

/// <summary>
/// Factory for creating ONNX Runtime session options
/// </summary>
public static class SessionOptionsFactory
{
    /// <summary>
    /// Creates default session options optimized for inference
    /// </summary>
    public static SessionOptions CreateDefault()
    {
        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
            EnableMemoryPattern = true,
            EnableCpuMemArena = true
        };

        // Set thread count based on available cores
        var threadCount = Math.Max(1, Environment.ProcessorCount / 2);
        options.InterOpNumThreads = threadCount;
        options.IntraOpNumThreads = threadCount;

        return options;
    }

    /// <summary>
    /// Creates session options for low-latency inference
    /// </summary>
    public static SessionOptions CreateLowLatency()
    {
        var options = CreateDefault();
        options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_EXTENDED;
        options.IntraOpNumThreads = 1;
        options.InterOpNumThreads = 1;
        return options;
    }

    /// <summary>
    /// Creates session options with GPU execution provider if available
    /// </summary>
    public static SessionOptions CreateWithGpu(int deviceId = 0)
    {
        var options = CreateDefault();

        try
        {
            // Try to append CUDA execution provider
            options.AppendExecutionProvider_CUDA(deviceId);
        }
        catch (Exception)
        {
            // CUDA not available, fall back to CPU
            // No action needed, CPU is the default
        }

        return options;
    }

    /// <summary>
    /// Creates session options optimized for batch inference
    /// </summary>
    public static SessionOptions CreateBatchOptimized(int threadCount)
    {
        var options = CreateDefault();
        options.ExecutionMode = ExecutionMode.ORT_PARALLEL;
        options.InterOpNumThreads = threadCount;
        options.IntraOpNumThreads = threadCount;
        return options;
    }
}
