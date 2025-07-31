using ImageForensics.Core.Models;

namespace ImageForensic.Api.Models;

public record AnalyzeImageRequest(string ImagePath, ForensicsOptions Options);
