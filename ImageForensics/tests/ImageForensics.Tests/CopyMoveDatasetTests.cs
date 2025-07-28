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

public class CopyMoveDatasetTests
{
    private static readonly string DataDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../", "dataset", "duplicated"));

    [Fact]
    public async Task Process_Duplicated_Dataset()
    {
        var files = Directory.GetFiles(DataDir);
        var analyzer = new ForensicsAnalyzer();
        var results = new List<(string Name, double Score, long Ms)>();

        foreach (var path in files)
        {
            string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            var opts = new ForensicsOptions { WorkDir = dir, CopyMoveMaskDir = dir };
            var sw = Stopwatch.StartNew();
            var res = await analyzer.AnalyzeAsync(path, opts);
            sw.Stop();
            File.Exists(res.CopyMoveMaskPath).Should().BeTrue();
            results.Add((Path.GetFileName(path), res.CopyMoveScore, sw.ElapsedMilliseconds));
        }

        foreach (var r in results)
            Console.WriteLine($"{r.Name};{r.Score:F3};{r.Ms}");

        results.Should().NotBeEmpty();
    }
}
