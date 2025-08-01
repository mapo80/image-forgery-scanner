using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace ImageForensics.Tests;

public class CliBenchmarkTests
{
    private static string RepoRoot => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../"));

    [Fact(Skip = "Requires CLI benchmark data and models")]
    public void Benchmark_All_Generates_Report()
    {
        string cliProj = Path.Combine(RepoRoot, "ImageForensics", "src", "ImageForensics.Cli", "ImageForensics.Cli.csproj");
        string dataDir = Path.Combine(AppContext.BaseDirectory, "testdata", "casia2", "authentic");
        string inputDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(inputDir);
        foreach (var img in Directory.GetFiles(dataDir).Take(5))
            File.Copy(img, Path.Combine(inputDir, Path.GetFileName(img)));
        string reportDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(reportDir);

        // Ensure the splicing model is reachable by the CLI working directory
        string modelSrc = Path.Combine(RepoRoot, "ImageForensics", "src", "Models", "onnx", "mantranet_256x256.onnx");
        string modelDst = Path.Combine(RepoRoot, "mantranet_256x256.onnx");
        if (!File.Exists(modelDst))
            File.Copy(modelSrc, modelDst);

        var psi = new ProcessStartInfo("dotnet", $"run --project {cliProj} -- --benchmark-all --input-dir {inputDir} --report-dir {reportDir} --workdir {reportDir}");
        psi.Environment["LD_LIBRARY_PATH"] = Path.Combine(RepoRoot, "so");
        psi.WorkingDirectory = RepoRoot;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        var proc = Process.Start(psi)!;
        string output = proc.StandardOutput.ReadToEnd();
        string err = proc.StandardError.ReadToEnd();
        proc.WaitForExit();
        output += err;

        proc.ExitCode.Should().Be(0, output);
        string jsonPath = Path.Combine(reportDir, "benchmark.json");
        File.Exists(jsonPath).Should().BeTrue();
        var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var stats = doc.RootElement.GetProperty("Stats");
        double avg = stats.GetProperty("ELA").GetProperty("Average").GetDouble();
        avg.Should().BeGreaterThan(0);
    }
}
