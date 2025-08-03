using System;
using System.Linq;
using OpenCvSharp;

namespace ImageForensics.Core.Algorithms;

/// <summary>
/// Utility methods for thresholding and metrics for copy–move evaluation.
/// </summary>
public static class CopyMoveMetrics
{
    public static double ComputeOtsuThreshold(Mat map)
    {
        using var tmp = new Mat();
        map.ConvertTo(tmp, MatType.CV_8U, 255);
        double t = Cv2.Threshold(tmp, new Mat(), 0, 255, ThresholdTypes.Otsu);
        return t / 255.0;
    }

    public static Mat BinarizeMap(Mat map, double threshold)
    {
        var binary = new Mat();
        Cv2.Threshold(map, binary, threshold, 255, ThresholdTypes.Binary);
        binary.ConvertTo(binary, MatType.CV_8U);
        return binary;
    }

    public static void RemoveSmallComponents(Mat mask, int minArea)
    {
        using var labels = new Mat();
        using var stats = new Mat();
        Cv2.ConnectedComponentsWithStats(mask, labels, stats, new Mat());
        for (int i = 1; i < stats.Rows; i++)
        {
            int area = stats.Get<int>(i, (int)ConnectedComponentsTypes.Area);
            if (area < minArea)
            {
                using var eq = new Mat();
                Cv2.Compare(labels, i, eq, CmpType.EQ);
                mask.SetTo(Scalar.Black, eq);
            }
        }
    }

