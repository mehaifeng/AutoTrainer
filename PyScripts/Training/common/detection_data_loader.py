"""
COCO格式检测数据加载器
支持COCO格式的目标检测数据集加载和预处理
支持数据增强：使用 albumentations 库进行图像和 bbox 的同步变换
"""
import torch
from torch.utils.data import Dataset, DataLoader
from torchvision import transforms as T
from PIL import Image
import json
import os
from typing import Dict, Any, List, Tuple, Optional
import numpy as np

try:
    import albumentations as A
    from albumentations.pytorch import ToTensorV2
    ALBUMENTATIONS_AVAILABLE = True
except ImportError:
    ALBUMENTATIONS_AVAILABLE = False
    print("警告: albumentations 未安装，数据增强功能将被禁用")
    print("安装命令: pip install albumentations")


class COCODetectionDataset(Dataset):
    """COCO格式检测数据集"""
    
    def __init__(self, 
                 images_dir: str, 
                 annotation_file: str,
                 transforms: Optional[Any] = None):
        """
        初始化COCO检测数据集
        
        Args:
            images_dir: 图像文件夹路径
            annotation_file: COCO格式标注文件路径（JSON）
            transforms: 数据变换（可选）
        """
        self.images_dir = images_dir
        self.transforms = transforms
        
        # 加载COCO标注
        with open(annotation_file, 'r', encoding='utf-8') as f:
            self.coco_data = json.load(f)
        
        # 构建图像ID到文件名的映射
        self.images = {img['id']: img for img in self.coco_data['images']}
        
        # 构建类别ID映射（COCO类别ID不连续，需要映射到1-N，0保留给背景）
        self.categories = {cat['id']: cat for cat in self.coco_data['categories']}
        
        # 按类别名称排序，确保训练集和验证集的映射一致
        # 注意：检测模型中，0 是背景类，前景类从 1 开始
        sorted_categories = sorted(self.coco_data['categories'], key=lambda x: x['name'])
        self.cat_id_to_label = {cat['id']: idx + 1 for idx, cat in enumerate(sorted_categories)}
        self.label_to_cat_id = {idx + 1: cat['id'] for idx, cat in enumerate(sorted_categories)}
        
        # 存储类别名称到标签的映射（用于验证）
        self.cat_name_to_label = {cat['name']: idx + 1 for idx, cat in enumerate(sorted_categories)}
        
        # 按图像ID组织标注
        self.image_to_annotations = {}
        for ann in self.coco_data['annotations']:
            image_id = ann['image_id']
            if image_id not in self.image_to_annotations:
                self.image_to_annotations[image_id] = []
            self.image_to_annotations[image_id].append(ann)
        
        # 获取所有图像ID列表
        self.image_ids = list(self.images.keys())
        
        # 打印类别映射信息
        print(f"\n{'='*60}")
        print(f"COCO数据集加载完成")
        print(f"{'='*60}")
        print(f"图像数量: {len(self.image_ids)}")
        print(f"类别数量: {len(self.categories)}")
        print(f"\n类别映射 (COCO ID -> 模型标签):")
        for cat in sorted_categories:
            coco_id = cat['id']
            model_label = self.cat_id_to_label[coco_id]
            print(f"  '{cat['name']}': {coco_id} -> {model_label}")
        print(f"{'='*60}\n")
        
    def __len__(self):
        return len(self.image_ids)
    
    def __getitem__(self, idx: int) -> Tuple[torch.Tensor, Dict[str, torch.Tensor]]:
        """
        获取一个样本
        
        Args:
            idx: 索引
            
        Returns:
            (image, target) 其中target包含boxes, labels, image_id等
        """
        image_id = self.image_ids[idx]
        image_info = self.images[image_id]
        
        # 加载图像
        image_path = os.path.join(self.images_dir, image_info['file_name'])
        image = Image.open(image_path).convert('RGB')
        
        # 获取该图像的所有标注
        annotations = self.image_to_annotations.get(image_id, [])
        
        # 提取boxes和labels
        boxes = []
        labels = []
        areas = []
        iscrowd = []
        
        for ann in annotations:
            # COCO格式: [x, y, width, height]
            x, y, w, h = ann['bbox']
            
            # 验证bbox有效性
            if w <= 0 or h <= 0:
                # 跳过无效的标注（宽度或高度为0或负数）
                continue
                
            # 转换为 [x_min, y_min, x_max, y_max]
            x_min, y_min = x, y
            x_max, y_max = x + w, y + h
            
            # 确保坐标为正数且有效
            if x_min < 0 or y_min < 0 or x_max <= x_min or y_max <= y_min:
                # 跳过无效的bbox
                continue
            
            boxes.append([x_min, y_min, x_max, y_max])
            
            # 映射类别ID到连续标签
            cat_id = ann['category_id']
            
            # 验证类别ID是否在映射中
            if cat_id not in self.cat_id_to_label:
                print(f"警告: 未知的类别ID {cat_id}，跳过该标注")
                # 移除刚添加的box
                boxes.pop()
                continue
                
            labels.append(self.cat_id_to_label[cat_id])
            
            areas.append(ann.get('area', w * h))
            iscrowd.append(ann.get('iscrowd', 0))
        
        # 应用变换（albumentations 或 torchvision）
        if self.transforms is not None:
            # 检查是否是 albumentations 变换
            if ALBUMENTATIONS_AVAILABLE and isinstance(self.transforms, A.Compose):
                # 转换为 numpy 数组（albumentations 需要）
                image_np = np.array(image)
                
                # 准备 albumentations 格式的数据
                # albumentations 使用 [x_min, y_min, x_max, y_max] 格式，与 COCO 转换后一致
                transformed = self.transforms(
                    image=image_np,
                    bboxes=boxes,
                    labels=labels
                )
                
                # 提取变换后的数据
                image = transformed['image']  # 已经是 tensor
                boxes = transformed['bboxes']  # list of boxes
                labels = transformed['labels']  # list of labels
                
                # 如果有框被裁剪掉（albumentations 会自动移除）
                if len(boxes) == 0:
                    # 创建空的 tensor
                    boxes_tensor = torch.zeros((0, 4), dtype=torch.float32)
                    labels_tensor = torch.zeros((0,), dtype=torch.int64)
                    areas_tensor = torch.zeros((0,), dtype=torch.float32)
                    iscrowd_tensor = torch.zeros((0,), dtype=torch.int64)
                else:
                    # 转换为 tensor
                    boxes_tensor = torch.as_tensor(boxes, dtype=torch.float32)
                    labels_tensor = torch.as_tensor(labels, dtype=torch.int64)
                    
                    # 重新计算 area（增强后可能改变）
                    widths = boxes_tensor[:, 2] - boxes_tensor[:, 0]
                    heights = boxes_tensor[:, 3] - boxes_tensor[:, 1]
                    areas_tensor = widths * heights
                    
                    # iscrowd 保持原样（假设都是0）
                    iscrowd_tensor = torch.zeros((len(boxes),), dtype=torch.int64)
                    
                    # 验证增强后的 bbox
                    assert labels_tensor.min() >= 1, f"标签必须 >= 1，得到 min={labels_tensor.min()}"
                    assert not torch.isnan(boxes_tensor).any(), "boxes 包含 NaN"
                    assert not torch.isinf(boxes_tensor).any(), "boxes 包含 Inf"
                    assert (boxes_tensor[:, 2] > boxes_tensor[:, 0]).all(), "x_max 必须 > x_min"
                    assert (boxes_tensor[:, 3] > boxes_tensor[:, 1]).all(), "y_max 必须 > y_min"
                
                boxes = boxes_tensor
                labels = labels_tensor
                areas = areas_tensor
                iscrowd = iscrowd_tensor
                
            else:
                # 传统 torchvision 变换（仅图像）
                image = self.transforms(image)
                
                # 转换为tensor
                if len(boxes) > 0:
                    boxes = torch.as_tensor(boxes, dtype=torch.float32)
                    labels = torch.as_tensor(labels, dtype=torch.int64)
                    areas = torch.as_tensor(areas, dtype=torch.float32)
                    iscrowd = torch.as_tensor(iscrowd, dtype=torch.int64)
                    
                    # 额外验证：确保标签在有效范围内
                    assert labels.min() >= 1, f"标签必须 >= 1（0是背景类），得到 min={labels.min()}"
                    assert not torch.isnan(boxes).any(), "boxes 包含 NaN"
                    assert not torch.isinf(boxes).any(), "boxes 包含 Inf"
                    assert (boxes[:, 2] > boxes[:, 0]).all(), "x_max 必须 > x_min"
                    assert (boxes[:, 3] > boxes[:, 1]).all(), "y_max 必须 > y_min"
                else:
                    # 如果没有有效标注，创建空的tensor
                    boxes = torch.zeros((0, 4), dtype=torch.float32)
                    labels = torch.zeros((0,), dtype=torch.int64)
                    areas = torch.zeros((0,), dtype=torch.float32)
                    iscrowd = torch.zeros((0,), dtype=torch.int64)
        else:
            # 没有变换，手动转换
            image = T.ToTensor()(image)
            
            if len(boxes) > 0:
                boxes = torch.as_tensor(boxes, dtype=torch.float32)
                labels = torch.as_tensor(labels, dtype=torch.int64)
                areas = torch.as_tensor(areas, dtype=torch.float32)
                iscrowd = torch.as_tensor(iscrowd, dtype=torch.int64)
            else:
                boxes = torch.zeros((0, 4), dtype=torch.float32)
                labels = torch.zeros((0,), dtype=torch.int64)
                areas = torch.zeros((0,), dtype=torch.float32)
                iscrowd = torch.zeros((0,), dtype=torch.int64)
        
        # 构建target字典
        target = {
            'boxes': boxes,
            'labels': labels,
            'image_id': torch.tensor([image_id]),
            'area': areas,
            'iscrowd': iscrowd
        }
        
        return image, target
    
    def get_num_classes(self) -> int:
        """获取类别数（不包括背景）"""
        return len(self.categories)
    
    def get_category_names(self) -> List[str]:
        """获取类别名称列表"""
        sorted_cats = sorted(self.categories.items(), key=lambda x: self.cat_id_to_label[x[0]])
        return [cat['name'] for _, cat in sorted_cats]


