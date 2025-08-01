using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ImageMagick;
using OpenCvSharp;

namespace ImageForensics.Core.Algorithms;

public record FeatureVector(
    float Ela1,
    float Ela2,
    float Ela3,
    float Gradient,
    float LoG,
    float LocalVar
);

public class RefineParams
{
    public int MinArea { get; set; }
    public int KernelSize { get; set; }
}

public class RefineResult
{
    public RefineParams Params { get; }
    public bool[,] Mask { get; }
    public double Metric { get; }
    public RefineResult(RefineParams p, bool[,] mask, double metric)
    {
        Params = p;
        Mask = mask;
        Metric = metric;
    }
}

public class RegionScore
{
    public int Label { get; }
    public double Score { get; }
    public Rect BoundingBox { get; }
    public double Area { get; }
    public RegionScore(int label, double score, Rect box, double area)
    {
        Label = label;
        Score = score;
        BoundingBox = box;
        Area = area;
    }
}

public static class ElaAdvanced
{
    public static float[][,] ComputeMultiScaleElaMap(MagickImage img, int[] jpegQualities)
    {
        var maps = new float[jpegQualities.Length][,];
        for (int i = 0; i < jpegQualities.Length; i++)
        {
            maps[i] = ElaMetrics.ComputeElaMap(img, jpegQualities[i]);
        }
        return maps;
    }

