using AutoTrainer.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AutoTrainer;

public partial class SetupView : UserControl
{
    public SetupView()
    {
        InitializeComponent();
    }

    private void Outout_tb_TextChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e)
    {
        Outout_tb.CaretIndex = int.MaxValue;
    }
}