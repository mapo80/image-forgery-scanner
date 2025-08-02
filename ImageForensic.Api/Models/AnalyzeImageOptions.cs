using ImageForensics.Core.Models;

namespace ImageForensic.Api;

public record AnalyzeImageOptions
{
    public int ElaQuality { get; init; } = 90;
    public int ElaWindowSize { get; init; } = 15;
    public double ElaK { get; init; } = 0.2;
    public int ElaMinArea { get; init; } = 50;
    public int ElaKernelSize { get; init; } = 3;

    public int CopyMoveFeatureCount { get; init; } = 5000;
    public double CopyMoveMatchDistance { get; init; } = 3.0;
    public double CopyMoveRansacReproj { get; init; } = 3.0;
    public double CopyMoveRansacProb { get; init; } = 0.99;

    public string[] ExpectedCameraModels { get; init; } = new[] { "Canon EOS 80D", "Nikon D850" };

    public double ElaWeight { get; init; } = 1.0;
    public double CopyMoveWeight { get; init; } = 1.0;
    public double InpaintingWeight { get; init; } = 1.0;
    public double ExifWeight { get; init; } = 1.0;

    public double CleanThreshold { get; init; } = 0.2;
    public double TamperedThreshold { get; init; } = 0.8;

    public ForensicsCheck EnabledChecks { get; init; } = ForensicsCheck.All;
    public int MaxParallelChecks { get; init; } = 1;

    public ForensicsOptions ToForensicsOptions() => new()
    {
        ElaQuality = ElaQuality > 0 ? ElaQuality : 90,
        ElaWindowSize = ElaWindowSize > 0 ? ElaWindowSize : 15,
        ElaK = ElaK != 0 ? ElaK : 0.2,
        ElaMinArea = ElaMinArea > 0 ? ElaMinArea : 50,
        ElaKernelSize = ElaKernelSize > 0 ? ElaKernelSize : 3,
        CopyMoveFeatureCount = CopyMoveFeatureCount,
        CopyMoveMatchDistance = CopyMoveMatchDistance,
        CopyMoveRansacReproj = CopyMoveRansacReproj,
        CopyMoveRansacProb = CopyMoveRansacProb,
        ExpectedCameraModels = ExpectedCameraModels,
        ElaWeight = ElaWeight,
        CopyMoveWeight = CopyMoveWeight,
        InpaintingWeight = InpaintingWeight,
        ExifWeight = ExifWeight,
        CleanThreshold = CleanThreshold,
        TamperedThreshold = TamperedThreshold,
        EnabledChecks = EnabledChecks,
        MaxParallelChecks = MaxParallelChecks
    };
}

