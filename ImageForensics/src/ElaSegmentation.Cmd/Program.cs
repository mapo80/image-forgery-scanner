using ImageForensics.Core.Algorithms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Linq;

var config = new ConfigurationBuilder()
    .AddCommandLine(args)
    .Build();

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
