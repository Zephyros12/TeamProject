using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;
using System;
using System.Windows.Input;

namespace TeamProject.Behaviors;

public class PointerWheelChangedBehavior : Behavior<Control>
{
    public static readonly StyledProperty<ICommand> ZoomInCommandProperty =
        AvaloniaProperty.Register<PointerWheelChangedBehavior, ICommand>(nameof(ZoomInCommand));

    public static readonly StyledProperty<ICommand> ZoomOutCommandProperty =
        AvaloniaProperty.Register<PointerWheelChangedBehavior, ICommand>(nameof(ZoomOutCommand));

    public ICommand? ZoomInCommand
    {
        get => GetValue(ZoomInCommandProperty);
        set => SetValue(ZoomInCommandProperty, value);
    }

    public ICommand? ZoomOutCommand
    {
        get => GetValue(ZoomOutCommandProperty);
        set => SetValue(ZoomOutCommandProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject is not null)
            AssociatedObject.PointerWheelChanged += OnPointerWheelChanged;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        if (AssociatedObject is not null)
            AssociatedObject.PointerWheelChanged -= OnPointerWheelChanged;
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if ((e.KeyModifiers & KeyModifiers.Control) != 0)
        {
            if (e.Delta.Y > 0 && ZoomInCommand?.CanExecute(e) == true)
            {
                ZoomInCommand.Execute(e);
                e.Handled = true;
            }
            else if (e.Delta.Y < 0 && ZoomOutCommand?.CanExecute(e) == true)
            {
                ZoomOutCommand.Execute(e);
                e.Handled = true;
            }
        }
    }
}