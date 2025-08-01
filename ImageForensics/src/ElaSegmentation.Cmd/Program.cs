using ImageForensics.Core.Algorithms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Linq;

var config = new ConfigurationBuilder()
    .AddCommandLine(args)
    .Build();

if (config["folds"] is not null)
{
    string imagesDir = config["imagesDir"] ?? "./dataset/imd2020/images/tampered";
    string masksDir = config["masksDir"] ?? "./dataset/imd2020/images/mask";
    int folds = int.Parse(config["folds"] ?? "5");
    string thrStr = config["thresholds"] ?? "0.01,0.05,0.10,0.15,0.20";
    string minAreasStr = config["minAreas"] ?? "50,100,200";
    string kernelsStr = config["kernelSizes"] ?? "3,5,7";
    string outputCsv = config["outputCsv"] ?? "cv_results.csv";
    string outputHtml = config["outputHtml"] ?? "cv_report.html";

    var thresholds = thrStr.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(double.Parse);
    var minAreas = minAreasStr.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse);
    var kernels = kernelsStr.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse);
    var refineParams = from ma in minAreas from ks in kernels select new RefineParams { MinArea = ma, KernelSize = ks };

    using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
    var logger = loggerFactory.CreateLogger<CrossValidationRunner>();
    var runner = new CrossValidationRunner(logger);
    runner.Run(imagesDir, masksDir, folds, thresholds, refineParams, outputCsv, outputHtml);
}
else
{
    string imagesDir = config["imagesDir"] ?? "./images";
    string masksDir = config["masksDir"] ?? "./masks";
    int jpegQuality = int.Parse(config["jpegQuality"] ?? "90");
    int sauvolaWindow = int.Parse(config["sauvolaWindow"] ?? "15");
    double sauvolaK = double.Parse(config["sauvolaK"] ?? "0.2");
    int blocksX = int.Parse(config["blocksX"] ?? "4");
    int blocksY = int.Parse(config["blocksY"] ?? "4");
    int minArea = int.Parse(config["minArea"] ?? "100");
    int kernelSize = int.Parse(config["kernelSize"] ?? "5");
    int sampleCount = int.Parse(config["sampleCount"] ?? "0");
    ElaPipeline.Run(imagesDir, masksDir, jpegQuality, sauvolaWindow, sauvolaK, blocksX, blocksY, minArea, kernelSize, sampleCount);
}
