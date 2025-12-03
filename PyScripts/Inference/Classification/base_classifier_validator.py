"""
分类任务基础验证器
提供分类模型验证的通用框架
"""
import torch
import torch.nn as nn
from torchvision import transforms
from torch.utils.data import DataLoader, Dataset
from PIL import Image
import json
import os
import sys
import time
from pathlib import Path
from typing import Dict, List, Tuple, Any
from abc import ABC, abstractmethod
import numpy as np
from sklearn.metrics import accuracy_score, recall_score, f1_score, confusion_matrix


class ImageFolderDataset(Dataset):
    """图片文件夹数据集"""
    
    def __init__(self, root_dir: str, transform=None):
        self.root_dir = Path(root_dir)
        self.transform = transform
        self.samples = []
        self.class_to_idx = {}
        self.classes = []
        
        # 扫描类别目录
        class_dirs = sorted([d for d in self.root_dir.iterdir() 
                           if d.is_dir()])
        
        if not class_dirs:
            raise ValueError(f"No class directories found in {root_dir}")
        
        self.classes = [d.name for d in class_dirs]
        self.class_to_idx = {cls: idx for idx, cls in enumerate(self.classes)}
        
        # 扫描图片
        image_extensions = {'.jpg', '.jpeg', '.png', '.bmp', '.webp', '.tiff', '.tif'}
        for class_dir in class_dirs:
            class_name = class_dir.name
            class_idx = self.class_to_idx[class_name]
            
            for img_path in class_dir.iterdir():
                if img_path.suffix.lower() in image_extensions:
                    self.samples.append((str(img_path), class_idx))
    
    def __len__(self):
        return len(self.samples)
    
    def __getitem__(self, idx):
        img_path, label = self.samples[idx]
        image = Image.open(img_path).convert('RGB')
        
        if self.transform:
            image = self.transform(image)
        
        return image, label, img_path


