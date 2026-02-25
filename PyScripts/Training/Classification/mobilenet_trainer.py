#!/usr/bin/env python3
"""
MobileNet V3系列训练器
支持的模型：
- mobilenet_v3_large
- mobilenet_v3_small
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


class MobileNetTrainer(BaseClassificationTrainer):
    """MobileNet V3系列训练器"""
    
    # 模型配置
    MODEL_CONFIGS = {
        'mobilenet_v3_large': {
            'input_size': 224,
            'dropout': 0.2,
            'features_dim': 960,
            'last_channel': 1280
        },
        'mobilenet_v3_small': {
            'input_size': 224,
            'dropout': 0.2,
            'features_dim': 576,
            'last_channel': 1024
        },
    }
    
    def __init__(self, config_path: str):
        """
        初始化MobileNet训练器
        
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
        准备MobileNet V3模型
        
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
            # MobileNet_V3_Large_Weights 或 MobileNet_V3_Small_Weights
            variant = 'Large' if 'large' in model_name else 'Small'
            weights_class = getattr(models, f'MobileNet_V3_{variant}_Weights')
            weights = weights_class.DEFAULT
                
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
        # MobileNetV3的分类器结构：
        # classifier:
        #   (0): Linear(in_features=960/576, out_features=1280/1024, bias=True)
        #   (1): Hardswish()
        #   (2): Dropout(p=0.2, inplace=True)
        #   (3): Linear(in_features=1280/1024, out_features=1000, bias=True)
        
        # 获取第一个Linear层的输出特征数（即last_channel）
        last_channel = model.classifier[0].out_features
        
        # 创建新的分类器
        model.classifier = nn.Sequential(
            nn.Linear(self.model_config['features_dim'], last_channel),
            nn.Hardswish(inplace=True),
            nn.Dropout(p=self.model_config['dropout'], inplace=True),
            nn.Linear(last_channel, num_classes)
        )
        
        return model
        
    @staticmethod
    def _count_parameters(model: nn.Module) -> int:
        """计算模型参数量"""
        return sum(p.numel() for p in model.parameters() if p.requires_grad)


def main():
    """主函数"""
    parser = argparse.ArgumentParser(description='MobileNet V3训练器')
    parser.add_argument('--config', type=str, required=True,
                       help='配置文件路径')
    args = parser.parse_args()
    
    # 创建训练器并开始训练
    trainer = MobileNetTrainer(args.config)
    trainer.train()


if __name__ == '__main__':
    main()
