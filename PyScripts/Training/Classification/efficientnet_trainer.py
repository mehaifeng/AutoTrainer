#!/usr/bin/env python3
"""
EfficientNet系列训练器
支持的模型：
- efficientnet_b4, efficientnet_b5, efficientnet_b6, efficientnet_b7
- efficientnet_v2_s, efficientnet_v2_m, efficientnet_v2_l
"""
import torch
import torch.nn as nn
from torchvision import models
import argparse
import sys
import os

# 添加父目录到路径
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from Classification.base_classifier import BaseClassificationTrainer
from common import StructuredLogger


class EfficientNetTrainer(BaseClassificationTrainer):
    """EfficientNet系列训练器"""
    
    # 模型配置
    MODEL_CONFIGS = {
        'efficientnet_b4': {
            'input_size': 380,
            'dropout': 0.4,
            'features_dim': 1792
        },
        'efficientnet_b5': {
            'input_size': 456,
            'dropout': 0.4,
            'features_dim': 2048
        },
        'efficientnet_b6': {
            'input_size': 528,
            'dropout': 0.5,
            'features_dim': 2304
        },
        'efficientnet_b7': {
            'input_size': 600,
            'dropout': 0.5,
            'features_dim': 2560
        },
        'efficientnet_v2_s': {
            'input_size': 384,
            'dropout': 0.2,
            'features_dim': 1280
        },
        'efficientnet_v2_m': {
            'input_size': 480,
            'dropout': 0.3,
            'features_dim': 1280
        },
        'efficientnet_v2_l': {
            'input_size': 480,
            'dropout': 0.4,
            'features_dim': 1280
        },
    }
    
    def __init__(self, config_path: str):
        """
        初始化EfficientNet训练器
        
        Args:
            config_path: 配置文件路径
        """
        super().__init__(config_path)
        
        # 验证模型名称
        model_name = self.config_parser.model_name
        if model_name not in self.MODEL_CONFIGS:
            raise ValueError(
                f"不支持的模型: {model_name}. "
                f"支持的模型: {list(self.MODEL_CONFIGS.keys())}"
            )
            
        self.model_config = self.MODEL_CONFIGS[model_name]
        StructuredLogger.info(f"使用模型配置: {self.model_config}")
        
    def get_input_size(self) -> int:
        """获取模型输入大小"""
        return self.model_config['input_size']
        
    def prepare_model(self) -> nn.Module:
        """
        准备EfficientNet模型
        
        Returns:
            配置好的模型
        """
        model_name = self.config_parser.model_name
        num_classes = self.config_parser.num_classes
        local_weights = self.config_parser.local_weights_path
        
        StructuredLogger.info(f"加载模型: {model_name}")
        
        # 加载预训练模型或本地权重
        if local_weights and os.path.exists(local_weights):
            StructuredLogger.info(f"从本地加载权重: {local_weights}")
            # 先创建模型结构
            model = getattr(models, model_name)(weights=None)
            # 修改分类器
            model = self._modify_classifier(model, num_classes)
            # 加载权重
            state_dict = torch.load(local_weights, map_location='cpu')
            if 'model_state_dict' in state_dict:
                state_dict = state_dict['model_state_dict']
            model.load_state_dict(state_dict)
        else:
            StructuredLogger.info("使用ImageNet预训练权重")
            # 加载预训练模型
            if model_name.startswith('efficientnet_v2'):
                # EfficientNetV2系列
                weights = getattr(models, f'EfficientNet_V2_{model_name.split("_")[-1].upper()}_Weights').DEFAULT
            else:
                # EfficientNet B系列
                weights = getattr(models, f'EfficientNet_{model_name.split("_")[-1].upper()}_Weights').DEFAULT
                
            model = getattr(models, model_name)(weights=weights)
            # 修改分类器
            model = self._modify_classifier(model, num_classes)
            
        StructuredLogger.info(f"模型准备完成，参数量: {self._count_parameters(model):,}")
        return model
        
    def _modify_classifier(self, model: nn.Module, num_classes: int) -> nn.Module:
        """
        修改分类器层
        
        Args:
            model: 原始模型
            num_classes: 类别数
            
        Returns:
            修改后的模型
        """
        # EfficientNet的分类器在classifier层
        in_features = model.classifier[1].in_features
        
        # 创建新的分类器
        model.classifier = nn.Sequential(
            nn.Dropout(p=self.model_config['dropout'], inplace=True),
            nn.Linear(in_features, num_classes)
        )
        
        return model
        
    @staticmethod
    def _count_parameters(model: nn.Module) -> int:
        """计算模型参数量"""
        return sum(p.numel() for p in model.parameters() if p.requires_grad)


def main():
    """主函数"""
    parser = argparse.ArgumentParser(description='EfficientNet训练器')
    parser.add_argument('--config', type=str, required=True,
                       help='配置文件路径')
    args = parser.parse_args()
    
    # 创建训练器并开始训练
    trainer = EfficientNetTrainer(args.config)
    trainer.train()


if __name__ == '__main__':
    main()
