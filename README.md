# ImageForensics

## Introduzione e panoramica
ImageForensics è una raccolta di strumenti per l'analisi di immagini e il rilevamento di possibili manipolazioni. La pipeline combina più controlli indipendenti che producono mappe di calore, maschere e punteggi normalizzati. Questi valori vengono poi aggregati da un motore decisionale che stabilisce se una foto è **Clean**, **Suspicious** oppure **Tampered**.

```
 +-----------+
 |Preprocess |
 +-----------+
       |
 +-----------+  +-----------+  +-----------+  +-----------+  +-----------+
 |    ELA    |  | Copy-Move |  | Splicing  |  |Inpainting |  |   EXIF    |
 +-----------+  +-----------+  +-----------+  +-----------+  +-----------+
       \\            |             |             |            //
        \\           |             |             |           //
         +-----------------------------------------------+
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
dotnet run --project ImageForensics/src/ImageForensics.Cli -- <image> [--workdir DIR] [--checks LIST] [--parallel N]
```
`LIST` è un elenco separato da virgole tra `ela`, `copymove`, `splicing`, `inpainting`, `exif`.
`N` limita il numero di controlli eseguiti in parallelo.
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

## Ottimizzazione Error-Level Analysis
Misurati i tempi del modulo ELA su 10 immagini di `dataset/tampered`:

| Versione | Tempo medio (ms) |
|----------|-----------------:|
| Prima ottimizzazione | 188 ± 106 |
| Dopo ottimizzazione  | 185 ± 109 |

L'eliminazione del `MemoryStream` a favore di una codifica in memoria riduce leggermente il tempo medio di analisi di circa l'1 %.

## Ottimizzazione Copy‑Move
Misurati i tempi del modulo di copy‑move su 10 immagini di `dataset/tampered`:

| Versione | Tempo medio (ms) |
|----------|-----------------:|
| Prima ottimizzazione | 115 ± 79 |
| Dopo ottimizzazione  | 112 ± 77 |

L'uso di un matcher FLANN e il riutilizzo dell'estrattore SIFT riducono il tempo medio di analisi di circa il 3 %.

## Ottimizzazione Splicing
Misurati i tempi del modulo di splicing su 10 immagini di `dataset/tampered`:

| Versione | Tempo medio (ms) |
|----------|-----------------:|
| Prima ottimizzazione | 2463 ± 353 |
| Dopo ottimizzazione  | 2120 ± 699 |

La cache dell'`InferenceSession` riduce il tempo medio di analisi di circa il 14 %.

## Ottimizzazione preprocessing Splicing
Misurati i tempi del modulo di splicing con preprocessing ottimizzato su 10 immagini di `dataset/tampered`:

| Versione | Tempo medio (ms) |
|----------|-----------------:|
| Prima ottimizzazione | 2137 ± 349 |
| Dopo ottimizzazione  | 2122 ± 278 |

La rimozione della copia intermedia e il ridimensionamento in-place riducono il tempo medio di analisi di circa l'1 %.

## Confronto sequenziale/parallelo Splicing
Analizzati 20 file (10 da `dataset/authentic` e 10 da `dataset/tampered`) confrontando l'esecuzione sequenziale con quella paralella su 4 thread. La parallelizzazione non riduce il tempo di elaborazione di ogni immagine, che anzi cresce a causa della contesa delle risorse, ma abbatte il tempo totale portando un miglior throughput complessivo.

| Modalità           | Immagini | Tempo totale (ms) | Tempo medio osservato (ms) | Deviazione standard (ms) | Tempo medio effettivo (ms) | Speedup |
|--------------------|---------:|------------------:|---------------------------:|-------------------------:|---------------------------:|-------:|
| Sequenziale        | 20       | 89497             | 4471                       | 648                     | 4475                       | 1.00× |
| Parallelo (4 thread)| 20      | 62613             | 12359                      | 2343                    | 3131                       | 1.43× |

L'esecuzione parallela riduce il tempo totale di circa il 30 % rispetto a quella sequenziale.

## Ottimizzazione Inpainting
Misurati i tempi del modulo Noiseprint su 10 immagini di `dataset/tampered`:

