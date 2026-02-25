"""
通用工具函数
"""
import json
import argparse
from pathlib import Path
from typing import Dict, Any


def load_config(config_path: str) -> Dict[str, Any]:
    """
    加载JSON配置文件
    
    Args:
        config_path: 配置文件路径
        
    Returns:
        配置字典
    """
    with open(config_path, 'r', encoding='utf-8') as f:
        return json.load(f)


def save_config(config: Dict[str, Any], output_path: str):
    """
    保存配置到JSON文件
    
    Args:
        config: 配置字典
        output_path: 输出路径
    """
    with open(output_path, 'w', encoding='utf-8') as f:
        json.dump(config, f, indent=2, ensure_ascii=False)


def parse_input_shape(shape_str: str) -> tuple:
    """
    解析输入形状字符串
    
    Args:
        shape_str: 形状字符串，如 "1,3,224,224"
        
    Returns:
        形状元组
    """
    return tuple(map(int, shape_str.split(',')))


def create_argparser() -> argparse.ArgumentParser:
    """
    创建命令行参数解析器
    
    Returns:
        参数解析器
    """
    parser = argparse.ArgumentParser(description='模型格式转换工具')
    
    parser.add_argument('--config', type=str, required=True,
                       help='配置文件路径（JSON格式）')
    parser.add_argument('--format', type=str, required=True,
                       choices=['onnx', 'torchscript'],
                       help='目标格式')
    
    return parser


def get_model_info(model_path: str) -> Dict[str, Any]:
    """
    从模型文件中提取信息
    
    Args:
        model_path: 模型路径
        
    Returns:
        模型信息字典
    """
    import torch
    
    try:
        # 尝试加载模型状态字典
        state_dict = torch.load(model_path, map_location='cpu', weights_only=True)
        
        # 提取信息
        info = {
            'num_params': sum(p.numel() for p in state_dict.values() if isinstance(p, torch.Tensor)),
            'keys': list(state_dict.keys())[:10],  # 前10个键
            'has_metadata': 'metadata' in state_dict
        }
        
        # 尝试提取元数据
        if 'metadata' in state_dict:
            info['metadata'] = state_dict['metadata']
        
        return info
        
    except Exception as e:
        return {'error': str(e)}


def format_size(size_bytes: int) -> str:
    """
    格式化文件大小
    
    Args:
        size_bytes: 字节数
        
    Returns:
        格式化的字符串
    """
    for unit in ['B', 'KB', 'MB', 'GB']:
        if size_bytes < 1024.0:
            return f"{size_bytes:.2f} {unit}"
        size_bytes /= 1024.0
    return f"{size_bytes:.2f} TB"
