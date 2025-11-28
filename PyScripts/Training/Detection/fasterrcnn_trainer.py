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
            
            # 先加载权重字典以确定原始类别数
            state_dict = torch.load(local_weights, map_location='cpu')
            if 'model_state_dict' in state_dict:
                state_dict = state_dict['model_state_dict']
            
            # 从权重中推断原始类别数
            # cls_score.weight 的形状是 [num_classes, feature_dim]
            if 'roi_heads.box_predictor.cls_score.weight' in state_dict:
                original_num_classes = state_dict['roi_heads.box_predictor.cls_score.weight'].shape[0]
                StructuredLogger.info(f"检测到权重文件的类别数: {original_num_classes}")
            else:
                # 如果无法推断，使用COCO的91类
                original_num_classes = 91
                StructuredLogger.warning(f"无法从权重推断类别数，使用默认值: {original_num_classes}")
            
            # 创建与权重匹配的模型结构
            model = getattr(models.detection, model_name)(weights=None, num_classes=original_num_classes)
            
            # 加载权重（此时形状完全匹配）
            model.load_state_dict(state_dict, strict=True)
            StructuredLogger.info("成功加载本地权重")
            
            # 如果目标类别数与原始类别数不同，修改检测头
            if num_classes != original_num_classes:
                StructuredLogger.info(f"修改检测头: {original_num_classes} 类 → {num_classes} 类")
                model = self._modify_detector_head(model, num_classes)
            else:
                StructuredLogger.info(f"类别数匹配，无需修改检测头")
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
                            error_msg = (
                                f"❌ 数据验证失败 (Batch {batch_idx}, 样本 {i}):\n"
                                f"   标签值必须 >= 1 (0保留给背景类)\n"
                                f"   当前最小标签: {labels.min().item()}\n"
                                f"   所有标签: {labels.tolist()}\n"
                                f"   这通常是因为COCO标注文件的category_id设置不正确。\n"
                                f"   请检查标注文件的categories字段，确保id从1开始。"
                            )
                            StructuredLogger.error(error_msg)
                            raise ValueError(error_msg)
                        
                        if labels.max() > self.config_parser.num_classes:
                            error_msg = (
                                f"❌ 数据验证失败 (Batch {batch_idx}, 样本 {i}):\n"
                                f"   标签值超出类别数范围\n"
                                f"   最大标签: {labels.max().item()}\n"
                                f"   配置的类别数: {self.config_parser.num_classes}\n"
                                f"   所有标签: {labels.tolist()}\n"
                                f"   这通常是因为:\n"
                                f"   1. 训练集和验证集的类别数不一致\n"
                                f"   2. 配置的类别数与实际数据不匹配\n"
                                f"   请检查COCO标注文件的categories字段。"
                            )
                            StructuredLogger.error(error_msg)
                            raise ValueError(error_msg)
                        
                        # 检查boxes有效性
                        if torch.isnan(boxes).any() or torch.isinf(boxes).any():
                            error_msg = f"Batch {batch_idx}, 样本 {i}: boxes 包含 NaN 或 Inf"
                            StructuredLogger.error(error_msg)
                            raise ValueError(error_msg)
                
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
            for batch_idx, (images, targets) in enumerate(self.val_loader):
                try:
                    # 将数据移到设备
                    images = [img.to(self.device) for img in images]
                    targets = [{k: v.to(self.device) for k, v in t.items()} for t in targets]
                    
                    # 验证数据有效性
                    for i, target in enumerate(targets):
                        labels = target['labels']
                        if len(labels) > 0:
                            if labels.min() < 1 or labels.max() > self.config_parser.num_classes:
                                error_msg = (
                                    f"❌ 验证集数据验证失败 (Batch {batch_idx}, 样本 {i}):\n"
                                    f"   标签范围: [{labels.min().item()}, {labels.max().item()}]\n"
                                    f"   有效范围: [1, {self.config_parser.num_classes}]\n"
                                    f"   这通常是因为验证集的类别与训练集不一致。"
                                )
                                StructuredLogger.error(error_msg)
                                raise ValueError(error_msg)
                    
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
                    
                except Exception as e:
                    StructuredLogger.error(f"验证 batch {batch_idx} 时出错: {e}")
                    raise
        
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
