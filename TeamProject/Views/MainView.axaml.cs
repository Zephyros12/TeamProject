using Avalonia.Controls;
using Avalonia.Input;
using System;
using TeamProject.ViewModels;

namespace TeamProject.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    private void OnImageWheel(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        double delta = e.Delta.Y > 0 ? 0.1 : -0.1;
        vm.ZoomLevel = Math.Clamp(vm.ZoomLevel + delta, 0.1, 5.0);

        e.Handled = true;
    }
}
