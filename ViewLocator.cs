using AutoTrainer.ViewModels;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using System;

namespace AutoTrainer
{
    public class ViewLocator : IDataTemplate
    {

        public Control? Build(object? data)
        {
            if (data is null)
                return null;

            var name = data.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
            var type = Type.GetType(name);

            if (type != null)
            {
                try
                {
                    var control = (Control)Activator.CreateInstance(type)!;
                    control.DataContext = data;
                    return control;
                }
                catch (Exception ex)
                {
                    // 如果创建控件失败，返回错误信息
                    return new TextBlock { Text = "Failed to create view: " + name + "\nError: " + ex.Message };
                }
            }

            return new TextBlock { Text = "Not Found: " + name };
        }

        public bool Match(object? data)
        {
            // 排除已经有DataTemplate的模型类型
            if (data?.GetType().Name.Contains("PreviewImageModel") == true)
                return false;

            return data is ViewModelBase;
        }
    }
}
