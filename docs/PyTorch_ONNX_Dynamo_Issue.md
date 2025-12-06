# PyTorch ONNX 导出 - dynamo 参数说明

## 问题

PyTorch 2.0+ 引入了新的 ONNX 导出器（基于 dynamo），默认启用。使用旧的 `dynamic_axes` 参数会产生警告和错误：

```
UserWarning: 'dynamic_axes' is not recommended when dynamo=True
torch._dynamo.exc.UserError: Constraints violated
```

## 解决方案

在 `torch.onnx.export()` 中添加 `dynamo=False` 参数，强制使用传统导出器：

```python
torch.onnx.export(
    model,
    dummy_input,
    output_path,
    # ... 其他参数
    dynamic_axes=dynamic_axes,
    dynamo=False  # ← 关键：禁用新的 dynamo 导出器
)
```

## 两种导出器对比

### 传统导出器（dynamo=False）

**优点**:
- ✅ 稳定，经过充分测试
- ✅ 支持 `dynamic_axes` 参数
- ✅ 兼容性好，适用于大多数模型
- ✅ 文档完善，社区支持好

**缺点**:
- ⚠️ 基于 TorchScript，可能在某些模型上有限制

### 新 Dynamo 导出器（dynamo=True，默认）

**优点**:
- ✅ 基于 torch.export，更现代
- ✅ 更好的性能和优化
- ✅ 未来发展方向

**缺点**:
- ⚠️ 需要使用 `dynamic_shapes` 而非 `dynamic_axes`
- ⚠️ 某些模型可能不兼容
- ⚠️ 文档和示例较少

## 我们的选择

**使用传统导出器（dynamo=False）** ✅

**原因**:
1. 更稳定，兼容性好
2. 支持我们现有的所有模型架构
3. `dynamic_axes` 参数简单易用
4. 社区支持和文档完善

## 代码示例

### 修改前（会报错）
```python
torch.onnx.export(
    model, dummy_input, output_path,
    dynamic_axes={
        'input': {0: 'batch_size'},
        'output': {0: 'batch_size'}
    }
    # 默认 dynamo=True，导致错误
)
```

### 修改后（正常工作）
```python
torch.onnx.export(
    model, dummy_input, output_path,
    dynamic_axes={
        'input': {0: 'batch_size'},
        'output': {0: 'batch_size'}
    },
    dynamo=False  # 明确使用传统导出器
)
```

## 如果要使用 Dynamo 导出器

如果未来需要使用新的 dynamo 导出器，需要修改为：

```python
# 使用 dynamic_shapes 而非 dynamic_axes
import torch

# 定义动态形状
dynamic_shapes = {
    'input': {0: torch.export.Dim("batch_size")},
}

torch.onnx.export(
    model, dummy_input, output_path,
    dynamic_shapes=dynamic_shapes,  # 新参数
    dynamo=True  # 使用新导出器
)
```

但这需要更多的代码修改和测试。

## 相关链接

- [PyTorch ONNX 官方文档](https://pytorch.org/docs/stable/onnx.html)
- [torch.onnx.export API](https://pytorch.org/docs/stable/onnx.html#torch.onnx.export)
- [TorchDynamo 文档](https://pytorch.org/docs/stable/dynamo/index.html)

## 总结

✅ **当前解决方案**: 添加 `dynamo=False` 参数
✅ **优点**: 稳定、兼容、简单
✅ **适用**: 所有分类和检测模型
✅ **性能**: 满足需求，导出速度快

不需要修改其他代码，只需在 `base_converter.py` 中添加一个参数即可解决问题。
