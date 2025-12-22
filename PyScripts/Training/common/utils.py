"""
工具函数模块
提供早停、GPU监控、模型保存等通用功能
"""
import torch
import os
import psutil
from typing import Optional, Dict
try:
    import pynvml
    NVML_AVAILABLE = True
except ImportError:
    NVML_AVAILABLE = False


class EarlyStopping:
    """早停机制"""
    
    def __init__(self, patience: int = 10, delta: float = 0.001, verbose: bool = True):
        """
        初始化早停
        
        Args:
            patience: 等待改善的轮数
            delta: 最小改善量
            verbose: 是否输出详细信息
        """
        self.patience = patience
        self.delta = delta
        self.verbose = verbose
        self.counter = 0
        self.best_score = None
        self.early_stop = False
        self.best_epoch = 0
        
    def __call__(self, val_loss: float, epoch: int) -> bool:
        """
        检查是否应该早停
        
        Args:
            val_loss: 验证损失
            epoch: 当前epoch
            
        Returns:
            是否应该早停
        """
        score = -val_loss
        
        if self.best_score is None:
            self.best_score = score
            self.best_epoch = epoch
        elif score < self.best_score + self.delta:
            self.counter += 1
            if self.verbose:
                print(f'EarlyStopping counter: {self.counter} out of {self.patience}')
            if self.counter >= self.patience:
                self.early_stop = True
        else:
            # 验证损失改善
            improvement = -score - (-self.best_score)  # 新损失 - 旧损失（负值表示改善）
            if self.counter > 0 and self.verbose:
                print(f'✓ 验证损失改善 {abs(improvement):.6f}，早停计数重置: {self.counter} → 0')
            self.best_score = score
            self.best_epoch = epoch
            self.counter = 0
            
        return self.early_stop


class GPUMonitor:
    """GPU使用率监控"""
    
    def __init__(self):
        """初始化GPU监控"""
        self.available = False
        if NVML_AVAILABLE:
            try:
                pynvml.nvmlInit()
                self.available = True
            except:
                pass
                
    def get_gpu_usage(self, device_id: int = 0) -> Optional[float]:
        """
        获取GPU使用率
        
        Args:
            device_id: GPU设备ID
            
        Returns:
            GPU使用率百分比，如果无法获取则返回None
        """
        if not self.available:
            return None
            
        try:
            handle = pynvml.nvmlDeviceGetHandleByIndex(device_id)
            utilization = pynvml.nvmlDeviceGetUtilizationRates(handle)
            return float(utilization.gpu)
        except:
            return None
            
    def get_memory_usage(self, device_id: int = 0) -> Optional[float]:
        """
        获取GPU显存使用量（GB）
        
        Args:
            device_id: GPU设备ID
            
        Returns:
            显存使用量（GB），如果无法获取则返回None
        """
        if not self.available:
            return None
            
        try:
            handle = pynvml.nvmlDeviceGetHandleByIndex(device_id)
            mem_info = pynvml.nvmlDeviceGetMemoryInfo(handle)
            return mem_info.used / (1024 ** 3)  # 转换为GB
        except:
            return None
            
    def __del__(self):
        """清理资源"""
        if self.available:
            try:
                pynvml.nvmlShutdown()
            except:
                pass


class SystemMonitor:
    """系统资源监控"""
    
    @staticmethod
    def get_cpu_usage() -> float:
        """获取CPU使用率"""
        return psutil.cpu_percent(interval=1)
        
    @staticmethod
    def get_memory_usage() -> float:
        """获取内存使用量（GB）"""
        mem = psutil.virtual_memory()
        return mem.used / (1024 ** 3)
        
    @staticmethod
    def get_memory_percent() -> float:
        """获取内存使用率百分比"""
        return psutil.virtual_memory().percent


class ModelCheckpoint:
    """模型检查点管理器"""
    
    def __init__(self, save_dir: str, save_name: str, architecture_name: str, num_classes: int):
        """
        初始化检查点管理器
        
        Args:
            save_dir: 保存目录
            save_name: 用于文件名的模型名称（可自定义）
            architecture_name: 模型架构名称（用于metadata）
            num_classes: 类别数（包含背景类）
        """
        self.save_dir = save_dir
        self.save_name = save_name
        self.architecture_name = architecture_name
        self.num_classes = num_classes
        self.best_epoch = 0
        self.best_metrics = {}
        
        # 确保保存目录存在
        os.makedirs(save_dir, exist_ok=True)
        
    def save(self, model: torch.nn.Module, epoch: int, metrics: Dict[str, float], 
             is_best: bool = False) -> str:
        """
        保存模型检查点
        
        Args:
            model: 模型
            epoch: 当前epoch
            metrics: 指标字典
            is_best: 是否是最佳模型
            
        Returns:
            保存路径
        """
        checkpoint = {
            'epoch': epoch,
            'model_state_dict': model.state_dict(),
            'metrics': metrics,
            'model_name': self.architecture_name,  # 架构名称
            'num_classes': self.num_classes  # 类别数
        }
        
        if is_best:
            save_path = os.path.join(self.save_dir, f'{self.save_name}_best.pth')
            self.best_epoch = epoch
            self.best_metrics = metrics.copy()
        else:
            save_path = os.path.join(self.save_dir, f'{self.save_name}_epoch{epoch}.pth')
            
        torch.save(checkpoint, save_path)
        return save_path
        
    def save_final(self, model: torch.nn.Module) -> str:
        """
        保存最终模型（包含完整元数据）
        
        Args:
            model: 模型
            
        Returns:
            保存路径
        """
        checkpoint = {
            'epoch': self.best_epoch,
            'model_state_dict': model.state_dict(),
            'metrics': self.best_metrics,
            'model_name': self.architecture_name,  # 架构名称
            'num_classes': self.num_classes  # 类别数
        }
        
        save_path = os.path.join(self.save_dir, f'{self.save_name}.pth')
        torch.save(checkpoint, save_path)
        return save_path


def get_device() -> torch.device:
    """获取训练设备，支持CUDA, MPS (Apple Silicon), 和CPU"""
    if torch.cuda.is_available():
        return torch.device('cuda')
    elif hasattr(torch.backends, 'mps') and torch.backends.mps.is_available():
        return torch.device('mps')
    else:
        return torch.device('cpu')


def set_seed(seed: int = 42):
    """设置随机种子以保证可复现性"""
    torch.manual_seed(seed)
    if torch.cuda.is_available():
        torch.cuda.manual_seed_all(seed)
        torch.backends.cudnn.deterministic = True
        torch.backends.cudnn.benchmark = False
    elif hasattr(torch.backends, 'mps') and torch.backends.mps.is_available():
        # MPS后端当前不支持deterministic模式，但可以设置随机种子
        pass


def should_pin_memory() -> bool:
    """判断是否应该使用pin_memory，MPS设备不支持"""
    return torch.cuda.is_available()
