"""
Cephalometric Landmark Detection Model
=======================================

Heatmap-based landmark detection model for lateral cephalometric X-rays.
Architecture: ResNet-50 backbone (1-channel grayscale) + Deconvolutional upsampling head.
Input: 1-channel grayscale image, 512x512
Output: 24 heatmaps (one per landmark), each 128x128
"""

import torch
import torch.nn as nn
import torch.nn.functional as F
import numpy as np
from typing import List, Tuple, Dict, Optional

# ──────────────────────────────────────────────────────────────────────────────
# Landmark definitions — must match the 24 landmarks in the application
# ──────────────────────────────────────────────────────────────────────────────
LANDMARK_KEYS = [
    'S', 'N', 'Or', 'Po', 'ANS', 'PNS', 'A', 'B', 'Pog', 'Gn',
    'Me', 'Go', 'Co', 'Ar', 'D', 'Pm', 'U1T', 'U1A', 'L1T', 'L1A',
    'LS', 'LI', 'Pn', 'Cm'
]

NUM_LANDMARKS = len(LANDMARK_KEYS)  # 24

# Arabic names for each landmark (same order as LANDMARK_KEYS)
LANDMARK_NAMES_AR = [
    'السرج', 'الناسيون', 'قاع المدار', 'قمة المسمع',
    'الشوكة الأنفية الأمامية', 'الشوكة الأنفية الخلفية',
    'النقطة A', 'النقطة B', 'الذقن البارز', 'الذقن',
    'الأسفل', 'زاوية الفك', 'رأس اللقمة', 'المفصل',
    'النقطة D', 'بروز الذقن',
    'طرف القاطع العلوي', 'قمة القاطع العلوي',
    'طرف القاطع السفلي', 'قمة القاطع السفلي',
    'الشفة العلوية', 'الشفة السفلية',
    'طرف الأنف', 'قاعدة الأنف'
]


# ──────────────────────────────────────────────────────────────────────────────
# Model Architecture
# ──────────────────────────────────────────────────────────────────────────────

class ResNetBlock(nn.Module):
    """Basic residual block for the encoder."""

    def __init__(self, in_channels: int, out_channels: int, stride: int = 1):
        super().__init__()
        self.conv1 = nn.Conv2d(in_channels, out_channels, 3, stride=stride, padding=1, bias=False)
        self.bn1 = nn.BatchNorm2d(out_channels)
        self.conv2 = nn.Conv2d(out_channels, out_channels, 3, stride=1, padding=1, bias=False)
        self.bn2 = nn.BatchNorm2d(out_channels)
        self.relu = nn.ReLU(inplace=True)

        self.shortcut = nn.Sequential()
        if stride != 1 or in_channels != out_channels:
            self.shortcut = nn.Sequential(
                nn.Conv2d(in_channels, out_channels, 1, stride=stride, bias=False),
                nn.BatchNorm2d(out_channels)
            )

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        residual = self.shortcut(x)
        out = self.relu(self.bn1(self.conv1(x)))
        out = self.bn2(self.conv2(out))
        out += residual
        out = self.relu(out)
        return out


class CephEncoder(nn.Module):
    """
    ResNet-style encoder for grayscale cephalometric X-rays.
    Takes 1-channel 512x512 input and produces feature maps at 1/4, 1/8, 1/16, 1/32 scales.
    """

    def __init__(self):
        super().__init__()
        # Stem: 1-channel input → 64 channels
        self.stem = nn.Sequential(
            nn.Conv2d(1, 64, 7, stride=2, padding=3, bias=False),
            nn.BatchNorm2d(64),
            nn.ReLU(inplace=True),
            nn.MaxPool2d(3, stride=2, padding=1)
        )  # Output: 64 x 128 x 128

        # Stage 1: 64 → 64, no downsampling
        self.stage1 = self._make_stage(64, 64, num_blocks=3, stride=1)  # 64 x 128 x 128

        # Stage 2: 64 → 128, downsample
        self.stage2 = self._make_stage(64, 128, num_blocks=4, stride=2)  # 128 x 64 x 64

        # Stage 3: 128 → 256, downsample
        self.stage3 = self._make_stage(128, 256, num_blocks=6, stride=2)  # 256 x 32 x 32

        # Stage 4: 256 → 512, downsample
        self.stage4 = self._make_stage(256, 512, num_blocks=3, stride=2)  # 512 x 16 x 16

    def _make_stage(self, in_ch: int, out_ch: int, num_blocks: int, stride: int) -> nn.Sequential:
        layers = [ResNetBlock(in_ch, out_ch, stride)]
        for _ in range(num_blocks - 1):
            layers.append(ResNetBlock(out_ch, out_ch, 1))
        return nn.Sequential(*layers)

    def forward(self, x: torch.Tensor) -> Dict[str, torch.Tensor]:
        """
        Returns multi-scale feature maps for skip connections.

        Returns:
            Dictionary with keys 's1', 's2', 's3', 's4' for each scale.
        """
        x = self.stem(x)    # 64 x 128 x 128
        s1 = self.stage1(x)  # 64 x 128 x 128
        s2 = self.stage2(s1)  # 128 x 64 x 64
        s3 = self.stage3(s2)  # 256 x 32 x 32
        s4 = self.stage4(s3)  # 512 x 16 x 16

        return {'s1': s1, 's2': s2, 's3': s3, 's4': s4}


