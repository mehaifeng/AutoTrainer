"""
Classification Training Scripts
分类训练脚本模块
"""

from .base_classifier import BaseClassificationTrainer
from .efficientnet_trainer import EfficientNetTrainer

__all__ = [
    'BaseClassificationTrainer',
    'EfficientNetTrainer',
]
