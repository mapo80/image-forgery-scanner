using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using FluentAssertions;
using ImageForensics.Core;
using ImageForensics.Core.Models;

namespace ImageForensics.Tests;

public class DlSplicingTests
{
    [Theory]
    [InlineData("authentic", 0.15)]
    [InlineData("tampered", 0.05)]
    public async Task AnalyzeSplicing_Dataset_ReturnsExpectedScores(string folder, double threshold)
    {
        var baseDir = AppContext.BaseDirectory;
        var files = Directory.GetFiles(Path.Combine(baseDir, "testdata", "casia2", folder));
        var analyzer = new ForensicsAnalyzer();
        var times = new List<long>();

        foreach (var file in files)
        {
            var sw = Stopwatch.StartNew();
            var res = await analyzer.AnalyzeAsync(file, new ForensicsOptions { WorkDir = "test_results" });
            sw.Stop();
            times.Add(sw.ElapsedMilliseconds);

            if (folder == "authentic")
                res.SplicingScore.Should().BeLessThan(threshold);
            else
                res.SplicingScore.Should().BeGreaterThan(threshold);

            File.Exists(res.SplicingMapPath).Should().BeTrue();
        }

        Console.WriteLine($"Processed {files.Length} images from '{folder}'.");
        Console.WriteLine($"Average time: {times.Average():F1} ms");
    }
}
