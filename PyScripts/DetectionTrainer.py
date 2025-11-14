import sys
import os
import json
import argparse
from typing import Dict, Any, Optional
import torch
import torch.nn as nn
from torch.utils.data import DataLoader, Dataset
import torchvision
from torchvision.models.detection import FasterRCNN, RetinaNet, FCOS
from torchvision.models.detection.faster_rcnn import FastRCNNPredictor
from torchvision.models.detection.retinanet import RetinaNetClassificationHead
from datetime import datetime
import traceback

# 引用原有的基础类
from ModelTrainer import BaseModelConfig, TrainingLogger, EarlyStopping

class DetectionModelConfig(BaseModelConfig):
    """目标检测模型配置类"""

    def __init__(self, model_name: str, num_classes: int, config: Dict[str, Any], logger: Any = None):
        self.model_name = model_name
        self.num_classes = num_classes
        self.config = config
        self.logger = logger

        # 检测模型输入尺寸配置
        self.model_input_sizes = {
            'fasterrcnn_resnet50_fpn': (800, 1333),
            'fasterrcnn_resnet50_fpn_v2': (800, 1333),
            'fasterrcnn_mobilenet_v3_large_fpn': (800, 1333),
            'fasterrcnn_mobilenet_v3_large_320_fpn': (320, 320),
            'ssd300_vgg16': (300, 300),
            'ssdlite320_mobilenet_v3_large': (320, 320),
            'retinanet_resnet50_fpn': (800, 1333),
            'retinanet_resnet50_fpn_v2': (800, 1333),
            'fcos_resnet50_fpn': (800, 1333),
            'maskrcnn_resnet50_fpn': (800, 1333),
            'maskrcnn_resnet50_fpn_v2': (800, 1333),
            'keypointrcnn_resnet50_fpn': (800, 1333),
        }

    def get_model(self) -> nn.Module:
        """根据模型名称创建检测模型"""
        model = None

        # Faster R-CNN 系列
        if self.model_name.startswith('fasterrcnn'):
            if self.model_name == 'fasterrcnn_resnet50_fpn':
                model = torchvision.models.detection.fasterrcnn_resnet50_fpn(weights='DEFAULT')
            elif self.model_name == 'fasterrcnn_resnet50_fpn_v2':
                model = torchvision.models.detection.fasterrcnn_resnet50_fpn_v2(weights='DEFAULT')
            elif self.model_name == 'fasterrcnn_mobilenet_v3_large_fpn':
                model = torchvision.models.detection.fasterrcnn_mobilenet_v3_large_fpn(weights='DEFAULT')
            elif self.model_name == 'fasterrcnn_mobilenet_v3_large_320_fpn':
                model = torchvision.models.detection.fasterrcnn_mobilenet_v3_large_320_fpn(weights='DEFAULT')

            # 替换分类头
            if model is not None:
                in_features = model.roi_heads.box_predictor.cls_score.in_features
                model.roi_heads.box_predictor = FastRCNNPredictor(in_features, self.num_classes)

        # SSD 系列
        elif self.model_name.startswith('ssd'):
            if self.model_name == 'ssd300_vgg16':
                model = torchvision.models.detection.ssd300_vgg16(weights='DEFAULT')
            elif self.model_name == 'ssdlite320_mobilenet_v3_large':
                model = torchvision.models.detection.ssdlite320_mobilenet_v3_large(weights='DEFAULT')

            # 替换分类头
            if model is not None:
                num_anchors = model.anchor_generator.num_anchors_per_location()
                in_channels = model.head.classification_head.conv[0].in_channels
                model.head.classification_head.num_classes = self.num_classes

        # RetinaNet 系列
        elif self.model_name.startswith('retinanet'):
            if self.model_name == 'retinanet_resnet50_fpn':
                model = torchvision.models.detection.retinanet_resnet50_fpn(weights='DEFAULT')
            elif self.model_name == 'retinanet_resnet50_fpn_v2':
                model = torchvision.models.detection.retinanet_resnet50_fpn_v2(weights='DEFAULT')

            # 替换分类头
            if model is not None:
                num_anchors = model.anchor_generator.num_anchors_per_location()
                in_channels = model.backbone.body.conv1.in_channels
                model.head = RetinaNetClassificationHead(in_channels, num_anchors, self.num_classes)

        # FCOS
        elif self.model_name == 'fcos_resnet50_fpn':
            model = torchvision.models.detection.fcos_resnet50_fpn(weights='DEFAULT')
            model.num_classes = self.num_classes

        # Mask R-CNN 系列
        elif self.model_name.startswith('maskrcnn'):
            if self.model_name == 'maskrcnn_resnet50_fpn':
                model = torchvision.models.detection.maskrcnn_resnet50_fpn(weights='DEFAULT')
            elif self.model_name == 'maskrcnn_resnet50_fpn_v2':
                model = torchvision.models.detection.maskrcnn_resnet50_fpn_v2(weights='DEFAULT')

            # 替换分类头和掩码头
            if model is not None:
                in_features = model.roi_heads.box_predictor.cls_score.in_features
                model.roi_heads.box_predictor = FastRCNNPredictor(in_features, self.num_classes)
                model.roi_heads.mask_predictor.conv5_mask.in_channels = 256
                model.roi_heads.mask_predictor.num_classes = self.num_classes

        # Keypoint R-CNN
        elif self.model_name == 'keypointrcnn_resnet50_fpn':
            model = torchvision.models.detection.keypointrcnn_resnet50_fpn(weights='DEFAULT')
            if model is not None:
                in_features = model.roi_heads.box_predictor.cls_score.in_features
                model.roi_heads.box_predictor = FastRCNNPredictor(in_features, self.num_classes)

        if model is None:
            raise ValueError(f"不支持的检测模型: {self.model_name}")

        self.logger.log_entry("Debug", f"成功创建检测模型: {self.model_name}，类别数: {self.num_classes}")
        print(f"成功创建检测模型: {self.model_name}，类别数: {self.num_classes}")
        return model

    def get_transforms(self):
        """检测任务的数据预处理"""
        import torchvision.transforms as transforms

        input_size = self.get_input_size()

        # 检测任务的数据预处理
        return transforms.Compose([
            transforms.ToTensor(),
            # 注意: 检测模型内部已经做了归一化,这里不需要额外Normalize
        ])

    def get_loss_function(self):
        """检测模型的损失函数已内置在模型中"""
        return None  # 检测模型的损失函数由模型内部定义

    def get_input_size(self):
        return self.model_input_sizes.get(self.model_name, (800, 1333))

