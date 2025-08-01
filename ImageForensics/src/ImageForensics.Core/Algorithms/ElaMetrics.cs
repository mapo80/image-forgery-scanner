using System;
using System.Linq;
using ImageMagick;
using MathNet.Numerics.Statistics;
using MathNet.Numerics.Distributions;

namespace ImageForensics.Core.Algorithms;

public static class ElaMetrics
{
    /// <summary>
    /// Generates the ELA heat-map for a given image.
    /// </summary>
    /// <param name="img">Magick image input.</param>
    /// <param name="jpegQuality">JPEG quality for recompression.</param>
    /// <returns>2D float array normalised to [0,1].</returns>
    public static float[,] ComputeElaMap(MagickImage img, int jpegQuality = 90)
    {
        using var orig = img.Clone();
        using var comp = img.Clone();
        comp.Quality = jpegQuality;
        byte[] jpeg = comp.ToByteArray(MagickFormat.Jpeg);
        using var rec = new MagickImage(jpeg);

        int w = orig.Width;
        int h = orig.Height;
        var map = new float[w, h];
        double max = 0;
        using var origPixels = orig.GetPixels();
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
}