class DetectionDataLoader:
    """检测任务数据加载器"""
    
    def __init__(self, config: Dict[str, Any]):
        """
        初始化检测数据加载器
        
        Args:
            config: 检测配置字典
        """
        self.config = config
        self.train_transform = self._get_train_transform()
        self.val_transform = self._get_val_transform()
        
    def _get_train_transform(self):
        """
        获取训练数据变换
        
        如果启用数据增强且 albumentations 可用，使用 albumentations
        否则回退到简单的 ToTensor
        """
        # 从配置中读取数据增强设置
        augmentation = self.config.get('data_augmentation', {})
        
        # 检查是否启用任何增强
        any_augmentation_enabled = any([
            augmentation.get('random_horizon_flip', False),
            augmentation.get('random_brightness', False),
            augmentation.get('random_contrast', False),
            augmentation.get('random_scale', False),
            augmentation.get('random_hue_saturation', False),
        ])
        
        # 如果启用了增强且 albumentations 可用
        if any_augmentation_enabled and ALBUMENTATIONS_AVAILABLE:
            transform_list = []
            
            # 1. 随机水平翻转
            if augmentation.get('random_horizon_flip', False):
                transform_list.append(A.HorizontalFlip(p=0.5))
                print("✓ 启用数据增强: 随机水平翻转")
            
            # 2. 随机缩放 (检测特有)
            if augmentation.get('random_scale', False):
                transform_list.append(
                    A.RandomScale(scale_limit=0.2, p=0.5)  # ±20% 缩放
                )
                print("✓ 启用数据增强: 随机缩放 (±20%)")
            
            # 3. 随机亮度和对比度
            if augmentation.get('random_brightness', False) or augmentation.get('random_contrast', False):
                brightness_limit = 0.2 if augmentation.get('random_brightness', False) else 0.0
                contrast_limit = 0.2 if augmentation.get('random_contrast', False) else 0.0
                
                if brightness_limit > 0 or contrast_limit > 0:
                    transform_list.append(
                        A.RandomBrightnessContrast(
                            brightness_limit=brightness_limit,
                            contrast_limit=contrast_limit,
                            p=0.5
                        )
                    )
                    if brightness_limit > 0 and contrast_limit > 0:
                        print("✓ 启用数据增强: 随机亮度对比度 (±20%)")
                    elif brightness_limit > 0:
                        print("✓ 启用数据增强: 随机亮度 (±20%)")
                    else:
                        print("✓ 启用数据增强: 随机对比度 (±20%)")
            
            # 4. 随机色调饱和度 (检测特有)
            if augmentation.get('random_hue_saturation', False):
                transform_list.append(
                    A.HueSaturationValue(
                        hue_shift_limit=10,      # 色调偏移 ±10
                        sat_shift_limit=20,      # 饱和度 ±20
                        val_shift_limit=10,      # 明度 ±10
                        p=0.5
                    )
                )
                print("✓ 启用数据增强: 随机色调饱和度")
            
            # 转换为 PyTorch Tensor（必须）
            transform_list.append(ToTensorV2())
            
            # 创建 albumentations Compose
            # bbox_params 指定 bbox 格式和标签字段
            return A.Compose(
                transform_list,
                bbox_params=A.BboxParams(
                    format='pascal_voc',  # [x_min, y_min, x_max, y_max]
                    label_fields=['labels'],
                    min_area=0,
                    min_visibility=0.3  # bbox至少30%可见才保留
                )
            )
        else:
            # 回退到简单变换
            if any_augmentation_enabled and not ALBUMENTATIONS_AVAILABLE:
                print("警告: 数据增强已启用但 albumentations 未安装，将使用简单变换")
                print("安装命令: pip install albumentations")
            
            # TorchVision的检测模型会在内部进行标准化
            return T.Compose([
                T.ToTensor(),
            ])
        
    def _get_val_transform(self):
        """获取验证数据变换"""
        # 同样，验证集也不需要标准化
        return T.Compose([
            T.ToTensor(),
            # 不使用 Normalize！检测模型内部会处理
        ])
        
    def get_dataloaders(self, batch_size: int, num_workers: int = 4) -> Tuple[DataLoader, DataLoader]:
        """
        获取训练和验证数据加载器
        
        Args:
            batch_size: 批次大小
            num_workers: 数据加载工作线程数
            
        Returns:
            (train_loader, val_loader)
        """
        # 从detection_config中获取路径
        train_images = self.config.get('train_images_path')
        train_ann = self.config.get('train_annotation_path')
        val_images = self.config.get('val_images_path')
        val_ann = self.config.get('val_annotation_path')
        
        if not train_images or not train_ann:
            raise ValueError("训练数据路径和标注文件路径未设置")
        
        # 创建训练数据集
        train_dataset = COCODetectionDataset(
            train_images,
            train_ann,
            transforms=self.train_transform
        )
        
        # 创建验证数据集
        if val_images and val_ann:
            val_dataset = COCODetectionDataset(
                val_images,
                val_ann,
                transforms=self.val_transform
            )
            
            # 验证训练集和验证集的类别一致性
            self._validate_category_consistency(train_dataset, val_dataset)
        else:
            # 如果没有验证集，使用训练集的一部分
            val_split = self.config.get('validation_split', 0.2)
            train_size = int((1 - val_split) * len(train_dataset))
            val_size = len(train_dataset) - train_size
            
            train_dataset, val_dataset = torch.utils.data.random_split(
                train_dataset, [train_size, val_size]
            )
        
        # 创建DataLoader
        # 注意：检测任务需要自定义collate_fn来处理不同数量的boxes
        train_loader = DataLoader(
            train_dataset,
            batch_size=batch_size,
            shuffle=True,
            num_workers=num_workers,
            collate_fn=self._collate_fn,
            pin_memory=True
        )
        
        val_loader = DataLoader(
            val_dataset,
            batch_size=batch_size,
            shuffle=False,
            num_workers=num_workers,
            collate_fn=self._collate_fn,
            pin_memory=True
        )
        
        return train_loader, val_loader
    
    @staticmethod
    def _validate_category_consistency(train_dataset, val_dataset):
        """
        验证训练集和验证集的类别一致性
        
        Args:
            train_dataset: 训练数据集
            val_dataset: 验证数据集
            
        Raises:
            ValueError: 如果类别不一致
        """
        # 获取类别名称映射
        train_cats = train_dataset.cat_name_to_label
        val_cats = val_dataset.cat_name_to_label
        
        # 检查类别名称集合是否一致
        train_names = set(train_cats.keys())
        val_names = set(val_cats.keys())
        
        if train_names != val_names:
            missing_in_val = train_names - val_names
            missing_in_train = val_names - train_names
            
            error_msg = "训练集和验证集的类别不一致：\n"
            if missing_in_val:
                error_msg += f"  验证集缺少类别: {sorted(missing_in_val)}\n"
            if missing_in_train:
                error_msg += f"  训练集缺少类别: {sorted(missing_in_train)}\n"
            error_msg += "请确保训练集和验证集的COCO标注文件包含相同的类别。"
            
            raise ValueError(error_msg)
        
        # 检查类别名称到标签的映射是否一致
        for cat_name in train_names:
            if train_cats[cat_name] != val_cats[cat_name]:
                error_msg = (
                    f"类别 '{cat_name}' 的标签映射不一致：\n"
                    f"  训练集: {train_cats[cat_name]}\n"
                    f"  验证集: {val_cats[cat_name]}\n"
                    f"这通常是因为类别在COCO文件中的顺序不同。"
                )
                raise ValueError(error_msg)
        
        print(f"✓ 类别验证通过: 训练集和验证集包含相同的 {len(train_names)} 个类别")
        print(f"  类别列表: {sorted(train_names)}")
    
    @staticmethod
    def _collate_fn(batch):
        """
        自定义collate函数，处理不同数量的目标框
        
        Args:
            batch: 批次数据
            
        Returns:
            (images, targets) 列表
        """
        images = []
        targets = []
        
        for image, target in batch:
            images.append(image)
            targets.append(target)
        
        # 检测模型需要图像列表，不是batch tensor
        # ToTensor() 已将图像转换为 float [0, 1] 范围
        return images, targets
