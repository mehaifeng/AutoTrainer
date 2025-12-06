# 模型名称自动读取优化

## 修改时间
2024-12-06

## 问题背景

**原实现问题**:
- 模型名称由用户手动输入或从文件名推断
- 用户可能输入错误的架构名称
- 无法确保是本软件训练的模型

**本软件的标准**:
- 训练时会在模型的 `metadata` 中保存标准架构名称（`model_name`）
- 如：`mobilenet_v3_large`, `efficientnet_b0`, `fasterrcnn_resnet50`
- 如果模型没有 `metadata['model_name']`，则不是本软件训练的模型

## 解决方案

### 核心逻辑

从模型 checkpoint 的 metadata 中自动读取标准架构名称：

```csharp
// 用户选择模型文件后
ModelPath = files[0].TryGetLocalPath();

// 自动读取 metadata
await LoadModelNameFromMetadata();

// 使用 CliWrapHelper 调用 Python 脚本
var modelName = await CliWrapHelper.GetModelNameFromMetadataAsync(
    ModelPath, 
    App.PythonVenvPath
);

if (!string.IsNullOrEmpty(modelName))
{
    ModelName = modelName;  // 如：mobilenet_v3_large
    ModelNameStatus = $"✓ 模型: {modelName}";
}
else
{
    ModelNameStatus = "✗ 非本软件训练的模型";
}
```

### Python 脚本

利用现有的 `PyScripts/Utils/ModelMetadataReader.py`：

```python
import torch
import json
import sys

checkpoint = torch.load(model_path, map_location='cpu', weights_only=True)

if 'metadata' in checkpoint and 'model_name' in checkpoint['metadata']:
    model_name = checkpoint['metadata']['model_name']
    print(json.dumps({
        "success": True,
        "model_name": model_name,
        "message": "Successfully read model_name from metadata"
    }))
else:
    print(json.dumps({
        "success": False,
        "model_name": None,
        "message": "No model_name in metadata"
    }))
```

## 实现细节

### 1. ViewModel 修改

**新增属性**:
```csharp
[ObservableProperty]
private string modelName = string.Empty;  // 从 "MyModel" 改为空

[ObservableProperty]
private string modelNameStatus = "未加载模型";  // 状态提示

[ObservableProperty]
private bool isModelNameReadOnly = true;  // 只读标志
```

**新增方法**:
```csharp
private async Task LoadModelNameFromMetadata()
{
    // 1. 检查 Python 环境
    if (string.IsNullOrEmpty(App.PythonVenvPath))
    {
        ModelNameStatus = "错误：未配置 Python 环境";
        return;
    }

    // 2. 读取 metadata
    var modelName = await CliWrapHelper.GetModelNameFromMetadataAsync(
        ModelPath, App.PythonVenvPath
    );

    // 3. 处理结果
    if (!string.IsNullOrEmpty(modelName))
    {
        ModelName = modelName;
        ModelNameStatus = $"✓ 模型: {modelName}";
        UpdateOutputPaths();
    }
    else
    {
        ModelName = string.Empty;
        ModelNameStatus = "✗ 非本软件训练的模型";
        // 显示提示对话框
    }
}
```

**修改验证逻辑**:
```csharp
private async Task<bool> ValidateInputs()
{
    if (string.IsNullOrEmpty(ModelName))
    {
        await MessageBoxManager.GetMessageBoxStandard(
            "错误", 
            "无法获取模型架构名称。\n\n" +
            "该模型可能不是本软件训练的。" +
            "本软件训练的模型会在 metadata 中保存标准架构名称。"
        ).ShowWindowAsync();
        return false;
    }
    // ...
}
```

### 2. UI 修改

**XAML 更新**:
```xml
<!-- 标签说明 -->
<TextBlock Text="模型架构名称（从 metadata 自动读取）" 
           Foreground="#666" FontSize="12"/>

<!-- 只读文本框，灰色背景 -->
<TextBox Text="{Binding ModelName}" 
         Background="#F5F5F5" 
         IsReadOnly="True"
         Watermark="选择模型后自动读取"/>

<!-- 状态提示 -->
<TextBlock Text="{Binding ModelNameStatus}" 
           Foreground="#999" 
           FontSize="11" 
           Margin="0,3,0,0"/>
```

## 工作流程

```
用户点击"浏览"按钮
  ↓
选择 .pth 模型文件
  ↓
ModelPath 赋值
  ↓
自动调用 LoadModelNameFromMetadata()
  ↓
调用 Python 脚本读取 checkpoint['metadata']['model_name']
  ↓
┌─────────────────────────────────────┐
│ 有 model_name                       │  没有 model_name
├─────────────────────────────────────┤
│ ModelName = "mobilenet_v3_large"    │  ModelName = ""
│ Status = "✓ 模型: mobilenet_v3_..."  │  Status = "✗ 非本软件训练的模型"
│ 自动更新输出路径                     │  显示提示对话框
└─────────────────────────────────────┘
  ↓
用户点击"开始转换"
  ↓
ValidateInputs() 检查 ModelName 是否为空
  ↓
为空 → 显示错误，无法转换
不为空 → 继续转换流程
```

