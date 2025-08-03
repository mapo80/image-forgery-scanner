using System;
using System.IO;
using System.Linq;
using System.Text;
using OpenCvSharp;

namespace ImageForensics.Core.Algorithms;

public static class CopyMoveAnalysisRunner
{
    static Mat LoadMask(string path)
    {
        var m = Cv2.ImRead(path, ImreadModes.Grayscale);
        Cv2.Threshold(m, m, 127, 1, ThresholdTypes.Binary);
        return m;
    }

    public static void Run(
        string datasetDir,
        string csvPath,
        int siftFeatures,
        double loweRatio,
        double clusterEps,
        int clusterMinPts,
        double minClusterPct,
        int morphOpen,
        int morphClose,
        string thresholdMode,
        double percentileThreshold,
        double fixedThreshold,
        double minAreaPct,
        bool dumpMapStats)
    {
        string forgedDir = Path.Combine(datasetDir, "forged");
        string maskDir = Path.Combine(datasetDir, "mask");
        Directory.CreateDirectory(Path.GetDirectoryName(csvPath)!);
        var files = Directory.GetFiles(forgedDir).OrderBy(f => f).ToArray();
        var sb = new StringBuilder();
        sb.AppendLine("image,MapMin,MapMax,MapMean,MapMedian,Map95Quantile,ThresholdUsed,minAreaPct,kernelOpen,kernelClose,RocAuc,Prauc,NSS,IoU,Dice,MCC,FPR@95TPR,AP,BoundaryF1,RegionIoU,TimeMs,PeakMemMb");
        foreach (var file in files)
        {
            string name = Path.GetFileName(file);
            string maskPath = Path.Combine(maskDir, name);
            if (!File.Exists(maskPath)) continue;
            using var img = Cv2.ImRead(file, ImreadModes.Color);
            using var gt = LoadMask(maskPath);
            double roc = 0, pr = 0, nss = 0, fpr95 = 0, ap = 0;
            double iou = 0, dice = 0, mcc = 0, bf1 = 0, regIoU = 0;
            double thr = 0; double mapMin = 0, mapMax = 0, mapMean = 0, mapMedian = 0, mapQ95 = 0;
            long time = 0; double mem = ElaAdvanced.MeasurePeakMemory(() =>
            {
                time = ElaAdvanced.MeasureElapsedMs(() =>
                {
                    using var map = CopyMoveMetrics.ComputeCopyMoveMap(img, siftFeatures, loweRatio, clusterEps, clusterMinPts, minClusterPct, morphOpen, morphClose, 0.99, minAreaPct);
                    var stats = CopyMoveMetrics.GetMapStats(map);
                    mapMin = stats.Min; mapMax = stats.Max; mapMean = stats.Mean; mapMedian = stats.Median; mapQ95 = stats.Q95;
                    if (dumpMapStats)
                        Console.WriteLine($"{name}: min {mapMin:F3} max {mapMax:F3} mean {mapMean:F3} median {mapMedian:F3} q95 {mapQ95:F3}");
                    roc = CopyMoveMetrics.ComputeRocAucPixel(gt, map);
                    pr = CopyMoveMetrics.ComputePraucPixel(gt, map);
                    nss = CopyMoveMetrics.ComputeNss(gt, map);
                    fpr95 = CopyMoveMetrics.ComputeFprAt95Tpr(gt, map);
                    ap = CopyMoveMetrics.ComputeAveragePrecision(gt, map);
                    var mode = Enum.TryParse<CopyMoveMetrics.ThresholdMode>(thresholdMode, true, out var m)
                        ? m : CopyMoveMetrics.ThresholdMode.Otsu;
                    thr = CopyMoveMetrics.SelectThreshold(map, mode, percentileThreshold, fixedThreshold);
                    using var pred = CopyMoveMetrics.BinarizeMap(map, thr);
                    iou = CopyMoveMetrics.ComputeIoUPixel(gt, pred);
                    dice = CopyMoveMetrics.ComputeDicePixel(gt, pred);
                    mcc = CopyMoveMetrics.ComputeMccPixel(gt, pred);
                    bf1 = CopyMoveMetrics.ComputeBoundaryF1(gt, pred);
                    regIoU = CopyMoveMetrics.ComputeRegionIoU(gt, pred);
                });
            });
            sb.AppendLine($"{name},{mapMin:F3},{mapMax:F3},{mapMean:F3},{mapMedian:F3},{mapQ95:F3},{thr:F3},{minAreaPct:F3},{morphOpen},{morphClose},{roc:F3},{pr:F3},{nss:F3},{iou:F3},{dice:F3},{mcc:F3},{fpr95:F3},{ap:F3},{bf1:F3},{regIoU:F3},{time},{mem:F2}");
        }
        File.WriteAllText(csvPath, sb.ToString());
    }
}
