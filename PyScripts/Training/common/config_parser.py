"""
通用配置解析器
从JSON配置文件加载训练参数
"""
import json
from typing import Dict, Any, Optional
from pathlib import Path


class ConfigParser:
    """配置文件解析器"""
    
    def __init__(self, config_path: str):
        """
        初始化配置解析器
        
        Args:
            config_path: JSON配置文件路径
        """
        self.config_path = Path(config_path)
        self.config = self._load_config()
        
    def _load_config(self) -> Dict[str, Any]:
        """加载并验证配置文件"""
        if not self.config_path.exists():
            raise FileNotFoundError(f"配置文件不存在: {self.config_path}")
            
        with open(self.config_path, 'r', encoding='utf-8') as f:
            config = json.load(f)
            
        # 验证必需字段
        self._validate_config(config)
        return config
        
    def _validate_config(self, config: Dict[str, Any]) -> None:
        """验证配置文件必需字段"""
        required_fields = ['task_type', 'pretrained_model', 'num_classes']
        
        for field in required_fields:
            if field not in config:
                raise ValueError(f"配置文件缺少必需字段: {field}")
                
    def get(self, key: str, default: Any = None) -> Any:
        """获取配置值，支持点号分隔的嵌套键"""
        keys = key.split('.')
        value = self.config
        
        for k in keys:
            if isinstance(value, dict):
                value = value.get(k, default)
            else:
                return default
                
        return value if value is not None else default
        
    def get_classification_config(self) -> Dict[str, Any]:
        """获取分类任务配置"""
        return self.config.get('classification_config', {})
        
    def get_detection_config(self) -> Dict[str, Any]:
        """获取检测任务配置"""
        return self.config.get('detection_config', {})
        
    def get_optimizer_config(self) -> Dict[str, Any]:
        """获取优化器配置"""
        return {
            'type': self.config.get('optimizer', 'Adam'),
            'lr': self.config.get('learning_rate', 0.001),
            'weight_decay': self.config.get('weight_decay', 0.0001)
        }
        
    def get_scheduler_config(self) -> Dict[str, Any]:
        """获取学习率调度器配置"""
        return {
            'type': self.config.get('lr_scheduler', 'StepLR'),
            'step_size': self.config.get('lr_step_size', 10),
            'gamma': self.config.get('lr_gamma', 0.1)
        }
        
    def get_early_stopping_config(self) -> Dict[str, Any]:
        """获取早停配置"""
        return {
            'enabled': self.config.get('early_stopping_rounds', 0) > 0,
            'patience': self.config.get('early_stopping_rounds', 10),
            'delta': self.config.get('early_stopping_delta', 0.001)
        }
        
    @property
    def model_name(self) -> str:
        """获取模型名称"""
        return self.config['pretrained_model']
        
    @property
    def num_classes(self) -> int:
        """获取类别数量"""
        return self.config['num_classes']
        
    @property
    def batch_size(self) -> int:
        """获取批次大小"""
        return self.config.get('batch_size', 32)
        
    @property
    def epochs(self) -> int:
        """获取训练轮数"""
        return self.config.get('epochs', 50)
        
    @property
    def learning_rate(self) -> float:
        """获取学习率"""
        return self.config.get('learning_rate', 0.001)
        
    @property
    def model_output_path(self) -> Optional[str]:
        """获取模型输出路径"""
        return self.config.get('model_output_path')
        
    @property
    def log_output_path(self) -> Optional[str]:
        """获取日志输出路径"""
        return self.config.get('py_train_log_output_path')
        
    @property
    def local_weights_path(self) -> Optional[str]:
        """获取本地权重路径"""
        return self.config.get('local_weights_path')
