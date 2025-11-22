# model_configs.py
from datetime import datetime
import sys
import traceback
import argparse
import os
from typing import Dict, Any, Optional, Tuple
import numpy as np
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

    def __init__(self, model_name: str, num_classes: int, config:Dict[str,Any], logger: Any = None):
        self.model_name = model_name
        self.num_classes = num_classes
        self.config = config  # 存储配置字典
        self.logger = logger

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
    def _modify_classifier(self, model: nn.Module):
        """
        根据模型名称修改模型的最后分类层，以匹配 self.num_classes。

        Args:
            model: 需要修改的 PyTorch 模型对象。
        """
        if self.model_name.startswith('resnet'):
            num_ftrs = model.fc.in_features
            model.fc = nn.Linear(num_ftrs, self.num_classes)
            self.logger.log_entry("Debug", f"Modified {self.model_name} final layer (fc) to output {self.num_classes} classes.")
            print(f"Modified {self.model_name} final layer (fc) to output {self.num_classes} classes.")
        elif self.model_name.startswith('vgg'):
            # VGG 的 classifier 是一个 Sequential，最后一层是 Linear
            num_ftrs = model.classifier[-1].in_features
            model.classifier[-1] = nn.Linear(num_ftrs, self.num_classes)
            self.logger.log_entry("Debug", f"Modified {self.model_name} final layer (classifier[-1]) to output {self.num_classes} classes.")
            print(f"Modified {self.model_name} final layer (classifier[-1]) to output {self.num_classes} classes.")
        elif self.model_name.startswith('densenet'):
            # DenseNet 的 classifier 直接是 Linear
            num_ftrs = model.classifier.in_features
            model.classifier = nn.Linear(num_ftrs, self.num_classes)
            self.logger.log_entry("Debug", f"Modified {self.model_name} final layer (classifier) to output {self.num_classes} classes.")
            print(f"Modified {self.model_name} final layer (classifier) to output {self.num_classes} classes.")
        elif self.model_name.startswith('efficientnet'):
             # EfficientNet 的 classifier 是一个 Sequential，最后一层是 Linear
            num_ftrs = model.classifier[-1].in_features
            model.classifier[-1] = nn.Linear(num_ftrs, self.num_classes)
            self.logger.log_entry("Debug", f"Modified {self.model_name} final layer (classifier[-1]) to output {self.num_classes} classes.")
            print(f"Modified {self.model_name} final layer (classifier[-1]) to output {self.num_classes} classes.")
        elif self.model_name.startswith('mobilenet'):
            # MobileNet 的 classifier 是一个 Sequential，最后一层是 Linear
            num_ftrs = model.classifier[-1].in_features
            model.classifier[-1] = nn.Linear(num_ftrs, self.num_classes)
            self.logger.log_entry("Debug", f"Modified {self.model_name} final layer (classifier[-1]) to output {self.num_classes} classes.")
            print(f"Modified {self.model_name} final layer (classifier[-1]) to output {self.num_classes} classes.")
        # 如果有其他模型类型，可以在这里继续添加 elif 判断和修改逻辑
        else:
            # 如果是其他未知模型，打印警告或根据需要处理
            self.logger.log_entry("Warning", f"Model type {self.model_name} not specifically handled for classifier modification.")
            print(f"Warning: Model type {self.model_name} not specifically handled for classifier modification.")

    def get_model(self) -> nn.Module:
        if not hasattr(models, self.model_name):
            raise ValueError(f"不支持的模型: {self.model_name}")

        # 记录模型名称
        model_loader = getattr(models, self.model_name, None) # [cite: 5] Use getattr with default None
        if model_loader is None:
            raise ValueError(f"不支持的模型: {self.model_name}")

        # 检查是否提供了本地预训练权重文件路径
        local_weights_path = self.config.get('local_weights_path', '')
        is_local_weights = False
        
        # 检查是否为文件路径且文件存在
        if isinstance(local_weights_path, str) and local_weights_path.endswith(('.pth', '.pt')) and os.path.isfile(local_weights_path):
            is_local_weights = True
            self.logger.log_entry("Debug", f"检测到本地预训练权重文件: {local_weights_path}")
            print(f"检测到本地预训练权重文件: {local_weights_path}")
        
        # 初始化模型 - 如果是本地权重则不使用预训练
        if is_local_weights:
            # 创建不带预训练权重的模型
            model = model_loader(weights=None)
            # 该不带预训练权重的模型需要修改全连接层结构，主要是修改分类数量
            self._modify_classifier(model)
            
            try:
                # 加载本地权重
                state_dict = torch.load(local_weights_path, map_location='cpu')
                assert os.path.exists(local_weights_path), "file {} does not exist.".format(local_weights_path)
                
                # 尝试加载权重
                model.load_state_dict(state_dict)
                self.logger.log_entry("Info", f"成功从本地文件加载权重: {local_weights_path}")
                print(f"成功从本地文件加载权重: {local_weights_path}")
            except Exception as e:
                error_msg = f"加载本地权重文件失败: {e}"
                self.logger.log_entry("Error", error_msg)
                print(f"错误: {error_msg}")
                
                # 出错时回退到默认初始化
                self.logger.log_entry("Warning", "回退到默认模型初始化方式")
                print("警告: 回退到默认模型初始化方式")
                
                # --- 使用标准初始化方式作为回退 ---
                try:
                    # 统一使用DEFAULT权重，简化处理逻辑
                    self.logger.log_entry("Debug", f"Loading model {self.model_name} with default weights")
                    print(f"Loading model {self.model_name} with default weights")
                    model = model_loader(weights='DEFAULT')

                except Exception as e:
                    # 更广泛的回退，以防上述逻辑对某些模型失败
                    self.logger.log_entry("Debug", f"Error loading weights via enum for {self.model_name}: {e}. Using default weights.")
                    print(f"Warning: Error loading weights via enum for {self.model_name}: {e}. Using default weights.")
                    try:
                        # 尝试使用默认权重
                        model = model_loader(weights='DEFAULT')
                    except:
                        # 如果不支持DEFAULT，尝试使用旧的API（但可能会有警告）
                        try:
                            model = model_loader(pretrained=True)
                        except:
                            # 最后的回退，使用IMAGENET1K_V1
                            model = model_loader(weights="IMAGENET1K_V1")
        else:
            # --- 使用标准初始化方式 ---
            try:
                # 统一使用DEFAULT权重，简化处理逻辑
                self.logger.log_entry("Debug", f"Loading model {self.model_name} with default weights")
                print(f"Loading model {self.model_name} with default weights")
                model = model_loader(weights='DEFAULT')

            except Exception as e:
                # 更广泛的回退，以防上述逻辑对某些模型失败
                self.logger.log_entry("Debug", f"Error loading default weights for {self.model_name}: {e}. Using alternative weights.")
                print(f"Warning: Error loading default weights for {self.model_name}: {e}. Using alternative weights.")
                try:
                    model = model_loader(weights="IMAGENET1K_V1")
                except:
                    # 最后的回退，使用旧的API（但可能会有警告）
                    model = model_loader(pretrained=True)

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
        return self._create_loss_from_config()
        #return nn.CrossEntropyLoss()

    # 创建损失函数的辅助方法（可以在CustomModelConfig中共享或复制）
    def _create_loss_from_config(self) -> nn.Module:
        # 检查损失函数配置是否存在
        if 'loss_function_config' not in self.config or not self.config['loss_function_config']:
            self.logger.log_entry("Warning", "loss_function_config not found in config. Using default CrossEntropyLoss.")
            print("Warning: loss_function_config not found in config. Using default CrossEntropyLoss.")
            return nn.CrossEntropyLoss() # Sensible default

        loss_config = self.config['loss_function_config']
        loss_type_str = loss_config.get('type')
        loss_args = loss_config.get('args', {})

        if not loss_type_str:
            self.logger.log_entry("Warning", "Loss function 'type' not specified in config. Using default CrossEntropyLoss.")
            print("Warning: Loss function 'type' not specified in config. Using default CrossEntropyLoss.")
            return nn.CrossEntropyLoss() # Default if type is missing

        # --- 参数解析和验证 ---
        parsed_args = {}
        device = torch.device("cuda" if torch.cuda.is_available() else "cpu") # Get device for weights

        # 处理 'weight' 参数（用于 CrossEntropy, BCE, BCEWithLogits）
        if 'weight' in loss_args and loss_args['weight'] is not None and loss_args['weight'] != '':
            try:
                weight_val = loss_args['weight']
                if isinstance(weight_val, str):
                    # 尝试解析逗号分隔的字符串
                    weights_list = [float(w.strip()) for w in weight_val.split(',') if w.strip()]
                elif isinstance(weight_val, list):
                    weights_list = [float(w) for w in weight_val]
                else:
                    raise ValueError(f"Unsupported weight format: {type(weight_val)}. Expecting list or comma-separated string.")

                if weights_list: # 解析后确保列表不为空
                    parsed_args['weight'] = torch.tensor(weights_list, dtype=torch.float).to(device)
                else:
                     print(f"Warning: Parsed empty weight list from input: {loss_args['weight']}. Ignoring weight.")

            except (ValueError, TypeError) as e:
                print(f"Warning: Could not parse 'weight' parameter ({loss_args['weight']}). Ignoring. Error: {e}")


        # 处理 'pos_weight' 参数（用于 BCEWithLogitsLoss）
        if 'pos_weight' in loss_args and loss_args['pos_weight'] is not None and loss_args['pos_weight'] != '':
             try:
                 # pos_weight 对于 BCEWithLogitsLoss 应该是每个类别的单个标量或张量
                 # 通常它是一个应用于所有正样本的单个标量。
                 # 假设这里传递的是一个标量。
                 pos_weight_val = float(loss_args['pos_weight'])
                 # PyTorch 期望 pos_weight 是一个 Tensor。
                 parsed_args['pos_weight'] = torch.tensor([pos_weight_val], dtype=torch.float).to(device)
             except (ValueError, TypeError) as e:
                print(f"Warning: Could not parse 'pos_weight' parameter ({loss_args['pos_weight']}). Ignoring. Error: {e}")

        # 处理 'label_smoothing' 参数（用于 CrossEntropyLoss）
        if 'label_smoothing' in loss_args and loss_args['label_smoothing'] is not None and loss_args['label_smoothing'] != '':
            try:
                parsed_args['label_smoothing'] = float(loss_args['label_smoothing'])
            except (ValueError, TypeError) as e:
                print(f"Warning: Could not parse 'label_smoothing' parameter ({loss_args['label_smoothing']}). Ignoring. Error: {e}")

        # 处理 'beta' 参数（用于 SmoothL1Loss）
        if 'beta' in loss_args and loss_args['beta'] is not None and loss_args['beta'] != '':
             try:
                 parsed_args['beta'] = float(loss_args['beta'])
             except (ValueError, TypeError) as e:
                print(f"Warning: Could not parse 'beta' parameter ({loss_args['beta']}). Ignoring. Error: {e}")


        # 处理 'reduction' 参数（许多损失函数通用）
        valid_reductions = ['mean', 'sum', 'none']
        if 'reduction' in loss_args and loss_args['reduction'] in valid_reductions:
            parsed_args['reduction'] = loss_args['reduction']
        elif 'reduction' in loss_args and loss_args['reduction'] is not None and loss_args['reduction'] != '':
             print(f"Warning: Invalid 'reduction' value '{loss_args['reduction']}'. Using loss function's default reduction.")


        # --- 实例化损失函数 ---
        try:
            loss_fn_class = getattr(nn, loss_type_str)
        except AttributeError:
             # 如果可用则使用 logger（需要将 logger 传递给配置或使其可访问）
             # self.logger.log_entry("Error", f"Unsupported loss function type: {loss_type_str}")
             print(f"Error: Unsupported loss function type specified in config: {loss_type_str}")
             raise ValueError(f"Unsupported loss function type: {loss_type_str}")


        try:
            # 确保只传递特定损失函数的有效参数
            # PyTorch 的损失函数通常会忽略额外的 kwargs，但过滤掉更清晰
            # 然而，过滤需要知道每个损失函数的确切签名，
            # 这很复杂。现在我们依赖 PyTorch 的处理方式。
            print(f"Initializing loss function: {loss_type_str} with args: {parsed_args}") # Debug output
            criterion = loss_fn_class(**parsed_args)
            # Use logger if available
            # self.logger.log_entry("Info", f"Using loss function {loss_type_str} with arguments: {parsed_args}")
            return criterion
        except Exception as e:
            # Use logger if available
            # self.logger.log_entry("Error", f"Error initializing {loss_type_str} with arguments {parsed_args}: {e}")
            print(f"Error initializing {loss_type_str} with arguments {parsed_args}: {e}")
            raise e # Re-raise the exception

    # ... (get_input_size method remains the same) ... [source: 8]

    def get_input_size(self) -> Tuple[int, int]:
        return self.model_input_sizes.get(self.model_name, (224, 224))

