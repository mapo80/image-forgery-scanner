using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using ImageForensics.Core;
using ImageForensics.Core.Models;
using Xunit;

namespace ImageForensics.Tests;

public class DatasetTests
{
    private static string DataDir => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../", "dataset"));

    public static IEnumerable<object[]> AuthenticImages()
        => Directory.GetFiles(Path.Combine(DataDir, "authentic"), "*.jpg")
            .Select(p => new object[] { p });

    public static IEnumerable<object[]> TamperedImages()
        => Directory.GetFiles(Path.Combine(DataDir, "tampered"), "*.jpg")
            .Select(p => new object[] { p });

    [Theory]
    [MemberData(nameof(AuthenticImages))]
    public async Task Authentic_Images_AreProcessed(string path)
    {
        var analyzer = new ForensicsAnalyzer();
        var options = new ForensicsOptions
        {
            WorkDir = Path.GetTempPath(),
            NoiseprintModelsDir = AppContext.BaseDirectory
        };
        var res = await analyzer.AnalyzeAsync(path, options);
        res.ElaScore.Should().BeGreaterThan(0);
    }

    [Theory]
    [MemberData(nameof(TamperedImages))]
    public async Task Tampered_Images_AreProcessed(string path)
    {
        var analyzer = new ForensicsAnalyzer();
        var options = new ForensicsOptions
        {
            WorkDir = Path.GetTempPath(),
            NoiseprintModelsDir = AppContext.BaseDirectory
        };
        var res = await analyzer.AnalyzeAsync(path, options);
        res.ElaScore.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Measure_Processing_Times()
    {
        var analyzer = new ForensicsAnalyzer();
        var options = new ForensicsOptions
        {
            WorkDir = Path.GetTempPath(),
            NoiseprintModelsDir = AppContext.BaseDirectory
        };

        double authTotal = 0;
        var authPaths = Directory.GetFiles(Path.Combine(DataDir, "authentic"), "*.jpg");
        foreach (var path in authPaths)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await analyzer.AnalyzeAsync(path, options);
            sw.Stop();
            authTotal += sw.Elapsed.TotalMilliseconds;
        }

        double tampTotal = 0;
        var tampPaths = Directory.GetFiles(Path.Combine(DataDir, "tampered"), "*.jpg");
        foreach (var path in tampPaths)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await analyzer.AnalyzeAsync(path, options);
            sw.Stop();
            tampTotal += sw.Elapsed.TotalMilliseconds;
        }

        double authAvg = authTotal / authPaths.Length;
        double tampAvg = tampTotal / tampPaths.Length;

        Console.WriteLine($"Average authentic: {authAvg:F0} ms");
        Console.WriteLine($"Average tampered: {tampAvg:F0} ms");
    }
}