class DeconvHead(nn.Module):
    """
    Deconvolutional (transposed convolution) upsampling head.
    Takes multi-scale features from encoder and produces 24 heatmaps at 128x128.
    Uses skip connections from encoder stages.
    """

    def __init__(self, num_landmarks: int = NUM_LANDMARKS):
        super().__init__()

        # Upsample s4 (512 x 16x16) → 256 x 32x32
        self.deconv4 = nn.Sequential(
            nn.ConvTranspose2d(512, 256, 4, stride=2, padding=1, bias=False),
            nn.BatchNorm2d(256),
            nn.ReLU(inplace=True)
        )
        # Skip connection fusion: 256 (deconv) + 256 (s3) = 512 → 256
        self.fuse3 = nn.Sequential(
            nn.Conv2d(256 + 256, 256, 3, padding=1, bias=False),
            nn.BatchNorm2d(256),
            nn.ReLU(inplace=True)
        )

        # Upsample to 128 x 64x64
        self.deconv3 = nn.Sequential(
            nn.ConvTranspose2d(256, 128, 4, stride=2, padding=1, bias=False),
            nn.BatchNorm2d(128),
            nn.ReLU(inplace=True)
        )
        # Skip fusion: 128 (deconv) + 128 (s2) = 256 → 128
        self.fuse2 = nn.Sequential(
            nn.Conv2d(128 + 128, 128, 3, padding=1, bias=False),
            nn.BatchNorm2d(128),
            nn.ReLU(inplace=True)
        )

        # Upsample to 64 x 128x128
        self.deconv2 = nn.Sequential(
            nn.ConvTranspose2d(128, 64, 4, stride=2, padding=1, bias=False),
            nn.BatchNorm2d(64),
            nn.ReLU(inplace=True)
        )
        # Skip fusion: 64 (deconv) + 64 (s1) = 128 → 64
        self.fuse1 = nn.Sequential(
            nn.Conv2d(64 + 64, 64, 3, padding=1, bias=False),
            nn.BatchNorm2d(64),
            nn.ReLU(inplace=True)
        )

        # Final prediction head: 64 → num_landmarks
        self.head = nn.Sequential(
            nn.Conv2d(64, 64, 3, padding=1, bias=False),
            nn.BatchNorm2d(64),
            nn.ReLU(inplace=True),
            nn.Conv2d(64, num_landmarks, 1)
        )

    def forward(self, features: Dict[str, torch.Tensor]) -> torch.Tensor:
        """
        Args:
            features: Dictionary from encoder with 's1'..'s4'

        Returns:
            Heatmap tensor of shape (B, num_landmarks, 128, 128)
        """
        s1, s2, s3, s4 = features['s1'], features['s2'], features['s3'], features['s4']

        # Upsample and fuse
        x = self.deconv4(s4)                  # 256 x 32x32
        x = self.fuse3(torch.cat([x, s3], 1))  # 256 x 32x32

        x = self.deconv3(x)                    # 128 x 64x64
        x = self.fuse2(torch.cat([x, s2], 1))  # 128 x 64x64

        x = self.deconv2(x)                    # 64 x 128x128
        x = self.fuse1(torch.cat([x, s1], 1))  # 64 x 128x128

        heatmaps = self.head(x)                # num_landmarks x 128x128
        return heatmaps