    public static IEnumerable<FeatureVector> ExtractElaFeatures(float[][,] elaMaps)
    {
        int w = elaMaps[0].GetLength(0);
        int h = elaMaps[0].GetLength(1);
        using var mat = new Mat(h, w, MatType.CV_32F);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                mat.Set(y, x, elaMaps[0][x, y]);
        using var gradX = new Mat();
        using var gradY = new Mat();
        Cv2.Sobel(mat, gradX, MatType.CV_32F, 1, 0);
        Cv2.Sobel(mat, gradY, MatType.CV_32F, 0, 1);
        using var gradMag = new Mat();
        Cv2.Magnitude(gradX, gradY, gradMag);
        using var blur = new Mat();
        Cv2.GaussianBlur(mat, blur, new OpenCvSharp.Size(3, 3), 0);
        using var lap = new Mat();
        Cv2.Laplacian(blur, lap, MatType.CV_32F);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float grad = gradMag.Get<float>(y, x);
                float lg = lap.Get<float>(y, x);
                float mean = 0, var = 0; int count = 0;
                for (int j = -1; j <= 1; j++)
                    for (int i = -1; i <= 1; i++)
                    {
                        int nx = x + i, ny = y + j;
                        if (nx >= 0 && nx < w && ny >= 0 && ny < h)
                        {
                            float v = elaMaps[0][nx, ny];
                            mean += v; count++;
                        }
                    }
                mean /= Math.Max(1, count);
                for (int j = -1; j <= 1; j++)
                    for (int i = -1; i <= 1; i++)
                    {
                        int nx = x + i, ny = y + j;
                        if (nx >= 0 && nx < w && ny >= 0 && ny < h)
                        {
                            float v = elaMaps[0][nx, ny];
                            var += (v - mean) * (v - mean);
                        }
                    }
                var /= Math.Max(1, count);
                float ela1 = elaMaps.Length > 0 ? elaMaps[0][x, y] : 0;
                float ela2 = elaMaps.Length > 1 ? elaMaps[1][x, y] : 0;
                float ela3 = elaMaps.Length > 2 ? elaMaps[2][x, y] : 0;
                yield return new FeatureVector(ela1, ela2, ela3, grad, lg, var);
            }
        }
    }

    public static double ComputeOtsuThreshold(float[,] elaMap) => ElaMetrics.ComputeOtsuThreshold(elaMap);

    public static bool[,] BinarizeElaMap(float[,] elaMap, double threshold) => ElaMetrics.BinarizeElaMap(elaMap, threshold);

    public static bool[,] RefineMask(bool[,] rawMask, int minArea = 50, int kernelSize = 3)
    {
        int w = rawMask.GetLength(0);
        int h = rawMask.GetLength(1);
        using var mat = new Mat(h, w, MatType.CV_8UC1);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                mat.Set(y, x, rawMask[x, y] ? (byte)255 : (byte)0);
        using var labels = new Mat();
        using var stats = new Mat();
        using var cents = new Mat();
        Cv2.ConnectedComponentsWithStats(mat, labels, stats, cents);
        for (int i = 1; i < stats.Rows; i++)
        {
            int area = stats.Get<int>(i, (int)ConnectedComponentsTypes.Area);
            if (area < minArea)
            {
                using var cmp = new Mat();
                Cv2.Compare(labels, i, cmp, CmpType.EQ);
                mat.SetTo(Scalar.All(0), cmp);
            }
        }
        var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(kernelSize, kernelSize));
        Cv2.MorphologyEx(mat, mat, MorphTypes.Close, kernel);
        Cv2.MorphologyEx(mat, mat, MorphTypes.Open, kernel);
        var result = new bool[w, h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                result[x, y] = mat.Get<byte>(y, x) > 0;
        return result;
    }

    public static double ComputeSauvolaThreshold(float[,] elaMap, int windowSize, double k)
    {
        int w = elaMap.GetLength(0);
        int h = elaMap.GetLength(1);
        int r = windowSize / 2;
        double R = 1.0;
        double sumT = 0;
        int count = 0;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int x0 = Math.Max(0, x - r);
                int x1 = Math.Min(w - 1, x + r);
                int y0 = Math.Max(0, y - r);
                int y1 = Math.Min(h - 1, y + r);
                double m = 0, s = 0; int n = 0;
                for (int yy = y0; yy <= y1; yy++)
                    for (int xx = x0; xx <= x1; xx++)
                    {
                        double v = elaMap[xx, yy];
                        m += v; n++;
                    }
                m /= Math.Max(1, n);
                for (int yy = y0; yy <= y1; yy++)
                    for (int xx = x0; xx <= x1; xx++)
                        s += Math.Pow(elaMap[xx, yy] - m, 2);
                s = Math.Sqrt(s / Math.Max(1, n));
                double t = m * (1 + k * (s / R - 1));
                sumT += t; count++;
            }
        }
        return count == 0 ? 0 : sumT / count;
    }

    public static IEnumerable<double> SweepThresholds(float[,] elaMap, double tMin, double tMax, double step, Func<bool[,], double> metricFunc)
    {
        var scored = new List<(double thr, double score)>();
        for (double t = tMin; t <= tMax; t += step)
        {
            var mask = BinarizeElaMap(elaMap, t);
            double metric = metricFunc(mask);
            scored.Add((t, metric));
        }
        return scored.OrderByDescending(s => s.score).Select(s => s.thr);
    }

    public static double[,] ComputeMultiBlockOtsu(float[,] elaMap, int blocksX, int blocksY)
    {
        int w = elaMap.GetLength(0);
        int h = elaMap.GetLength(1);
        var map = new double[blocksX, blocksY];
        int bw = Math.Max(1, w / blocksX);
        int bh = Math.Max(1, h / blocksY);
        for (int by = 0; by < blocksY; by++)
        {
            for (int bx = 0; bx < blocksX; bx++)
            {
                int x0 = bx * bw;
                int y0 = by * bh;
                int x1 = bx == blocksX - 1 ? w : x0 + bw;
                int y1 = by == blocksY - 1 ? h : y0 + bh;
                var block = new float[x1 - x0, y1 - y0];
                for (int yy = y0; yy < y1; yy++)
                    for (int xx = x0; xx < x1; xx++)
                        block[xx - x0, yy - y0] = elaMap[xx, yy];
                map[bx, by] = ElaMetrics.ComputeOtsuThreshold(block);
            }
        }
        return map;
    }

    public static IEnumerable<RefineResult> SweepMorphology(bool[,] rawMask, IEnumerable<RefineParams> paramSpace, Func<bool[,], double> metricFunc)
    {
        var results = new List<RefineResult>();
        foreach (var p in paramSpace)
        {
            var refined = RefineMask(rawMask, p.MinArea, p.KernelSize);
            double metric = metricFunc(refined);
            results.Add(new RefineResult(p, refined, metric));
        }
        return results.OrderByDescending(r => r.Metric);
    }

    public static List<RegionScore> RankRegions(bool[,] mask, float[,] elaMap)
    {
        int w = mask.GetLength(0);
        int h = mask.GetLength(1);
        using var mat = new Mat(h, w, MatType.CV_8UC1);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                mat.Set(y, x, mask[x, y] ? (byte)255 : (byte)0);
        using var labels = new Mat();
        using var stats = new Mat();
        using var cents = new Mat();
        Cv2.ConnectedComponentsWithStats(mat, labels, stats, cents);
        var list = new List<RegionScore>();
        for (int i = 1; i < stats.Rows; i++)
        {
            int x = stats.Get<int>(i, (int)ConnectedComponentsTypes.Left);
            int y = stats.Get<int>(i, (int)ConnectedComponentsTypes.Top);
            int bw = stats.Get<int>(i, (int)ConnectedComponentsTypes.Width);
            int bh = stats.Get<int>(i, (int)ConnectedComponentsTypes.Height);
            int area = stats.Get<int>(i, (int)ConnectedComponentsTypes.Area);
            double sum = 0;
            for (int yy = y; yy < y + bh; yy++)
                for (int xx = x; xx < x + bw; xx++)
                    if (labels.Get<int>(yy, xx) == i)
                        sum += elaMap[xx, yy];
            double mean = area > 0 ? sum / area : 0;
            list.Add(new RegionScore(i, mean, new Rect(x, y, bw, bh), area));
        }
        return list.OrderByDescending(r => r.Score).ToList();
    }

    public static double ComputeRocAucPixel(byte[,] gt, float[,] elaMap) =>
        ElaMetrics.ComputeRocAucPixel(gt, elaMap);

    public static double ComputePraucPixel(byte[,] gt, float[,] elaMap) =>
        ElaMetrics.ComputePraucPixel(gt, elaMap);

    public static double ComputeNss(byte[,] gt, float[,] elaMap) => ElaMetrics.ComputeNss(gt, elaMap);

    public static double ComputeFprAtTpr(byte[,] gt, float[,] elaMap, double tprTarget) =>
        ElaMetrics.ComputeFprAtTpr(gt, elaMap, tprTarget);

    public static double ComputeAveragePrecision(byte[,] gt, float[,] scoresMap) =>
        ElaMetrics.ComputeAveragePrecision(gt, scoresMap);

    public static double ComputeRegionIoU(byte[,] gt, bool[,] predMask)
    {
        int w = gt.GetLength(0);
        int h = gt.GetLength(1);
        using var gtMat = new Mat(h, w, MatType.CV_8UC1);
        using var prMat = new Mat(h, w, MatType.CV_8UC1);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                gtMat.Set(y, x, gt[x, y] > 0 ? (byte)255 : (byte)0);
                prMat.Set(y, x, predMask[x, y] ? (byte)255 : (byte)0);
            }
        using var gtLabels = new Mat();
        int gtCount = Cv2.ConnectedComponents(gtMat, gtLabels);
        using var prLabels = new Mat();
        int prCount = Cv2.ConnectedComponents(prMat, prLabels);
        var ious = new List<double>();
        for (int p = 1; p < prCount; p++)
        {
            using var prMaskBin = new Mat();
            Cv2.Compare(prLabels, p, prMaskBin, CmpType.EQ);
            int prArea = Cv2.CountNonZero(prMaskBin);
            double best = 0;
            for (int g = 1; g < gtCount; g++)
            {
                using var gtMaskBin = new Mat();
                Cv2.Compare(gtLabels, g, gtMaskBin, CmpType.EQ);
                int gtArea = Cv2.CountNonZero(gtMaskBin);
                using var inter = new Mat();
                Cv2.BitwiseAnd(prMaskBin, gtMaskBin, inter);
                int interArea = Cv2.CountNonZero(inter);
                int union = prArea + gtArea - interArea;
                if (union > 0)
                {
                    double iou = (double)interArea / union;
                    if (iou > best) best = iou;
                }
            }
            ious.Add(best);
        }
        return ious.Count == 0 ? 0 : ious.Average();
    }

    public static double ComputeBoundaryF1(byte[,] gt, bool[,] predMask, int tol) =>
        ElaMetrics.ComputeBoundaryF1(gt, predMask, tol);

    public static double ComputeObjectF1(byte[,] gt, bool[,] predMask)
    {
        int w = gt.GetLength(0);
        int h = gt.GetLength(1);
        using var gtMat = new Mat(h, w, MatType.CV_8UC1);
        using var prMat = new Mat(h, w, MatType.CV_8UC1);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                gtMat.Set(y, x, gt[x, y] > 0 ? (byte)255 : (byte)0);
                prMat.Set(y, x, predMask[x, y] ? (byte)255 : (byte)0);
            }
        using var gtLabels = new Mat();
        int gtCount = Cv2.ConnectedComponents(gtMat, gtLabels);
        using var prLabels = new Mat();
        int prCount = Cv2.ConnectedComponents(prMat, prLabels);
        int tp = 0; int fp = 0; int fn = 0;
        var matchedGt = new HashSet<int>();
        for (int p = 1; p < prCount; p++)
        {
            using var prMaskBin = new Mat();
            Cv2.Compare(prLabels, p, prMaskBin, CmpType.EQ);
            int prArea = Cv2.CountNonZero(prMaskBin);
            double best = 0; int bestGt = 0;
            for (int g = 1; g < gtCount; g++)
            {
                using var gtMaskBin = new Mat();
                Cv2.Compare(gtLabels, g, gtMaskBin, CmpType.EQ);
                int gtArea = Cv2.CountNonZero(gtMaskBin);
                using var inter = new Mat();
                Cv2.BitwiseAnd(prMaskBin, gtMaskBin, inter);
                int interArea = Cv2.CountNonZero(inter);
                int union = prArea + gtArea - interArea;
                double iou = union == 0 ? 0 : (double)interArea / union;
                if (iou > best)
                {
                    best = iou; bestGt = g;
                }
            }
            if (best > 0.5)
            {
                tp++; matchedGt.Add(bestGt);
            }
            else
            {
                fp++;
            }
        }
        fn = gtCount - matchedGt.Count;
        double denom = 2 * tp + fp + fn;
        return denom == 0 ? 0 : 2.0 * tp / denom;
    }

    public static long MeasureElapsedMs(Action work)
    {
        var sw = Stopwatch.StartNew();
        work();
        sw.Stop();
        return sw.ElapsedMilliseconds;
    }

    public static double MeasurePeakMemory(Action work)
    {
        var proc = Process.GetCurrentProcess();
        long before = proc.PeakWorkingSet64;
        work();
        proc.Refresh();
        long after = proc.PeakWorkingSet64;
        return (after - before) / (1024.0 * 1024.0);
    }
}
