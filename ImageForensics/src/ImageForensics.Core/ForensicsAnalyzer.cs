using ImageForensics.Core.Algorithms;
using ImageForensics.Core.Models;

namespace ImageForensics.Core;

public interface IForensicsAnalyzer
{
    Task<ForensicsResult> AnalyzeAsync(string imagePath, ForensicsOptions options);
}

public class ForensicsAnalyzer : IForensicsAnalyzer
{
    public Task<ForensicsResult> AnalyzeAsync(string imagePath, ForensicsOptions options)
    {
        var (elaScore, elaMapPath) = ElaAnalyzer.Analyze(imagePath, options.WorkDir, options.ElaQuality);

        var (cmScore, cmMaskPath) = CopyMoveDetector.Analyze(
            imagePath,
            options.CopyMoveMaskDir,
            options.CopyMoveFeatureCount,
            options.CopyMoveMatchDistance,
            options.CopyMoveRansacReproj,
            options.CopyMoveRansacProb);

        string verdict = elaScore < 0.018 ? "Clean" : elaScore < 0.022 ? "Suspicious" : "Tampered";
        var result = new ForensicsResult(elaScore, elaMapPath, verdict, cmScore, cmMaskPath);
        return Task.FromResult(result);
    }
}