class CephLandmarkDetector(nn.Module):
    """
    Full cephalometric landmark detection model.

    Architecture:
        - Encoder: ResNet-style backbone (grayscale input)
        - Head: Deconvolutional upsampling with skip connections
        - Output: 24 heatmaps at 128x128 resolution

    The heatmaps are then post-processed to extract landmark coordinates
    using argmax per channel.
    """

    INPUT_SIZE = 512
    HEATMAP_SIZE = 128
    SIGMA = 4.0  # Gaussian sigma for heatmap generation (in heatmap pixels)

    def __init__(self):
        super().__init__()
        self.encoder = CephEncoder()
        self.head = DeconvHead(num_landmarks=NUM_LANDMARKS)

        # Initialize weights
        self._init_weights()

    def _init_weights(self):
        """Initialize model weights using Kaiming initialization."""
        for m in self.modules():
            if isinstance(m, nn.Conv2d):
                nn.init.kaiming_normal_(m.weight, mode='fan_out', nonlinearity='relu')
                if m.bias is not None:
                    nn.init.zeros_(m.bias)
            elif isinstance(m, nn.BatchNorm2d):
                nn.init.ones_(m.weight)
                nn.init.zeros_(m.bias)
            elif isinstance(m, nn.ConvTranspose2d):
                nn.init.kaiming_normal_(m.weight, mode='fan_out', nonlinearity='relu')

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        """
        Forward pass.

        Args:
            x: Input tensor of shape (B, 1, 512, 512), normalized to [0, 1]

        Returns:
            Heatmaps of shape (B, 24, 128, 128), raw logits (before sigmoid)
        """
        features = self.encoder(x)
        heatmaps = self.head(features)
        return heatmaps

    def predict(self, x: torch.Tensor) -> List[Dict[str, Tuple[float, float, float]]]:
        """
        Run inference and extract landmark coordinates from heatmaps.

        Args:
            x: Input tensor of shape (B, 1, 512, 512), normalized to [0, 1]

        Returns:
            List (batch_size) of dicts mapping landmark key → (x, y, confidence)
            where x, y are in the 512x512 input coordinate space.
        """
        self.eval()
        with torch.no_grad():
            heatmaps = self.forward(x)
            # Apply sigmoid to get probabilities
            heatmaps_prob = torch.sigmoid(heatmaps)

        batch_size = heatmaps_prob.shape[0]
        results = []

        for b in range(batch_size):
            landmarks = {}
            for i, key in enumerate(LANDMARK_KEYS):
                hm = heatmaps_prob[b, i]  # (128, 128)

                # Find peak via argmax
                flat_idx = hm.argmax().item()
                peak_y = flat_idx // self.HEATMAP_SIZE
                peak_x = flat_idx % self.HEATMAP_SIZE
                confidence = hm[peak_y, peak_x].item()

                # Map back to input image coordinates (512x512)
                scale = self.INPUT_SIZE / self.HEATMAP_SIZE  # 4.0
                img_x = (peak_x + 0.5) * scale
                img_y = (peak_y + 0.5) * scale

                # Sub-pixel refinement: weighted mean around peak
                r = 2  # search radius
                y_min = max(0, peak_y - r)
                y_max = min(self.HEATMAP_SIZE, peak_y + r + 1)
                x_min = max(0, peak_x - r)
                x_max = min(self.HEATMAP_SIZE, peak_x + r + 1)

                region = hm[y_min:y_max, x_min:x_max]
                if region.sum() > 1e-6:
                    yy, xx = torch.meshgrid(
                        torch.arange(y_min, y_max, dtype=torch.float32, device=hm.device),
                        torch.arange(x_min, x_max, dtype=torch.float32, device=hm.device),
                        indexing='ij'
                    )
                    refined_x = (xx * region).sum() / region.sum()
                    refined_y = (yy * region).sum() / region.sum()
                    img_x = (refined_x.item() + 0.5) * scale
                    img_y = (refined_y.item() + 0.5) * scale

                landmarks[key] = (img_x, img_y, confidence)
            results.append(landmarks)

        return results


# ──────────────────────────────────────────────────────────────────────────────
# Heatmap Generation Utilities
# ──────────────────────────────────────────────────────────────────────────────

