#!/usr/bin/env python3
"""
Training Script for Cephalometric Landmark Detection Model
===========================================================

Supports:
  - Custom dataset with images + JSON annotations
  - ISBI 2015 Challenge dataset format
  - Data augmentation (rotation, brightness, contrast, flip, Gaussian noise)
  - Combined loss: MSE heatmaps + Wing Loss on coordinates
  - AdamW optimizer with CosineAnnealingLR
  - 2mm tolerance accuracy metric
  - ONNX export after training

Usage:
  # Train on ISBI 2015 dataset
  python train.py --data_dir ./data/isbi2015 --epochs 100 --batch_size 8

  # Train on custom dataset
  python train.py --data_dir ./data/custom --format custom --epochs 100

  # Download ISBI dataset first
  python train.py --download_isbi --data_dir ./data/isbi2015
"""

import os
import sys
import json
import argparse
import logging
from pathlib import Path
from typing import Dict, List, Tuple, Optional

import numpy as np
import cv2
import torch
import torch.nn as nn
import torch.optim as optim
from torch.utils.data import Dataset, DataLoader, random_split
from torchvision import transforms

import albumentations as A
from albumentations.pytorch import ToTensorV2
from tqdm import tqdm
from sklearn.model_selection import KFold

from landmark_detector import (
    CephLandmarkDetector, NUM_LANDMARKS, LANDMARK_KEYS, LANDMARK_NAMES_AR,
    generate_gaussian_heatmap, landmarks_to_heatmaps, heatmaps_to_landmarks,
    CombinedLoss
)

# ──────────────────────────────────────────────────────────────────────────────
# Logging
# ──────────────────────────────────────────────────────────────────────────────
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s [%(levelname)s] %(message)s',
    handlers=[logging.StreamHandler(sys.stdout)]
)
logger = logging.getLogger(__name__)


# ──────────────────────────────────────────────────────────────────────────────
# Dataset
# ──────────────────────────────────────────────────────────────────────────────

