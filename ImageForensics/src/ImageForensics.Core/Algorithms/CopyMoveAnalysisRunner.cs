using System;
using System.IO;
using System.Linq;
using System.Text;
using OpenCvSharp;

namespace ImageForensics.Core.Algorithms;

public static class CopyMoveAnalysisRunner
{
    static byte[,] LoadMask(string path)
    {
        using var m = Cv2.ImRead(path, ImreadModes.Grayscale);
        int w = m.Width; int h = m.Height;
        var mask = new byte[w, h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                mask[x, y] = m.At<byte>(y, x) > 127 ? (byte)1 : (byte)0;
        return mask;
    }

    public static void Run(
        string datasetDir,
        string csvPath,
        int siftFeatures,
        double loweRatio,
        double clusterEps,
        int clusterMinPts,
        int morphOpen,
        int morphClose,
        string thresholdMode,
        double percentile,
        double minAreaPct)
    {
        string forgedDir = Path.Combine(datasetDir, "forged");
        string maskDir = Path.Combine(datasetDir, "mask");
        Directory.CreateDirectory(Path.GetDirectoryName(csvPath)!);
        var files = Directory.GetFiles(forgedDir).OrderBy(f => f).ToArray();
        var sb = new StringBuilder();
        sb.AppendLine("image,threshold,minAreaPct,kernelOpen,kernelClose,RocAuc,Prauc,NSS,IoU,Dice,MCC,FPR@95TPR,AP,BoundaryF1,RegionIoU,TimeMs,PeakMemMb");
        foreach (var file in files)
        {
            string name = Path.GetFileName(file);
            string maskPath = Path.Combine(maskDir, name);
            if (!File.Exists(maskPath)) continue;
            using var img = Cv2.ImRead(file, ImreadModes.Grayscale);
            var gt = LoadMask(maskPath);
            double roc = 0, pr = 0, nss = 0, fpr95 = 0, ap = 0;
            double iou = 0, dice = 0, mcc = 0, bf1 = 0, regIoU = 0;
            double thr = 0; bool[,] pred = new bool[1,1];
            long time = 0; double mem = ElaAdvanced.MeasurePeakMemory(() =>
            {
                time = ElaAdvanced.MeasureElapsedMs(() =>
                {
                    var map = CopyMoveMetrics.ComputeCopyMoveMap(img, siftFeatures, loweRatio, clusterEps, clusterMinPts, morphOpen, morphClose, 0.99, minAreaPct);
                    roc = CopyMoveMetrics.ComputeRocAucPixel(gt, map);
                    pr = CopyMoveMetrics.ComputePraucPixel(gt, map);
                    nss = CopyMoveMetrics.ComputeNss(gt, map);
                    fpr95 = CopyMoveMetrics.ComputeFprAt95Tpr(gt, map);
                    ap = CopyMoveMetrics.ComputeAveragePrecision(gt, map);
                    thr = thresholdMode.Equals("percentile", StringComparison.OrdinalIgnoreCase)
                        ? CopyMoveMetrics.ComputePercentileThreshold(map, percentile)
                        : CopyMoveMetrics.ComputeOtsuThreshold(map);
                    pred = CopyMoveMetrics.BinarizeMap(map, thr);
                    iou = CopyMoveMetrics.ComputeIoUPixel(gt, pred);
                    dice = CopyMoveMetrics.ComputeDicePixel(gt, pred);
                    mcc = CopyMoveMetrics.ComputeMccPixel(gt, pred);
                    bf1 = CopyMoveMetrics.ComputeBoundaryF1(gt, pred);
                    regIoU = CopyMoveMetrics.ComputeRegionIoU(gt, pred);
                });
            });
            sb.AppendLine($"{name},{thr:F3},{minAreaPct:F3},{morphOpen},{morphClose},{roc:F3},{pr:F3},{nss:F3},{iou:F3},{dice:F3},{mcc:F3},{fpr95:F3},{ap:F3},{bf1:F3},{regIoU:F3},{time},{mem:F2}");
        }
        File.WriteAllText(csvPath, sb.ToString());
    }
}
