using System;
using System.IO;
using FluentAssertions;
using ImageForensics.Core;
using ImageForensics.Core.Models;
using Xunit;

namespace ImageForensics.Tests;

public class ExifTests
{
    [Fact]
    public async Task Clean_Metadata_No_Anomalies()
    {
        string img = Path.Combine(AppContext.BaseDirectory, "testdata", "clean_exif.jpg");
        string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);

        var analyzer = new ForensicsAnalyzer();
        var opts = new ForensicsOptions
        {
            WorkDir = dir,
            CopyMoveMaskDir = dir,
            SplicingMapDir = dir,
            NoiseprintMapDir = dir,
            MetadataMapDir = dir,
            NoiseprintModelsDir = AppContext.BaseDirectory
        };
        var res = await analyzer.AnalyzeAsync(img, opts);

        res.ExifScore.Should().BeLessThan(0.05);
        res.ExifAnomalies.Should().BeEmpty();
    }

    [Fact]
    public async Task Edited_Metadata_Detects_Anomalies()
    {
        string img = Path.Combine(AppContext.BaseDirectory, "testdata", "edited_exif.jpg");
        string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);

        var analyzer = new ForensicsAnalyzer();
        var opts = new ForensicsOptions
        {
            WorkDir = dir,
            CopyMoveMaskDir = dir,
            SplicingMapDir = dir,
            NoiseprintMapDir = dir,
            MetadataMapDir = dir,
            NoiseprintModelsDir = AppContext.BaseDirectory
        };
        var res = await analyzer.AnalyzeAsync(img, opts);

        res.ExifScore.Should().BeGreaterThanOrEqualTo(0.5);
        res.ExifAnomalies.Keys.Should().Contain(new[] { "Software", "Model" });
    }
}