class CephDataset(Dataset):
    """
    Dataset for cephalometric landmark detection.

    Supports two annotation formats:

    1. Custom format: JSON file with structure:
       [
         {
           "image": "path/to/image.jpg",
           "landmarks": {"S": [x, y], "N": [x, y], ...}
         },
         ...
       ]

    2. ISBI 2015 format: Each image has a corresponding .txt file with
       one landmark per line: "x,y" (24 lines, in LANDMARK_KEYS order)
    """

    INPUT_SIZE = 512
    HEATMAP_SIZE = 128
    SIGMA = 4.0

    def __init__(
        self,
        data_dir: str,
        format: str = 'isbi',
        annotation_file: Optional[str] = None,
        transform: Optional[A.Compose] = None,
        split: str = 'train'
    ):
        self.data_dir = Path(data_dir)
        self.format = format
        self.transform = transform
        self.split = split

        self.samples = self._load_annotations(annotation_file)
        logger.info(f"Loaded {len(self.samples)} samples for {split}")

    def _load_annotations(self, annotation_file: Optional[str]) -> List[Dict]:
        """Load annotations based on the dataset format."""
        if self.format == 'custom':
            return self._load_custom(annotation_file)
        elif self.format == 'isbi':
            return self._load_isbi()
        else:
            raise ValueError(f"Unknown format: {self.format}. Use 'isbi' or 'custom'.")

    def _load_custom(self, annotation_file: Optional[str]) -> List[Dict]:
        """Load custom JSON annotation format."""
        if annotation_file is None:
            annotation_file = str(self.data_dir / 'annotations.json')

        with open(annotation_file, 'r') as f:
            raw = json.load(f)

        samples = []
        for item in raw:
            image_path = item['image']
            if not os.path.isabs(image_path):
                image_path = str(self.data_dir / image_path)

            if not os.path.exists(image_path):
                logger.warning(f"Image not found: {image_path}")
                continue

            landmarks = {}
            for key in LANDMARK_KEYS:
                if key in item.get('landmarks', {}):
                    coords = item['landmarks'][key]
                    landmarks[key] = (float(coords[0]), float(coords[1]))

            if len(landmarks) > 0:
                samples.append({
                    'image_path': image_path,
                    'landmarks': landmarks
                })

        return samples

    def _load_isbi(self) -> List[Dict]:
        """
        Load ISBI 2015 Challenge dataset format.

        Expected structure:
            data_dir/
              images/    (or RawImage/)
                *.bmp or *.jpg
              landmarks/ (or 400_senior/)
                *.txt

        Each .txt file has 24 lines, one landmark per line as "x,y".
        Landmarks are in LANDMARK_KEYS order.
        """
        # Find image directory
        img_dir = None
        for candidate in ['images', 'RawImage', 'raw']:
            d = self.data_dir / candidate
            if d.exists():
                img_dir = d
                break
        if img_dir is None:
            img_dir = self.data_dir

        # Find landmark directory
        lm_dir = None
        for candidate in ['landmarks', '400_senior', 'senior', 'annotation']:
            d = self.data_dir / candidate
            if d.exists():
                lm_dir = d
                break
        if lm_dir is None:
            lm_dir = self.data_dir

        # Find image files
        image_extensions = {'.bmp', '.jpg', '.jpeg', '.png', '.tif', '.tiff'}
        image_files = sorted([
            f for f in img_dir.iterdir()
            if f.suffix.lower() in image_extensions
        ])

        samples = []
        for img_path in image_files:
            # Find corresponding landmark file
            lm_name = img_path.stem + '.txt'
            lm_path = lm_dir / lm_name

            if not lm_path.exists():
                # Try other naming conventions
                for alt_name in [f'{img_path.stem}_landmarks.txt', f'{img_path.stem}.pts']:
                    alt_path = lm_dir / alt_name
                    if alt_path.exists():
                        lm_path = alt_path
                        break

            if not lm_path.exists():
                logger.warning(f"Landmark file not found for {img_path.name}")
                continue

            # Parse landmark file
            landmarks = {}
            try:
                with open(lm_path, 'r') as f:
                    lines = f.read().strip().split('\n')
                    for i, line in enumerate(lines):
                        line = line.strip()
                        if not line or line.startswith('#'):
                            continue
                        parts = line.replace(' ', '').split(',')
                        if len(parts) >= 2 and i < NUM_LANDMARKS:
                            x, y = float(parts[0]), float(parts[1])
                            landmarks[LANDMARK_KEYS[i]] = (x, y)
            except Exception as e:
                logger.warning(f"Error parsing {lm_path}: {e}")
                continue

            if len(landmarks) > 0:
                samples.append({
                    'image_path': str(img_path),
                    'landmarks': landmarks
                })

        return samples

    def __len__(self) -> int:
        return len(self.samples)

    def __getitem__(self, idx: int) -> Dict[str, torch.Tensor]:
        sample = self.samples[idx]

        # Load image as grayscale
        image = cv2.imread(sample['image_path'], cv2.IMREAD_GRAYSCALE)
        if image is None:
            raise ValueError(f"Failed to load image: {sample['image_path']}")

        h, w = image.shape[:2]
        landmarks = sample['landmarks'].copy()

        # Apply augmentations
        if self.transform:
            # Prepare keypoints for albumentations
            keypoints = []
            keypoint_names = []
            for key in LANDMARK_KEYS:
                if key in landmarks:
                    x, y = landmarks[key]
                    keypoints.append((x, y))
                    keypoint_names.append(key)

            transformed = self.transform(
                image=image,
                keypoints=keypoints
            )
            image = transformed['image']
            transformed_kps = transformed['keypoints']

            # Update landmarks with transformed coordinates
            for kp, name in zip(transformed_kps, keypoint_names):
                landmarks[name] = (kp[0], kp[1])
        else:
            # Just resize and normalize
            image = cv2.resize(image, (self.INPUT_SIZE, self.INPUT_SIZE))
            image = image.astype(np.float32) / 255.0
            image = torch.from_numpy(image).unsqueeze(0)  # (1, H, W)

        # Generate heatmaps
        # Scale landmarks to heatmap space
        current_h, current_w = self.INPUT_SIZE, self.INPUT_SIZE
        if isinstance(image, torch.Tensor):
            current_h, current_w = image.shape[1], image.shape[2]

        heatmaps = landmarks_to_heatmaps(
            landmarks, current_w, current_h,
            heatmap_size=self.HEATMAP_SIZE, sigma=self.SIGMA
        )

        # Create coordinate tensor (24, 2) in 512x512 space
        coords = np.zeros((NUM_LANDMARKS, 2), dtype=np.float32)
        visibility = np.zeros(NUM_LANDMARKS, dtype=np.float32)
        for i, key in enumerate(LANDMARK_KEYS):
            if key in landmarks:
                x, y = landmarks[key]
                coords[i] = [x, y]
                visibility[i] = 1.0

        return {
            'image': image if isinstance(image, torch.Tensor) else torch.from_numpy(image),
            'heatmaps': torch.from_numpy(heatmaps),
            'coords': torch.from_numpy(coords),
            'visibility': torch.from_numpy(visibility),
        }


