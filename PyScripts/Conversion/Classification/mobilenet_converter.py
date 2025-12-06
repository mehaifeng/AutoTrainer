"""
MobileNet模型转换器
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


class MobileNetConverter(BaseConverter):
    """MobileNet系列模型转换器"""
    
    SUPPORTED_MODELS = [
        'mobilenet_v2',
        'mobilenet_v3_large',
        'mobilenet_v3_small'
    ]
    
    def __init__(self, config: Dict[str, Any]):
        """
        初始化MobileNet转换器
        
        Args:
            config: 转换配置
        """
        super().__init__(config)
        
        if self.model_name not in self.SUPPORTED_MODELS:
            raise ValueError(f"不支持的模型: {self.model_name}. "
                           f"支持的模型: {self.SUPPORTED_MODELS}")
    
    def load_model(self) -> nn.Module:
        """
        加载MobileNet模型
        
        Returns:
            加载的模型
        """
        self.logger.info(f"加载MobileNet模型: {self.model_name}")
        
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
        if self.model_name == 'mobilenet_v2':
            model = models.mobilenet_v2(weights=None)
            # 修改分类器
            model.classifier[1] = nn.Linear(model.classifier[1].in_features, num_classes)
        elif self.model_name == 'mobilenet_v3_large':
            model = models.mobilenet_v3_large(weights=None)
            model.classifier[3] = nn.Linear(model.classifier[3].in_features, num_classes)
        elif self.model_name == 'mobilenet_v3_small':
            model = models.mobilenet_v3_small(weights=None)
            model.classifier[3] = nn.Linear(model.classifier[3].in_features, num_classes)
        else:
            raise ValueError(f"不支持的模型: {self.model_name}")
        
        # 加载权重
        model.load_state_dict(state_dict)
        
        self.logger.info("✓ MobileNet模型加载成功")
        return model
    
    def _infer_num_classes(self, state_dict: dict) -> int:
        """
        从state_dict推断类别数
        
        Args:
            state_dict: 模型权重字典
            
        Returns:
            类别数
        """
        # 查找classifier层的权重
        classifier_key = None
        for key in state_dict.keys():
            if 'classifier' in key and 'weight' in key:
                classifier_key = key
                break
        
        if classifier_key is None:
            raise ValueError("无法在模型权重中找到classifier层")
        
        num_classes = state_dict[classifier_key].shape[0]
        self.logger.info(f"从权重推断类别数: {num_classes}")
        return num_classes
    
    def get_input_size(self) -> int:
        """获取MobileNet的输入尺寸"""
        return 224


def main():
    """主函数"""
    import argparse
    from common import load_config, parse_input_shape
    
    parser = argparse.ArgumentParser(description='MobileNet模型转换')
    parser.add_argument('--config', type=str, required=True, help='配置文件路径')
    parser.add_argument('--format', type=str, required=True, 
                       choices=['onnx', 'torchscript'], help='目标格式')
    
    args = parser.parse_args()
    
    # 加载配置
    config = load_config(args.config)
    
    # 创建转换器
    converter = MobileNetConverter(config)
    
    # 执行转换
    success = converter.convert(args.format)
    
    # 退出
    sys.exit(0 if success else 1)


if __name__ == '__main__':
    main()
