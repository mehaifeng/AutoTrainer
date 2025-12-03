#!/usr/bin/env python3
"""
Faster R-CNN系列验证器
支持的模型：
- fasterrcnn_resnet50_fpn
- fasterrcnn_mobilenet_v3_large_fpn
"""
import torch
import torch.nn as nn
from torchvision.models.detection import fasterrcnn_resnet50_fpn, fasterrcnn_mobilenet_v3_large_fpn
import argparse
import sys
import os

# 添加父目录到路径
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from Detection.base_detector_validator import BaseDetectionValidator


class FasterRCNNValidator(BaseDetectionValidator):
    """Faster R-CNN系列验证器"""
    
    def __init__(self, config_path: str):
        """初始化验证器"""
        super().__init__(config_path)
        
        # 从模型metadata读取模型变体（必须）
        model_path = self.config['model_weights_path']
        self.model_variant = self._get_model_variant_from_metadata(model_path)
        
        print(f"使用模型变体: {self.model_variant}", flush=True)
    
    def _get_model_variant_from_metadata(self, model_path: str) -> str:
        """
        从模型metadata读取模型变体（必须存在）
        
        Args:
            model_path: 模型文件路径
            
        Returns:
            模型变体名称
            
        Raises:
            ValueError: 如果metadata中不存在model_name或格式错误
        """
        try:
            checkpoint = torch.load(model_path, map_location='cpu')
            
            if not isinstance(checkpoint, dict):
                raise ValueError("模型文件格式错误：不是字典格式")
            
            if 'model_name' not in checkpoint:
                raise ValueError(
                    "模型文件缺少必需的元数据字段 'model_name'。\n"
                    "请使用本软件训练的模型，或确保模型包含正确的元数据。"
                )
            
            model_name = checkpoint['model_name']
            print(f"从metadata读取到模型名称: {model_name}", flush=True)
            
            # 根据model_name确定变体
            if 'mobilenet' in model_name.lower():
                return 'fasterrcnn_mobilenet_v3_large_fpn'
            else:
                return 'fasterrcnn_resnet50_fpn'
                
        except Exception as e:
            raise ValueError(f"读取模型metadata失败: {str(e)}")
    
    def get_model_name(self) -> str:
        """获取模型名称"""
        return self.model_variant
    
    def load_model(self, model_path: str, num_classes: int) -> nn.Module:
        """
        加载Faster R-CNN模型
        
        Args:
            model_path: 模型权重路径
            num_classes: 类别数（包含背景）
            
        Returns:
            加载好的模型
        """
        # 创建模型结构
        if self.model_variant == 'fasterrcnn_mobilenet_v3_large_fpn':
            model = fasterrcnn_mobilenet_v3_large_fpn(pretrained=False, num_classes=num_classes)
        else:
            model = fasterrcnn_resnet50_fpn(pretrained=False, num_classes=num_classes)
        
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
        
        model.load_state_dict(state_dict, strict=False)
        
        print(f"模型加载完成", flush=True)
        return model


def main():
    """主函数"""
    parser = argparse.ArgumentParser(description='Faster R-CNN验证器')
    parser.add_argument('--config', type=str, required=True,
                       help='配置文件路径')
    args = parser.parse_args()
    
    validator = FasterRCNNValidator(args.config)
    validator.run()


if __name__ == '__main__':
    main()
