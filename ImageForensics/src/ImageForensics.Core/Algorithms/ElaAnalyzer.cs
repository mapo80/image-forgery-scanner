using System.IO;
using ImageMagick;

namespace ImageForensics.Core.Algorithms;

public static class ElaAnalyzer
{
    public static (double Score, string MapPath) Analyze(
        string imagePath,
        string workDir,
        int quality)
    {
        Directory.CreateDirectory(workDir);

        string baseName = Path.GetFileNameWithoutExtension(imagePath);
        string mapPath = Path.Combine(workDir, $"{baseName}_ela.png");

        using var orig = new MagickImage(imagePath);
        using var comp = orig.Clone();
        comp.Quality = quality;

        using var ms = new MemoryStream();
        comp.Write(ms, MagickFormat.Jpeg);
        ms.Position = 0;
        using var compReloaded = new MagickImage(ms);

        using var diff = orig.Clone();
        diff.Composite(compReloaded, CompositeOperator.Difference);
        diff.Depth = 8;
        diff.Write(mapPath, MagickFormat.Png);

        var stats = diff.Statistics();
        double score = stats.GetChannel(PixelChannel.Red)!.Mean / Quantum.Max;
        return (score, mapPath);
    }
}
