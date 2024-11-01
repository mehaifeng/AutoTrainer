import torchvision.models as models
import inspect
from typing import Dict, Any, Optional, List
import json
import argparse
import sys
import os
from textwrap import fill

# 设置控制台输出编码
#if sys.platform == 'win32':
#    import codecs
#    sys.stdout = codecs.getwriter('utf-8')(sys.stdout.buffer)
#    os.environ['PYTHONIOENCODING'] = 'utf-8'

class ModelInfoAPI:
    @staticmethod
    def get_all_model_names() -> List[str]:
        """获取所有可用的模型名称"""
        return [
            name for name, obj in inspect.getmembers(models)
            if inspect.isfunction(obj) and not name.startswith('_')
        ]

    @staticmethod
    def get_model_info(model_name: str) -> Dict[str, Any]:
        """获取指定模型的详细信息"""
        try:
            model_func = getattr(models, model_name, None)
            if not model_func or not inspect.isfunction(model_func):
                return {"error": f"Model '{model_name}' not found"}

            sig = inspect.signature(model_func)
            
            params = {}
            for name, param in sig.parameters.items():
                param_info = {
                    "type": str(param.annotation),
                    "default": str(param.default) if param.default is not param.empty else "required",
                    "kind": str(param.kind)
                }
                params[name] = param_info

            info = {
                "name": model_name,
                "documentation": inspect.getdoc(model_func),
                "parameters": params,
                "signature": str(sig)
            }

            return info
        except Exception as e:
            return {"error": f"Error getting model info: {str(e)}"}

    @staticmethod
    def search_models(query: str) -> List[str]:
        """搜索模型名称（支持模糊匹配）"""
        all_models = ModelInfoAPI.get_all_model_names()
        return [name for name in all_models if query.lower() in name.lower()]

    @staticmethod
    def format_model_info(info: Dict[str, Any], width: int = 30) -> str:
        """格式化模型信息为易读的文本格式"""
        if "error" in info:
            return f"Error: {info['error']}"

        output = []
        
        # 模型名称
        output.append("###ModelInfo###")
        output.append("=" * width)
        output.append(f"模型名称: {info['name']}")
        output.append("=" * width)
        output.append("")
        
        # 函数签名
        output.append("方法签名:")
        output.append("-" * width)
        output.append(info['signature'])
        output.append("")
        
        # 文档
        if info['documentation']:
            output.append("文档:")
            output.append("-" * width)
            # 使用textwrap进行文本换行
            wrapped_doc = fill(info['documentation'], width=width, replace_whitespace=False)
            output.append(wrapped_doc)
            output.append("")
        
        # 参数详情
        output.append("参数详情:")
        output.append("-" * width)
        for param_name, param_info in info['parameters'].items():
            output.append(f"* {param_name}:")
            output.append(f"  Type: {param_info['type']}")
            output.append(f"  Default: {param_info['default']}")
            output.append("")
        output.append("###ModelInfo###")
        
        return "\n".join(output)

def main():
    parser = argparse.ArgumentParser(description='Get torchvision model information')
    
    # 添加子命令
    subparsers = parser.add_subparsers(dest='command', help='Available commands')
    
    # list 命令
    list_parser = subparsers.add_parser('list', help='List all available models')
    list_parser.add_argument('--format', choices=['simple', 'detailed'], 
                            default='simple', help='Output format')
    
    # search 命令
    search_parser = subparsers.add_parser('search', help='Search models')
    search_parser.add_argument('query', help='Search keyword')
    search_parser.add_argument('--format', choices=['simple', 'detailed'], 
                              default='simple', help='Output format')
    
    # info 命令
    info_parser = subparsers.add_parser('info', help='Get specific model information')
    info_parser.add_argument('model_name', help='Model name')
    info_parser.add_argument('--format', choices=['text', 'json'], 
                            default='text', help='Output format')
    info_parser.add_argument('--width', type=int, default=80, 
                            help='Output text width')
    
    # 解析参数
    args = parser.parse_args()
    
    try:
        # 如果没有提供命令，显示帮助信息
        if not args.command:
            parser.print_help()
            sys.exit(1)
        
        # 处理命令
        if args.command == 'list':
            models = ModelInfoAPI.get_all_model_names()
            print("\nAvailable Models:")
            if args.format == 'simple':
                print("###Models###")
                for model in sorted(models):
                    print(f"{model}")
                print("###Models###")
            else:
                for i, model in enumerate(sorted(models), 1):
                    print(f"{i:3d}. {model}")
            print(f"\nTotal: {len(models)} models")
            
        elif args.command == 'search':
            results = ModelInfoAPI.search_models(args.query)
            if results:
                print(f"\nFound {len(results)} matching models:")
                if args.format == 'simple':
                    print("###Models###")
                    for model in sorted(results):
                        print(f"{model}")
                    print("###Models###")
                else:
                    for i, model in enumerate(sorted(results), 1):
                        print(f"{i:3d}. {model}")
            else:
                print(f"\nNo models found matching '{args.query}'")
                
        elif args.command == 'info':
            info = ModelInfoAPI.get_model_info(args.model_name)
            if args.format == 'json':
                print(json.dumps(info, indent=2, ensure_ascii=False))
            else:
                print(ModelInfoAPI.format_model_info(info, args.width))
        
        # 确保输出被刷新
        sys.stdout.flush()
        
    except Exception as e:
        print(f"Error: {str(e)}", file=sys.stderr)
        sys.stderr.flush()
        sys.exit(1)

if __name__ == "__main__":
    main()