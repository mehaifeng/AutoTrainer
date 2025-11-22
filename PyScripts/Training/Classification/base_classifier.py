"""
分类训练基类
提供所有分类模型训练的通用框架
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
    ClassificationDataLoader, MetricsCalculator, AverageMeter,
    EarlyStopping, GPUMonitor, SystemMonitor, ModelCheckpoint,
    get_device, set_seed
)


class BaseClassificationTrainer:
    """分类训练基类"""
    
    def __init__(self, config_path: str):
        """
        初始化训练器
        
        Args:
            config_path: 配置文件路径
        """
        # 加载配置
        self.config_parser = ConfigParser(config_path)
        self.config = self.config_parser.config
        self.cls_config = self.config_parser.get_classification_config()
        
        # 设置设备
        self.device = get_device()
        set_seed(42)
        
        # 初始化组件
        self.model = None
        self.criterion = None
        self.optimizer = None
        self.scheduler = None
        self.early_stopping = None
        
        # 训练状态
        self.best_acc = 0.0
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
        
    def get_input_size(self) -> int:
        """
        获取模型输入大小（子类可以重写）
        
        Returns:
            输入图像大小
        """
        return 224  # 默认值
        
    def prepare_data(self):
        """准备数据加载器"""
        input_size = self.get_input_size()
        data_loader = ClassificationDataLoader(self.cls_config, input_size)
        self.train_loader, self.val_loader = data_loader.get_dataloaders(
            self.config_parser.batch_size
        )
        
        StructuredLogger.info(f"训练集大小: {len(self.train_loader.dataset)}")
        StructuredLogger.info(f"验证集大小: {len(self.val_loader.dataset)}")
        
    def prepare_criterion(self):
        """准备损失函数"""
        loss_config = self.cls_config.get('loss_function_config', {})
        loss_type = loss_config.get('type', 'CrossEntropyLoss')
        
        if loss_type == 'CrossEntropyLoss':
            label_smoothing = loss_config.get('args', {}).get('label_smoothing', 0.0)
            self.criterion = nn.CrossEntropyLoss(label_smoothing=label_smoothing)
        else:
            self.criterion = nn.CrossEntropyLoss()
            
    def prepare_optimizer(self):
        """准备优化器"""
        opt_config = self.config_parser.get_optimizer_config()
        opt_type = opt_config['type']
        
        if opt_type == 'Adam':
            self.optimizer = optim.Adam(
                self.model.parameters(),
                lr=opt_config['lr'],
                weight_decay=opt_config['weight_decay']
            )
        elif opt_type == 'SGD':
            self.optimizer = optim.SGD(
                self.model.parameters(),
                lr=opt_config['lr'],
                weight_decay=opt_config['weight_decay'],
                momentum=0.9
            )
        else:
            self.optimizer = optim.Adam(self.model.parameters(), lr=opt_config['lr'])
            
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
        训练一个epoch
        
        Args:
            epoch: 当前epoch
            
        Returns:
            训练指标字典
        """
        self.model.train()
        running_loss = 0.0
        running_corrects = 0
        
        for batch_idx, (inputs, labels) in enumerate(self.train_loader):
            inputs = inputs.to(self.device)
            labels = labels.to(self.device)
            
            # 前向传播
            self.optimizer.zero_grad()
            outputs = self.model(inputs)
            loss = self.criterion(outputs, labels)
            
            # 反向传播
            loss.backward()
            self.optimizer.step()
            
            # 统计
            running_loss += loss.item() * inputs.size(0)
            _, preds = torch.max(outputs, 1)
            running_corrects += torch.sum(preds == labels.data)
            
        # 计算epoch指标
        epoch_loss = running_loss / len(self.train_loader.dataset)
        epoch_acc = running_corrects.double() / len(self.train_loader.dataset)
        
        return {
            'loss': epoch_loss,
            'accuracy': epoch_acc.item()
        }
        
    def validate_epoch(self, epoch: int) -> Dict[str, float]:
        """
        验证一个epoch
        
        Args:
            epoch: 当前epoch
            
        Returns:
            验证指标字典
        """
        self.model.eval()
        running_loss = 0.0
        running_corrects = 0
        
        with torch.no_grad():
            for inputs, labels in self.val_loader:
                inputs = inputs.to(self.device)
                labels = labels.to(self.device)
                
                outputs = self.model(inputs)
                loss = self.criterion(outputs, labels)
                
                running_loss += loss.item() * inputs.size(0)
                _, preds = torch.max(outputs, 1)
                running_corrects += torch.sum(preds == labels.data)
                
        # 计算epoch指标
        epoch_loss = running_loss / len(self.val_loader.dataset)
        epoch_acc = running_corrects.double() / len(self.val_loader.dataset)
        
        return {
            'loss': epoch_loss,
            'accuracy': epoch_acc.item()
        }
        
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
            self.prepare_criterion()
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
                    accuracy=train_metrics['accuracy'],
                    lr=current_lr
                )
                
                # 验证阶段
                val_metrics = self.validate_epoch(epoch)
                
                # 输出验证指标
                StructuredLogger.metrics(
                    epoch=epoch,
                    phase='val',
                    loss=val_metrics['loss'],
                    accuracy=val_metrics['accuracy'],
                    lr=current_lr
                )
                
                # 检查是否改进
                improved = val_metrics['accuracy'] > self.best_acc
                StructuredLogger.validation(epoch, val_metrics, improved)
                
                # 保存最佳模型
                if improved:
                    self.best_acc = val_metrics['accuracy']
                    self.best_epoch = epoch
                    save_path = checkpoint_manager.save(
                        self.model, epoch, val_metrics, is_best=True
                    )
                    StructuredLogger.checkpoint(
                        epoch, save_path, 'best_accuracy', val_metrics
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
                            {'accuracy': self.best_acc}
                        )
                        break
                        
                # 系统资源监控（每5个epoch）
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
                {'accuracy': self.best_acc},
                final_path,
                total_time
            )
            
        except Exception as e:
            StructuredLogger.error("训练过程中发生错误", e)
            raise