class BaseClassificationValidator(ABC):
    """分类验证器基类"""
    
    def __init__(self, config_path: str):
        """
        初始化验证器
        
        Args:
            config_path: 配置文件路径
        """
        with open(config_path, 'r') as f:
            self.config = json.load(f)
        
        self.device = torch.device('cuda' if torch.cuda.is_available() else 'cpu')
        self.model = None
        self.transform = None
        
        print(f"使用设备: {self.device}", flush=True)
    
    @abstractmethod
    def get_input_size(self) -> int:
        """获取模型输入尺寸"""
        pass
    
    @abstractmethod
    def load_model(self, model_path: str, num_classes: int) -> nn.Module:
        """
        加载模型
        
        Args:
            model_path: 模型权重路径
            num_classes: 类别数
            
        Returns:
            加载好的模型
        """
        pass
    
    def _get_num_classes_from_model(self, model_path: str) -> int:
        """
        从模型checkpoint获取类别数
        
        Args:
            model_path: 模型文件路径
            
        Returns:
            类别数
        """
        try:
            checkpoint = torch.load(model_path, map_location='cpu')
            
            if isinstance(checkpoint, dict):
                # 优先从metadata读取
                if 'num_classes' in checkpoint:
                    num_classes = checkpoint['num_classes']
                    print(f"从metadata读取类别数: {num_classes}", flush=True)
                    return num_classes
                
                # 回退：从权重shape推断
                if 'model_state_dict' in checkpoint:
                    state_dict = checkpoint['model_state_dict']
                elif 'state_dict' in checkpoint:
                    state_dict = checkpoint['state_dict']
                else:
                    state_dict = checkpoint
                
                # 从分类头推断类别数
                # 不同模型的分类头key不同
                classifier_keys = [
                    'classifier.1.weight',  # EfficientNet
                    'classifier.3.weight',  # MobileNetV3
                    'fc.weight',            # ResNet等
                    'head.weight',          # Vision Transformer
                ]
                
                for key in classifier_keys:
                    if key in state_dict:
                        num_classes = state_dict[key].shape[0]
                        print(f"从权重shape推断类别数: {num_classes} (key: {key})", flush=True)
                        return num_classes
                
                raise ValueError("无法从模型文件推断类别数")
            
            raise ValueError("模型文件格式不正确")
            
        except Exception as e:
            raise RuntimeError(f"读取模型metadata失败: {e}")
    
    def prepare_transforms(self):
        """准备数据预处理"""
        input_size = self.get_input_size()
        
        self.transform = transforms.Compose([
            transforms.Resize((input_size, input_size)),
            transforms.ToTensor(),
            transforms.Normalize(
                mean=[0.485, 0.456, 0.406],
                std=[0.229, 0.224, 0.225]
            )
        ])
    
    def prepare_dataloader(self, batch_size: int = 32) -> Tuple[DataLoader, List[str]]:
        """
        准备数据加载器
        
        Args:
            batch_size: 批次大小
            
        Returns:
            (dataloader, class_names)
        """
        validation_path = self.config['validation_data_path']
        
        print(f"加载验证集: {validation_path}", flush=True)
        
        dataset = ImageFolderDataset(validation_path, transform=self.transform)
        
        dataloader = DataLoader(
            dataset,
            batch_size=batch_size,
            shuffle=False,
            num_workers=4,
            pin_memory=True
        )
        
        print(f"找到 {len(dataset.classes)} 个类别: {', '.join(dataset.classes)}", flush=True)
        print(f"总计 {len(dataset)} 张图片", flush=True)
        
        return dataloader, dataset.classes
    
    def validate(self) -> Dict[str, Any]:
        """
        执行验证
        
        Returns:
            验证结果字典
        """
        start_time = time.time()
        
        model_path = self.config['model_weights_path']
        
        # 首先从模型metadata获取num_classes
        num_classes_from_model = self._get_num_classes_from_model(model_path)
        
        # 准备组件
        print("准备数据预处理...", flush=True)
        self.prepare_transforms()
        
        print("加载数据集...", flush=True)
        dataloader, class_names = self.prepare_dataloader()
        num_classes_from_data = len(class_names)
        
        # 验证类别数是否一致
        if num_classes_from_model != num_classes_from_data:
            print(f"警告: 模型类别数({num_classes_from_model})与验证集类别数({num_classes_from_data})不一致", 
                  flush=True)
            print(f"将使用模型的类别数: {num_classes_from_model}", flush=True)
        
        num_classes = num_classes_from_model
        
        # 加载模型
        print(f"加载模型: {model_path}", flush=True)
        print(f"类别数: {num_classes}", flush=True)
        self.model = self.load_model(model_path, num_classes)
        self.model.to(self.device)
        self.model.eval()
        
        # 推理
        print("开始推理...", flush=True)
        all_preds = []
        all_labels = []
        image_results = []
        
        with torch.no_grad():
            for batch_idx, (images, labels, img_paths) in enumerate(dataloader):
                images = images.to(self.device)
                outputs = self.model(images)
                
                # 获取softmax概率
                probabilities = torch.nn.functional.softmax(outputs, dim=1)
                confidences, predicted = torch.max(probabilities, 1)
                
                all_preds.extend(predicted.cpu().numpy())
                all_labels.extend(labels.numpy())
                
                # 记录每张图片的结果（包含置信度）
                for i, img_path in enumerate(img_paths):
                    pred_class = class_names[predicted[i].item()]
                    confidence = confidences[i].item()
                    image_results.append({
                        'path': img_path,
                        'predicted_class': pred_class,
                        'confidence': round(confidence, 4)  # 保留4位小数
                    })
                
                if (batch_idx + 1) % 10 == 0:
                    print(f"已处理 {(batch_idx + 1) * len(images)}/{len(dataloader.dataset)} 张图片", flush=True)
        
        # 计算指标
        print("计算指标...", flush=True)
        accuracy = accuracy_score(all_labels, all_preds)
        recall = recall_score(all_labels, all_preds, average='macro')
        f1 = f1_score(all_labels, all_preds, average='macro')
        conf_matrix = confusion_matrix(all_labels, all_preds)
        
        elapsed_time = time.time() - start_time
        
        # 构建结果
        results = {
            'metrics': {
                'time': elapsed_time,
                'accuracy': float(accuracy),
                'recall_macro': float(recall),
                'f1_macro': float(f1)
            },
            'chart_data': {
                'confusion_matrix': conf_matrix.tolist(),
                'labels': class_names
            },
            'image_results': image_results
        }
        
        print(f"\n验证完成!", flush=True)
        print(f"准确率: {accuracy:.4f}", flush=True)
        print(f"召回率: {recall:.4f}", flush=True)
        print(f"F1分数: {f1:.4f}", flush=True)
        print(f"耗时: {elapsed_time:.2f}秒", flush=True)
        
        return results
    
    def run(self):
        """运行验证并保存结果"""
        try:
            results = self.validate()
            
            # 保存结果
            config_dir = Path(self.config['model_weights_path']).parent.parent / 'Configs'
            output_path = config_dir / 'classification_results.json'
            
            with open(output_path, 'w') as f:
                json.dump(results, f, indent=4)
            
            print(f"\n结果已保存至: {output_path}", flush=True)
            
        except Exception as e:
            print(f"验证过程出错: {e}", file=sys.stderr, flush=True)
            import traceback
            traceback.print_exc()
            sys.exit(1)
