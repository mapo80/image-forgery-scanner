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
