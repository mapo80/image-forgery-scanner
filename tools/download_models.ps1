<#
.SYNOPSIS
Scarica i modelli ONNX necessari alla pipeline e ne verifica l’hash SHA-256.
#>

param()

function Get-Model {
    param(
        [string]$Url,
        [string]$Path,
        [string]$Sha256
    )
    if (Test-Path $Path) {
        $hash = (Get-FileHash $Path -Algorithm SHA256).Hash.ToLower()
        if ($hash -eq $Sha256.ToLower()) { Write-Host "✓ $($Path) ok"; return }
        else  { Write-Warning "Hash mismatch → riscarico" }
    }
    Write-Host "↓ Downloading $Url"
    Invoke-WebRequest -Uri $Url -OutFile $Path
    $hash = (Get-FileHash $Path -Algorithm SHA256).Hash.ToLower()
    if ($hash -ne $Sha256.ToLower()) {
        Write-Error "Hash verification failed for $Path"
        exit 1
    }
    Write-Host "✓ Saved $Path"
}

$models = @(
    @{ Url = "https://huggingface.co/spaces/akhaliq/ManTraNet/resolve/main/ManTraNet_256x256.onnx";
       Path = "src/Models/onnx/mantranet_256x256.onnx";
       Sha  = "a0f6a5e231fb9c255df6340ee2efdc4c93eae243c64c48ba74392f7c5d6d4c6e" },
    @{ Url = "https://github.com/ZhendongWang6/CMFDFormer/releases/download/v1.0/cmfdformer_base.onnx";
       Path = "src/Models/onnx/cmfdformer_base.onnx";
       Sha  = "3c5743acb4507c43f9a5d6b6dffe9d399e1cbb3bf6aaa1fe6a74b1e48dce07d5" },
    @{ Url = "https://github.com/grip-unina/noiseprint/releases/download/v1.0/noiseprint_spp.onnx";
       Path = "src/Models/onnx/noiseprint_spp.onnx";
       Sha  = "5e1312ed7d2e5ffa9b37d348862d97911fa2050c3df91d5a4823bbf9e82431ab" }
)

foreach ($m in $models) { Get-Model @m }

Write-Host "Models ready."
