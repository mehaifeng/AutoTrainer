"""
ResNet模型转换器
"""
import torch
import torch.nn as nn
import torchvision.models as models
import sys
import os
from typing import Dict, Any

# 添加common模块到路径
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from common import BaseConverter


class ResNetConverter(BaseConverter):
    """ResNet系列模型转换器"""
    
    SUPPORTED_MODELS = [
        'resnet18', 'resnet34', 'resnet50', 'resnet101', 'resnet152'
    ]
    
    def __init__(self, config: Dict[str, Any]):
        """
        初始化ResNet转换器
        
        Args:
            config: 转换配置
        """
        super().__init__(config)
        
        if self.model_name not in self.SUPPORTED_MODELS:
            raise ValueError(f"不支持的模型: {self.model_name}. "
                           f"支持的模型: {self.SUPPORTED_MODELS}")
    
    def load_model(self) -> nn.Module:
        """
        加载ResNet模型
        
        Returns:
            加载的模型
        """
        self.logger.info(f"加载ResNet模型: {self.model_name}")
        
        # 加载checkpoint
        checkpoint = torch.load(self.model_path, map_location='cpu', weights_only=True)
        
        # 提取state_dict（可能在checkpoint字典中）
        if isinstance(checkpoint, dict) and 'model_state_dict' in checkpoint:
            state_dict = checkpoint['model_state_dict']
            # 从checkpoint中读取类别数
            if 'num_classes' in checkpoint:
                num_classes = checkpoint['num_classes']
                self.logger.info(f"从checkpoint元数据读取类别数: {num_classes}")
            else:
                # 从state_dict推断
                num_classes = self._infer_num_classes(state_dict)
        else:
            # 直接是state_dict
            state_dict = checkpoint
            num_classes = self._infer_num_classes(state_dict)
        
        # 创建模型架构
        model_func = getattr(models, self.model_name)
        model = model_func(weights=None)
        
        # 修改全连接层以匹配类别数
        in_features = model.fc.in_features
        model.fc = nn.Linear(in_features, num_classes)
        
        # 加载权重
        model.load_state_dict(state_dict)
        
        self.logger.info("✓ ResNet模型加载成功")
        return model
    
    def _infer_num_classes(self, state_dict: dict) -> int:
        """
        从state_dict推断类别数
        
        Args:
            state_dict: 模型权重字典
            
        Returns:
            类别数
        """
        fc_key = 'fc.weight'
        if fc_key not in state_dict:
            raise ValueError("无法在模型权重中找到fc层")
        
        num_classes = state_dict[fc_key].shape[0]
        self.logger.info(f"从权重推断类别数: {num_classes}")
        return num_classes
    
    def get_input_size(self) -> int:
        """获取ResNet的输入尺寸"""
        return 224


def main():
    """主函数"""
    import argparse
    from common import load_config
    
    parser = argparse.ArgumentParser(description='ResNet模型转换')
    parser.add_argument('--config', type=str, required=True, help='配置文件路径')
    parser.add_argument('--format', type=str, required=True, 
                       choices=['onnx', 'torchscript'], help='目标格式')
    
    args = parser.parse_args()
    
    # 加载配置
    config = load_config(args.config)
    
    # 创建转换器
    converter = ResNetConverter(config)
    
    # 执行转换
    success = converter.convert(args.format)
    
    # 退出
    sys.exit(0 if success else 1)


if __name__ == '__main__':
    main()
