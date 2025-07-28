using ImageForensics.Core;
using ImageForensics.Core.Models;
using System.Diagnostics;
using System.Collections.Generic;

if (args.Length == 0)
{
    Console.WriteLine("Usage: ImageForensics.Cli <image> [--workdir DIR] [--benchmark --benchdir DIR] [--benchmark-inpainting]");
    return;
}

string? image = null;
string workDir = "results";
bool benchmark = false;
bool benchmarkIp = false;
string benchDir = string.Empty;

for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--workdir" && i + 1 < args.Length)
    {
        workDir = args[i + 1];
        i++;
    }
    else if (args[i] == "--benchmark")
    {
        benchmark = true;
    }
    else if (args[i] == "--benchmark-inpainting")
    {
        benchmarkIp = true;
    }
    else if (args[i] == "--benchdir" && i + 1 < args.Length)
    {
        benchDir = args[i + 1];
        i++;
    }
    else if (image == null)
    {
        image = args[i];
    }
}

var analyzer = new ForensicsAnalyzer();
var options = new ForensicsOptions
{
    WorkDir = workDir,
    CopyMoveMaskDir = workDir,
    SplicingMapDir = workDir,
    NoiseprintMapDir = workDir
};

if (benchmarkIp)
{
    var files = Directory.GetFiles(benchDir, "*.png");
    var times = new List<long>();
    foreach (var file in files)
    {
        var sw = Stopwatch.StartNew();
        var res = await analyzer.AnalyzeAsync(file, options);
        sw.Stop();
        Console.WriteLine($"{file}: score={res.InpaintingScore:F3}, time={sw.ElapsedMilliseconds} ms");
        times.Add(sw.ElapsedMilliseconds);
    }

    Console.WriteLine($"Average inpainting time: {times.Average():F1} ms");
}
else if (benchmark)
{
    var files = Directory.GetFiles(benchDir, "*.jpg");
    var times = new List<long>();
    foreach (var file in files)
    {
        var sw = Stopwatch.StartNew();
        var res = await analyzer.AnalyzeAsync(file, options);
        sw.Stop();
        Console.WriteLine($"{file}: score={res.SplicingScore:F3}, time={sw.ElapsedMilliseconds} ms");
        times.Add(sw.ElapsedMilliseconds);
    }

    Console.WriteLine($"Average splicing time: {times.Average():F1} ms");
}
else if (image != null)
{
    var res = await analyzer.AnalyzeAsync(image, options);

    Console.WriteLine($"ELA score : {res.ElaScore:F3}");
    Console.WriteLine($"Verdict   : {res.Verdict}");
    Console.WriteLine($"Heat-map  : {res.ElaMapPath}");
    Console.WriteLine($"CopyMove score : {res.CopyMoveScore:F3}");
    Console.WriteLine($"CopyMove mask  : {res.CopyMoveMaskPath}");
    Console.WriteLine($"Splicing score : {res.SplicingScore:F3}");
    Console.WriteLine($"Splicing map   : {res.SplicingMapPath}");
    Console.WriteLine($"Inpainting score : {res.InpaintingScore:F3}");
    Console.WriteLine($"Inpainting map   : {res.InpaintingMapPath}");
    Console.WriteLine($"Total score   : {res.TotalScore:F3}");
    Console.WriteLine($"Final verdict : {res.Verdict}");
}
