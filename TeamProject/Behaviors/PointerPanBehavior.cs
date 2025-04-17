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
            AssociatedObject.PointerPressed += OnPressed;
            AssociatedObject.PointerMoved += OnMoved;
            AssociatedObject.PointerReleased += OnReleased;
            AssociatedObject.Cursor = new Cursor(StandardCursorType.SizeAll);
        }
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        if (AssociatedObject != null)
        {
            AssociatedObject.PointerPressed -= OnPressed;
            AssociatedObject.PointerMoved -= OnMoved;
            AssociatedObject.PointerReleased -= OnReleased;
            AssociatedObject.Cursor = Cursor.Default;
        }
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(AssociatedObject).Properties.IsLeftButtonPressed)
        {
            _lastPoint = e.GetPosition(AssociatedObject);
        }
    }

    private void OnMoved(object? sender, PointerEventArgs e)
    {
        if (_lastPoint == null || !e.GetCurrentPoint(AssociatedObject).Properties.IsLeftButtonPressed)
            return;

        var current = e.GetPosition(AssociatedObject);
        var deltaPoint = current - _lastPoint.Value;
        _lastPoint = current;

        var delta = new Vector(deltaPoint.X, deltaPoint.Y);

        if (PanCommand?.CanExecute(delta) == true)
        {
            PanCommand.Execute(delta);
        }
    }

    private void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        _lastPoint = null;
    }
}