| Versione | Tempo medio (ms) |
|----------|-----------------:|
| Prima ottimizzazione | 768 ± 339 |
| Dopo ottimizzazione  | 638 ± 145 |

La cache di più modelli e la lettura diretta in scala di grigi riducono il tempo medio di analisi di circa il 17 %.

## Ottimizzazione Metadata
Misurati i tempi del modulo EXIF su 10 immagini di `dataset/tampered`:

| Versione | Tempo medio (ms) |
|----------|-----------------:|
| Prima ottimizzazione | 26 ± 74 |
| Dopo ottimizzazione  | 24 ± 69 |

La lettura delle directory di metadati in un'unica passata e l'uso di un `HashSet` per i modelli attesi eliminano enumerazioni ripetute, riducendo il tempo medio di analisi di circa l'8 %.

## API REST
Il progetto `ImageForensic.Api` espone un endpoint HTTP per analizzare un'immagine tramite le stesse opzioni della libreria.

### Avvio
```bash
dotnet run --project ImageForensic.Api/ImageForensic.Api.csproj
```

### Endpoint `/analyze`
Richiede una richiesta `POST` `multipart/form-data` contenente il file da analizzare nel campo `image`. Eventuali parametri di `AnalyzeImageOptions` possono essere passati come altri campi del form; i percorsi dei file generati sono gestiti internamente creando una cartella temporanea per ogni richiesta. Esempio:
```bash
curl -F "image=@percorso/dell/immagine.jpg" http://localhost:5000/analyze
```
La risposta è un oggetto `AnalyzeImageResult` con punteggi, mappe generate come array di byte e verdetto. La documentazione Swagger è disponibile all'indirizzo `/swagger`.

### Parametri principali
`AnalyzeImageOptions` espone diversi parametri regolabili:

| Property | Description | Default |
|--------------------------|----------------------------------------------------------|--------:|
| `ElaQuality` | JPEG quality used when recompressing the image for ELA | `75` |
| `ElaWindowSize` | Neighborhood size used by the Sauvola threshold | `15` |
| `ElaK` | Parameter *k* for the Sauvola threshold | `0.2` |
| `ElaMinArea` | Minimum region area kept after morphology | `100` |
| `ElaKernelSize` | Morphological kernel size for mask refinement | `5` |
| `CopyMoveFeatureCount` | Number of SIFT features extracted for copy‑move detection | `5000` |
| `CopyMoveMatchDistance` | Maximum descriptor distance for a valid match | `3.0` |
| `CopyMoveRansacReproj` | RANSAC reprojection threshold in pixels | `3.0` |
| `CopyMoveRansacProb` | Desired RANSAC success probability | `0.99` |
| `SplicingInputWidth` | Width for the splicing model input | `256` |
| `SplicingInputHeight` | Height for the splicing model input | `256` |
| `NoiseprintInputSize` | Resize size fed into the Noiseprint model | `320` |
| `ElaWeight` | Weight of the ELA score in the final decision | `1.0` |
| `CopyMoveWeight` | Weight of the copy‑move score | `1.0` |
| `SplicingWeight` | Weight of the splicing score | `1.0` |
| `InpaintingWeight` | Weight of the inpainting score | `1.0` |
| `ExifWeight` | Weight of the EXIF score | `1.0` |
| `CleanThreshold` | Score below this value is considered `Clean` | `0.2` |
| `TamperedThreshold` | Score above this value is considered `Tampered` | `0.8` |


`AnalyzeImageResult` restituisce i valori seguenti:

| Field | Meaning |
|--------------------|------------------------------------------------------------|
| `ElaScore` | Normalised error level analysis score |
| `ElaMap` | PNG heat‑map bytes |
| `CopyMoveScore` | Ratio of matched keypoints consistent with a geometric transform |
| `CopyMoveMask` | Copy‑move mask bytes |
| `SplicingScore` | Score from the splicing detector |
| `SplicingMap` | Splicing heat‑map bytes |
| `InpaintingScore` | Mean value of the Noiseprint heat‑map |
| `InpaintingMap` | Noiseprint heat‑map bytes |
| `ExifScore` | Metadata checker score |
| `ExifAnomalies` | Detected metadata anomalies |
| `Errors` | Mapping of failed checks to error messages |
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
Utilizza la libreria `MetadataExtractor` per analizzare EXIF/XMP/IPTC. Vengono controllati `DateTimeOriginal`, `Software`, `Make/Model` e i tag GPS. Ogni anomalia incrementa lo score (range 0‑1). Viene scritto un report JSON con tutti i tag.

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
  "ElaMap": "iVBORw0KGgo...",
  "CopyMoveScore": 0.000,
  "CopyMoveMask": "iVBORw0KGgo...",
  "SplicingScore": 0.042,
  "SplicingMap": "iVBORw0KGgo...",
  "InpaintingScore": 0.015,
  "InpaintingMap": "iVBORw0KGgo...",
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
Il progetto `ImageForensic.Api.Tests` contiene i test per l'API REST:
```bash
dotnet test ImageForensic.Api.Tests/ImageForensic.Api.Tests.csproj -v n
```
I dataset di riferimento (CASIA2) sono collocati in `dataset/authentic` e `dataset/tampered`; altri file come `clean.png` e `inpainting.png` risiedono in `tests/ImageForensics.Tests/testdata`.

Ultima esecuzione test: **2025-08-01 08:37 UTC** – superati `ImageForensic.Api.Tests` (4 test), `TestOpenCvSharp` (2 test) e `ImageForensics.Tests` (46 test).

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

## ELA Metrics (IMD2020)