class COCODetectionDataset(Dataset):
    """COCO格式的检测数据集"""

    def __init__(self, images_dir: str, annotation_file: str, transforms=None):
        self.images_dir = images_dir
        self.transforms = transforms

        # 加载COCO标注文件
        with open(annotation_file, 'r') as f:
            self.coco_data = json.load(f)

        self.images = self.coco_data['images']
        self.annotations = self.coco_data['annotations']

        # COCO的category_id需要连续,映射到1, 2, 3...
        categories = sorted(self.coco_data['categories'], key=lambda x: x['id'])
        self.category_id_map = {cat['id']: idx + 1 for idx, cat in enumerate(categories)}
        print(f"类别ID映射: {self.category_id_map}")

        # 构建图像ID到标注的映射
        from collections import defaultdict
        self.img_to_anns = defaultdict(list)
        for ann in self.annotations:
            self.img_to_anns[ann['image_id']].append(ann)

        print(f"加载数据集: {len(self.images)} 张图像, {len(self.annotations)} 个标注")

    def __len__(self):
        return len(self.images)

    def __getitem__(self, idx):
        # 加载图像
        img_info = self.images[idx]
        img_path = os.path.join(self.images_dir, img_info['file_name'])

        from PIL import Image
        image = Image.open(img_path).convert("RGB")
        img_width, img_height = image.size

        # 获取该图像的所有标注
        anns = self.img_to_anns[img_info['id']]

        # 准备目标数据
        boxes = []
        labels = []
        area = []
        iscrowd = []

        for ann in anns:
            bbox = ann['bbox']  # [x, y, width, height]

            # 验证bbox格式和有效性
            if len(bbox) != 4:
                continue  # 跳过格式错误的bbox

            x, y, w, h = bbox

            # 确保坐标和尺寸是有效的
            if w <= 0 or h <= 0:
                continue  # 跳过无效的标注

            # 转换为[xmin, ymin, xmax, ymax]格式
            xmin, ymin, xmax, ymax = x, y, x + w, y + h
            
            # 裁剪到图像范围内
            xmin = max(0, min(xmin, img_width - 1))
            ymin = max(0, min(ymin, img_height - 1))
            xmax = max(xmin + 1, min(xmax, img_width))
            ymax = max(ymin + 1, min(ymax, img_height))
            
            # 再次检查有效性
            if xmax <= xmin or ymax <= ymin:
                continue

            boxes.append([xmin, ymin, xmax, ymax])
            labels.append(self.category_id_map[ann['category_id']])  # 使用映射后的ID
            area.append(ann['area'] if 'area' in ann else (xmax - xmin) * (ymax - ymin))
            iscrowd.append(ann.get('iscrowd', 0))

        # 如果没有有效的标注，创建一个空的target
        if len(boxes) == 0:
            target = {
                'boxes': torch.zeros((0, 4), dtype=torch.float32),
                'labels': torch.zeros(0, dtype=torch.int64),
                'image_id': torch.tensor([img_info['id']]),
                'area': torch.zeros(0, dtype=torch.float32),
                'iscrowd': torch.zeros(0, dtype=torch.int64)
            }
        else:
            target = {
                'boxes': torch.as_tensor(boxes, dtype=torch.float32),
                'labels': torch.as_tensor(labels, dtype=torch.int64),
                'image_id': torch.tensor([img_info['id']]),
                'area': torch.as_tensor(area, dtype=torch.float32),
                'iscrowd': torch.as_tensor(iscrowd, dtype=torch.int64)
            }

        if self.transforms is not None:
            image = self.transforms(image)

        return image, target

