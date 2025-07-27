#!/usr/bin/env bash
set -euo pipefail

# Build OpenCV and OpenCvSharp for Ubuntu 24.04
OPENCV_VERSION=4.10.0
WORKDIR=$(pwd)

# Install dependencies
sudo apt-get update
sudo apt-get install -y --no-install-recommends \
    build-essential cmake git wget unzip \
    libgtk2.0-dev libavcodec-dev libavformat-dev libswscale-dev libtesseract-dev \
    libdc1394-dev libxine2-dev libv4l-dev libtheora-dev libvorbis-dev \
    libxvidcore-dev libopencore-amrnb-dev libopencore-amrwb-dev libtbb-dev \
    libatlas-base-dev x264 libgdiplus \
    python3 python3-dev python3-pip

# Ensure numpy is available for the PyEnv python used by OpenCV's build system
python3 -m pip install --user --upgrade numpy

# Download and build OpenCV with contrib modules (reduced build list for speed)
wget -q https://github.com/opencv/opencv/archive/${OPENCV_VERSION}.zip
unzip -qo ${OPENCV_VERSION}.zip
rm -rf opencv opencv_contrib
mv opencv-${OPENCV_VERSION} opencv
rm -f ${OPENCV_VERSION}.zip
wget -q https://github.com/opencv/opencv_contrib/archive/${OPENCV_VERSION}.zip
unzip -qo ${OPENCV_VERSION}.zip
mv opencv_contrib-${OPENCV_VERSION} opencv_contrib
rm -f ${OPENCV_VERSION}.zip
mkdir -p opencv/build
cd opencv/build
cmake .. \
    -D OPENCV_EXTRA_MODULES_PATH=../../opencv_contrib/modules \
    -D CMAKE_BUILD_TYPE=Release \
    -D BUILD_LIST=core,imgproc,imgcodecs,highgui,features2d,calib3d,xfeatures2d \
    -D BUILD_SHARED_LIBS=OFF \
    -D BUILD_EXAMPLES=OFF \
    -D BUILD_TESTS=OFF \
    -D BUILD_PERF_TESTS=OFF \
    -D BUILD_JAVA=OFF \
    -D BUILD_opencv_python_bindings_generator=OFF \
    -D BUILD_opencv_python_tests=OFF \
    -D OPENCV_ENABLE_NONFREE=ON
make -j"$(nproc)"
sudo make install
sudo ldconfig
cd "$WORKDIR"

# Build OpenCvSharp extern library
if [ ! -d opencvsharp ]; then
    git clone --depth 1 https://github.com/shimat/opencvsharp.git
fi
cd opencvsharp/src
mkdir -p build && cd build
cmake .. \
    -D CMAKE_BUILD_TYPE=Release \
    -D CMAKE_INSTALL_PREFIX=/usr/local
make -j"$(nproc)"
# The library will be under OpenCvSharpExtern
cp OpenCvSharpExtern/libOpenCvSharpExtern.so "$WORKDIR/libOpenCvSharpExtern.so"

echo "libOpenCvSharpExtern.so built at $WORKDIR/libOpenCvSharpExtern.so"
