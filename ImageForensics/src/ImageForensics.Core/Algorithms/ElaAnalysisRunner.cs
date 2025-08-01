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
    private readonly double _tMin;
    private readonly double _tMax;
    private readonly double _tStep;
    private readonly IEnumerable<RefineParams> _paramSpace;
    private readonly int _boundaryTol;
    private readonly int _sauvolaWindow;
    private readonly double _sauvolaK;

    public ElaAnalysisRunner(ILogger logger, int[] jpegQualities, double tMin, double tMax, double tStep,
        IEnumerable<RefineParams> paramSpace, int boundaryTolerance, int sauvolaWindow, double sauvolaK)
    {
        _logger = logger;
        _jpegQualities = jpegQualities;
        _tMin = tMin;
        _tMax = tMax;
        _tStep = tStep;
        _paramSpace = paramSpace;
        _boundaryTol = boundaryTolerance;
        _sauvolaWindow = sauvolaWindow;
        _sauvolaK = sauvolaK;
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
        csv.Add("Image,Threshold,MinArea,Kernel,RocAuc,Prauc,Nss,IoU,Dice,Mcc,Bf1,Fpr95,Ap,BoundaryF1,RegionIoU,TimeMs,PeakMemMb");
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
            double iou = 0, dice = 0, mcc = 0, bf1 = 0;
            double regIoU = 0, boundaryF1 = 0;
            double bestThr = 0; RefineParams? bestParams = null;
            long time = 0;
            double mem = ElaAdvanced.MeasurePeakMemory(() =>
            {
                time = ElaAdvanced.MeasureElapsedMs(() =>
                {
                    var maps = ElaAdvanced.ComputeMultiScaleElaMap(img, _jpegQualities);
                    _ = ElaAdvanced.ExtractElaFeatures(maps).First();
                    var ela = AggregateEla(maps);
                    _ = ElaAdvanced.ComputeSauvolaThreshold(ela, _sauvolaWindow, _sauvolaK);
                    bestThr = ElaAdvanced.SweepThresholds(ela, _tMin, _tMax, _tStep, m => ElaMetrics.ComputeMccPixel(gt, m)).First();
                    var rawMask = ElaAdvanced.BinarizeElaMap(ela, bestThr);
                    var morphRes = ElaAdvanced.SweepMorphology(rawMask, _paramSpace, m => ElaMetrics.ComputeMccPixel(gt, m)).First();
                    var pred = morphRes.Mask; bestParams = morphRes.Params;
                    roc = ElaAdvanced.ComputeRocAucPixel(gt, ela);
                    pr = ElaAdvanced.ComputePraucPixel(gt, ela);
                    nss = ElaAdvanced.ComputeNss(gt, ela);
                    fpr95 = ElaAdvanced.ComputeFprAtTpr(gt, ela, 0.95);
                    ap = ElaAdvanced.ComputeAveragePrecision(gt, ela);
                    iou = ElaMetrics.ComputeIoUPixel(gt, pred);
                    dice = ElaMetrics.ComputeDicePixel(gt, pred);
                    mcc = ElaMetrics.ComputeMccPixel(gt, pred);
                    bf1 = ElaMetrics.ComputeDicePixel(gt, pred);
                    boundaryF1 = ElaMetrics.ComputeBoundaryF1(gt, pred, _boundaryTol);
                    regIoU = ElaAdvanced.ComputeRegionIoU(gt, pred);
                });
            });
            rocSeries.Add(roc);
            _logger.LogInformation("[{Name}] ROC_AUC={Roc:F2}, PR_AUC={Pr:F2}, NSS={Nss:F2}, IOU={IoU:F2}, Dice={Dice:F2}, MCC={Mcc:F2}",
                name, roc, pr, nss, iou, dice, mcc);
            _logger.LogInformation("Processed {Current}/{Total} images", idx + 1, files.Length);
            csv.Add($"{name},{bestThr:F3},{bestParams?.MinArea},{bestParams?.KernelSize},{roc:F3},{pr:F3},{nss:F3},{iou:F3},{dice:F3},{mcc:F3},{bf1:F3},{fpr95:F3},{ap:F3},{boundaryF1:F3},{regIoU:F3},{time},{mem:F2}");
        }
        File.WriteAllLines(outputCsv, csv);
        var chart = Plotly.NET.CSharp.Chart.Line<double, double, string>(Enumerable.Range(0, rocSeries.Count).Select(i => (double)i), rocSeries);
        Plotly.NET.CSharp.GenericChartExtensions.SaveHtml(chart, outputHtml);
    }
}