def generate_gaussian_heatmap(
    heatmap_size: int,
    center_x: float,
    center_y: float,
    sigma: float = 4.0
) -> np.ndarray:
    """
    Generate a 2D Gaussian heatmap centered at (center_x, center_y).

    Coordinates are in heatmap space (0 to heatmap_size-1).

    Args:
        heatmap_size: Size of the heatmap (e.g., 128)
        center_x: X coordinate of the landmark center (in heatmap pixels)
        center_y: Y coordinate of the landmark center (in heatmap pixels)
        sigma: Standard deviation of the Gaussian

    Returns:
        2D numpy array of shape (heatmap_size, heatmap_size)
    """
    x = np.arange(0, heatmap_size, dtype=np.float32)
    y = np.arange(0, heatmap_size, dtype=np.float32)
    xx, yy = np.meshgrid(x, y)

    heatmap = np.exp(-((xx - center_x) ** 2 + (yy - center_y) ** 2) / (2 * sigma ** 2))
    return heatmap


def landmarks_to_heatmaps(
    landmarks: Dict[str, Tuple[float, float]],
    image_width: int,
    image_height: int,
    heatmap_size: int = 128,
    sigma: float = 4.0
) -> np.ndarray:
    """
    Convert landmark coordinates to heatmap targets.

    Args:
        landmarks: Dict mapping landmark key → (x, y) in image coordinates
        image_width: Original image width
        image_height: Original image height
        heatmap_size: Output heatmap size (default 128)
        sigma: Gaussian sigma in heatmap pixels

    Returns:
        numpy array of shape (NUM_LANDMARKS, heatmap_size, heatmap_size)
    """
    heatmaps = np.zeros((NUM_LANDMARKS, heatmap_size, heatmap_size), dtype=np.float32)

    for i, key in enumerate(LANDMARK_KEYS):
        if key in landmarks:
            x, y = landmarks[key]
            # Map from image coordinates to heatmap coordinates
            hx = (x / image_width) * heatmap_size
            hy = (y / image_height) * heatmap_size
            heatmaps[i] = generate_gaussian_heatmap(heatmap_size, hx, hy, sigma)

    return heatmaps


def heatmaps_to_landmarks(
    heatmaps: np.ndarray,
    image_width: int,
    image_height: int,
    threshold: float = 0.1
) -> List[Tuple[str, float, float, float]]:
    """
    Extract landmark coordinates from predicted heatmaps.

    Args:
        heatmaps: numpy array of shape (NUM_LANDMARKS, heatmap_size, heatmap_size)
                  (after sigmoid, i.e., values in [0, 1])
        image_width: Original image width
        image_height: Original image height
        threshold: Minimum confidence threshold

    Returns:
        List of (key, x, y, confidence) tuples in image coordinates
    """
    heatmap_size = heatmaps.shape[1]
    scale_x = image_width / heatmap_size
    scale_y = image_height / heatmap_size
    results = []

    for i, key in enumerate(LANDMARK_KEYS):
        hm = heatmaps[i]
        flat_idx = np.argmax(hm)
        peak_y = flat_idx // heatmap_size
        peak_x = flat_idx % heatmap_size
        confidence = hm[peak_y, peak_x]

        if confidence < threshold:
            # Low confidence — still include but mark it
            pass

        # Sub-pixel refinement
        r = 2
        y_min = max(0, peak_y - r)
        y_max = min(heatmap_size, peak_y + r + 1)
        x_min = max(0, peak_x - r)
        x_max = min(heatmap_size, peak_x + r + 1)

        region = hm[y_min:y_max, x_min:x_max]
        if region.sum() > 1e-6:
            yy, xx = np.mgrid[y_min:y_max, x_min:x_max].astype(np.float32)
            refined_x = (xx * region).sum() / region.sum()
            refined_y = (yy * region).sum() / region.sum()
        else:
            refined_x = float(peak_x)
            refined_y = float(peak_y)

        img_x = (refined_x + 0.5) * scale_x
        img_y = (refined_y + 0.5) * scale_y

        results.append((key, img_x, img_y, float(confidence)))

    return results


# ──────────────────────────────────────────────────────────────────────────────
# Loss Functions
# ──────────────────────────────────────────────────────────────────────────────

class HeatmapMSELoss(nn.Module):
    """MSE loss between predicted and target heatmaps."""

    def __init__(self):
        super().__init__()
        self.mse = nn.MSELoss()

    def forward(self, pred: torch.Tensor, target: torch.Tensor) -> torch.Tensor:
        # Apply sigmoid to predictions first
        pred_prob = torch.sigmoid(pred)
        return self.mse(pred_prob, target)


