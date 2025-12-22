# AutoTrainer 主题系统使用指南

## 概述

AutoTrainer 现已配备完整的浅色/深色主题系统，基于 Material Design 3 设计规范，提供专业、现代的视觉体验。

## 主题配色方案

### 浅色主题 (Light Theme)
- **主色调**: 科技蓝 (#2196F3) - 专业、可靠
- **强调色**: 活力橙 (#FF9800) - 突出重点操作
- **背景**: 浅灰 (#FAFAFA) - 舒适护眼
- **文字**: 深灰 (#212121) - 清晰易读

### 深色主题 (Dark Theme)
- **主色调**: 明亮蓝 (#42A5F5) - 柔和不刺眼
- **强调色**: 柔和橙 (#FFB74D) - 温暖提示
- **背景**: 深黑 (#121212) - 舒适夜间使用
- **文字**: 纯白 (#FFFFFF) - 高对比度

## 使用方法

### 1. 程序级主题切换

```csharp
using AutoTrainer.Helpers;

// 切换到浅色主题
ThemeManager.SwitchToLight();

// 切换到深色主题
ThemeManager.SwitchToDark();

// 跟随系统主题
ThemeManager.FollowSystem();

// 在浅色/深色之间切换
ThemeManager.Toggle();
```

### 2. XAML 中使用主题颜色

```xml
<!-- 按钮样式 -->
<Button Classes="primary" Content="主要按钮"/>
<Button Classes="accent" Content="强调按钮"/>
<Button Classes="outlined" Content="轮廓按钮"/>

<!-- 卡片样式 -->
<Border Classes="card">
    <TextBlock Text="内容"/>
</Border>

<!-- 文本样式 -->
<TextBlock Text="主要文本"/>
<TextBlock Classes="secondary" Text="次要文本"/>
```

### 3. 添加主题切换按钮

```xml
<Button Content="🌙" Click="ToggleTheme_Click"/>
```

```csharp
private void ToggleTheme_Click(object sender, RoutedEventArgs e)
{
    ThemeManager.Toggle();
}
```

## 颜色对照表

| 功能 | 浅色主题 | 深色主题 |
|------|---------|---------|
| 主色调 | #2196F3 | #42A5F5 |
| 强调色 | #FF9800 | #FFB74D |
| 背景 | #FAFAFA | #121212 |
| 卡片 | #FFFFFF | #2C2C2C |
| 主文字 | #212121 | #FFFFFF |
| 成功 | #4CAF50 | #66BB6A |
| 警告 | #FF9800 | #FFA726 |
| 错误 | #F44336 | #EF5350 |

详细使用方法请参考代码中的注释和示例。
