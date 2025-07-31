using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ImageForensics.Core;
using ImageForensics.Core.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ImageForensic.Api.Tests;

public class DatasetImageTests
{
    private static string GetDatasetImagePath() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "dataset", "lena.jpg"));

    [Fact]
    public async Task Analyzer_ReturnsElaMap_ForDatasetImage()
    {
        var analyzer = new ForensicsAnalyzer();
        var workDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var options = new ForensicsOptions
        {
            EnabledChecks = ForensicsCheck.Ela,
            WorkDir = workDir,
            CopyMoveMaskDir = workDir,
            SplicingMapDir = workDir,
            NoiseprintMapDir = workDir,
            MetadataMapDir = workDir
        };
        Directory.CreateDirectory(workDir);
        try
        {
            var result = await analyzer.AnalyzeAsync(GetDatasetImagePath(), options);
            Assert.NotNull(result);
            Assert.NotEmpty(result.ElaMap);
        }
        finally
        {
            try { Directory.Delete(workDir, true); } catch { }
        }
    }

    [Fact]
    public async Task ApiAnalyze_ReturnsElaMap_ForDatasetImage()
    {
        var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        using var content = new MultipartFormDataContent();
        var bytes = await File.ReadAllBytesAsync(GetDatasetImagePath());
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
        content.Add(fileContent, "image", "lena.jpg");
        content.Add(new StringContent("Ela"), "EnabledChecks");

        var response = await client.PostAsync("/analyze", content);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ForensicsResult>();
        Assert.NotNull(result);
        Assert.NotEmpty(result!.ElaMap);
    }
}

