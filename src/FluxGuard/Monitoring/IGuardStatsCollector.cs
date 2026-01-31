using FluxGuard.Core;

namespace FluxGuard.Monitoring;

/// <summary>
/// Interface for collecting guard statistics
/// </summary>
public interface IGuardStatsCollector
{
    /// <summary>
    /// Record a check result
    /// </summary>
    /// <param name="result">Guard result</param>
    /// <param name="isInput">True if input check, false if output check</param>
    void RecordCheck(GuardResult result, bool isInput);

    /// <summary>
    /// Record a guard trigger
    /// </summary>
    /// <param name="guardName">Guard name</param>
    /// <param name="layer">Guard layer</param>
    /// <param name="latencyMs">Latency in milliseconds</param>
    /// <param name="triggered">Whether guard triggered</param>
    void RecordGuardExecution(string guardName, string layer, double latencyMs, bool triggered);

    /// <summary>
    /// Record a guard error
    /// </summary>
    /// <param name="guardName">Guard name</param>
    /// <param name="layer">Guard layer</param>
    void RecordGuardError(string guardName, string layer);

    /// <summary>
    /// Get current statistics
    /// </summary>
    /// <returns>Current guard statistics</returns>
    GuardStats GetStats();

    /// <summary>
    /// Reset all statistics
    /// </summary>
    void Reset();
}
