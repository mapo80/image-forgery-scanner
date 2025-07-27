using ImageForensics.Core;
using ImageForensics.Core.Models;

if (args.Length == 0)
{
    Console.WriteLine("Usage: ImageForensics.Cli <image> [--workdir DIR]");
    return;
}

string image = args[0];
string workDir = "results";
for (int i = 1; i < args.Length; i++)
{
    if (args[i] == "--workdir" && i + 1 < args.Length)
    {
        workDir = args[i + 1];
        i++;
    }
}

var analyzer = new ForensicsAnalyzer();
var options = new ForensicsOptions { WorkDir = workDir, CopyMoveMaskDir = workDir };
var res = await analyzer.AnalyzeAsync(image, options);

Console.WriteLine($"ELA score : {res.ElaScore:F3}");
Console.WriteLine($"Verdict   : {res.Verdict}");
Console.WriteLine($"Heat-map  : {res.ElaMapPath}");
Console.WriteLine($"CopyMove score : {res.CopyMoveScore:F3}");
Console.WriteLine($"CopyMove mask  : {res.CopyMoveMaskPath}");
