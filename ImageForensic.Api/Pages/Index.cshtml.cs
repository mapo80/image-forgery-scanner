using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ImageForensics.Core;
using ImageForensics.Core.Models;
using Serilog;

namespace ImageForensic.Api.Pages;

[IgnoreAntiforgeryToken]
public class IndexModel : PageModel
{
    private readonly IForensicsAnalyzer _analyzer;

    public IndexModel(IForensicsAnalyzer analyzer) => _analyzer = analyzer;

    [BindProperty]
    public IFormFile? Image { get; set; }

    [BindProperty]
    public AnalyzeImageOptionsForm Options { get; set; } = new();

    public AnalyzeImageResult? Result { get; private set; }

    public string ElaMapBase64 { get; private set; } = string.Empty;
    public string CopyMoveMapBase64 { get; private set; } = string.Empty;
    public string InpaintingMapBase64 { get; private set; } = string.Empty;

    public AnalyzeImageOptionsForm DefaultOptions { get; } = new() { ElaQuality = 90, ElaMinArea = 50, ElaKernelSize = 3 };
    public List<string> AvailableCameraModels { get; } = new() { "Canon EOS 80D", "Nikon D850" };
    public Dictionary<ForensicsCheck, string> CheckDescriptions { get; } = new()
    {
        { ForensicsCheck.Ela, "Error Level Analysis highlights compression inconsistencies." },
        { ForensicsCheck.CopyMove, "Detects duplicated regions from copy-move operations." },
        { ForensicsCheck.Inpainting, "Looks for traces of inpainting." },
        { ForensicsCheck.Exif, "Analyzes image metadata for anomalies." }
    };

    public string ExplainScore(double score)
    {
        if (score < Options.CleanThreshold)
            return $"Below clean threshold ({Options.CleanThreshold:F2}): the check shows no tampering.";
        if (score > Options.TamperedThreshold)
            return $"Above tampered threshold ({Options.TamperedThreshold:F2}): the check suggests tampering.";
        return $"Between thresholds ({Options.CleanThreshold:F2}-{Options.TamperedThreshold:F2}): the result is uncertain.";
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Image == null) return Page();

        var tempFile = Path.Combine(Path.GetTempPath(),
            $"{Guid.NewGuid()}{Path.GetExtension(Image.FileName)}");
        await using (var stream = System.IO.File.Create(tempFile))
        {
            await Image.CopyToAsync(stream);
        }

        var workDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(workDir);

        var opts = Options.ToOptions().ToForensicsOptions() with
        {
            WorkDir = workDir,
            CopyMoveMaskDir = workDir,
            MetadataMapDir = workDir
        };

        try
        {
            var res = await _analyzer.AnalyzeAsync(tempFile, opts);
            Result = AnalyzeImageResult.From(res);
            ElaMapBase64 = Convert.ToBase64String(Result.ElaMap);
            CopyMoveMapBase64 = Convert.ToBase64String(Result.CopyMoveMask);
            InpaintingMapBase64 = Convert.ToBase64String(Result.InpaintingMap);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error analyzing image");
        }
        finally
        {
            try { System.IO.File.Delete(tempFile); } catch (Exception ex) { Log.Error(ex, "Error deleting temp file"); }
            try { Directory.Delete(workDir, true); } catch (Exception ex) { Log.Error(ex, "Error deleting work dir"); }
        }

        return Page();
    }

    public class AnalyzeImageOptionsForm
    {
        [Display(Name = "ELA quality")]
        public int ElaQuality { get; set; } = 90;

        [Display(Name = "ELA window size")]
        public int ElaWindowSize { get; set; } = 15;

        [Display(Name = "ELA k")]
        public double ElaK { get; set; } = 0.2;

        [Display(Name = "ELA min area")]
        public int ElaMinArea { get; set; } = 50;

        [Display(Name = "ELA kernel size")]
        public int ElaKernelSize { get; set; } = 3;

        [Display(Name = "Copy-Move feature count")]
        public int CopyMoveFeatureCount { get; set; } = 5000;

        [Display(Name = "Copy-Move match distance")]
        public double CopyMoveMatchDistance { get; set; } = 3.0;

        [Display(Name = "Copy-Move RANSAC reprojection")]
        public double CopyMoveRansacReproj { get; set; } = 3.0;

        [Display(Name = "Copy-Move RANSAC probability")]
        public double CopyMoveRansacProb { get; set; } = 0.99;

        [Display(Name = "Expected camera models")]
        public List<string> ExpectedCameraModels { get; set; } = new() { "Canon EOS 80D", "Nikon D850" };

        [Display(Name = "ELA weight")]
        public double ElaWeight { get; set; } = 1.0;

        [Display(Name = "Copy-Move weight")]
        public double CopyMoveWeight { get; set; } = 1.0;

        [Display(Name = "Inpainting weight")]
        public double InpaintingWeight { get; set; } = 1.0;

        [Display(Name = "Metadata weight")]
        public double ExifWeight { get; set; } = 1.0;

        [Display(Name = "Clean threshold")]
        public double CleanThreshold { get; set; } = 0.2;

        [Display(Name = "Tampered threshold")]
        public double TamperedThreshold { get; set; } = 0.8;

        [Display(Name = "Enabled checks")]
        public List<string> EnabledChecks { get; set; } = new() { "Ela", "CopyMove", "Inpainting", "Exif" };

        [Display(Name = "Max parallel checks")]
        public int MaxParallelChecks { get; set; } = 1;

        public AnalyzeImageOptions ToOptions()
        {
            var flags = ForensicsCheck.None;
            foreach (var e in EnabledChecks)
                flags |= Enum.Parse<ForensicsCheck>(e, true);

            return new AnalyzeImageOptions
            {
                ElaQuality = ElaQuality,
                ElaWindowSize = ElaWindowSize,
                ElaK = ElaK,
                ElaMinArea = ElaMinArea,
                ElaKernelSize = ElaKernelSize,
                CopyMoveFeatureCount = CopyMoveFeatureCount,
                CopyMoveMatchDistance = CopyMoveMatchDistance,
                CopyMoveRansacReproj = CopyMoveRansacReproj,
                CopyMoveRansacProb = CopyMoveRansacProb,
                ExpectedCameraModels = ExpectedCameraModels.ToArray(),
                ElaWeight = ElaWeight,
                CopyMoveWeight = CopyMoveWeight,
                InpaintingWeight = InpaintingWeight,
                ExifWeight = ExifWeight,
                CleanThreshold = CleanThreshold,
                TamperedThreshold = TamperedThreshold,
                EnabledChecks = flags,
                MaxParallelChecks = MaxParallelChecks
            };
        }
    }
}
