using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using OpenCvSharp;
using OpenCvSharp.Features2D;

namespace ImageForensics.Core.Algorithms;

public static class CopyMoveMetrics
{
    private static readonly ConcurrentDictionary<int, SIFT> SiftCache = new();

    public static float[,] ComputeCopyMoveMap(Mat img, int featureCount, double matchMaxDist, double ransacReprojThresh, double ransacConfidence, int minArea, int kernelSize)
    {
        var sift = SiftCache.GetOrAdd(featureCount, f => SIFT.Create(f));
        using var descriptors = new Mat();
        sift.DetectAndCompute(img, null, out KeyPoint[] keypoints, descriptors);
        if (descriptors.Empty() || keypoints.Length < 2)
            return new float[img.Width, img.Height];

        using var matcher = new FlannBasedMatcher();
        var knn = matcher.KnnMatch(descriptors, descriptors, 2);
        var matches = knn
            .Where(m => m.Length == 2 &&
                        m[0].QueryIdx != m[0].TrainIdx &&
                        m[0].Distance < matchMaxDist &&
                        m[0].Distance < 0.75 * m[1].Distance)
            .Select(m => m[0])
            .ToArray();
        if (matches.Length < 3)
            return new float[img.Width, img.Height];

        var srcPts = matches.Select(m => keypoints[m.QueryIdx].Pt).ToArray();
        var dstPts = matches.Select(m => keypoints[m.TrainIdx].Pt).ToArray();
        using var srcArr = InputArray.Create(srcPts);
        using var dstArr = InputArray.Create(dstPts);
        using var inliers = new Mat();
        Cv2.EstimateAffine2D(srcArr, dstArr, inliers, RobustEstimationAlgorithms.RANSAC,
            ransacReprojThresh, 2000, ransacConfidence, 10);
        inliers.GetArray(out byte[] maskData);

        var map = new Mat(img.Size(), MatType.CV_32FC1, Scalar.All(0));
        for (int i = 0; i < maskData.Length; i++)
        {
            if (maskData[i] != 0)
            {
                var pt = srcPts[i];
                Cv2.Circle(map, (int)pt.X, (int)pt.Y, 5, Scalar.All(1), -1);
            }
        }

        if (kernelSize > 1)
        {
            using var k = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(kernelSize, kernelSize));
            Cv2.MorphologyEx(map, map, MorphTypes.Close, k);
        }

        Cv2.GaussianBlur(map, map, new Size(5, 5), 0);
        Cv2.Normalize(map, map, 0, 1, NormTypes.MinMax);

        using var bin = new Mat();
        Cv2.Threshold(map, bin, 0.01, 1, ThresholdTypes.Binary);
        using var labels = new Mat();
        using var stats = new Mat();
        Cv2.ConnectedComponentsWithStats(bin, labels, stats, new Mat(), PixelConnectivity.Connectivity8, MatType.CV_32S);
        for (int i = 1; i < stats.Rows; i++)
        {
            int area = stats.At<int>(i, (int)ConnectedComponentsTypes.Area);
            if (area < minArea)
            {
                using var mask = new Mat();
                Cv2.Compare(labels, i, mask, CmpType.EQ);
                map.SetTo(0, mask);
            }
        }
        Cv2.Normalize(map, map, 0, 1, NormTypes.MinMax);

        int w = img.Width; int h = img.Height;
        var result = new float[w, h];
        map.GetArray(out float[] data);
        int idx = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                result[x, y] = data[idx++];
        return result;
    }

    public static double ComputeRocAucPixel(byte[,] mask, float[,] map) => ElaMetrics.ComputeRocAucPixel(mask, map);
    public static double ComputePraucPixel(byte[,] mask, float[,] map) => ElaMetrics.ComputePraucPixel(mask, map);
    public static double ComputeNss(byte[,] mask, float[,] map) => ElaMetrics.ComputeNss(mask, map);
    public static double ComputeFprAtTpr(byte[,] mask, float[,] map, double tpr = 0.95) => ElaMetrics.ComputeFprAtTpr(mask, map, tpr);
    public static double ComputeAveragePrecision(byte[,] mask, float[,] map) => ElaMetrics.ComputeAveragePrecision(mask, map);
    public static double ComputeOtsuThreshold(float[,] map) => ElaMetrics.ComputeOtsuThreshold(map);
    public static bool[,] BinarizeMap(float[,] map, double threshold) => ElaMetrics.BinarizeElaMap(map, threshold);
    public static double ComputeIoUPixel(byte[,] mask, bool[,] pred) => ElaMetrics.ComputeIoUPixel(mask, pred);
    public static double ComputeDicePixel(byte[,] mask, bool[,] pred) => ElaMetrics.ComputeDicePixel(mask, pred);
    public static double ComputeMccPixel(byte[,] mask, bool[,] pred) => ElaMetrics.ComputeMccPixel(mask, pred);
    public static double ComputeBoundaryF1(byte[,] mask, bool[,] pred) => ElaMetrics.ComputeBoundaryF1(mask, pred, 1);
    public static double ComputeRegionIoU(byte[,] mask, bool[,] pred) => ElaMetrics.ComputeRegionIoU(mask, pred);
}
