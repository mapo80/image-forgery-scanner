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
        var expected = new ForensicsResult(0.1, new byte[] { 9 }, "ela.png", 0.2, new byte[] { 8 }, "mask.png")
        { Verdict = "ok" };
        var mock = new Mock<IForensicsAnalyzer>();
        var options = new ForensicsOptions();
        mock.Setup(a => a.AnalyzeAsync(It.IsAny<string>(), It.IsAny<ForensicsOptions>()))
            .ReturnsAsync(expected);

        await using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var file = new FormFile(stream, 0, stream.Length, "image", "img.jpg");
        var result = await AnalyzerEndpoints.AnalyzeImage(file, options, mock.Object);
        var ok = Assert.IsType<Ok<ForensicsResult>>(result);
        Assert.Equal(expected, ok.Value);
        Assert.NotEmpty(ok.Value!.ElaMap);
        Assert.NotEmpty(ok.Value.CopyMoveMask);
        mock.Verify(a => a.AnalyzeAsync(It.IsAny<string>(), It.IsAny<ForensicsOptions>()), Times.Once);
    }
}
