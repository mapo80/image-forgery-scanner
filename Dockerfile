# 1) Runtime base AMD64
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/aspnet:9.0-noble AS base
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
      libgtk2.0-0 libgdk-pixbuf2.0-0 libtesseract5 libdc1394-25 \
      libavcodec60 libavformat60 libswscale7 libsm6 libxext6 \
      libxrender1 libgomp1 && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app
EXPOSE 8080

# 2) Build nativo ARM64 (no QEMU)
FROM mcr.microsoft.com/dotnet/sdk:9.0-noble AS build
ARG BUILD_CONFIGURATION=Release

WORKDIR /src
COPY ["ImageForensics/", "ImageForensics/"]
COPY ["ImageForensic.Api/ImageForensic.Api.csproj", "ImageForensic.Api/"]

# specifichiamo il RID linux-x64 già in fase di restore
RUN dotnet restore "ImageForensic.Api/ImageForensic.Api.csproj" \
    --runtime linux-x64

COPY . .
WORKDIR "/src/ImageForensic.Api"

# costruiamo la tua app (produce DLL cross-platform)
RUN dotnet build "./ImageForensic.Api.csproj" \
    -c $BUILD_CONFIGURATION -o /app/build

# 3) Publish per linux-x64: genera il launcher x64 anche se siamo su ARM64
FROM build AS publish
ARG BUILD_CONFIGURATION=Release

RUN dotnet publish "./ImageForensic.Api.csproj" \
    -c $BUILD_CONFIGURATION \
    -r linux-x64 \
    --self-contained false \
    -o /app/publish \
    /p:UseAppHost=true

# 4) Finale: runtime AMD64 + .so custom x64
FROM base AS final
WORKDIR /app

# Copio l’app pubblicata (x64)
COPY --from=publish /app/publish .

# Creo la cartella dove OpenCvSharp cerca le native per linux-x64
RUN mkdir -p runtimes/linux-x64/native

# Copio il tuo .so (buildato x64) in quella cartella
COPY so/libOpenCvSharpExtern.so runtimes/linux-x64/native/

# Imposto i permessi
RUN chmod 755 runtimes/linux-x64/native/libOpenCvSharpExtern.so

# Aggiungo il path alle librerie native
ENV LD_LIBRARY_PATH=/app/runtimes/linux-x64/native:${LD_LIBRARY_PATH}

ENTRYPOINT ["dotnet", "ImageForensic.Api.dll"]
