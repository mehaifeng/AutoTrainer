using AutoTrainer.Models;
using AutoTrainer.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AutoTrainer;

public partial class DataPickerView : UserControl
{

    private DataPickerViewModel _viewmodel;
    public DataPickerView()
    {
        InitializeComponent();
        _viewmodel = new DataPickerViewModel();
        DataContext = _viewmodel;
    }
}