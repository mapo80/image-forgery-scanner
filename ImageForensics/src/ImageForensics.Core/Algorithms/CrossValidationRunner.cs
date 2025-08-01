using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageMagick;
using Microsoft.Extensions.Logging;
using Chart = Plotly.NET.CSharp.Chart;
using GenericChart = Plotly.NET.CSharp.GenericChartExtensions;

namespace ImageForensics.Core.Algorithms;

public class CrossValidationRunner
{
    private readonly ILogger _logger;

    public CrossValidationRunner(ILogger logger)
    {
        _logger = logger;
    }

    static byte[,] LoadMask(string path)
    {
        using var img = new MagickImage(path);
        int w = img.Width;
        int h = img.Height;
        var mask = new byte[w, h];
        using var pixels = img.GetPixels();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                mask[x, y] = pixels.GetPixel(x, y).GetChannel(0) > 127 ? (byte)1 : (byte)0;
        return mask;
    }

    static double Std(IEnumerable<double> vals)
    {
        var arr = vals.ToArray();
        if (arr.Length == 0) return 0;
        double mean = arr.Average();
        double sum = arr.Sum(v => (v - mean) * (v - mean));
        return Math.Sqrt(sum / arr.Length);
    }

    public void Run(string imagesDir, string masksDir, int folds,
        IEnumerable<double> thresholdCandidates,
        IEnumerable<RefineParams> refineParamsList,
        string outputCsv, string outputHtml)
    {
        var files = Directory.GetFiles(imagesDir).OrderBy(f => f).ToArray();
        var pairs = files.Select(f => (Image: f, Mask: Path.Combine(masksDir,
            Path.GetFileNameWithoutExtension(f) + "_mask.png")))
            .Where(p => File.Exists(p.Mask)).ToArray();
        int n = pairs.Length;
        var elaMaps = new float[n][,];
        var gtMasks = new byte[n][,];
        for (int i = 0; i < n; i++)
        {
            using var img = new MagickImage(pairs[i].Image);
            elaMaps[i] = ElaAdvanced.ComputeElaMap(img);
            gtMasks[i] = LoadMask(pairs[i].Mask);
        }
        int foldSize = (int)Math.Ceiling(n / (double)folds);
        var results = new List<(int Fold, double Thr, int MinArea, int Kernel, double MeanIoU, double MeanDice, double MeanMcc, double StdIoU, double StdDice, double StdMcc)>();
        for (int f = 0; f < folds; f++)
        {
            var testIdx = Enumerable.Range(f * foldSize, Math.Min(foldSize, n - f * foldSize)).ToHashSet();
            var trainIdx = Enumerable.Range(0, n).Where(i => !testIdx.Contains(i)).ToArray();
            double bestMcc = double.NegativeInfinity;
            double bestThr = 0;
            RefineParams bestParams = default;
            foreach (var thr in thresholdCandidates)
            {
                foreach (var rp in refineParamsList)
                {
                    var mccVals = new List<double>();
                    foreach (var idx in trainIdx)
                    {
                        var ela = elaMaps[idx];
                        var raw = ElaAdvanced.BinarizeElaMap(ela, thr);
                        var refined = ElaAdvanced.RefineMask(raw, rp.MinArea, rp.KernelSize);
                        var gt = gtMasks[idx];
                        mccVals.Add(ElaMetrics.ComputeMccPixel(gt, refined));
                    }
                    double meanMcc = mccVals.Count > 0 ? mccVals.Average() : 0;
                    if (meanMcc > bestMcc)
                    {
                        bestMcc = meanMcc;
                        bestThr = thr;
                        bestParams = rp;
                    }
                }
            }
            var iouVals = new List<double>();
            var diceVals = new List<double>();
            var mccValsTest = new List<double>();
            foreach (var idx in testIdx)
            {
                var ela = elaMaps[idx];
                var raw = ElaAdvanced.BinarizeElaMap(ela, bestThr);
                var refined = ElaAdvanced.RefineMask(raw, bestParams.MinArea, bestParams.KernelSize);
                var gt = gtMasks[idx];
                // mandatory metrics
                iouVals.Add(ElaMetrics.ComputeIoUPixel(gt, refined));
                diceVals.Add(ElaMetrics.ComputeDicePixel(gt, refined));
                mccValsTest.Add(ElaMetrics.ComputeMccPixel(gt, refined));
                // additional metrics (not aggregated)
                _ = ElaMetrics.ComputeFprAtTpr(gt, ela, 0.95);
                _ = ElaMetrics.ComputeAveragePrecision(gt, ela);
                _ = ElaMetrics.ComputeBoundaryF1(gt, refined, 2);
                _ = ElaMetrics.ComputeRegionIoU(gt, refined);
            }
            results.Add((f + 1, bestThr, bestParams.MinArea, bestParams.KernelSize,
                iouVals.Average(), diceVals.Average(), mccValsTest.Average(),
                Std(iouVals), Std(diceVals), Std(mccValsTest)));
            _logger.LogInformation("Fold {Fold} -> Thr={Thr:F2}, MinArea={MA}, Kernel={K}", f + 1, bestThr, bestParams.MinArea, bestParams.KernelSize);
        }
        using var sw = new StreamWriter(outputCsv);
        sw.WriteLine("Fold,Threshold,MinArea,KernelSize,MeanIoU,MeanDice,MeanMCC,StdIoU,StdDice,StdMCC");
        foreach (var r in results)
            sw.WriteLine($"{r.Fold},{r.Thr:F3},{r.MinArea},{r.Kernel},{r.MeanIoU:F3},{r.MeanDice:F3},{r.MeanMcc:F3},{r.StdIoU:F3},{r.StdDice:F3},{r.StdMcc:F3}");
        var foldsArr = results.Select(r => (double)r.Fold);
        var chart = Chart.Combine(new[]{
            Chart.Line<double,double,string>(foldsArr, results.Select(r => r.MeanIoU), Name:"IoU"),
            Chart.Line<double,double,string>(foldsArr, results.Select(r => r.MeanDice), Name:"Dice"),
            Chart.Line<double,double,string>(foldsArr, results.Select(r => r.MeanMcc), Name:"MCC")
        });
        GenericChart.SaveHtml(chart, outputHtml);
    }
}
