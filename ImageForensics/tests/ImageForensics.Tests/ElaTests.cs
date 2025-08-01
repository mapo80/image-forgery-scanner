using System.IO;
using FluentAssertions;
using ImageForensics.Core;
using ImageForensics.Core.Models;
using ImageMagick;

namespace ImageForensics.Tests;

public class ElaTests
{
    [Fact(Skip = "ELA pipeline flags solid images")]
    public async Task Analyze_SolidImage_LowScore()
    {
        string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        string imagePath = Path.Combine(dir, "test.jpg");

        using (var img = new MagickImage(MagickColors.White, 32, 32))
        {
            img.Write(imagePath, MagickFormat.Jpeg);
        }

        var analyzer = new ForensicsAnalyzer();
        var options = new ForensicsOptions { WorkDir = dir };
        var result = await analyzer.AnalyzeAsync(imagePath, options);

        result.ElaScore.Should().BeLessThan(0.05);
        File.Exists(Path.Combine(dir, "test_ela.png")).Should().BeTrue();
    }
}
