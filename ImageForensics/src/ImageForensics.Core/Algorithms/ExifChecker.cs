using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Iptc;
using MetadataExtractor.Formats.Xmp;

namespace ImageForensics.Core.Algorithms;

public static class ExifChecker
{
    public static (double Score, IReadOnlyDictionary<string, string?> Anomalies) Analyze(
        string imagePath,
        string mapDir,
        IReadOnlyList<string> expectedCameraModels)
    {
        var directories = ImageMetadataReader.ReadMetadata(imagePath);
        var anomalies = new Dictionary<string, string?>();

        var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
        var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();

        if (subIfd == null || !subIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out DateTime dto))
        {
            anomalies["DateTimeOriginal"] = null;
        }
        else if (dto > DateTime.UtcNow)
        {
            anomalies["DateTimeOriginal"] = dto.ToString("o");
        }

        string? software = ifd0?.GetDescription(ExifDirectoryBase.TagSoftware);
        if (!string.IsNullOrEmpty(software) &&
            (software.Contains("Adobe", StringComparison.OrdinalIgnoreCase) ||
             software.Contains("GIMP", StringComparison.OrdinalIgnoreCase) ||
             software.Contains("Photoshop", StringComparison.OrdinalIgnoreCase)))
        {
            anomalies["Software"] = software;
        }

        string? model = ifd0?.GetDescription(ExifDirectoryBase.TagModel);
        if (string.IsNullOrEmpty(model) || !expectedCameraModels.Contains(model))
        {
            anomalies["Model"] = model;
        }

        var gpsDir = directories.OfType<GpsDirectory>().FirstOrDefault();
        var geo = gpsDir?.GetGeoLocation();
        if (geo != null)
        {
            anomalies["GPS"] = geo.ToString();
        }

        double score = anomalies.Count / 4.0;
        score = Math.Clamp(score, 0.0, 1.0);

        if (!string.IsNullOrEmpty(mapDir))
        {
            System.IO.Directory.CreateDirectory(mapDir);
            var tagMap = directories
                .SelectMany(d => d.Tags.Select(t => new KeyValuePair<string, string?>($"{d.Name}.{t.Name}", t.Description)))
                .ToDictionary(k => k.Key, v => v.Value);
            var obj = new { Tags = tagMap, Anomalies = anomalies };
            string jsonPath = Path.Combine(mapDir, Path.GetFileNameWithoutExtension(imagePath) + "_metadata.json");
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true }));
        }

        return (score, anomalies);
    }
}