def get_train_transforms() -> A.Compose:
    """Training augmentations."""
    return A.Compose([
        A.Resize(512, 512),
        A.Rotate(limit=15, p=0.5),
        A.RandomBrightnessContrast(brightness_limit=0.2, contrast_limit=0.2, p=0.5),
        A.GaussNoise(var_limit=(5, 25), p=0.3),
        A.HorizontalFlip(p=0.5),
        A.GaussianBlur(blur_limit=(3, 5), p=0.2),
        A.ToFloat(max_value=255.0),
        ToTensorV2(),  # (1, H, W), float32
    ], keypoint_params=A.KeypointParams(format='xy', remove_invisible=False))


def get_val_transforms() -> A.Compose:
    """Validation augmentations (minimal)."""
    return A.Compose([
        A.Resize(512, 512),
        A.ToFloat(max_value=255.0),
        ToTensorV2(),
    ], keypoint_params=A.KeypointParams(format='xy', remove_invisible=False))


# ──────────────────────────────────────────────────────────────────────────────
# Training
# ──────────────────────────────────────────────────────────────────────────────

def train_one_epoch(
    model: CephLandmarkDetector,
    dataloader: DataLoader,
    criterion: CombinedLoss,
    optimizer: optim.Optimizer,
    device: torch.device
) -> Dict[str, float]:
    """Train for one epoch."""
    model.train()
    total_loss = 0.0
    total_hm_loss = 0.0
    total_coord_loss = 0.0
    num_batches = 0

    pbar = tqdm(dataloader, desc='Training', leave=False)
    for batch in pbar:
        images = batch['image'].to(device)
        heatmaps = batch['heatmaps'].to(device)
        coords = batch['coords'].to(device)
        visibility = batch['visibility'].to(device)

        optimizer.zero_grad()

        pred_heatmaps = model(images)
        losses = criterion(pred_heatmaps, heatmaps, coords, visibility)

        losses['total'].backward()
        # Gradient clipping
        torch.nn.utils.clip_grad_norm_(model.parameters(), max_norm=1.0)
        optimizer.step()

        total_loss += losses['total'].item()
        total_hm_loss += losses['heatmap'].item()
        total_coord_loss += losses['coord'].item()
        num_batches += 1

        pbar.set_postfix({
            'loss': f'{losses["total"].item():.4f}',
            'hm': f'{losses["heatmap"].item():.4f}',
            'coord': f'{losses["coord"].item():.4f}'
        })

    return {
        'loss': total_loss / max(num_batches, 1),
        'heatmap_loss': total_hm_loss / max(num_batches, 1),
        'coord_loss': total_coord_loss / max(num_batches, 1),
    }


