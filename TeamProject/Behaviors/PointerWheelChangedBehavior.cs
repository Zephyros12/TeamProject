using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;
using System;

namespace TeamProject.Behaviors;

public class PointerWheelChangedBehavior : Behavior<Control>
{
    public static readonly StyledProperty<object?> ZoomHandlerProperty =
        AvaloniaProperty.Register<PointerWheelChangedBehavior, object?>(nameof(ZoomHandler));

    public object? ZoomHandler
    {
        get => GetValue(ZoomHandlerProperty);
        set => SetValue(ZoomHandlerProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject != null)
        {
            AssociatedObject.PointerWheelChanged += OnPointerWheelChanged;
        }
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        if (AssociatedObject != null)
        {
            AssociatedObject.PointerWheelChanged -= OnPointerWheelChanged;
        }
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if ((e.KeyModifiers & KeyModifiers.Control) == 0 || ZoomHandler == null)
            return;

        if (ZoomHandler is not ViewModels.MainViewModel vm)
            return;

        var position = e.GetPosition(AssociatedObject);
        vm.ZoomWithMouse(position.X, position.Y, e.Delta.Y);
        e.Handled = true;
    }
}
