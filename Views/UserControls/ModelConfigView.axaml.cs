using AutoTrainer.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AutoTrainer;

public partial class ModelConfigView : UserControl
{
    public ModelConfigView()
    {
        InitializeComponent();
        DataContext = new ModelConfigViewModel();
    }
}