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

public class DeepfakeDatasetTests
{
    private static readonly string DataDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../", "dataset", "deepfake"));

    [Fact(Skip = "Requires deepfake dataset and noiseprint models")]
    public async Task Process_Deepfake_Dataset()
    {
        var analyzer = new ForensicsAnalyzer();
        var results = new List<(string Type, string Name, double Score, long Ms)>();

        foreach (string type in new[] { "real", "fake" })
        {
            var files = Directory.GetFiles(Path.Combine(DataDir, type), "*.jpg").Take(3);
            foreach (var file in files)
            {
                string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                Directory.CreateDirectory(dir);

                var opts = new ForensicsOptions
                {
                    WorkDir = dir,
                    NoiseprintMapDir = dir,
                    NoiseprintModelsDir = AppContext.BaseDirectory
                };

                var sw = Stopwatch.StartNew();
                var res = await analyzer.AnalyzeAsync(file, opts);
                sw.Stop();

                results.Add((type, Path.GetFileName(file), res.InpaintingScore, sw.ElapsedMilliseconds));

                if (type == "real")
                    res.InpaintingScore.Should().BeLessThan(0.35);
                else
                    res.InpaintingScore.Should().BeGreaterThan(0.20);
            }
        }

        foreach (var r in results)
            Console.WriteLine($"{r.Type};{r.Name};{r.Score:F3};{r.Ms}");

        results.Should().NotBeEmpty();
    }
}