class WingLoss(nn.Module):
    """
    Wing Loss for robust coordinate regression.

    Reference: Feng et al., "Wing Loss for Robust Facial Landmark Localisation"
    Better than L1/L2 for landmark localization because it emphasizes small errors.
    """

    def __init__(self, omega: float = 10.0, epsilon: float = 2.0):
        super().__init__()
        self.omega = omega
        self.epsilon = epsilon
        self.c = omega - omega * np.log(1 + omega / epsilon)

    def forward(
        self,
        pred_coords: torch.Tensor,
        target_coords: torch.Tensor,
        visibility: Optional[torch.Tensor] = None
    ) -> torch.Tensor:
        """
        Args:
            pred_coords: (B, N, 2) predicted coordinates
            target_coords: (B, N, 2) target coordinates
            visibility: (B, N) optional mask (1=visible, 0=ignore)
        """
        diff = torch.abs(pred_coords - target_coords)
        loss = torch.where(
            diff < self.omega,
            self.omega * torch.log(1 + diff / self.epsilon),
            diff - self.c
        )

        if visibility is not None:
            loss = loss * visibility.unsqueeze(-1)
            return loss.sum() / (visibility.sum() * 2 + 1e-6)

        return loss.mean()


class CombinedLoss(nn.Module):
    """
    Combined loss: MSE on heatmaps + Wing Loss on coordinates.

    The coordinate loss is computed by extracting coordinates from the
    predicted heatmaps and comparing to ground truth coordinates.
    """

    def __init__(self, heatmap_weight: float = 1.0, coord_weight: float = 0.5):
        super().__init__()
        self.heatmap_loss = HeatmapMSELoss()
        self.coord_loss = WingLoss()
        self.heatmap_weight = heatmap_weight
        self.coord_weight = coord_weight

    def forward(
        self,
        pred_heatmaps: torch.Tensor,
        target_heatmaps: torch.Tensor,
        target_coords: torch.Tensor,
        visibility: Optional[torch.Tensor] = None
    ) -> Dict[str, torch.Tensor]:
        """
        Args:
            pred_heatmaps: (B, 24, 128, 128) raw predictions (before sigmoid)
            target_heatmaps: (B, 24, 128, 128) Gaussian heatmaps
            target_coords: (B, 24, 2) ground truth coordinates in 512x512 space
            visibility: (B, 24) optional visibility mask

        Returns:
            Dictionary with 'total', 'heatmap', 'coord' losses
        """
        hm_loss = self.heatmap_loss(pred_heatmaps, target_heatmaps)

        # Extract predicted coordinates from heatmaps
        pred_prob = torch.sigmoid(pred_heatmaps)  # (B, 24, 128, 128)
        B = pred_prob.shape[0]
        pred_coords = torch.zeros(B, NUM_LANDMARKS, 2, device=pred_prob.device)

        for i in range(NUM_LANDMARKS):
            hm = pred_prob[:, i]  # (B, 128, 128)
            # Argmax per sample in batch
            flat_idx = hm.view(B, -1).argmax(dim=1)
            peak_y = flat_idx // 128
            peak_x = flat_idx % 128
            # Map to 512x512 space
            pred_coords[:, i, 0] = (peak_x.float() + 0.5) * (512.0 / 128)
            pred_coords[:, i, 1] = (peak_y.float() + 0.5) * (512.0 / 128)

        coord_loss = self.coord_loss(pred_coords, target_coords, visibility)

        total = self.heatmap_weight * hm_loss + self.coord_weight * coord_loss
        return {
            'total': total,
            'heatmap': hm_loss,
            'coord': coord_loss
        }


# ──────────────────────────────────────────────────────────────────────────────
# Convenience: model summary
# ──────────────────────────────────────────────────────────────────────────────

def count_parameters(model: nn.Module) -> int:
    """Count the number of trainable parameters."""
    return sum(p.numel() for p in model.parameters() if p.requires_grad)


if __name__ == '__main__':
    model = CephLandmarkDetector()
    print(f"CephLandmarkDetector — {count_parameters(model):,} trainable parameters")

    # Test forward pass
    x = torch.randn(2, 1, 512, 512)
    with torch.no_grad():
        heatmaps = model(x)
    print(f"Input:  {x.shape}")
    print(f"Output: {heatmaps.shape}")

    # Test prediction
    preds = model.predict(x[:1])
    print(f"Predicted landmarks for first sample:")
    for key, (px, py, conf) in preds[0].items():
        print(f"  {key:4s}: ({px:6.1f}, {py:6.1f}) conf={conf:.3f}")