## 状态提示说明

| 状态文本 | 含义 | 颜色 |
|---------|------|------|
| `未加载模型` | 初始状态，未选择文件 | 灰色 |
| `正在读取模型信息...` | 正在调用 Python 脚本 | 灰色 |
| `✓ 模型: mobilenet_v3_large` | 成功读取到架构名称 | 灰色 |
| `✗ 非本软件训练的模型` | 无法读取 metadata | 灰色 |
| `✗ 读取失败` | 发生异常 | 灰色 |
| `错误：未配置 Python 环境` | Python 环境未配置 | 灰色 |

## 用户体验

### 成功场景
1. 用户选择本软件训练的模型
2. 自动显示：`✓ 模型: mobilenet_v3_large`
3. 输出路径自动更新为：`exports/mobilenet_v3_large.onnx`
4. 用户直接点击"开始转换"即可

### 失败场景（外部模型）
1. 用户选择非本软件训练的模型
2. 显示：`✗ 非本软件训练的模型`
3. 弹出提示对话框说明原因
4. 点击"开始转换"时再次提示无法转换

### 失败场景（环境问题）
1. 用户未配置 Python 虚拟环境
2. 显示：`错误：未配置 Python 环境`
3. 弹出提示引导用户配置环境

## 优势总结

### 1. 自动化
✅ 无需用户输入或猜测模型名称
✅ 避免人为错误

### 2. 准确性
✅ 直接读取训练时保存的标准架构名称
✅ 确保与转换器映射表匹配

### 3. 安全性
✅ 明确区分本软件训练的模型
✅ 防止转换不兼容的模型文件

### 4. 用户体验
✅ 清晰的状态提示
✅ 友好的错误信息
✅ 自动更新输出路径

## 技术细节

### 依赖项
- `CliWrapHelper.GetModelNameFromMetadataAsync()` - 已存在
- `PyScripts/Utils/ModelMetadataReader.py` - 已存在
- `App.PythonVenvPath` - 全局配置

### 异常处理
- Python 环境未配置 → 提示用户配置
- 模型文件损坏 → 显示错误信息
- 无 metadata → 提示非本软件模型
- 网络/IO 异常 → 捕获并显示

### 性能
- 读取 metadata 是轻量级操作（只读字典键值）
- 不加载完整模型权重
- 通常在 1 秒内完成

## 与其他模块的一致性

这个实现与验证模块保持一致：

**验证模块** (`ValidModelPerformanceViewModel.cs`):
```csharp
// 也是从 metadata 读取 model_name
var modelName = await CliWrapHelper.GetModelNameFromMetadataAsync(...);

// 根据 model_name 选择验证器
if (modelName.StartsWith("mobilenet"))
    return "mobilenet_validator.py";
```

**转换模块** (`ConvertToExportViewModel.cs`):
```csharp
// 同样从 metadata 读取 model_name
var modelName = await CliWrapHelper.GetModelNameFromMetadataAsync(...);

// 根据 model_name 选择转换器（通过 convert_model.py）
config.model_name = modelName;  // mobilenet_v3_large
```

## 测试建议

### 单元测试
- [ ] 测试有 metadata 的模型
- [ ] 测试无 metadata 的模型
- [ ] 测试损坏的模型文件
- [ ] 测试 Python 环境未配置

### 集成测试
- [ ] 端到端：选择模型 → 读取名称 → 转换成功
- [ ] 错误处理：外部模型 → 显示提示 → 无法转换

### UI 测试
- [ ] 状态提示正确显示
- [ ] 模型名称只读
- [ ] 输出路径自动更新

## 未来扩展

### 显示更多信息
可以扩展显示更多 metadata 信息：
- 类别数
- 训练时间
- 准确率
- 输入尺寸建议

### 批量转换
支持选择多个模型批量转换：
- 逐个读取 metadata
- 显示模型列表
- 批量执行转换

## 总结

✅ **问题解决** - 从用户输入改为自动读取
✅ **准确性提升** - 直接使用训练时保存的标准名称
✅ **用户体验** - 自动化、清晰提示、友好错误
✅ **架构一致** - 与验证模块保持相同逻辑
✅ **安全性** - 明确区分本软件训练的模型

这个改进确保了模型转换功能只处理本软件训练的标准模型，提高了转换的成功率和用户体验。
