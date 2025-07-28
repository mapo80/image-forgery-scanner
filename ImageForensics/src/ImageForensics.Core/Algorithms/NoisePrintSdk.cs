using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Collections.Generic;
using System.Linq;

namespace ImageForensics.Core.Algorithms;

/// <summary>
/// Minimal wrapper around ONNX Runtime for the NoisePrint models.
/// </summary>
public static class NoisePrintSdk
{
    private static InferenceSession? _session;

    /// <summary>Load the ONNX model from disk.</summary>
    public static void LoadModel(string modelPath)
    {
        _session?.Dispose();
        _session = new InferenceSession(modelPath);
    }

    /// <summary>Run inference using the previously loaded model.</summary>
    public static float[] RunInference(DenseTensor<float> tensor)
    {
        if (_session == null)
            throw new InvalidOperationException("Model not loaded");

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input", tensor)
        };

        using var results = _session.Run(inputs);
        return results.First().AsTensor<float>().ToArray();
    }
}
