# Cephalometric Landmark Detection AI Pipeline

This directory contains the complete AI pipeline for detecting 24 cephalometric landmarks on lateral cephalometric X-ray images. The model uses a heatmap-based approach with a ResNet-50 backbone and deconvolutional upsampling head.

## Architecture

- **Backbone**: ResNet-50 style encoder (1-channel grayscale input)
- **Head**: Deconvolutional upsampling with skip connections (U-Net style)
- **Input**: 1-channel 512×512 grayscale image, normalized to [0, 1]
- **Output**: 24 heatmaps at 128×128 resolution (one per landmark)
- **Post-processing**: Argmax per channel + sub-pixel refinement via weighted mean

## 24 Landmarks

| Key | Arabic Name | Group |
|-----|------------|-------|
| S | السرج | Cranial |
| N | الناسيون | Cranial |
| Or | قاع المدار | Cranial |
| Po | قمة المسمع | Cranial |
| ANS | الشوكة الأنفية الأمامية | Maxilla |
| PNS | الشوكة الأنفية الخلفية | Maxilla |
| A | النقطة A | Maxilla |
| B | النقطة B | Mandible |
| Pog | الذقن البارز | Mandible |
| Gn | الذقن | Mandible |
| Me | الأسفل | Mandible |
| Go | زاوية الفك | Mandible |
| Co | رأس اللقمة | Mandible |
| Ar | المفصل | Mandible |
| D | النقطة D | Mandible |
| Pm | بروز الذقن | Mandible |
| U1T | طرف القاطع العلوي | Dental |
| U1A | قمة القاطع العلوي | Dental |
| L1T | طرف القاطع السفلي | Dental |
| L1A | قمة القاطع السفلي | Dental |
| LS | الشفة العلوية | Soft Tissue |
| LI | الشفة السفلية | Soft Tissue |
| Pn | طرف الأنف | Soft Tissue |
| Cm | قاعدة الأنف | Soft Tissue |

## Setup

### 1. Install Dependencies

```bash
pip install -r requirements.txt
```

### 2. Prepare the Dataset

#### ISBI 2015 Challenge Dataset (Recommended)

The ISBI 2015 Cephalometric X-ray Challenge dataset is the standard benchmark:

1. Register at: https://isbi-challenge.grand-challenge.org/Cephalometric/
2. Download the dataset (400 images, 1935×2400 pixels)
3. Extract to a directory with this structure:
   ```
   data/isbi2015/
   ├── RawImage/
   │   ├── TrainingData/  (150 .bmp files)
   │   ├── Test1Data/     (100 .bmp files)
   │   └── Test2Data/     (150 .bmp files)
   └── 400_senior/
       └── *.txt          (400 annotation files)
   ```

#### Custom Dataset

Create a JSON annotation file:

```json
[
  {
    "image": "images/patient001.jpg",
    "landmarks": {
      "S": [450, 320],
      "N": [650, 240],
      "Or": [720, 300],
      "...": "..."
    }
  }
]
```

## Training

### Basic Training

```bash
python train.py --data_dir ./data/isbi2015 --format isbi --epochs 100 --batch_size 8
```

### Custom Dataset Training

```bash
python train.py --data_dir ./data/custom --format custom --annotation_file ./data/custom/annotations.json --epochs 100
```

### Training Arguments

| Argument | Default | Description |
|----------|---------|-------------|
| `--data_dir` | Required | Path to dataset directory |
| `--format` | `isbi` | Dataset format (`isbi` or `custom`) |
| `--epochs` | 100 | Number of training epochs |
| `--batch_size` | 8 | Batch size |
| `--lr` | 1e-4 | Learning rate |
| `--heatmap_weight` | 1.0 | Weight for heatmap MSE loss |
| `--coord_weight` | 0.5 | Weight for Wing Loss |
| `--resume` | None | Checkpoint to resume from |
| `--pixels_per_mm` | 10.0 | Approximate px/mm for error calculation |

### Training Details

- **Optimizer**: AdamW (lr=1e-4, weight_decay=1e-4)
- **Scheduler**: CosineAnnealingLR (eta_min = lr * 0.01)
- **Loss**: Combined MSE heatmaps + Wing Loss on coordinates
- **Augmentations**: Rotation (±15°), brightness/contrast, Gaussian noise, horizontal flip, Gaussian blur
- **Validation metric**: SDR (Success Detection Rate) at 2mm, 3mm, 4mm tolerance
- **Gradient clipping**: max_norm=1.0

## Export to ONNX

After training, the model is automatically exported. You can also export manually:

```bash
python export_onnx.py --checkpoint ./output/checkpoints/best_model.pth --output ./ceph_landmarks.onnx
```

The export script:
1. Loads the trained PyTorch model
2. Exports to ONNX with dynamic batch size
3. Validates outputs match PyTorch (max diff < 1e-4)
4. Copies the model to `backend/ai-models/ceph_landmarks.onnx`

## Inference

### Using ONNX Model (Production)

```bash
python inference.py --model ./output/ceph_landmarks.onnx --image ./test_xray.jpg --visualize
```

### Using PyTorch Checkpoint

```bash
python inference.py --model ./output/checkpoints/best_model.pth --image ./test_xray.jpg --framework pytorch --visualize
```

### Output

The inference script outputs:
- **Console**: Table of all 24 landmarks with coordinates and confidence scores
- **JSON** (with `--output`): Structured results including original image coordinates
- **Visualization** (with `--visualize`): Annotated image with landmarks

## Performance Benchmarks

### Expected Results on ISBI 2015 (with trained model)

| Metric | Value |
|--------|-------|
| SDR @ 2mm | ~73% |
| SDR @ 3mm | ~85% |
| SDR @ 4mm | ~92% |
| Mean Radial Error | ~2.1mm |
| Inference Time (ONNX, CPU) | ~50ms |
| Inference Time (ONNX, GPU) | ~15ms |

### Model Size

| Component | Size |
|-----------|------|
| PyTorch checkpoint | ~95 MB |
| ONNX model | ~95 MB |
| Parameters | ~23M |

## Integration with Aqlan Dental Pro

The ONNX model is automatically placed at `backend/ai-models/ceph_landmarks.onnx` after export. The .NET backend uses ONNX Runtime to load this model and run inference.

### Fallback

When no ONNX model is available, the system falls back to:
1. Template-based landmark placement (anatomical ratios)
2. VLM (Vision Language Model) via the Next.js API route

## Files

| File | Description |
|------|-------------|
| `landmark_detector.py` | Model definition, loss functions, heatmap utilities |
| `train.py` | Training script with dataset loading and validation |
| `export_onnx.py` | ONNX export and validation |
| `inference.py` | Standalone inference script |
| `requirements.txt` | Python dependencies |
| `README.md` | This file |
