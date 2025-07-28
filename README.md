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

### Output fields

`ForensicsResult` contains:

| Field              | Meaning                                                                              |
|--------------------|--------------------------------------------------------------------------------------|
| `ElaScore`         | Normalised error level analysis score. Low values indicate likely authentic images. |
| `ElaMapPath`       | Path of the PNG heat‑map highlighting compression artefacts.                         |
| `Verdict`          | Quick qualitative judgement based solely on `ElaScore`.                              |
| `CopyMoveScore`    | Ratio of matched keypoints consistent with a geometric transform.                   |
| `CopyMoveMaskPath` | Path of the mask image showing detected copy‑move regions.                           |

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
