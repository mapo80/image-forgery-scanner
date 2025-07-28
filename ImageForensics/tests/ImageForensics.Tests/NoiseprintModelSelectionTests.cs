using System;
using System.IO;
using FluentAssertions;
using ImageForensics.Core.Algorithms;
using ImageMagick;
using Xunit;

namespace ImageForensics.Tests;

public class NoiseprintModelSelectionTests
{
    private static string ModelsDir => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../ImageForensics/src/Models/onnx/noiseprint"));

    [Fact]
    public void Run_Png_LoadsDefaultModel()
    {
        string img = Path.Combine(AppContext.BaseDirectory, "testdata", "clean.png");
        string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);

        NoiseprintSdkWrapper.Run(img, dir, ModelsDir, 32);
        NoiseprintSdkWrapper.LoadedQf.Should().Be(101);
    }

    [Fact]
    public void Run_Jpeg_LoadsQualitySpecificModel()
    {
        using var m = new MagickImage(MagickColors.White, 32, 32);
        m.Quality = 90;
        string imgPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".jpg");
        m.Write(imgPath, MagickFormat.Jpeg);
        string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);

        NoiseprintSdkWrapper.Run(imgPath, dir, ModelsDir, 32);
        NoiseprintSdkWrapper.LoadedQf.Should().Be(89);
    }
}
