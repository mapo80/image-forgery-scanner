using System.Collections.Generic;

namespace ImageForensics.Core.Models;

public record ForensicsResult(
    double ElaScore,
    string ElaMapPath,
    double CopyMoveScore,
    string CopyMoveMaskPath)
{
    public double SplicingScore { get; init; }
    public string SplicingMapPath { get; init; } = string.Empty;

    public double InpaintingScore { get; init; }
    public string InpaintingMapPath { get; init; } = string.Empty;

    public double ExifScore { get; init; }
    public IReadOnlyDictionary<string, string?> ExifAnomalies { get; init; } = new Dictionary<string, string?>();

    // Errors produced by individual checks
    public IReadOnlyDictionary<string, string> Errors { get; init; } = new Dictionary<string, string>();

    // Aggregated score produced by the decision engine
    // combining all individual detectors.
    public double TotalScore { get; init; }
    public string Verdict { get; init; } = string.Empty;
}
