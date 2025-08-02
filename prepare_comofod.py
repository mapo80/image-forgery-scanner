#!/usr/bin/env python3
# -*- coding: utf-8 -*-

"""
prepare_copy_move_dataset.py

Organizza il dataset CoMoFoD per detection copy-move suddividendo in 3 cartelle:
  - orig    : immagini originali
  - forged  : immagini forgery (copy-move)
  - mask    : maschere binarie (ground truth)

Opzioni:
  --sample-size  Numero di set da estrarre casualmente (default = tutti)
  --seed         Seed per riproducibilità della scelta casuale

Usage:
    python prepare_copy_move_dataset.py \
        --input-dir /percorso/_cache/CoMoFoD_small_v2 \
        --output-dir /percorso/dataset_copy_move \
        --sample-size 50 --seed 42
"""

import argparse
import random
import shutil
from pathlib import Path


def parse_args():
    parser = argparse.ArgumentParser(
        description="Prepara il dataset CoMoFoD per copy-move detection"
    )
    parser.add_argument(
        "--input-dir",
        type=Path,
        required=True,
        help="Cartella sorgente contenente i file CoMoFoD_small_v2"
    )
    parser.add_argument(
        "--output-dir",
        type=Path,
        required=True,
        help="Cartella di destinazione che conterrà le 3 sottocartelle"
    )
    parser.add_argument(
        "--sample-size",
        type=int,
        default=0,
        help="Numero di set da estrarre casualmente. 0 = tutti"
    )
    parser.add_argument(
        "--seed",
        type=int,
        default=None,
        help="Seed per la selezione casuale"
    )
    return parser.parse_args()


def main():
    args = parse_args()

    # Cerca tutte le immagini originali (_O.*) e ottieni il base name prima di '_'
    all_orig = list(args.input_dir.glob('*_O.*'))
    if not all_orig:
        print(f"Nessuna immagine originale trovata in {args.input_dir}")
        return

    stems = [p.stem.split('_')[0] for p in all_orig]

    # Se richiesta, estrai un subset casuale
    if args.sample_size > 0:
        if args.sample_size > len(stems):
            raise ValueError(
                f"Richiesti {args.sample_size} set, ma ne esistono solo {len(stems)}"
            )
        if args.seed is not None:
            random.seed(args.seed)
        stems = random.sample(stems, args.sample_size)

    # Prepara cartelle di output
    orig_dir = args.output_dir / 'orig'
    forg_dir = args.output_dir / 'forged'
    mask_dir = args.output_dir / 'mask'
    for d in (orig_dir, forg_dir, mask_dir):
        d.mkdir(parents=True, exist_ok=True)

    count = 0
    for stem in stems:
        # costruisci pattern di ricerca
        orig_path = next(args.input_dir.glob(f"{stem}_O.*"), None)
        forg_path = next(args.input_dir.glob(f"{stem}_F.*"), None)
        mask_path = next(args.input_dir.glob(f"{stem}_B.*"), None)

        if not orig_path or not forg_path or not mask_path:
            print(f"[Warning] File mancanti per {stem}, skip")
            continue

        # copia mantenendo solo il base name + estensione
        shutil.copy2(orig_path, orig_dir / (stem + orig_path.suffix))
        shutil.copy2(forg_path, forg_dir / (stem + forg_path.suffix))
        shutil.copy2(mask_path, mask_dir / (stem + mask_path.suffix))
        count += 1

    print(f"Organizzati {count} set in {args.output_dir}")


if __name__ == '__main__':
    main()