using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
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

    static byte[,] LoadMask(Bitmap bmp)
    {
        int w = bmp.Width;
        int h = bmp.Height;
        var mask = new byte[w, h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                mask[x, y] = bmp.GetPixel(x, y).R > 127 ? (byte)1 : (byte)0;
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

    public void Run(string imagesDir, string masksDir, string modelPath, string outputCsv, string outputHtml)
    {
        var files = Directory.GetFiles(imagesDir).OrderBy(f => f).ToArray();
        var csv = new List<string>();
        csv.Add("Image,RocAuc,Prauc,Nss,Fpr95,Ap,RegIoU,BndF1,ObjF1,TimeMs,PeakMemMb");
        var rocSeries = new List<double>();
        foreach (var imgPath in files)
        {
            string name = Path.GetFileName(imgPath);
            string maskPath = Path.Combine(masksDir, Path.GetFileNameWithoutExtension(imgPath) + "_mask.png");
            if (!File.Exists(maskPath)) continue;
            _logger.LogInformation("Processing {Name}", name);
            using var bmp = new Bitmap(imgPath);
            using var maskBmp = new Bitmap(maskPath);
            var gt = LoadMask(maskBmp);
            double roc = 0, pr = 0, nss = 0, fpr95 = 0, ap = 0, regIoU = 0, bndF1 = 0, objF1 = 0;
            long time = 0;
            double mem = ElaAdvanced.MeasurePeakMemory(() =>
            {
                time = ElaAdvanced.MeasureElapsedMs(() =>
                {
                    var maps = ElaAdvanced.ComputeMultiScaleElaMap(bmp, _jpegQualities);
                    var _ = ElaAdvanced.ExtractElaFeatures(maps).First();
                    var ela = AggregateEla(maps);
                    double thr = ElaAdvanced.ComputeOtsuThreshold(ela);
                    var rawMask = ElaAdvanced.BinarizeElaMap(ela, thr);
                    var pred = ElaAdvanced.RefineMask(rawMask, _minArea);
                    roc = ElaAdvanced.ComputeRocAucPixel(gt, pred);
                    pr = ElaAdvanced.ComputePraucPixel(gt, pred);
                    nss = ElaAdvanced.ComputeNss(gt, ela);
                    fpr95 = ElaAdvanced.ComputeFprAtTpr(gt, ela, 0.95);
                    ap = ElaAdvanced.ComputeAveragePrecision(gt, ela);
                    regIoU = ElaAdvanced.ComputeRegionIoU(gt, pred);
                    bndF1 = ElaAdvanced.ComputeBoundaryF1(gt, pred, _boundaryTol);
                    objF1 = ElaAdvanced.ComputeObjectF1(gt, pred);
                });
            });
            rocSeries.Add(roc);
            csv.Add($"{name},{roc:F3},{pr:F3},{nss:F3},{fpr95:F3},{ap:F3},{regIoU:F3},{bndF1:F3},{objF1:F3},{time},{mem:F2}");
        }
        File.WriteAllLines(outputCsv, csv);
        var chart = Plotly.NET.CSharp.Chart.Line<double, double, string>(Enumerable.Range(0, rocSeries.Count).Select(i => (double)i), rocSeries);
        Plotly.NET.CSharp.GenericChartExtensions.SaveHtml(chart, outputHtml);
    }
}
