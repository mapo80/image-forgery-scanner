# 1) Base runtime amd64
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/aspnet:9.0-noble AS base
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        libgtk2.0-0 libgdk-pixbuf2.0-0 libtesseract5 libdc1394-25 \
        libavcodec60 libavformat60 libswscale7 libsm6 libxext6 \
        libxrender1 libgomp1 && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app
EXPOSE 8080

# 2) Build amd64
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:9.0-noble AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["ImageForensic.Api/ImageForensic.Api.csproj", "ImageForensic.Api/"]
RUN dotnet restore "ImageForensic.Api/ImageForensic.Api.csproj"
COPY . .
WORKDIR "/src/ImageForensic.Api"
RUN dotnet build "./ImageForensic.Api.csproj" \
    -c $BUILD_CONFIGURATION -o /app/build

# 3) Publish amd64
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./ImageForensic.Api.csproj" \
    -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# 4) Finale: copio il tuo .so custom
FROM base AS final
WORKDIR /app

# Copio l'app pubblicata
COPY --from=publish /app/publish .

# Creo la cartella dove OpenCvSharp cerca le native per linux-x64
RUN mkdir -p runtimes/linux-x64/native

# Copio il tuo .so (buildato da te) in quella cartella
COPY so/libOpenCvSharpExtern.so runtimes/linux-x64/native/

# Imposto i permessi
RUN chmod 755 runtimes/linux-x64/native/libOpenCvSharpExtern.so

# Aggiungo il path alle librerie nativa
ENV LD_LIBRARY_PATH=/app/runtimes/linux-x64/native:${LD_LIBRARY_PATH}

ENTRYPOINT ["dotnet", "ImageForensic.Api.dll"]