| Immagine | Threshold | MinArea | Kernel | RocAuc | Prauc | NSS | IoU | Dice | MCC | BF1 | Fpr@95TPR | AveragePrecision | BoundaryF1 | RegionIoU | TimeMs | PeakMemMb |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 00010_fake_02.png | 0.850 | 50 | 3 | 0.277 | 0.334 | -0.384 | 0.000 | 0.000 | 0.000 | 0.000 | 0.997 | 0.334 | 0.000 | 0.000 | 6243 | 174.99 |
| 00028_fake.jpg | 0.200 | 100 | 5 | 0.576 | 0.005 | 0.271 | 0.006 | 0.011 | 0.021 | 0.011 | 0.939 | 0.005 | 0.008 | 0.001 | 3633 | 32.57 |
| 00031_fake.jpg | 0.850 | 50 | 3 | 0.323 | 0.026 | -0.473 | 0.000 | 0.000 | 0.000 | 0.000 | 0.890 | 0.026 | 0.000 | 0.000 | 2145 | 0.00 |
| 00056_fake.jpg | 0.200 | 100 | 5 | 0.690 | 0.016 | 0.701 | 0.028 | 0.054 | 0.123 | 0.054 | 0.804 | 0.016 | 0.002 | 0.001 | 9963 | 0.00 |
| 00057_fake.jpg | 0.300 | 100 | 5 | 0.617 | 0.403 | 0.549 | 0.466 | 0.636 | 0.622 | 0.636 | 0.962 | 0.403 | 0.371 | 0.054 | 827 | 0.00 |
| 00064_fake.jpg | 0.700 | 50 | 3 | 0.417 | 0.290 | -0.174 | 0.000 | 0.000 | 0.000 | 0.000 | 0.912 | 0.290 | 0.000 | 0.000 | 3168 | 7.80 |
| 00109_fake.jpg | 0.400 | 50 | 3 | 0.807 | 0.153 | 1.286 | 0.171 | 0.292 | 0.332 | 0.292 | 0.701 | 0.153 | 0.319 | 0.011 | 5130 | 77.50 |
| c8u4gpd_0.jpg | 0.850 | 50 | 3 | 0.362 | 0.538 | -0.172 | 0.000 | 0.000 | 0.000 | 0.000 | 0.974 | 0.538 | 0.000 | 0.000 | 2050 | 0.00 |
| c8v1094_0.png | 0.150 | 100 | 5 | 0.954 | 0.690 | 1.906 | 0.753 | 0.859 | 0.843 | 0.859 | 0.178 | 0.690 | 0.287 | 0.253 | 3420 | 0.00 |
| c8vqhe0_0.jpg | 0.150 | 100 | 5 | 0.762 | 0.363 | 0.748 | 0.305 | 0.467 | 0.355 | 0.467 | 0.633 | 0.363 | 0.005 | 0.015 | 3621 | 0.00 |
| c8wu4jn_0.jpg | 0.100 | 100 | 5 | 0.583 | 0.017 | 0.273 | 0.020 | 0.039 | 0.077 | 0.039 | 0.891 | 0.017 | 0.010 | 0.001 | 6122 | 0.00 |
| c8wyxuh_0.jpg | 0.850 | 50 | 3 | 0.466 | 0.718 | -0.043 | 0.000 | 0.000 | 0.000 | 0.000 | 0.954 | 0.718 | 0.000 | 0.000 | 4417 | 0.00 |
| c8xbjz3_0.jpg | 0.250 | 100 | 5 | 0.588 | 0.202 | 0.334 | 0.152 | 0.264 | 0.212 | 0.264 | 0.930 | 0.202 | 0.147 | 0.004 | 6056 | 0.00 |
| c8xgnf4_0.jpg | 0.200 | 100 | 5 | 0.736 | 0.068 | 0.980 | 0.116 | 0.208 | 0.215 | 0.208 | 0.700 | 0.068 | 0.096 | 0.012 | 5031 | 0.00 |
| c8xkcsl_0.jpg | 0.850 | 50 | 3 | 0.476 | 0.158 | -0.158 | 0.000 | 0.000 | 0.000 | 0.000 | 0.951 | 0.158 | 0.000 | 0.000 | 5133 | 0.00 |
| c8zu1fj_0.jpg | 0.100 | 50 | 3 | 0.702 | 0.031 | 0.602 | 0.027 | 0.052 | 0.096 | 0.052 | 0.641 | 0.031 | 0.001 | 0.001 | 4820 | 0.00 |
| c909zbe_0.jpg | 0.200 | 100 | 5 | 0.652 | 0.107 | 0.652 | 0.133 | 0.234 | 0.196 | 0.234 | 0.843 | 0.107 | 0.098 | 0.004 | 36355 | 236.03 |
| c90k1ai_0.jpg | 0.100 | 100 | 5 | 0.664 | 0.431 | 0.329 | 0.411 | 0.582 | 0.343 | 0.582 | 0.777 | 0.431 | 0.041 | 0.051 | 8278 | 131.94 |
| c92uvo3_0.jpg | 0.450 | 50 | 3 | 0.768 | 0.105 | 1.094 | 0.053 | 0.100 | 0.226 | 0.100 | 0.609 | 0.105 | 0.018 | 0.053 | 3923 | 84.62 |
| c92zvkq_0.jpg | 0.250 | 100 | 5 | 0.717 | 0.165 | 0.993 | 0.159 | 0.274 | 0.323 | 0.274 | 0.821 | 0.165 | 0.208 | 0.016 | 10613 | 34.25 |
| c94a4qa_0.jpg | 0.300 | 50 | 3 | 0.349 | 0.101 | -0.322 | 0.002 | 0.004 | -0.001 | 0.004 | 0.998 | 0.101 | 0.103 | 0.000 | 21072 | 0.00 |
| c94s183_0.jpg | 0.100 | 100 | 5 | 0.705 | 0.064 | 0.642 | 0.060 | 0.114 | 0.178 | 0.114 | 0.797 | 0.064 | 0.007 | 0.004 | 3170 | 0.00 |
| c956l7o_0.png | 0.200 | 100 | 5 | 0.563 | 0.074 | 0.292 | 0.107 | 0.193 | 0.153 | 0.193 | 0.954 | 0.074 | 0.178 | 0.009 | 4406 | 0.00 |
| c97knp0_0.jpg | 0.150 | 100 | 5 | 0.630 | 0.167 | 0.398 | 0.150 | 0.261 | 0.169 | 0.261 | 0.830 | 0.167 | 0.097 | 0.010 | 5005 | 0.00 |
| c98qgm4_0.jpg | 0.150 | 100 | 5 | 0.720 | 0.021 | 0.765 | 0.027 | 0.052 | 0.126 | 0.052 | 0.762 | 0.021 | 0.002 | 0.001 | 10266 | 0.00 |
| c99k5bh_0.jpg | 0.850 | 50 | 3 | 0.248 | 0.032 | -0.701 | 0.000 | 0.000 | 0.000 | 0.000 | 0.990 | 0.032 | 0.000 | 0.000 | 16548 | 0.00 |
| c9abglu_0.jpg | 0.100 | 50 | 3 | 0.737 | 0.055 | 0.485 | 0.066 | 0.123 | 0.189 | 0.123 | 0.478 | 0.055 | 0.069 | 0.004 | 3200 | 0.00 |
| c9e4ble_0.jpg | 0.100 | 50 | 3 | 0.547 | 0.394 | 0.070 | 0.335 | 0.502 | 0.133 | 0.502 | 0.923 | 0.394 | 0.055 | 0.003 | 8398 | 0.00 |
| c9g9t6z_0.jpg | 0.200 | 100 | 5 | 0.966 | 0.367 | 3.494 | 0.605 | 0.754 | 0.751 | 0.754 | 0.115 | 0.367 | 0.518 | 0.150 | 6735 | 0.00 |
| c9gd5k9_0.jpg | 0.150 | 100 | 5 | 0.578 | 0.036 | 0.282 | 0.038 | 0.073 | 0.089 | 0.073 | 0.960 | 0.036 | 0.007 | 0.002 | 52677 | 0.00 |
| c9hsiyf_0.jpg | 0.100 | 50 | 3 | 0.606 | 0.176 | 0.292 | 0.153 | 0.266 | 0.133 | 0.266 | 0.839 | 0.176 | 0.000 | 0.002 | 7304 | 0.00 |
| c9juyz1_0.jpg | 0.100 | 100 | 5 | 0.944 | 0.832 | 3.232 | 0.812 | 0.896 | 0.893 | 0.896 | 0.463 | 0.832 | 0.570 | 0.780 | 7641 | 78.96 |
| c9o3qze_0.jpg | 0.850 | 50 | 3 | 0.455 | 0.067 | -0.186 | 0.000 | 0.000 | 0.000 | 0.000 | 0.898 | 0.067 | 0.000 | 0.000 | 3047 | 68.88 |
| c9qhtma_0.jpg | 0.800 | 50 | 3 | 0.489 | 0.168 | -0.102 | 0.000 | 0.000 | 0.000 | 0.000 | 0.908 | 0.168 | 0.000 | 0.000 | 2522 | 55.62 |
| c9qn2pn_0.jpg | 0.100 | 100 | 5 | 0.628 | 0.057 | 0.259 | 0.080 | 0.148 | 0.183 | 0.148 | 0.721 | 0.057 | 0.022 | 0.012 | 3424 | 66.25 |
| c9r2mxr_0.jpg | 0.100 | 50 | 3 | 0.709 | 0.046 | 0.564 | 0.062 | 0.116 | 0.134 | 0.116 | 0.786 | 0.046 | 0.020 | 0.001 | 58800 | 173.85 |
| c9rsftz_0.jpg | 0.400 | 100 | 5 | 0.473 | 0.005 | -0.045 | 0.000 | 0.000 | 0.000 | 0.000 | 0.999 | 0.005 | 0.000 | 0.000 | 11255 | 0.00 |
| c9sevmb_0.jpg | 0.150 | 100 | 5 | 0.772 | 0.487 | 0.749 | 0.438 | 0.609 | 0.492 | 0.609 | 0.656 | 0.487 | 0.096 | 0.019 | 8246 | 0.00 |
| c9sl9vi_0.jpg | 0.100 | 100 | 5 | 0.782 | 0.077 | 0.870 | 0.076 | 0.141 | 0.126 | 0.141 | 0.618 | 0.077 | 0.045 | 0.009 | 14014 | 0.00 |
| c9t437l_0.jpg | 0.850 | 50 | 3 | 0.219 | 0.096 | -0.731 | 0.000 | 0.000 | 0.000 | 0.000 | 0.997 | 0.096 | 0.000 | 0.000 | 6824 | 0.00 |
| c9tm0rs_0.jpg | 0.150 | 100 | 5 | 0.661 | 0.114 | 0.514 | 0.149 | 0.260 | 0.201 | 0.260 | 0.818 | 0.114 | 0.070 | 0.004 | 15576 | 0.00 |
| c9u4rvj_0.jpg | 0.300 | 100 | 5 | 0.931 | 0.311 | 3.439 | 0.439 | 0.610 | 0.622 | 0.610 | 0.352 | 0.311 | 0.230 | 0.151 | 9634 | 0.00 |
| c9urh6i_0.jpg | 0.150 | 100 | 5 | 0.630 | 0.256 | 0.413 | 0.370 | 0.541 | 0.456 | 0.541 | 0.908 | 0.256 | 0.056 | 0.003 | 38271 | 0.00 |
| c9v6alw_0.jpg | 0.100 | 100 | 5 | 0.728 | 0.357 | 0.665 | 0.270 | 0.425 | 0.333 | 0.425 | 0.620 | 0.357 | 0.022 | 0.004 | 12401 | 0.00 |
| c9vwn43_0.jpg | 0.450 | 50 | 3 | 0.865 | 0.088 | 1.933 | 0.043 | 0.083 | 0.208 | 0.083 | 0.565 | 0.088 | 0.000 | 0.053 | 7951 | 0.00 |
| **Media** | 0.33 | 78.89 | 4.16 | 0.62 | 0.21 | 0.59 | 0.16 | 0.23 | 0.21 | 0.23 | 0.78 | 0.21 | 0.08 | 0.04 | 10429.67 | 27.18 |
## Advanced ELA Analysis

