# 模型转换功能所需的 Python 包

## 快速安装

在您的 Python 虚拟环境中运行：

```bash
pip install onnx onnxruntime onnxscript onnxsim
```

## 详细说明

### 必需包（模型转换）

| 包名 | 版本 | 说明 |
|------|------|------|
| `onnx` | 1.19.1 | ONNX 模型格式支持 |
| `onnxruntime` | 最新 | ONNX 模型推理引擎（用于验证） |
| `onnxscript` | 最新 | **PyTorch 2.0+ 导出 ONNX 的必需依赖** |
| `onnxsim` | 最新 | ONNX 模型简化和优化工具 |

### 为什么需要 onnxscript？

从 PyTorch 2.0 开始，`torch.onnx.export()` 内部依赖 `onnxscript` 包来生成 ONNX 模型。如果没有安装此包，会报错：

```
ModuleNotFoundError: No module named 'onnxscript'
```

### 安装步骤

#### 方法 1: 逐个安装
```bash
# 进入虚拟环境
# Windows:
venv\Scripts\activate

# Linux/Mac:
source venv/bin/activate

# 安装 ONNX 相关包
pip install onnx==1.19.1
pip install onnxruntime
pip install onnxscript
pip install onnxsim
```

#### 方法 2: 批量安装
```bash
pip install onnx==1.19.1 onnxruntime onnxscript onnxsim
```

#### 方法 3: 使用 Requirements.json
软件会自动读取 `Configs/Requirements.json` 并提示安装。

### 验证安装

```python
# 测试导入
import onnx
import onnxruntime
import onnxscript
from onnxsim import simplify

print("所有 ONNX 包已成功安装！")
```

### 常见问题

#### Q: 安装 onnxscript 失败？
A: 确保 Python 版本 >= 3.8，PyTorch >= 2.0

```bash
python --version
python -c "import torch; print(torch.__version__)"
```

#### Q: onnxruntime 安装慢？
A: 使用国内镜像源

```bash
pip install onnxruntime -i https://pypi.tuna.tsinghua.edu.cn/simple
```

#### Q: 需要 GPU 支持的 onnxruntime？
A: 安装 GPU 版本

```bash
pip install onnxruntime-gpu
```

### 可选包（高级功能）

| 包名 | 用途 | 安装命令 |
|------|------|----------|
| `tensorrt` | NVIDIA GPU 加速 | 需要先安装 CUDA |
| `openvino` | Intel 硬件加速 | `pip install openvino` |
| `coremltools` | Apple Core ML | `pip install coremltools` |

### 包版本兼容性

- **PyTorch**: >= 2.0 (建议 2.1+)
- **ONNX**: 1.15-1.19
- **onnxscript**: 最新版本即可
- **onnxruntime**: 与 ONNX 版本兼容

### 完整依赖列表

参考 `Configs/Requirements.json`:

```json
{
  "packages": [
    "torch",
    "torchvision", 
    "onnx==1.19.1",
    "onnxruntime",
    "onnxscript",
    "onnxsim",
    ...
  ]
}
```

### 故障排查

#### 1. ModuleNotFoundError: No module named 'onnxscript'
**解决**: 
```bash
pip install onnxscript
```

#### 2. ONNX 导出失败
**检查**:
```python
import torch
import onnxscript

print(f"PyTorch: {torch.__version__}")
print(f"ONNX Script: {onnxscript.__version__}")
```

#### 3. 权限错误
**解决**: 使用 `--user` 参数
```bash
pip install --user onnxscript
```

### 更新包

定期更新以获取最新功能和bug修复：

```bash
pip install --upgrade onnx onnxruntime onnxscript onnxsim
```

### 卸载重装

如果遇到问题，可以尝试卸载后重新安装：

```bash
pip uninstall onnx onnxruntime onnxscript onnxsim -y
pip install onnx==1.19.1 onnxruntime onnxscript onnxsim
```

## 总结

✅ **最小安装**（仅 ONNX 转换）:
```bash
pip install onnx onnxruntime onnxscript onnxsim
```

✅ **完整安装**（包含训练和推理）:
```bash
pip install torch torchvision onnx onnxruntime onnxscript onnxsim opencv-python pillow matplotlib scikit-learn albumentations tqdm psutil
```

记住：**onnxscript 是 PyTorch 2.0+ 导出 ONNX 的必需依赖！**
