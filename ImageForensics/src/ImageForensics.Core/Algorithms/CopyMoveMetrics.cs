using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using OpenCvSharp.Features2D;

namespace ImageForensics.Core.Algorithms;

public static class CopyMoveMetrics
{
    private static readonly ConcurrentDictionary<int, SIFT> SiftCache = new();

    public static Mat ComputeCopyMoveMap(
        Mat src,
        int siftFeatures = 500,
        double loweRatio = 0.75,
        double clusterEps = 20.0,
        int clusterMinPts = 5,
        double minClusterSizePct = 0.001,
        int morphOpenKernel = 3,
        int morphCloseKernel = 5,
        double normPercentile = 0.99,
        double minAreaPct = 0.001)
    {
        using var gray = new Mat();
        if (src.Channels() > 1)
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        else
            src.CopyTo(gray);

        var empty = new Mat(gray.Size(), MatType.CV_32F, Scalar.All(0));

        var sift = SiftCache.GetOrAdd(siftFeatures, f => SIFT.Create(f));
        using var descriptors = new Mat();
        sift.DetectAndCompute(gray, null, out KeyPoint[] keypoints, descriptors);
        if (descriptors.Empty() || keypoints.Length < 2)
            return empty;

        using var matcher = new FlannBasedMatcher();
        var knn = matcher.KnnMatch(descriptors, descriptors, 2);
        var rawMatches = knn
            .Where(m => m.Length == 2 && m[0].QueryIdx != m[0].TrainIdx &&
                        m[0].Distance < loweRatio * m[1].Distance)
            .Select(m => m[0])
            .ToArray();
        if (rawMatches.Length < 3)
            return empty;

        var pts = rawMatches.Select(m => keypoints[m.QueryIdx].Pt).ToArray();
        int[] labels = Dbscan(pts, clusterEps, clusterMinPts);

        // filter clusters below area threshold
        double imgArea = gray.Width * gray.Height;
        double minClusterArea = imgArea * minClusterSizePct;
        const double circleArea = Math.PI * 25.0; // radius 5 circles
        var clusterCounts = new Dictionary<int, int>();
        for (int i = 0; i < labels.Length; i++)
        {
            int l = labels[i];
            if (l >= 0)
            {
                clusterCounts.TryGetValue(l, out int c);
                clusterCounts[l] = c + 1;
            }
        }
        var validClusters = clusterCounts
            .Where(kv => kv.Value * circleArea >= minClusterArea)
            .Select(kv => kv.Key)
            .ToHashSet();

        var matches = rawMatches
            .Where((m, i) => labels[i] >= 0 && validClusters.Contains(labels[i]))
            .ToArray();
        if (matches.Length == 0)
            return empty;

        var map = new Mat(gray.Size(), MatType.CV_32F, Scalar.All(0));
        foreach (var m in matches)
        {
            var pt = keypoints[m.QueryIdx].Pt;
            Cv2.Circle(map, (int)pt.X, (int)pt.Y, 5, Scalar.All(1), -1);
        }

        using var kOpen = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(morphOpenKernel, morphOpenKernel));
        Cv2.MorphologyEx(map, map, MorphTypes.Open, kOpen);
        using var kClose = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(morphCloseKernel, morphCloseKernel));
        Cv2.MorphologyEx(map, map, MorphTypes.Close, kClose);
        using var kClose2 = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(morphCloseKernel + 2, morphCloseKernel + 2));
        Cv2.MorphologyEx(map, map, MorphTypes.Close, kClose2);

        int minArea = (int)(gray.Width * gray.Height * minAreaPct);
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

        using var flat = map.Reshape(1, map.Rows * map.Cols);
        Cv2.Sort(flat, flat, SortFlags.Ascending);
        long total = flat.Total();
        int idx = (int)Math.Clamp((long)(normPercentile * (total - 1)), 0, total - 1);
        float q = flat.Get<float>(idx);
        double norm = q;
        if (norm <= 0)
        {
            Cv2.MinMaxLoc(map, out _, out double maxVal);
            norm = maxVal;
        }
        if (norm <= 0)
        {
            Console.WriteLine("Warning: empty copy-move map");
            map.SetTo(0);
        }
        else
        {
            Cv2.Divide(map, norm, map);
            Cv2.Min(map, 1.0, map);
        }

        return map;
    }

    public static (double Min, double Max, double Mean, double Median, double Q95) GetMapStats(Mat map)
    {
        Cv2.MinMaxLoc(map, out double minVal, out double maxVal);
        double mean = Cv2.Mean(map).Val0;
        using var flat = map.Reshape(1, map.Rows * map.Cols);
        Cv2.Sort(flat, flat, SortFlags.Ascending);
        long total = flat.Total();
        int mid = (int)Math.Clamp((total - 1) / 2, 0, total - 1);
        int idx95 = (int)Math.Clamp((long)(0.95 * (total - 1)), 0, total - 1);
        double median = flat.Get<float>(mid);
        double q95 = flat.Get<float>(idx95);
        return (minVal, maxVal, mean, median, q95);
    }

    public enum ThresholdMode
    {
        Otsu,
        Percentile,
        Fixed
    }

    public static double SelectThreshold(Mat map, ThresholdMode mode, double percentile = 0.50, double fixedValue = 0.20)
    {
        return mode switch
        {
            ThresholdMode.Percentile => ComputePercentileThreshold(map, percentile),
            ThresholdMode.Fixed => fixedValue,
            _ => ComputeOtsuThreshold(map)
        };
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

    public static double ComputeRocAucPixel(Mat mask, Mat map)
    {
        ToArrays(mask, map, out var m, out var f);
        return ElaMetrics.ComputeRocAucPixel(m, f);
    }

    public static double ComputePraucPixel(Mat mask, Mat map)
    {
        ToArrays(mask, map, out var m, out var f);
        return ElaMetrics.ComputePraucPixel(m, f);
    }

    public static double ComputeNss(Mat mask, Mat map)
    {
        ToArrays(mask, map, out var m, out var f);
        return ElaMetrics.ComputeNss(m, f);
    }

    public static double ComputeFprAt95Tpr(Mat mask, Mat map)
    {
        ToArrays(mask, map, out var m, out var f);
        return ElaMetrics.ComputeFprAtTpr(m, f, 0.95);
    }

    public static double ComputeAveragePrecision(Mat mask, Mat map)
    {
        ToArrays(mask, map, out var m, out var f);
        return ElaMetrics.ComputeAveragePrecision(m, f);
    }

    public static double ComputeOtsuThreshold(Mat map)
    {
        using var tmp = new Mat();
        map.ConvertTo(tmp, MatType.CV_8U, 255);
        double t = Cv2.Threshold(tmp, new Mat(), 0, 255, ThresholdTypes.Otsu);
        return t / 255.0;
    }

    public static double ComputePercentileThreshold(Mat map, double percentile = 0.95)
    {
        using var flat = map.Reshape(1, map.Rows * map.Cols);
        Cv2.Sort(flat, flat, SortFlags.Ascending);
        long total = flat.Total();
        int idx = (int)Math.Clamp((long)(percentile * (total - 1)), 0, total - 1);
        return flat.Get<float>(idx);
    }

    public static Mat BinarizeMap(Mat map, double threshold)
    {
        var binary = new Mat();
        Cv2.Threshold(map, binary, threshold, 255, ThresholdTypes.Binary);
        binary.ConvertTo(binary, MatType.CV_8U);
        return binary;
    }

    public static double ComputeIoUPixel(Mat mask, Mat pred)
    {
        ToBinaryArrays(mask, pred, out var m, out var p);
        return ElaMetrics.ComputeIoUPixel(m, p);
    }

    public static double ComputeDicePixel(Mat mask, Mat pred)
    {
        ToBinaryArrays(mask, pred, out var m, out var p);
        return ElaMetrics.ComputeDicePixel(m, p);
    }

    public static double ComputeMccPixel(Mat mask, Mat pred)
    {
        ToBinaryArrays(mask, pred, out var m, out var p);
        return ElaMetrics.ComputeMccPixel(m, p);
    }

    public static double ComputeBoundaryF1(Mat mask, Mat pred)
    {
        ToBinaryArrays(mask, pred, out var m, out var p);
        return ElaMetrics.ComputeBoundaryF1(m, p, 1);
    }

    public static double ComputeRegionIoU(Mat mask, Mat pred)
    {
        ToBinaryArrays(mask, pred, out var m, out var p);
        return ElaMetrics.ComputeRegionIoU(m, p);
    }

    static void ToArrays(Mat mask, Mat map, out byte[,] m, out float[,] f)
    {
        int w = map.Width; int h = map.Height;
        m = new byte[w, h];
        f = new float[w, h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                m[x, y] = mask.At<byte>(y, x) > 0 ? (byte)1 : (byte)0;
                f[x, y] = map.At<float>(y, x);
            }
        }
    }

    static void ToBinaryArrays(Mat mask, Mat pred, out byte[,] m, out bool[,] p)
    {
        int w = mask.Width; int h = mask.Height;
        m = new byte[w, h];
        p = new bool[w, h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                m[x, y] = mask.At<byte>(y, x) > 0 ? (byte)1 : (byte)0;
                p[x, y] = pred.At<byte>(y, x) > 0;
            }
        }
    }
}

