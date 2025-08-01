using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using Serilog;

namespace ImageForensics.Core.Algorithms;

/// <summary>
/// Utility to run NoisePrint inpainting detection using the provided ONNX model.
/// </summary>
public static class NoiseprintSdkWrapper
{
    private static int _lastQf = -1;

    /// <summary>Quality factor of the last model used.</summary>
    public static int LoadedQf => _lastQf;

    public static (double Score, string MapPath) Run(
        string imagePath,
        string mapDir,
        string modelsDir,
        int inputSize)
    {
        Log.Information("Inpainting analysis for {Image}", imagePath);
        var estimator = new JpegQualityEstimator();
        int qf = estimator.EstimateQuality(imagePath) ?? 101;
        string modelPath = Path.Combine(modelsDir, $"model_qf{qf}.onnx");
        if (!File.Exists(modelPath))
        {
            modelPath = Path.Combine(modelsDir, "model_qf101.onnx");
            qf = 101;
        }

        // Cache delle sessioni ONNX gestita da NoisePrintSdk
        _lastQf = qf;

        Directory.CreateDirectory(mapDir);
        string baseName = Path.GetFileNameWithoutExtension(imagePath);
        string outPath = Path.Combine(mapDir, $"{baseName}_inpainting.png");

        var sw = Stopwatch.StartNew();

        using Mat gray = Cv2.ImRead(imagePath, ImreadModes.Grayscale);
        using Mat resized = new();
        Cv2.Resize(gray, resized, new Size(inputSize, inputSize));
        resized.ConvertTo(resized, MatType.CV_32FC1, 1.0 / 255.0);

        var tensor = new DenseTensor<float>(new[] { 1, 1, inputSize, inputSize });
        float[] data = new float[inputSize * inputSize];
        Marshal.Copy(resized.Data, data, 0, data.Length);
        data.AsSpan().CopyTo(tensor.Buffer.Span);

        float[] output = NoisePrintSdk.RunInference(modelPath, tensor);
        int side = (int)Math.Sqrt(output.Length);
        using Mat heat = new(side, side, MatType.CV_32FC1);
        heat.SetArray(output);
        Cv2.Resize(heat, heat, new Size(gray.Width, gray.Height), 0, 0, InterpolationFlags.Linear);
        Cv2.Normalize(heat, heat, 0, 255, NormTypes.MinMax);
        heat.ConvertTo(heat, MatType.CV_8UC1);
        Cv2.ImWrite(outPath, heat);

        sw.Stop();
        double score = Cv2.Mean(heat)[0] / 255.0;
        Log.Information("Inpainting completed for {Image} in {Elapsed} ms: {Score}", imagePath, sw.ElapsedMilliseconds, score);
        return (score, outPath);
    }
}
