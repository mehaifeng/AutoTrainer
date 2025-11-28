#!/usr/bin/env python3
"""
EfficientNet系列验证器
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

from Classification.base_classifier_validator import BaseClassificationValidator


class EfficientNetValidator(BaseClassificationValidator):
    """EfficientNet系列验证器"""
    
    # 模型配置
    MODEL_CONFIGS = {
        'efficientnet_b4': {'input_size': 380},
        'efficientnet_b5': {'input_size': 456},
        'efficientnet_b6': {'input_size': 528},
        'efficientnet_b7': {'input_size': 600},
        'efficientnet_v2_s': {'input_size': 384},
        'efficientnet_v2_m': {'input_size': 480},
        'efficientnet_v2_l': {'input_size': 480},
    }
    
    def __init__(self, config_path: str):
        """初始化验证器"""
        super().__init__(config_path)
        
        # 从模型路径推断模型名称
        model_path = self.config['model_weights_path']
        self.model_name = self._infer_model_name(model_path)
        
        if self.model_name not in self.MODEL_CONFIGS:
            raise ValueError(
                f"不支持的模型: {self.model_name}. "
                f"支持的模型: {list(self.MODEL_CONFIGS.keys())}"
            )
        
        self.model_config = self.MODEL_CONFIGS[self.model_name]
        print(f"使用模型: {self.model_name}", flush=True)
        print(f"输入尺寸: {self.model_config['input_size']}x{self.model_config['input_size']}", flush=True)
    
    def _infer_model_name(self, model_path: str) -> str:
        """从文件路径推断模型名称"""
        path_lower = model_path.lower()
        
        # 按照特定顺序检查，避免误匹配
        if 'efficientnet_v2_l' in path_lower:
            return 'efficientnet_v2_l'
        elif 'efficientnet_v2_m' in path_lower:
            return 'efficientnet_v2_m'
        elif 'efficientnet_v2_s' in path_lower:
            return 'efficientnet_v2_s'
        elif 'efficientnet_b7' in path_lower:
            return 'efficientnet_b7'
        elif 'efficientnet_b6' in path_lower:
            return 'efficientnet_b6'
        elif 'efficientnet_b5' in path_lower:
            return 'efficientnet_b5'
        elif 'efficientnet_b4' in path_lower:
            return 'efficientnet_b4'
        else:
            # 默认
            return 'efficientnet_b4'
    
    def get_input_size(self) -> int:
        """获取模型输入尺寸"""
        return self.model_config['input_size']
    
    def load_model(self, model_path: str, num_classes: int) -> nn.Module:
        """
        加载EfficientNet模型
        
        Args:
            model_path: 模型权重路径
            num_classes: 类别数
            
        Returns:
            加载好的模型
        """
        # 创建模型结构
        model = getattr(models, self.model_name)(weights=None)
        
        # 修改分类器
        in_features = model.classifier[1].in_features
        model.classifier = nn.Sequential(
            nn.Dropout(p=0.2, inplace=True),
            nn.Linear(in_features, num_classes)
        )
        
        # 加载权重
        checkpoint = torch.load(model_path, map_location='cpu')
        if isinstance(checkpoint, dict):
            if 'model_state_dict' in checkpoint:
                state_dict = checkpoint['model_state_dict']
            elif 'state_dict' in checkpoint:
                state_dict = checkpoint['state_dict']
            else:
                state_dict = checkpoint
        else:
            state_dict = checkpoint.state_dict() if hasattr(checkpoint, 'state_dict') else checkpoint
        
        model.load_state_dict(state_dict, strict=True)
        
        print(f"模型加载完成", flush=True)
        return model


def main():
    """主函数"""
    parser = argparse.ArgumentParser(description='EfficientNet验证器')
    parser.add_argument('--config', type=str, required=True,
                       help='配置文件路径')
    args = parser.parse_args()
    
    validator = EfficientNetValidator(args.config)
    validator.run()


if __name__ == '__main__':
    main()
