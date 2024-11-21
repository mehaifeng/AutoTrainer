# model_configs.py
from datetime import datetime
import sys
import traceback
import argparse
import os
from typing import Dict, Any, Optional, Tuple
import torch
import torch.nn as nn
from torchvision import transforms, models, datasets
from abc import ABC, abstractmethod
from torch.utils.data import DataLoader, Dataset
import json


class BaseModelConfig(ABC):
    """模型配置的基类，定义了所有模型配置必须实现的接口"""

def __init__(self):

    @abstractmethod
    def get_model(self) -> nn.Module:
        """返回初始化好的模型"""
        pass

    @abstractmethod
    def get_transforms(self) -> transforms.Compose:
        """返回数据预处理转换"""
        pass

    @abstractmethod
    def get_loss_function(self) -> nn.Module:
        """返回损失函数"""
        pass

    @abstractmethod
    def get_input_size(self) -> Tuple[int, int]:
        """返回模型输入尺寸 (height, width)"""
        pass


class TorchvisionModelConfig(BaseModelConfig):
    """torchvision预训练模型的配置类"""

    def __init__(self, model_name: str, num_classes: int, pretrained: bool = True):
        self.model_name = model_name
        self.num_classes = num_classes
        self.pretrained = pretrained

        # 不同模型的默认输入尺寸
        self.model_input_sizes = {
            'resnet18': (224, 224),
            'resnet50': (224, 224),
            'vgg16': (224, 224),
            'densenet121': (224, 224),
            'efficientnet_b0': (224, 224),
            'mobilenet_v2': (224, 224),
            #'inception_v3': (299, 299),
        }

    def get_model(self) -> nn.Module:
        if not hasattr(models, self.model_name):
            raise ValueError(f"不支持的模型: {self.model_name}")

        # 记录模型名称
        model = getattr(models, self.model_name)(pretrained=self.pretrained)

        # 修改最后的分类层
        if self.model_name.startswith('resnet'):
            num_ftrs = model.fc.in_features
            model.fc = nn.Linear(num_ftrs, self.num_classes)
        elif self.model_name.startswith('vgg'):
            num_ftrs = model.classifier[-1].in_features
            model.classifier[-1] = nn.Linear(num_ftrs, self.num_classes)
        elif self.model_name.startswith('densenet'):
            num_ftrs = model.classifier.in_features
            model.classifier = nn.Linear(num_ftrs, self.num_classes)
        elif self.model_name.startswith('efficientnet'):
            num_ftrs = model.classifier[-1].in_features
            model.classifier[-1] = nn.Linear(num_ftrs, self.num_classes)
        elif self.model_name.startswith('mobilenet'):
            num_ftrs = model.classifier[-1].in_features
            model.classifier[-1] = nn.Linear(num_ftrs, self.num_classes)

        return model

    def get_transforms(self) -> transforms.Compose:
        input_size = self.get_input_size()
        return transforms.Compose([
            transforms.Resize(input_size),
            transforms.ToTensor(),
            transforms.Normalize(mean=[0.485, 0.456, 0.406],
                                 std=[0.229, 0.224, 0.225])
        ])

    def get_loss_function(self) -> nn.Module:
        return nn.CrossEntropyLoss()

    def get_input_size(self) -> Tuple[int, int]:
        return self.model_input_sizes.get(self.model_name, (224, 224))


class CustomModelConfig(BaseModelConfig):
    """自定义模型的配置类"""

    def __init__(self, model_class: type, model_params: Dict[str, Any],
                 input_size: Tuple[int, int], custom_transforms: Optional[transforms.Compose] = None):
        self.model_class = model_class
        self.model_params = model_params
        self._input_size = input_size
        self.custom_transforms = custom_transforms

    def get_model(self) -> nn.Module:
        return self.model_class(**self.model_params)

    def get_transforms(self) -> transforms.Compose:
        if self.custom_transforms:
            return self.custom_transforms

        return transforms.Compose([
            transforms.Resize(self._input_size),
            transforms.ToTensor(),
            transforms.Normalize(mean=[0.485, 0.456, 0.406],
                                 std=[0.229, 0.224, 0.225])
        ])

    def get_loss_function(self) -> nn.Module:
        return nn.CrossEntropyLoss()

    def get_input_size(self) -> Tuple[int, int]:
        return self._input_size


