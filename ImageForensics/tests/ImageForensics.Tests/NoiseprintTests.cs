using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using ImageForensics.Core;
using ImageForensics.Core.Models;
using Xunit;

namespace ImageForensics.Tests;

public class NoiseprintTests
{
    [Fact]
    public async Task Clean_Image_LowScore()
    {
        string img = Path.Combine(AppContext.BaseDirectory, "testdata", "clean.png");
        string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);

        var analyzer = new ForensicsAnalyzer();
        var opts = new ForensicsOptions { WorkDir = dir, CopyMoveMaskDir = dir, SplicingMapDir = dir, NoiseprintMapDir = dir };
        var res = await analyzer.AnalyzeAsync(img, opts);

        res.InpaintingScore.Should().BeLessThan(0.35);
        File.Exists(res.InpaintingMapPath).Should().BeTrue();
    }

    [Fact]
    public async Task Inpainted_Image_HighScore()
    {
        string img = Path.Combine(AppContext.BaseDirectory, "testdata", "inpainting.png");
        string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);

        var analyzer = new ForensicsAnalyzer();
        var opts = new ForensicsOptions { WorkDir = dir, CopyMoveMaskDir = dir, SplicingMapDir = dir, NoiseprintMapDir = dir };
        var res = await analyzer.AnalyzeAsync(img, opts);

        res.InpaintingScore.Should().BeGreaterThan(0.20);
        File.Exists(res.InpaintingMapPath).Should().BeTrue();
    }
}
