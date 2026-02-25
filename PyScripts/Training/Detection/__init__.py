"""
Detection Training Scripts
检测训练脚本模块
"""

from .base_detector import BaseDetectionTrainer
from .fasterrcnn_trainer import FasterRCNNTrainer

__all__ = [
    'BaseDetectionTrainer',
    'FasterRCNNTrainer',
]
