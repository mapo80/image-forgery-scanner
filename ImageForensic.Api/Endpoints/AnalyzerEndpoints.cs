using System;
using System.IO;
using ImageForensics.Core;
using ImageForensics.Core.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace ImageForensic.Api;

public static class AnalyzerEndpoints
{
    public static void MapAnalyzerEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/analyze", AnalyzeImage)
              .WithName("AnalyzeImage")
              .DisableAntiforgery();
    }

    internal static async Task<IResult> AnalyzeImage(IFormFile? image, [FromForm] AnalyzeImageOptions? options, IForensicsAnalyzer analyzer)
    {
        if (image is null)
            return Results.BadRequest("Missing image");

        options ??= new AnalyzeImageOptions();

        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{Path.GetExtension(image.FileName)}");
        await using (var stream = File.Create(tempFile))
        {
            await image.CopyToAsync(stream);
        }

        var workDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(workDir);

        var opts = options.ToForensicsOptions() with
        {
            WorkDir = workDir,
            CopyMoveMaskDir = workDir,
            SplicingMapDir = workDir,
            NoiseprintMapDir = workDir,
            MetadataMapDir = workDir,
            SplicingModelPath = "Models/onnx/mantranet_256x256.onnx",
            NoiseprintModelsDir = "Models/onnx/noiseprint",
            SplicingInputWidth = 256,
            SplicingInputHeight = 256,
            NoiseprintInputSize = 320
        };

        try
        {
            var result = await analyzer.AnalyzeAsync(tempFile, opts);
            return Results.Ok(AnalyzeImageResult.From(result));
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
            try { Directory.Delete(workDir, true); } catch { }
        }
    }
}
