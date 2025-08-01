using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Statistics;
using MathNet.Numerics.Distributions;

namespace ImageForensics.Core.Algorithms;

public static class ElaMetrics
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

    static double[] FlattenScores(float[,] map)
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

    public static double ComputeRocAucPixel(byte[,] mask, float[,] elaMap)
    {
        var labels = FlattenMask(mask);
        var scores = FlattenScores(elaMap);
        var pairs = scores.Zip(labels, (s, l) => (Score: s, Label: l)).OrderByDescending(p => p.Score).ToArray();
        double pos = labels.Count(l => l > 0.5);
        double neg = labels.Length - pos;
        double tp = 0, fp = 0;
        double prevTp = 0, prevFp = 0, prevScore = double.PositiveInfinity;
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
        var scores = FlattenScores(elaMap);
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
        var scores = FlattenScores(elaMap);
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
        var scores = FlattenScores(elaMap);
        double r = Correlation.Pearson(labels, scores);
        int n = labels.Length;
        double t = r * Math.Sqrt((n - 2) / (1 - r * r));
        double p = 2 * (1 - StudentT.CDF(0, 1, n - 2, Math.Abs(t)));
        return (r, p);
    }

    public static (double rho, double p) ComputeSpearman(byte[,] mask, float[,] elaMap)
    {
        var labels = FlattenMask(mask);
        var scores = FlattenScores(elaMap);
        double rho = Correlation.Spearman(labels, scores);
        int n = labels.Length;
        double t = rho * Math.Sqrt((n - 2) / (1 - rho * rho));
        double p = 2 * (1 - StudentT.CDF(0, 1, n - 2, Math.Abs(t)));
        return (rho, p);
    }

    public static double ComputeFprAtTpr(byte[,] gtMask, float[,] scores, double tprTarget = 0.95)
    {
        var labels = FlattenMask(gtMask);
        var vals = FlattenScores(scores);
        var pairs = vals.Zip(labels, (s, l) => (Score: s, Label: l)).OrderByDescending(p => p.Score);
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

    public static double ComputeAveragePrecision(byte[,] gtMask, float[,] scores)
    {
        var labels = FlattenMask(gtMask);
        var vals = FlattenScores(scores);
        var pairs = vals.Zip(labels, (s, l) => (Score: s, Label: l)).OrderByDescending(p => p.Score);
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

    public static double ComputeOtsuThreshold(float[,] elaMap)
    {
        int w = elaMap.GetLength(0);
        int h = elaMap.GetLength(1);
        const int bins = 256;
        var hist = new int[bins];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int bin = (int)Math.Round(Math.Clamp(elaMap[x, y], 0f, 1f) * (bins - 1));
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
                else if (!gt && pr) fp++;
                else if (gt && !pr) fn++;
                else tn++;
            }
        double denom = Math.Sqrt((tp + fp) * (tp + fn) * (tn + fp) * (tn + fn));
        return denom == 0 ? 0 : (tp * tn - fp * fn) / denom;
    }

    public static double ComputeBoundaryF1(byte[,] gtMask, bool[,] predMask, int tolerance)
    {
        int w = gtMask.GetLength(0);
        int h = gtMask.GetLength(1);
        var gtEdge = ExtractEdges(gtMask);
        var prEdge = ExtractEdges(predMask);

        bool HasNeighbor(bool[,] edges, int x, int y)
        {
            for (int j = -tolerance; j <= tolerance; j++)
            {
                int ny = y + j;
                if (ny < 0 || ny >= h) continue;
                for (int i = -tolerance; i <= tolerance; i++)
                {
                    int nx = x + i;
                    if (nx < 0 || nx >= w) continue;
                    if (edges[nx, ny]) return true;
                }
            }
            return false;
        }

        int tp = 0, fp = 0, fn = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (gtEdge[x, y])
                {
                    if (HasNeighbor(prEdge, x, y)) tp++; else fn++;
                }
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (prEdge[x, y])
                {
                    if (!HasNeighbor(gtEdge, x, y)) fp++;
                }
        double denom = 2 * tp + fp + fn;
        return denom == 0 ? 0 : 2.0 * tp / denom;
    }

    static bool[,] ExtractEdges(byte[,] mask)
    {
        int w = mask.GetLength(0);
        int h = mask.GetLength(1);
        var edges = new bool[w, h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (mask[x, y] > 0)
                {
                    if (x == 0 || y == 0 || x == w - 1 || y == h - 1 ||
                        mask[x - 1, y] == 0 || mask[x + 1, y] == 0 || mask[x, y - 1] == 0 || mask[x, y + 1] == 0)
                        edges[x, y] = true;
                }
        return edges;
    }

    static bool[,] ExtractEdges(bool[,] mask)
    {
        int w = mask.GetLength(0);
        int h = mask.GetLength(1);
        var edges = new bool[w, h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (mask[x, y])
                {
                    if (x == 0 || y == 0 || x == w - 1 || y == h - 1 ||
                        !mask[x - 1, y] || !mask[x + 1, y] || !mask[x, y - 1] || !mask[x, y + 1])
                        edges[x, y] = true;
                }
        return edges;
    }

    public static double ComputeRegionIoU(byte[,] gtMask, bool[,] predMask)
    {
        int w = gtMask.GetLength(0);
        int h = gtMask.GetLength(1);
        var gtBool = new bool[w, h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                gtBool[x, y] = gtMask[x, y] > 0;
        var (gtLabels, gtAreas) = LabelComponents(gtBool);
        var (prLabels, prAreas) = LabelComponents(predMask);
        int gtCount = gtAreas.Count;
        if (gtCount == 0) return 0;
        var inter = new Dictionary<(int, int), int>();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int g = gtLabels[x, y];
                int p = prLabels[x, y];
                if (g > 0 && p > 0)
                {
                    var key = (g, p);
                    inter[key] = inter.TryGetValue(key, out int v) ? v + 1 : 1;
                }
            }
        double sum = 0;
        for (int g = 1; g <= gtCount; g++)
        {
            int bestP = 0;
            int bestInter = 0;
            foreach (var kv in inter)
            {
                if (kv.Key.Item1 == g && kv.Value > bestInter)
                {
                    bestInter = kv.Value;
                    bestP = kv.Key.Item2;
                }
            }
            if (bestInter > 0 && bestP > 0)
            {
                int union = gtAreas[g - 1] + prAreas[bestP - 1] - bestInter;
                sum += union > 0 ? (double)bestInter / union : 0;
            }
        }
        return sum / gtCount;
    }

    static (int[,] labels, List<int> areas) LabelComponents(bool[,] mask)
    {
        int w = mask.GetLength(0);
        int h = mask.GetLength(1);
        var labels = new int[w, h];
        var areas = new List<int>();
        int label = 0;
        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };
        var stack = new Stack<(int, int)>();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (mask[x, y] && labels[x, y] == 0)
                {
                    label++;
                    int area = 0;
                    labels[x, y] = label;
                    stack.Push((x, y));
                    while (stack.Count > 0)
                    {
                        var (cx, cy) = stack.Pop();
                        area++;
                        for (int k = 0; k < 4; k++)
                        {
                            int nx = cx + dx[k];
                            int ny = cy + dy[k];
                            if (nx >= 0 && nx < w && ny >= 0 && ny < h &&
                                mask[nx, ny] && labels[nx, ny] == 0)
                            {
                                labels[nx, ny] = label;
                                stack.Push((nx, ny));
                            }
                        }
                    }
                    areas.Add(area);
                }
        return (labels, areas);
    }
}
