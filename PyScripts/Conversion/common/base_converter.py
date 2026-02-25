"""
模型转换基类
提供所有模型转换的通用框架
"""
import torch
import torch.onnx as onnx
from pathlib import Path
from typing import Dict, Any, Tuple, Optional
import json
import logging
import sys
from abc import ABC, abstractmethod


class BaseConverter(ABC):
    """模型转换基类"""
    
    def __init__(self, config: Dict[str, Any]):
        """
        初始化转换器
        
        Args:
            config: 转换配置字典
        """
        self.config = config
        self.model_path = config.get('model_path')
        self.output_path = config.get('output_path')
        self.model_name = config.get('model_name')
        self.input_shape = config.get('input_shape', (1, 3, 224, 224))
        self.device = torch.device('cuda' if torch.cuda.is_available() else 'cpu')
        
        # 日志配置
        self._setup_logging()
        
        self.logger.info(f"初始化 {self.__class__.__name__}")
        self.logger.info(f"模型路径: {self.model_path}")
        self.logger.info(f"输出路径: {self.output_path}")
        self.logger.info(f"设备: {self.device}")
    
    def _setup_logging(self):
        """配置日志系统"""
        self.logger = logging.getLogger(self.__class__.__name__)
        self.logger.setLevel(logging.INFO)
        
        # 控制台输出
        if not self.logger.handlers:
            handler = logging.StreamHandler(sys.stdout)
            handler.setLevel(logging.INFO)
            formatter = logging.Formatter('%(asctime)s - %(levelname)s - %(message)s')
            handler.setFormatter(formatter)
            self.logger.addHandler(handler)
    
    @abstractmethod
    def load_model(self) -> torch.nn.Module:
        """
        加载模型（子类必须实现）
        
        Returns:
            加载的PyTorch模型
        """
        raise NotImplementedError("子类必须实现 load_model 方法")
    
    def prepare_model_for_export(self, model: torch.nn.Module) -> torch.nn.Module:
        """
        准备模型用于导出（可选重写）
        
        Args:
            model: 原始模型
            
        Returns:
            准备好的模型
        """
        model.eval()
        model.to(self.device)
        return model
    
    def convert_to_onnx(self, model: torch.nn.Module, output_path: str) -> bool:
        """
        转换为ONNX格式
        
        Args:
            model: PyTorch模型
            output_path: 输出路径
            
        Returns:
            是否成功
        """
        try:
            self.logger.info("开始转换为ONNX格式...")
            
            # 创建示例输入
            dummy_input = torch.randn(self.input_shape).to(self.device)
            
            # ONNX导出参数
            opset_version = self.config.get('opset_version', 15)
            enable_dynamic = self.config.get('enable_dynamic_axes', False)
            
            # 根据配置决定是否使用 dynamic_axes
            export_kwargs = {
                'export_params': True,
                'opset_version': opset_version,
                'do_constant_folding': True,
                'input_names': ['input'],
                'output_names': ['output'],
                'verbose': False,
                'dynamo': False  # 禁用新的 dynamo 导出器
            }
            
            # 只有启用时才添加 dynamic_axes
            if enable_dynamic:
                export_kwargs['dynamic_axes'] = {
                    'input': {0: 'batch_size'},
                    'output': {0: 'batch_size'}
                }
                self.logger.info("✓ 启用动态输入尺寸支持")
            else:
                self.logger.info("使用固定输入尺寸")
            
            # 导出
            torch.onnx.export(
                model,
                dummy_input,
                output_path,
                **export_kwargs
            )
            
            self.logger.info(f"✓ ONNX模型已保存到: {output_path}")
            
            # 优化模型（如果启用）
            if self.config.get('optimize_model', False):
                output_path = self._optimize_onnx(output_path)
            
            # 验证ONNX模型
            if self._verify_onnx(output_path):
                self.logger.info("✓ ONNX模型验证通过")
                return True
            else:
                self.logger.warning("✗ ONNX模型验证失败")
                return False
                
        except Exception as e:
            self.logger.error(f"✗ ONNX转换失败: {str(e)}")
            return False
    
    def _optimize_onnx(self, onnx_path: str) -> str:
        """
        使用 onnxslim 优化 ONNX 模型
        
        Args:
            onnx_path: 原始 ONNX 模型路径
            
        Returns:
            优化后的模型路径
        """
        try:
            import onnxslim
            import os
            
            self.logger.info("开始优化 ONNX 模型...")
            
            # 获取原始文件大小
            original_size = os.path.getsize(onnx_path) / (1024 * 1024)  # MB
            
            # 优化后的文件路径
            optimized_path = onnx_path.replace('.onnx', '_optimized.onnx')
            
            # 使用 onnxslim 优化
            onnxslim.slim(
                onnx_path,
                optimized_path,
                skip_fusion_patterns=False,  # 启用算子融合
                skip_shape_inference=False,  # 启用形状推断
                skip_optimization=False,     # 启用优化
                verbose=False
            )
            
            # 获取优化后文件大小
            optimized_size = os.path.getsize(optimized_path) / (1024 * 1024)  # MB
            reduction = ((original_size - optimized_size) / original_size) * 100
            
            self.logger.info(f"✓ 模型优化完成")
            self.logger.info(f"  原始大小: {original_size:.2f} MB")
            self.logger.info(f"  优化后: {optimized_size:.2f} MB")
            self.logger.info(f"  减少: {reduction:.1f}%")
            
            # 删除原始文件，重命名优化后的文件
            os.remove(onnx_path)
            os.rename(optimized_path, onnx_path)
            
            return onnx_path
            
        except ImportError:
            self.logger.warning("✗ onnxslim 未安装，跳过优化")
            self.logger.warning("  安装命令: pip install onnxslim")
            return onnx_path
        except Exception as e:
            self.logger.warning(f"✗ 模型优化失败: {str(e)}")
            self.logger.warning("  使用未优化的模型")
            return onnx_path
    
    def _verify_onnx(self, onnx_path: str) -> bool:
        """
        验证ONNX模型
        
        Args:
            onnx_path: ONNX模型路径
            
        Returns:
            是否有效
        """
        try:
            import onnx
            model = onnx.load(onnx_path)
            onnx.checker.check_model(model)
            return True
        except Exception as e:
            self.logger.error(f"ONNX验证错误: {str(e)}")
            return False
    
    def convert_to_torchscript(self, model: torch.nn.Module, output_path: str) -> bool:
        """
        转换为TorchScript格式
        
        Args:
            model: PyTorch模型
            output_path: 输出路径
            
        Returns:
            是否成功
        """
        try:
            self.logger.info("开始转换为TorchScript格式...")
            
            # 创建示例输入
            example_input = torch.randn(self.input_shape).to(self.device)
            
            # 使用trace模式转换
            traced_model = torch.jit.trace(model, example_input)
            
            # 保存
            torch.jit.save(traced_model, output_path)
            
            self.logger.info(f"✓ TorchScript模型已保存到: {output_path}")
            
            # 验证加载
            if self._verify_torchscript(output_path):
                self.logger.info("✓ TorchScript模型验证通过")
                return True
            else:
                self.logger.warning("✗ TorchScript模型验证失败")
                return False
                
        except Exception as e:
            self.logger.error(f"✗ TorchScript转换失败: {str(e)}")
            return False
    
    def _verify_torchscript(self, ts_path: str) -> bool:
        """
        验证TorchScript模型
        
        Args:
            ts_path: TorchScript模型路径
            
        Returns:
            是否有效
        """
        try:
            loaded_model = torch.jit.load(ts_path)
            dummy_input = torch.randn(self.input_shape).to(self.device)
            _ = loaded_model(dummy_input)
            return True
        except Exception as e:
            self.logger.error(f"TorchScript验证错误: {str(e)}")
            return False
    
    def save_conversion_metadata(self, output_path: str, format_name: str):
        """
        保存转换元数据
        
        Args:
            output_path: 输出路径
            format_name: 格式名称
        """
        metadata = {
            'source_model': self.model_path,
            'model_name': self.model_name,
            'target_format': format_name,
            'input_shape': list(self.input_shape),
            'device': str(self.device),
            'conversion_config': self.config
        }
        
        metadata_path = Path(output_path).with_suffix('.json')
        with open(metadata_path, 'w', encoding='utf-8') as f:
            json.dump(metadata, f, indent=2, ensure_ascii=False)
        
        self.logger.info(f"元数据已保存到: {metadata_path}")
    
    def convert(self, target_format: str) -> bool:
        """
        执行转换
        
        Args:
            target_format: 目标格式 (onnx, torchscript等)
            
        Returns:
            是否成功
        """
        try:
            # 加载模型
            self.logger.info("正在加载模型...")
            model = self.load_model()
            
            # 准备模型
            self.logger.info("正在准备模型...")
            model = self.prepare_model_for_export(model)
            
            # 根据格式转换
            success = False
            if target_format.lower() == 'onnx':
                output_file = f"{self.output_path}.onnx"
                success = self.convert_to_onnx(model, output_file)
            elif target_format.lower() == 'torchscript':
                output_file = f"{self.output_path}.pt"
                success = self.convert_to_torchscript(model, output_file)
            else:
                self.logger.error(f"不支持的格式: {target_format}")
                return False
            
            # 保存元数据
            if success:
                self.save_conversion_metadata(output_file, target_format)
            
            return success
            
        except Exception as e:
            self.logger.error(f"转换过程失败: {str(e)}")
            import traceback
            traceback.print_exc()
            return False