@torch.no_grad()
def validate(
    model: CephLandmarkDetector,
    dataloader: DataLoader,
    criterion: CombinedLoss,
    device: torch.device,
    pixels_per_mm: float = 10.0  # approximate for ISBI dataset
) -> Dict[str, float]:
    """
    Validate the model.

    Computes:
    - Loss
    - 2mm tolerance accuracy: percentage of landmarks within 2mm of ground truth
    - Mean radial error (MRE) in mm
    """
    model.eval()
    total_loss = 0.0
    num_batches = 0

    all_errors = []  # radial errors in mm
    correct_2mm = 0
    total_landmarks = 0
    correct_3mm = 0
    correct_4mm = 0

    pbar = tqdm(dataloader, desc='Validating', leave=False)
    for batch in pbar:
        images = batch['image'].to(device)
        heatmaps = batch['heatmaps'].to(device)
        coords = batch['coords'].to(device)
        visibility = batch['visibility'].to(device)

        pred_heatmaps = model(images)
        losses = criterion(pred_heatmaps, heatmaps, coords, visibility)
        total_loss += losses['total'].item()
        num_batches += 1

        # Extract predicted coordinates
        pred_prob = torch.sigmoid(pred_heatmaps).cpu().numpy()
        gt_coords = coords.cpu().numpy()
        vis = visibility.cpu().numpy()

        for b in range(pred_prob.shape[0]):
            for i in range(NUM_LANDMARKS):
                if vis[b, i] < 0.5:
                    continue

                hm = pred_prob[b, i]
                flat_idx = np.argmax(hm)
                peak_y = flat_idx // 128
                peak_x = flat_idx % 128

                # Sub-pixel refinement
                r = 2
                y_min = max(0, peak_y - r)
                y_max = min(128, peak_y + r + 1)
                x_min = max(0, peak_x - r)
                x_max = min(128, peak_x + r + 1)
                region = hm[y_min:y_max, x_min:x_max]

                if region.sum() > 1e-6:
                    yy, xx = np.mgrid[y_min:y_max, x_min:x_max].astype(np.float32)
                    refined_x = (xx * region).sum() / region.sum()
                    refined_y = (yy * region).sum() / region.sum()
                else:
                    refined_x, refined_y = float(peak_x), float(peak_y)

                pred_x = (refined_x + 0.5) * (512.0 / 128)
                pred_y = (refined_y + 0.5) * (512.0 / 128)

                gt_x, gt_y = gt_coords[b, i]
                error_px = np.sqrt((pred_x - gt_x) ** 2 + (pred_y - gt_y) ** 2)
                error_mm = error_px / pixels_per_mm

                all_errors.append(error_mm)
                total_landmarks += 1
                if error_mm <= 2.0:
                    correct_2mm += 1
                if error_mm <= 3.0:
                    correct_3mm += 1
                if error_mm <= 4.0:
                    correct_4mm += 1

    mre = np.mean(all_errors) if all_errors else float('inf')
    sdr_2mm = correct_2mm / max(total_landmarks, 1) * 100
    sdr_3mm = correct_3mm / max(total_landmarks, 1) * 100
    sdr_4mm = correct_4mm / max(total_landmarks, 1) * 100

    return {
        'loss': total_loss / max(num_batches, 1),
        'mre_mm': mre,
        'sdr_2mm': sdr_2mm,
        'sdr_3mm': sdr_3mm,
        'sdr_4mm': sdr_4mm,
    }


