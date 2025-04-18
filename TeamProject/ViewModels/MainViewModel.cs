using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using TeamProject.Models;

namespace TeamProject.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private Bitmap? _image;
    public Bitmap? Image
    {
        get => _image;
        set => SetProperty(ref _image, value);
    }

    private Bitmap? _previewImage;
    public Bitmap? PreviewImage
    {
        get => _previewImage;
        set => SetProperty(ref _previewImage, value);
    }

    public ObservableCollection<Defect> Defects { get; } = new();

    private Defect? _selectedDefect;
    public Defect? SelectedDefect
    {
        get => _selectedDefect;
        set
        {
            if (SetProperty(ref _selectedDefect, value) && value is not null)
            {
                ShowPreview(value);
            }
        }
    }

    private double _zoomLevel = 1.0;
    public double ZoomLevel
    {
        get => _zoomLevel;
        set => SetProperty(ref _zoomLevel, Math.Clamp(value, 0.05, 10.0));
    }

    private double _offsetX;
    public double OffsetX
    {
        get => _offsetX;
        set => SetProperty(ref _offsetX, value);
    }

    private double _offsetY;
    public double OffsetY
    {
        get => _offsetY;
        set => SetProperty(ref _offsetY, value);
    }

    public string? CurrentImagePath { get; set; }

    public int ThresholdValue { get; set; } = 30;

    public ICommand LoadImageCommand { get; }
    public ICommand InspectCommand { get; }
    public ICommand ResetZoomCommand { get; }
    public ICommand PanCommand { get; }

    public MainViewModel()
    {
        LoadImageCommand = new RelayCommand(async () => await LoadImageAsync());
        InspectCommand = new RelayCommand(InspectImage);
        ResetZoomCommand = new RelayCommand(ResetZoom);
        PanCommand = new RelayCommand<Vector>(delta => Pan(delta.X, delta.Y));
    }

    private async Task LoadImageAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "이미지 열기",
            Filters = new List<FileDialogFilter>
            {
                new FileDialogFilter { Name = "Image Files", Extensions = { "jpg", "png", "bmp" } }
            },
            AllowMultiple = false
        };

        if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = desktop.MainWindow;
            var result = await dialog.ShowAsync(window);

            if (result is { Length: > 0 } && File.Exists(result[0]))
            {
                CurrentImagePath = result[0];

                await using var stream = File.OpenRead(CurrentImagePath);
                Image = await Task.Run(() => Bitmap.DecodeToWidth(stream, 1200));

                Defects.Clear();
                PreviewImage = null;
                SelectedDefect = null;
                ResetZoom();
            }
        }
    }

    private void InspectImage()
    {
        if (string.IsNullOrEmpty(CurrentImagePath) || !File.Exists(CurrentImagePath))
            return;

        using var original = new Mat(CurrentImagePath, ImreadModes.Grayscale);

        int cutTop = 570;
        int cutBottom = 560;
        int cutRight = 1700;

        int croppedWidth = original.Cols - cutRight;
        int croppedHeight = original.Rows - cutTop - cutBottom;

        var cropRect = new OpenCvSharp.Rect(0, cutTop, croppedWidth, croppedHeight);
        var offset = new OpenCvSharp.Point(cropRect.X, cropRect.Y);

        if (cropRect.X < 0 || cropRect.Y < 0 || cropRect.Right > original.Cols || cropRect.Bottom > original.Rows)
        {
            Console.WriteLine("잘라낸 ROI가 이미지 범위를 벗어납니다.");
            return;
        }

        using var cropped = new Mat(original, cropRect);

        var (defects, _) = DefectChecker.FindDefectsWithDraw(cropped, ThresholdValue, offset);

        var result = new Mat();
        Cv2.CvtColor(original, result, ColorConversionCodes.GRAY2BGR);

        foreach (var defect in defects)
        {
            int padding = 30;
            int x = Math.Max(defect.X - padding, 0);
            int y = Math.Max(defect.Y - padding, 0);
            int width = Math.Min(defect.Width + padding * 2, result.Cols - x);
            int height = Math.Min(defect.Height + padding * 2, result.Rows - y);

            Cv2.Rectangle(result, new OpenCvSharp.Rect(x, y, width, height), Scalar.Yellow, 2);
        }

        using var ms = result.ToMemoryStream();
        Image = new Bitmap(ms);

        Defects.Clear();
        foreach (var defect in defects)
            Defects.Add(defect);

        PreviewImage = null;
        SelectedDefect = null;
        ResetZoom();
    }

    public void ZoomWithMouse(double mouseX, double mouseY, double delta)
    {
        double oldZoom = ZoomLevel;
        double newZoom = delta > 0
            ? Math.Clamp(oldZoom + 0.05, 0.05, 10.0)
            : Math.Clamp(oldZoom - 0.05, 0.05, 10.0);

        if (Math.Abs(newZoom - oldZoom) > 0.0001)
        {
            double zoomRatio = newZoom / oldZoom;
            OffsetX = (OffsetX - mouseX) * zoomRatio + mouseX;
            OffsetY = (OffsetY - mouseY) * zoomRatio + mouseY;
            ZoomLevel = newZoom;
        }
    }

    public void Pan(double deltaX, double deltaY)
    {
        OffsetX += deltaX;
        OffsetY += deltaY;
    }

    private void ResetZoom()
    {
        ZoomLevel = 1.0;
        OffsetX = 0;
        OffsetY = 0;
    }

    private void ShowPreview(Defect defect)
    {
        if (string.IsNullOrEmpty(CurrentImagePath))
            return;

        using var src = new Mat(CurrentImagePath, ImreadModes.Grayscale);
        if (src.Empty())
            return;

        int padding = 30;
        int scale = 4;

        int x = Math.Max(defect.X - padding, 0);
        int y = Math.Max(defect.Y - padding, 0);
        int width = Math.Min(defect.Width + padding * 2, src.Width - x);
        int height = Math.Min(defect.Height + padding * 2, src.Height - y);

        var roi = new OpenCvSharp.Rect(x, y, width, height);
        using var cropped = new Mat(src, roi);

        var zoomed = new Mat();
        Cv2.Resize(cropped, zoomed, new OpenCvSharp.Size(cropped.Width * scale, cropped.Height * scale), 0, 0, InterpolationFlags.Linear);

        using var ms = zoomed.ToMemoryStream();
        PreviewImage = new Bitmap(ms);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
