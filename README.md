# ImageForensics

## Introduzione e panoramica
ImageForensics è una raccolta di strumenti per l'analisi di immagini e il rilevamento di possibili manipolazioni. La pipeline combina più controlli indipendenti che producono mappe di calore, maschere e punteggi normalizzati. Questi valori vengono poi aggregati da un motore decisionale che stabilisce se una foto è **Clean**, **Suspicious** oppure **Tampered**.

```
 +-----------+
 |Preprocess |
 +-----------+
       |
 +-----------+
 |    ELA    |
 +-----------+
       |
 +-----------+
 | Copy-Move |
 +-----------+
       |
 +-----------+
 | Splicing  |
 +-----------+
       |
 +-----------+
 |Inpainting |
 +-----------+
       |
 +-----------+
 |   EXIF    |
 +-----------+
       |
 +-----------+
 | Decision  |
 |  Engine   |
 +-----------+
```

## Requisiti e installazione
- SDK **.NET 9**. Nel repository è presente uno script per installarlo:
  ```bash
  ./dotnet-install.sh --version 9.0.100 --install-dir "$HOME/dotnet"
  export PATH="$HOME/dotnet:$PATH"
  ```
- La libreria nativa **libOpenCvSharpExtern.so** è fornita già compilata in `so/` (Ubuntu&nbsp;24.04). Per eseguire i test è necessario che sia raggiungibile tramite `LD_LIBRARY_PATH`:
  ```bash
  export LD_LIBRARY_PATH="$PWD/so:$LD_LIBRARY_PATH"
  ```
  Se serve ricompilarla eseguire `scripts-build-opencvsharp.sh` e copiare il file prodotto in `so/` insieme alle eventuali dipendenze di `ldd`.
- I sistemi appena installati potrebbero richiedere alcuni pacchetti aggiuntivi:
  ```bash
  sudo apt-get update
  sudo apt-get install -y libgtk2.0-0 libgdk-pixbuf2.0-0 libtesseract5 libdc1394-25 libavcodec60 libavformat60 libswscale7 libsm6 libxext6 libxrender1 libgomp1
  ```
- Clonare il repository e scaricare manualmente i modelli ONNX necessari (ManTraNet e Noiseprint) con lo script:
  ```bash
  tools/download_models.sh
  ```
  I modelli vengono salvati in `ImageForensics/src/Models/onnx`.

## Struttura del progetto
- `ImageForensics/src` contiene le librerie e la CLI principale.
- `ImageForensics/tests` include i test unitari e di integrazione con i relativi `testdata`.
- `tools` ospita script di utilità (download modelli, conversione ManTraNet).
- `Models/onnx` è la cartella dove scaricare ManTraNet e i modelli Noiseprint.
- `dataset` raccoglie alcune immagini di esempio (CASIA2) usate nei benchmark.

## Struttura del dataset
Il dataset integrato nel repository è suddiviso in cartelle che riflettono le diverse tipologie di immagini usate nei test:

```
dataset/
├─ authentic/           # Immagini originali "real" pulite (nessuna manipolazione)
├─ clean/               # Varianti di immagini pulite usate per controlli "clean"
├─ deepfake/
│   ├─ real/            # Frame "veri" estratti da video originali
│   └─ fake/            # Frame "deepfake" estratti da video manipolati
├─ duplicated/          # Immagini con copy–move forgery (regioni duplicate)
├─ exif/
│   ├─ original/        # JPEG con EXIF integro (DateTimeOriginal, Make/Model, Software)
│   └─ exif_edited/     # Stesse immagini EXIF‑edited (tag rimossi o modificati)
└─ tampered/            # Immagini generiche "tampered" non classificate altrove
```

* `authentic/` raccoglie un sottoinsieme di foto certificate, utili per verificare i falsi positivi dei vari moduli.
* `clean/` contiene immagini non manipolate usate come baseline sia per ELA sia per Noiseprint.
* `deepfake/real` e `deepfake/fake` ospitano rispettivamente i frame genuini e quelli manipolati di video deepfake (Face2Face, NeuralTextures ecc.).
* `duplicated/` include copie con aree duplicate (copy‑move) e le relative maschere di ground truth.
* `exif/original` raggruppa JPEG con metadati integri, mentre `exif/exif_edited` offre le stesse immagini con tag rimossi o alterati.
* `tampered/` raccoglie immagini manomesse di vario tipo (splicing, inpainting o altre tecniche).

Questa organizzazione permette di esercitare separatamente i moduli ELA, Copy‑Move, Splicing, Deepfake/Inpainting e Metadata, oltre a comporre test di integrazione combinati.

