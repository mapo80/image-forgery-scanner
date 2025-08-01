using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
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

public static class ElaAdvanced
{
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

    static double[] FlattenEla(float[,] map)
    {
        int w = map.GetLength(0);
        int h = map.GetLength(1);
        var arr = new double[w * h];
        int idx = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                arr[idx++] = map[x, y];
        return arr;
    }

    public static float[][,] ComputeMultiScaleElaMap(Bitmap img, int[] jpegQualities)
    {
        var maps = new float[jpegQualities.Length][,];
        using var ms = new MemoryStream();
        img.Save(ms, ImageFormat.Png);
        ms.Position = 0;
        using var magick = new ImageMagick.MagickImage(ms);
        for (int i = 0; i < jpegQualities.Length; i++)
        {
            maps[i] = ElaMetrics.ComputeElaMap(magick, jpegQualities[i]);
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

    public static bool[,] RefineMask(bool[,] rawMask, int minArea = 50)
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
        var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(3, 3));
        Cv2.MorphologyEx(mat, mat, MorphTypes.Close, kernel);
        Cv2.MorphologyEx(mat, mat, MorphTypes.Open, kernel);
        var result = new bool[w, h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                result[x, y] = mat.Get<byte>(y, x) > 0;
        return result;
    }

    public static double ComputeRocAucPixel(byte[,] gt, bool[,] predMask)
    {
        var labels = FlattenMask(gt);
        var scores = new double[labels.Length];
        int w = gt.GetLength(0);
        int h = gt.GetLength(1);
        int idx = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                scores[idx++] = predMask[x, y] ? 1.0 : 0.0;
        var pairs = scores.Zip(labels, (s, l) => (Score: s, Label: l)).OrderByDescending(p => p.Score).ToArray();
        double pos = labels.Count(l => l > 0.5);
        double neg = labels.Length - pos;
        double tp = 0, fp = 0;
        double prevTp = 0, prevFp = 0, prevScore = double.PositiveInfinity, auc = 0;
        foreach (var p in pairs)
        {
            if (p.Score != prevScore)
            {
                auc += (fp / neg - prevFp / neg) * (tp / pos + prevTp / pos) / 2;
                prevScore = p.Score; prevTp = tp; prevFp = fp;
            }
            if (p.Label > 0.5) tp++; else fp++;
        }
        auc += (fp / neg - prevFp / neg) * (tp / pos + prevTp / pos) / 2;
        return auc;
    }

    public static double ComputePraucPixel(byte[,] gt, bool[,] predMask)
    {
        var labels = FlattenMask(gt);
        var scores = new double[labels.Length];
        int w = gt.GetLength(0);
        int h = gt.GetLength(1);
        int idx = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                scores[idx++] = predMask[x, y] ? 1.0 : 0.0;
        var pairs = scores.Zip(labels, (s, l) => (Score: s, Label: l)).OrderByDescending(p => p.Score).ToArray();
        double pos = labels.Count(l => l > 0.5);
        double tp = 0, fp = 0;
        double prevRecall = 0, prevPrecision = 1, auc = 0;
        foreach (var p in pairs)
        {
            if (p.Label > 0.5) tp++; else fp++;
            double recall = tp / pos;
            double precision = tp / (tp + fp);
            auc += (recall - prevRecall) * (precision + prevPrecision) / 2;
            prevRecall = recall; prevPrecision = precision;
        }
        return auc;
    }

    public static double ComputeNss(byte[,] gt, float[,] elaMap) => ElaMetrics.ComputeNss(gt, elaMap);

    public static double ComputeFprAtTpr(byte[,] gt, float[,] elaMap, double tprTarget)
    {
        var labels = FlattenMask(gt);
        var scores = FlattenEla(elaMap);
        var pairs = scores.Zip(labels, (s, l) => (Score: s, Label: l)).OrderByDescending(p => p.Score).ToArray();
        double pos = labels.Count(l => l > 0.5);
        double neg = labels.Length - pos;
        double tp = 0, fp = 0;
        foreach (var p in pairs)
        {
            if (p.Label > 0.5) tp++; else fp++;
            double tpr = tp / pos;
            if (tpr >= tprTarget)
                return fp / neg;
        }
        return 1.0;
    }

    public static double ComputeAveragePrecision(byte[,] gt, float[,] scoresMap)
    {
        var labels = FlattenMask(gt);
        var scores = FlattenEla(scoresMap);
        var pairs = scores.Zip(labels, (s, l) => (Score: s, Label: l)).OrderByDescending(p => p.Score).ToArray();
        double pos = labels.Count(l => l > 0.5);
        double tp = 0, fp = 0;
        double ap = 0, prevRecall = 0;
        foreach (var p in pairs)
        {
            if (p.Label > 0.5) tp++; else fp++;
            double recall = tp / pos;
            double precision = tp / (tp + fp);
            ap += precision * (recall - prevRecall);
            prevRecall = recall;
        }
        return ap;
    }

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

    public static double ComputeBoundaryF1(byte[,] gt, bool[,] predMask, int tol)
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
        using var gtEdges = new Mat();
        using var prEdges = new Mat();
        Cv2.Canny(gtMat, gtEdges, 100, 200);
        Cv2.Canny(prMat, prEdges, 100, 200);
        var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(2 * tol + 1, 2 * tol + 1));
        using var gtDil = new Mat();
        using var prDil = new Mat();
        Cv2.Dilate(gtEdges, gtDil, kernel);
        Cv2.Dilate(prEdges, prDil, kernel);
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
