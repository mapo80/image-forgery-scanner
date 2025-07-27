using System;
using System.IO;
using System.Runtime.InteropServices;
using OpenCvSharp;
using Xunit;

namespace TestOpenCvSharp
{
    public class CvVersionTests
    {
        [Fact]
        public void GetVersionString_ReturnsNonEmpty()
        {
            var baseDir = AppContext.BaseDirectory;
            var libPath = Path.Combine(baseDir, "libOpenCvSharpExtern.so");
            if (File.Exists(libPath))
            {
                NativeLibrary.Load(libPath);
            }

            var version = Cv2.GetVersionString();
            Assert.False(string.IsNullOrWhiteSpace(version));
        }
    }
}
