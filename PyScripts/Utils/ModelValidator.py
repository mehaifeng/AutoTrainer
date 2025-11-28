"""
模型验证工具
用于验证本地模型权重与选择的预训练模型是否一致
"""
import torch
import sys
import json


def validate_model_consistency(model_path: str, expected_model_name: str) -> dict:
    """
    验证模型一致性
    
    Args:
        model_path: 模型文件路径
        expected_model_name: 期望的模型名称
        
    Returns:
        验证结果字典，包含 valid, model_name, message
    """
    try:
        # 加载模型检查点
        checkpoint = torch.load(model_path, map_location='cpu')
        
        # 如果是纯state_dict（没有元数据）
        if not isinstance(checkpoint, dict) or 'model_state_dict' not in checkpoint:
            return {
                'valid': False,
                'model_name': None,
                'message': '警告：模型文件不包含元数据信息。\n可能不是由本软件训练生成，建议谨慎使用。'
            }
        
        # 获取模型名称
        model_name = checkpoint.get('model_name', None)
        
        if model_name is None:
            return {
                'valid': False,
                'model_name': None,
                'message': '警告：模型文件缺少模型名称信息。\n可能不是由本软件训练生成，建议谨慎使用。'
            }
        
        # 验证模型名称是否一致
        if model_name != expected_model_name:
            return {
                'valid': False,
                'model_name': model_name,
                'message': f'错误：模型不匹配！\n' +
                          f'选择的模型：{expected_model_name}\n' +
                          f'权重文件的模型：{model_name}\n' +
                          f'请选择正确的模型权重文件。'
            }
        
        # 验证通过
        return {
            'valid': True,
            'model_name': model_name,
            'message': '模型验证通过'
        }
        
    except Exception as e:
        return {
            'valid': False,
            'model_name': None,
            'message': f'错误：无法读取模型文件\n{str(e)}'
        }


if __name__ == '__main__':
    if len(sys.argv) != 3:
        print(json.dumps({
            'valid': False,
            'model_name': None,
            'message': '参数错误：需要提供模型路径和期望的模型名称'
        }))
        sys.exit(1)
    
    model_path = sys.argv[1]
    expected_model_name = sys.argv[2]
    
    result = validate_model_consistency(model_path, expected_model_name)
    print(json.dumps(result, ensure_ascii=False))