class CustomModelConfig(BaseModelConfig):
    """自定义模型的配置类"""

    def __init__(self, model_class: type, model_params: Dict[str, Any],
                 input_size: Tuple[int, int], custom_transforms: Optional[transforms.Compose] = None,
                 config: Dict[str, Any] = None):  # 添加config参数
        self.model_class = model_class
        self.model_params = model_params
        self._input_size = input_size
        self.custom_transforms = custom_transforms
        self.config = config # Store config

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
        # return nn.CrossEntropyLoss()
        return TorchvisionModelConfig._create_loss_from_config(self) # Example call if method is on TorchvisionModelConfig

    def get_input_size(self) -> Tuple[int, int]:
        return self._input_size

    def get_input_size(self) -> Tuple[int, int]:
        return self._input_size # [source: 11]

class EarlyStopping:
    """Early stops the training if validation loss doesn't improve after a given patience."""
    def __init__(self, save_path, patience=5, verbose=True, delta=0):
        """
        Args:
            save_path (str): 模型保存路径.
            patient（int）：上次验证损失改进后等待的时间。默认值：5
            verbose（bool）：如果为 True，则每次验证损失改进后打印一条消息。默认值：True
            delta（float）：监控量中符合改进条件的最小变化量。默认值：0
        """
        self.save_path = save_path
        self.patience = patience
        self.verbose = verbose
        self.counter = 0
        self.best_score = None
        self.early_stop = False
        self.val_loss_min = np.inf
        self.delta = delta
        self.logger = None # 添加一个logger属性，方便集成日志

    def set_logger(self, logger_instance):
        self.logger = logger_instance

    def __call__(self, val_loss, model):
        score = -val_loss

        if self.best_score is None:
            self.best_score = score
            self.save_checkpoint(val_loss, model)
        elif score < self.best_score + self.delta:
            self.counter += 1
            log_message = f'EarlyStopping counter: {self.counter} out of {self.patience}'
            if self.logger:
                self.logger.log_entry("Info", log_message)
            print(log_message)
            if self.counter >= self.patience:
                self.early_stop = True
        else:
            self.best_score = score
            self.save_checkpoint(val_loss, model)
            self.counter = 0

    def save_checkpoint(self, val_loss, model):
        '''Saves model when validation loss decrease.'''
        if self.verbose:
            log_message = f'Validation loss decreased ({self.val_loss_min:.6f} --> {val_loss:.6f}). Saving model ...'
            if self.logger:
                self.logger.log_entry("Status", log_message)
            print(log_message)
        torch.save(model.state_dict(), self.save_path)
        self.val_loss_min = val_loss

