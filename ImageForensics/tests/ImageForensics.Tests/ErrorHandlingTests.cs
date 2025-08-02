using System;
using System.IO;
using System.Threading.Tasks;
using ImageForensics.Core;
using ImageForensics.Core.Models;
using Xunit;

public class ErrorHandlingTests
{
    [Fact]
    public async Task AnalyzerCollectsErrorsPerCheck()
    {
        var analyzer = new ForensicsAnalyzer();
        string workDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var options = new ForensicsOptions
        {
            WorkDir = workDir,
            SplicingMapDir = workDir,
            SplicingModelPath = typeof(ErrorHandlingTests).Assembly.Location,
            EnabledChecks = ForensicsCheck.Ela | ForensicsCheck.Splicing | ForensicsCheck.Exif
        };
        var result = await analyzer.AnalyzeAsync("missing.jpg", options);
        Assert.Contains("ELA", result.Errors.Keys);
        Assert.Contains("Splicing", result.Errors.Keys);
        Assert.Contains("EXIF", result.Errors.Keys);
    }

    [Fact]
    public async Task InpaintingFailureIsReported()
    {
        var analyzer = new ForensicsAnalyzer();
        string workDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(workDir);
        var options = new ForensicsOptions
        {
            WorkDir = workDir,
            NoiseprintMapDir = workDir,
            NoiseprintModelsDir = Path.Combine(workDir, "missingModels"),
            EnabledChecks = ForensicsCheck.Inpainting
        };

        string img = Path.Combine(AppContext.BaseDirectory, "testdata", "clean.png");
        var result = await analyzer.AnalyzeAsync(img, options);

        Assert.Contains("Inpainting", result.Errors.Keys);
    }
}
