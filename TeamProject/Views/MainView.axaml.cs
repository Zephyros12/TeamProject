using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using System;
using TeamProject.ViewModels;

namespace TeamProject.Views;

public partial class MainView : UserControl
{
    private Point? _lastPanPoint;

    public MainView()
    {
        InitializeComponent();
    }

    private void OnImageWheel(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        var modifiers = e.KeyModifiers;

        if ((modifiers & KeyModifiers.Control) != 0)
        {
            if (ZoombleImage == null)
                return;

            // ��Ȯ�� �̹��� �������� ���콺 ��ġ ���
            var mousePos = e.GetPosition(ZoombleImage);

            // ���� ��
            double oldZoom = vm.ZoomLevel;
            double newZoom = oldZoom;

            // Ȯ��/��� ���
            if (e.Delta.Y > 0)
                newZoom = Math.Clamp(oldZoom + 0.05, 0.05, 10.0);
            else if (e.Delta.Y < 0)
                newZoom = Math.Clamp(oldZoom - 0.05, 0.05, 10.0);

            if (Math.Abs(newZoom - oldZoom) > 0.0001)
            {
                double zoomRatio = newZoom / oldZoom;

                // ������ ���� (���콺 ���� �� �ٽ�)
                vm.OffsetX = (vm.OffsetX - mousePos.X) * zoomRatio + mousePos.X;
                vm.OffsetY = (vm.OffsetY - mousePos.Y) * zoomRatio + mousePos.Y;

                vm.ZoomLevel = newZoom;
            }

            e.Handled = true;
        }
        else
        {
            e.Handled = false; // �⺻ ��ũ�� ����
        }
    }

    private void OnImagePressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _lastPanPoint = e.GetPosition(this);
        }
    }

    private void OnImageMoved(object? sender, PointerEventArgs e)
    {
        if (_lastPanPoint is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        var current = e.GetPosition(this);
        var delta = current - _lastPanPoint.Value;
        _lastPanPoint = current;

        if (DataContext is MainViewModel vm)
        {
            vm.OffsetX += delta.X;
            vm.OffsetY += delta.Y;
        }
    }

    private void OnImageReleased(object? sender, PointerReleasedEventArgs e)
    {
        _lastPanPoint = null;
    }
}
