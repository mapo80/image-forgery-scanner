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
        string debugDir,
        int siftFeatures,
        double loweRatio,
        double clusterEps,
        int clusterMinPts,
        int morphKernel,
        string thresholdMode,
        double percentileThreshold,
        double fixedThreshold,
        double minAreaPct)
    {
        string fakeDir = Path.Combine(datasetDir, "fake");
        string maskDir = Path.Combine(datasetDir, "mask");
        Directory.CreateDirectory(Path.GetDirectoryName(csvPath)!);
        Directory.CreateDirectory(debugDir);
        var files = Directory.GetFiles(fakeDir).OrderBy(f => f).ToArray();
        var sb = new StringBuilder();
        sb.AppendLine("image,threshold,minArea,kernel,RocAuc,Prauc,Nss,IoU,Dice,MCC,Fpr95TPR,AP,BoundaryF1,RegionIoU,TimeMs,PeakMemMb");
        foreach (var file in files)
        {
            string name = Path.GetFileName(file);
            string baseName = Path.GetFileNameWithoutExtension(file);
            string maskPath = Path.Combine(maskDir, name);
            if (!File.Exists(maskPath)) continue;
            using var img = Cv2.ImRead(file, ImreadModes.Color);
            using var gt = LoadMask(maskPath);
            double roc = 0, pr = 0, nss = 0, fpr95 = 0, ap = 0;
            double iou = 0, dice = 0, mcc = 0, bf1 = 0, regIoU = 0;
            double thr = 0; int minArea = 0;
            long time = 0; double mem = ElaAdvanced.MeasurePeakMemory(() =>
            {
                time = ElaAdvanced.MeasureElapsedMs(() =>
                {
                    var (raw, map) = CopyMoveMetrics.ComputeCopyMoveMap(
                        img, siftFeatures, loweRatio, clusterEps, clusterMinPts,
                        0.001, morphKernel, 0.99, minAreaPct);
                    minArea = (int)(img.Width * img.Height * minAreaPct);
                    roc = CopyMoveMetrics.ComputeRocAucPixel(gt, map);
                    pr = CopyMoveMetrics.ComputePraucPixel(gt, map);
                    nss = CopyMoveMetrics.ComputeNss(gt, map);
                    fpr95 = CopyMoveMetrics.ComputeFprAt95Tpr(gt, map);
                    ap = CopyMoveMetrics.ComputeAveragePrecision(gt, map);
                    var mode = Enum.TryParse<CopyMoveMetrics.ThresholdMode>(thresholdMode, true, out var m)
                        ? m : CopyMoveMetrics.ThresholdMode.Otsu;
                    thr = CopyMoveMetrics.SelectThreshold(map, mode, percentileThreshold, fixedThreshold);
                    using var pred = CopyMoveMetrics.BinarizeMap(map, thr);
                    SaveBase64(raw, Path.Combine(debugDir, $"{baseName}_map_raw.base64"));
                    SaveBase64(map, Path.Combine(debugDir, $"{baseName}_map_norm.base64"));
                    SaveBase64(pred, Path.Combine(debugDir, $"{baseName}_map_bin.base64"), true);
                    iou = CopyMoveMetrics.ComputeIoUPixel(gt, pred);
                    dice = CopyMoveMetrics.ComputeDicePixel(gt, pred);
                    mcc = CopyMoveMetrics.ComputeMccPixel(gt, pred);
                    bf1 = CopyMoveMetrics.ComputeBoundaryF1(gt, pred);
                    regIoU = CopyMoveMetrics.ComputeRegionIoU(gt, pred);
                    raw.Dispose();
                    map.Dispose();
                });
            });
            sb.AppendLine($"{name},{thr:F3},{minArea},{morphKernel},{roc:F3},{pr:F3},{nss:F3},{iou:F3},{dice:F3},{mcc:F3},{fpr95:F3},{ap:F3},{bf1:F3},{regIoU:F3},{time},{mem:F2}");
        }
        File.WriteAllText(csvPath, sb.ToString());
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
}

