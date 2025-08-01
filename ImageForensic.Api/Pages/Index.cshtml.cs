using System;
using System.IO;
using System.Threading.Tasks;
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
    public string SplicingMapBase64 { get; private set; } = string.Empty;
    public string InpaintingMapBase64 { get; private set; } = string.Empty;

    public double DefaultCleanThreshold => new AnalyzeImageOptionsForm().CleanThreshold;
    public double DefaultTamperedThreshold => new AnalyzeImageOptionsForm().TamperedThreshold;

    public string SpiegaPunteggio(double punteggio)
    {
        if (punteggio < Options.CleanThreshold)
            return $"Inferiore alla soglia di pulizia ({Options.CleanThreshold:F2}): il controllo non evidenzia manipolazioni.";
        if (punteggio > Options.TamperedThreshold)
            return $"Superiore alla soglia di manomissione ({Options.TamperedThreshold:F2}): il controllo suggerisce manomissioni.";
        return $"Tra le soglie ({Options.CleanThreshold:F2}-{Options.TamperedThreshold:F2}): il risultato è incerto.";
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
        [Display(Name = "Qualità ELA")]
        public int ElaQuality { get; set; } = 75;

        [Display(Name = "Numero caratteristiche copia e sposta")]
        public int CopyMoveFeatureCount { get; set; } = 5000;

        [Display(Name = "Distanza corrispondenza copia e sposta")]
        public double CopyMoveMatchDistance { get; set; } = 3.0;

        [Display(Name = "RANSAC reproiezione copia e sposta")]
        public double CopyMoveRansacReproj { get; set; } = 3.0;

        [Display(Name = "Probabilità RANSAC copia e sposta")]
        public double CopyMoveRansacProb { get; set; } = 0.99;

        [Display(Name = "Larghezza input giunzione")]
        public int SplicingInputWidth { get; set; } = 256;

        [Display(Name = "Altezza input giunzione")]
        public int SplicingInputHeight { get; set; } = 256;

        [Display(Name = "Dimensione input rumore")]
        public int NoiseprintInputSize { get; set; } = 320;

        [Display(Name = "Modelli fotocamera attesi")]
        public string ExpectedCameraModels { get; set; } = "Canon EOS 80D,Nikon D850";

        [Display(Name = "Peso ELA")]
        public double ElaWeight { get; set; } = 1.0;

        [Display(Name = "Peso copia e sposta")]
        public double CopyMoveWeight { get; set; } = 1.0;

        [Display(Name = "Peso giunzione")]
        public double SplicingWeight { get; set; } = 1.0;

        [Display(Name = "Peso riempimento")]
        public double InpaintingWeight { get; set; } = 1.0;

        [Display(Name = "Peso EXIF")]
        public double ExifWeight { get; set; } = 1.0;

        [Display(Name = "Soglia pulita")]
        public double CleanThreshold { get; set; } = 0.2;

        [Display(Name = "Soglia manomessa")]
        public double TamperedThreshold { get; set; } = 0.8;

        [Display(Name = "Controlli abilitati")]
        public string EnabledChecks { get; set; } = "Ela,CopyMove,Splicing,Inpainting,Exif";

        [Display(Name = "Numero massimo controlli paralleli")]
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
