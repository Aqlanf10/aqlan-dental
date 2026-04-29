#!/usr/bin/env python3
"""
Standalone Inference Script for Cephalometric Landmark Detection
================================================================

Runs landmark detection on a single cephalometric X-ray image using
either an ONNX model or PyTorch checkpoint.

Usage:
  # Using ONNX model (recommended for production)
  python inference.py --model ./output/ceph_landmarks.onnx --image ./test.jpg

  # Using PyTorch checkpoint
  python inference.py --model ./output/checkpoints/best_model.pth --image ./test.jpg --framework pytorch

  # With visualization
  python inference.py --model ./output/ceph_landmarks.onnx --image ./test.jpg --visualize
"""

import argparse
import json
import sys
from pathlib import Path
from typing import Dict, List, Tuple, Optional

import cv2
import numpy as np

from landmark_detector import (
    LANDMARK_KEYS, LANDMARK_NAMES_AR, NUM_LANDMARKS,
    heatmaps_to_landmarks
)


# ──────────────────────────────────────────────────────────────────────────────
# ONNX Inference
# ──────────────────────────────────────────────────────────────────────────────

class ONNXLandmarkDetector:
    """Landmark detector using ONNX Runtime."""

    INPUT_SIZE = 512
    HEATMAP_SIZE = 128

    def __init__(self, model_path: str, provider: str = 'CPUExecutionProvider'):
        """
        Args:
            model_path: Path to the ONNX model file
            provider: ONNX Runtime execution provider
        """
        import onnxruntime as ort

        self.session = ort.InferenceSession(
            model_path,
            providers=[provider]
        )
        self.input_name = self.session.get_inputs()[0].name
        self.output_name = self.session.get_outputs()[0].name

        # Warm up
        dummy = np.zeros((1, 1, self.INPUT_SIZE, self.INPUT_SIZE), dtype=np.float32)
        self.session.run([self.output_name], {self.input_name: dummy})

        print(f"ONNX model loaded: {model_path}")
        print(f"  Provider: {self.session.get_providers()}")
        print(f"  Input:  {self.input_name} {self.session.get_inputs()[0].shape}")
        print(f"  Output: {self.output_name} {self.session.get_outputs()[0].shape}")

    def detect(
        self,
        image_path: str,
        threshold: float = 0.1
    ) -> Dict[str, Dict[str, float]]:
        """
        Detect landmarks in a cephalometric X-ray image.

        Args:
            image_path: Path to the X-ray image
            threshold: Minimum confidence threshold

        Returns:
            Dict mapping landmark key → {
                'x': float, 'y': float,
                'confidence': float,
                'name_ar': str,
                'original_x': float, 'original_y': float
            }
            Coordinates are in original image space.
        """
        # Load and preprocess
        image = cv2.imread(image_path, cv2.IMREAD_GRAYSCALE)
        if image is None:
            raise ValueError(f"Failed to load image: {image_path}")

        original_h, original_w = image.shape[:2]

        # Resize to 512x512
        resized = cv2.resize(image, (self.INPUT_SIZE, self.INPUT_SIZE))

        # Normalize to [0, 1]
        normalized = resized.astype(np.float32) / 255.0

        # Convert to NCHW format
        input_tensor = normalized.reshape(1, 1, self.INPUT_SIZE, self.INPUT_SIZE)

        # Run inference
        outputs = self.session.run([self.output_name], {self.input_name: input_tensor})
        heatmaps_raw = outputs[0]  # (1, 24, 128, 128)

        # Apply sigmoid
        heatmaps_prob = 1.0 / (1.0 + np.exp(-heatmaps_raw))

        # Extract landmarks in 512x512 space
        landmarks_512 = heatmaps_to_landmarks(
            heatmaps_prob[0],
            image_width=self.INPUT_SIZE,
            image_height=self.INPUT_SIZE,
            threshold=threshold
        )

        # Map to original image coordinates
        results = {}
        scale_x = original_w / self.INPUT_SIZE
        scale_y = original_h / self.INPUT_SIZE

        for key, x, y, confidence in landmarks_512:
            orig_x = x * scale_x
            orig_y = y * scale_y
            idx = LANDMARK_KEYS.index(key) if key in LANDMARK_KEYS else -1
            name_ar = LANDMARK_NAMES_AR[idx] if idx >= 0 else key

            results[key] = {
                'x': round(orig_x, 1),
                'y': round(orig_y, 1),
                'x_512': round(x, 1),
                'y_512': round(y, 1),
                'confidence': round(confidence, 4),
                'name_ar': name_ar,
                'original_x': round(orig_x, 1),
                'original_y': round(orig_y, 1),
            }

        return results, (original_w, original_h)


