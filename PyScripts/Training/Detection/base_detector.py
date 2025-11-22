"""
检测训练基类
提供所有检测模型训练的通用框架
"""
import torch
import torch.nn as nn
import torch.optim as optim
from torch.optim import lr_scheduler
import time
from pathlib import Path
from typing import Dict, Any, Tuple, Optional
import sys
import os

# 添加common模块到路径
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from common import (
    ConfigParser, StructuredLogger, LegacyJsonLogger,
    MetricsCalculator, AverageMeter,
    EarlyStopping, GPUMonitor, SystemMonitor, ModelCheckpoint,
    get_device, set_seed
)


class BaseDetectionTrainer:
    """检测训练基类"""
    
    def __init__(self, config_path: str):
        """
        初始化训练器
        
        Args:
            config_path: 配置文件路径
        """
        # 加载配置
        self.config_parser = ConfigParser(config_path)
        self.config = self.config_parser.config
        self.det_config = self.config_parser.get_detection_config()
        
        # 设置设备
        self.device = get_device()
        set_seed(42)
        
        # 初始化组件
        self.model = None
        self.optimizer = None
        self.scheduler = None
        self.early_stopping = None
        
        # 训练状态
        self.best_loss = float('inf')
        self.best_epoch = 0
        self.start_time = None
        
        # 监控器
        self.gpu_monitor = GPUMonitor()
        self.system_monitor = SystemMonitor()
        
        # 日志记录器
        log_path = self.config_parser.log_output_path
        self.json_logger = LegacyJsonLogger(log_path) if log_path else None
        
        # 输出配置信息
        StructuredLogger.config(
            model=self.config_parser.model_name,
            num_classes=self.config_parser.num_classes,
            batch_size=self.config_parser.batch_size,
            epochs=self.config_parser.epochs,
            learning_rate=self.config_parser.learning_rate,
            device=str(self.device)
        )
        
    def prepare_model(self) -> nn.Module:
        """
        准备模型（子类必须实现）
        
        Returns:
            准备好的模型
        """
        raise NotImplementedError("子类必须实现prepare_model方法")
        
    def prepare_data(self):
        """准备数据加载器（子类必须实现）"""
        raise NotImplementedError("子类必须实现prepare_data方法")
        
    def prepare_optimizer(self):
        """准备优化器"""
        opt_config = self.config_parser.get_optimizer_config()
        opt_type = opt_config['type']
        
        if opt_type == 'SGD':
            self.optimizer = optim.SGD(
                self.model.parameters(),
                lr=opt_config['lr'],
                weight_decay=opt_config['weight_decay'],
                momentum=0.9
            )
        elif opt_type == 'Adam':
            self.optimizer = optim.Adam(
                self.model.parameters(),
                lr=opt_config['lr'],
                weight_decay=opt_config['weight_decay']
            )
        else:
            self.optimizer = optim.SGD(
                self.model.parameters(), 
                lr=opt_config['lr'],
                momentum=0.9
            )
            
    def prepare_scheduler(self):
        """准备学习率调度器"""
        sched_config = self.config_parser.get_scheduler_config()
        sched_type = sched_config['type']
        
        if sched_type == 'StepLR':
            self.scheduler = lr_scheduler.StepLR(
                self.optimizer,
                step_size=sched_config['step_size'],
                gamma=sched_config['gamma']
            )
        elif sched_type == 'CosineAnnealingLR':
            self.scheduler = lr_scheduler.CosineAnnealingLR(
                self.optimizer,
                T_max=self.config_parser.epochs
            )
        else:
            self.scheduler = lr_scheduler.StepLR(self.optimizer, step_size=10, gamma=0.1)
            
    def prepare_early_stopping(self):
        """准备早停机制"""
        es_config = self.config_parser.get_early_stopping_config()
        if es_config['enabled']:
            self.early_stopping = EarlyStopping(
                patience=es_config['patience'],
                delta=es_config['delta']
            )
            
    def train_epoch(self, epoch: int) -> Dict[str, float]:
        """
        训练一个epoch（子类可以重写）
        
        Args:
            epoch: 当前epoch
            
        Returns:
            训练指标字典
        """
        raise NotImplementedError("子类必须实现train_epoch方法")
        
    def validate_epoch(self, epoch: int) -> Dict[str, float]:
        """
        验证一个epoch（子类可以重写）
        
        Args:
            epoch: 当前epoch
            
        Returns:
            验证指标字典
        """
        raise NotImplementedError("子类必须实现validate_epoch方法")
        
    def train(self):
        """训练主循环"""
        try:
            # 准备所有组件
            StructuredLogger.info("准备模型...")
            self.model = self.prepare_model()
            self.model = self.model.to(self.device)
            
            StructuredLogger.info("准备数据...")
            self.prepare_data()
            
            StructuredLogger.info("准备训练组件...")
            self.prepare_optimizer()
            self.prepare_scheduler()
            self.prepare_early_stopping()
            
            # 模型检查点管理器
            checkpoint_manager = ModelCheckpoint(
                self.config_parser.model_output_path,
                self.config_parser.model_name
            )
            
            # 开始训练
            self.start_time = time.time()
            StructuredLogger.info("开始训练...")
            
            for epoch in range(1, self.config_parser.epochs + 1):
                # 输出进度
                StructuredLogger.progress(epoch, self.config_parser.epochs)
                
                # 训练阶段
                train_metrics = self.train_epoch(epoch)
                current_lr = self.optimizer.param_groups[0]['lr']
                
                # 输出训练指标
                StructuredLogger.metrics(
                    epoch=epoch,
                    phase='train',
                    loss=train_metrics['loss'],
                    accuracy=0.0,  # 检测任务没有简单的accuracy
                    lr=current_lr
                )
                
                # 验证阶段
                val_metrics = self.validate_epoch(epoch)
                
                # 输出验证指标
                StructuredLogger.metrics(
                    epoch=epoch,
                    phase='val',
                    loss=val_metrics['loss'],
                    accuracy=0.0,
                    lr=current_lr
                )
                
                # 检查是否改进（基于损失）
                improved = val_metrics['loss'] < self.best_loss
                StructuredLogger.validation(epoch, val_metrics, improved)
                
                # 保存最佳模型
                if improved:
                    self.best_loss = val_metrics['loss']
                    self.best_epoch = epoch
                    save_path = checkpoint_manager.save(
                        self.model, epoch, val_metrics, is_best=True
                    )
                    StructuredLogger.checkpoint(
                        epoch, save_path, 'best_loss', val_metrics
                    )
                    
                # 学习率调度
                old_lr = current_lr
                self.scheduler.step()
                new_lr = self.optimizer.param_groups[0]['lr']
                if old_lr != new_lr:
                    StructuredLogger.lr_schedule(
                        epoch, old_lr, new_lr,
                        self.config_parser.get_scheduler_config()['type']
                    )
                    
                # 早停检查
                if self.early_stopping:
                    if self.early_stopping(val_metrics['loss'], epoch):
                        StructuredLogger.early_stop(
                            epoch,
                            f"验证损失在{self.early_stopping.patience}个epoch内未改善",
                            self.best_epoch,
                            {'loss': self.best_loss}
                        )
                        break
                        
                # 系统资源监控
                if epoch % 5 == 0:
                    gpu_usage = self.gpu_monitor.get_gpu_usage()
                    mem_usage = self.gpu_monitor.get_memory_usage()
                    cpu_usage = self.system_monitor.get_cpu_usage()
                    StructuredLogger.system(gpu_usage, mem_usage, cpu_usage)
                    
                # 写入JSON日志
                if self.json_logger:
                    self.json_logger.log_epoch(epoch, 'train', train_metrics)
                    self.json_logger.log_epoch(epoch, 'val', val_metrics)
                    
            # 保存最终模型
            final_path = checkpoint_manager.save_final(self.model)
            
            # 训练完成
            total_time = time.time() - self.start_time
            StructuredLogger.complete(
                self.best_epoch,
                {'loss': self.best_loss},
                final_path,
                total_time
            )
            
        except Exception as e:
            StructuredLogger.error("训练过程中发生错误", e)
            raise
