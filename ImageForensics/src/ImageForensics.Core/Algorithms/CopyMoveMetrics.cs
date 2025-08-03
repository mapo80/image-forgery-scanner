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

    static void ToArrays(Mat mask, Mat map, out byte[,] m, out float[,] f)
    {
        int w = map.Width; int h = map.Height;
        m = new byte[w, h];
        f = new float[w, h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                m[x, y] = mask.At<byte>(y, x) > 0 ? (byte)1 : (byte)0;
                f[x, y] = map.At<float>(y, x);
            }
    }

    static void ToBinaryArrays(Mat mask, Mat pred, out byte[,] m, out bool[,] p)
    {
        int w = mask.Width; int h = mask.Height;
        m = new byte[w, h];
        p = new bool[w, h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                m[x, y] = mask.At<byte>(y, x) > 0 ? (byte)1 : (byte)0;
                p[x, y] = pred.At<byte>(y, x) > 0;
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
}
