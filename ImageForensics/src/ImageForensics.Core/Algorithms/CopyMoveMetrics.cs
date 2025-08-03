using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using OpenCvSharp.Features2D;

namespace ImageForensics.Core.Algorithms;

public static class CopyMoveMetrics
{
    private static readonly ConcurrentDictionary<int, SIFT> SiftCache = new();

    public static float[,] ComputeCopyMoveMap(
        Mat src,
        int siftFeatures = 500,
        double loweRatio = 0.75,
        double clusterEps = 20.0,
        int clusterMinPts = 5,
        int morphOpenKernel = 3,
        int morphCloseKernel = 5,
        double normPercentile = 0.99,
        double minAreaPct = 0.001)
    {
        if (src.Channels() > 1)
            Cv2.CvtColor(src, src, ColorConversionCodes.BGR2GRAY);

        var sift = SiftCache.GetOrAdd(siftFeatures, f => SIFT.Create(f));
        using var descriptors = new Mat();
        sift.DetectAndCompute(src, null, out KeyPoint[] keypoints, descriptors);
        if (descriptors.Empty() || keypoints.Length < 2)
            return new float[src.Width, src.Height];

        using var matcher = new FlannBasedMatcher();
        var knn = matcher.KnnMatch(descriptors, descriptors, 2);
        var rawMatches = knn
            .Where(m => m.Length == 2 && m[0].QueryIdx != m[0].TrainIdx &&
                        m[0].Distance < loweRatio * m[1].Distance)
            .Select(m => m[0])
            .ToArray();
        if (rawMatches.Length < 3)
            return new float[src.Width, src.Height];

        var srcPts = rawMatches.Select(m => keypoints[m.QueryIdx].Pt).ToArray();
        int[] labels = Dbscan(srcPts, clusterEps, clusterMinPts);
        var matches = rawMatches.Where((m, i) => labels[i] >= 0).ToArray();
        if (matches.Length == 0)
            return new float[src.Width, src.Height];

        var map = new Mat(src.Size(), MatType.CV_32FC1, Scalar.All(0));
        foreach (var m in matches)
        {
            var pt = keypoints[m.QueryIdx].Pt;
            Cv2.Circle(map, (int)pt.X, (int)pt.Y, 5, Scalar.All(1), -1);
        }

        using var kOpen = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(morphOpenKernel, morphOpenKernel));
        Cv2.MorphologyEx(map, map, MorphTypes.Open, kOpen);
        using var kClose = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(morphCloseKernel, morphCloseKernel));
        Cv2.MorphologyEx(map, map, MorphTypes.Close, kClose);

        int minArea = (int)(src.Width * src.Height * minAreaPct);
        using var bin = new Mat();
        Cv2.Threshold(map, bin, 0.01, 1, ThresholdTypes.Binary);
        using var labelsMat = new Mat();
        using var stats = new Mat();
        Cv2.ConnectedComponentsWithStats(bin, labelsMat, stats, new Mat(), PixelConnectivity.Connectivity8, MatType.CV_32S);
        for (int i = 1; i < stats.Rows; i++)
        {
            int area = stats.At<int>(i, (int)ConnectedComponentsTypes.Area);
            if (area < minArea)
            {
                using var mask = new Mat();
                Cv2.Compare(labelsMat, i, mask, CmpType.EQ);
                map.SetTo(0, mask);
            }
        }

        map.GetArray(out float[] data);
        var sorted = (float[])data.Clone();
        Array.Sort(sorted);
        float q = sorted[(int)(normPercentile * (sorted.Length - 1))];
        if (q > 0)
        {
            Cv2.Divide(map, new Scalar(q), map);
            Cv2.Min(map, 1.0, map);
        }

        int w = src.Width; int h = src.Height;
        var result = new float[w, h];
        map.GetArray(out data);
        int idx = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                result[x, y] = data[idx++];
        return result;
    }

    public static float[,] ComputeCopyMoveMap(
        Bitmap img,
        int siftFeatures = 500,
        double loweRatio = 0.75,
        double clusterEps = 20.0,
        int clusterMinPts = 5,
        int morphOpenKernel = 3,
        int morphCloseKernel = 5,
        double normPercentile = 0.99,
        double minAreaPct = 0.001)
    {
        using var mat = BitmapConverter.ToMat(img);
        return ComputeCopyMoveMap(mat, siftFeatures, loweRatio, clusterEps, clusterMinPts, morphOpenKernel, morphCloseKernel, normPercentile, minAreaPct);
    }

    static int[] Dbscan(Point2f[] pts, double eps, int minPts)
    {
        int n = pts.Length;
        var labels = Enumerable.Repeat(-1, n).ToArray();
        int cluster = 0;
        for (int i = 0; i < n; i++)
        {
            if (labels[i] != -1) continue;
            var neighbors = RangeQuery(i);
            if (neighbors.Count < minPts)
            {
                labels[i] = -2;
                continue;
            }
            labels[i] = cluster;
            var queue = new Queue<int>(neighbors);
            while (queue.Count > 0)
            {
                int j = queue.Dequeue();
                if (labels[j] == -2) labels[j] = cluster;
                if (labels[j] != -1) continue;
                labels[j] = cluster;
                var neigh2 = RangeQuery(j);
                if (neigh2.Count >= minPts)
                    foreach (var k in neigh2)
                        if (labels[k] < 0)
                            queue.Enqueue(k);
            }
            cluster++;
        }
        for (int i = 0; i < n; i++)
            if (labels[i] < 0) labels[i] = -1;
        return labels;

        List<int> RangeQuery(int idx)
        {
            var res = new List<int>();
            for (int k = 0; k < n; k++)
            {
                double dx = pts[idx].X - pts[k].X;
                double dy = pts[idx].Y - pts[k].Y;
                if (dx * dx + dy * dy <= eps * eps)
                    res.Add(k);
            }
            return res;
        }
    }

    public static double ComputeRocAucPixel(byte[,] mask, float[,] map) => ElaMetrics.ComputeRocAucPixel(mask, map);
    public static double ComputePraucPixel(byte[,] mask, float[,] map) => ElaMetrics.ComputePraucPixel(mask, map);
    public static double ComputeNss(byte[,] mask, float[,] map) => ElaMetrics.ComputeNss(mask, map);
    public static double ComputeFprAt95Tpr(byte[,] mask, float[,] map) => ElaMetrics.ComputeFprAtTpr(mask, map, 0.95);
    public static double ComputeAveragePrecision(byte[,] mask, float[,] map) => ElaMetrics.ComputeAveragePrecision(mask, map);
    public static double ComputeOtsuThreshold(float[,] map) => ElaMetrics.ComputeOtsuThreshold(map);
    public static double ComputePercentileThreshold(float[,] map, double percentile = 0.95)
    {
        int w = map.GetLength(0); int h = map.GetLength(1);
        var arr = new float[w * h]; int idx = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                arr[idx++] = map[x, y];
        Array.Sort(arr);
        int pos = (int)(percentile * (arr.Length - 1));
        return arr[pos];
    }
    public static bool[,] BinarizeMap(float[,] map, double threshold) => ElaMetrics.BinarizeElaMap(map, threshold);
    public static double ComputeIoUPixel(byte[,] mask, bool[,] pred) => ElaMetrics.ComputeIoUPixel(mask, pred);
    public static double ComputeDicePixel(byte[,] mask, bool[,] pred) => ElaMetrics.ComputeDicePixel(mask, pred);
    public static double ComputeMccPixel(byte[,] mask, bool[,] pred) => ElaMetrics.ComputeMccPixel(mask, pred);
    public static double ComputeBoundaryF1(byte[,] mask, bool[,] pred) => ElaMetrics.ComputeBoundaryF1(mask, pred, 1);
    public static double ComputeRegionIoU(byte[,] mask, bool[,] pred) => ElaMetrics.ComputeRegionIoU(mask, pred);
}
