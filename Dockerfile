# syntax=docker/dockerfile:1.4
#############################################
# Builder per OpenCV 4.10 e OpenCvSharpExtern (x86_64)
#############################################
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/aspnet:8.0-noble AS builder

ENV DEBIAN_FRONTEND=noninteractive  
ENV OPENCV_VERSION=4.10.0  

WORKDIR /

# Install system dependencies per Ubuntu 24.04
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
       apt-transport-https \
       software-properties-common \
       wget \
       unzip \
       ca-certificates \
       build-essential \
       cmake \
       git \
       libtbb-dev \
       libatlas-base-dev \
       libgtk2.0-dev \
       libavcodec-dev \
       libavformat-dev \
       libswscale-dev \
       libdc1394-dev \
       libxine2-dev \
       libv4l-dev \
       libtheora-dev \
       libvorbis-dev \
       libxvidcore-dev \
       libopencore-amrnb-dev \
       libopencore-amrwb-dev \
       x264 \
       libtesseract-dev \
       libgdiplus \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Scarica e compila OpenCV 4.10 con moduli contrib (shared libs)
RUN wget -q https://github.com/opencv/opencv/archive/${OPENCV_VERSION}.zip \
    && unzip -q ${OPENCV_VERSION}.zip && rm ${OPENCV_VERSION}.zip && mv opencv-${OPENCV_VERSION} opencv \
    && wget -q https://github.com/opencv/opencv_contrib/archive/${OPENCV_VERSION}.zip \
    && unzip -q ${OPENCV_VERSION}.zip && rm ${OPENCV_VERSION}.zip && mv opencv_contrib-${OPENCV_VERSION} opencv_contrib

RUN cd opencv \
    && mkdir build && cd build \
    && cmake \
         -D OPENCV_EXTRA_MODULES_PATH=/opencv_contrib/modules \
         -D CMAKE_BUILD_TYPE=RELEASE \
         -D BUILD_SHARED_LIBS=ON \
         -D ENABLE_CXX11=ON \
         -D BUILD_EXAMPLES=OFF \
         -D BUILD_DOCS=OFF \
         -D BUILD_PERF_TESTS=OFF \
         -D BUILD_TESTS=OFF \
         -D BUILD_JAVA=OFF \
         -D BUILD_opencv_app=OFF \
         -D BUILD_opencv_barcode=OFF \
         -D BUILD_opencv_wechat_qrcode=ON \
         -D WITH_GSTREAMER=OFF \
         -D WITH_ADE=OFF \
         -D OPENCV_ENABLE_NONFREE=ON \
         -D CMAKE_RULE_MESSAGES=OFF \
         -D CMAKE_MESSAGE_LOG_LEVEL=WARNING \
         .. \
    && make -j"$(nproc)" \
    && make install \
    && ldconfig

# Clona e compila OpenCvSharp binding nativo
RUN git clone https://github.com/shimat/opencvsharp.git /opencvsharp \
    && mkdir /opencvsharp/make \
    && cd /opencvsharp/make \
    && cmake -D CMAKE_INSTALL_PREFIX=/opencvsharp/make /opencvsharp/src \
    && make -j"$(nproc)" install \
    && cp /opencvsharp/make/OpenCvSharpExtern/libOpenCvSharpExtern.so /usr/lib/

# Raccogli dipendenze native via ldd (inclusi transitivi)
RUN mkdir -p /usr/lib/deps \
    && ldd /usr/lib/libOpenCvSharpExtern.so \
       | awk '/=>/ { print $3 }' \
       | xargs -r -I{} sh -c 'ldd {} | awk "/=>/ {print \$3}" | xargs -r -I% cp -v % /usr/lib/deps; cp -v {} /usr/lib/deps'

#############################################
# Final: immagine runtime con solo .so e dipendenze (x86_64)
#############################################
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/runtime-deps:8.0-noble AS final

# Copia binding e dipendenze native
COPY --from=builder /usr/lib/libOpenCvSharpExtern.so /usr/lib/
COPY --from=builder /usr/lib/deps/*.so /usr/lib/

#############################################
# Export .so su filesystem locale (host) + shell
#############################################
FROM --platform=linux/amd64 alpine:3.18 AS export
RUN mkdir /so
COPY --from=builder /usr/lib/libOpenCvSharpExtern.so /so/
COPY --from=builder /usr/lib/deps/*.so       /so/
CMD ["/bin/sh"]