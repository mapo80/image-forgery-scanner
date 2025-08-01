using System;
using System.Collections.Concurrent;
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
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();
    }

    public async Task<ForensicsResult> AnalyzeAsync(string imagePath, ForensicsOptions options)
    {
        Log.Information("Starting analysis of {Image}", imagePath);
        Directory.CreateDirectory(options.WorkDir);

        var semaphore = new SemaphoreSlim(Math.Max(1, options.MaxParallelChecks));
        var errors = new ConcurrentDictionary<string, string>();

        Task<T> RunAsync<T>(Func<T> func, T defaultValue, string checkName) => Task.Run(() =>
        {
            semaphore.Wait();
            try
            {
                Log.Debug("Starting {Check}", checkName);
                var result = func();
                Log.Debug("Completed {Check}", checkName);
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Check} failed", checkName);
                errors[checkName] = ex.Message;
                return defaultValue;
            }
            finally
            {
                semaphore.Release();
            }
        });

        Task<(double, string)> elaTask = Task.FromResult((0d, string.Empty));
        if (options.EnabledChecks.HasFlag(ForensicsCheck.Ela))
        {
            Log.Information("Running ELA check");
            try
            {
                elaTask = RunAsync(
                    () => ElaPipeline.Analyze(
                        imagePath,
                        options.WorkDir,
                        options.ElaQuality,
                        options.ElaWindowSize,
                        options.ElaK,
                        options.ElaMinArea,
                        options.ElaKernelSize),
                    (0d, string.Empty),
                    "ELA");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ELA check failed to start");
                errors["ELA"] = ex.Message;
            }
        }

        Task<(double, string)> cmTask = Task.FromResult((0d, string.Empty));
        if (options.EnabledChecks.HasFlag(ForensicsCheck.CopyMove))
        {
            Directory.CreateDirectory(options.CopyMoveMaskDir);
            Log.Information("Running Copy-Move check");
            try
            {
                cmTask = RunAsync(
                    () => CopyMoveDetector.Analyze(
                        imagePath,
                        options.CopyMoveMaskDir,
                        options.CopyMoveFeatureCount,
                        options.CopyMoveMatchDistance,
                        options.CopyMoveRansacReproj,
                        options.CopyMoveRansacProb),
                    (0d, string.Empty),
                    "Copy-Move");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Copy-Move check failed to start");
                errors["Copy-Move"] = ex.Message;
            }
        }

        Task<(double, string)> spTask = Task.FromResult((0d, string.Empty));
        if (options.EnabledChecks.HasFlag(ForensicsCheck.Splicing))
        {
            Directory.CreateDirectory(options.SplicingMapDir);
            Log.Information("Running Splicing check");
            try
            {
                spTask = RunAsync(() =>
                {
                    string modelPath = options.SplicingModelPath;

                    Log.Information("Mantranet model path: {ModelPath}", modelPath);

                    // if (!Path.IsPathRooted(modelPath))
                    // {
                    //     if (!File.Exists(modelPath))
                    //         modelPath = Path.Combine(AppContext.BaseDirectory, modelPath);
                    //     if (!File.Exists(modelPath))
                    //         modelPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                    //             "..", "..", "..", "..", "..", "..", modelPath));
                    // }

                    Log.Information("Mantranet model path: {ModelPath}", modelPath);

                    return DlSplicingDetector.AnalyzeSplicing(
                        imagePath,
                        options.SplicingMapDir,
                        modelPath,
                        options.SplicingInputWidth,
                        options.SplicingInputHeight);
                }, (0d, string.Empty), "Splicing");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Splicing check failed to start");
                errors["Splicing"] = ex.Message;
            }
        }

        Task<(double, string)> ipTask = Task.FromResult((0d, string.Empty));
        if (options.EnabledChecks.HasFlag(ForensicsCheck.Inpainting))
        {
            Directory.CreateDirectory(options.NoiseprintMapDir);
            Log.Information("Running Inpainting check");
            try
            {
                ipTask = RunAsync(() =>
                {
                    string modelsDir = options.NoiseprintModelsDir;

                    Log.Information("Noiseprint models directory: {ModelsDir}", modelsDir);

                    if (!Directory.Exists(modelsDir))
                        throw new DirectoryNotFoundException($"Noiseprint models not found in '{modelsDir}'");

                    return NoiseprintSdkWrapper.Run(
                        imagePath,
                        options.NoiseprintMapDir,
                        modelsDir,
                        options.NoiseprintInputSize);
                }, (0d, string.Empty), "Inpainting");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Inpainting check failed to start");
                errors["Inpainting"] = ex.Message;
            }
        }

        Task<(double, IReadOnlyDictionary<string, string?>)> exifTask =
            Task.FromResult<(double, IReadOnlyDictionary<string, string?>)>(
                (0d, new Dictionary<string, string?>()));
        if (options.EnabledChecks.HasFlag(ForensicsCheck.Exif))
        {
            Directory.CreateDirectory(options.MetadataMapDir);
            Log.Information("Running EXIF check");
            try
            {
                exifTask = RunAsync(() => ExifChecker.Analyze(
                    imagePath,
                    options.MetadataMapDir,
                    options.ExpectedCameraModels),
                    (0d, new Dictionary<string, string?>()),
                    "EXIF");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "EXIF check failed to start");
                errors["EXIF"] = ex.Message;
            }
        }

        try
        {
            await Task.WhenAll(elaTask, cmTask, spTask, ipTask, exifTask);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unexpected error during analysis");
        }

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
            ExifAnomalies = exifAnomalies,
            Errors = new Dictionary<string, string>(errors)
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

