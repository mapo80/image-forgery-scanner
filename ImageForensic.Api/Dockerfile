# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble AS base
# Install dependencies required by OpenCvSharp
RUN apt-get update \
    && apt-get install -y libgtk2.0-0 libgdk-pixbuf2.0-0 libtesseract5 libdc1394-25 libavcodec60 libavformat60 libswscale7 libsm6 libxext6 libxrender1 libgomp1 \
    && rm -rf /var/lib/apt/lists/*
USER $APP_UID
WORKDIR /app
EXPOSE 8080


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:9.0-noble AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["ImageForensic.Api/ImageForensic.Api.csproj", "ImageForensic.Api/"]
RUN dotnet restore "./ImageForensic.Api/ImageForensic.Api.csproj"
COPY . .
WORKDIR "/src/ImageForensic.Api"
RUN dotnet build "./ImageForensic.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./ImageForensic.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY ["so/libOpenCvSharpExtern.so", "/app/"]
ENV LD_LIBRARY_PATH=/app:$LD_LIBRARY_PATH
ENTRYPOINT ["dotnet", "ImageForensic.Api.dll"]
