#!/usr/bin/env python3
"""
Faster R-CNN系列训练器
支持的模型：
- fasterrcnn_mobilenet_v3_large_fpn
- fasterrcnn_resnet50_fpn_v2
"""
import torch
import torch.nn as nn
from torchvision import models
from torchvision.models.detection.faster_rcnn import FastRCNNPredictor
import argparse
import sys
import os

# 添加父目录到路径
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from Detection.base_detector import BaseDetectionTrainer
from common import StructuredLogger, DetectionDataLoader


class FasterRCNNTrainer(BaseDetectionTrainer):
    """Faster R-CNN系列训练器"""
    
    # 支持的模型
    SUPPORTED_MODELS = [
        'fasterrcnn_mobilenet_v3_large_fpn',
        'fasterrcnn_resnet50_fpn_v2'
    ]
    
    def __init__(self, config_path: str):
        """
        初始化Faster R-CNN训练器
        
        Args:
            config_path: 配置文件路径
        """
        super().__init__(config_path)
        
        # 验证模型名称
        model_name = self.config_parser.model_name
        if model_name not in self.SUPPORTED_MODELS:
            raise ValueError(
                f"不支持的模型: {model_name}. "
                f"支持的模型: {self.SUPPORTED_MODELS}"
            )
            
    def prepare_model(self) -> nn.Module:
        """
        准备Faster R-CNN模型
        
        Returns:
            配置好的模型
        """
        model_name = self.config_parser.model_name
        num_classes = self.config_parser.num_classes
        local_weights = self.config_parser.local_weights_path
        
        StructuredLogger.info(f"加载模型: {model_name}")
        
        # 加载预训练模型或本地权重
        if local_weights and os.path.exists(local_weights):
            StructuredLogger.info(f"从本地加载权重: {local_weights}")
            # 先创建模型结构
            model = getattr(models.detection, model_name)(weights=None, num_classes=91)  # COCO类别数
            # 修改检测头
            model = self._modify_detector_head(model, num_classes)
            # 加载权重
            state_dict = torch.load(local_weights, map_location='cpu')
            if 'model_state_dict' in state_dict:
                state_dict = state_dict['model_state_dict']
            model.load_state_dict(state_dict, strict=False)  # strict=False允许类别数不同
        else:
            StructuredLogger.info("使用COCO预训练权重")
            # 加载预训练模型
            if model_name == 'fasterrcnn_mobilenet_v3_large_fpn':
                weights = models.detection.FasterRCNN_MobileNet_V3_Large_FPN_Weights.DEFAULT
            elif model_name == 'fasterrcnn_resnet50_fpn_v2':
                weights = models.detection.FasterRCNN_ResNet50_FPN_V2_Weights.DEFAULT
            else:
                weights = None
                
            model = getattr(models.detection, model_name)(weights=weights)
            # 修改检测头
            model = self._modify_detector_head(model, num_classes)
            
        StructuredLogger.info(f"模型准备完成")
        return model
        
    def _modify_detector_head(self, model: nn.Module, num_classes: int) -> nn.Module:
        """
        修改检测头以适应新的类别数
        
        Args:
            model: 原始模型
            num_classes: 类别数（不包含背景）
            
        Returns:
            修改后的模型
        """
        # 获取分类器输入特征数
        in_features = model.roi_heads.box_predictor.cls_score.in_features
        
        # 替换预测头（num_classes + 1 for background）
        model.roi_heads.box_predictor = FastRCNNPredictor(in_features, num_classes + 1)
        
        return model
        
    def prepare_data(self):
        """准备检测数据加载器"""
        StructuredLogger.info("准备检测数据集...")
        
        # 使用DetectionDataLoader
        data_loader = DetectionDataLoader(self.det_config)
        self.train_loader, self.val_loader = data_loader.get_dataloaders(
            self.config_parser.batch_size,
            num_workers=4
        )
        
        StructuredLogger.info(f"训练集大小: {len(self.train_loader.dataset)}")
        StructuredLogger.info(f"验证集大小: {len(self.val_loader.dataset)}")
        
    def train_epoch(self, epoch: int) -> dict:
        """
        训练一个epoch
        
        Args:
            epoch: 当前epoch
            
        Returns:
            训练指标字典
        """
        self.model.train()
        running_loss = 0.0
        num_batches = 0
        
        for batch_idx, (images, targets) in enumerate(self.train_loader):
            try:
                # 将数据移到设备
                images = [img.to(self.device) for img in images]
                targets = [{k: v.to(self.device) for k, v in t.items()} for t in targets]
                
                # 验证数据有效性
                for i, target in enumerate(targets):
                    labels = target['labels']
                    boxes = target['boxes']
                    
                    # 检查标签范围
                    if len(labels) > 0:
                        if labels.min() < 1:
                            StructuredLogger.error(f"Batch {batch_idx}, 样本 {i}: 标签 < 1, min={labels.min()}")
                            raise ValueError(f"标签必须 >= 1，得到 {labels.min()}")
                        
                        if labels.max() > self.config_parser.num_classes:
                            StructuredLogger.error(f"Batch {batch_idx}, 样本 {i}: 标签超过类别数，max={labels.max()}, num_classes={self.config_parser.num_classes}")
                            raise ValueError(f"标签不能超过 {self.config_parser.num_classes}")
                        
                        # 检查boxes有效性
                        if torch.isnan(boxes).any() or torch.isinf(boxes).any():
                            StructuredLogger.error(f"Batch {batch_idx}, 样本 {i}: boxes 包含 NaN 或 Inf")
                            raise ValueError("boxes 包含无效值")
                
                # 前向传播（训练模式下返回损失字典）
                self.optimizer.zero_grad()
                loss_dict = self.model(images, targets)
                
            except Exception as e:
                StructuredLogger.error(f"训练 batch {batch_idx} 时出错: {e}")
                # 打印详细的调试信息
                for i, target in enumerate(targets):
                    StructuredLogger.info(f"样本 {i}: labels={target['labels']}, boxes shape={target['boxes'].shape}")
                raise
            
            # 计算总损失
            losses = sum(loss for loss in loss_dict.values())
            
            # 反向传播
            losses.backward()
            self.optimizer.step()
            
            # 统计
            running_loss += losses.item()
            num_batches += 1
            
            # 定期输出batch进度（每10个batch）
            if (batch_idx + 1) % 10 == 0:
                avg_loss = running_loss / num_batches
                StructuredLogger.info(
                    f"Epoch {epoch} [{batch_idx + 1}/{len(self.train_loader)}] "
                    f"Loss: {avg_loss:.4f}"
                )
        
        # 计算epoch平均损失
        epoch_loss = running_loss / num_batches if num_batches > 0 else 0.0
        
        return {'loss': epoch_loss}
        
    def validate_epoch(self, epoch: int) -> dict:
        """
        验证一个epoch
        
        Args:
            epoch: 当前epoch
            
        Returns:
            验证指标字典
        """
        self.model.eval()
        running_loss = 0.0
        num_batches = 0
        
        with torch.no_grad():
            for images, targets in self.val_loader:
                # 将数据移到设备
                images = [img.to(self.device) for img in images]
                targets = [{k: v.to(self.device) for k, v in t.items()} for t in targets]
                
                # 前向传播
                # 注意：评估模式下，模型返回预测结果，需要切换到训练模式来获取损失
                self.model.train()
                loss_dict = self.model(images, targets)
                self.model.eval()
                
                # 计算总损失
                losses = sum(loss for loss in loss_dict.values())
                
                # 统计
                running_loss += losses.item()
                num_batches += 1
        
        # 计算epoch平均损失
        epoch_loss = running_loss / num_batches if num_batches > 0 else 0.0
        
        return {'loss': epoch_loss}


def main():
    """主函数"""
    parser = argparse.ArgumentParser(description='Faster R-CNN训练器')
    parser.add_argument('--config', type=str, required=True,
                       help='配置文件路径')
    args = parser.parse_args()
    
    # 创建训练器并开始训练
    trainer = FasterRCNNTrainer(args.config)
    trainer.train()


if __name__ == '__main__':
    main()