# trainer.py
class ModelTrainer:
    """模型训练器类"""

    def __init__(self, config: Dict[str, Any], model_config: BaseModelConfig, logger: Any):
        self.config = config
        self.model_config = model_config
        self.logger = logger
        self.device = torch.device("cuda" if torch.cuda.is_available() else "cpu")

        # 记录模型名称
        self.logger.log_entry("ModelName", f"模型名称: {self.config.get("pretrained_model")}")
        # 记录训练硬件
        self.logger.log_entry("TrainingDevice", f"用于训练的硬件: {self.device}")

        # 初始化模型和相关组件
        self.model = self.model_config.get_model().to(self.device)
        self.criterion = self.model_config.get_loss_function()
        self.optimizer = self._get_optimizer()
        self.scheduler = self._get_scheduler()


    def _get_optimizer(self):
        optimizer_class = getattr(torch.optim, self.config['optimizer'])
        return optimizer_class(
            self.model.parameters(),
            lr=self.config['learning_rate'],
            # weight_decay=self.config['weight_decay']
        )

    def _get_scheduler(self):
        if self.config['lr_scheduler'] == 'ReduceLROnPlateau':
            return torch.optim.lr_scheduler.ReduceLROnPlateau(
                self.optimizer, mode='max', patience=5, factor=0.1
            )
        elif self.config['lr_scheduler'] == 'StepLR':
            return torch.optim.lr_scheduler.StepLR(
                self.optimizer, step_size=30, gamma=0.1
            )
        return None

    def prepare_data(self) -> Tuple[DataLoader, DataLoader]:
        transform = self.model_config.get_transforms()

        # 加载训练数据集
        train_dataset = datasets.ImageFolder(self.config['train_data_path'], transform=transform)

        # 处理验证集
        if self.config.get('val_data_path'):
            val_dataset = datasets.ImageFolder(self.config['val_data_path'], transform=transform)
        else:
            train_size = int((1 - self.config['validation_split']) * len(train_dataset))
            val_size = len(train_dataset) - train_size
            train_dataset, val_dataset = torch.utils.data.random_split(
                train_dataset, [train_size, val_size]
            )

        train_loader = DataLoader(
            train_dataset,
            batch_size=self.config['batch_size'],
            shuffle=True
        )
        val_loader = DataLoader(
            val_dataset,
            batch_size=self.config['batch_size']
        )

        return train_loader, val_loader

    def train_epoch(self, train_loader: DataLoader) -> Tuple[float, float]:
        self.model.train()
        total_loss = 0
        correct = 0
        total = 0

        for batch_idx, (inputs, targets) in enumerate(train_loader):
            inputs, targets = inputs.to(self.device), targets.to(self.device)

            self.optimizer.zero_grad()
            outputs = self.model(inputs)
            loss = self.criterion(outputs, targets)
            loss.backward()
            self.optimizer.step()

            total_loss += loss.item()
            _, predicted = outputs.max(1)
            total += targets.size(0)
            correct += predicted.eq(targets).sum().item()

            if batch_idx % 10 == 0:
                self.logger.log_entry(
                    "Training",
                    f"Batch [{batch_idx}/{len(train_loader)}] "
                    f"Loss: {loss.item():.4f} Acc: {100. * correct / total:.2f}%"
                )

        return total_loss / len(train_loader), 100. * correct / total

    def validate(self, val_loader: DataLoader) -> Tuple[float, float]:
        self.model.eval()
        total_loss = 0
        correct = 0
        total = 0

        with torch.no_grad():
            for inputs, targets in val_loader:
                inputs, targets = inputs.to(self.device), targets.to(self.device)
                outputs = self.model(inputs)
                loss = self.criterion(outputs, targets)

                total_loss += loss.item()
                _, predicted = outputs.max(1)
                total += targets.size(0)
                correct += predicted.eq(targets).sum().item()

        return total_loss / len(val_loader), 100. * correct / total

    def save_checkpoint(self, epoch: int, best_val_acc: float):
        checkpoint = {
            'epoch': epoch,
            'model_state_dict': self.model.state_dict(),
            'optimizer_state_dict': self.optimizer.state_dict(),
            'scheduler_state_dict': self.scheduler.state_dict() if self.scheduler else None,
            'config': self.config,
            'best_val_acc': best_val_acc
        }
        checkpoint_path = f"{self.config['model_output_path']}.checkpoint"
        torch.save(checkpoint, checkpoint_path)
        self.logger.log_entry("Status", f"保存检查点：epoch {epoch}")

    def save_best_model(self, val_acc: float, epoch: int):
        torch.save(self.model.state_dict(), self.config['model_output_path'])
        self.logger.log_entry("Status", f"保存最佳模型，验证准确率: {val_acc:.4f}")

