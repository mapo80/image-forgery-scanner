using System.IO;

namespace ImageForensics.Core.Models;

public record ForensicsOptions
{
    // Directories where intermediate artifacts are written. They default to
    // the system temporary folder so that callers can simply override the
    // desired paths.
    public string WorkDir          { get; init; } = Path.GetTempPath();
    public string CopyMoveMaskDir  { get; init; } = Path.GetTempPath();
    public string SplicingMapDir   { get; init; } = Path.GetTempPath();
    public string NoiseprintMapDir { get; init; } = Path.GetTempPath();
    public string MetadataMapDir   { get; init; } = Path.GetTempPath();

    // Model locations which can be overridden for custom deployments.
    public string SplicingModelPath  { get; init; } = "Models/onnx/mantranet_256x256.onnx";
    public string NoiseprintModelsDir { get; init; } = "Models/onnx/noiseprint";

    public int ElaQuality { get; init; } = 90;
    public int ElaWindowSize { get; init; } = 15;
    public double ElaK { get; init; } = 0.2;
    public int ElaMinArea { get; init; } = 50;
    public int ElaKernelSize { get; init; } = 3;

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
