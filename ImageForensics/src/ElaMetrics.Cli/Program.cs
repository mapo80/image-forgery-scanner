using ImageForensics.Core.Algorithms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .Build();

int[]? jpegQualitiesConf = config.GetSection("JpegQualities").Get<int[]>();
int[] jpegQualities = jpegQualitiesConf ?? new[] { 90, 80, 70 };
int minArea = config.GetSection("Morphology").GetValue<int>("MinArea", 50);
int boundaryTol = config.GetValue<int>("BoundaryTolerancePixel", 2);
string modelPath = config.GetValue<string>("ModelPath", string.Empty) ?? string.Empty;

string datasetRoot = args.Length > 0 ? args[0] : "dataset/imd2020";
string imagesDir = Path.Combine(datasetRoot, "images", "tampered");
string masksDir = Path.Combine(datasetRoot, "images", "mask");

using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<ElaAnalysisRunner>();

var runner = new ElaAnalysisRunner(logger, jpegQualities, minArea, boundaryTol);
runner.Run(imagesDir, masksDir, modelPath, Path.Combine(datasetRoot, "ela-advanced-metrics.csv"), Path.Combine(datasetRoot, "ela-advanced-report.html"));