class DetectionTrainer:
    """目标检测训练器"""

    def __init__(self, config: Dict[str, Any], model_config: DetectionModelConfig, logger: TrainingLogger):
        self.config = config
        self.model_config = model_config
        self.logger = logger
        self.device = torch.device("cuda" if torch.cuda.is_available() else "cpu")

        # 记录模型信息
        self.logger.log_entry("ModelName", f"检测模型: {self.config.get('pretrained_model')}")
        self.logger.log_entry("TrainingDevice", f"训练设备: {self.device}")

        # 初始化模型
        self.model = self.model_config.get_model().to(self.device)
        self.optimizer = self._get_optimizer()
        self.lr_scheduler = self._get_scheduler()

        # 损失函数权重配置
        self.loss_config = config.get('detection_loss_config', {})

        # 初始化 EarlyStopping
        model_save_path = os.path.join(self.config['model_output_path'], f"{self.config['pretrained_model']}_detection.pth")
        self.early_stopping = EarlyStopping(
            save_path=model_save_path,
            patience=self.config.get('early_stopping_rounds', 5),
            verbose=True,
            delta=self.config.get('early_stopping_delta', 0.0)
        )
        self.early_stopping.set_logger(self.logger)

    def _get_optimizer(self):
        """获取优化器 - 降低学习率"""
        # 检测任务推荐更小的学习率
        base_lr = self.config.get('learning_rate', 0.005)
        # MobileNet模型需要更小的学习率
        if 'mobilenet' in self.config['pretrained_model'].lower():
            adjusted_lr = min(base_lr, 0.0005)  # MobileNet用0.0005
        else:
            adjusted_lr = min(base_lr, 0.005)   # ResNet可以用0.005
        
        if adjusted_lr != base_lr:
            print(f"警告: 学习率从 {base_lr} 调整为 {adjusted_lr} (检测任务推荐)")
            self.logger.log_entry("Info", f"学习率调整: {base_lr} -> {adjusted_lr}")
        
        optimizer_class = getattr(torch.optim, self.config['optimizer'])
        # SGD需要添加momentum
        if self.config['optimizer'] == 'SGD':
            return optimizer_class(
                self.model.parameters(),
                lr=adjusted_lr,
                momentum=0.9,  # 添加momentum
                weight_decay=self.config.get('weight_decay', 0.0005)
            )
        else:
            return optimizer_class(
                self.model.parameters(),
                lr=adjusted_lr,
                weight_decay=self.config.get('weight_decay', 0.0005)
            )

    def _get_scheduler(self):
        if self.config['lr_scheduler'] == 'StepLR':
            return torch.optim.lr_scheduler.StepLR(
                self.optimizer, step_size=5, gamma=0.5  # 改为5轮衰减50%,更温和
            )
        elif self.config['lr_scheduler'] == 'ReduceLROnPlateau':
            return torch.optim.lr_scheduler.ReduceLROnPlateau(
                self.optimizer, mode='min', patience=3, factor=0.5  # factor改为0.5
            )
        return None

    def prepare_data(self) -> tuple[DataLoader, DataLoader]:
        """准备检测数据集"""
        # 训练数据集
        train_dataset = COCODetectionDataset(
            self.config['train_images_path'],
            self.config['train_annotation_path'],
            transforms=self.model_config.get_transforms()
        )

        # 验证数据集
        val_dataset = COCODetectionDataset(
            self.config['val_images_path'],
            self.config['val_annotation_path'],
            transforms=self.model_config.get_transforms()
        )

        train_loader = DataLoader(
            train_dataset,
            batch_size=self.config['batch_size'],
            shuffle=True,
            collate_fn=self._collate_fn,
            num_workers=2
        )

        val_loader = DataLoader(
            val_dataset,
            batch_size=self.config['batch_size'],
            shuffle=False,
            collate_fn=self._collate_fn,
            num_workers=2
        )

        return train_loader, val_loader

    @staticmethod
    def _collate_fn(batch):
        """检测数据的批处理函数"""
        return tuple(zip(*batch))

    def _validate_targets(self, targets):
        """验证target数据的有效性"""
        for i, target in enumerate(targets):
            # 检查必需字段
            if 'boxes' not in target or 'labels' not in target:
                return False, f"Target {i} 缺少必需字段"
            
            boxes = target['boxes']
            labels = target['labels']
            
            # 检查维度
            if boxes.dim() != 2 or boxes.shape[1] != 4:
                return False, f"Target {i} boxes shape错误: {boxes.shape}"
            
            # 检查数量一致性
            if boxes.shape[0] != labels.shape[0]:
                return False, f"Target {i} boxes和labels数量不一致"
            
            # 如果有boxes,检查坐标有效性
            if boxes.shape[0] > 0:
                # 检查NaN或Inf
                if torch.isnan(boxes).any() or torch.isinf(boxes).any():
                    return False, f"Target {i} 包含NaN或Inf"
                
                # 检查坐标顺序
                if (boxes[:, 2] <= boxes[:, 0]).any() or (boxes[:, 3] <= boxes[:, 1]).any():
                    return False, f"Target {i} bbox坐标顺序错误"
                
                # 检查坐标范围 (假设图像不会超过10000像素)
                if (boxes < 0).any() or (boxes > 10000).any():
                    return False, f"Target {i} bbox坐标超出合理范围"
        
        return True, "Valid"

    def train_epoch(self, train_loader: DataLoader):
        """训练一个epoch"""
        self.model.train()
        total_loss = 0
        num_batches = len(train_loader)
        valid_batches = 0

        for batch_idx, (images, targets) in enumerate(train_loader):
            try:
                images = [image.to(self.device) for image in images]
                targets = [{k: v.to(self.device) for k, v in t.items()} for t in targets]

                # 验证targets
                is_valid, msg = self._validate_targets(targets)
                if not is_valid:
                    print(f"Warning: Skipping batch {batch_idx}: {msg}")
                    continue

                # 前向传播
                loss_dict = self.model(images, targets)

                # 计算加权总损失
                losses = sum(loss for loss in loss_dict.values())

                # 检查损失是否有效
                if not torch.isfinite(losses):
                    print(f"Warning: Non-finite loss in batch {batch_idx}, skipping")
                    # 打印详细信息帮助调试
                    for key, value in loss_dict.items():
                        print(f"  {key}: {value.item()}")
                    continue

                # 梯度裁剪防止爆炸
                self.optimizer.zero_grad()
                losses.backward()
                torch.nn.utils.clip_grad_norm_(self.model.parameters(), max_norm=10.0)
                self.optimizer.step()

                total_loss += losses.item()
                valid_batches += 1

                if batch_idx % 10 == 0:
                    log_msg = (
                        f"Batch [{batch_idx}/{num_batches}] "
                        f"Loss: {losses.item():.4f} "
                    )
                    
                    # 记录各个子损失
                    for key, value in loss_dict.items():
                        log_msg += f"{key}: {value.item():.4f} "
                    
                    self.logger.log_entry("Training", log_msg)
                    print(log_msg)

            except Exception as e:
                print(f"Warning: Error in training batch {batch_idx}: {str(e)}")
                traceback.print_exc()
                continue

        avg_loss = total_loss / valid_batches if valid_batches > 0 else float('inf')
        print(f"Training epoch 完成: 平均损失 = {avg_loss:.4f}, 有效batch数 = {valid_batches}/{num_batches}")
        return avg_loss

    @torch.no_grad()
    def evaluate(self, val_loader: DataLoader):
        """评估模型"""
        self.model.train()

        # 冻结BatchNorm层的统计信息更新
        for module in self.model.modules():
            if isinstance(module, torch.nn.BatchNorm2d):
                module.eval()  # BatchNorm用eval模式

        total_loss = 0
        num_batches = len(val_loader)
        valid_batches = 0

        for batch_idx, (images, targets) in enumerate(val_loader):
            try:
                images = [image.to(self.device) for image in images]
                targets = [{k: v.to(self.device) for k, v in t.items()} for t in targets]

                # 验证targets
                is_valid, msg = self._validate_targets(targets)
                if not is_valid:
                    print(f"Warning: Skipping validation batch {batch_idx}: {msg}")
                    continue

                # 前向传播
                loss_dict = self.model(images, targets)
                losses = sum(loss for loss in loss_dict.values())

                if torch.isfinite(losses):
                    total_loss += losses.item()
                    valid_batches += 1
                else:
                    print(f"Warning: Non-finite loss in validation batch {batch_idx}")
                    for key, value in loss_dict.items():
                        print(f"  {key}: {value.item()}")

            except Exception as e:
                print(f"Warning: Error in evaluation batch {batch_idx}: {str(e)}")
                traceback.print_exc()
                continue

        avg_loss = total_loss / valid_batches if valid_batches > 0 else float('inf')
        print(f"Validation 完成: 平均损失 = {avg_loss:.4f}, 有效batch数 = {valid_batches}/{num_batches}")
        return avg_loss

