namespace ImageForensics.Core.Models;

public record ForensicsResult(
    double ElaScore,
    string ElaMapPath,
    string Verdict,
    double CopyMoveScore,
    string CopyMoveMaskPath)
{
    public double SplicingScore   { get; init; }
    public string SplicingMapPath { get; init; } = string.Empty;
}
