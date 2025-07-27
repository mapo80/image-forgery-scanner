namespace ImageForensics.Core.Models;

public record ForensicsOptions
{
    public int ElaQuality { get; init; } = 75;
    public string WorkDir { get; init; } = "results";

    public int    CopyMoveFeatureCount  { get; init; } = 5000;
    public double CopyMoveMatchDistance { get; init; } = 3.0;
    public double CopyMoveRansacReproj  { get; init; } = 3.0;
    public double CopyMoveRansacProb    { get; init; } = 0.99;
    public string CopyMoveMaskDir       { get; init; } = "results";
}
