using ImageForensics.Core.Models;

namespace ImageForensics.Core.Algorithms;

/// <summary>
/// Combines the partial detector scores into a single value and
/// assigns a textual verdict.
/// </summary>
public static class DecisionEngine
{
    public static (double Score, string Verdict) Decide(ForensicsResult result, ForensicsOptions options)
    {
        double score =
            result.ElaScore         * options.ElaWeight +
            result.CopyMoveScore    * options.CopyMoveWeight +
            result.SplicingScore    * options.SplicingWeight +
            result.InpaintingScore  * options.InpaintingWeight +
            result.ExifScore        * options.ExifWeight;

        string verdict = score < options.CleanThreshold
            ? "Clean"
            : score < options.TamperedThreshold
                ? "Suspicious"
                : "Tampered";

        return (score, verdict);
    }
}