## Modalità d'uso CLI
Per analizzare una singola immagine:
```bash
dotnet run --project ImageForensics/src/ImageForensics.Cli -- <image> [--workdir DIR]
```
Tutti i file generati (mappe, maschere, report) vengono salvati in `DIR` (default `results`).

### Benchmark
La CLI dispone di varie opzioni per misurare le prestazioni:
- `--benchmark-ela`
- `--benchmark-copy-move`
- `--benchmark-splicing`
- `--benchmark-inpainting`
- `--benchmark-exif`
- `--benchmark-all`
- `--input-dir <folder>` cartella con le immagini da processare
- `--report-dir <folder>` destinazione dei report CSV/JSON

Esempi:
```bash
dotnet run --project ImageForensics/src/ImageForensics.Cli -- --benchmark-ela --input-dir dataset/authentic --report-dir bench
dotnet run --project ImageForensics/src/ImageForensics.Cli -- --benchmark-all --input-dir dataset/casia2 --report-dir bench
```

### Parametri principali
`ForensicsOptions` espone diversi parametri regolabili:

| Property | Description | Default |
|--------------------------|----------------------------------------------------------|--------:|
| `ElaQuality` | JPEG quality used when recompressing the image for ELA | `75` |
| `WorkDir` | Directory where the ELA map is saved | `results` |
| `CopyMoveFeatureCount` | Number of SIFT features extracted for copy‑move detection | `5000` |
| `CopyMoveMatchDistance` | Maximum descriptor distance for a valid match | `3.0` |
| `CopyMoveRansacReproj` | RANSAC reprojection threshold in pixels | `3.0` |
| `CopyMoveRansacProb` | Desired RANSAC success probability | `0.99` |
| `CopyMoveMaskDir` | Directory where the copy‑move mask is written | `results` |
| `NoiseprintModelsDir` | Directory containing Noiseprint ONNX models | `ImageForensics/src/Models/onnx/noiseprint` |
| `NoiseprintInputSize` | Resize size fed into the Noiseprint model | `320` |
| `NoiseprintMapDir` | Directory where the Noiseprint heat-map is saved | `results` |
| `ElaWeight` | Weight of the ELA score in the final decision | `1.0` |
| `CopyMoveWeight` | Weight of the copy‑move score | `1.0` |
| `SplicingWeight` | Weight of the splicing score | `1.0` |
| `InpaintingWeight` | Weight of the inpainting score | `1.0` |
| `ExifWeight` | Weight of the EXIF score | `1.0` |
| `CleanThreshold` | Score below this value is considered `Clean` | `0.2` |
| `TamperedThreshold` | Score above this value is considered `Tampered` | `0.8` |

`ForensicsResult` restituisce i valori seguenti:

| Field | Meaning |
|--------------------|------------------------------------------------------------|
| `ElaScore` | Normalised error level analysis score |
| `ElaMapPath` | Path of the PNG heat‑map |
| `CopyMoveScore` | Ratio of matched keypoints consistent with a geometric transform |
| `CopyMoveMaskPath` | Path of the copy‑move mask |
| `InpaintingScore` | Mean value of the Noiseprint heat‑map |
| `InpaintingMapPath` | Path of the Noiseprint heat‑map |
| `Verdict` | Final decision after aggregating all detectors |
| `TotalScore` | Weighted sum of all individual scores |

## Descrizione dei moduli
### Error‑Level Analysis
Ricomprime l'immagine con qualità JPEG inferiore e confronta i due file. La mappa generata mette in evidenza zone con errori di compressione anomali. Il punteggio è la media normalizzata dei valori della heat‑map.

### Copy‑Move Detection
Usa SIFT e RANSAC per individuare regioni duplicate all'interno della stessa immagine. Produce una maschera PNG con le aree sospette. Lo score è dato dal rapporto tra i match coerenti e il totale delle feature.

### Splicing Detection
Basato sul modello ONNX **ManTraNet** eseguito con ONNX Runtime. Richiede `mantranet_256x256.onnx` sotto `Models/onnx`. Restituisce una heat‑map e uno score compreso tra 0 e 1.

### Inpainting / Deepfake Detection
Utilizza i modelli Noiseprint specifici per ogni fattore di qualità JPEG (`model_qfXX.onnx`) presenti in `noiseprint`. Il modulo stima la qualità con `JpegQualityEstimator` e carica il modello corrispondente (per PNG usa `model_qf101.onnx`). L'immagine viene convertita in scala di grigi, ridimensionata a `NoiseprintInputSize` (320 di default) e analizzata. La heat-map risultante evidenzia le zone potenzialmente inpainted; lo score è la media normalizzata di tale mappa.

