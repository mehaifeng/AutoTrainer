"""
统一模型转换入口
根据模型名称自动选择合适的转换器
"""
import sys
import os
import json
import argparse
from pathlib import Path

# 添加模块路径
current_dir = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, current_dir)

from common import load_config, BaseConverter


# 模型到转换器的映射
CONVERTER_MAP = {
    # 分类模型 - MobileNet
    'mobilenet_v2': 'Classification.mobilenet_converter.MobileNetConverter',
    'mobilenet_v3_large': 'Classification.mobilenet_converter.MobileNetConverter',
    'mobilenet_v3_small': 'Classification.mobilenet_converter.MobileNetConverter',
    
    # 分类模型 - EfficientNet
    'efficientnet_b0': 'Classification.efficientnet_converter.EfficientNetConverter',
    'efficientnet_b1': 'Classification.efficientnet_converter.EfficientNetConverter',
    'efficientnet_b2': 'Classification.efficientnet_converter.EfficientNetConverter',
    'efficientnet_b3': 'Classification.efficientnet_converter.EfficientNetConverter',
    'efficientnet_b4': 'Classification.efficientnet_converter.EfficientNetConverter',
    'efficientnet_b5': 'Classification.efficientnet_converter.EfficientNetConverter',
    'efficientnet_b6': 'Classification.efficientnet_converter.EfficientNetConverter',
    'efficientnet_b7': 'Classification.efficientnet_converter.EfficientNetConverter',
    'efficientnet_v2_s': 'Classification.efficientnet_converter.EfficientNetConverter',
    'efficientnet_v2_m': 'Classification.efficientnet_converter.EfficientNetConverter',
    'efficientnet_v2_l': 'Classification.efficientnet_converter.EfficientNetConverter',
    
    # 分类模型 - ResNet
    'resnet18': 'Classification.resnet_converter.ResNetConverter',
    'resnet34': 'Classification.resnet_converter.ResNetConverter',
    'resnet50': 'Classification.resnet_converter.ResNetConverter',
    'resnet101': 'Classification.resnet_converter.ResNetConverter',
    'resnet152': 'Classification.resnet_converter.ResNetConverter',
    
    # 检测模型 - Faster R-CNN
    'fasterrcnn_resnet50': 'Detection.fasterrcnn_converter.FasterRCNNConverter',
    'fasterrcnn_mobilenet_v3_large_320': 'Detection.fasterrcnn_converter.FasterRCNNConverter',
    'fasterrcnn_mobilenet_v3_large_fpn': 'Detection.fasterrcnn_converter.FasterRCNNConverter',
}


def get_converter_class(model_name: str):
    """
    根据模型名称获取转换器类
    
    Args:
        model_name: 模型名称
        
    Returns:
        转换器类
    """
    if model_name not in CONVERTER_MAP:
        raise ValueError(f"不支持的模型: {model_name}\n"
                        f"支持的模型: {list(CONVERTER_MAP.keys())}")
    
    # 解析模块路径
    module_path = CONVERTER_MAP[model_name]
    parts = module_path.split('.')
    
    # 导入模块
    module_name = '.'.join(parts[:-1])
    class_name = parts[-1]
    
    module = __import__(module_name, fromlist=[class_name])
    converter_class = getattr(module, class_name)
    
    return converter_class


def main():
    """主函数"""
    parser = argparse.ArgumentParser(description='通用模型格式转换工具')
    parser.add_argument('--config', type=str, required=True,
                       help='配置文件路径（JSON格式）')
    parser.add_argument('--format', type=str, required=True,
                       choices=['onnx', 'torchscript'],
                       help='目标格式')
    
    args = parser.parse_args()
    
    try:
        # 加载配置
        config = load_config(args.config)
        
        # 获取模型名称
        model_name = config.get('model_name')
        if not model_name:
            print("错误：配置文件中缺少'model_name'字段", file=sys.stderr)
            sys.exit(1)
        
        print(f"模型名称: {model_name}")
        print(f"目标格式: {args.format}")
        print(f"配置文件: {args.config}")
        print("-" * 50)
        
        # 获取转换器类
        converter_class = get_converter_class(model_name)
        
        # 创建转换器实例
        converter = converter_class(config)
        
        # 执行转换
        success = converter.convert(args.format)
        
        # 输出结果
        if success:
            print("-" * 50)
            print("✓ 转换成功完成！")
            sys.exit(0)
        else:
            print("-" * 50)
            print("✗ 转换失败", file=sys.stderr)
            sys.exit(1)
            
    except Exception as e:
        print(f"✗ 转换过程发生错误: {str(e)}", file=sys.stderr)
        import traceback
        traceback.print_exc()
        sys.exit(1)


if __name__ == '__main__':
    main()
