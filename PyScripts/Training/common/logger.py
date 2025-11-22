"""
结构化日志输出模块
基于stdout输出JSON格式的日志，供C#端实时解析
"""
import json
import sys
import traceback
from typing import Dict, Any, Optional
from datetime import datetime


class StructuredLogger:
    """结构化日志记录器 - 输出到stdout供C#实时解析"""
    
    @staticmethod
    def _log(log_type: str, data: Dict[str, Any]) -> None:
        """
        输出结构化日志
        
        Args:
            log_type: 日志类型
            data: 日志数据
        """
        log_entry = {
            'type': log_type,
            'timestamp': datetime.now().isoformat(),
            **data
        }
        print(json.dumps(log_entry, ensure_ascii=False), flush=True)
        
    @staticmethod
    def config(model: str, num_classes: int, batch_size: int, epochs: int, **kwargs) -> None:
        """记录训练配置"""
        StructuredLogger._log('config', {
            'model': model,
            'num_classes': num_classes,
            'batch_size': batch_size,
            'epochs': epochs,
            **kwargs
        })
        
    @staticmethod
    def progress(epoch: int, total_epochs: int) -> None:
        """记录训练进度"""
        percent = (epoch / total_epochs) * 100
        StructuredLogger._log('progress', {
            'epoch': epoch,
            'total_epochs': total_epochs,
            'percent': round(percent, 2)
        })
        
    @staticmethod
    def metrics(epoch: int, phase: str, loss: float, accuracy: float, 
                lr: float, **additional_metrics) -> None:
        """记录训练/验证指标"""
        StructuredLogger._log('metrics', {
            'epoch': epoch,
            'phase': phase,
            'loss': round(loss, 6),
            'accuracy': round(accuracy, 6),
            'lr': lr,
            **{k: round(v, 6) if isinstance(v, float) else v 
               for k, v in additional_metrics.items()}
        })
        
    @staticmethod
    def validation(epoch: int, metrics: Dict[str, float], improved: bool) -> None:
        """记录验证结果"""
        StructuredLogger._log('validation', {
            'epoch': epoch,
            'metrics': {k: round(v, 6) for k, v in metrics.items()},
            'improved': improved
        })
        
    @staticmethod
    def batch(epoch: int, batch_idx: int, total_batches: int, loss: float) -> None:
        """记录批次进度（可选，大量输出时慎用）"""
        StructuredLogger._log('batch', {
            'epoch': epoch,
            'batch': batch_idx,
            'total_batches': total_batches,
            'loss': round(loss, 6),
            'percent': round((batch_idx / total_batches) * 100, 2)
        })
        
    @staticmethod
    def checkpoint(epoch: int, path: str, reason: str, metrics: Optional[Dict] = None) -> None:
        """记录模型保存点"""
        data = {
            'epoch': epoch,
            'path': path,
            'reason': reason
        }
        if metrics:
            data['metrics'] = {k: round(v, 6) for k, v in metrics.items()}
        StructuredLogger._log('checkpoint', data)
        
    @staticmethod
    def lr_schedule(epoch: int, old_lr: float, new_lr: float, reason: str) -> None:
        """记录学习率调整"""
        StructuredLogger._log('lr_schedule', {
            'epoch': epoch,
            'old_lr': old_lr,
            'new_lr': new_lr,
            'reason': reason
        })
        
    @staticmethod
    def early_stop(epoch: int, reason: str, best_epoch: int, best_metrics: Dict) -> None:
        """记录早停"""
        StructuredLogger._log('early_stop', {
            'epoch': epoch,
            'reason': reason,
            'best_epoch': best_epoch,
            'best_metrics': {k: round(v, 6) for k, v in best_metrics.items()}
        })
        
    @staticmethod
    def system(gpu_usage: Optional[float] = None, memory_usage: Optional[float] = None, 
               cpu_usage: Optional[float] = None) -> None:
        """记录系统资源使用"""
        data = {}
        if gpu_usage is not None:
            data['gpu_usage'] = round(gpu_usage, 2)
        if memory_usage is not None:
            data['memory_usage'] = round(memory_usage, 2)
        if cpu_usage is not None:
            data['cpu_usage'] = round(cpu_usage, 2)
        
        if data:
            StructuredLogger._log('system', data)
        
    @staticmethod
    def info(message: str, **kwargs) -> None:
        """记录信息"""
        StructuredLogger._log('info', {'message': message, **kwargs})
        
    @staticmethod
    def warning(message: str, **kwargs) -> None:
        """记录警告"""
        StructuredLogger._log('warning', {'message': message, **kwargs})
        
    @staticmethod
    def error(message: str, exception: Optional[Exception] = None) -> None:
        """记录错误"""
        data = {'message': message}
        if exception:
            data['error_type'] = type(exception).__name__
            data['traceback'] = traceback.format_exc()
        StructuredLogger._log('error', data)
        
    @staticmethod
    def complete(best_epoch: int, best_metrics: Dict[str, float], 
                 model_path: str, total_time: float) -> None:
        """记录训练完成"""
        StructuredLogger._log('complete', {
            'best_epoch': best_epoch,
            'best_metrics': {k: round(v, 6) for k, v in best_metrics.items()},
            'model_path': model_path,
            'total_time': round(total_time, 2)
        })


class LegacyJsonLogger:
    """传统JSON文件日志记录器（保留用于训练历史记录）"""
    
    def __init__(self, log_path: str):
        """
        初始化JSON日志记录器
        
        Args:
            log_path: JSON日志文件路径
        """
        self.log_path = log_path
        self.entries = []
        
    def add_entry(self, entry: Dict[str, Any]) -> None:
        """添加日志条目"""
        self.entries.append(entry)
        self._write_to_file()
        
    def _write_to_file(self) -> None:
        """写入文件"""
        try:
            with open(self.log_path, 'w', encoding='utf-8') as f:
                json.dump({'entries': self.entries}, f, ensure_ascii=False, indent=2)
        except Exception as e:
            StructuredLogger.error(f"写入JSON日志文件失败: {e}")
            
    def log_epoch(self, epoch: int, phase: str, metrics: Dict[str, float]) -> None:
        """记录epoch数据"""
        entry = {
            'timestamp': datetime.now().isoformat(),
            'epoch': epoch,
            'phase': phase,
            **metrics
        }
        self.add_entry(entry)
