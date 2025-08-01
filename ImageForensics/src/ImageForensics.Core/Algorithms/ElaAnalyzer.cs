using System.IO;
using ImageMagick;
using Serilog;

namespace ImageForensics.Core.Algorithms;

public static class ElaAnalyzer
{
    public static (double Score, string MapPath) Analyze(
        string imagePath,
        string workDir,
        int quality)
    {
        Log.Information("ELA analysis for {Image} with quality {Quality}", imagePath, quality);
        Directory.CreateDirectory(workDir);

        string baseName = Path.GetFileNameWithoutExtension(imagePath);
        string mapPath = Path.Combine(workDir, $"{baseName}_ela.png");

        using var orig = new MagickImage(imagePath);
        using var comp = orig.Clone();
        comp.Quality = quality;
        byte[] jpeg = comp.ToByteArray(MagickFormat.Jpeg);
        using var compReloaded = new MagickImage(jpeg);

        using var diff = new MagickImage();
        double score = orig.Compare(compReloaded, ErrorMetric.RootMeanSquared, diff);
        diff.Depth = 8;
        diff.Write(mapPath, MagickFormat.Png);

        Log.Information("ELA analysis completed for {Image}: {Score}", imagePath, score);
        return (score, mapPath);
    }
}