def load_config():
    parser = argparse.ArgumentParser()
    parser.add_argument('--config', type=str, required=True, help='Path to config JSON file')
    args = parser.parse_args()

    if not os.path.exists(args.config):
        raise FileNotFoundError(f"配置文件不存在: {args.config}")

    with open(args.config, 'r', encoding='utf-8') as f:
        return json.load(f)

def main():
    """主训练函数"""
    try:
        # 加载配置
        config = load_config()

        # 检查任务类型
        if config.get('task_type') != 'detection':
            raise ValueError("此脚本仅支持检测任务")

        logger = TrainingLogger(config['py_train_log_output_path'])
        logger.log_data["config"] = config

        # 验证数据路径
        if not os.path.isdir(config['train_images_path']):
            raise FileNotFoundError(f"训练图像路径不存在: {config['train_images_path']}")
        if not os.path.isfile(config['train_annotation_path']):
            raise FileNotFoundError(f"训练标注文件不存在: {config['train_annotation_path']}")

        # 确定类别数（从标注文件中读取）
        with open(config['train_annotation_path'], 'r') as f:
            coco_data = json.load(f)
        num_classes = len(coco_data['categories']) + 1  # +1 for background

        logger.log_entry("Info", f"确定检测类别数: {num_classes} (包含背景)")
        print(f"检测类别数: {num_classes}")

        # 创建模型配置
        model_config = DetectionModelConfig(
            model_name=config['pretrained_model'],
            num_classes=num_classes,
            config=config,
            logger=logger
        )

        # 创建训练器
        trainer = DetectionTrainer(config, model_config, logger)

        # 准备数据
        train_loader, val_loader = trainer.prepare_data()

        # 训练循环
        best_val_loss = float('inf')
        for epoch in range(config['epochs']):
            print(f"\n{'='*60}")
            print(f"Epoch {epoch + 1}/{config['epochs']}")
            print(f"{'='*60}")
            
            # 训练
            train_loss = trainer.train_epoch(train_loader)

            # 评估
            val_loss = trainer.evaluate(val_loader)

            # 记录指标
            metrics = {
                "train_loss": train_loss,
                "validation_loss": val_loss,
                "learning_rate": trainer.optimizer.param_groups[0]['lr']
            }

            logger.log_entry(
                "DetectionValidation",
                f"Epoch {epoch + 1} 完成 - 训练损失: {train_loss:.4f}, 验证损失: {val_loss:.4f}",
                epoch=epoch + 1,
                metrics=metrics
            )

            if val_loss < best_val_loss:
                best_val_loss = val_loss
                print(f"✓ 新的最佳验证损失: {best_val_loss:.4f}")

            # 更新学习率
            if isinstance(trainer.lr_scheduler, torch.optim.lr_scheduler.ReduceLROnPlateau):
                trainer.lr_scheduler.step(val_loss)
            elif trainer.lr_scheduler:
                trainer.lr_scheduler.step()

            # 调用 EarlyStopping
            trainer.early_stopping(val_loss, trainer.model)

            # 检查是否早停
            if trainer.early_stopping.early_stop:
                logger.log_entry("Status", "触发早停机制，停止训练")
                print("触发早停机制")
                break

        logger.update_status(is_training=False)
        logger.log_entry("System", f"检测训练完成 - 最佳验证损失: {best_val_loss:.4f}")
        print(f"\n训练完成! 最佳验证损失: {best_val_loss:.4f}")

    except Exception as e:
        error_msg = f"发生错误: {str(e)}\n{traceback.format_exc()}"
        if 'logger' in locals():
            logger.log_entry("Error", error_msg)
            logger.update_status(is_training=False)
        else:
            with open('detection_training_error.log', 'w', encoding='utf-8') as f:
                f.write(error_msg)
        print(error_msg)
        sys.exit(1)

if __name__ == "__main__":
    main()