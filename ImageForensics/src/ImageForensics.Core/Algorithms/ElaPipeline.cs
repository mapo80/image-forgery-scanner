using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using OpenCvSharp;
using ImageMagick;

namespace ImageForensics.Core.Algorithms;

public static class ElaPipeline
{
    public static (double Score, string MapPath) Analyze(
        string imagePath,
        string workDir,
        int jpegQuality,
        int windowSize,
        double k,
        int minArea,
        int kernelSize)
    {
        Directory.CreateDirectory(workDir);
        using var img = new MagickImage(imagePath);
        var ela = ElaAdvanced.ComputeElaMap(img, jpegQuality);
        double thr = ComputeSauvolaThreshold(ela, windowSize, k);
        var rawMask = ElaMetrics.BinarizeElaMap(ela, thr);
        var refined = RefineMask(rawMask, minArea, kernelSize);

        string baseName = Path.GetFileNameWithoutExtension(imagePath);
        string mapPath = Path.Combine(workDir, $"{baseName}_ela.png");
        int w = ela.GetLength(0);
        int h = ela.GetLength(1);
        var buffer = new byte[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                buffer[y * w + x] = (byte)Math.Round(Math.Clamp(ela[x, y], 0f, 1f) * 255.0);
        var settings = new MagickReadSettings { Width = w, Height = h, Format = MagickFormat.Gray, Depth = 8 };
        using (var outImg = new MagickImage(buffer, settings))
            outImg.Write(mapPath);

        int count = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (refined[x, y]) count++;
        double score = w * h == 0 ? 0 : (double)count / (w * h);

        return (score, mapPath);
    }

    public static float[,] ComputeElaMap(Bitmap img, int jpegQuality = 90)
    {
        using var ms = new MemoryStream();
        var codec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
        var encParams = new EncoderParameters(1);
        encParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, jpegQuality);
        img.Save(ms, codec, encParams);
        ms.Position = 0;
        using var rec = new Bitmap(ms);
        int w = img.Width;
        int h = img.Height;
        var map = new float[w, h];
        float maxDiff = 0f;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var o = img.GetPixel(x, y);
                var r = rec.GetPixel(x, y);
                float diff = MathF.Max(MathF.Abs(o.R - r.R), MathF.Max(MathF.Abs(o.G - r.G), MathF.Abs(o.B - r.B)));
                map[x, y] = diff;
                if (diff > maxDiff) maxDiff = diff;
            }
        }
        if (maxDiff > 0)
        {
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    map[x, y] /= maxDiff;
        }
        return map;
    }

    public static double ComputeSauvolaThreshold(float[,] elaMap, int windowSize, double k)
    {
        int w = elaMap.GetLength(0);
        int h = elaMap.GetLength(1);
        int r = windowSize / 2;
        double R = 1.0;
        double sumT = 0;
        int count = 0;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int x0 = Math.Max(0, x - r);
                int x1 = Math.Min(w - 1, x + r);
                int y0 = Math.Max(0, y - r);
                int y1 = Math.Min(h - 1, y + r);
                double m = 0, s = 0; int n = 0;
                for (int yy = y0; yy <= y1; yy++)
                    for (int xx = x0; xx <= x1; xx++)
                    {
                        double v = elaMap[xx, yy];
                        m += v; n++;
                    }
                m /= Math.Max(1, n);
                for (int yy = y0; yy <= y1; yy++)
                    for (int xx = x0; xx <= x1; xx++)
                        s += Math.Pow(elaMap[xx, yy] - m, 2);
                s = Math.Sqrt(s / Math.Max(1, n));
                double t = m * (1 + k * (s / R - 1));
                sumT += t; count++;
            }
        }
        return count == 0 ? 0 : sumT / count;
    }

    public static double[,] ComputeMultiBlockOtsu(float[,] elaMap, int blocksX, int blocksY)
    {
        int w = elaMap.GetLength(0);
        int h = elaMap.GetLength(1);
        var map = new double[blocksX, blocksY];
        int bw = Math.Max(1, w / blocksX);
        int bh = Math.Max(1, h / blocksY);
        for (int by = 0; by < blocksY; by++)
        {
            for (int bx = 0; bx < blocksX; bx++)
            {
                int x0 = bx * bw;
                int y0 = by * bh;
                int x1 = bx == blocksX - 1 ? w : x0 + bw;
                int y1 = by == blocksY - 1 ? h : y0 + bh;
                var block = new float[x1 - x0, y1 - y0];
                for (int yy = y0; yy < y1; yy++)
                    for (int xx = x0; xx < x1; xx++)
                        block[xx - x0, yy - y0] = elaMap[xx, yy];
                map[bx, by] = ElaMetrics.ComputeOtsuThreshold(block);
            }
        }
        return map;
    }

    public static bool[,] RefineMask(bool[,] rawMask, int minArea = 100, int kernelSize = 5)
    {
        int w = rawMask.GetLength(0);
        int h = rawMask.GetLength(1);
        using var mat = new Mat(h, w, MatType.CV_8UC1);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                mat.Set(y, x, rawMask[x, y] ? (byte)255 : (byte)0);
        using var labels = new Mat();
        using var stats = new Mat();
        using var cents = new Mat();
        Cv2.ConnectedComponentsWithStats(mat, labels, stats, cents);
        for (int i = 1; i < stats.Rows; i++)
        {
            int area = stats.Get<int>(i, (int)ConnectedComponentsTypes.Area);
            if (area < minArea)
            {
                using var cmp = new Mat();
                Cv2.Compare(labels, i, cmp, CmpType.EQ);
                mat.SetTo(Scalar.All(0), cmp);
            }
        }
        var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(kernelSize, kernelSize));
        Cv2.MorphologyEx(mat, mat, MorphTypes.Close, kernel);
        Cv2.MorphologyEx(mat, mat, MorphTypes.Open, kernel);
        var result = new bool[w, h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                result[x, y] = mat.Get<byte>(y, x) > 0;
        return result;
    }

    public static void Run(string imagesDir, string masksDir, int jpegQuality, int sauvolaWindow, double sauvolaK, int blocksX, int blocksY, int minArea, int kernelSize, int sampleCount = 0, string readmePath = "README.md")
    {
        var rows = new List<string>();
        var samples = new List<string>();
        var imgFiles = Directory.GetFiles(imagesDir).Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)).OrderBy(f => f);
        int processed = 0;
        foreach (var imgPath in imgFiles)
        {
            var name = Path.GetFileName(imgPath);
            var maskPath = Path.Combine(masksDir, Path.ChangeExtension(name, ".png"));
            if (!File.Exists(maskPath))
                continue;
            using var bmp = new Bitmap(imgPath);
            var gt = LoadMask(maskPath);
            var ela = ComputeElaMap(bmp, jpegQuality);
            double thr = ComputeSauvolaThreshold(ela, sauvolaWindow, sauvolaK);
            var rawMask = ElaMetrics.BinarizeElaMap(ela, thr);
            var refined = RefineMask(rawMask, minArea, kernelSize);
            double roc = ElaMetrics.ComputeRocAucPixel(gt, ela);
            double nss = ElaMetrics.ComputeNss(gt, ela);
            double iou = ElaMetrics.ComputeIoUPixel(gt, refined);
            double dice = ElaMetrics.ComputeDicePixel(gt, refined);
            double mcc = ElaMetrics.ComputeMccPixel(gt, refined);
            Console.WriteLine($"Image: {name} | RocAuc={roc:F2} | NSS={nss:F2} | IoU={iou:F2} | Dice={dice:F2} | MCC={mcc:F2}");
            rows.Add($"| {name} | {roc:F2} | {nss:F2} | {iou:F2} | {dice:F2} | {mcc:F2} |");
            if (processed < sampleCount)
            {
                var elaBase = Path.ChangeExtension(name, "_ela.png.base64");
                var maskBase = Path.ChangeExtension(name, "_mask.png.base64");
                SaveBase64(ToBitmap(ela), elaBase);
                SaveBase64(ToBitmap(refined), maskBase);
                samples.Add($"![ELA]({elaBase})\n![Mask]({maskBase})");
            }
            processed++;
        }
        var sb = new StringBuilder();
        sb.AppendLine("| Immagine | RocAuc | NSS | IoU | Dice | MCC |");
        sb.AppendLine("|----------|--------|-----|-----|------|-----|");
        foreach (var r in rows) sb.AppendLine(r);
        foreach (var s in samples) sb.AppendLine(s);
        File.AppendAllText(readmePath, "\n" + sb.ToString());
    }

    static byte[,] LoadMask(string path)
    {
        using var bmp = new Bitmap(path);
        int w = bmp.Width;
        int h = bmp.Height;
        var mask = new byte[w, h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                mask[x, y] = bmp.GetPixel(x, y).R > 127 ? (byte)1 : (byte)0;
        return mask;
    }

    static Bitmap ToBitmap(float[,] map)
    {
        int w = map.GetLength(0);
        int h = map.GetLength(1);
        var bmp = new Bitmap(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int v = (int)Math.Round(Math.Clamp(map[x, y], 0f, 1f) * 255.0);
                bmp.SetPixel(x, y, Color.FromArgb(v, v, v));
            }
        return bmp;
    }

    static Bitmap ToBitmap(bool[,] mask)
    {
        int w = mask.GetLength(0);
        int h = mask.GetLength(1);
        var bmp = new Bitmap(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                bmp.SetPixel(x, y, mask[x, y] ? Color.White : Color.Black);
        return bmp;
    }

    static void SaveBase64(Bitmap bmp, string path)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        var b64 = Convert.ToBase64String(ms.ToArray());
        File.WriteAllText(path, b64);
    }
}
