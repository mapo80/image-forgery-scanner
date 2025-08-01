using System.Text;
using ImageMagick;
using ImageForensics.Core.Algorithms;

static byte[,] LoadMask(MagickImage img)
{
    int w = img.Width;
    int h = img.Height;
    var mask = new byte[w, h];
    using var pixels = img.GetPixels();
    for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            mask[x, y] = pixels.GetPixel(x, y).GetChannel(0) > 127 ? (byte)1 : (byte)0;
    return mask;
}

string datasetRoot = args.Length > 0 ? args[0] : "dataset/imd2020";
string tamperedDir = Path.Combine(datasetRoot, "images", "tampered");
string maskDir = Path.Combine(datasetRoot, "images", "mask");

var files = Directory.GetFiles(tamperedDir).OrderBy(f => f).ToArray();
var sb = new StringBuilder();
sb.AppendLine("| Immagine    | RocAuc | Prauc | NSS  | IoU   | Dice  | MCC   |");
sb.AppendLine("|-------------|--------|-------|------|-------|-------|-------|");
foreach (var imgPath in files)
{
    string name = Path.GetFileName(imgPath);
    string maskPath = Path.Combine(maskDir, Path.GetFileNameWithoutExtension(imgPath) + "_mask.png");
    if (!File.Exists(maskPath)) continue;
    using var img = new MagickImage(imgPath);
    using var maskImg = new MagickImage(maskPath);
    var mask = LoadMask(maskImg);
    var ela = ElaMetrics.ComputeElaMap(img);
    double roc = ElaMetrics.ComputeRocAucPixel(mask, ela);
    double pr = ElaMetrics.ComputePraucPixel(mask, ela);
    double nss = ElaMetrics.ComputeNss(mask, ela);
    double thr = ElaMetrics.ComputeOtsuThreshold(ela);
    var pred = ElaMetrics.BinarizeElaMap(ela, thr);
    double iou = ElaMetrics.ComputeIoUPixel(mask, pred);
    double dice = ElaMetrics.ComputeDicePixel(mask, pred);
    double mcc = ElaMetrics.ComputeMccPixel(mask, pred);
    sb.AppendLine($"| {name} | {roc:F2} | {pr:F2} | {nss:F2} | {iou:F2} | {dice:F2} | {mcc:F2} |");
}
File.WriteAllText(Path.Combine(datasetRoot, "..", "..", "ela-metrics-table.md"), sb.ToString());
