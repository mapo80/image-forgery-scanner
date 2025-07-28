using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using ImageForensics.Core;
using ImageForensics.Core.Models;
using Xunit;

namespace ImageForensics.Tests;

public class MetadataCheckerDatasetTests
{
    private static readonly string DataDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../", "dataset", "exif"));

    [Fact]
    public async Task Process_Original_Metadata()
    {
        var analyzer = new ForensicsAnalyzer();
        var results = new List<(string Name, double Score, long Ms)>();
        var files = Directory.GetFiles(Path.Combine(DataDir, "original"), "*.jpg").Take(3);
        foreach (var file in files)
        {
            string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            var opts = new ForensicsOptions
            {
                WorkDir = dir,
                MetadataMapDir = dir,
                NoiseprintModelsDir = AppContext.BaseDirectory
            };
            var sw = Stopwatch.StartNew();
            var res = await analyzer.AnalyzeAsync(file, opts);
            sw.Stop();
            results.Add((Path.GetFileName(file), res.ExifScore, sw.ElapsedMilliseconds));
            res.ExifAnomalies.Keys.Should().Contain(new[] { "DateTimeOriginal", "Model" });
            res.ExifAnomalies.Should().NotContainKey("GPS");
        }
        foreach (var r in results)
            Console.WriteLine($"original;{r.Name};{r.Score:F2};{r.Ms};OK");
        double avg = results.Average(r => r.Ms);
        Console.WriteLine($"original;average;;{avg:F0};");
    }

    [Fact]
    public async Task Process_Edited_Metadata()
    {
        var analyzer = new ForensicsAnalyzer();
        var results = new List<(string Name, double Score, long Ms)>();
        var files = Directory.GetFiles(Path.Combine(DataDir, "exif_edited"), "*.jpg").Take(3);
        foreach (var file in files)
        {
            string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            var opts = new ForensicsOptions
            {
                WorkDir = dir,
                MetadataMapDir = dir,
                NoiseprintModelsDir = AppContext.BaseDirectory
            };
            var sw = Stopwatch.StartNew();
            var res = await analyzer.AnalyzeAsync(file, opts);
            sw.Stop();
            results.Add((Path.GetFileName(file), res.ExifScore, sw.ElapsedMilliseconds));
            res.ExifAnomalies.Keys.Should().Contain(new[] { "Model", "GPS" });
            res.ExifAnomalies.Should().NotContainKey("DateTimeOriginal");
        }
        foreach (var r in results)
            Console.WriteLine($"edited;{r.Name};{r.Score:F2};{r.Ms};OK");
        double avg = results.Average(r => r.Ms);
        Console.WriteLine($"edited;average;;{avg:F0};");
    }
}
