namespace FluxGuard.Configuration;

/// <summary>
/// What a PII guard does with a match of one pattern, overriding the default it derives from the pattern's severity
/// (<see cref="InputGuardOptions.PIIExposureActions"/>, <see cref="OutputGuardOptions.PIILeakageActions"/>).
/// </summary>
public enum PIIAction
{
    /// <summary>The guard refuses the text: the check blocks whatever the thresholds are.</summary>
    Block = 1,

    /// <summary>The guard passes the text and reports the match: the check can be flagged, never blocked.</summary>
    Flag = 2,

    /// <summary>The match is dropped, as if the pattern did not exist.</summary>
    Ignore = 3
}
