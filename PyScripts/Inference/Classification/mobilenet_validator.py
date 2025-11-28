#!/usr/bin/env python3
"""
MobileNet V3系列验证器
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

from Classification.base_classifier_validator import BaseClassificationValidator


class MobileNetValidator(BaseClassificationValidator):
    """MobileNet V3系列验证器"""
    
    # 模型配置
    MODEL_CONFIGS = {
        'mobilenet_v3_large': {
            'input_size': 224,
            'features_dim': 960,
            'last_channel': 1280
        },
        'mobilenet_v3_small': {
            'input_size': 224,
            'features_dim': 576,
            'last_channel': 1024
        },
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
        
        if 'mobilenet_v3_large' in path_lower:
            return 'mobilenet_v3_large'
        elif 'mobilenet_v3_small' in path_lower:
            return 'mobilenet_v3_small'
        elif 'large' in path_lower:
            return 'mobilenet_v3_large'
        elif 'small' in path_lower:
            return 'mobilenet_v3_small'
        else:
            # 默认
            return 'mobilenet_v3_large'
    
    def get_input_size(self) -> int:
        """获取模型输入尺寸"""
        return self.model_config['input_size']
    
    def load_model(self, model_path: str, num_classes: int) -> nn.Module:
        """
        加载MobileNet V3模型
        
        Args:
            model_path: 模型权重路径
            num_classes: 类别数
            
        Returns:
            加载好的模型
        """
        # 创建模型结构
        model = getattr(models, self.model_name)(weights=None)
        
        # 修改分类器（保留MobileNetV3的结构）
        last_channel = self.model_config['last_channel']
        model.classifier = nn.Sequential(
            nn.Linear(self.model_config['features_dim'], last_channel),
            nn.Hardswish(inplace=True),
            nn.Dropout(p=0.2, inplace=True),
            nn.Linear(last_channel, num_classes)
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
    parser = argparse.ArgumentParser(description='MobileNet V3验证器')
    parser.add_argument('--config', type=str, required=True,
                       help='配置文件路径')
    args = parser.parse_args()
    
    validator = MobileNetValidator(args.config)
    validator.run()


if __name__ == '__main__':
    main()
