using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageMagick;
using Microsoft.Extensions.Logging;
using Plotly.NET;
using Plotly.NET.CSharp;

namespace ImageForensics.Core.Algorithms;

public class ElaAnalysisRunner
{
    private readonly ILogger _logger;
    private readonly int[] _jpegQualities;
    private readonly int _minArea;
    private readonly int _boundaryTol;

    public ElaAnalysisRunner(ILogger logger, int[] jpegQualities, int minArea, int boundaryTolerance)
    {
        _logger = logger;
        _jpegQualities = jpegQualities;
        _minArea = minArea;
        _boundaryTol = boundaryTolerance;
    }

    static byte[,] LoadMask(MagickImage img)
    {
        int w = img.Width;
        int h = img.Height;
        var mask = new byte[w, h];
        using var pixels = img.GetPixels();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                mask[x, y] = pixels.GetPixel(x, y).GetChannel(0) > 127 ? (byte)1 : (byte)0;
        return mask;
    }

    static float[,] AggregateEla(float[][,] maps)
    {
        int w = maps[0].GetLength(0);
        int h = maps[0].GetLength(1);
        var agg = new float[w, h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float max = 0;
                for (int i = 0; i < maps.Length; i++)
                    if (maps[i][x, y] > max) max = maps[i][x, y];
                agg[x, y] = max;
            }
        return agg;
    }

    public void Run(string imagesDir, string masksDir, string outputCsv, string outputHtml)
    {
        var files = Directory.GetFiles(imagesDir).OrderBy(f => f).ToArray();
        var csv = new List<string>();
        csv.Add("Image,RocAuc,Prauc,Nss,Fpr95,Ap,IoU,Dice,Mcc,RegIoU,BndF1,ObjF1,TimeMs,PeakMemMb");
        var rocSeries = new List<double>();
        for (int idx = 0; idx < files.Length; idx++)
        {
            var imgPath = files[idx];
            string name = Path.GetFileName(imgPath);
            string maskPath = Path.Combine(masksDir, Path.GetFileNameWithoutExtension(imgPath) + "_mask.png");
            if (!File.Exists(maskPath)) continue;
            _logger.LogInformation("Processing {Name}", name);
            using var img = new MagickImage(imgPath);
            using var maskImg = new MagickImage(maskPath);
            var gt = LoadMask(maskImg);
            double roc = 0, pr = 0, nss = 0, fpr95 = 0, ap = 0;
            double iou = 0, dice = 0, mcc = 0;
            double regIoU = 0, bndF1 = 0, objF1 = 0;
            long time = 0;
            double mem = ElaAdvanced.MeasurePeakMemory(() =>
            {
                time = ElaAdvanced.MeasureElapsedMs(() =>
                {
                    var maps = ElaAdvanced.ComputeMultiScaleElaMap(img, _jpegQualities);
                    var _ = ElaAdvanced.ExtractElaFeatures(maps).First();
                    var ela = AggregateEla(maps);
                    double thr = ElaAdvanced.ComputeOtsuThreshold(ela);
                    var rawMask = ElaAdvanced.BinarizeElaMap(ela, thr);
                    var pred = ElaAdvanced.RefineMask(rawMask, _minArea);
                    roc = ElaAdvanced.ComputeRocAucPixel(gt, ela);
                    pr = ElaAdvanced.ComputePraucPixel(gt, ela);
                    nss = ElaAdvanced.ComputeNss(gt, ela);
                    fpr95 = ElaAdvanced.ComputeFprAtTpr(gt, ela, 0.95);
                    ap = ElaAdvanced.ComputeAveragePrecision(gt, ela);
                    iou = ElaMetrics.ComputeIoUPixel(gt, pred);
                    dice = ElaMetrics.ComputeDicePixel(gt, pred);
                    mcc = ElaMetrics.ComputeMccPixel(gt, pred);
                    regIoU = ElaAdvanced.ComputeRegionIoU(gt, pred);
                    bndF1 = ElaAdvanced.ComputeBoundaryF1(gt, pred, _boundaryTol);
                    objF1 = ElaAdvanced.ComputeObjectF1(gt, pred);
                });
            });
            rocSeries.Add(roc);
            _logger.LogInformation("[{Name}] ROC_AUC={Roc:F2}, PR_AUC={Pr:F2}, NSS={Nss:F2}, IOU={IoU:F2}, Dice={Dice:F2}, MCC={Mcc:F2}",
                name, roc, pr, nss, iou, dice, mcc);
            _logger.LogInformation("Processed {Current}/{Total} images", idx + 1, files.Length);
            csv.Add($"{name},{roc:F3},{pr:F3},{nss:F3},{fpr95:F3},{ap:F3},{iou:F3},{dice:F3},{mcc:F3},{regIoU:F3},{bndF1:F3},{objF1:F3},{time},{mem:F2}");
        }
        File.WriteAllLines(outputCsv, csv);
        var chart = Plotly.NET.CSharp.Chart.Line<double, double, string>(Enumerable.Range(0, rocSeries.Count).Select(i => (double)i), rocSeries);
        Plotly.NET.CSharp.GenericChartExtensions.SaveHtml(chart, outputHtml);
    }
}
