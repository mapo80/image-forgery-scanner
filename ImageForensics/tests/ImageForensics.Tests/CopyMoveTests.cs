using System.IO;
using FluentAssertions;
using ImageForensics.Core;
using ImageForensics.Core.Models;

namespace ImageForensics.Tests;

public class CopyMoveTests
{
    private static string DataDir => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../", "dataset"));

    [Fact]
    public async Task Tampered_Image_ProducesMask()
    {
        string img = Path.Combine(DataDir, "tampered", "Tp_D_CNN_M_N_ind00091_ind00091_10647.jpg");
        string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);

        var analyzer = new ForensicsAnalyzer();
        var options = new ForensicsOptions { WorkDir = dir, CopyMoveMaskDir = dir };
        var res = await analyzer.AnalyzeAsync(img, options);

        res.CopyMoveScore.Should().BeGreaterThanOrEqualTo(0);
        File.Exists(res.CopyMoveMaskPath).Should().BeTrue();
    }

    [Fact]
    public async Task Authentic_Image_ProducesMask()
    {
        string img = Path.Combine(DataDir, "authentic", "Au_ani_00001.jpg");
        string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);

        var analyzer = new ForensicsAnalyzer();
        var options = new ForensicsOptions { WorkDir = dir, CopyMoveMaskDir = dir };
        var res = await analyzer.AnalyzeAsync(img, options);

        res.CopyMoveScore.Should().BeGreaterThanOrEqualTo(0);
        File.Exists(res.CopyMoveMaskPath).Should().BeTrue();
    }
}
