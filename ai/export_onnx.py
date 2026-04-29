#!/usr/bin/env python3
"""
ONNX Export Script for Cephalometric Landmark Detection Model
=============================================================

Exports a trained PyTorch model to ONNX format and validates that
the ONNX model produces the same outputs as the PyTorch model.

Usage:
  python export_onnx.py --checkpoint ./output/checkpoints/best_model.pth
  python export_onnx.py --checkpoint ./output/checkpoints/best_model.pth --output ./model.onnx
"""

import argparse
import os
import sys
from pathlib import Path

import numpy as np
import torch
import onnx
import onnxruntime as ort

from landmark_detector import CephLandmarkDetector, NUM_LANDMARKS


def export_to_onnx(
    checkpoint_path: str,
    output_path: str,
    opset_version: int = 17,
    validate: bool = True
):
    """
    Load a trained PyTorch model and export to ONNX format.

    Args:
        checkpoint_path: Path to the PyTorch checkpoint (.pth)
        output_path: Output path for the ONNX model
        opset_version: ONNX opset version
        validate: Whether to validate the exported model
    """
    device = torch.device('cuda' if torch.cuda.is_available() else 'cpu')
    print(f"Device: {device}")

    # Load model
    print(f"Loading checkpoint: {checkpoint_path}")
    model = CephLandmarkDetector().to(device)

    checkpoint = torch.load(checkpoint_path, map_location=device)
    if 'model_state_dict' in checkpoint:
        model.load_state_dict(checkpoint['model_state_dict'])
        if 'best_sdr' in checkpoint:
            print(f"  Best SDR@2mm: {checkpoint['best_sdr']:.1f}%")
        if 'val_metrics' in checkpoint:
            metrics = checkpoint['val_metrics']
            print(f"  MRE: {metrics.get('mre_mm', 'N/A')}mm")
    else:
        model.load_state_dict(checkpoint)

    model.eval()

    # Ensure output directory exists
    output_path = Path(output_path)
    output_path.parent.mkdir(parents=True, exist_ok=True)

    # Create dummy input
    dummy_input = torch.randn(1, 1, 512, 512, device=device)

    # Export
    print(f"\nExporting to ONNX (opset {opset_version})...")
    torch.onnx.export(
        model,
        dummy_input,
        str(output_path),
        export_params=True,
        opset_version=opset_version,
        do_constant_folding=True,
        input_names=['image'],
        output_names=['heatmaps'],
        dynamic_axes={
            'image': {0: 'batch_size'},
            'heatmaps': {0: 'batch_size'}
        }
    )
    print(f"ONNX model saved to: {output_path}")

    # Verify the ONNX model graph
    print("\nVerifying ONNX model graph...")
    onnx_model = onnx.load(str(output_path))
    onnx.checker.check_model(onnx_model)
    print("  ✓ ONNX model graph is valid")

    # Print model info
    print(f"\nModel info:")
    print(f"  Input:  {onnx_model.graph.input[0].name}")
    print(f"  Output: {onnx_model.graph.output[0].name}")

    # Validate outputs
    if validate:
        print("\nValidating ONNX model outputs vs PyTorch...")
        validate_onnx(model, str(output_path), device)

    # Also copy to backend directory
    backend_dir = Path(__file__).parent.parent / 'backend' / 'ai-models'
    backend_dir.mkdir(parents=True, exist_ok=True)
    backend_path = backend_dir / 'ceph_landmarks.onnx'

    import shutil
    shutil.copy2(output_path, backend_path)
    print(f"\nModel also copied to: {backend_path}")

    return str(output_path)


def validate_onnx(
    model: CephLandmarkDetector,
    onnx_path: str,
    device: torch.device,
    num_tests: int = 5,
    tolerance: float = 1e-4
):
    """
    Validate that the ONNX model produces the same outputs as PyTorch.

    Args:
        model: PyTorch model
        onnx_path: Path to the ONNX model
        device: Torch device
        num_tests: Number of random tests
        tolerance: Maximum allowed absolute difference
    """
    # Create ONNX Runtime session
    sess_options = ort.SessionOptions()
    sess_options.graph_optimization_level = ort.GraphOptimizationLevel.ORT_ENABLE_ALL
    session = ort.InferenceSession(onnx_path, sess_options)

    input_name = session.get_inputs()[0].name
    output_name = session.get_outputs()[0].name

    max_diff = 0.0
    all_passed = True

    for test_idx in range(num_tests):
        # Random input
        test_input = np.random.randn(1, 1, 512, 512).astype(np.float32)

        # PyTorch inference
        with torch.no_grad():
            torch_input = torch.from_numpy(test_input).to(device)
            torch_output = model(torch_input)
            torch_output_np = torch.sigmoid(torch_output).cpu().numpy()

        # ONNX Runtime inference
        onnx_output = session.run([output_name], {input_name: test_input})[0]
        onnx_output_np = 1.0 / (1.0 + np.exp(-onnx_output))  # sigmoid

        # Compare
        diff = np.abs(torch_output_np - onnx_output_np).max()
        max_diff = max(max_diff, diff)

        passed = diff < tolerance
        status = "✓ PASS" if passed else "✗ FAIL"
        print(f"  Test {test_idx + 1}: max diff = {diff:.6f} {status}")

        if not passed:
            all_passed = False

    print(f"\n  Maximum difference across all tests: {max_diff:.6f}")
    print(f"  Overall: {'✓ ALL TESTS PASSED' if all_passed else '✗ SOME TESTS FAILED'}")

    return all_passed


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='Export model to ONNX')
    parser.add_argument('--checkpoint', type=str, required=True,
                        help='Path to PyTorch checkpoint (.pth)')
    parser.add_argument('--output', type=str,
                        default='./output/ceph_landmarks.onnx',
                        help='Output ONNX file path')
    parser.add_argument('--opset', type=int, default=17,
                        help='ONNX opset version')
    parser.add_argument('--no_validate', action='store_true',
                        help='Skip validation')

    args = parser.parse_args()

    export_to_onnx(
        checkpoint_path=args.checkpoint,
        output_path=args.output,
        opset_version=args.opset,
        validate=not args.no_validate
    )
