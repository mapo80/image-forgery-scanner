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
        int blockSize, int stride, int k, double tau,
        int minShift, double eps, int minPts,
        int morphKernel, int minArea, double thresholdFixed)
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
        sb.AppendLine("image,threshold,blockSize,stride,K,tau,minArea,ROC_AUC,PRAUC,NSS,IoU,Dice,MCC,Fpr95TPR,AP,BoundaryF1,RegionIoU,TimeMs,PeakMemMb");
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
            Console.WriteLine($"[{name}] maskExists={File.Exists(maskPath)} imgSize={img.Width}x{img.Height} maskSize={gt.Width}x{gt.Height}");
            double roc = 0, pr = 0, nss = 0, fpr95 = 0, ap = 0;
            double iou = 0, dice = 0, mcc = 0, bf1 = 0, regIoU = 0;
            double threshold = thresholdFixed;
            long time = 0;
            double mem = ElaAdvanced.MeasurePeakMemory(() =>
            {
                time = ElaAdvanced.MeasureElapsedMs(() =>
                {
                    var (raw, norm, blocks, cand, kept) = CopyMoveDense.ComputeCopyMoveMap(img,
                        blockSize, stride, k, tau, minShift, eps, minPts, morphKernel);
                    if (threshold < 0)
                        threshold = CopyMoveMetrics.ComputeOtsuThreshold(norm);
                    using var bin = CopyMoveMetrics.BinarizeMap(norm, threshold);
                    CopyMoveMetrics.RemoveSmallComponents(bin, minArea);
                    roc = CopyMoveMetrics.ComputeRocAucPixel(gt, norm);
                    pr = CopyMoveMetrics.ComputePraucPixel(gt, norm);
                    nss = CopyMoveMetrics.ComputeNss(gt, norm);
                    fpr95 = CopyMoveMetrics.ComputeFprAt95Tpr(gt, norm);
                    ap = CopyMoveMetrics.ComputeAveragePrecision(gt, norm);
                    iou = CopyMoveMetrics.ComputeIoUPixel(gt, bin);
                    dice = CopyMoveMetrics.ComputeDicePixel(gt, bin);
                    mcc = CopyMoveMetrics.ComputeMccPixel(gt, bin);
                    bf1 = CopyMoveMetrics.ComputeBoundaryF1(gt, bin);
                    regIoU = CopyMoveMetrics.ComputeRegionIoU(gt, bin);
                    var nz = new Mat();
                    Cv2.Compare(norm, 0, nz, CmpType.GT);
                    int nonZero = Cv2.CountNonZero(nz);
                    int nonZeroMap = Cv2.CountNonZero(bin);
                    int nonZeroGt = Cv2.CountNonZero(gt);
                    using var overlapMat = new Mat();
                    Cv2.BitwiseAnd(gt, bin, overlapMat);
                    int overlap = Cv2.CountNonZero(overlapMat);
                    Console.WriteLine($"[{name}] blocks={blocks} candidates={cand} kept={kept} nonZeroPixels={nonZero} time={time} ms");
                    Console.WriteLine($"[{name}] nonZeroMap={nonZeroMap} nonZeroGT={nonZeroGt} overlap={overlap}");
                    SaveBase64(raw, Path.Combine(debugDir, $"{baseName}_map_raw.base64"));
                    SaveBase64(norm, Path.Combine(debugDir, $"{baseName}_map_norm.base64"));
                    SaveBase64(bin, Path.Combine(debugDir, $"{baseName}_map_bin.base64"), true);
                    raw.Dispose();
                    norm.Dispose();
                    bin.Dispose();
                });
            });
            sb.AppendLine($"{name},{threshold:F3},{blockSize},{stride},{k},{tau:F2},{minArea},{roc:F3},{pr:F3},{nss:F3},{iou:F3},{dice:F3},{mcc:F3},{fpr95:F3},{ap:F3},{bf1:F3},{regIoU:F3},{time},{mem:F2}");
        }
        File.WriteAllText(csvPath, sb.ToString());
    }
}