# ──────────────────────────────────────────────────────────────────────────────
# PyTorch Inference
# ──────────────────────────────────────────────────────────────────────────────

class PyTorchLandmarkDetector:
    """Landmark detector using PyTorch."""

    INPUT_SIZE = 512

    def __init__(self, checkpoint_path: str):
        import torch
        from landmark_detector import CephLandmarkDetector

        self.device = torch.device('cuda' if torch.cuda.is_available() else 'cpu')
        self.model = CephLandmarkDetector().to(self.device)

        checkpoint = torch.load(checkpoint_path, map_location=self.device)
        if 'model_state_dict' in checkpoint:
            self.model.load_state_dict(checkpoint['model_state_dict'])
        else:
            self.model.load_state_dict(checkpoint)

        self.model.eval()
        print(f"PyTorch model loaded: {checkpoint_path}")

    def detect(self, image_path: str, threshold: float = 0.1):
        """Same interface as ONNXLandmarkDetector.detect()"""
        import torch

        image = cv2.imread(image_path, cv2.IMREAD_GRAYSCALE)
        if image is None:
            raise ValueError(f"Failed to load image: {image_path}")

        original_h, original_w = image.shape[:2]

        resized = cv2.resize(image, (self.INPUT_SIZE, self.INPUT_SIZE))
        normalized = resized.astype(np.float32) / 255.0
        input_tensor = torch.from_numpy(normalized).unsqueeze(0).unsqueeze(0).to(self.device)

        with torch.no_grad():
            heatmaps_raw = self.model(input_tensor)

        heatmaps_prob = torch.sigmoid(heatmaps_raw).cpu().numpy()

        landmarks_512 = heatmaps_to_landmarks(
            heatmaps_prob[0],
            image_width=self.INPUT_SIZE,
            image_height=self.INPUT_SIZE,
            threshold=threshold
        )

        results = {}
        scale_x = original_w / self.INPUT_SIZE
        scale_y = original_h / self.INPUT_SIZE

        for key, x, y, confidence in landmarks_512:
            orig_x = x * scale_x
            orig_y = y * scale_y
            idx = LANDMARK_KEYS.index(key) if key in LANDMARK_KEYS else -1
            name_ar = LANDMARK_NAMES_AR[idx] if idx >= 0 else key

            results[key] = {
                'x': round(orig_x, 1),
                'y': round(orig_y, 1),
                'x_512': round(x, 1),
                'y_512': round(y, 1),
                'confidence': round(confidence, 4),
                'name_ar': name_ar,
                'original_x': round(orig_x, 1),
                'original_y': round(orig_y, 1),
            }

        return results, (original_w, original_h)


# ──────────────────────────────────────────────────────────────────────────────
# Visualization
# ──────────────────────────────────────────────────────────────────────────────

LANDMARK_COLORS = {
    'cranial':  (96, 165, 250),   # Blue
    'maxilla':  (251, 146, 60),   # Orange
    'mandible': (248, 113, 113),  # Red
    'dental':   (52, 211, 153),   # Green
    'soft':     (244, 114, 182),  # Pink
}

LANDMARK_GROUPS = {
    'S': 'cranial', 'N': 'cranial', 'Or': 'cranial', 'Po': 'cranial',
    'ANS': 'maxilla', 'PNS': 'maxilla', 'A': 'maxilla',
    'B': 'mandible', 'Pog': 'mandible', 'Gn': 'mandible', 'Me': 'mandible',
    'Go': 'mandible', 'Co': 'mandible', 'Ar': 'mandible', 'D': 'mandible', 'Pm': 'mandible',
    'U1T': 'dental', 'U1A': 'dental', 'L1T': 'dental', 'L1A': 'dental',
    'LS': 'soft', 'LI': 'soft', 'Pn': 'soft', 'Cm': 'soft',
}


