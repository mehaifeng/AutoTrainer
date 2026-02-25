"""
测试脚本：验证模型 metadata 读取
"""
import torch
import sys
import json


def test_model_metadata(model_path: str):
    """
    测试读取模型的 metadata
    
    Args:
        model_path: 模型文件路径
    """
    print(f"\n{'='*60}")
    print(f"测试模型: {model_path}")
    print(f"{'='*60}\n")
    
    try:
        # 加载checkpoint
        checkpoint = torch.load(model_path, map_location='cpu')
        
        # 检查类型
        print(f"1. Checkpoint类型: {type(checkpoint)}")
        
        if isinstance(checkpoint, dict):
            print(f"2. Checkpoint键列表:")
            for key in checkpoint.keys():
                if key != 'model_state_dict':  # 跳过权重字典
                    value = checkpoint[key]
                    if isinstance(value, (str, int, float)):
                        print(f"   - {key}: {value}")
                    elif isinstance(value, dict):
                        print(f"   - {key}: {type(value)} (字典，{len(value)} 个键)")
                    else:
                        print(f"   - {key}: {type(value)}")
            
            # 检查 model_name
            print(f"\n3. model_name 检查:")
            if 'model_name' in checkpoint:
                print(f"   ✓ 直接包含 'model_name': {checkpoint['model_name']}")
            else:
                print(f"   ✗ 不包含 'model_name'")
            
            # 检查 metadata
            print(f"\n4. metadata 字典检查:")
            if 'metadata' in checkpoint:
                print(f"   ✓ 包含 'metadata' 字典")
                metadata = checkpoint['metadata']
                for key, value in metadata.items():
                    print(f"      - {key}: {value}")
            else:
                print(f"   ✗ 不包含 'metadata' 字典")
            
            # 检查 num_classes
            print(f"\n5. num_classes 检查:")
            if 'num_classes' in checkpoint:
                print(f"   ✓ 直接包含 'num_classes': {checkpoint['num_classes']}")
            else:
                print(f"   ✗ 不包含 'num_classes'")
            
            # 检查 epoch
            print(f"\n6. epoch 检查:")
            if 'epoch' in checkpoint:
                print(f"   ✓ 包含 'epoch': {checkpoint['epoch']}")
            else:
                print(f"   ✗ 不包含 'epoch'")
            
            # 检查 metrics
            print(f"\n7. metrics 检查:")
            if 'metrics' in checkpoint:
                print(f"   ✓ 包含 'metrics': {checkpoint['metrics']}")
            else:
                print(f"   ✗ 不包含 'metrics'")
                
        else:
            print("   ✗ Checkpoint不是字典格式（可能是纯state_dict）")
        
        print(f"\n{'='*60}")
        print("测试完成")
        print(f"{'='*60}\n")
        
    except Exception as e:
        print(f"✗ 错误: {str(e)}")
        import traceback
        traceback.print_exc()


if __name__ == '__main__':
    if len(sys.argv) < 2:
        print("用法: python test_metadata.py <model_path>")
        print("示例: python test_metadata.py models/mobilenet_v3_large.pth")
        sys.exit(1)
    
    model_path = sys.argv[1]
    test_model_metadata(model_path)
