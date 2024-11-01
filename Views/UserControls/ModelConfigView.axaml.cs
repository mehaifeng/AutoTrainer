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

    private void Outout_tb_TextChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e)
    {
        Outout_tb.CaretIndex = int.MaxValue;
    }
}