def train(
    data_dir: str,
    format: str = 'isbi',
    annotation_file: Optional[str] = None,
    output_dir: str = './output',
    epochs: int = 100,
    batch_size: int = 8,
    lr: float = 1e-4,
    weight_decay: float = 1e-4,
    heatmap_weight: float = 1.0,
    coord_weight: float = 0.5,
    num_workers: int = 4,
    resume: Optional[str] = None,
    pixels_per_mm: float = 10.0,
):
    """Main training loop."""
    device = torch.device('cuda' if torch.cuda.is_available() else 'cpu')
    logger.info(f"Using device: {device}")

    output_dir = Path(output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    ckpt_dir = output_dir / 'checkpoints'
    ckpt_dir.mkdir(exist_ok=True)

    # ── Dataset ────────────────────────────────────────────────────────────
    full_dataset = CephDataset(
        data_dir=data_dir,
        format=format,
        annotation_file=annotation_file,
        transform=None,  # Will be set per-split
        split='full'
    )

    if len(full_dataset) == 0:
        logger.error("No samples found! Check data_dir and format.")
        return

    # Train/val split (90/10)
    n = len(full_dataset)
    n_val = max(1, n // 10)
    n_train = n - n_val
    train_indices, val_indices = random_split(range(n), [n_train, n_val])

    train_dataset = CephDataset(
        data_dir=data_dir, format=format, annotation_file=annotation_file,
        transform=get_train_transforms(), split='train'
    )
    val_dataset = CephDataset(
        data_dir=data_dir, format=format, annotation_file=annotation_file,
        transform=get_val_transforms(), split='val'
    )

    # Use Subset to apply transforms
    from torch.utils.data import Subset
    train_subset = Subset(train_dataset, list(train_indices))
    val_subset = Subset(val_dataset, list(val_indices))

    train_loader = DataLoader(
        train_subset, batch_size=batch_size, shuffle=True,
        num_workers=num_workers, pin_memory=True, drop_last=True
    )
    val_loader = DataLoader(
        val_subset, batch_size=batch_size, shuffle=False,
        num_workers=num_workers, pin_memory=True
    )

    logger.info(f"Training: {len(train_subset)} samples, Validation: {len(val_subset)} samples")

    # ── Model ──────────────────────────────────────────────────────────────
    model = CephLandmarkDetector().to(device)
    criterion = CombinedLoss(heatmap_weight=heatmap_weight, coord_weight=coord_weight)
    optimizer = optim.AdamW(model.parameters(), lr=lr, weight_decay=weight_decay)
    scheduler = optim.lr_scheduler.CosineAnnealingLR(optimizer, T_max=epochs, eta_min=lr * 0.01)

    start_epoch = 0
    best_sdr = 0.0

    # Resume from checkpoint
    if resume:
        if os.path.exists(resume):
            ckpt = torch.load(resume, map_location=device)
            model.load_state_dict(ckpt['model_state_dict'])
            optimizer.load_state_dict(ckpt['optimizer_state_dict'])
            start_epoch = ckpt.get('epoch', 0) + 1
            best_sdr = ckpt.get('best_sdr', 0.0)
            logger.info(f"Resumed from epoch {start_epoch}, best SDR@2mm: {best_sdr:.1f}%")
        else:
            logger.warning(f"Checkpoint not found: {resume}")

    # ── Training Loop ─────────────────────────────────────────────────────
    logger.info("Starting training...")
    for epoch in range(start_epoch, epochs):
        logger.info(f"\n{'='*60}\nEpoch {epoch+1}/{epochs}\n{'='*60}")

        train_metrics = train_one_epoch(model, train_loader, criterion, optimizer, device)
        val_metrics = validate(model, val_loader, criterion, device, pixels_per_mm)

        scheduler.step()

        logger.info(
            f"Train Loss: {train_metrics['loss']:.4f} "
            f"(HM: {train_metrics['heatmap_loss']:.4f}, Coord: {train_metrics['coord_loss']:.4f})"
        )
        logger.info(
            f"Val Loss: {val_metrics['loss']:.4f} | "
            f"MRE: {val_metrics['mre_mm']:.2f}mm | "
            f"SDR@2mm: {val_metrics['sdr_2mm']:.1f}% | "
            f"SDR@3mm: {val_metrics['sdr_3mm']:.1f}% | "
            f"SDR@4mm: {val_metrics['sdr_4mm']:.1f}%"
        )

        # Save best model
        if val_metrics['sdr_2mm'] > best_sdr:
            best_sdr = val_metrics['sdr_2mm']
            torch.save({
                'epoch': epoch,
                'model_state_dict': model.state_dict(),
                'optimizer_state_dict': optimizer.state_dict(),
                'best_sdr': best_sdr,
                'val_metrics': val_metrics,
            }, ckpt_dir / 'best_model.pth')
            logger.info(f"  ★ New best model! SDR@2mm: {best_sdr:.1f}%")

        # Save periodic checkpoint
        if (epoch + 1) % 10 == 0:
            torch.save({
                'epoch': epoch,
                'model_state_dict': model.state_dict(),
                'optimizer_state_dict': optimizer.state_dict(),
                'best_sdr': best_sdr,
                'val_metrics': val_metrics,
            }, ckpt_dir / f'checkpoint_epoch_{epoch+1}.pth')

    # ── Export to ONNX ────────────────────────────────────────────────────
    logger.info("\nExporting best model to ONNX...")
    best_ckpt = torch.load(ckpt_dir / 'best_model.pth', map_location=device)
    model.load_state_dict(best_ckpt['model_state_dict'])

    onnx_path = output_dir / 'ceph_landmarks.onnx'
    export_to_onnx(model, str(onnx_path), device)
    logger.info(f"ONNX model saved to: {onnx_path}")

    # Also copy to backend ai-models directory
    backend_model_dir = Path(__file__).parent.parent / 'backend' / 'ai-models'
    backend_model_dir.mkdir(parents=True, exist_ok=True)
    import shutil
    shutil.copy2(onnx_path, backend_model_dir / 'ceph_landmarks.onnx')
    logger.info(f"ONNX model also copied to: {backend_model_dir / 'ceph_landmarks.onnx'}")

    logger.info("\nTraining complete!")


def export_to_onnx(model: CephLandmarkDetector, output_path: str, device: torch.device):
    """Export the PyTorch model to ONNX format."""
    model.eval()
    dummy_input = torch.randn(1, 1, 512, 512, device=device)

    torch.onnx.export(
        model,
        dummy_input,
        output_path,
        export_params=True,
        opset_version=17,
        do_constant_folding=True,
        input_names=['image'],
        output_names=['heatmaps'],
        dynamic_axes={
            'image': {0: 'batch_size'},
            'heatmaps': {0: 'batch_size'}
        }
    )
    logger.info(f"ONNX export complete: {output_path}")


# ──────────────────────────────────────────────────────────────────────────────
# ISBI 2015 Dataset Download
# ──────────────────────────────────────────────────────────────────────────────

def download_isbi_dataset(output_dir: str):
    """
    Download the ISBI 2015 Cephalometric X-ray Challenge dataset.

    Note: The dataset requires registration. This function provides instructions
    and attempts to download if publicly accessible.
    """
    output_dir = Path(output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    logger.info("=" * 60)
    logger.info("ISBI 2015 Cephalometric X-ray Challenge Dataset")
    logger.info("=" * 60)
    logger.info("""
    The ISBI 2015 dataset contains:
    - 400 cephalometric X-ray images (1935x2400 pixels)
    - 24 landmarks annotated by 2 senior orthodontists
    - Training set: 150 images
    - Test set: 250 images

    To download:
    1. Visit: https://isbi-challenge.grand-challenge.org/Cephalometric/
    2. Register and download the dataset
    3. Extract to the specified output directory

    Expected directory structure after extraction:
        output_dir/
          RawImage/
            TrainingData/
              *.bmp (150 training images)
              Test1Data/
              *.bmp (100 test images)
            Test2Data/
              *.bmp (150 test images)
          400_senior/
            *.txt (400 annotation files, each with 24 lines of "x,y")

    Alternatively, if you find a direct download URL, provide it below.
    """)

    # Try known mirror URLs
    urls = [
        'https://isbi-challenge.grand-challenge.org/',
    ]

    for url in urls:
        logger.info(f"Dataset page: {url}")

    logger.info(f"Please download and extract the dataset to: {output_dir}")
    logger.info("Then run training with: python train.py --data_dir <output_dir> --format isbi")


# ──────────────────────────────────────────────────────────────────────────────
# Main
# ──────────────────────────────────────────────────────────────────────────────

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='Train Cephalometric Landmark Detector')
    parser.add_argument('--data_dir', type=str, required=True,
                        help='Path to dataset directory')
    parser.add_argument('--format', type=str, default='isbi', choices=['isbi', 'custom'],
                        help='Dataset format (isbi or custom)')
    parser.add_argument('--annotation_file', type=str, default=None,
                        help='Path to annotation JSON file (custom format only)')
    parser.add_argument('--output_dir', type=str, default='./output',
                        help='Output directory for checkpoints and models')
    parser.add_argument('--epochs', type=int, default=100,
                        help='Number of training epochs')
    parser.add_argument('--batch_size', type=int, default=8,
                        help='Batch size')
    parser.add_argument('--lr', type=float, default=1e-4,
                        help='Learning rate')
    parser.add_argument('--weight_decay', type=float, default=1e-4,
                        help='Weight decay')
    parser.add_argument('--heatmap_weight', type=float, default=1.0,
                        help='Weight for heatmap MSE loss')
    parser.add_argument('--coord_weight', type=float, default=0.5,
                        help='Weight for coordinate Wing loss')
    parser.add_argument('--num_workers', type=int, default=4,
                        help='Number of data loading workers')
    parser.add_argument('--resume', type=str, default=None,
                        help='Path to checkpoint to resume from')
    parser.add_argument('--pixels_per_mm', type=float, default=10.0,
                        help='Approximate pixels per mm for error calculation')
    parser.add_argument('--download_isbi', action='store_true',
                        help='Show instructions for downloading ISBI dataset')

    args = parser.parse_args()

    if args.download_isbi:
        download_isbi_dataset(args.data_dir)
    else:
        train(
            data_dir=args.data_dir,
            format=args.format,
            annotation_file=args.annotation_file,
            output_dir=args.output_dir,
            epochs=args.epochs,
            batch_size=args.batch_size,
            lr=args.lr,
            weight_decay=args.weight_decay,
            heatmap_weight=args.heatmap_weight,
            coord_weight=args.coord_weight,
            num_workers=args.num_workers,
            resume=args.resume,
            pixels_per_mm=args.pixels_per_mm,
        )
