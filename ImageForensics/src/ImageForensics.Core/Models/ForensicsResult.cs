using System;
using System.Collections.Generic;

namespace ImageForensics.Core.Models;

public record ForensicsResult(
    double ElaScore,
    byte[] ElaMap,
    string Verdict,
    double CopyMoveScore,
    byte[] CopyMoveMask)
{
    public double SplicingScore   { get; init; }
    public byte[] SplicingMap { get; init; } = Array.Empty<byte>();
    public double InpaintingScore   { get; init; }
    public byte[] InpaintingMap { get; init; } = Array.Empty<byte>();

    public double ExifScore { get; init; }
    public IReadOnlyDictionary<string, string?> ExifAnomalies { get; init; } = new Dictionary<string, string?>();

    // Aggregated score produced by the decision engine
    // combining all individual detectors.
    public double TotalScore { get; init; }
}
