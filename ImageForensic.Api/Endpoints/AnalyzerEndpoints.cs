using ImageForensic.Api.Models;
using ImageForensics.Core;
using ImageForensics.Core.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ImageForensic.Api;

public static class AnalyzerEndpoints
{
    public static void MapAnalyzerEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/analyze", AnalyzeImage)
              .WithName("AnalyzeImage");
    }

    internal static async Task<IResult> AnalyzeImage(AnalyzeImageRequest request, IForensicsAnalyzer analyzer)
    {
        var result = await analyzer.AnalyzeAsync(request.ImagePath, request.Options);
        return Results.Ok(result);
    }
}
