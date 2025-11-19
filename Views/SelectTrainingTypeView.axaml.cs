using AutoTrainer.Helpers;
using AutoTrainer.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AutoTrainer;

public partial class SelectTrainingTypeView : Window
{
    public SelectTrainingTypeView()
    {
        InitializeComponent();
    }

    private void ImageClassify_Train_Btn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        this.IsVisible = false;
        MainWindow mainWindow = new MainWindow
        {
            DataContext = App.MainVM
        };
        mainWindow.Show();
    }
    private void Connect_Btn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        this.IsVisible = false;
        ModelStorehouse modelStorehouse = new ModelStorehouse();
        modelStorehouse.Show();
    }
}