using System;
using System.IO;
using System.Runtime.InteropServices;
using OpenCvSharp;
using Xunit;

namespace TestOpenCvSharp
{
    public class ImageTransformTests
    {
        [Fact]
        public void ConvertImageToGray_Succeeds()
        {
            var baseDir = AppContext.BaseDirectory;
            var libPath = Path.Combine(baseDir, "libOpenCvSharpExtern.so");
            if (File.Exists(libPath))
            {
                NativeLibrary.Load(libPath);
            }

            var imgPath = Path.Combine(baseDir, "dataset", "lena.jpg");
            Assert.True(File.Exists(imgPath), $"Image not found at {imgPath}");

            using var src = Cv2.ImRead(imgPath);
            using var dst = new Mat();
            Cv2.CvtColor(src, dst, ColorConversionCodes.BGR2GRAY);
            Assert.False(dst.Empty());
            Assert.Equal(src.Height, dst.Height);
            Assert.Equal(src.Width, dst.Width);
        }
    }
}
