namespace ImageForensics.Core.Models;

public record ForensicsOptions
{
    public int ElaQuality { get; init; } = 75;
    public string WorkDir { get; init; } = "results";
}
