using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;
using System;
using System.Windows.Input;

namespace TeamProject.Behaviors;

public class PointerPanBehavior : Behavior<InputElement>
{
    public static readonly StyledProperty<ICommand> PanCommandProperty =
        AvaloniaProperty.Register<PointerPanBehavior, ICommand>(nameof(PanCommand));

    public ICommand? PanCommand
    {
        get => GetValue(PanCommandProperty);
        set => SetValue(PanCommandProperty, value);
    }

    private Point? _lastPoint;

    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject != null)
        {
            AssociatedObject.PointerPressed += OnPointerPressed;
            AssociatedObject.PointerMoved += OnPointerMoved;
            AssociatedObject.PointerReleased += OnPointerReleased;
            AssociatedObject.Cursor = new Cursor(StandardCursorType.SizeAll);
        }
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        if (AssociatedObject != null)
        {
            AssociatedObject.PointerPressed -= OnPointerPressed;
            AssociatedObject.PointerMoved -= OnPointerMoved;
            AssociatedObject.PointerReleased -= OnPointerReleased;
            AssociatedObject.Cursor = Cursor.Default;
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(AssociatedObject).Properties.IsLeftButtonPressed)
        {
            _lastPoint = e.GetPosition(AssociatedObject);
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_lastPoint is not { } last || !e.GetCurrentPoint(AssociatedObject).Properties.IsLeftButtonPressed)
            return;

        var current = e.GetPosition(AssociatedObject);
        var delta = new Vector(current.X - last.X, current.Y - last.Y);
        _lastPoint = current;

        if (PanCommand?.CanExecute(delta) == true)
        {
            PanCommand.Execute(delta);
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _lastPoint = null;
    }
}
