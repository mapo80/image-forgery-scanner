using ImageForensics.Core;
using ImageForensics.Core.Models;
using ImageForensics.Core.Algorithms;
using System.Diagnostics;
using System.Text.Json;
using System.Linq;
using System.Collections.Concurrent;

void PrintUsage()
{
    Console.WriteLine("Usage: ImageForensics.Cli <image> [--workdir DIR] [--checks LIST] [--parallel N]");
    Console.WriteLine("       ImageForensics.Cli --benchmark-ela|--benchmark-copy-move|--benchmark-splicing|--benchmark-inpainting|--benchmark-exif|--benchmark-all --input-dir DIR [--report-dir DIR] [--workdir DIR] [--parallel-images N]");
}

if (args.Length == 0)
{
    PrintUsage();
    return;
}

string? image = null;
string workDir = "results";
string inputDir = string.Empty;
string reportDir = "benchmark_report";
ForensicsCheck checks = ForensicsCheck.All;
int parallel = 1;
int parallelImages = 1;
int siftFeatures = 1000;
double loweRatio = 0.8;
string thresholdMode = "otsu";
double minAreaPct = 0.001;
double clusterEps = 20.0;
int clusterMinPts = 5;
int morphOpen = 3;
int morphClose = 5;
double percentile = 0.95;

bool bench = false;
bool benchEla = false;
bool benchCm = false;
bool benchSp = false;
bool benchIp = false;
bool benchExif = false;
bool benchAll = false;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--workdir" when i + 1 < args.Length:
            workDir = args[++i];
            break;
        case "--input-dir" when i + 1 < args.Length:
            inputDir = args[++i];
            break;
        case "--report-dir" when i + 1 < args.Length:
            reportDir = args[++i];
            break;
        case "--checks" when i + 1 < args.Length:
            checks = args[++i]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => Enum.Parse<ForensicsCheck>(s, true))
                .Aggregate(ForensicsCheck.None, (a, c) => a | c);
            break;
        case "--parallel" when i + 1 < args.Length:
            parallel = int.Parse(args[++i]);
            break;
        case "--parallel-images" when i + 1 < args.Length:
            parallelImages = int.Parse(args[++i]);
            break;
        case "--sift-features" when i + 1 < args.Length:
            siftFeatures = int.Parse(args[++i]);
            break;
        case "--lowe-ratio" when i + 1 < args.Length:
            loweRatio = double.Parse(args[++i]);
            break;
        case "--cluster-eps" when i + 1 < args.Length:
            clusterEps = double.Parse(args[++i]);
            break;
        case "--cluster-min-pts" when i + 1 < args.Length:
            clusterMinPts = int.Parse(args[++i]);
            break;
        case "--morph-open" when i + 1 < args.Length:
            morphOpen = int.Parse(args[++i]);
            break;
        case "--morph-close" when i + 1 < args.Length:
            morphClose = int.Parse(args[++i]);
            break;
        case "--threshold-mode" when i + 1 < args.Length:
            thresholdMode = args[++i];
            break;
        case "--percentile" when i + 1 < args.Length:
            percentile = double.Parse(args[++i]);
            break;
        case "--min-area-pct" when i + 1 < args.Length:
            minAreaPct = double.Parse(args[++i]);
            break;
        case "--benchmark":
            bench = true;
            break;
        case "--benchmark-ela":
            benchEla = true;
            break;
        case "--benchmark-copy-move":
            benchCm = true;
            break;
        case "--benchmark-splicing":
            benchSp = true;
            break;
        case "--benchmark-inpainting":
            benchIp = true;
            break;
        case "--benchmark-exif":
            benchExif = true;
            break;
        case "--benchmark-all":
            benchAll = true;
            break;
        default:
            if (image == null)
                image = args[i];
            break;
    }
}

if (benchAll)
{
    benchEla = benchCm = benchSp = benchIp = benchExif = true;
    bench = true;
}

bool runBenchmark = bench || benchEla || benchCm || benchSp || benchIp || benchExif;
if (runBenchmark)
{
    if (string.IsNullOrEmpty(inputDir))
    {
        Console.WriteLine("--input-dir is required for benchmarking.");
        return;
    }
    await RunBenchmarkAsync(inputDir, reportDir, workDir, benchEla, benchCm, benchSp, benchIp, benchExif, benchAll, parallelImages, siftFeatures, loweRatio, clusterEps, clusterMinPts, morphOpen, morphClose, thresholdMode, percentile, minAreaPct);
    return;
}