    static void Flatten(Mat mask, Mat map, out float[] scores, out byte[] labels)
    {
        int w = map.Width; int h = map.Height;
        scores = new float[w * h];
        labels = new byte[w * h];
        int idx = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                scores[idx] = map.At<float>(y, x);
                labels[idx] = mask.At<byte>(y, x) > 0 ? (byte)1 : (byte)0;
                idx++;
            }
    }

    static void FlattenBinary(Mat mask, Mat pred, out byte[] gt, out byte[] pr)
    {
        int w = mask.Width; int h = mask.Height;
        gt = new byte[w * h];
        pr = new byte[w * h];
        int idx = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                gt[idx] = mask.At<byte>(y, x) > 0 ? (byte)1 : (byte)0;
                pr[idx] = pred.At<byte>(y, x) > 0 ? (byte)1 : (byte)0;
                idx++;
            }
    }

    public static double ComputeRocAucPixel(Mat mask, Mat map)
    {
        Flatten(mask, map, out var scores, out var labels);
        if (labels.Sum(l => l) == 0) return 0;
        double P = labels.Count(l => l == 1);
        double N = labels.Length - P;
        if (P == 0 || N == 0) return 0;
        var order = scores.Select((s, i) => (s, i)).OrderByDescending(t => t.s);
        double tp = 0, fp = 0, prevFpr = 0, prevTpr = 0, auc = 0;
        foreach (var (_, idx) in order)
        {
            if (labels[idx] == 1) tp++; else fp++;
            double tpr = tp / P;
            double fpr = fp / N;
            auc += (fpr - prevFpr) * (tpr + prevTpr) / 2.0;
            prevFpr = fpr; prevTpr = tpr;
        }
        return auc;
    }

    public static double ComputePraucPixel(Mat mask, Mat map)
    {
        Flatten(mask, map, out var scores, out var labels);
        double P = labels.Count(l => l == 1);
        if (P == 0) return 0;
        var order = scores.Select((s, i) => (s, i)).OrderByDescending(t => t.s);
        double tp = 0, fp = 0, prevRecall = 0, auc = 0;
        foreach (var (_, idx) in order)
        {
            if (labels[idx] == 1) tp++; else fp++;
            double recall = tp / P;
            double precision = tp / (tp + fp);
            auc += (recall - prevRecall) * precision;
            prevRecall = recall;
        }
        return auc;
    }

    public static double ComputeNss(Mat mask, Mat map)
    {
        Flatten(mask, map, out var scores, out var labels);
        double mean = scores.Average();
        double std = Math.Sqrt(scores.Select(v => (v - mean) * (v - mean)).Average());
        if (std == 0) return 0;
        double sum = 0; int count = 0;
        for (int i = 0; i < scores.Length; i++)
        {
            if (labels[i] == 1)
            {
                sum += (scores[i] - mean) / std;
                count++;
            }
        }
        return count == 0 ? 0 : sum / count;
    }

    public static double ComputeFprAt95Tpr(Mat mask, Mat map)
    {
        Flatten(mask, map, out var scores, out var labels);
        double P = labels.Count(l => l == 1);
        double N = labels.Length - P;
        if (P == 0 || N == 0) return 1.0;
        var order = scores.Select((s, i) => (s, i)).OrderByDescending(t => t.s);
        double tp = 0, fp = 0;
        foreach (var (_, idx) in order)
        {
            if (labels[idx] == 1) tp++; else fp++;
            double tpr = tp / P;
            if (tpr >= 0.95)
                return fp / N;
        }
        return 1.0;
    }

    public static double ComputeAveragePrecision(Mat mask, Mat map)
    {
        Flatten(mask, map, out var scores, out var labels);
        double P = labels.Count(l => l == 1);
        if (P == 0) return 0;
        var order = scores.Select((s, i) => (s, i)).OrderByDescending(t => t.s);
        double tp = 0, fp = 0, prevRecall = 0, ap = 0;
        foreach (var (_, idx) in order)
        {
            if (labels[idx] == 1) tp++; else fp++;
            double recall = tp / P;
            double precision = tp / (tp + fp);
            ap += precision * (recall - prevRecall);
            prevRecall = recall;
        }
        return ap;
    }

    public static double ComputeIoUPixel(Mat mask, Mat pred)
    {
        FlattenBinary(mask, pred, out var gt, out var pr);
        double tp = 0, fp = 0, fn = 0;
        for (int i = 0; i < gt.Length; i++)
        {
            bool g = gt[i] > 0; bool p = pr[i] > 0;
            if (g && p) tp++; else if (!g && p) fp++; else if (g && !p) fn++;
        }
        return tp / (tp + fp + fn + 1e-9);
    }

    public static double ComputeDicePixel(Mat mask, Mat pred)
    {
        FlattenBinary(mask, pred, out var gt, out var pr);
        double tp = 0, fp = 0, fn = 0;
        for (int i = 0; i < gt.Length; i++)
        {
            bool g = gt[i] > 0; bool p = pr[i] > 0;
            if (g && p) tp++; else if (!g && p) fp++; else if (g && !p) fn++;
        }
        return (2 * tp) / (2 * tp + fp + fn + 1e-9);
    }

    public static double ComputeMccPixel(Mat mask, Mat pred)
    {
        FlattenBinary(mask, pred, out var gt, out var pr);
        double tp = 0, fp = 0, fn = 0, tn = 0;
        for (int i = 0; i < gt.Length; i++)
        {
            bool g = gt[i] > 0; bool p = pr[i] > 0;
            if (g && p) tp++; else if (!g && p) fp++; else if (g && !p) fn++; else tn++;
        }
        double denom = Math.Sqrt((tp + fp) * (tp + fn) * (tn + fp) * (tn + fn) + 1e-9);
        return (tp * tn - fp * fn) / denom;
    }

    public static double ComputeBoundaryF1(Mat mask, Mat pred)
    {
        using var gtEdge = new Mat();
        using var prEdge = new Mat();
        Cv2.Canny(mask, gtEdge, 100, 200);
        Cv2.Canny(pred, prEdge, 100, 200);
        var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
        using var gtDil = new Mat();
        using var prDil = new Mat();
        Cv2.Dilate(gtEdge, gtDil, kernel);
        Cv2.Dilate(prEdge, prDil, kernel);
        int predTotal = Cv2.CountNonZero(prEdge);
        int gtTotal = Cv2.CountNonZero(gtEdge);
        using var tmp1 = new Mat();
        using var tmp2 = new Mat();
        Cv2.BitwiseAnd(prEdge, gtDil, tmp1);
        Cv2.BitwiseAnd(gtEdge, prDil, tmp2);
        int predMatch = Cv2.CountNonZero(tmp1);
        int gtMatch = Cv2.CountNonZero(tmp2);
        double precision = predTotal == 0 ? 0 : predMatch / (double)predTotal;
        double recall = gtTotal == 0 ? 0 : gtMatch / (double)gtTotal;
        return (precision + recall) > 0 ? 2 * precision * recall / (precision + recall) : 0;
    }

    public static double ComputeRegionIoU(Mat mask, Mat pred)
    {
        using var labels = new Mat();
        int num = Cv2.ConnectedComponents(mask, labels);
        double sumIoU = 0; int count = 0;
        for (int i = 1; i < num; i++)
        {
            using var comp = new Mat();
            Cv2.Compare(labels, i, comp, CmpType.EQ);
            Cv2.ConvertScaleAbs(comp, comp); // ensure 0/255
            using var inter = new Mat();
            using var uni = new Mat();
            Cv2.BitwiseAnd(comp, pred, inter);
            Cv2.BitwiseOr(comp, pred, uni);
            int union = Cv2.CountNonZero(uni);
            if (union == 0) continue;
            int interCount = Cv2.CountNonZero(inter);
            sumIoU += interCount / (double)union;
            count++;
        }
        return count == 0 ? 0 : sumIoU / count;
    }
}
