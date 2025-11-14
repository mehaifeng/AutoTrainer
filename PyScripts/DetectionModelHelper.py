import torchvision.models.detection as detection
import torchvision.models as models
import inspect
from typing import Dict, Any, List
import json
import argparse
import sys
import os
from textwrap import fill

class DetectionModelInfoAPI:
    # 检测模型数据库 - 手动维护用户友好的信息
    DETECTION_MODEL_DATABASE = {
        'fasterrcnn_resnet50_fpn': {
            'name': 'Faster R-CNN with ResNet-50 FPN',
            'description': '两阶段目标检测器，结合区域建议网络和Fast R-CNN',
            'backbone': 'ResNet-50 + Feature Pyramid Network',
            'input_size': 'Variable, 建议 800x1333',
            'strengths': ['精度高', '适用于复杂场景', '小目标检测效果好'],
            'weaknesses': ['速度较慢', '内存占用大'],
            'use_cases': ['通用目标检测', '医学影像', '安防监控'],
            'paper_year': 2017,
            'mAP_coco': '37.0'  # COCO mAP@0.5:0.95
        },
        'fasterrcnn_resnet50_fpn_v2': {
            'name': 'Faster R-CNN with ResNet-50 FPN V2',
            'description': '改进版Faster R-CNN，使用新的权重初始化和训练策略',
            'backbone': 'ResNet-50 FPN V2',
            'input_size': 'Variable, 建议 800x1333',
            'strengths': ['比原版精度更高', '训练更稳定', '收敛更快'],
            'weaknesses': ['速度较慢', '内存占用大'],
            'use_cases': ['高精度检测任务', '学术研究', '竞赛基准'],
            'paper_year': 2021,
            'mAP_coco': '42.0'
        },
        'fasterrcnn_mobilenet_v3_large_fpn': {
            'name': 'Faster R-CNN with MobileNet V3 Large FPN',
            'description': '轻量级Faster R-CNN，使用MobileNet V3作为主干网络',
            'backbone': 'MobileNet V3 Large + FPN',
            'input_size': 'Variable, 建议 800x1333',
            'strengths': ['计算效率高', '适合移动端', '内存占用较小'],
            'weaknesses': ['精度相比ResNet稍低', '对小目标敏感度降低'],
            'use_cases': ['移动端检测', '实时应用', '边缘计算'],
            'paper_year': 2020,
            'mAP_coco': '32.5'
        },
        'fasterrcnn_mobilenet_v3_large_320_fpn': {
            'name': 'Faster R-CNN with MobileNet V3 Large 320 FPN',
            'description': '超轻量级Faster R-CNN，固定输入尺寸320x320',
            'backbone': 'MobileNet V3 Large + FPN',
            'input_size': '固定 320x320',
            'strengths': ['速度极快', '内存占用极小', '适合低端设备'],
            'weaknesses': ['精度明显降低', '不适用高精度场景'],
            'use_cases': ['低端移动设备', '超实时检测', '快速原型验证'],
            'paper_year': 2020,
            'mAP_coco': '22.8'
        },
        'ssd300_vgg16': {
            'name': 'SSD with VGG-16 (300x300)',
            'description': '单阶段目标检测器，直接预测边界框和类别',
            'backbone': 'VGG-16 + Extra Layers',
            'input_size': '固定 300x300',
            'strengths': ['速度快', '适合实时检测', '计算量小'],
            'weaknesses': ['小目标检测精度较低', '精度比两阶段模型低'],
            'use_cases': ['实时视频检测', '移动端部署', '自动驾驶'],
            'paper_year': 2016,
            'mAP_coco': '23.2'
        },
        'ssdlite320_mobilenet_v3_large': {
            'name': 'SSDlite with MobileNet V3 Large (320x320)',
            'description': '轻量级SSD，使用深度可分离卷积减少计算量',
            'backbone': 'MobileNet V3 Large',
            'input_size': '固定 320x320',
            'strengths': ['计算效率极高', '移动端优化', '速度快'],
            'weaknesses': ['精度相比普通SSD稍低', '对小目标不敏感'],
            'use_cases': ['移动端实时检测', '嵌入式设备', 'IoT应用'],
            'paper_year': 2019,
            'mAP_coco': '21.3'
        },
        'retinanet_resnet50_fpn': {
            'name': 'RetinaNet with ResNet-50 FPN',
            'description': '使用Focal Loss的单阶段检测器，解决正负样本不平衡问题',
            'backbone': 'ResNet-50 + Feature Pyramid Network',
            'input_size': 'Variable, 建议 800x1333',
            'strengths': ['平衡精度和速度', 'Focal Loss效果好', '无需预设锚框版本可用'],
            'weaknesses': ['训练相对复杂', '超参数敏感'],
            'use_cases': ['通用检测', '自动驾驶', '工业检测'],
            'paper_year': 2018,
            'mAP_coco': '34.4'
        },
        'retinanet_resnet50_fpn_v2': {
            'name': 'RetinaNet with ResNet-50 FPN V2',
            'description': '改进版RetinaNet，使用新的权重初始化策略',
            'backbone': 'ResNet-50 FPN V2',
            'input_size': 'Variable, 建议 800x1333',
            'strengths': ['比原版精度更高', '训练更稳定', '收敛更快'],
            'weaknesses': ['训练相对复杂', '超参数敏感'],
            'use_cases': ['高精度单阶段检测', '研究基准', '竞赛应用'],
            'paper_year': 2021,
            'mAP_coco': '38.9'
        },
        'fcos_resnet50_fpn': {
            'name': 'FCOS with ResNet-50 FPN',
            'description': 'Anchor-free检测器，无需预设锚框，直接预测目标位置',
            'backbone': 'ResNet-50 + Feature Pyramid Network',
            'input_size': 'Variable, 建议 800x1333',
            'strengths': ['无需锚框设计', '超参数较少', '对小目标友好'],
            'weaknesses': ['需要较多训练技巧', '在某些场景下精度不如锚框方法'],
            'use_cases': ['通用检测研究', '锚框替代方案', '简化训练流程'],
            'paper_year': 2020,
            'mAP_coco': '37.9'
        },
        'maskrcnn_resnet50_fpn': {
            'name': 'Mask R-CNN with ResNet-50 FPN',
            'description': '实例分割模型，同时提供目标检测和像素级分割',
            'backbone': 'ResNet-50 + Feature Pyramid Network',
            'input_size': 'Variable, 建议 800x1333',
            'strengths': ['同时检测和分割', '精度高', '功能全面'],
            'weaknesses': ['计算复杂度高', '推理速度慢', '内存占用大'],
            'use_cases': ['实例分割', '医学影像分析', '精细图像理解'],
            'paper_year': 2017,
            'mAP_coco': '37.2'
        },
        'maskrcnn_resnet50_fpn_v2': {
            'name': 'Mask R-CNN with ResNet-50 FPN V2',
            'description': '改进版Mask R-CNN，使用新的权重初始化和训练策略',
            'backbone': 'ResNet-50 FPN V2',
            'input_size': 'Variable, 建议 800x1333',
            'strengths': ['比原版精度更高', '分割质量更好', '训练更稳定'],
            'weaknesses': ['计算复杂度高', '推理速度慢'],
            'use_cases': ['高精度实例分割', '研究基准', '竞赛应用'],
            'paper_year': 2021,
            'mAP_coco': '42.5'
        },
        'keypointrcnn_resnet50_fpn': {
            'name': 'Keypoint R-CNN with ResNet-50 FPN',
            'description': '关键点检测模型，专门用于检测人体关键点',
            'backbone': 'ResNet-50 + Feature Pyramid Network',
            'input_size': 'Variable, 建议 800x1333',
            'strengths': ['关键点检测精度高', '多人场景效果好', '基于成熟的R-CNN架构'],
            'weaknesses': ['仅适用于关键点任务', '计算复杂度高'],
            'use_cases': ['人体姿态估计', '动作识别', '运动分析'],
            'paper_year': 2017,
            'mAP_coco': '53.9'  # 关键点AP
        }
    }

    @staticmethod
    def get_detection_model_info(model_name: str) -> Dict[str, Any]:
        """获取检测模型的详细信息"""
        if model_name in DetectionModelInfoAPI.DETECTION_MODEL_DATABASE:
            info = DetectionModelInfoAPI.DETECTION_MODEL_DATABASE[model_name].copy()
            info['task_type'] = 'detection'

            # 尝试获取torchvision的API信息作为补充
            try:
                if hasattr(detection, model_name):
                    model_func = getattr(detection, model_name)
                    if callable(model_func):
                        sig = inspect.signature(model_func)
                        info['torchvision_signature'] = str(sig)
                        info['torchvision_doc'] = inspect.getdoc(model_func)
            except:
                pass

            return info
        else:
            return {"error": f"Detection model '{model_name}' not found in database"}

    @staticmethod
    def get_classification_model_info(model_name: str) -> Dict[str, Any]:
        """获取分类模型的信息（兼容原有功能）"""
        try:
            model_func = getattr(models, model_name, None)
            if not model_func or not inspect.isfunction(model_func):
                return {"error": f"Model '{model_name}' not found"}

            sig = inspect.signature(model_func)

            info = {
                'name': model_name,
                'task_type': 'classification',
                'torchvision_signature': str(sig),
                'torchvision_doc': inspect.getdoc(model_func),
                'parameters': {}
            }

            # 添加用户友好的信息（可以扩展）
            CLASSIFICATION_MODEL_ENHANCEMENTS = {
                'resnet50': {
                    'display_name': 'ResNet-50',
                    'description': '深度残差网络，经典的CNN架构',
                    'input_size': '224x224',
                    'strengths': ['性能优秀', '训练稳定', '泛化能力强'],
                    'use_cases': ['通用图像分类', '特征提取']
                },
                'mobilenet_v2': {
                    'display_name': 'MobileNet V2',
                    'description': '轻量级网络，专为移动端优化',
                    'input_size': '224x224',
                    'strengths': ['计算量小', '速度快', '适合移动端'],
                    'use_cases': ['移动端应用', '边缘计算', '实时分类']
                },
                'efficientnet_b0': {
                    'display_name': 'EfficientNet-B0',
                    'description': '高效卷积网络，平衡准确率和计算效率',
                    'input_size': '224x224',
                    'strengths': ['高效', '准确率高', '参数量少'],
                    'use_cases': ['高效分类', '移动部署', '资源受限环境']
                }
            }

            if model_name in CLASSIFICATION_MODEL_ENHANCEMENTS:
                info.update(CLASSIFICATION_MODEL_ENHANCEMENTS[model_name])

            return info
        except Exception as e:
            return {"error": f"Error getting model info: {str(e)}"}

    @staticmethod
    def format_model_info(info: Dict[str, Any], width: int = 80) -> str:
        """格式化模型信息为用户友好的文本"""
        if "error" in info:
            return f"Error: {info['error']}"

        output = []
        output.append("###ModelInfo###")
        output.append("=" * width)

        # 根据任务类型格式化不同信息
        if info.get('task_type') == 'detection':
            output.append(f"检测模型: {info.get('name', 'Unknown')}")
            output.append("=" * width)
            output.append("")

            # 基本信息
            output.append("模型描述:")
            output.append(f"   {info.get('description', 'N/A')}")
            output.append("")

            output.append("架构:")
            output.append(f"   主干网络: {info.get('backbone', 'N/A')}")
            output.append(f"   输入尺寸: {info.get('input_size', 'N/A')}")
            output.append("")

            # 性能信息
            output.append("性能特点:")
            if 'strengths' in info:
                for strength in info['strengths']:
                    output.append(f"   [优点] {strength}")
            if 'weaknesses' in info:
                for weakness in info['weaknesses']:
                    output.append(f"   [缺点] {weakness}")
            output.append("")

            # 应用场景
            output.append("适用场景:")
            if 'use_cases' in info:
                for case in info['use_cases']:
                    output.append(f"   • {case}")
            output.append("")

            # 技术参数
            output.append("技术参数:")
            if 'paper_year' in info:
                output.append(f"   发表年份: {info['paper_year']}")
            if 'mAP_coco' in info:
                output.append(f"   COCO mAP: {info['mAP_coco']}")
            output.append("")

        else:  # 分类模型
            output.append(f"分类模型: {info.get('display_name', info.get('name', 'Unknown'))}")
            output.append("=" * width)
            output.append("")

            if 'description' in info:
                output.append("模型描述:")
                output.append(f"   {info['description']}")
                output.append("")

            output.append("基本信息:")
            output.append(f"   输入尺寸: {info.get('input_size', 'N/A')}")
            output.append("")

            if 'strengths' in info:
                output.append("性能特点:")
                for strength in info['strengths']:
                    output.append(f"   [优点] {strength}")
                output.append("")

            if 'use_cases' in info:
                output.append("适用场景:")
                for case in info['use_cases']:
                    output.append(f"   • {case}")
                output.append("")

        # 添加torchvision信息作为参考
        if 'torchvision_signature' in info:
            output.append("torchvision API:")
            output.append(f"   {info['torchvision_signature']}")
            output.append("")

        output.append("###ModelInfo###")
        return "\n".join(output)

