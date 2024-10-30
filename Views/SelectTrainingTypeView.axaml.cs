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
        MainWindow mainWindow = new MainWindow();
        mainWindow.Show();
    }
    private void ValuePredic_Train_Btn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        this.IsVisible = false;
        MainWindow mainWindow = new MainWindow();
        mainWindow.Show();
    }
}