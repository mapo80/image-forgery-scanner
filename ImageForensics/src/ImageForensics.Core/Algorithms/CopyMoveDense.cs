using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using OpenCvSharp.Flann;

namespace ImageForensics.Core.Algorithms;

/// <summary>
/// Dense block matching copy–move detector.
/// Generates a copy–move confidence map using DCT features
/// extracted from overlapping blocks and FLANN matching.
/// </summary>
public static class CopyMoveDense
{
    // First 15 zig-zag indices for a 16x16 block (x,y pairs).
    static readonly (int X, int Y)[] ZigZag = new (int, int)[]
    {
        (0,0),(1,0),(0,1),(2,0),(1,1),(0,2),(3,0),(2,1),(1,2),(0,3),
        (4,0),(3,1),(2,2),(1,3),(0,4)
    };

    /// <summary>
    /// Compute raw and normalized copy–move confidence maps.
    /// </summary>
    public static (Mat Raw, Mat Norm, int Blocks, int Candidates, int KeptMatches, int Clusters, int KeptClusters) ComputeCopyMoveMap(
        Mat src,
        int blockSize = 16,
        int stride = 4,
        int k = 5,
        double tau = 0.10,
        int minShift = 20,
        int minPts = 20,
        int minArea = 50,
        double normPercentile = 0.95)
    {
        using var gray = new Mat();
        if (src.Channels() > 1)
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        else
            src.CopyTo(gray);

        int w = gray.Cols;
        int h = gray.Rows;
        var raw = new Mat(h, w, MatType.CV_32F, Scalar.All(0));

        int blocksX = (w - blockSize) / stride + 1;
        int blocksY = (h - blockSize) / stride + 1;
        int blockCount = blocksX * blocksY;

        var features = new Mat(blockCount, ZigZag.Length, MatType.CV_32F);
        var positions = new List<Point>(blockCount);

        int idx = 0;
        for (int y = 0; y <= h - blockSize; y += stride)
        {
            for (int x = 0; x <= w - blockSize; x += stride)
            {
                var roi = new Rect(x, y, blockSize, blockSize);
                using var block = new Mat(gray, roi);
                using var block32 = new Mat();
                block.ConvertTo(block32, MatType.CV_32F);
                Cv2.Dct(block32, block32);
                for (int z = 0; z < ZigZag.Length; z++)
                {
                    var (xx, yy) = ZigZag[z];
                    features.Set(idx, z, block32.At<float>(yy, xx));
                }
                positions.Add(new Point(x, y));
                idx++;
            }
        }

        int candidateMatches = 0;
        var matches = new List<(int i, int j, int dx, int dy, float dist)>();
        using (var flann = new OpenCvSharp.Flann.Index(features, new KDTreeIndexParams(4)))
        using (var indices = new Mat())
        using (var dists = new Mat())
        {
            flann.KnnSearch(features, indices, dists, k + 1, new SearchParams());
            for (int i = 0; i < blockCount; i++)
            {
                for (int nn = 1; nn <= k; nn++)
                {
                    int j = indices.Get<int>(i, nn);
                    if (j < 0 || j >= blockCount) continue;
                    float dist = dists.Get<float>(i, nn);
                    int dx = positions[j].X - positions[i].X;
                    int dy = positions[j].Y - positions[i].Y;
                    if (Math.Sqrt(dx * dx + dy * dy) < minShift) continue;
                    candidateMatches++;
                    matches.Add((i, j, dx, dy, dist));
                }
            }
        }
        // Prune by distance
        if (matches.Count > 8000)
            matches = matches.OrderBy(m => m.dist).Take(8000).ToList();

        int keptMatches = 0;
        int clusterCount = 0;
        int keptClusters = 0;
        if (matches.Count > 0)
        {
            double eps = tau * Math.Max(w, h);
            var offsets = matches.Select(m => new Point2d(m.dx, m.dy)).ToList();
            var labels = Dbscan(offsets, eps, minPts);
            var grouped = matches.Select((m, idx) => (m, idx))
                .GroupBy(t => labels[t.idx])
                .Where(g => g.Key >= 0)
                .Select(g => g.Select(t => t.m).ToList())
                .ToList();
            clusterCount = grouped.Count;
            var counts = grouped.Select(g => g.Count).OrderBy(c => c).ToArray();
            double blockCountMed = 0;
            if (counts.Length > 0)
                blockCountMed = counts.Length % 2 == 1 ? counts[counts.Length / 2] : (counts[counts.Length / 2 - 1] + counts[counts.Length / 2]) / 2.0;
            foreach (var clusterMatches in grouped)
            {
                int clusterArea = clusterMatches.Count * blockSize * blockSize;
                if (clusterArea < minArea) continue;
                var srcPts = clusterMatches.Select(m => new Point2f(positions[m.i].X + blockSize / 2f, positions[m.i].Y + blockSize / 2f)).ToArray();
                var dstPts = clusterMatches.Select(m => new Point2f(positions[m.j].X + blockSize / 2f, positions[m.j].Y + blockSize / 2f)).ToArray();
                var srcMat = InputArray.Create(srcPts);
                var dstMat = InputArray.Create(dstPts);
                Mat inliers = new();
                Cv2.EstimateAffine2D(srcMat, dstMat, inliers);
                int inlierCount = inliers.Empty() ? 0 : Cv2.CountNonZero(inliers);
                if (inlierCount < 0.6 * blockCountMed) continue;
                keptClusters++;
                for (int t = 0; t < clusterMatches.Count; t++)
                {
                    if (inliers.At<byte>(t, 0) == 0) continue;
                    keptMatches++;
                    var match = clusterMatches[t];
                    var p1 = positions[match.i];
                    var p2 = positions[match.j];
                    for (int yy = 0; yy < blockSize; yy++)
                    {
                        for (int xx = 0; xx < blockSize; xx++)
                        {
                            int x1 = p1.X + xx;
                            int y1 = p1.Y + yy;
                            int x2 = p2.X + xx;
                            int y2 = p2.Y + yy;
                            raw.Set(y1, x1, raw.At<float>(y1, x1) + 1f);
                            raw.Set(y2, x2, raw.At<float>(y2, x2) + 1f);
                        }
                    }
                }
                inliers.Dispose();
            }
        }

        var norm = raw.Clone();
        double p = Percentile(norm, normPercentile);
        if (p > 0)
            norm.ConvertTo(norm, MatType.CV_32F, 1.0 / p);
        Cv2.Min(norm, 1.0, norm);

        return (raw, norm, blockCount, candidateMatches, keptMatches, clusterCount, keptClusters);
    }