class ModelTrainer:
    """模型训练器类"""

    def __init__(self, config: Dict[str, Any], model_config: BaseModelConfig, logger: Any):
        self.symbolModelPath = None
        self.config = config
        self.model_config = model_config
        self.logger = logger
        self.device = torch.device("cuda" if torch.cuda.is_available() else "cpu")

        # 记录模型名称
        self.logger.log_entry("ModelName", f"模型权重名称/地址: {self.config.get('pretrained_model')}")
        # 记录训练硬件
        self.logger.log_entry("TrainingDevice", f"用于训练的硬件: {self.device}")

        # 初始化模型和相关组件
        self.model = self.model_config.get_model().to(self.device)
        self.criterion = self.model_config.get_loss_function()
        self.optimizer = self._get_optimizer()
        self.scheduler = self._get_scheduler()

        # 初始化 EarlyStopping
        # 定义模型保存路径
        model_save_path = os.path.join(self.config['model_output_path'], f"{self.config['pretrained_model']}.pth")
        self.early_stopping = EarlyStopping(
            save_path=model_save_path,
            patience=self.config.get('early_stopping_rounds', 5), # 从配置中获取 patience
            verbose=True,
            delta=self.config.get('early_stopping_delta', 0.0) # 从配置中获取 delta
        )
        self.early_stopping.set_logger(self.logger) # 将logger传递给EarlyStopping实例


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

