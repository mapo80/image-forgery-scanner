using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using OpenCvSharp;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Diagnostics;

namespace ImageForensics.Core.Algorithms
{
    /// <summary>
    /// Inferisce il modello ManTraNet per rilevare splicing/local tampering.
    /// </summary>
    /// <param name="imagePath">percorso file immagine input</param>
    /// <param name="mapDir">cartella output heat‑map</param>
    /// <param name="modelPath">percorso file ONNX (non committato)</param>
    /// <param name="inputW">larghezza di ridimensionamento</param>
    /// <param name="inputH">altezza di ridimensionamento</param>
    /// <returns>
    /// (double Score – punteggio normalizzato [0–1], 
    ///  string MapPath – percorso heat‑map PNG)
    /// </returns>
    public static class DlSplicingDetector
    {
        public static (double Score, string MapPath) AnalyzeSplicing(
            string imagePath,
            string mapDir,
            string modelPath,
            int inputW,
            int inputH)
        {
            if (!Directory.Exists(mapDir))
                Directory.CreateDirectory(mapDir);

            var sw = Stopwatch.StartNew();

            // 1) Load & preprocess
            Mat bgr = Cv2.ImRead(imagePath, ImreadModes.Color);
            Mat resized = new Mat();
            Cv2.Resize(bgr, resized, new Size(inputW, inputH));
            resized.ConvertTo(resized, MatType.CV_32FC3, 1/255.0);
            Cv2.CvtColor(resized, resized, ColorConversionCodes.BGR2RGB);

            // 2) Tensor NCHW
            var tensor = new DenseTensor<float>(new[] {1, inputH, inputW, 3});
            var data = new float[inputW * inputH * 3];
            Marshal.Copy(resized.Data, data, 0, data.Length);
            data.AsSpan().CopyTo(tensor.Buffer.Span);

            // 3) Inference
            using var session = new InferenceSession(modelPath);
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("img_in", tensor)
            };
            using var results = session.Run(inputs);
            var output = results.First().AsTensor<float>().ToArray();

            // 4) Post‑process
            Mat heat = new Mat(inputH, inputW, MatType.CV_32FC1);
            heat.SetArray(output);
            Cv2.Resize(heat, heat, new Size(bgr.Cols, bgr.Rows), 0, 0, InterpolationFlags.Linear);
            Cv2.Normalize(heat, heat, 0, 255, NormTypes.MinMax);
            heat.ConvertTo(heat, MatType.CV_8UC1);

            string baseName = Path.GetFileNameWithoutExtension(imagePath);
            string outPath  = Path.Combine(mapDir, $"{baseName}_splicing.png");
            Cv2.ImWrite(outPath, heat);

            sw.Stop();
            Console.WriteLine($"Splicing detection time for {baseName}: {sw.ElapsedMilliseconds} ms");

            double score = Cv2.Mean(heat)[0] / 255.0;
            return (score, outPath);
        }
    }
}
