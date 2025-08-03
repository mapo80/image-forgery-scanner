using System;
using System.IO;
using System.Linq;
using System.Text;
using OpenCvSharp;

namespace ImageForensics.Core.Algorithms;

/// <summary>
/// Runs the dense copy–move detector over a dataset and computes metrics.
/// </summary>
public static class CopyMoveEvalRunner
{
    static Mat LoadMask(string path)
    {
        var m = Cv2.ImRead(path, ImreadModes.Grayscale);
        Cv2.Threshold(m, m, 127, 255, ThresholdTypes.Binary);
        return m;
    }

    static void SaveBase64(Mat m, string path, bool binary = false)
    {
        using var tmp = new Mat();
        if (binary)
            m.CopyTo(tmp);
        else
            m.ConvertTo(tmp, MatType.CV_8U, 255);
        Cv2.ImEncode(".png", tmp, out var buf);
        File.WriteAllText(path, Convert.ToBase64String(buf));
    }

    public static void Run(string dataRoot, string reportDir,
        int[] blockSizes, int k, double tau,
        int minShift, double eps, int minPts,
        int minArea, double thresholdFixed)
    {
        string forgedDir = Directory.Exists(Path.Combine(dataRoot, "forged"))
            ? Path.Combine(dataRoot, "forged")
            : Path.Combine(dataRoot, "fake");
        string maskDir = Path.Combine(dataRoot, "mask");
        Directory.CreateDirectory(reportDir);
        string debugDir = Path.Combine(reportDir, "debug");
        Directory.CreateDirectory(debugDir);
        string csvPath = Path.Combine(reportDir, "metrics.csv");
        var files = Directory.GetFiles(forgedDir).OrderBy(f => f).ToArray();
        var sb = new StringBuilder();
        sb.AppendLine("image,thresholdUsed,blockSizes,K,tau,minArea,ROC_AUC,PRAUC,NSS,IoU,Dice,MCC,BoundaryF1,RegionIoU,TimeMs,PeakMemMb");
        foreach (var file in files)
        {
            string name = Path.GetFileName(file);
            string baseName = Path.GetFileNameWithoutExtension(file);
            string maskPath = Path.Combine(maskDir, name);
            if (!File.Exists(maskPath))
            {
                Console.WriteLine($"[WARN] mask not found for {name}");
                continue;
            }
            using var img = Cv2.ImRead(file, ImreadModes.Color);
            using var gt = LoadMask(maskPath);
            if (gt.Width != img.Width || gt.Height != img.Height)
                Cv2.Resize(gt, gt, new Size(img.Width, img.Height), 0, 0, InterpolationFlags.Nearest);
            SaveBase64(gt, Path.Combine(debugDir, $"{baseName}_gt_resized.base64"), true);
            Console.WriteLine($"[{name}] sizeFake={img.Width}x{img.Height}, sizeMask={gt.Width}x{gt.Height}");
            double roc = 0, pr = 0, nss = 0;
            double iou = 0, dice = 0, mcc = 0, bf1 = 0, regIoU = 0;
            double threshold = thresholdFixed;
            long time = 0;
            double mem = ElaAdvanced.MeasurePeakMemory(() =>
            {
                time = ElaAdvanced.MeasureElapsedMs(() =>
                {
                    var merged = new Mat(img.Rows, img.Cols, MatType.CV_32F, Scalar.All(0));
                    int totalMatches = 0, totalClusters = 0, keptClusters = 0;
                    foreach (var bs in blockSizes)
                    {
                        int strideS = Math.Max(1, (int)Math.Round(bs / 3.0));
                        var (rawS, normS, _, candidates, kept, clusters, kClusters) = CopyMoveDense.ComputeCopyMoveMap(img,
                            bs, strideS, k, tau, minShift, eps, minPts, minArea);
                        totalMatches += kept;
                        totalClusters += clusters;
                        keptClusters += kClusters;
                        Cv2.Add(merged, normS, merged);
                        rawS.Dispose();
                        normS.Dispose();
                    }
                    merged.ConvertTo(merged, MatType.CV_32F, 1.0 / blockSizes.Length);
                    var raw = merged.Clone();
                    double p95 = Percentile(raw, 0.95);
                    if (p95 > 0)
                        merged.ConvertTo(merged, MatType.CV_32F, 1.0 / p95);
                    Cv2.Min(merged, 1.0, merged);
                    var kernel3 = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
                    Cv2.MorphologyEx(merged, merged, MorphTypes.Open, kernel3);
                    var norm = merged;
                    if (threshold < 0)
                        threshold = CopyMoveMetrics.ComputeOtsuThreshold(norm);
                    using var bin = CopyMoveMetrics.BinarizeMap(norm, threshold);
                    CopyMoveMetrics.RemoveSmallComponents(bin, minArea);
                    roc = CopyMoveMetrics.ComputeRocAucPixel(gt, norm);
                    pr = CopyMoveMetrics.ComputePraucPixel(gt, norm);
                    nss = CopyMoveMetrics.ComputeNss(gt, norm);
                    iou = CopyMoveMetrics.ComputeIoUPixel(gt, bin);
                    dice = CopyMoveMetrics.ComputeDicePixel(gt, bin);
                    mcc = CopyMoveMetrics.ComputeMccPixel(gt, bin);
                    bf1 = CopyMoveMetrics.ComputeBoundaryF1(gt, bin);
                    regIoU = CopyMoveMetrics.ComputeRegionIoU(gt, bin);
                    SaveBase64(raw, Path.Combine(debugDir, $"{baseName}_map_raw.base64"));
                    SaveBase64(norm, Path.Combine(debugDir, $"{baseName}_map_norm.base64"));
                    SaveBase64(bin, Path.Combine(debugDir, $"{baseName}_map_bin.base64"), true);
                    raw.Dispose();
                    norm.Dispose();
                    bin.Dispose();
                    Console.WriteLine($"[{name}] scales={blockSizes.Length} matches={totalMatches} clusters={totalClusters} kept={keptClusters} IoU={iou:F2} time={time/1000.0:F2}s");
                });
            });
            sb.AppendLine($"{name},{threshold:F3},{string.Join('-', blockSizes)},{k},{tau:F2},{minArea},{roc:F3},{pr:F3},{nss:F3},{iou:F3},{dice:F3},{mcc:F3},{bf1:F3},{regIoU:F3},{time},{mem:F2}");
        }
        File.WriteAllText(csvPath, sb.ToString());
        var lines = sb.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(l => l.Split(',')).ToList();
        if (lines.Count > 0)
        {
            var ious = lines.Select(l => double.Parse(l[9]));
            double meanIoU = ious.Average();
            int successes = ious.Count(v => v > 0.05);
            Console.WriteLine($"\u2713 Copy-Move run finished: {lines.Count} images, mean IoU = {meanIoU:F3}, successes = {successes}");
        }
    }

    static double Percentile(Mat m, double percentile)
    {
        using var flat = m.Reshape(1, m.Rows * m.Cols);
        Cv2.Sort(flat, flat, SortFlags.Ascending);
        long total = flat.Total();
        int idx = (int)Math.Clamp((long)(percentile * (total - 1)), 0, total - 1);
        return flat.Get<float>(idx);
    }
}
