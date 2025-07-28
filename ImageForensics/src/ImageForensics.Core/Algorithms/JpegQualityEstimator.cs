using System;
using System.Collections.Generic;
using System.IO;
using MetadataExtractor;
using MetadataExtractor.Formats.Jpeg;
using MetadataExtractor.IO;

namespace ImageForensics.Core.Algorithms;

/// <summary>
/// Utility class for estimating the JPEG Quality Factor from quantization tables
/// using <see cref="MetadataExtractor"/>.
/// </summary>
public class JpegQualityEstimator
{
    /// <summary>
    /// Reads the quantization tables of a JPEG image and estimates the Quality Factor (1-100).
    /// Returns null for non-JPEG files. Note: qf101 is reserved for PNG images.
    /// </summary>
    public int? EstimateQuality(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext != ".jpg" && ext != ".jpeg")
            return null;

        var segments = JpegSegmentReader.ReadSegments(path, new[] { JpegSegmentType.Dqt });
        if (segments.Count == 0)
            return null;

        var values = new List<int>();
        foreach (var seg in segments)
        {
            var reader = new SequentialByteArrayReader(seg.Bytes);
            while (reader.Available() > 0)
            {
                byte pqTq = reader.GetByte();
                int precision = pqTq >> 4;
                for (int i = 0; i < 64; i++)
                {
                    int val = precision == 0 ? reader.GetByte() : reader.GetUInt16();
                    values.Add(val);
                }
            }
        }

        if (values.Count < 64)
            return null;

        int[] baseTable = new int[]
        {
            16,11,10,16,24,40,51,61,12,12,14,19,26,58,60,55,
            14,13,16,24,40,57,69,56,14,17,22,29,51,87,80,62,
            18,22,37,56,68,109,103,77,24,35,55,64,81,104,113,92,
            49,64,78,87,103,121,120,101,72,92,95,98,112,100,103,99
        };

        double scaleSum = 0;
        for (int i = 0; i < 64; i++)
            scaleSum += (values[i] * 100.0 - 50.0) / baseTable[i];

        double scale = scaleSum / 64.0;
        int q = scale <= 100.0 ? (int)Math.Round((200.0 - scale) / 2.0) : (int)Math.Round(5000.0 / scale);
        return Math.Clamp(q, 1, 100);
    }
}
