"""
评估指标计算模块
提供训练和验证过程中的各种指标计算
"""
import torch
import numpy as np
from typing import Dict, Tuple
from sklearn.metrics import accuracy_score, precision_score, recall_score, f1_score


class MetricsCalculator:
    """指标计算器"""
    
    @staticmethod
    def calculate_accuracy(outputs: torch.Tensor, labels: torch.Tensor) -> float:
        """
        计算准确率
        
        Args:
            outputs: 模型输出
            labels: 真实标签
            
        Returns:
            准确率
        """
        _, predicted = torch.max(outputs, 1)
        correct = (predicted == labels).sum().item()
        total = labels.size(0)
        return correct / total if total > 0 else 0.0
        
    @staticmethod
    def calculate_classification_metrics(outputs: torch.Tensor, 
                                        labels: torch.Tensor) -> Dict[str, float]:
        """
        计算分类任务的各种指标
        
        Args:
            outputs: 模型输出
            labels: 真实标签
            
        Returns:
            包含accuracy, precision, recall, f1的字典
        """
        _, predicted = torch.max(outputs, 1)
        predicted = predicted.cpu().numpy()
        labels = labels.cpu().numpy()
        
        return {
            'accuracy': accuracy_score(labels, predicted),
            'precision': precision_score(labels, predicted, average='macro', zero_division=0),
            'recall': recall_score(labels, predicted, average='macro', zero_division=0),
            'f1': f1_score(labels, predicted, average='macro', zero_division=0)
        }
        
    @staticmethod
    def calculate_loss_accuracy(running_loss: float, running_corrects: int, 
                               dataset_size: int) -> Tuple[float, float]:
        """
        计算平均损失和准确率
        
        Args:
            running_loss: 累计损失
            running_corrects: 累计正确数
            dataset_size: 数据集大小
            
        Returns:
            (epoch_loss, epoch_acc)
        """
        epoch_loss = running_loss / dataset_size
        epoch_acc = running_corrects / dataset_size
        return epoch_loss, epoch_acc


class AverageMeter:
    """用于跟踪指标的平均值"""
    
    def __init__(self):
        self.reset()
        
    def reset(self):
        """重置所有统计"""
        self.val = 0
        self.avg = 0
        self.sum = 0
        self.count = 0
        
    def update(self, val, n=1):
        """
        更新统计
        
        Args:
            val: 新值
            n: 值的数量
        """
        self.val = val
        self.sum += val * n
        self.count += n
        self.avg = self.sum / self.count if self.count > 0 else 0
