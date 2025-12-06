# 模型转换层架构文档

## 架构概览

模型转换层采用模块化设计，与训练和推理层架构保持一致：

```
PyScripts/Conversion/
├── common/                      # 通用模块
│   ├── __init__.py
│   ├── base_converter.py        # 转换基类
│   └── utils.py                 # 工具函数
├── Classification/              # 分类模型转换器
│   ├── __init__.py
│   ├── mobilenet_converter.py
│   ├── efficientnet_converter.py
│   └── resnet_converter.py
├── Detection/                   # 检测模型转换器
│   ├── __init__.py
│   └── fasterrcnn_converter.py
└── convert_model.py             # 统一入口脚本
```

## 设计原则

### 1. 模块化设计
- **通用功能抽象**：`BaseConverter` 基类提供所有转换器的通用功能
- **特定模型专一化**：每个模型有独立的转换器实现
- **代码复用**：通用工具函数统一管理

### 2. 一致性架构
与训练层和推理层保持相同的架构模式：
- `common/` 存放通用模块
- `Classification/` 存放分类模型专用转换器
- `Detection/` 存放检测模型专用转换器

### 3. 扩展性
添加新模型转换器只需：
1. 继承 `BaseConverter` 基类
2. 实现 `load_model()` 方法
3. 在 `convert_model.py` 中注册映射关系

## 核心组件

### BaseConverter 基类

提供所有转换器的通用功能：

```python
class BaseConverter(ABC):
    def __init__(self, config: Dict[str, Any])
    def load_model(self) -> torch.nn.Module  # 抽象方法，子类实现
    def prepare_model_for_export(self, model) -> torch.nn.Module
    def convert_to_onnx(self, model, output_path) -> bool
    def convert_to_torchscript(self, model, output_path) -> bool
    def save_conversion_metadata(self, output_path, format_name)
    def convert(self, target_format) -> bool  # 主转换方法
```

### 分类模型转换器

#### MobileNetConverter
- 支持：`mobilenet_v2`, `mobilenet_v3_large`, `mobilenet_v3_small`
- 输入尺寸：224x224
- 自动检测类别数

#### EfficientNetConverter
- 支持：`efficientnet_b0-b7`, `efficientnet_v2_s/m/l`
- 输入尺寸：根据模型版本自动调整（224-600）
- 自动检测类别数

#### ResNetConverter
- 支持：`resnet18`, `resnet34`, `resnet50`, `resnet101`, `resnet152`
- 输入尺寸：224x224
- 自动检测类别数

### 检测模型转换器

#### FasterRCNNConverter
- 支持：
  - `fasterrcnn_resnet50`
  - `fasterrcnn_mobilenet_v3_large_320`
  - `fasterrcnn_mobilenet_v3_large_fpn`
- 从模型元数据或权重自动检测类别数
- 特殊处理检测模型的ONNX导出

## 使用方式

### 从 .NET 调用

```csharp
// 创建配置文件
var config = new {
    model_path = "path/to/model.pth",
    model_name = "mobilenet_v3_large",
    output_path = "path/to/output",
    input_shape = new[] { 1, 3, 224, 224 },
    opset_version = 15
};

// 保存配置
File.WriteAllText(configPath, JsonSerializer.Serialize(config));

// 调用Python脚本
await CliWrapHelper.ExecutePythonScriptWithStreamingAsync(
    "PyScripts/Conversion/convert_model.py",
    pythonVenvPath,
    $"--config \"{configPath}\" --format onnx"
);
```

### 直接从命令行调用

```bash
python convert_model.py \
    --config config.json \
    --format onnx
```

配置文件格式（config.json）：
```json
{
  "model_path": "models/mobilenet_v3_large.pth",
  "model_name": "mobilenet_v3_large",
  "output_path": "exports/mobilenet",
  "input_shape": [1, 3, 224, 224],
  "opset_version": 15,
  "dynamic_axes": {
    "input": {"batch_size": 0},
    "output": {"batch_size": 0}
  }
}
```

## 支持的转换格式

### ONNX
- **特点**：跨平台、跨框架
- **应用**：TensorRT、OpenVINO、ONNX Runtime
- **配置**：支持自定义 opset 版本和动态轴

### TorchScript
- **特点**：PyTorch 原生格式
- **应用**：生产环境部署、C++ 集成
- **配置**：使用 trace 模式转换