The `ElaMetrics.Cli` utility now supports a multi-scale ELA pipeline with feature extraction, adaptive segmentation and an extended set of pixel and region metrics. Configuration lives in `ImageForensics/src/ElaMetrics.Cli/appsettings.json`.

Key options include:

- `Sauvola.WindowSize` – window size used by `ComputeSauvolaThreshold`
- `Sweep.Thresholds` – `{ Min, Max, Step }` range explored by `SweepThresholds`
- `Morphology.ParamSpace` – array of `{ MinArea, KernelSize }` pairs for `SweepMorphology`

### Usage
```bash
dotnet run --project ImageForensics/src/ElaMetrics.Cli -- dataset/imd2020
```
The command produces `ela-advanced-metrics.csv` and an interactive `ela-advanced-report.html` inside the dataset directory.

## Cross-Validation

To search thresholds and morphology parameters via k-fold validation:

```bash
dotnet run --project ElaSegmentation.Cmd \
  --imagesDir ./dataset/imd2020/images/tampered \
  --masksDir ./dataset/imd2020/images/mask \
  --folds 5 \
  --thresholds 0.01,0.05,0.10,0.15,0.20 \
  --minAreas 50,100,200 \
  --kernelSizes 3,5,7 \
  --outputCsv cv_results.csv \
  --outputHtml cv_report.html
```

`cv_results.csv` and `cv_report.html` may be versioned. Avoid committing raw images; convert them to Base64 if needed.

| Fold | Threshold | MinArea | KernelSize | MeanIoU | MeanDice | MeanMCC | StdIoU | StdDice | StdMCC |
|------|-----------|---------|------------|--------|---------|--------|-------|--------|-------|
| 1 | 0.10 | 50 | 3 | 0.16 | 0.23 | 0.21 | 0.21 | 0.25 | 0.23 |


## Esecuzione
```bash
dotnet run --project ElaSegmentation.Cmd \
  --imagesDir ./images \
  --masksDir ./masks \
  --jpegQuality 90 \
  --sauvolaWindow 15 \
  --sauvolaK 0.2 \
  --blocksX 4 \
  --blocksY 4 \
  --minArea 100 \
  --kernelSize 5
```
`report.md` and any generated `.base64` files can be committed alongside the code.
