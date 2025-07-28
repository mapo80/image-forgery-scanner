using System;
using System.IO;
using FluentAssertions;
using ImageForensics.Core.Algorithms;
using ImageMagick;
using Xunit;

namespace ImageForensics.Tests;

public class JpegQualityEstimatorTests
{
    [Theory]
    [InlineData(75)]
    [InlineData(90)]
    public void EstimateQuality_ReturnsCloseValue(int quality)
    {
        using var img = new MagickImage(MagickColors.White, 32, 32);
        img.Quality = quality;
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".jpg");
        img.Write(path, MagickFormat.Jpeg);

        var est = new JpegQualityEstimator();
        int? qf = est.EstimateQuality(path);

        qf.Should().NotBeNull();
        Math.Abs(qf!.Value - quality).Should().BeLessThanOrEqualTo(5);
    }

    [Fact]
    public void EstimateQuality_NonJpeg_ReturnsNull()
    {
        using var img = new MagickImage(MagickColors.White, 32, 32);
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".png");
        img.Write(path, MagickFormat.Png);

        var est = new JpegQualityEstimator();
        est.EstimateQuality(path).Should().BeNull();
    }
}
