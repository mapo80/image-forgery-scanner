using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace ImageForensics.Core.Algorithms;

/// <summary>
/// Utility to run NoisePrint inpainting detection using the provided ONNX model.
/// </summary>
public static class NoiseprintSdkWrapper
{
    private static bool _loaded;

    public static (double Score, string MapPath) Run(
        string imagePath,
        string mapDir,
        string modelPath,
        int inputSize)
    {
        if (!_loaded)
        {
            NoisePrintSdk.LoadModel(modelPath);
            _loaded = true;
        }

        Directory.CreateDirectory(mapDir);
        string baseName = Path.GetFileNameWithoutExtension(imagePath);
        string outPath = Path.Combine(mapDir, $"{baseName}_inpainting.png");

        var sw = Stopwatch.StartNew();

        using Mat bgr = Cv2.ImRead(imagePath, ImreadModes.Color);
        using Mat resized = new();
        Cv2.Resize(bgr, resized, new Size(inputSize, inputSize));
        resized.ConvertTo(resized, MatType.CV_32FC3, 1.0 / 255.0);
        Cv2.CvtColor(resized, resized, ColorConversionCodes.BGR2RGB);

        var tensor = new DenseTensor<float>(new[] { 1, 3, inputSize, inputSize });
        float[] data = new float[inputSize * inputSize * 3];
        Marshal.Copy(resized.Data, data, 0, data.Length);
        data.AsSpan().CopyTo(tensor.Buffer.Span);

        float[] output = NoisePrintSdk.RunInference(tensor);
        int side = (int)Math.Sqrt(output.Length);
        using Mat heat = new(side, side, MatType.CV_32FC1);
        heat.SetArray(output);
        Cv2.Resize(heat, heat, new Size(bgr.Width, bgr.Height), 0, 0, InterpolationFlags.Linear);
        Cv2.Normalize(heat, heat, 0, 255, NormTypes.MinMax);
        heat.ConvertTo(heat, MatType.CV_8UC1);
        Cv2.ImWrite(outPath, heat);

        sw.Stop();
        Console.WriteLine($"Noiseprint detection time for {baseName}: {sw.ElapsedMilliseconds} ms");

        double score = Cv2.Mean(heat)[0] / 255.0;
        return (score, outPath);
    }
}
