namespace ImageForensics.Core.Models;

public record ForensicsOptions
{
    public int ElaQuality { get; init; } = 75;

    public int    CopyMoveFeatureCount  { get; init; } = 5000;
    public double CopyMoveMatchDistance { get; init; } = 3.0;
    public double CopyMoveRansacReproj  { get; init; } = 3.0;
    public double CopyMoveRansacProb    { get; init; } = 0.99;

    public int    SplicingInputWidth  { get; init; } = 256;
    public int    SplicingInputHeight { get; init; } = 256;

    public int    NoiseprintInputSize { get; init; } = 320;

    public string[] ExpectedCameraModels { get; init; } = new[]
    {
        "Canon EOS 80D",
        "Nikon D850"
    };

    // Weights used by the decision engine when combining the partial
    // scores into a single value.
    public double ElaWeight        { get; init; } = 1.0;
    public double CopyMoveWeight   { get; init; } = 1.0;
    public double SplicingWeight   { get; init; } = 1.0;
    public double InpaintingWeight { get; init; } = 1.0;
    public double ExifWeight       { get; init; } = 1.0;

    // Thresholds separating the three final verdict classes.
    public double CleanThreshold    { get; init; } = 0.2;
    public double TamperedThreshold { get; init; } = 0.8;

    // Enabled checks and parallelization settings.
    public ForensicsCheck EnabledChecks { get; init; } = ForensicsCheck.All;
    public int MaxParallelChecks      { get; init; } = 1;
}
