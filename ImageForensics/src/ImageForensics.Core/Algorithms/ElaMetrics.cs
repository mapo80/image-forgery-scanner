using System;
using System.Linq;
using ImageMagick;
using MathNet.Numerics.Statistics;
using MathNet.Numerics.Distributions;
using OpenCvSharp;

namespace ImageForensics.Core.Algorithms;

public static class ElaMetrics
{
    public enum ElaAggregation
    {
        Max,
        Mean,
        Median
    }

    /// <summary>
    /// Generates the ELA heat-map for a given image applying an optional Laplacian
    /// high-pass filter before JPEG recompression.
    /// </summary>
    public static float[,] ComputeElaMap(MagickImage img, int jpegQuality = 90, bool highPass = true)
    {
        using var pre = img.Clone();
        if (highPass)
        {
            byte[] bmp = pre.ToByteArray(MagickFormat.Bmp);
            using var mat = Cv2.ImDecode(bmp, ImreadModes.Color);
            using var lap = new Mat();
            Cv2.Laplacian(mat, lap, mat.Type());
            Cv2.ImEncode(".bmp", lap, out var lapBytes);
            pre.Read(lapBytes);
        }
        using var comp = pre.Clone();
        comp.Quality = jpegQuality;
        byte[] jpeg = comp.ToByteArray(MagickFormat.Jpeg);
        using var rec = new MagickImage(jpeg);

        int w = pre.Width;
        int h = pre.Height;
        var map = new float[w, h];
        double max = 0;
        using var origPixels = pre.GetPixels();
        using var recPixels = rec.GetPixels();
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var o = origPixels.GetPixel(x, y);
                var r = recPixels.GetPixel(x, y);
                int dr = (int)Math.Abs(o.GetChannel(0) - r.GetChannel(0));
                int dg = (int)Math.Abs(o.GetChannel(1) - r.GetChannel(1));
                int db = (int)Math.Abs(o.GetChannel(2) - r.GetChannel(2));
                int diff = Math.Max(Math.Max(dr, dg), db);
                map[x, y] = diff;
                if (diff > max) max = diff;
            }
        }
        if (max > 0)
        {
            float scale = (float)(1.0 / max);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    map[x, y] *= scale;
        }
        return map;
    }

    /// <summary>
    /// Computes ELA maps for multiple JPEG qualities and aggregates them.
    /// </summary>
    public static float[,] ComputeElaMapMulti(MagickImage img, int[] qualities, ElaAggregation agg = ElaAggregation.Max)
    {
        var maps = qualities.Select(q => ComputeElaMap(img, q)).ToArray();
        int w = maps[0].GetLength(0);
        int h = maps[0].GetLength(1);
        var result = new float[w, h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (agg == ElaAggregation.Mean)
                {
                    float sum = 0;
                    foreach (var m in maps) sum += m[x, y];
                    result[x, y] = sum / maps.Length;
                }
                else if (agg == ElaAggregation.Median)
                {
                    var vals = maps.Select(m => m[x, y]).OrderBy(v => v).ToArray();
                    result[x, y] = vals[vals.Length / 2];
                }
                else
                {
                    float max = 0;
                    foreach (var m in maps) if (m[x, y] > max) max = m[x, y];
                    result[x, y] = max;
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Applies robust z-score normalization with optional logarithmic scaling.
    /// </summary>
    public static void RobustNormalize(float[,] map, double? alphaLog = null)
    {
        var flat = FlattenEla(map);
        double median = flat.Median();
        var abs = flat.Select(v => Math.Abs(v - median)).ToArray();
        double mad = abs.Median();
        if (mad == 0) mad = 1e-6;
        int w = map.GetLength(0);
        int h = map.GetLength(1);
        double min = double.MaxValue, max = double.MinValue;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                double z = (map[x, y] - median) / mad;
                if (alphaLog.HasValue)
                    z = Math.Log(1 + alphaLog.Value * Math.Abs(z));
                map[x, y] = (float)z;
                if (z < min) min = z;
                if (z > max) max = z;
            }
        }
        if (max > min)
        {
            float scale = (float)(1.0 / (max - min));
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    map[x, y] = (float)((map[x, y] - min) * scale);
        }
    }

    /// <summary>
    /// Performs a simple morphological opening (dilation then erosion).
    /// </summary>
    public static void MorphologicalOpening(float[,] map, int iterations = 1)
    {
        int w = map.GetLength(0);
        int h = map.GetLength(1);
        var tmp = new float[w, h];
        for (int it = 0; it < iterations; it++)
        {
            // dilation
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float max = 0;
                    for (int j = -1; j <= 1; j++)
                        for (int i = -1; i <= 1; i++)
                        {
                            int nx = x + i, ny = y + j;
                            if (nx >= 0 && nx < w && ny >= 0 && ny < h)
                                if (map[nx, ny] > max) max = map[nx, ny];
                        }
                    tmp[x, y] = max;
                }
            // erosion
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float min = float.MaxValue;
                    for (int j = -1; j <= 1; j++)
                        for (int i = -1; i <= 1; i++)
                        {
                            int nx = x + i, ny = y + j;
                            if (nx >= 0 && nx < w && ny >= 0 && ny < h)
                                if (tmp[nx, ny] < min) min = tmp[nx, ny];
                        }
                    map[x, y] = min;
                }
        }
    }

    static double[] FlattenMask(byte[,] mask)
    {
        int w = mask.GetLength(0);
        int h = mask.GetLength(1);
        var arr = new double[w * h];
        int idx = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                arr[idx++] = mask[x, y] > 0 ? 1.0 : 0.0;
        return arr;
    }

    static double[] FlattenEla(float[,] ela)
    {
        int w = ela.GetLength(0);
        int h = ela.GetLength(1);
        var arr = new double[w * h];
        int idx = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                arr[idx++] = ela[x, y];
        return arr;
    }

    public static double ComputeRocAucPixel(byte[,] mask, float[,] elaMap)
    {
        var labels = FlattenMask(mask);
        var scores = FlattenEla(elaMap);
        var pairs = scores.Zip(labels, (s, l) => (Score: s, Label: l)).OrderByDescending(p => p.Score).ToArray();
        double pos = labels.Count(l => l > 0.5);
        double neg = labels.Length - pos;
        double tp = 0, fp = 0;
        double prevTp = 0, prevFp = 0;
        double prevScore = double.PositiveInfinity;
        double auc = 0;
        foreach (var p in pairs)
        {
            if (p.Score != prevScore)
            {
                auc += (fp / neg - prevFp / neg) * (tp / pos + prevTp / pos) / 2;
                prevScore = p.Score;
                prevTp = tp;
                prevFp = fp;
            }
            if (p.Label > 0.5) tp++; else fp++;
        }
        auc += (fp / neg - prevFp / neg) * (tp / pos + prevTp / pos) / 2;
        return auc;
    }

    public static double ComputePraucPixel(byte[,] mask, float[,] elaMap)
    {
        var labels = FlattenMask(mask);
        var scores = FlattenEla(elaMap);
        var pairs = scores.Zip(labels, (s, l) => (Score: s, Label: l)).OrderByDescending(p => p.Score).ToArray();
        double pos = labels.Count(l => l > 0.5);
        double tp = 0, fp = 0;
        double prevRecall = 0, prevPrecision = 1;
        double auc = 0;
        foreach (var p in pairs)
        {
            if (p.Label > 0.5) tp++; else fp++;
            double recall = tp / pos;
            double precision = tp / (tp + fp);
            auc += (recall - prevRecall) * (precision + prevPrecision) / 2;
            prevRecall = recall;
            prevPrecision = precision;
        }
        return auc;
    }

    public static double ComputeNss(byte[,] mask, float[,] elaMap)
    {
        var labels = FlattenMask(mask);
        var scores = FlattenEla(elaMap);
        double mean = scores.Average();
        double std = Math.Sqrt(scores.Sum(s => (s - mean) * (s - mean)) / scores.Length);
        if (std == 0) return 0;
        double sum = 0;
        int count = 0;
        for (int i = 0; i < scores.Length; i++)
        {
            double z = (scores[i] - mean) / std;
            if (labels[i] > 0.5)
            {
                sum += z;
                count++;
            }
        }
        return count == 0 ? 0 : sum / count;
    }

    public static (double r, double p) ComputePearson(byte[,] mask, float[,] elaMap)
    {
        var labels = FlattenMask(mask);
        var scores = FlattenEla(elaMap);
        double r = Correlation.Pearson(labels, scores);
        int n = labels.Length;
        double t = r * Math.Sqrt((n - 2) / (1 - r * r));
        double p = 2 * (1 - StudentT.CDF(0, 1, n - 2, Math.Abs(t)));
        return (r, p);
    }

    public static (double rho, double p) ComputeSpearman(byte[,] mask, float[,] elaMap)
    {
        var labels = FlattenMask(mask);
        var scores = FlattenEla(elaMap);
        double rho = Correlation.Spearman(labels, scores);
        int n = labels.Length;
        double t = rho * Math.Sqrt((n - 2) / (1 - rho * rho));
        double p = 2 * (1 - StudentT.CDF(0, 1, n - 2, Math.Abs(t)));
        return (rho, p);
    }

    public static double ComputeOtsuThreshold(float[,] elaMap)
    {
        int w = elaMap.GetLength(0);
        int h = elaMap.GetLength(1);
        int bins = 256;
        var hist = new int[bins];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int bin = (int)Math.Round(elaMap[x, y] * (bins - 1));
                if (bin < 0) bin = 0; if (bin >= bins) bin = bins - 1;
                hist[bin]++;
            }
        int total = w * h;
        double sum = 0;
        for (int i = 0; i < bins; i++) sum += i * hist[i];
        int wB = 0; double sumB = 0; double max = 0; int threshold = 0;
        for (int i = 0; i < bins; i++)
        {
            wB += hist[i];
            if (wB == 0) continue;
            int wF = total - wB;
            if (wF == 0) break;
            sumB += i * hist[i];
            double mB = sumB / wB;
            double mF = (sum - sumB) / wF;
            double between = wB * wF * (mB - mF) * (mB - mF);
            if (between > max)
            {
                max = between;
                threshold = i;
            }
        }
        return threshold / (double)(bins - 1);
    }

    public static bool[,] BinarizeElaMap(float[,] elaMap, double threshold)
    {
        int w = elaMap.GetLength(0);
        int h = elaMap.GetLength(1);
        var mask = new bool[w, h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                mask[x, y] = elaMap[x, y] >= threshold;
        return mask;
    }

    /// <summary>
    /// Computes a global percentile threshold on the ELA map.
    /// </summary>
    public static double ComputePercentileThreshold(float[,] elaMap, double percentile)
    {
        var arr = FlattenEla(elaMap).OrderBy(v => v).ToArray();
        double pos = percentile * (arr.Length - 1);
        int idx = (int)pos;
        double frac = pos - idx;
        if (idx >= arr.Length - 1) return arr[^1];
        return arr[idx] * (1 - frac) + arr[idx + 1] * frac;
    }

    /// <summary>
    /// Adaptive thresholding using OpenCvSharp's AdaptiveThreshold.
    /// </summary>
    public static bool[,] AdaptiveThreshold(float[,] elaMap, int blockSize = 31, double c = 0.01)
    {
        int w = elaMap.GetLength(0);
        int h = elaMap.GetLength(1);
        using var mat = new Mat(h, w, MatType.CV_8UC1);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                mat.Set(y, x, (byte)(Math.Clamp(elaMap[x, y], 0, 1) * 255));
        using var bin = new Mat();
        Cv2.AdaptiveThreshold(mat, bin, 255, AdaptiveThresholdTypes.MeanC, ThresholdTypes.Binary, blockSize, c);
        var mask = new bool[w, h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                mask[x, y] = bin.Get<byte>(y, x) > 0;
        return mask;
    }

    /// <summary>
    /// Finds the threshold that maximises the F1 score on a validation mask.
    /// </summary>
    public static double ComputeBestF1Threshold(byte[,] mask, float[,] elaMap, int steps = 100)
    {
        double bestT = 0, bestF1 = 0;
        for (int i = 0; i <= steps; i++)
        {
            double t = i / (double)steps;
            var pred = BinarizeElaMap(elaMap, t);
            double f1 = ComputeDicePixel(mask, pred);
            if (f1 > bestF1)
            {
                bestF1 = f1;
                bestT = t;
            }
        }
        return bestT;
    }

    public static double ComputeIoUPixel(byte[,] mask, bool[,] predMask)
    {
        double tp = 0, fp = 0, fn = 0;
        int w = mask.GetLength(0);
        int h = mask.GetLength(1);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool gt = mask[x, y] > 0;
                bool pr = predMask[x, y];
                if (gt && pr) tp++;
                else if (!gt && pr) fp++;
                else if (gt && !pr) fn++;
            }
        double denom = tp + fp + fn;
        return denom == 0 ? 0 : tp / denom;
    }

    public static double ComputeDicePixel(byte[,] mask, bool[,] predMask)
    {
        double tp = 0, fp = 0, fn = 0;
        int w = mask.GetLength(0);
        int h = mask.GetLength(1);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool gt = mask[x, y] > 0;
                bool pr = predMask[x, y];
                if (gt && pr) tp++;
                else if (!gt && pr) fp++;
                else if (gt && !pr) fn++;
            }
        double denom = 2 * tp + fp + fn;
        return denom == 0 ? 0 : 2 * tp / denom;
    }

    public static double ComputeMccPixel(byte[,] mask, bool[,] predMask)
    {
        double tp = 0, tn = 0, fp = 0, fn = 0;
        int w = mask.GetLength(0);
        int h = mask.GetLength(1);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool gt = mask[x, y] > 0;
                bool pr = predMask[x, y];
                if (gt && pr) tp++;
                else if (!gt && !pr) tn++;
                else if (!gt && pr) fp++;
                else fn++;
            }
        double denom = Math.Sqrt((tp + fp) * (tp + fn) * (tn + fp) * (tn + fn));
        return denom == 0 ? 0 : (tp * tn - fp * fn) / denom;
    }

    /// <summary>
    /// Computes the Boundary F1 score using Canny edges with a tolerance of 2 pixels.
    /// </summary>
    public static double ComputeBoundaryF1(byte[,] mask, bool[,] predMask)
    {
        int w = mask.GetLength(0);
        int h = mask.GetLength(1);
        using var gt = new Mat(h, w, MatType.CV_8UC1);
        using var pr = new Mat(h, w, MatType.CV_8UC1);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                gt.Set(y, x, mask[x, y] > 0 ? (byte)255 : (byte)0);
                pr.Set(y, x, predMask[x, y] ? (byte)255 : (byte)0);
            }
        using var gtEdges = new Mat();
        using var prEdges = new Mat();
        Cv2.Canny(gt, gtEdges, 100, 200);
        Cv2.Canny(pr, prEdges, 100, 200);
        var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(5, 5));
        using var gtDil = new Mat();
        using var prDil = new Mat();
        Cv2.Dilate(gtEdges, gtDil, kernel, iterations: 2);
        Cv2.Dilate(prEdges, prDil, kernel, iterations: 2);
        using var tpMat = new Mat();
        Cv2.BitwiseAnd(gtEdges, prDil, tpMat);
        using var fpMat = new Mat();
        using var notGtDil = new Mat();
        Cv2.BitwiseNot(gtDil, notGtDil);
        Cv2.BitwiseAnd(prEdges, notGtDil, fpMat);
        using var fnMat = new Mat();
        using var notPrDil = new Mat();
        Cv2.BitwiseNot(prDil, notPrDil);
        Cv2.BitwiseAnd(gtEdges, notPrDil, fnMat);
        double tp = Cv2.CountNonZero(tpMat);
        double fp = Cv2.CountNonZero(fpMat);
        double fn = Cv2.CountNonZero(fnMat);
        double denom = 2 * tp + fp + fn;
        return denom == 0 ? 0 : 2 * tp / denom;
    }
}
