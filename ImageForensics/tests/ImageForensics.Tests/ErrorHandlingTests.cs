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
        var options = new ForensicsOptions
        {
            WorkDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
            EnabledChecks = ForensicsCheck.Ela | ForensicsCheck.Exif
        };
        var result = await analyzer.AnalyzeAsync("missing.jpg", options);
        Assert.Contains("ELA", result.Errors.Keys);
        Assert.Contains("EXIF", result.Errors.Keys);
    }
}