def load_config():
    parser = argparse.ArgumentParser()
    parser.add_argument('--config', type=str, required=True, help='Path to config JSON file')
    args = parser.parse_args()

    if not os.path.exists(args.config):
        raise FileNotFoundError(f"配置文件不存在: {args.config}")

    with open(args.config, 'r', encoding='utf-8') as f:
        return json.load(f)
class TrainingLogger:
    def __init__(self, base_log_path):

        self.log_path = base_log_path
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
        max_retries = 5
        retry_delay = 0.1  # 100ms

        for attempt in range(max_retries):
            try:
                # 使用临时文件写入，然后原子性替换
                temp_path = self.log_path + '.tmp'
                with open(temp_path, 'w', encoding='utf-8') as f:
                    json.dump(self.log_data, f, ensure_ascii=False, indent=2)

                # 原子性地替换文件
                import shutil
                shutil.move(temp_path, self.log_path)
                break  # 成功写入，退出循环

            except PermissionError as e:
                if attempt < max_retries - 1:
                    # 等待一段时间后重试
                    import time
                    time.sleep(retry_delay * (attempt + 1))  # 递增等待时间
                else:
                    # 最后一次尝试失败，记录警告但不中断训练
                    print(f"Warning: Could not write to log file after {max_retries} attempts: {e}")
                    print("Training will continue, but log updates may be delayed.")

            except Exception as e:
                print(f"Error saving log file: {e}")
                break  # 非权限错误，直接退出


