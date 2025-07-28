# image-forgery-scanner

Per compilare o eseguire i test è necessario installare l’SDK .NET 9 incluso nel repository. Eseguire i comandi seguenti e assicurarsi che siano presenti nel proprio PATH:

./dotnet-install.sh --version 9.0.100 --install-dir "$HOME/dotnet"
export PATH="$HOME/dotnet:$PATH"
## Building libOpenCvSharpExtern.so

Run the helper script to compile OpenCV from source and generate the native wrapper library (this may take a long time):

```bash
./scripts-build-opencvsharp.sh
```

At the end of the build the file `libOpenCvSharpExtern.so` will be created in the repository root.
Copy this file together with any additional shared libraries reported by `ldd libOpenCvSharpExtern.so` into a directory listed in your `LD_LIBRARY_PATH`.

## Test project

A sample console project is available in `TestOpenCvSharp`. After building the extern library, run:

```bash
cd TestOpenCvSharp
../dotnet-install.sh --version 9.0.100 --install-dir "$HOME/dotnet"
export PATH="$HOME/dotnet:$PATH"
dotnet run
```

## Dataset

The sample images used for testing are from the [CASIA 2.0 Image Tampering Detection Dataset](https://www.kaggle.com/datasets/divg07/casia-20-image-tampering-detection-dataset?resource=download).
They are organized in `dataset/authentic` and `dataset/tampered`.

Download the required ONNX models by running `tools/download_models.sh`.

## Command line usage

Run the CLI by specifying an image path and an optional working directory where
the analysis artefacts will be written:

```bash
dotnet run --project ImageForensics/src/ImageForensics.Cli -- <image> [--workdir DIR]
```

The application prints several metrics and saves diagnostic images in `DIR`
(default `results`).

### Input parameters

`ForensicsOptions` exposes the following tunables:

| Property                 | Description                                               | Default |
|--------------------------|-----------------------------------------------------------|--------:|
| `ElaQuality`             | JPEG quality used when recompressing the image for ELA    | `75`    |
| `WorkDir`                | Directory where the ELA map is saved                      | `results` |
| `CopyMoveFeatureCount`   | Number of SIFT features extracted for copy‑move detection | `5000`  |
| `CopyMoveMatchDistance`  | Maximum descriptor distance for a valid match             | `3.0`   |
| `CopyMoveRansacReproj`   | RANSAC reprojection threshold in pixels                   | `3.0`   |
| `CopyMoveRansacProb`     | Desired RANSAC success probability                        | `0.99`  |
| `CopyMoveMaskDir`        | Directory where the copy‑move mask is written             | `results` |
| `NoiseprintModelsDir`    | Directory containing Noiseprint ONNX models | `ImageForensics/src/Models/onnx/noiseprint` |
| `NoiseprintInputSize`    | Resize size fed into the Noiseprint model | `320` |
| `NoiseprintMapDir`       | Directory where the Noiseprint heat-map is saved   | `results` |
| `ElaWeight`              | Weight of the ELA score in the final decision | `1.0` |
| `CopyMoveWeight`         | Weight of the copy‑move score | `1.0` |
| `SplicingWeight`         | Weight of the splicing score | `1.0` |
| `InpaintingWeight`       | Weight of the inpainting score | `1.0` |
| `ExifWeight`             | Weight of the EXIF score | `1.0` |
| `CleanThreshold`         | Score below this value is considered `Clean` | `0.2` |
| `TamperedThreshold`      | Score above this value is considered `Tampered` | `0.8` |

### Output fields

`ForensicsResult` contains:

| Field              | Meaning                                                                              |
|--------------------|--------------------------------------------------------------------------------------|
| `ElaScore`         | Normalised error level analysis score. Low values indicate likely authentic images. |
| `ElaMapPath`       | Path of the PNG heat‑map highlighting compression artefacts.                         |
| `Verdict`          | Final decision after aggregating all detectors.                                     |
| `TotalScore`       | Weighted sum of all individual scores.                                              |
| `CopyMoveScore`    | Ratio of matched keypoints consistent with a geometric transform.                   |
| `CopyMoveMaskPath` | Path of the mask image showing detected copy‑move regions.                           |
| `InpaintingScore`  | Mean value of the Noiseprint heat-map in [0‑1].                                    |
| `InpaintingMapPath`| Path of the Noiseprint heat-map highlighting inpainted areas.                      |

## Splicing Detection

This module relies on the neural network `ManTraNet_256x256.onnx` (ManTraNet, CVPR 2019) to detect
local splicing artefacts. The model is bundled under `ImageForensics/src/Models/onnx` and is
originally exported from [mapo80/ManTraNet](https://github.com/mapo80/ManTraNet).

To obtain it run the helper script which downloads the pretrained Keras weights and converts them to ONNX:

```bash
python tools/export_mantranet_onnx.py
```

This recreates `src/Models/onnx/mantranet_256x256.onnx` if needed.

### `AnalyzeSplicing` inputs

- `string imagePath` – path of the input image.
- `string mapDir` – directory where the heat‑map will be saved.
- `string modelPath` – location of the ONNX model.
- `int inputW`, `int inputH` – resize width and height.

### Outputs

- `double Score` – normalised tampering score in the range [0–1].
- `string MapPath` – path of the generated PNG heat‑map.

Copy the CASIA dataset under
`tests/ImageForensics.Tests/testdata/casia2/authentic` and
`tests/ImageForensics.Tests/testdata/casia2/tampered`.

Run a benchmark with:

```bash
dotnet run --project ImageForensics/src/ImageForensics.Cli -- --benchmark --benchdir <folder>
```

## Noiseprint Inpainting Detection

This feature relies on the Noiseprint ONNX models from the
[mapo80/noiseprint-pytorch](https://github.com/mapo80/noiseprint-pytorch) project.
Download them with `tools/download_models.sh` and place them under
`ImageForensics/src/Models/onnx/noiseprint`.
Add test images (e.g. `clean.png` and `inpainting.png`) to
`tests/ImageForensics.Tests/testdata`.

Run a benchmark with:

```bash
dotnet run --project ImageForensics/src/ImageForensics.Cli -- --benchmark-inpainting --benchdir <folder>
```

## Noiseprint benchmark

Average processing time for the dataset images (release build):

| Category   | Avg ms |
|------------|-------:|
| Authentic  | 413 |
| Tampered   | 396 |

The following tables report the processing time for each image when running only the Noiseprint check (milliseconds on a typical container). The last row shows the category average.

### Authentic

| Image | ms |
|-------|---:|
| Au_ani_00001.jpg | 415 |
| Au_ani_00002.jpg | 375 |
| Au_ani_00003.jpg | 373 |
| Au_ani_00004.jpg | 420 |
| Au_ani_00005.jpg | 368 |
| Au_ani_00006.jpg | 482 |
| Au_ani_00007.jpg | 527 |
| Au_ani_00008.jpg | 388 |
| Au_ani_00009.jpg | 379 |
| Au_ani_00010.jpg | 400 |
| **Average** | **413** |

### Tampered

| Image | ms |
|-------|---:|
| Tp_D_CND_S_N_txt00028_txt00006_10848.jpg | 392 |
| Tp_D_CNN_M_B_nat00056_nat00099_11105.jpg | 393 |
| Tp_D_CNN_M_B_nat10139_nat00059_11949.jpg | 421 |
| Tp_D_CNN_M_B_nat10139_nat00097_11948.jpg | 411 |
| Tp_D_CNN_M_N_ani00052_ani00054_11130.jpg | 369 |
| Tp_D_CNN_M_N_ani00057_ani00055_11149.jpg | 394 |
| Tp_D_CNN_M_N_art00052_arc00030_11853.jpg | 383 |
| Tp_D_CNN_M_N_cha00026_cha00028_11784.jpg | 407 |
| Tp_D_CNN_M_N_ind00091_ind00091_10647.jpg | 403 |
| Tp_D_CNN_M_N_ind00091_ind00091_10648.jpg | 383 |
| **Average** | **396** |

## ELA benchmark

Average processing time for the dataset images (release build):

| Category   | Avg ms |
|------------|-------:|
| Authentic  | 76 |
| Tampered   | 113 |

## Execution times

The following table reports the processing time for each image in the demo
dataset (milliseconds on a typical container). The last row shows the category
average.

The times were recorded by the `DatasetTests.Measure_Processing_Times` test. Each
file was processed once with every available detector. On the reference machine
an authentic photo takes about **3.5&nbsp;s**, while tampered images average
slightly above **3.5&nbsp;s**.

### Authentic

| Image | ms |
|-------|---:|
| Au_ani_00001.jpg | 3347 |
| Au_ani_00002.jpg | 3636 |
| Au_ani_00003.jpg | 3481 |
| Au_ani_00004.jpg | 3668 |
| Au_ani_00005.jpg | 3304 |
| Au_ani_00006.jpg | 3400 |
| Au_ani_00007.jpg | 3318 |
| Au_ani_00008.jpg | 3428 |
| Au_ani_00009.jpg | 3563 |
| Au_ani_00010.jpg | 3428 |
| **Average** | **3457** |

### Tampered

| Image | ms |
|-------|---:|
| Tp_D_CND_S_N_txt00028_txt00006_10848.jpg | 3516 |
| Tp_D_CNN_M_B_nat00056_nat00099_11105.jpg | 3623 |
| Tp_D_CNN_M_B_nat10139_nat00059_11949.jpg | 3778 |
| Tp_D_CNN_M_B_nat10139_nat00097_11948.jpg | 3790 |
| Tp_D_CNN_M_N_ani00052_ani00054_11130.jpg | 3461 |
| Tp_D_CNN_M_N_ani00057_ani00055_11149.jpg | 3440 |
| Tp_D_CNN_M_N_art00052_arc00030_11853.jpg | 3397 |
| Tp_D_CNN_M_N_cha00026_cha00028_11784.jpg | 3336 |
| Tp_D_CNN_M_N_ind00091_ind00091_10647.jpg | 3429 |
| Tp_D_CNN_M_N_ind00091_ind00091_10648.jpg | 3361 |
| **Average** | **3513** |
## Metadata Checking

The EXIF/XMP/IPTC metadata of input images are analysed using the [MetadataExtractor](https://github.com/drewnoakes/metadata-extractor-dotnet) library. The analyzer verifies `DateTimeOriginal`, `Software`, `Make/Model` and GPS tags. Anomalies such as missing or future timestamps, editing software strings, and unexpected camera models contribute to an `ExifScore` in the range [0‑1].

Internally `ExifChecker` loads all metadata directories and checks the four tags above. The score is computed as `anomalyCount / 4` and clamped to the unit interval. When `MetadataMapDir` is set, a JSON file is written with every parsed tag and the detected anomalies. This allows manual inspection of the metadata used for scoring.

All parsed tags together with any detected anomalies can be written to `ForensicsOptions.MetadataMapDir` as a JSON report. Test images for this feature must be placed in `tests/ImageForensics.Tests/testdata`. Reports are generated in the same folder used for the other analysis maps.

## Decision Engine

The decision engine combines all detector scores using configurable weights and computes an aggregated value:

```
score = ElaScore * ElaWeight +
        CopyMoveScore * CopyMoveWeight +
        SplicingScore * SplicingWeight +
        InpaintingScore * InpaintingWeight +
        ExifScore * ExifWeight
```

If the score is below `CleanThreshold` the image is **Clean**. Between the two thresholds the result is **Suspicious**. A score above `TamperedThreshold` produces a **Tampered** verdict.

Example output:

```
ELA score : 0.010
Verdict   : Clean
Heat-map  : results/photo_ela.png
CopyMove score : 0.000
CopyMove mask  : results/photo_copymove.png
Splicing score : 0.042
Splicing map   : results/photo_splicing.png
Inpainting score : 0.015
Inpainting map   : results/photo_inpainting.png
Total score   : 0.067
Final verdict : Clean
```

