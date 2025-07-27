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
        var (score, mapPath) = ElaAnalyzer.Analyze(imagePath, options.WorkDir, options.ElaQuality);

        string verdict = score < 0.15 ? "Clean" : score < 0.35 ? "Suspicious" : "Tampered";
        var result = new ForensicsResult(score, mapPath, verdict);
        return Task.FromResult(result);
    }
}
