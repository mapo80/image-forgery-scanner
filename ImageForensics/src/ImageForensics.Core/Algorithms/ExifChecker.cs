using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Iptc;
using MetadataExtractor.Formats.Xmp;
using Serilog;

namespace ImageForensics.Core.Algorithms;

public static class ExifChecker
{
    public static (double Score, IReadOnlyDictionary<string, string?> Anomalies) Analyze(
        string imagePath,
        string mapDir,
        IReadOnlyList<string> expectedCameraModels)
    {
        Log.Information("EXIF analysis for {Image}", imagePath);
        var directories = ImageMetadataReader.ReadMetadata(imagePath);
        Log.Debug("Parsed {Count} metadata directories", directories.Count);
        var anomalies = new Dictionary<string, string?>();
        var expectedModels = expectedCameraModels as ISet<string> ?? new HashSet<string>(expectedCameraModels);

        ExifIfd0Directory? ifd0 = null;
        ExifSubIfdDirectory? subIfd = null;
        GpsDirectory? gpsDir = null;
        Dictionary<string, string?>? tagMap = string.IsNullOrEmpty(mapDir) ? null : new();

        foreach (var dir in directories)
        {
            if (dir is ExifIfd0Directory exif0)
                ifd0 = exif0;
            else if (dir is ExifSubIfdDirectory sub)
                subIfd = sub;
            else if (dir is GpsDirectory gps)
                gpsDir = gps;

            if (tagMap != null)
            {
                foreach (var t in dir.Tags)
                    tagMap[$"{dir.Name}.{t.Name}"] = t.Description;
            }
        }

        if (subIfd == null || !subIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out DateTime dto))
        {
            anomalies["DateTimeOriginal"] = null;
            Log.Debug("DateTimeOriginal missing or invalid");
        }
        else if (dto > DateTime.UtcNow)
        {
            anomalies["DateTimeOriginal"] = dto.ToString("o");
            Log.Debug("DateTimeOriginal in the future: {Value}", dto);
        }

        string? software = ifd0?.GetDescription(ExifDirectoryBase.TagSoftware);
        if (!string.IsNullOrEmpty(software) &&
            (software.Contains("Adobe", StringComparison.OrdinalIgnoreCase) ||
             software.Contains("GIMP", StringComparison.OrdinalIgnoreCase) ||
             software.Contains("Photoshop", StringComparison.OrdinalIgnoreCase)))
        {
            anomalies["Software"] = software;
            Log.Debug("Suspicious software tag {Software}", software);
        }

        string? model = ifd0?.GetDescription(ExifDirectoryBase.TagModel);
        if (string.IsNullOrEmpty(model) || !expectedModels.Contains(model))
        {
            anomalies["Model"] = model;
            Log.Debug("Unexpected camera model {Model}", model);
        }

        var geo = gpsDir?.GetGeoLocation();
        if (geo != null)
        {
            anomalies["GPS"] = geo.ToString();
            Log.Debug("GPS data present {GPS}", geo);
        }

        double score = anomalies.Count / 4.0;
        score = Math.Clamp(score, 0.0, 1.0);
        Log.Information("EXIF analysis completed for {Image}: {Score} anomalies {Count}", imagePath, score, anomalies.Count);

        if (tagMap != null)
        {
            System.IO.Directory.CreateDirectory(mapDir);
            var obj = new { Tags = tagMap, Anomalies = anomalies };
            string jsonPath = Path.Combine(mapDir, Path.GetFileNameWithoutExtension(imagePath) + "_metadata.json");
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true }));
        }

        return (score, anomalies);
    }
}
