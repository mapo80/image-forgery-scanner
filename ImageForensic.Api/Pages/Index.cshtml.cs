using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ImageForensics.Core;
using ImageForensics.Core.Models;

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
    public string SplicingMapBase64 { get; private set; } = string.Empty;
    public string InpaintingMapBase64 { get; private set; } = string.Empty;

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
            SplicingMapDir = workDir,
            NoiseprintMapDir = workDir,
            MetadataMapDir = workDir
        };

        try
        {
            var res = await _analyzer.AnalyzeAsync(tempFile, opts);
            Result = AnalyzeImageResult.From(res);
            ElaMapBase64 = Convert.ToBase64String(Result.ElaMap);
            CopyMoveMapBase64 = Convert.ToBase64String(Result.CopyMoveMask);
            SplicingMapBase64 = Convert.ToBase64String(Result.SplicingMap);
            InpaintingMapBase64 = Convert.ToBase64String(Result.InpaintingMap);
        }
        finally
        {
            try { System.IO.File.Delete(tempFile); } catch { }
            try { Directory.Delete(workDir, true); } catch { }
        }

        return Page();
    }

    public class AnalyzeImageOptionsForm
    {
        public int ElaQuality { get; set; } = 75;
        public int CopyMoveFeatureCount { get; set; } = 5000;
        public double CopyMoveMatchDistance { get; set; } = 3.0;
        public double CopyMoveRansacReproj { get; set; } = 3.0;
        public double CopyMoveRansacProb { get; set; } = 0.99;
        public int SplicingInputWidth { get; set; } = 256;
        public int SplicingInputHeight { get; set; } = 256;
        public int NoiseprintInputSize { get; set; } = 320;
        public string ExpectedCameraModels { get; set; } = "Canon EOS 80D,Nikon D850";
        public double ElaWeight { get; set; } = 1.0;
        public double CopyMoveWeight { get; set; } = 1.0;
        public double SplicingWeight { get; set; } = 1.0;
        public double InpaintingWeight { get; set; } = 1.0;
        public double ExifWeight { get; set; } = 1.0;
        public double CleanThreshold { get; set; } = 0.2;
        public double TamperedThreshold { get; set; } = 0.8;
        public string EnabledChecks { get; set; } = "Ela,CopyMove,Splicing,Inpainting,Exif";
        public int MaxParallelChecks { get; set; } = 1;

        public AnalyzeImageOptions ToOptions()
        {
            var enabled = EnabledChecks.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var flags = ForensicsCheck.None;
            foreach (var e in enabled)
                flags |= Enum.Parse<ForensicsCheck>(e, true);

            return new AnalyzeImageOptions
            {
                ElaQuality = ElaQuality,
                CopyMoveFeatureCount = CopyMoveFeatureCount,
                CopyMoveMatchDistance = CopyMoveMatchDistance,
                CopyMoveRansacReproj = CopyMoveRansacReproj,
                CopyMoveRansacProb = CopyMoveRansacProb,
                SplicingInputWidth = SplicingInputWidth,
                SplicingInputHeight = SplicingInputHeight,
                NoiseprintInputSize = NoiseprintInputSize,
                ExpectedCameraModels = ExpectedCameraModels.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                ElaWeight = ElaWeight,
                CopyMoveWeight = CopyMoveWeight,
                SplicingWeight = SplicingWeight,
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