# main.py
def main():
    try:
        # 加载配置
        config = load_config()
        logger = TrainingLogger(config['py_train_log_output_path'])
        logger.log_data["config"] = config

        if not os.path.isdir(config['train_data_path']):
             raise FileNotFoundError(f"Training data path not found or not a directory: {config['train_data_path']}")
        num_classes = len([d for d in os.listdir(config['train_data_path']) if os.path.isdir(os.path.join(config['train_data_path'], d))])
        if num_classes == 0:
            raise ValueError(f"No subdirectories found in {config['train_data_path']}, cannot determine number of classes.")
        logger.log_entry("Info", f"Determined number of classes: {num_classes}")

        # 创建模型配置
        model_config = TorchvisionModelConfig(
            model_name=config['pretrained_model'],
            num_classes=num_classes,
            config = config,
            logger = logger
        )

        # 创建训练器
        trainer = ModelTrainer(config, model_config, logger)

        # 准备数据
        train_loader, val_loader = trainer.prepare_data()

        # 训练循环
        # best_val_acc = 0.0
        # early_stopping_counter = 0

        for epoch in range(config['epochs']):
            # 训练一个epoch
            train_loss, train_acc = trainer.train_epoch(train_loader)

            # 验证
            val_loss, val_acc = trainer.validate(val_loader)

            # 记录指标
            metrics = {
                "train_loss": train_loss,
                "train_accuracy": train_acc,
                "validation_loss": val_loss, # 早停的监控指标，验证损失
                "validation_accuracy": val_acc,
                "learning_rate": trainer.optimizer.param_groups[0]['lr']
            }

            logger.log_entry(
                "Validation",
                f"Epoch {epoch + 1} 完成 - 验证准确率: {val_acc:.2f}%",
                epoch=epoch + 1,
                metrics=metrics
            )

            # 更新学习率 (如果使用 ReduceLROnPlateau，现在应该传入 val_loss)
            if isinstance(trainer.scheduler, torch.optim.lr_scheduler.ReduceLROnPlateau):
                # 如果 ReduceLROnPlateau 的 mode 是 'min' (默认)，则传入 val_loss
                # 如果 mode 是 'max'，则保持传入 val_acc
                # 根据配置的 scheduler mode 来选择传入 val_loss 或 val_acc
                trainer.scheduler.step(val_loss) # 假设mode='min'，如果scheduler是mode='max'则改为val_acc
            elif trainer.scheduler:
                trainer.scheduler.step()

            # 调用 EarlyStopping
            trainer.early_stopping(val_loss, trainer.model) # 传入 val_loss 和模型

            # 检查是否早停
            if trainer.early_stopping.early_stop:
                logger.log_entry("Status", "触发早停机制，停止训练")
                # trainer.save_best_model(val_acc, epoch) # 这一行可以移除，EarlyStopping 会自动保存
                break

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