def visualize_landmarks(
    image_path: str,
    landmarks: Dict[str, Dict[str, float]],
    output_path: str,
    show_labels: bool = True,
    show_confidence: bool = True
):
    """Visualize detected landmarks on the X-ray image."""
    image = cv2.imread(image_path)
    if image is None:
        raise ValueError(f"Failed to load image: {image_path}")

    # Darken slightly for visibility
    overlay = image.copy()
    cv2.addWeighted(overlay, 0.85, np.zeros_like(overlay), 0, 0, image)

    for key, data in landmarks.items():
        x, y = int(data['x']), int(data['y'])
        confidence = data['confidence']
        group = LANDMARK_GROUPS.get(key, 'cranial')
        color = LANDMARK_COLORS.get(group, (255, 255, 255))

        # Draw point
        radius = 8 if confidence > 0.8 else 6
        cv2.circle(image, (x, y), radius, color, -1)
        cv2.circle(image, (x, y), radius, (255, 255, 255), 1)

        # Label
        if show_labels:
            label = f"{key}"
            if show_confidence:
                label += f" {confidence:.0%}"
            cv2.putText(image, label, (x + 10, y + 4),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.4, color, 1)

    cv2.imwrite(output_path, image)
    print(f"Visualization saved to: {output_path}")


# ──────────────────────────────────────────────────────────────────────────────
# Main
# ──────────────────────────────────────────────────────────────────────────────

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='Cephalometric Landmark Detection Inference')
    parser.add_argument('--model', type=str, required=True,
                        help='Path to ONNX model or PyTorch checkpoint')
    parser.add_argument('--image', type=str, required=True,
                        help='Path to cephalometric X-ray image')
    parser.add_argument('--framework', type=str, default='onnx', choices=['onnx', 'pytorch'],
                        help='Model framework (onnx or pytorch)')
    parser.add_argument('--output', type=str, default=None,
                        help='Output JSON file path')
    parser.add_argument('--visualize', action='store_true',
                        help='Generate visualization')
    parser.add_argument('--vis_output', type=str, default=None,
                        help='Output path for visualization image')
    parser.add_argument('--threshold', type=float, default=0.1,
                        help='Minimum confidence threshold')

    args = parser.parse_args()

    # Create detector
    if args.framework == 'onnx':
        detector = ONNXLandmarkDetector(args.model)
    else:
        detector = PyTorchLandmarkDetector(args.model)

    # Run inference
    print(f"\nRunning inference on: {args.image}")
    results, (img_w, img_h) = detector.detect(args.image, threshold=args.threshold)

    # Print results
    print(f"\nDetected {len(results)} landmarks (image size: {img_w}x{img_h}):")
    print(f"{'Key':4s}  {'X':>8s}  {'Y':>8s}  {'Conf':>6s}  Arabic Name")
    print("-" * 60)
    for key in LANDMARK_KEYS:
        if key in results:
            d = results[key]
            print(f"{key:4s}  {d['x']:8.1f}  {d['y']:8.1f}  {d['confidence']:6.1%}  {d['name_ar']}")
        else:
            print(f"{key:4s}  {'---':>8s}  {'---':>8s}  {'---':>6s}  Not detected")

    # Average confidence
    confidences = [d['confidence'] for d in results.values()]
    avg_conf = sum(confidences) / len(confidences) if confidences else 0
    print(f"\nAverage confidence: {avg_conf:.1%}")

    # Save JSON output
    if args.output:
        output_data = {
            'image': args.image,
            'image_width': img_w,
            'image_height': img_h,
            'landmarks': results,
            'average_confidence': avg_conf,
        }
        with open(args.output, 'w', encoding='utf-8') as f:
            json.dump(output_data, f, ensure_ascii=False, indent=2)
        print(f"Results saved to: {args.output}")

    # Visualize
    if args.visualize:
        vis_path = args.vis_output or str(Path(args.image).stem + '_landmarks.jpg')
        visualize_landmarks(args.image, results, vis_path)
