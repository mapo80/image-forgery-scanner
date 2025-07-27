Per buildare libOpenCvSharpExtern.so devi sempre eseguire lo script `scripts-build-opencvsharp.sh`. Il file `.so` deve essere generato usando la versione 4.10 di OpenCV e OpenCvSharp.
Non usare versioni gia` compilate: potrebbero non funzionare. Lo script deve terminare prima di procedere.
Al termine della build, committa `libOpenCvSharpExtern.so` e tutte le altre librerie `.so` richieste da `ldd libOpenCvSharpExtern.so`.
Per questo task non ci sono limiti di tempo: attendi sempre la conclusione dello script di build senza interromperlo.

## Note sulla build
La compilazione di `OpenCvSharpExtern` fallisce se nella variabile `BUILD_LIST` di
OpenCV non vengono abilitati alcuni moduli extra (ad esempio `dnn`, `stitching`,
`shape`, `videoio` e `video`).  È quindi necessario assicurarsi che questi
moduli siano inclusi in `scripts-build-opencvsharp.sh`.  Inoltre il build system
di OpenCV richiede che `numpy` sia installato nel Python di sistema, altrimenti
la generazione fallisce durante la configurazione.
