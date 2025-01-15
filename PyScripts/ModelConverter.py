import argparse
import torch
import torch.onnx as onnx
import torchvision.models as models
from pathlib import Path
import json
import logging
import sys
import onnx2tf

# 配置日志
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s',
    handlers=[
        logging.StreamHandler(sys.stdout)
    ]
)

# 支持的模型字典
SUPPORTED_MODELS = {
    'densenet': ['densenet121', 'densenet161', 'densenet169', 'densenet201'],
    'efficientnet': [
        'efficientnet_b0', 'efficientnet_b1', 'efficientnet_b2', 'efficientnet_b3',
        'efficientnet_b4', 'efficientnet_b5', 'efficientnet_b6', 'efficientnet_b7',
        'efficientnet_v2_l', 'efficientnet_v2_m', 'efficientnet_v2_s'
    ],
    'mobilenet': ['mobilenet_v2', 'mobilenet_v3_large', 'mobilenet_v3_small'],
    'resnet': ['resnet18', 'resnet34', 'resnet50', 'resnet101', 'resnet152'],
    'vgg': [
        'vgg11', 'vgg11_bn', 'vgg13', 'vgg13_bn',
        'vgg16', 'vgg16_bn', 'vgg19', 'vgg19_bn'
    ]
}

# 支持的转换格式
SUPPORTED_FORMATS = ['onnx', 'tensorflow']

def load_model(model_name: str, weights_path: str = None) -> torch.nn.Module:
    """
    加载指定的模型
    """
    try:
        if hasattr(models, model_name):
            if weights_path:
                state_dict = torch.load(weights_path,weights_only=True)
                classifier_weight_key = [k for k in state_dict.keys() if 'classifier' in k and 'weight' in k][-1]
                class_nums = state_dict[classifier_weight_key].shape[0]
                model = getattr(models, model_name)()
                model.classifier[-1] = torch.nn.Linear(model.classifier[-1].in_features, class_nums)
                model.load_state_dict(state_dict)
            else:
                model = getattr(models, model_name)(pretrained=True)
            return model
        else:
            raise ValueError(f"不支持的模型: {model_name}")
    except Exception as e:
        logging.error(f"加载模型时出错: {str(e)}")
        raise

def convert_to_onnx(model: torch.nn.Module, output_path: str, input_shape: tuple = (1, 3, 224, 224)):
    """
    转换为ONNX格式
    """
    try:
        dummy_input = torch.randn(input_shape)
        torch.onnx.export(
            model,
            dummy_input,
            output_path,
            verbose=True,
            input_names=['input'],
            output_names=['output'],
            dynamic_axes={
                'input': {0: 'batch_size'},
                'output': {0: 'batch_size'}
            }
        )
        logging.info(f"模型已成功转换为ONNX格式并保存到: {output_path}")
    except Exception as e:
        logging.error(f"转换ONNX时出错: {str(e)}")
        raise

def convert_to_torchscript(model: torch.nn.Module, output_path: str):
    """
    转换为TorchScript格式
    """
    try:
        scripted_model = torch.jit.script(model)
        torch.jit.save(scripted_model, output_path)
        logging.info(f"模型已成功转换为TorchScript格式并保存到: {output_path}")
    except Exception as e:
        logging.error(f"转换TorchScript时出错: {str(e)}")
        raise

def convert_to_tensorflow(model: torch.nn.Module, output_path: str):
    """
    转换为TensorFlow格式
    注意：需要先转换为ONNX，然后使用onnx2tf进行转换
    """
    try:
        # 首先转换为ONNX
        temp_onnx_path = output_path.replace('.pb', '.onnx')
        convert_to_onnx(model, temp_onnx_path)


        # 使用onnx2tf进行转换
        onnx2tf.convert(
            input_onnx_file_path=temp_onnx_path,
            output_folder_path=str(Path(output_path).parent),
            output_tfv1_pb=True,
            non_verbose=True
        )
        # 重命名 saved_model.pb 为目标文件名
        saved_model_path = Path(output_path).parent / 'saved_model.pb'
        if saved_model_path.exists():
            saved_model_path.rename(output_path)
        logging.info(f"模型已成功转换为TensorFlow格式并保存到: {output_path}")


        # 删除临时ONNX文件
        Path(temp_onnx_path).unlink()
    except Exception as e:
        logging.error(f"转换TensorFlow时出错: {str(e)}")
        raise

def convert_model(
    model_name: str,
    target_format: str,
    weights_path: str = None,
    output_path: str = None,
    input_shape: tuple = (1, 3, 224, 224)):
    """
    主转换函数
    """
    # 验证模型名称
    model_found = False
    for model_type in SUPPORTED_MODELS.values():
        if model_name in model_type:
            model_found = True
            break
    if not model_found:
        raise ValueError(f"不支持的模型: {model_name}")

    # 验证目标格式
    if target_format.lower() not in SUPPORTED_FORMATS:
        raise ValueError(f"不支持的转换格式: {target_format}")

    # 加载模型
    model = load_model(model_name, weights_path)
    model.eval()

    # 如果未指定输出路径，创建默认路径
    if output_path is None:
        output_dir = Path("converted_models")
        output_dir.mkdir(exist_ok=True)
        output_path = str(output_dir / f"{model_name}_{target_format}")

    # 根据目标格式选择转换方法
    if target_format.lower() == 'onnx':
        output_path = f"{output_path}.onnx"
        convert_to_onnx(model, output_path, input_shape)
    elif target_format.lower() == 'torchscript':
        output_path = f"{output_path}.pt"
        convert_to_torchscript(model, output_path)
    elif target_format.lower() == 'tensorflow':
        output_path = f"{output_path}.pb"
        convert_to_tensorflow(model, output_path)
    
    logging.info(f"模型转换完成！输出路径: {output_path}")

def main():
    parser = argparse.ArgumentParser(description='深度学习模型格式转换工具')
    parser.add_argument('--model', type=str, required=True, help='模型名称')
    parser.add_argument('--format', type=str, required=True, help='目标格式')
    parser.add_argument('--weights', type=str, help='权重文件路径(.pth)')
    parser.add_argument('--output', type=str, help='输出文件路径')
    parser.add_argument('--input_shape', type=str, default='1,3,224,224',
                      help='输入形状 (例如: 1,3,224,224)')
    parser.add_argument('--list_supported', action='store_true',
                      help='列出所有支持的模型和格式')

    args = parser.parse_args()

    if args.list_supported:
        print("\n支持的模型:")
        print(json.dumps(SUPPORTED_MODELS, indent=2))
        print("\n支持的转换格式:")
        print(json.dumps(SUPPORTED_FORMATS, indent=2))
        return

    try:
        # 解析输入形状
        input_shape = tuple(map(int, args.input_shape.split(',')))
        
        # 执行转换
        convert_model(
            model_name=args.model,
            target_format=args.format,
            weights_path=args.weights,
            output_path=args.output,
            input_shape=input_shape
        )
    except Exception as e:
        logging.error(f"转换过程中出错: {str(e)}")
        sys.exit(1)

if __name__ == '__main__':
    main()