### Metadata Checker
Utilizza la libreria `MetadataExtractor` per analizzare EXIF/XMP/IPTC. Vengono controllati `DateTimeOriginal`, `Software`, `Make/Model` e i tag GPS. Ogni anomalia incrementa lo score (range 0‑1). Se `MetadataMapDir` è impostato viene scritto un report JSON con tutti i tag.

## Decision Engine
I punteggi dei moduli sono combinati tramite pesi configurabili:
```text
score = ElaScore * ElaWeight +
        CopyMoveScore * CopyMoveWeight +
        SplicingScore * SplicingWeight +
        InpaintingScore * InpaintingWeight +
        ExifScore * ExifWeight
```
Il risultato è **Clean** se al di sotto di `CleanThreshold`, **Suspicious** fra le due soglie e **Tampered** oltre `TamperedThreshold`.

Esempio di output JSON:
```json
{
  "ElaScore": 0.010,
  "ElaMapPath": "results/photo_ela.png",
  "CopyMoveScore": 0.000,
  "CopyMoveMaskPath": "results/photo_copymove.png",
  "SplicingScore": 0.042,
  "SplicingMapPath": "results/photo_splicing.png",
  "InpaintingScore": 0.015,
  "InpaintingMapPath": "results/photo_inpainting.png",
  "ExifScore": 0.000,
  "TotalScore": 0.067,
  "Verdict": "Clean"
}
```

## Test e qualità
I test unitari e di integrazione si trovano in `ImageForensics/tests`. Eseguire:
```bash
dotnet test ImageForensics/tests/ImageForensics.Tests/ImageForensics.Tests.csproj
```
Il progetto `TestOpenCvSharp` verifica il corretto caricamento della libreria nativa:
```bash
dotnet test TestOpenCvSharp/TestOpenCvSharp.csproj -v n
```
I dataset di riferimento (CASIA2) sono collocati in `dataset/authentic` e `dataset/tampered`; altri file come `clean.png` e `inpainting.png` risiedono in `tests/ImageForensics.Tests/testdata`.


Ultima esecuzione test: **2025-07-28** – 46 test completati con successo (incluso il dataset duplicato).

### Riepilogo test

| Test                                   | Controlli verificati |
|----------------------------------------|----------------------|
| `ElaTests`                             | punteggi e mappe ELA |
| `CopyMoveTests`                        | generazione maschera copy‑move |
| `DlSplicingTests`                      | esecuzione ManTraNet su CASIA2 |
| `NoiseprintTests`                      | heat‑map e soglie Noiseprint |
| `ExifTests` / `DatasetExifTests`       | anomalie EXIF e punteggi previsti |
| `DatasetTests`                         | tempi medi dell'intera pipeline |
| `DecisionEngineTests`                  | logica di aggregazione e verdetti |
| `CliBenchmarkTests`                    | generazione dei report CSV/JSON |
| `JpegQualityEstimatorTests`            | stima qualità JPEG |
| `NoiseprintModelSelectionTests`        | scelta del modello Noiseprint corretto |
| `UnitTest1`                            | test segnaposto |

## Benchmark & performance
La modalità benchmark genera file CSV e JSON con i tempi medi e la deviazione standard per ogni modulo. Di seguito sono riportati i tempi di risposta misurati sui dataset `authentic` e `tampered`.

### Tempi per singolo modulo

| Modulo     | Authentic (ms) | Tampered (ms) |
|------------|---------------:|--------------:|
| ELA        | 80 ± 5 | 122 ± 55 |
| Copy‑Move  | 33 ± 15 | 62 ± 31 |
| Splicing   | 2129 ± 47 | 2170 ± 77 |
| Inpainting | 430 ± 35 | 411 ± 33 |
| EXIF       | 14 ± 31 | 22 ± 51 |
*Nota:* una precedente versione del benchmark riportava tempi medi di 413&nbsp;ms per il modulo Noiseprint su immagini autentiche e 396&nbsp;ms su immagini manomesse.

### Benchmark Noiseprint
Tempo medio (build *Release*) per le immagini del dataset:

| Categoria  | ms medi |
|-----------|-------:|
| Autentiche | 413 |
| Manomesse | 396 |

La tabella seguente mostra i tempi di elaborazione ottenuti eseguendo solo il modulo Noiseprint su ogni immagine di test.

#### Autentiche
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

