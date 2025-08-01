using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using OpenCvSharp;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Diagnostics;
using Serilog;

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
        private static readonly ConcurrentDictionary<string, InferenceSession> SessionCache = new();

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
            Log.Information("Splicing analysis for {Image}", imagePath);

            // 1) Load & preprocess
            Mat img = Cv2.ImRead(imagePath, ImreadModes.Color);
            int origW = img.Cols;
            int origH = img.Rows;
            Cv2.Resize(img, img, new Size(inputW, inputH));
            img.ConvertTo(img, MatType.CV_32FC3, 1 / 255.0);
            Cv2.CvtColor(img, img, ColorConversionCodes.BGR2RGB);

            // 2) Tensor NHWC
            var data = new float[inputW * inputH * 3];
            Marshal.Copy(img.Data, data, 0, data.Length);
            var tensor = new DenseTensor<float>(data, new[] { 1, inputH, inputW, 3 });

            // 3) Inference
            var session = SessionCache.GetOrAdd(modelPath, p => new InferenceSession(p));
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("img_in", tensor)
            };
            using var results = session.Run(inputs);
            var output = results.First().AsTensor<float>().ToArray();

            // 4) Post‑process
            Mat heat = new Mat(inputH, inputW, MatType.CV_32FC1);
            heat.SetArray(output);
            Cv2.Resize(heat, heat, new Size(origW, origH), 0, 0, InterpolationFlags.Linear);
            Cv2.Normalize(heat, heat, 0, 255, NormTypes.MinMax);
            heat.ConvertTo(heat, MatType.CV_8UC1);

            string baseName = Path.GetFileNameWithoutExtension(imagePath);
            string outPath  = Path.Combine(mapDir, $"{baseName}_splicing.png");
            Cv2.ImWrite(outPath, heat);

            sw.Stop();
            double score = Cv2.Mean(heat)[0] / 255.0;
            Log.Information("Splicing completed for {Image} in {Elapsed} ms: {Score}", imagePath, sw.ElapsedMilliseconds, score);
            return (score, outPath);
        }
    }
}
