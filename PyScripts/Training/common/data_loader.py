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
from .utils import should_pin_memory


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
        支持三种模式：
        1. ImageFolder格式（目录结构）
        2. 图片目录 + 标注文件（TXT）
        3. 单独的训练集和验证集目录
        
        Args:
            batch_size: 批次大小
            num_workers: 数据加载工作线程数
            
        Returns:
            (train_loader, val_loader)
        """
        train_path = self.config.get('train_data_path')
        annotation_path = self.config.get('train_annotation_path')
        val_path = self.config.get('val_data_path')
        
        if not train_path:
            raise ValueError("训练数据路径未设置")
        
        # 检查是否有标注文件
        if annotation_path and Path(annotation_path).exists():
            print(f"使用标注文件模式加载数据: {annotation_path}")
            # 使用标注文件模式
            train_dataset = AnnotationDataset(
                train_path, 
                annotation_path, 
                transform=self.train_transform
            )
        else:
            # 使用ImageFolder模式
            print(f"使用ImageFolder模式加载数据: {train_path}")
            train_dataset = datasets.ImageFolder(train_path, transform=self.train_transform)
            
        train_loader = DataLoader(
            train_dataset,
            batch_size=batch_size,
            shuffle=True,
            num_workers=num_workers,
            pin_memory=should_pin_memory()
        )
        
        # 加载验证数据
        if val_path:
            val_dataset = datasets.ImageFolder(val_path, transform=self.val_transform)
            val_loader = DataLoader(
                val_dataset,
                batch_size=batch_size,
                shuffle=False,
                num_workers=num_workers,
                pin_memory=should_pin_memory()
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
                pin_memory=should_pin_memory()
            )
            
            val_loader = DataLoader(
                val_subset,
                batch_size=batch_size,
                shuffle=False,
                num_workers=num_workers,
                pin_memory=should_pin_memory()
            )
            
        return train_loader, val_loader


class AnnotationDataset(Dataset):
    """基于TXT标注文件的分类数据集"""
    
    def __init__(self, image_dir: str, annotation_path: str, transform=None):
        """
        初始化数据集
        
        Args:
            image_dir: 图片目录
            annotation_path: 标注文件路径（TXT格式：imageName className）
            transform: 图像变换
        """
        self.image_dir = Path(image_dir)
        self.transform = transform
        self.samples = []
        self.class_to_idx = {}
        self.classes = []
        
        # 读取标注文件
        self._load_annotations(annotation_path)
        
    def _load_annotations(self, annotation_path: str):
        """加载标注文件"""
        annotations = {}
        
        # 读取标注文件（格式：imageName className）
        with open(annotation_path, 'r', encoding='utf-8') as f:
            for line_num, line in enumerate(f, 1):
                line = line.strip()
                if not line:  # 跳过空行
                    continue
                    
                parts = line.split(None, 1)  # 按空格或制表符分割，最多分2部分
                if len(parts) == 2:
                    image_name, class_name = parts
                    annotations[image_name] = class_name
                else:
                    print(f"警告: 第{line_num}行格式不正确，跳过: {line}")
        
        if not annotations:
            raise ValueError(f"标注文件为空或格式不正确: {annotation_path}")
        
        # 建立类别到索引的映射
        unique_classes = sorted(set(annotations.values()))
        self.class_to_idx = {cls: idx for idx, cls in enumerate(unique_classes)}
        self.classes = unique_classes
        
        print(f"检测到 {len(unique_classes)} 个类别: {', '.join(unique_classes)}")
        
        # 查找图片文件
        image_extensions = {'.jpg', '.jpeg', '.png', '.bmp', '.gif', '.webp', '.tiff', '.tif'}
        
        # 建立图片文件索引（不带扩展名 -> 完整路径）
        image_files = {}
        for ext in image_extensions:
            for img_path in self.image_dir.rglob(f'*{ext}'):
                # 使用不带扩展名的文件名作为key
                name_without_ext = img_path.stem
                if name_without_ext not in image_files:
                    image_files[name_without_ext] = img_path
        
        # 也索引带扩展名的完整文件名
        for ext in image_extensions:
            for img_path in self.image_dir.rglob(f'*{ext}'):
                full_name = img_path.name
                if full_name not in image_files:
                    image_files[full_name] = img_path
        
        # 匹配标注和图片
        matched_count = 0
        for image_name, class_name in annotations.items():
            img_path = None
            
            # 尝试1: 直接匹配（可能带或不带扩展名）
            if image_name in image_files:
                img_path = image_files[image_name]
            # 尝试2: 去掉扩展名后匹配
            elif Path(image_name).stem in image_files:
                img_path = image_files[Path(image_name).stem]
            # 尝试3: 在图片目录中查找
            else:
                # 尝试添加不同扩展名
                for ext in image_extensions:
                    potential_path = self.image_dir / f"{image_name}{ext}"
                    if potential_path.exists():
                        img_path = potential_path
                        break
                    # 尝试递归查找
                    if not img_path:
                        for found_path in self.image_dir.rglob(f"{image_name}{ext}"):
                            img_path = found_path
                            break
            
            if img_path and img_path.exists():
                self.samples.append((str(img_path), self.class_to_idx[class_name]))
                matched_count += 1
            else:
                print(f"警告: 找不到图片 '{image_name}'")
        
        if len(self.samples) == 0:
            raise ValueError(f"在 {self.image_dir} 中未找到任何标注的图片")
        
        print(f"成功加载 {matched_count}/{len(annotations)} 张图片")
        if matched_count < len(annotations):
            print(f"警告: 有 {len(annotations) - matched_count} 张图片未找到")
    
    def __len__(self):
        return len(self.samples)
    
    def __getitem__(self, idx):
        img_path, label = self.samples[idx]
        
        try:
            image = Image.open(img_path).convert('RGB')
        except Exception as e:
            print(f"错误: 无法加载图片 {img_path}: {e}")
            # 返回一个黑色图片作为fallback
            image = Image.new('RGB', (224, 224), color='black')
        
        if self.transform:
            image = self.transform(image)
            
        return image, label


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
