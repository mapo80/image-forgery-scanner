using System;
using System.Collections.Generic;
using System.IO;
using ImageForensics.Core.Models;

namespace ImageForensic.Api;

public record AnalyzeImageResult(
    double ElaScore,
    byte[] ElaMap,
    double CopyMoveScore,
    byte[] CopyMoveMask)
{
    public double SplicingScore { get; init; }
    public byte[] SplicingMap { get; init; } = Array.Empty<byte>();

    public double InpaintingScore { get; init; }
    public byte[] InpaintingMap { get; init; } = Array.Empty<byte>();

    public double ExifScore { get; init; }
    public IReadOnlyDictionary<string, string?> ExifAnomalies { get; init; } = new Dictionary<string, string?>();

    public IReadOnlyDictionary<string, string> Errors { get; init; } = new Dictionary<string, string>();

    public double TotalScore { get; init; }
    public string Verdict { get; init; } = string.Empty;

    public static AnalyzeImageResult From(ForensicsResult result)
    {
        byte[] ReadBytes(string p) => File.Exists(p) ? File.ReadAllBytes(p) : Array.Empty<byte>();

        return new AnalyzeImageResult(
            result.ElaScore,
            ReadBytes(result.ElaMapPath),
            result.CopyMoveScore,
            ReadBytes(result.CopyMoveMaskPath))
        {
            SplicingScore = result.SplicingScore,
            SplicingMap = ReadBytes(result.SplicingMapPath),
            InpaintingScore = result.InpaintingScore,
            InpaintingMap = ReadBytes(result.InpaintingMapPath),
            ExifScore = result.ExifScore,
            ExifAnomalies = result.ExifAnomalies,
            Errors = result.Errors,
            TotalScore = result.TotalScore,
            Verdict = result.Verdict
        };
    }
}