class TrainingLogger:
    def __init__(self, base_log_path):
        # 生成唯一的日志文件名
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        filename = os.path.basename(base_log_path)
        name, ext = os.path.splitext(filename)
        self.log_path = os.path.join(
            os.path.dirname(base_log_path),
            f"{name}_{timestamp}{ext}"
        )

        self.log_data = {
            "config": None,
            "status": {
                "is_training": False,
                "current_epoch": 0,
                "total_epochs": 0,
                "best_validation_accuracy": None,
                "early_stopping_counter": 0,
                "current_learning_rate": 0.0
            },
            "entries": []
        }

        # 确保日志目录存在
        os.makedirs(os.path.dirname(self.log_path), exist_ok=True)

    def update_status(self, **kwargs):
        self.log_data["status"].update(kwargs)
        self._save_log()

    def log_entry(self, message_type, message, epoch=None, metrics=None):
        entry = {
            "timestamp": datetime.now().isoformat(),
            "type": message_type,
            "message": message,
            "epoch": epoch,
            "metrics": metrics
        }
        self.log_data["entries"].append(entry)
        self._save_log()
        # 同时打印到控制台
        print(f"[{entry['timestamp']}] {message_type}: {message}")

    def _save_log(self):
        with open(self.log_path, 'w', encoding='utf-8') as f:
            json.dump(self.log_data, f, ensure_ascii=False, indent=2)

def load_config():
    parser = argparse.ArgumentParser()
    parser.add_argument('--config', type=str, required=True, help='Path to config JSON file')
    args = parser.parse_args()

    if not os.path.exists(args.config):
        raise FileNotFoundError(f"配置文件不存在: {args.config}")

    with open(args.config, 'r', encoding='utf-8') as f:
        return json.load(f)
# main.py
def main():
    try:
        # 加载配置
        config = load_config()
        logger = TrainingLogger(config['log_output_path'])
        logger.log_data["config"] = config

        # 创建模型配置
        num_classes = len(os.listdir(config['train_data_path']))
        model_config = TorchvisionModelConfig(
            model_name=config['pretrained_model'],
            num_classes=num_classes,
            pretrained=True
        )

        # 创建训练器
        trainer = ModelTrainer(config, model_config, logger)

        # 准备数据
        train_loader, val_loader = trainer.prepare_data()

        # 训练循环
        best_val_acc = 0.0
        early_stopping_counter = 0

        for epoch in range(config['epochs']):
            # 训练一个epoch
            train_loss, train_acc = trainer.train_epoch(train_loader)

            # 验证
            val_loss, val_acc = trainer.validate(val_loader)

            # 记录指标
            metrics = {
                "train_loss": train_loss,
                "train_accuracy": train_acc,
                "validation_loss": val_loss,
                "validation_accuracy": val_acc,
                "learning_rate": trainer.optimizer.param_groups[0]['lr']
            }

            logger.log_entry(
                "Validation",
                f"Epoch {epoch + 1} 完成 - 验证准确率: {val_acc:.2f}%",
                epoch=epoch + 1,
                metrics=metrics
            )

            # 更新学习率
            if isinstance(trainer.scheduler, torch.optim.lr_scheduler.ReduceLROnPlateau):
                trainer.scheduler.step(val_acc)
            elif trainer.scheduler:
                trainer.scheduler.step()

            # 保存最佳模型
            if val_acc > best_val_acc:
                best_val_acc = val_acc
                early_stopping_counter = 0
                trainer.save_best_model(val_acc, epoch)
            else:
                early_stopping_counter += 1

            # 检查是否早停
            if early_stopping_counter >= config['early_stopping_rounds']:
                logger.log_entry("Status", "触发早停机制，停止训练")
                trainer.save_best_model(val_acc, epoch)
                break

            # 保存检查点
            trainer.save_checkpoint(epoch, best_val_acc)

        logger.update_status(is_training=False)
        logger.log_entry("System", "训练完成")

    except Exception as e:
        error_msg = f"发生错误: {str(e)}\n{traceback.format_exc()}"
        if 'logger' in locals():
            logger.log_entry("Error", error_msg)
            logger.update_status(is_training=False)
        else:
            with open('training_error.log', 'w', encoding='utf-8') as f:
                f.write(error_msg)
        sys.exit(1)


if __name__ == "__main__":
    main()