if (image == null)
{
    PrintUsage();
    return;
}

var analyzer = new ForensicsAnalyzer();
var opts = new ForensicsOptions
{
    WorkDir = workDir,
    CopyMoveMaskDir = workDir,
    SplicingMapDir = workDir,
    NoiseprintMapDir = workDir,
    MetadataMapDir = workDir,
    EnabledChecks = checks,
    MaxParallelChecks = parallel
};
var result = await analyzer.AnalyzeAsync(image, opts);
Console.WriteLine($"ELA score       : {result.ElaScore:F3}");
Console.WriteLine($"CopyMove score  : {result.CopyMoveScore:F3}");
Console.WriteLine($"Splicing score  : {result.SplicingScore:F3}");
Console.WriteLine($"Inpainting score: {result.InpaintingScore:F3}");
Console.WriteLine($"Exif score      : {result.ExifScore:F3}");
Console.WriteLine($"Total score     : {result.TotalScore:F3}");
Console.WriteLine($"Verdict         : {result.Verdict}");

return;

static Task RunBenchmarkAsync(
    string inputDir,
    string reportDir,
    string workDir,
    bool benchEla,
    bool benchCm,
    bool benchSp,
    bool benchIp,
    bool benchExif,
    bool saveReport,
    int parallelImages,
    int siftFeatures,
    double loweRatio,
    double clusterEps,
    int clusterMinPts,
    int morphOpen,
    int morphClose,
    string thresholdMode,
    double percentile,
    double minAreaPct)
{
    Directory.CreateDirectory(workDir);
    Directory.CreateDirectory(reportDir);

    var opts = new ForensicsOptions
    {
        WorkDir = workDir,
        CopyMoveMaskDir = workDir,
        SplicingMapDir = workDir,
        NoiseprintMapDir = workDir,
        MetadataMapDir = workDir
    };

    if (benchCm && Directory.Exists(Path.Combine(inputDir, "forged")) && Directory.Exists(Path.Combine(inputDir, "mask")))
    {
        string csvPath = Path.Combine("bench", "copymove", "metrics.csv");
        CopyMoveAnalysisRunner.Run(inputDir, csvPath,
            siftFeatures, loweRatio, clusterEps, clusterMinPts, morphOpen, morphClose, thresholdMode, percentile, minAreaPct);
        Console.WriteLine($"Copy-Move metrics written to {csvPath}");
        return Task.CompletedTask;
    }

    var files = Directory.GetFiles(inputDir, "*.*", SearchOption.AllDirectories)
        .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        .ToArray();

    var elaTimes = new ConcurrentBag<long>();
    var cmTimes = new ConcurrentBag<long>();
    var spTimes = new ConcurrentBag<long>();
    var ipTimes = new ConcurrentBag<long>();
    var exifTimes = new ConcurrentBag<long>();

    var results = new ConcurrentBag<Dictionary<string, object?>>();

    var totalSw = Stopwatch.StartNew();

    void ProcessFile(string file)
    {
        var record = new Dictionary<string, object?>
        {
            ["file"] = file
        };

        if (benchEla)
        {
            var sw = Stopwatch.StartNew();
            var (score, _) = ElaPipeline.Analyze(
                file,
                workDir,
                opts.ElaQuality,
                opts.ElaWindowSize,
                opts.ElaK,
                opts.ElaMinArea,
                opts.ElaKernelSize);
            sw.Stop();
            elaTimes.Add(sw.ElapsedMilliseconds);
            record["elaScore"] = score;
            record["elaMs"] = sw.ElapsedMilliseconds;
        }
        if (benchCm)
        {
            var sw = Stopwatch.StartNew();
            var (score, _) = CopyMoveDetector.Analyze(file, workDir,
                opts.CopyMoveFeatureCount, opts.CopyMoveMatchDistance,
                opts.CopyMoveRansacReproj, opts.CopyMoveRansacProb);
            sw.Stop();
            cmTimes.Add(sw.ElapsedMilliseconds);
            record["copyMoveScore"] = score;
            record["copyMoveMs"] = sw.ElapsedMilliseconds;
        }
        if (benchSp)
        {
            var sw = Stopwatch.StartNew();
            var (score, _) = DlSplicingDetector.AnalyzeSplicing(file, workDir,
                opts.SplicingModelPath, opts.SplicingInputWidth, opts.SplicingInputHeight);
            sw.Stop();
            spTimes.Add(sw.ElapsedMilliseconds);
            record["splicingScore"] = score;
            record["splicingMs"] = sw.ElapsedMilliseconds;
        }
        if (benchIp)
        {
            var sw = Stopwatch.StartNew();
            var (score, _) = NoiseprintSdkWrapper.Run(file, workDir,
                opts.NoiseprintModelsDir, opts.NoiseprintInputSize);
            sw.Stop();
            ipTimes.Add(sw.ElapsedMilliseconds);
            record["inpaintingScore"] = score;
            record["inpaintingMs"] = sw.ElapsedMilliseconds;
        }
        if (benchExif)
        {
            var sw = Stopwatch.StartNew();
            var (score, _) = ExifChecker.Analyze(file, workDir, opts.ExpectedCameraModels);
            sw.Stop();
            exifTimes.Add(sw.ElapsedMilliseconds);
            record["exifScore"] = score;
            record["exifMs"] = sw.ElapsedMilliseconds;
        }

        Console.WriteLine($"{file}: " +
            (benchEla ? $"ELA {record["elaMs"]} ms {record["elaScore"]:0.000} " : "") +
            (benchCm ? $"CM {record["copyMoveMs"]} ms {record["copyMoveScore"]:0.000} " : "") +
            (benchSp ? $"SP {record["splicingMs"]} ms {record["splicingScore"]:0.000} " : "") +
            (benchIp ? $"IP {record["inpaintingMs"]} ms {record["inpaintingScore"]:0.000} " : "") +
            (benchExif ? $"EXIF {record["exifMs"]} ms {record["exifScore"]:0.000}" : ""));

        results.Add(record);
    }

    if (parallelImages <= 1)
    {
        foreach (var file in files)
            ProcessFile(file);
    }
    else
    {
        Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = parallelImages }, ProcessFile);
    }

    totalSw.Stop();
    Console.WriteLine($"Processed {files.Length} images in {totalSw.ElapsedMilliseconds} ms");

    void PrintStats(string name, IEnumerable<long> list)
    {
        var arr = list.ToArray();
        if (arr.Length == 0) return;
        double avg = arr.Average();
        double std = Math.Sqrt(arr.Sum(t => (t - avg) * (t - avg)) / arr.Length);
        Console.WriteLine($"{name,-10} – avg {avg:F0} ms ± {std:F0} ms on {arr.Length} images");
    }

    Console.WriteLine("Benchmark summary:");
    PrintStats("ELA", elaTimes);
    PrintStats("Copy-Move", cmTimes);
    PrintStats("Splicing", spTimes);
    PrintStats("Inpainting", ipTimes);
    PrintStats("EXIF", exifTimes);

    if (saveReport)
    {
        var stats = new Dictionary<string, object?>
        {
            ["ELA"] = elaTimes.Count == 0 ? null : new { Average = elaTimes.Average(), StdDev = Math.Sqrt(elaTimes.Sum(t => (t - elaTimes.Average()) * (t - elaTimes.Average())) / elaTimes.Count), Count = elaTimes.Count },
            ["CopyMove"] = cmTimes.Count == 0 ? null : new { Average = cmTimes.Average(), StdDev = Math.Sqrt(cmTimes.Sum(t => (t - cmTimes.Average()) * (t - cmTimes.Average())) / cmTimes.Count), Count = cmTimes.Count },
            ["Splicing"] = spTimes.Count == 0 ? null : new { Average = spTimes.Average(), StdDev = Math.Sqrt(spTimes.Sum(t => (t - spTimes.Average()) * (t - spTimes.Average())) / spTimes.Count), Count = spTimes.Count },
            ["Inpainting"] = ipTimes.Count == 0 ? null : new { Average = ipTimes.Average(), StdDev = Math.Sqrt(ipTimes.Sum(t => (t - ipTimes.Average()) * (t - ipTimes.Average())) / ipTimes.Count), Count = ipTimes.Count },
            ["EXIF"] = exifTimes.Count == 0 ? null : new { Average = exifTimes.Average(), StdDev = Math.Sqrt(exifTimes.Sum(t => (t - exifTimes.Average()) * (t - exifTimes.Average())) / exifTimes.Count), Count = exifTimes.Count }
        };
        var obj = new { Results = results.ToArray(), Stats = stats };
        string jsonPath = Path.Combine(reportDir, "benchmark.json");
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true }));
    }
    return Task.CompletedTask;
}
