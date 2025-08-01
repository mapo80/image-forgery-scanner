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

        Log.Debug("Loading original image {Image}", imagePath);
        using var orig = new MagickImage(imagePath);
        Log.Debug("Cloning image and setting quality to {Quality}", quality);
        using var comp = orig.Clone();
        comp.Quality = quality;
        comp.Format = MagickFormat.Jpeg;
        Log.Debug("Encoding image to JPEG");
        byte[] jpeg = comp.ToByteArray();
        Log.Debug("Reloading compressed image for comparison");
        using var compReloaded = new MagickImage(jpeg);

        Log.Debug("Comparing images to generate ELA diff");
        using var diff = new MagickImage();
        double score = orig.Compare(compReloaded, ErrorMetric.RootMeanSquared, diff);
        Log.Debug("Writing diff map to {MapPath}", mapPath);
        diff.Depth = 8;
        diff.Write(mapPath, MagickFormat.Png);

        Log.Information("ELA analysis completed for {Image}: {Score}", imagePath, score);
        return (score, mapPath);
    }
}
