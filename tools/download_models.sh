#!/usr/bin/env bash
set -euo pipefail

models=(
  "https://huggingface.co/spaces/akhaliq/ManTraNet/resolve/main/ManTraNet_256x256.onnx src/Models/onnx/mantranet_256x256.onnx a0f6a5e231fb9c255df6340ee2efdc4c93eae243c64c48ba74392f7c5d6d4c6e"
  "https://github.com/ZhendongWang6/CMFDFormer/releases/download/v1.0/cmfdformer_base.onnx src/Models/onnx/cmfdformer_base.onnx 3c5743acb4507c43f9a5d6b6dffe9d399e1cbb3bf6aaa1fe6a74b1e48dce07d5"
  "https://github.com/grip-unina/noiseprint/releases/download/v1.0/noiseprint_spp.onnx src/Models/onnx/noiseprint_spp.onnx 5e1312ed7d2e5ffa9b37d348862d97911fa2050c3df91d5a4823bbf9e82431ab"
)

for entry in "${models[@]}"; do
  IFS=' ' read -r url path sha <<< "$entry"
  dir=$(dirname "$path")
  mkdir -p "$dir"
  if [ -f "$path" ]; then
    hash=$(sha256sum "$path" | cut -d' ' -f1)
    if [ "$hash" = "$sha" ]; then
      echo "✓ $path ok"
      continue
    else
      echo "Hash mismatch for $path → redownloading"
    fi
  fi
  echo "↓ Downloading $url"
  curl -L -o "$path" "$url"
  hash=$(sha256sum "$path" | cut -d' ' -f1)
  if [ "$hash" != "$sha" ]; then
    echo "Hash verification failed for $path" >&2
    exit 1
  fi
  echo "✓ Saved $path"
done

echo "Models ready."
