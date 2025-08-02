#!/usr/bin/env python3
"""
prepare_dataset.py — Unified dataset builder for image-forgery metrics
===========================================================================
Creates a unified directory of images (orig, tampered) and masks for multiple datasets.

Output structure:
  <output_root>/images/orig       # clean/original images
  <output_root>/images/tampered   # forgery images
  <output_root>/images/mask       # binary masks (0/255)

Usage Examples:
  # Prepare CoMoFoD only, using existing cache:
  python prepare_dataset.py \
    --output-root ./data \
    --comofod-sample 50

  # Download and prepare IMD2020 subset 100:
  python prepare_dataset.py \
    --output-root ./data \
    --download-imd2020 --imd2020-sample 100

Dependencies:
  Python>=3.8
  pip install pillow numpy requests tqdm
  For CoMoFoD download: pip install kaggle
"""

import argparse
import csv
import random
import shutil
import sys
from pathlib import Path
from typing import List, Tuple

import numpy as np
from PIL import Image

try:
    from tqdm import tqdm
except ImportError:
    def tqdm(it, **kwargs): return it

import requests

# URLs for zip-based datasets
COLUMBIA_URL  = "https://www.ee.columbia.edu/ln/dvmm/downloads/columbia_USplicingDataSet.zip"
MICC_F220_URL = "https://imagelab.ing.unimore.it/imagelab/research/imagelab-datasets/MICC-F220.zip"
IMD2020_URL    = "https://staff.utia.cas.cz/novozada/db/IMD2020.zip"
# Kaggle dataset identifier for CoMoFoD
COMOFOD_KAGGLE = 'tusharchauhan1898/comofod'

# Utility: download+extract zip

def download_extract_zip(url: str, cache_dir: Path) -> Path:
    cache_dir.mkdir(parents=True, exist_ok=True)
    zip_path = cache_dir / url.split('/')[-1]
    if not zip_path.exists():
        print(f"Downloading {url}")
        resp = requests.get(url, stream=True, timeout=60)
        resp.raise_for_status()
        total = int(resp.headers.get('content-length', 0))
        with open(zip_path, 'wb') as f, tqdm(total=total, unit='B', unit_scale=True) as bar:
            for chunk in resp.iter_content(8192): f.write(chunk); bar.update(len(chunk))
    extract_dir = cache_dir / zip_path.stem
    if not extract_dir.exists():
        print(f"Extracting {zip_path.name}")
        import zipfile
        with zipfile.ZipFile(zip_path) as zf: zf.extractall(extract_dir)
    # unwrap single subdir
    subs = [d for d in extract_dir.iterdir() if d.is_dir()]
    return subs[0] if len(subs)==1 else extract_dir

# Utility: download CoMoFoD via Kaggle API if needed

def prepare_comofod(cache_dir: Path, sample: int) -> Path:
    # locate existing
    for sub in cache_dir.iterdir():
        if sub.is_dir() and 'comofod' in sub.name.lower():
            images = sub / 'images'; gt = sub / 'gt'
            if images.is_dir() and gt.is_dir():
                print(f"Using cached CoMoFoD in {sub}")
                return sub
    # no cache => require env variables
    from kaggle.api.kaggle_api_extended import KaggleApi
    api = KaggleApi(); api.authenticate()
    print("Downloading CoMoFoD via Kaggle API")
    api.dataset_download_files(COMOFOD_KAGGLE, path=cache_dir, unzip=True, quiet=False)
    # find extracted dir
    return prepare_comofod(cache_dir, sample)

# Helpers for copying and mask conversion

def copy_file(src: Path, dst_folder: Path) -> Path:
    dst_folder.mkdir(parents=True, exist_ok=True)
    dst = dst_folder / src.name
    shutil.copy2(src, dst)
    return dst

# Collector: generic copy/move solvents

def collect_dataset(root: Path, patterns: List[str], label: str,
                    out_img: Path, out_mask: Path) -> List[Tuple[str,str,str]]:
    rec = []
    for pat in patterns:
        for img_path in root.glob(pat):
            if not img_path.is_file(): continue
            base = img_path.stem
            # find mask by base name
            mask = next(root.glob(f"{base}*mask*.*"), None)
            img_dst = copy_file(img_path, out_img / label)
            mask_dst = ''
            if mask:
                mask_dst = str(copy_file(mask, out_mask))
            rec.append((str(img_dst), label, mask_dst))
    return rec

# Build unified dataset

def build(args):
    out_root = Path(args.output_root)
    imgs_dir = out_root / 'images'
    masks_dir = imgs_dir / 'mask'
    for d in (imgs_dir/'orig', imgs_dir/'tampered', masks_dir): d.mkdir(parents=True, exist_ok=True)

    records: List[Tuple[str,str,str]] = []
    cache = Path(args.cache_dir)

    if args.download_columbia:
        r = download_extract_zip(COLUMBIA_URL, cache)
        records += collect_dataset(r/ 'final_dataset'/ 'images', ['*.tif'], 'tampered', imgs_dir, masks_dir)
        records += collect_dataset(r/ 'final_dataset'/ 'genuine', ['*.tif'], 'orig', imgs_dir, masks_dir)

    if args.download_micc:
        r = download_extract_zip(MICC_F220_URL, cache)
        records += collect_dataset(r/'f220'/'tampered', ['*.jpg'], 'tampered', imgs_dir, masks_dir)
        records += collect_dataset(r/'f220'/'original', ['*.jpg'], 'orig', imgs_dir, masks_dir)

    if args.comofod_sample:
        r = prepare_comofod(cache, args.comofod_sample)
        records += collect_dataset(r/'images', ['*.png','*.jpg'], 'tampered', imgs_dir, masks_dir)

    if args.download_imd2020:
        r = download_extract_zip(IMD2020_URL, cache)
        subs = [d for d in r.iterdir() if d.is_dir()]
        chosen = random.sample(subs, min(args.imd2020_sample, len(subs)))
        for sd in chosen:
            orig = next(sd.glob('*_orig*'))
            mask = next(sd.glob('*_mask*'))
            tampered = next(p for p in sd.iterdir() if p not in (orig,mask))
            records.append((str(copy_file(tampered, imgs_dir/'tampered')),'tampered',str(copy_file(mask,masks_dir))))
            records.append((str(copy_file(orig, imgs_dir/'orig')),'orig',''))

    if not records:
        print("No sources provided.")
        sys.exit(1)

    random.shuffle(records)
    with open(out_root/'manifest.csv','w',newline='') as f:
        w=csv.writer(f); w.writerow(['image_path','class_label','mask_path']); w.writerows(records)
    print(f"Manifest written with {len(records)} entries at {out_root/'manifest.csv'}")

if __name__=='__main__':
    p = argparse.ArgumentParser()
    p.add_argument('--output-root', required=True)
    p.add_argument('--cache-dir', default='./_cache')
    p.add_argument('--download-columbia', action='store_true')
    p.add_argument('--download-micc', action='store_true')
    p.add_argument('--comofod-sample', type=int, default=0)
    p.add_argument('--download-imd2020', action='store_true')
    p.add_argument('--imd2020-sample', type=int, default=0)
    args = p.parse_args()
    build(args)