def main():
    import argparse

    parser = argparse.ArgumentParser(description='Get model information for both classification and detection')

    subparsers = parser.add_subparsers(dest='command', help='Available commands')

    # list 命令 - 支持任务类型过滤
    list_parser = subparsers.add_parser('list', help='List available models')
    list_parser.add_argument('--task', choices=['classification', 'detection', 'all'],
                            default='all', help='Filter by task type')

    # info 命令 - 自动检测任务类型
    info_parser = subparsers.add_parser('info', help='Get specific model information')
    info_parser.add_argument('model_name', help='Model name')
    info_parser.add_argument('--width', type=int, default=80, help='Output width')

    args = parser.parse_args()

    if not args.command:
        parser.print_help()
        return

    if args.command == 'list':
        models_list = []

        if args.task in ['classification', 'all']:
            classification_models = ['resnet18', 'resnet50', 'resnet101', 'mobilenet_v2',
                                   'efficientnet_b0', 'vgg16', 'densenet121']
            models_list.extend([(m, 'classification') for m in classification_models])

        if args.task in ['detection', 'all']:
            detection_models = DetectionModelInfoAPI.DETECTION_MODEL_DATABASE.keys()
            models_list.extend([(m, 'detection') for m in detection_models])

        print("###Models###")
        for model_name, task_type in sorted(models_list):
            print(f"{model_name} [{task_type}]")
        print("###Models###")

    elif args.command == 'info':
        model_name = args.model_name.lower()

        # 尝试检测模型
        if model_name in ['fasterrcnn', 'ssd', 'retinanet', 'fcos', 'maskrcnn', 'keypointrcnn'] or \
           any(keyword in model_name for keyword in ['fasterrcnn', 'maskrcnn', 'ssd']):
            info = DetectionModelInfoAPI.get_detection_model_info(model_name)
        else:
            info = DetectionModelInfoAPI.get_classification_model_info(model_name)

        print(DetectionModelInfoAPI.format_model_info(info, args.width))

if __name__ == "__main__":
    main()