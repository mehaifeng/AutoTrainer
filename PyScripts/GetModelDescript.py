import torchvision.models as models
import inspect
from typing import Dict, Any

def get_model_info(model_name: str, model_func: Any) -> Dict[str, Any]:
    """获取模型的详细信息"""
    info = {
        'name': model_name,
        'docstring': inspect.getdoc(model_func),
        'signature': str(inspect.signature(model_func)),
        'parameters': {},
        'defaults': {}
    }
    
    # 获取函数参数信息
    sig = inspect.signature(model_func)
    for name, param in sig.parameters.items():
        info['parameters'][name] = str(param.annotation)
        if param.default is not param.empty:
            info['defaults'][name] = param.default
            
    return info

def analyze_models(model_name):
    """分析所有可用的模型"""
    # model_infos = {}
    # # 获取所有模型函数
    # for name, obj in inspect.getmembers(models):
    #     if inspect.isfunction(obj) and not name.startswith('_'):
    #         model_infos[name] = get_model_info(name, obj)
    
    # return model_infos
    model_info = get_model_info(model_name='resnet18', model_func=models.resnet18)
    print(model_info)
    return model_info

def print_model_info(model_info, detailed: bool = False):
    """打印模型信息"""
    #print(f"Found {len(model_infos)} models in torchvision.models\n")
    
    name, info = model_info
    for name, info in sorted(model_infos.items()):
        print(f"\n{'='*80}")
        print(f"Model: {name}")
        print(f"{'='*80}")
        
        # 打印文档字符串
        if info['docstring']:
            print("\nDescription:")
            print(info['docstring'])
        
        # 打印函数签名
        print("\nFunction signature:")
        print(info['signature'])
        
        if detailed:
            # 打印参数详细信息
            if info['parameters']:
                print("\nParameters:")
                for param_name, param_type in info['parameters'].items():
                    default = info['defaults'].get(param_name, 'Required')
                    print(f"  - {param_name}: {param_type}")
                    print(f"    Default value: {default}")

if __name__ == "__main__":
    # 获取所有模型信息
    model_infos = analyze_models()
    
    # 打印简要信息
    #print_model_info(model_infos)
    
    # 获取单个模型的详细信息示例
    print("\nExample: Detailed information for ResNet50")
    print_model_info(
        "resnet50",
        detailed=True
    )