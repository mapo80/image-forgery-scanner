using System.Linq;
using System.Net.Http.Json;
using ImageForensic.Api.Models;
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
        var fakeResult = new ForensicsResult(0.1, "ela", "ok", 0.2, "mask");
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.Single(d => d.ServiceType == typeof(IForensicsAnalyzer));
                services.Remove(descriptor);
                services.AddSingleton<IForensicsAnalyzer>(new FakeAnalyzer(fakeResult));
            });
        });
        var client = factory.CreateClient();
        var request = new AnalyzeImageRequest("img.jpg", new ForensicsOptions());
        var response = await client.PostAsJsonAsync("/analyze", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ForensicsResult>();
        Assert.NotNull(result);
        Assert.Equal(fakeResult.ElaScore, result!.ElaScore);
        Assert.Equal(fakeResult.CopyMoveScore, result.CopyMoveScore);
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
