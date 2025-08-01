using System.IO;
using System.Linq;
using System.Collections.Concurrent;
using OpenCvSharp;
using OpenCvSharp.Features2D;
using Serilog;

namespace ImageForensics.Core.Algorithms;

public static class CopyMoveDetector
{
    private static readonly ConcurrentDictionary<int, SIFT> SiftCache = new();

    public static (double Score, string MaskPath) Analyze(
        string imagePath,
        string maskDir,
        int featureCount,
        double matchMaxDist,
        double ransacReprojThresh,
        double ransacConfidence)
    {
        Log.Information("Copy-move analysis for {Image}", imagePath);
        Directory.CreateDirectory(maskDir);
        string baseName = Path.GetFileNameWithoutExtension(imagePath);
        string maskPath = Path.Combine(maskDir, $"{baseName}_copymove.png");

        using var img = Cv2.ImRead(imagePath, ImreadModes.Grayscale);
        if (img.Empty())
            return (0.0, maskPath);
        Log.Debug("Image loaded {Width}x{Height}", img.Width, img.Height);

        var sift = SiftCache.GetOrAdd(featureCount, f => SIFT.Create(f));
        using var descriptors = new Mat();
        sift.DetectAndCompute(img, null, out KeyPoint[] keypoints, descriptors);
        Log.Debug("Detected {Count} keypoints", keypoints.Length);

        if (descriptors.Empty() || keypoints.Length < 2)
        {
            Cv2.ImWrite(maskPath, new Mat(img.Size(), MatType.CV_8UC1, Scalar.Black));
            Log.Debug("Not enough keypoints, empty mask written to {MaskPath}", maskPath);
            return (0.0, maskPath);
        }

        using var matcher = new FlannBasedMatcher();
        var knn = matcher.KnnMatch(descriptors, descriptors, 2);
        var matches = knn
            .Where(m => m.Length == 2 &&
                        m[0].QueryIdx != m[0].TrainIdx &&
                        m[0].Distance < matchMaxDist &&
                        m[0].Distance < 0.75 * m[1].Distance)
            .Select(m => m[0])
            .ToArray();
        Log.Debug("Found {Count} candidate matches", matches.Length);

        if (matches.Length < 3)
        {
            Cv2.ImWrite(maskPath, new Mat(img.Size(), MatType.CV_8UC1, Scalar.Black));
            Log.Debug("Not enough matches, empty mask written to {MaskPath}", maskPath);
            return (0.0, maskPath);
        }

        var srcPts = matches.Select(m => keypoints[m.QueryIdx].Pt).ToArray();
        var dstPts = matches.Select(m => keypoints[m.TrainIdx].Pt).ToArray();

        using var srcArr = InputArray.Create(srcPts);
        using var dstArr = InputArray.Create(dstPts);
        using var inliers = new Mat();
        Cv2.EstimateAffine2D(srcArr, dstArr, inliers, RobustEstimationAlgorithms.RANSAC,
            ransacReprojThresh, 2000, ransacConfidence, 10);

        inliers.GetArray(out byte[] maskData);
        int inlierCount = maskData.Count(b => b != 0);
        Log.Debug("Inlier count {InlierCount}", inlierCount);

        using var mask = new Mat(img.Size(), MatType.CV_8UC1, Scalar.Black);
        for (int i = 0; i < maskData.Length; i++)
        {
            if (maskData[i] != 0)
            {
                var pt = srcPts[i];
                Cv2.Circle(mask, (int)pt.X, (int)pt.Y, 5, Scalar.White, -1);
            }
        }

        Cv2.ImWrite(maskPath, mask);
        Log.Debug("Mask written to {MaskPath}", maskPath);
        double score = inlierCount / (double)matches.Length;
        Log.Information("Copy-move completed for {Image}: {Score}", imagePath, score);
        return (score, maskPath);
    }
}
