"""
数据加载和增强模块
处理图像数据集的加载、预处理和增强
"""
import torch
from torch.utils.data import DataLoader, Dataset
from torchvision import transforms, datasets
from typing import Tuple, Optional, Dict, Any
from pathlib import Path
import albumentations as A
from albumentations.pytorch import ToTensorV2
from PIL import Image


class ClassificationDataLoader:
    """分类任务数据加载器"""
    
    def __init__(self, config: Dict[str, Any], input_size: int = 224):
        """
        初始化数据加载器
        
        Args:
            config: 分类配置字典
            input_size: 输入图像大小
        """
        self.config = config
        self.input_size = input_size
        self.train_transform = self._get_train_transform()
        self.val_transform = self._get_val_transform()
        
    def _get_train_transform(self) -> transforms.Compose:
        """获取训练数据增强"""
        augmentation = self.config.get('data_augmentation', {})
        
        transform_list = [
            transforms.Resize((self.input_size, self.input_size)),
        ]
        
        # 根据配置添加数据增强
        if augmentation.get('random_horizon_flip', False):
            transform_list.append(transforms.RandomHorizontalFlip())
            
        if augmentation.get('random_vertical_flip', False):
            transform_list.append(transforms.RandomVerticalFlip())
            
        if augmentation.get('random_rotation', False):
            transform_list.append(transforms.RandomRotation(15))
            
        if augmentation.get('random_brightness', False):
            transform_list.append(transforms.ColorJitter(brightness=0.2))
            
        if augmentation.get('random_contrast', False):
            transform_list.append(transforms.ColorJitter(contrast=0.2))
            
        # 标准化
        transform_list.extend([
            transforms.ToTensor(),
            transforms.Normalize(mean=[0.485, 0.456, 0.406], 
                               std=[0.229, 0.224, 0.225])
        ])
        
        return transforms.Compose(transform_list)
        
    def _get_val_transform(self) -> transforms.Compose:
        """获取验证数据变换"""
        return transforms.Compose([
            transforms.Resize((self.input_size, self.input_size)),
            transforms.ToTensor(),
            transforms.Normalize(mean=[0.485, 0.456, 0.406], 
                               std=[0.229, 0.224, 0.225])
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
        train_path = self.config.get('train_data_path')
        val_path = self.config.get('val_data_path')
        
        if not train_path:
            raise ValueError("训练数据路径未设置")
            
        # 加载训练数据
        train_dataset = datasets.ImageFolder(train_path, transform=self.train_transform)
        train_loader = DataLoader(
            train_dataset,
            batch_size=batch_size,
            shuffle=True,
            num_workers=num_workers,
            pin_memory=True
        )
        
        # 加载验证数据
        if val_path:
            val_dataset = datasets.ImageFolder(val_path, transform=self.val_transform)
            val_loader = DataLoader(
                val_dataset,
                batch_size=batch_size,
                shuffle=False,
                num_workers=num_workers,
                pin_memory=True
            )
        else:
            # 如果没有验证集，使用训练集的一部分
            val_split = self.config.get('validation_split', 0.2)
            train_size = int((1 - val_split) * len(train_dataset))
            val_size = len(train_dataset) - train_size
            
            train_subset, val_subset = torch.utils.data.random_split(
                train_dataset, [train_size, val_size]
            )
            
            train_loader = DataLoader(
                train_subset,
                batch_size=batch_size,
                shuffle=True,
                num_workers=num_workers,
                pin_memory=True
            )
            
            val_loader = DataLoader(
                val_subset,
                batch_size=batch_size,
                shuffle=False,
                num_workers=num_workers,
                pin_memory=True
            )
            
        return train_loader, val_loader


class AlbumentationsDataset(Dataset):
    """使用Albumentations的数据集（可选，用于更复杂的增强）"""
    
    def __init__(self, image_paths, labels, transform=None):
        """
        初始化数据集
        
        Args:
            image_paths: 图像路径列表
            labels: 标签列表
            transform: Albumentations变换
        """
        self.image_paths = image_paths
        self.labels = labels
        self.transform = transform
        
    def __len__(self):
        return len(self.image_paths)
        
    def __getitem__(self, idx):
        image = Image.open(self.image_paths[idx]).convert('RGB')
        image = np.array(image)
        label = self.labels[idx]
        
        if self.transform:
            augmented = self.transform(image=image)
            image = augmented['image']
            
        return image, label
