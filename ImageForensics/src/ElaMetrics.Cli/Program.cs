using ImageForensics.Core.Algorithms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

// Enable System.Drawing on Unix platforms
AppContext.SetSwitch("System.Drawing.EnableUnixSupport", true);

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .Build();

int[]? jpegQualitiesConf = config.GetSection("JpegQualities").Get<int[]>();
int[] jpegQualities = jpegQualitiesConf ?? new[] { 90, 80, 70 };
var sweepSec = config.GetSection("Sweep").GetSection("Thresholds");
double tMin = sweepSec.GetValue<double>("Min", 0.0);
double tMax = sweepSec.GetValue<double>("Max", 1.0);
double tStep = sweepSec.GetValue<double>("Step", 0.1);
var morphSpace = config.GetSection("Morphology").GetSection("ParamSpace").Get<RefineParams[]>()
    ?? new[] { new RefineParams { MinArea = 50, KernelSize = 3 } };
int boundaryTol = config.GetValue<int>("BoundaryTolerancePixel", 2);
int sauvolaWin = config.GetSection("Sauvola").GetValue<int>("WindowSize", 15);
double sauvolaK = config.GetSection("Sauvola").GetValue<double>("K", 0.5);

string datasetRoot = args.Length > 0 ? args[0] : "dataset/imd2020";
string imagesDir = Path.Combine(datasetRoot, "images", "tampered");
string masksDir = Path.Combine(datasetRoot, "images", "mask");

using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<ElaAnalysisRunner>();

var runner = new ElaAnalysisRunner(logger, jpegQualities, tMin, tMax, tStep, morphSpace, boundaryTol, sauvolaWin, sauvolaK);
runner.Run(imagesDir, masksDir, Path.Combine(datasetRoot, "ela-advanced-metrics.csv"), Path.Combine(datasetRoot, "ela-advanced-report.html"));
