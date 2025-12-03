"""
模型元数据读取工具
用于从模型文件中读取model_name元数据
"""
import torch
import sys
import json


def get_model_name(model_path: str) -> dict:
    """
    从模型文件中读取model_name
    
    Args:
        model_path: 模型文件路径
        
    Returns:
        结果字典，包含 success, model_name, message
    """
    try:
        # 加载模型检查点
        checkpoint = torch.load(model_path, map_location='cpu')
        
        # 如果是纯state_dict（没有元数据）
        if not isinstance(checkpoint, dict):
            return {
                'success': False,
                'model_name': None,
                'message': '模型文件不是字典格式，无法读取元数据'
            }
        
        # 尝试读取model_name
        if 'model_name' in checkpoint:
            model_name = checkpoint['model_name']
            return {
                'success': True,
                'model_name': model_name,
                'message': '成功读取模型名称'
            }
        else:
            return {
                'success': False,
                'model_name': None,
                'message': '模型文件中不包含model_name元数据'
            }
        
    except Exception as e:
        return {
            'success': False,
            'model_name': None,
            'message': f'读取模型文件时发生错误: {str(e)}'
        }


if __name__ == '__main__':
    if len(sys.argv) != 2:
        print(json.dumps({
            'success': False,
            'model_name': None,
            'message': '参数错误：需要提供模型路径'
        }))
        sys.exit(1)
    
    model_path = sys.argv[1]
    result = get_model_name(model_path)
    print(json.dumps(result, ensure_ascii=False))
