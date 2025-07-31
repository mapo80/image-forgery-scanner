using ImageForensic.Api.Models;
using ImageForensics.Core;
using ImageForensics.Core.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using Xunit;
using System.Threading.Tasks;

namespace ImageForensic.Api.Tests;

public class AnalyzerEndpointsTests
{
    [Fact]
    public async Task AnalyzeImage_ReturnsResultFromAnalyzer()
    {
        var expected = new ForensicsResult(0.1, "ela", "ok", 0.2, "mask");
        var mock = new Mock<IForensicsAnalyzer>();
        var options = new ForensicsOptions();
        mock.Setup(a => a.AnalyzeAsync("img.jpg", options)).ReturnsAsync(expected);

        var request = new AnalyzeImageRequest("img.jpg", options);
        var result = await AnalyzerEndpoints.AnalyzeImage(request, mock.Object);
        var ok = Assert.IsType<Ok<ForensicsResult>>(result);
        Assert.Equal(expected, ok.Value);
        mock.Verify(a => a.AnalyzeAsync("img.jpg", options), Times.Once);
    }
}