### 未来支持
- TensorRT（NVIDIA GPU 优化）
- OpenVINO（Intel 硬件优化）
- Core ML（Apple 设备优化）

## 转换流程

```
1. 加载配置文件
   ↓
2. 根据 model_name 选择转换器
   ↓
3. 加载模型权重
   ↓
4. 自动检测模型参数（类别数等）
   ↓
5. 准备模型（eval模式、移至设备）
   ↓
6. 执行格式转换
   ↓
7. 验证转换结果
   ↓
8. 保存元数据
```

## 错误处理

- **模型加载失败**：检查模型路径和权重文件
- **类别数检测失败**：检查权重字典中的classifier/fc层
- **ONNX导出失败**：检查opset版本兼容性
- **TorchScript失败**：检查模型中是否有不支持的操作

## 日志输出

转换器使用结构化日志输出：

```
2024-12-06 15:30:00 - INFO - 初始化 MobileNetConverter
2024-12-06 15:30:01 - INFO - 模型路径: models/mobilenet.pth
2024-12-06 15:30:02 - INFO - 加载MobileNet模型: mobilenet_v3_large
2024-12-06 15:30:03 - INFO - 检测到类别数: 10
2024-12-06 15:30:04 - INFO - ✓ MobileNet模型加载成功
2024-12-06 15:30:05 - INFO - 开始转换为ONNX格式...
2024-12-06 15:30:10 - INFO - ✓ ONNX模型已保存到: exports/mobilenet.onnx
2024-12-06 15:30:11 - INFO - ✓ ONNX模型验证通过
2024-12-06 15:30:12 - INFO - 元数据已保存到: exports/mobilenet.json
```

## 元数据保存

每次转换会生成元数据文件（.json）：

```json
{
  "source_model": "models/mobilenet.pth",
  "model_name": "mobilenet_v3_large",
  "target_format": "onnx",
  "input_shape": [1, 3, 224, 224],
  "device": "cuda:0",
  "conversion_config": {
    "opset_version": 15,
    "dynamic_axes": {...}
  }
}
```

## 添加新模型转换器

### 步骤 1：创建转换器类

```python
# PyScripts/Conversion/Classification/mymodel_converter.py

from common import BaseConverter

class MyModelConverter(BaseConverter):
    SUPPORTED_MODELS = ['mymodel_v1', 'mymodel_v2']
    
    def load_model(self) -> nn.Module:
        # 实现模型加载逻辑
        model = create_model(self.model_name)
        state_dict = torch.load(self.model_path)
        model.load_state_dict(state_dict)
        return model
```

### 步骤 2：注册到映射表

在 `convert_model.py` 的 `CONVERTER_MAP` 中添加：

```python
CONVERTER_MAP = {
    'mymodel_v1': 'Classification.mymodel_converter.MyModelConverter',
    'mymodel_v2': 'Classification.mymodel_converter.MyModelConverter',
    # ...
}
```

### 步骤 3：测试

```bash
python convert_model.py \
    --config config.json \
    --format onnx
```

## 与训练/推理层的集成

转换层使用与训练/推理层相同的模型架构和权重格式：

```
训练层  →  生成 .pth 权重文件（包含metadata）
  ↓
转换层  →  读取 .pth 文件，转换为 ONNX/TorchScript
  ↓
推理层  →  使用转换后的模型进行验证
```

## 最佳实践

1. **始终验证转换结果**：转换后进行推理测试
2. **保存元数据**：便于追踪模型来源和配置
3. **使用动态轴**：提高模型灵活性
4. **选择合适的opset版本**：平衡兼容性和功能
5. **检查模型大小**：确保转换后大小合理

## 故障排查

### 问题：找不到classifier层
**解决**：检查模型权重中的键名，可能是 `fc` 或其他名称

### 问题：ONNX导出失败
**解决**：尝试降低opset版本或使用简化的dynamic_axes配置

### 问题：TorchScript trace失败
**解决**：模型可能包含动态控制流，考虑使用script模式

## 性能优化

- 使用 FP16 量化减小模型大小
- 启用常量折叠优化
- 移除未使用的节点
- 合并连续的操作

## 版本兼容性

- **PyTorch**: >= 2.0
- **ONNX**: opset 11-17
- **TorchVision**: >= 0.15
- **Python**: >= 3.8
