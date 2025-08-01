using ImageForensics.Core;
using ImageForensics.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using Xunit;
using System.IO;
using System.Threading.Tasks;

namespace ImageForensic.Api.Tests;

public class AnalyzerEndpointsTests
{
    [Fact]
    public async Task AnalyzeImage_ReturnsResultFromAnalyzer()
    {
        var elaPath = Path.GetTempFileName();
        var maskPath = Path.GetTempFileName();
        await File.WriteAllBytesAsync(elaPath, new byte[] { 9 });
        await File.WriteAllBytesAsync(maskPath, new byte[] { 8 });
        var expected = new ForensicsResult(0.1, elaPath, 0.2, maskPath)
        { Verdict = "ok" };
        var mock = new Mock<IForensicsAnalyzer>();
        var options = new AnalyzeImageOptions();
        mock.Setup(a => a.AnalyzeAsync(It.IsAny<string>(), It.IsAny<ForensicsOptions>()))
            .ReturnsAsync(expected);

        await using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var file = new FormFile(stream, 0, stream.Length, "image", "img.jpg");
        var result = await AnalyzerEndpoints.AnalyzeImage(file, options, mock.Object);
        var ok = Assert.IsType<Ok<AnalyzeImageResult>>(result);
        var actual = ok.Value!;
        Assert.Equal(expected.ElaScore, actual.ElaScore);
        Assert.Equal(expected.CopyMoveScore, actual.CopyMoveScore);
        Assert.Equal(expected.Verdict, actual.Verdict);
        Assert.NotEmpty(actual.ElaMap);
        Assert.NotEmpty(actual.CopyMoveMask);
        mock.Verify(a => a.AnalyzeAsync(It.IsAny<string>(), It.IsAny<ForensicsOptions>()), Times.Once);
    }
}
