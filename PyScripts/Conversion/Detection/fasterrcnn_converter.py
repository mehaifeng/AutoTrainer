"""
Faster R-CNN模型转换器
"""
import torch
import torch.nn as nn
import torchvision.models.detection as detection
import sys
import os
from typing import Dict, Any

# 添加common模块到路径
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from common import BaseConverter


class FasterRCNNConverter(BaseConverter):
    """Faster R-CNN模型转换器"""
    
    SUPPORTED_BACKBONES = [
        'resnet50',
        'mobilenet_v3_large_320',
        'mobilenet_v3_large_fpn'
    ]
    
    def __init__(self, config: Dict[str, Any]):
        """
        初始化Faster R-CNN转换器
        
        Args:
            config: 转换配置
        """
        super().__init__(config)
        
        # 从model_name提取backbone信息
        # 格式: fasterrcnn_resnet50 或 fasterrcnn_mobilenet_v3_large
        if not self.model_name.startswith('fasterrcnn_'):
            raise ValueError(f"模型名称应该以'fasterrcnn_'开头，实际: {self.model_name}")
        
        self.backbone_name = self.model_name.replace('fasterrcnn_', '')
        
        if self.backbone_name not in self.SUPPORTED_BACKBONES:
            raise ValueError(f"不支持的backbone: {self.backbone_name}. "
                           f"支持的backbone: {self.SUPPORTED_BACKBONES}")
    
    def load_model(self) -> nn.Module:
        """
        加载Faster R-CNN模型
        
        Returns:
            加载的模型
        """
        self.logger.info(f"加载Faster R-CNN模型，backbone: {self.backbone_name}")
        
        # 加载checkpoint
        checkpoint = torch.load(self.model_path, map_location='cpu', weights_only=True)
        
        # 提取state_dict和类别数
        if isinstance(checkpoint, dict) and 'model_state_dict' in checkpoint:
            state_dict = checkpoint['model_state_dict']
            # 从checkpoint中读取类别数
            if 'num_classes' in checkpoint:
                num_classes_no_bg = checkpoint['num_classes']
                self.logger.info(f"从checkpoint元数据读取类别数（不含背景）: {num_classes_no_bg}")
                # 检测模型需要 +1 来包含背景类
                num_classes = num_classes_no_bg + 1
                self.logger.info(f"实际模型类别数（含背景）: {num_classes}")
            else:
                # 从state_dict推断（已经含背景）
                num_classes = self._infer_num_classes(state_dict)
        else:
            # 直接是state_dict
            state_dict = checkpoint
            num_classes = self._infer_num_classes(state_dict)
        
        # 创建模型架构
        if self.backbone_name == 'resnet50':
            model = detection.fasterrcnn_resnet50_fpn(weights=None, num_classes=num_classes)
        elif self.backbone_name == 'mobilenet_v3_large_320':
            model = detection.fasterrcnn_mobilenet_v3_large_320_fpn(weights=None, num_classes=num_classes)
        elif self.backbone_name == 'mobilenet_v3_large_fpn':
            model = detection.fasterrcnn_mobilenet_v3_large_fpn(weights=None, num_classes=num_classes)
        else:
            raise ValueError(f"不支持的backbone: {self.backbone_name}")
        
        # 加载权重
        model.load_state_dict(state_dict)
        
        self.logger.info("✓ Faster R-CNN模型加载成功")
        return model
    
    def _infer_num_classes(self, state_dict: dict) -> int:
        """
        从state_dict推断类别数
        
        注意：从权重推断的类别数已经包含背景类
        
        Args:
            state_dict: 模型权重字典
            
        Returns:
            类别数（含背景）
        """
        # 尝试从box_predictor权重推断
        if 'roi_heads.box_predictor.cls_score.weight' in state_dict:
            num_classes = state_dict['roi_heads.box_predictor.cls_score.weight'].shape[0]
            self.logger.info(f"从权重推断类别数（含背景）: {num_classes}")
            return num_classes
        else:
            # 默认COCO类别数
            num_classes = 91
            self.logger.warning(f"无法确定类别数，使用默认值: {num_classes}")
            return num_classes
    
    def prepare_model_for_export(self, model: nn.Module) -> nn.Module:
        """
        准备Faster R-CNN模型用于导出
        
        注意：检测模型在导出时需要特殊处理
        """
        model.eval()
        model.to(self.device)
        
        # 设置为评估模式，避免训练时的随机性
        for module in model.modules():
            if isinstance(module, nn.BatchNorm2d):
                module.eval()
        
        return model
    
    def convert_to_onnx(self, model: nn.Module, output_path: str) -> bool:
        """
        转换Faster R-CNN为ONNX格式
        
        检测模型的ONNX导出需要特殊配置
        """
        try:
            self.logger.info("开始转换Faster R-CNN为ONNX格式...")
            
            # 创建示例输入 - Faster R-CNN 需要单个张量（不是列表）
            dummy_input = torch.randn(self.input_shape).to(self.device)
            
            # ONNX导出参数
            opset_version = self.config.get('opset_version', 11)  # 检测模型建议使用11
            enable_dynamic = self.config.get('enable_dynamic_axes', False)
            
            # 根据配置决定是否使用 dynamic_axes
            export_kwargs = {
                'export_params': True,
                'opset_version': opset_version,
                'do_constant_folding': True,
                'input_names': ['images'],
                'output_names': ['boxes', 'labels', 'scores'],
                'verbose': False,
                'dynamo': False  # 禁用新的 dynamo 导出器
            }
            
            # 只有启用时才添加 dynamic_axes
            if enable_dynamic:
                export_kwargs['dynamic_axes'] = {
                    'images': {0: 'batch_size'},
                }
                self.logger.info("✓ 启用动态批次支持")
            else:
                self.logger.info("使用固定批次大小")
            
            # 导出
            torch.onnx.export(
                model,
                dummy_input,
                output_path,
                **export_kwargs
            )
            
            self.logger.info(f"✓ ONNX模型已保存到: {output_path}")
            self.logger.info("注意：检测模型的ONNX导出可能需要额外的后处理步骤")
            
            # 优化模型（如果启用）
            if self.config.get('optimize_model', False):
                output_path = self._optimize_onnx(output_path)
            
            return True
                
        except Exception as e:
            self.logger.error(f"✗ ONNX转换失败: {str(e)}")
            import traceback
            traceback.print_exc()
            return False
    
    def convert_to_torchscript(self, model: nn.Module, output_path: str) -> bool:
        """
        转换Faster R-CNN为TorchScript格式
        
        检测模型返回字典，需要使用script模式而非trace模式
        """
        try:
            self.logger.info("开始转换Faster R-CNN为TorchScript格式...")
            
            # 使用script模式转换（而非trace）
            # script模式可以处理字典输出
            scripted_model = torch.jit.script(model)
            
            # 保存
            torch.jit.save(scripted_model, output_path)
            
            self.logger.info(f"✓ TorchScript模型已保存到: {output_path}")
            
            # 验证加载
            try:
                loaded_model = torch.jit.load(output_path)
                self.logger.info("✓ TorchScript模型验证通过")
                return True
            except Exception as e:
                self.logger.warning(f"✗ TorchScript模型验证失败: {str(e)}")
                return False
                
        except Exception as e:
            self.logger.error(f"✗ TorchScript转换失败: {str(e)}")
            import traceback
            traceback.print_exc()
            return False
    
    def get_input_size(self) -> int:
        """获取Faster R-CNN的输入尺寸"""
        if '320' in self.backbone_name:
            return 320
        return 800  # ResNet50 FPN默认


def main():
    """主函数"""
    import argparse
    from common import load_config
    
    parser = argparse.ArgumentParser(description='Faster R-CNN模型转换')
    parser.add_argument('--config', type=str, required=True, help='配置文件路径')
    parser.add_argument('--format', type=str, required=True, 
                       choices=['onnx', 'torchscript'], help='目标格式')
    
    args = parser.parse_args()
    
    # 加载配置
    config = load_config(args.config)
    
    # 创建转换器
    converter = FasterRCNNConverter(config)
    
    # 执行转换
    success = converter.convert(args.format)
    
    # 退出
    sys.exit(0 if success else 1)


if __name__ == '__main__':
    main()
