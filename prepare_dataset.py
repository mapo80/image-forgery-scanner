#!/usr/bin/env python3
"""
prepare_dataset.py — Unified dataset builder for image-forgery metrics
===========================================================================
Extended 6 Aug 2025 — Output structure: images/{orig,tampered,mask}

Now all images and masks are copied into:
  <output-root>/images/orig       # original/clean images
  <output-root>/images/tampered   # tampered images
  <output-root>/images/mask       # mask PNGs (0/255)

Usage example (IMD2020 subset 100):
  python prepare_dataset.py \
    --output-root ./data \
    --download-imd2020 \
    --imd2020-sample 100

Supported datasets:
  • Columbia Splicing         (zip download)
  • MICC-F220 Copy–Move       (zip download)
  • CASIA v2 subset           (local path)
  • IMD2020 Real-Life         (zip download)

Dependencies: Python>=3.8, Pillow, numpy, requests, tqdm
"""
from __future__ import annotations
import argparse, csv, random, shutil, zipfile
from pathlib import Path
from typing import List, Tuple
import numpy as np
from PIL import Image

try:
    from tqdm import tqdm
except ImportError:
    def tqdm(x, **kwargs): return x

import requests

# Download URLs
COLUMBIA_URL = "https://www.ee.columbia.edu/ln/dvmm/downloads/columbia_USplicingDataSet.zip"
MICC_F220_URL = "https://imagelab.ing.unimore.it/imagelab/research/imagelab-datasets/MICC-F220.zip"
IMD2020_URL    = "https://staff.utia.cas.cz/novozada/db/IMD2020.zip"

# Helpers

def download_and_extract(url: str, dest_dir: Path) -> Path:
    dest_dir.mkdir(parents=True, exist_ok=True)
    archive = dest_dir / Path(url).name
    if not archive.exists():
        print(f"Downloading {url} …")
        with requests.get(url, stream=True, timeout=60) as r:
            r.raise_for_status()
            total = int(r.headers.get("content-length", 0))
            with open(archive, "wb") as f, tqdm(total=total, unit="B", unit_scale=True) as bar:
                for chunk in r.iter_content(8192): f.write(chunk); bar.update(len(chunk))
    extract_dir = dest_dir / archive.stem
    if not extract_dir.exists():
        print(f"Extracting {archive.name} …")
        with zipfile.ZipFile(archive) as zf:
            zf.extractall(extract_dir)
    # unwrap single-subdir
    subs = [d for d in extract_dir.iterdir() if d.is_dir()]
    if len(subs) == 1:
        return subs[0]
    return extract_dir


def ensure_mask_png(src: Path, dst: Path) -> None:
    dst.parent.mkdir(parents=True, exist_ok=True)
    img = Image.open(src).convert("L")
    arr = np.asarray(img)
    bin_arr = ((arr > 127) * 255).astype(np.uint8)
    Image.fromarray(bin_arr).save(dst, "PNG")


def copy_to(src: Path, dst_dir: Path) -> Path:
    dst_dir.mkdir(parents=True, exist_ok=True)
    dst = dst_dir / src.name
    shutil.copy2(src, dst)
    return dst

# Collectors copy into unified images/{orig,tampered,mask}

def collect_columbia(root: Path, out_img: Path, out_mask: Path) -> List[Tuple[str,str,str]]:
    rec=[]
    imgs = root / "final_dataset" / "images"
    masks= root / "final_dataset" / "masks"
    for img in imgs.glob("*.tif"):
        base = img.stem
        mask_src = masks / f"{base}_mask.tif"
        mask_dst = copy_to(mask_src, out_mask)
        img_dst   = copy_to(img, out_img / "tampered")
        rec.append((str(img_dst), "tampered", str(mask_dst)))
    for img in (root / "final_dataset" / "genuine").glob("*.tif"):
        img_dst = copy_to(img, out_img / "orig")
        rec.append((str(img_dst), "clean", ""))
    return rec


def collect_micc_f220(root: Path, out_img: Path, out_mask: Path) -> List[Tuple[str,str,str]]:
    rec=[]
    for img in (root / "f220" / "tampered").glob("*.jpg"):
        mask_src = root / "f220" / "mask" / f"{img.stem}.png"
        mask_dst = copy_to(mask_src, out_mask)
        img_dst   = copy_to(img, out_img / "tampered")
        rec.append((str(img_dst), "tampered", str(mask_dst)))
    for img in (root / "f220" / "original").glob("*.jpg"):
        img_dst = copy_to(img, out_img / "orig")
        rec.append((str(img_dst), "clean", ""))
    return rec


