using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ImageForensics.Core.Algorithms;
using ImageForensics.Core.Models;
using Serilog;

namespace ImageForensics.Core;

public interface IForensicsAnalyzer
{
    Task<ForensicsResult> AnalyzeAsync(string imagePath, ForensicsOptions options);
}

public class ForensicsAnalyzer : IForensicsAnalyzer
{
    static ForensicsAnalyzer()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();
    }

    public async Task<ForensicsResult> AnalyzeAsync(string imagePath, ForensicsOptions options)
    {
        Log.Information("Starting analysis of {Image}", imagePath);
        Directory.CreateDirectory(options.WorkDir);

        var semaphore = new SemaphoreSlim(Math.Max(1, options.MaxParallelChecks));
        Task<T> RunAsync<T>(Func<T> func) => Task.Run(() =>
        {
            semaphore.Wait();
            try { return func(); }
            finally { semaphore.Release(); }
        });

        Task<(double, string)> elaTask = Task.FromResult((0d, string.Empty));
        if (options.EnabledChecks.HasFlag(ForensicsCheck.Ela))
        {
            Log.Information("Running ELA check");
            elaTask = RunAsync(() => ElaAnalyzer.Analyze(imagePath, options.WorkDir, options.ElaQuality));
        }

        Task<(double, string)> cmTask = Task.FromResult((0d, string.Empty));
        if (options.EnabledChecks.HasFlag(ForensicsCheck.CopyMove))
        {
            Directory.CreateDirectory(options.CopyMoveMaskDir);
            Log.Information("Running Copy-Move check");
            cmTask = RunAsync(() => CopyMoveDetector.Analyze(
                imagePath,
                options.CopyMoveMaskDir,
                options.CopyMoveFeatureCount,
                options.CopyMoveMatchDistance,
                options.CopyMoveRansacReproj,
                options.CopyMoveRansacProb));
        }

        Task<(double, string)> spTask = Task.FromResult((0d, string.Empty));
        if (options.EnabledChecks.HasFlag(ForensicsCheck.Splicing))
        {
            Directory.CreateDirectory(options.SplicingMapDir);
            Log.Information("Running Splicing check");
            spTask = RunAsync(() =>
            {
                string modelPath = options.SplicingModelPath;
                if (!Path.IsPathRooted(modelPath))
                {
                    if (!File.Exists(modelPath))
                        modelPath = Path.Combine(AppContext.BaseDirectory, modelPath);
                    if (!File.Exists(modelPath))
                        modelPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                            "..", "..", "..", "..", "..", "..", modelPath));
                }

                return DlSplicingDetector.AnalyzeSplicing(
                    imagePath,
                    options.SplicingMapDir,
                    modelPath,
                    options.SplicingInputWidth,
                    options.SplicingInputHeight);
            });
        }

        Task<(double, string)> ipTask = Task.FromResult((0d, string.Empty));
        if (options.EnabledChecks.HasFlag(ForensicsCheck.Inpainting))
        {
            Directory.CreateDirectory(options.NoiseprintMapDir);
            Log.Information("Running Inpainting check");
            ipTask = RunAsync(() =>
            {
                string modelsDir = options.NoiseprintModelsDir;
                if (!Path.IsPathRooted(modelsDir))
                {
                    modelsDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                        "..", "..", "..", "..", "..", "..", modelsDir));
                }
                if (!Directory.Exists(modelsDir))
                    throw new DirectoryNotFoundException($"Noiseprint models not found in '{modelsDir}'");

                return NoiseprintSdkWrapper.Run(
                    imagePath,
                    options.NoiseprintMapDir,
                    modelsDir,
                    options.NoiseprintInputSize);
            });
        }

        Task<(double, IReadOnlyDictionary<string, string?>)> exifTask =
            Task.FromResult<(double, IReadOnlyDictionary<string, string?>)>(
                (0d, new Dictionary<string, string?>()));
        if (options.EnabledChecks.HasFlag(ForensicsCheck.Exif))
        {
            Directory.CreateDirectory(options.MetadataMapDir);
            Log.Information("Running EXIF check");
            exifTask = RunAsync(() => ExifChecker.Analyze(
                imagePath,
                options.MetadataMapDir,
                options.ExpectedCameraModels));
        }

        await Task.WhenAll(elaTask, cmTask, spTask, ipTask, exifTask);

        var (elaScore, elaMapPath) = await elaTask;
        Log.Information("ELA score {Score} map {Map}", elaScore, elaMapPath);
        var (cmScore, cmMaskPath) = await cmTask;
        Log.Information("Copy-Move score {Score} mask {Mask}", cmScore, cmMaskPath);
        var (spScore, spMapPath) = await spTask;
        Log.Information("Splicing score {Score} map {Map}", spScore, spMapPath);
        var (ipScore, ipMapPath) = await ipTask;
        Log.Information("Inpainting score {Score} map {Map}", ipScore, ipMapPath);
        var (exifScore, exifAnomalies) = await exifTask;
        Log.Information("EXIF score {Score} anomalies {Count}", exifScore, exifAnomalies.Count);

        var result = new ForensicsResult(
            elaScore,
            elaMapPath,
            cmScore,
            cmMaskPath)
        {
            SplicingScore = spScore,
            SplicingMapPath = spMapPath,
            InpaintingScore = ipScore,
            InpaintingMapPath = ipMapPath,
            ExifScore = exifScore,
            ExifAnomalies = exifAnomalies
        };

        var effectiveOptions = options with
        {
            ElaWeight = options.EnabledChecks.HasFlag(ForensicsCheck.Ela) ? options.ElaWeight : 0,
            CopyMoveWeight = options.EnabledChecks.HasFlag(ForensicsCheck.CopyMove) ? options.CopyMoveWeight : 0,
            SplicingWeight = options.EnabledChecks.HasFlag(ForensicsCheck.Splicing) ? options.SplicingWeight : 0,
            InpaintingWeight = options.EnabledChecks.HasFlag(ForensicsCheck.Inpainting) ? options.InpaintingWeight : 0,
            ExifWeight = options.EnabledChecks.HasFlag(ForensicsCheck.Exif) ? options.ExifWeight : 0
        };

        var (total, verdict) = DecisionEngine.Decide(result, effectiveOptions);
        result = result with { Verdict = verdict, TotalScore = total };

        Log.Information("Analysis completed. Verdict {Verdict} score {Score}", result.Verdict, result.TotalScore);

        return result;
    }
}