#### Manomesse
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

### Benchmark ELA
Tempo medio di elaborazione per il dataset (build *Release*):

| Categoria  | ms medi |
|-----------|-------:|
| Autentiche | 76 |
| Manomesse | 113 |

### Tempi di esecuzione complessivi
I dati seguenti provengono dal test `DatasetTests.Measure_Processing_Times` e mostrano i tempi medi per l'intera pipeline (ms).

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

### Copy‑Move detection (dataset `duplicated`)
Risultati per il dataset di immagini con regioni duplicate. Tutte le foto sono
contrassegnate come tampered.

| Image | Score | ms |
|-------|------:|---:|
| 002_F_JC8.jpg | 0.000 | 3348 |
| 001_O_NA2.png | 0.000 | 2891 |
| 002_B.png | 0.000 | 2674 |
| 002_F_JC3.jpg | 0.000 | 2725 |
| 001_O_JC6.jpg | 0.000 | 2836 |
| 002_F_CA1.png | 0.000 | 2897 |
| 002_F_IB2.png | 0.000 | 2703 |
| 001_O_NA3.png | 0.000 | 2921 |
| 002_F_IB3.png | 0.000 | 2681 |
| 001_O_JC7.jpg | 0.000 | 2590 |
| 002_F_JC7.jpg | 0.000 | 2720 |
| 002_F_CR2.png | 0.000 | 2924 |
| 002_F_JC5.jpg | 0.000 | 2681 |
| 001_O_NA1.png | 0.000 | 2794 |
| 002_F_BC2.png | 0.000 | 2982 |
| 002_F_CA2.png | 0.000 | 3034 |
| 002_F_JC6.jpg | 0.000 | 2677 |
| 002_F_CA3.png | 0.000 | 3038 |
| 002_F_JC1.jpg | 0.000 | 2801 |
| 002_F_CR1.png | 0.000 | 2909 |
| 001_O.png | 0.000 | 2925 |
| 002_F_BC1.png | 0.000 | 2782 |
| 002_F_JC4.jpg | 0.000 | 2743 |
| 001_O_JC5.jpg | 0.000 | 2828 |
| 002_F_CR3.png | 0.000 | 2897 |
| 001_O_JC8.jpg | 0.000 | 2757 |
| 002_F_BC3.png | 0.000 | 2753 |
| 002_F_JC2.jpg | 0.000 | 2589 |
| 002_F_IB1.png | 0.000 | 2807 |
| 001_O_JC9.jpg | 0.000 | 2964 |
| 001_O_JC9.jpg | 0.000 | 2964 |

### Deepfake detection (dataset `deepfake`)
Risultati su un sottoinsieme di frame reali e falsi. Tutti i file
sono processati con il modulo Noiseprint.

| Tipo | Image | Score | ms |
|------|-------|------:|---:|
| real | real_20.jpg | 0.238 | 8344 |
| real | real_0.jpg  | 0.240 | 6187 |
| real | real_6.jpg  | 0.239 | 5797 |
| fake | fake_7.jpg  | 0.239 | 5761 |
| fake | fake_1.jpg  | 0.239 | 6000 |
| fake | fake_20.jpg | 0.239 | 6759 |
| **Average real** |  | **0.239** | **6776** |
| **Average fake** |  | **0.239** | **6173** |

### Metadata Checker (dataset `exif`)
Risultati su un sottoinsieme di immagini con metadati integri e manipolati.

| Cartella | Image | ms | Anomalie rilevate | Esito |
|----------|-------|---:|------------------|:----:|
| original | 156065.jpg | 2410 | DateTimeOriginal, Model | ✓ |
| original | 157055.jpg | 2310 | DateTimeOriginal, Model | ✓ |
| original | 159008.jpg | 2379 | DateTimeOriginal, Model | ✓ |
| **Media original** | | **2366** | | |
| exif_edited | 156065.jpg | 2368 | Model, GPS | ✓ |
| exif_edited | 157055.jpg | 2532 | Model, GPS | ✓ |
| exif_edited | 159008.jpg | 2590 | Model, GPS | ✓ |
| **Media edited** | | **2497** | | |
## Contributi e licenze
Le librerie utilizzate sono soggette alle rispettive licenze open source:
- **OpenCvSharp** (BSD-3-Clause)
- **ONNX Runtime** (MIT)
- **MetadataExtractor** (Apache-2.0)
- **Noiseprint SDK** (MIT)

Il codice del repository è rilasciato con licenza MIT. Contributi e segnalazioni sono benvenuti tramite pull request.