    static double Percentile(Mat m, double percentile)
    {
        using var flat = m.Reshape(1, m.Rows * m.Cols);
        Cv2.Sort(flat, flat, SortFlags.Ascending);
        long total = flat.Total();
        int idx = (int)Math.Clamp((long)(percentile * (total - 1)), 0, total - 1);
        return flat.Get<float>(idx);
    }

    static int[] Dbscan(IList<Point2d> pts, double eps, int minPts)
    {
        int n = pts.Count;
        var labels = Enumerable.Repeat(-99, n).ToArray();
        int clusterId = 0;
        double eps2 = eps * eps;
        for (int i = 0; i < n; i++)
        {
            if (labels[i] != -99) continue;
            var neighbors = RangeQuery(pts, i, eps2);
            if (neighbors.Count < minPts)
            {
                labels[i] = -1; // noise
                continue;
            }
            labels[i] = clusterId;
            var queue = new Queue<int>(neighbors);
            while (queue.Count > 0)
            {
                int j = queue.Dequeue();
                if (labels[j] == -1) labels[j] = clusterId;
                if (labels[j] != -99) continue;
                labels[j] = clusterId;
                var neigh2 = RangeQuery(pts, j, eps2);
                if (neigh2.Count >= minPts)
                    foreach (var q in neigh2)
                        if (labels[q] == -99) queue.Enqueue(q);
            }
            clusterId++;
        }
        return labels;
    }

    static List<int> RangeQuery(IList<Point2d> pts, int idx, double eps2)
    {
        var res = new List<int>();
        var p = pts[idx];
        for (int i = 0; i < pts.Count; i++)
        {
            if (i == idx) continue;
            var q = pts[i];
            double dx = p.X - q.X;
            double dy = p.Y - q.Y;
            if (dx * dx + dy * dy <= eps2)
                res.Add(i);
        }
        return res;
    }
}
