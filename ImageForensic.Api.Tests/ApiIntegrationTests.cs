using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IO;
using ImageForensic.Api;
using ImageForensics.Core;
using ImageForensics.Core.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using System.Threading.Tasks;

namespace ImageForensic.Api.Tests;

public class ApiIntegrationTests
{
    [Fact]
    public async Task PostAnalyze_ReturnsOkWithResult()
    {
        var elaPath = Path.GetTempFileName();
        var maskPath = Path.GetTempFileName();
        await File.WriteAllBytesAsync(elaPath, new byte[] { 7 });
        await File.WriteAllBytesAsync(maskPath, new byte[] { 6 });
        var fakeResult = new ForensicsResult(0.1, elaPath, 0.2, maskPath)
        { Verdict = "ok" };
        var factory = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.Single(d => d.ServiceType == typeof(IForensicsAnalyzer));
                services.Remove(descriptor);
                services.AddSingleton<IForensicsAnalyzer>(new FakeAnalyzer(fakeResult));
            });
        });
        var client = factory.CreateClient();
        using var content = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(new byte[] { 1, 2, 3 });
        imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
        content.Add(imageContent, "image", "img.jpg");
        var response = await client.PostAsync("/analyze", content);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ImageForensic.Api.AnalyzeImageResult>();
        Assert.NotNull(result);
        Assert.NotEmpty(result!.ElaMap);
        Assert.NotEmpty(result.CopyMoveMask);
        Assert.Equal(fakeResult.Verdict, result.Verdict);
    }

    private class FakeAnalyzer : IForensicsAnalyzer
    {
        private readonly ForensicsResult _result;
        public FakeAnalyzer(ForensicsResult result) => _result = result;
        public Task<ForensicsResult> AnalyzeAsync(string imagePath, ForensicsOptions options)
            => Task.FromResult(_result);
    }
}