def collect_casia_subset(root: Path, out_img: Path, out_mask: Path, sample: int) -> List[Tuple[str,str,str]]:
    tampered = list((root / "Tampered" / "Splicing").glob("*.jpg")) + list((root / "Tampered" / "CopyMove").glob("*.jpg"))
    clean   = list((root / "Original" / "Splicing" / "Au").glob("*.jpg"))
    random.seed(42)
    n_t = min(sample//2, len(tampered)); n_c = min(sample-n_t, len(clean))
    t_s = random.sample(tampered, n_t); c_s = random.sample(clean, n_c)
    rec=[]
    for img in t_s:
        gt = root / "GroundTruth" / img.parent.parent.name / f"{img.stem}_gt.png"
        mask_dst = copy_to(gt, out_mask)
        img_dst  = copy_to(img, out_img / "tampered")
        rec.append((str(img_dst), "tampered", str(mask_dst)))
    for img in c_s:
        img_dst = copy_to(img, out_img / "orig")
        rec.append((str(img_dst), "clean", ""))
    return rec


def collect_imd2020(root: Path, out_img: Path, out_mask: Path, sample: int) -> List[Tuple[str,str,str]]:
    subs = [d for d in root.iterdir() if d.is_dir()]
    random.seed(42)
    chosen = random.sample(subs, min(sample, len(subs)))
    rec=[]
    for sd in chosen:
        orig = next(p for p in sd.iterdir() if p.stem.endswith("_orig"))
        mask = next(p for p in sd.iterdir() if "_mask" in p.stem)
        tam  = next(p for p in sd.iterdir() if p.is_file() and not p.stem.endswith("_orig") and "_mask" not in p.stem)
        mask_dst = copy_to(mask, out_mask)
        tam_dst  = copy_to(tam, out_img / "tampered")
        orig_dst = copy_to(orig, out_img / "orig")
        rec.extend([(str(tam_dst), "tampered", str(mask_dst)),
                    (str(orig_dst),"clean", "")])
    return rec

# Main

def build_dataset(args):
    out = Path(args.output_root).resolve()
    out_img = out / "images"
    out_mask= out_img / "mask"
    # prepare subdirs: orig, tampered, mask
    for sub in [out_img / "orig", out_img / "tampered", out_mask]:
        sub.mkdir(parents=True, exist_ok=True)
    records: List[Tuple[str,str,str]] = []

    if args.download_columbia:
        root = download_and_extract(COLUMBIA_URL, Path(args.cache_dir))
        records += collect_columbia(root, out_img, out_mask)
    if args.download_micc:
        root = download_and_extract(MICC_F220_URL, Path(args.cache_dir))
        records += collect_micc_f220(root, out_img, out_mask)
    if args.casia_path and args.casia_sample > 0:
        records += collect_casia_subset(Path(args.casia_path), out_img, out_mask, args.casia_sample)
    if args.download_imd2020 and args.imd2020_sample > 0:
        root = download_and_extract(IMD2020_URL, Path(args.cache_dir))
        subs = [d for d in root.iterdir() if d.is_dir()]
        if len(subs) == 1:
            root = subs[0]
        records += collect_imd2020(root, out_img, out_mask, args.imd2020_sample)

    if not records:
        raise SystemExit("No sources provided.")

    random.shuffle(records)
    manifest = out / "manifest.csv"
    with manifest.open("w", newline="") as f:
        writer = csv.writer(f)
        writer.writerow([
            "image_path", "class_label", "mask_path"
        ])
        writer.writerows(records)

    print(f"✓ Manifest saved: {manifest} (entries: {len(records)})")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(
        description="Build unified forgery dataset"
    )
    parser.add_argument("--output-root", required=True)
    parser.add_argument("--cache-dir", default="./_cache")
    parser.add_argument("--download-columbia", action="store_true")
    parser.add_argument("--download-micc", action="store_true")
    parser.add_argument("--casia-path")
    parser.add_argument("--casia-sample", type=int, default=0)
    parser.add_argument("--download-imd2020", action="store_true")
    parser.add_argument("--imd2020-sample", type=int, default=0)
    args = parser.parse_args()
    build_dataset(args)