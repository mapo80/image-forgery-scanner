using System;
using System.IO;
using FluentAssertions;
using ImageForensics.Core;
using ImageForensics.Core.Models;
using Xunit;

namespace ImageForensics.Tests;

public class DatasetExifTests
{
    private static string DataDir => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../", "dataset"));

    [Fact]
    public async Task Authentic_Image_HasExpected_ExifScore()
    {
        string path = Path.Combine(DataDir, "authentic", "Au_ani_00001.jpg");
        var analyzer = new ForensicsAnalyzer();
        var opts = new ForensicsOptions
        {
            WorkDir = Path.GetTempPath(),
            NoiseprintModelsDir = AppContext.BaseDirectory
        };
        var res = await analyzer.AnalyzeAsync(path, opts);
        res.ExifScore.Should().BeInRange(0.4, 0.6);
        res.ExifAnomalies.Keys.Should().Contain(new[] { "DateTimeOriginal", "Model" });
        res.ExifAnomalies.Should().NotContainKey("Software");
    }

    [Fact]
    public async Task Tampered_Image_HasExpected_ExifScore()
    {
        string path = Path.Combine(DataDir, "tampered", "Tp_D_CNN_M_B_nat00056_nat00099_11105.jpg");
        var analyzer = new ForensicsAnalyzer();
        var opts = new ForensicsOptions
        {
            WorkDir = Path.GetTempPath(),
            NoiseprintModelsDir = AppContext.BaseDirectory
        };
        var res = await analyzer.AnalyzeAsync(path, opts);
        res.ExifScore.Should().BeGreaterThan(0.7);
        res.ExifAnomalies.Keys.Should().Contain(new[] { "Software", "DateTimeOriginal", "Model" });
    }
}
