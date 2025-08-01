using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace ImageForensics.Core.Algorithms;

/// <summary>
/// Minimal wrapper around ONNX Runtime for the NoisePrint models.
/// </summary>
public static class NoisePrintSdk
{
    private static readonly ConcurrentDictionary<string, InferenceSession> Sessions = new();

    /// <summary>Run inference using the ONNX model at <paramref name="modelPath"/>.</summary>
    public static float[] RunInference(string modelPath, DenseTensor<float> tensor)
    {
        var session = Sessions.GetOrAdd(modelPath, p => new InferenceSession(p));

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input", tensor)
        };

        using var results = session.Run(inputs);
        return results.First().AsTensor<float>().ToArray();
    }
}
