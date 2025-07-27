Per buildare libOpenCvSharpExtern.so devi sempre eseguire lo script `scripts-build-opencvsharp.sh`. Il file `.so` deve essere generato usando la versione 4.10 di OpenCV e OpenCvSharp.
Non usare versioni gia` compilate: potrebbero non funzionare. Lo script deve terminare prima di procedere.
Al termine della build, committa `libOpenCvSharpExtern.so` e tutte le altre librerie `.so` richieste da `ldd libOpenCvSharpExtern.so`.
Per questo task non ci sono limiti di tempo: attendi sempre la conclusione dello script di build senza interromperlo.
