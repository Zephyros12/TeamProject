using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using OpenCvSharp;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using TeamProject.Models;

namespace TeamProject.ViewModels;

public class MainViewModel : ViewModelBase
{
    private double _offsetX;
    public double OffsetX
    {
        get => _offsetX;
        set => this.RaiseAndSetIfChanged(ref _offsetX, value);
    }

    private double _offsetY;
    public double OffsetY
    {
        get => _offsetY;
        set => this.RaiseAndSetIfChanged(ref _offsetY, value);
    }

    private double _zoomLevel = 1.0;
    public double ZoomLevel
    {
        get => _zoomLevel;
        set => this.RaiseAndSetIfChanged(ref _zoomLevel, Math.Clamp(value, 0.1, 5.0));
    }

    private Bitmap? _image;
    public Bitmap? Image
    {
        get => _image;
        set => this.RaiseAndSetIfChanged(ref _image, value);
    }

    private Bitmap? _previewImage;
    public Bitmap? PreviewImage
    {
        get => _previewImage;
        set => this.RaiseAndSetIfChanged(ref _previewImage, value);
    }

    public ObservableCollection<Defect> Defects { get; } = [];

    public string? CurrentImagePath { get; set; }

    private int _thresholdValue = 120;
    public int ThresholdValue
    {
        get => _thresholdValue;
        set => this.RaiseAndSetIfChanged(ref _thresholdValue, value);
    }

    private Defect? _selectedDefect;
    public Defect? SelectedDefect
    {
        get => _selectedDefect;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedDefect, value);
            if (value != null)
                ShowPreview(value);
        }
    }

    public ReactiveCommand<Unit, Unit> LoadImageCommand { get; }
    public ReactiveCommand<Unit, Unit> InspectCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetZoomCommand { get; }

    public MainViewModel()
    {
        LoadImageCommand = ReactiveCommand.CreateFromTask(LoadImageAsync);
        InspectCommand = ReactiveCommand.Create(InspectImage);
        ResetZoomCommand = ReactiveCommand.Create(ResetZoom);
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

        var window = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

        if (window == null)
            return;

        var result = await dialog.ShowAsync(window);

        if (result is { Length: > 0 } && File.Exists(result[0]))
        {
            CurrentImagePath = result[0];

            await using var stream = File.OpenRead(CurrentImagePath);
            Image = await Task.Run(() => Bitmap.DecodeToWidth(stream, 1200));

            Defects.Clear();
            PreviewImage = null;
            SelectedDefect = null;
        }
    }

    private void InspectImage()
    {
        if (string.IsNullOrEmpty(CurrentImagePath) || !File.Exists(CurrentImagePath))
            return;

        var (defects, resultMat) = DefectChecker.FindDefectsWithDraw(CurrentImagePath, ThresholdValue);

        using var ms = resultMat.ToMemoryStream();
        Image = new Bitmap(ms);

        Defects.Clear();
        foreach (var defect in defects)
        {
            Defects.Add(defect);
        }

        ZoomLevel = 1.0;
        OffsetX = 0;
        OffsetY = 0;

        PreviewImage = null;
        SelectedDefect = null;
    }

    private void ShowPreview(Defect defect)
    {
        if (string.IsNullOrEmpty(CurrentImagePath))
            return;

        using var src = new Mat(CurrentImagePath, ImreadModes.Color);
        if (src.Empty())
            return;

        int padding = 100;

        int x = Math.Max(defect.X - padding, 0);
        int y = Math.Max(defect.Y - padding, 0);
        int width = Math.Min(defect.Width + padding * 2, src.Width - x);
        int height = Math.Min(defect.Height + padding * 2, src.Height - x);

        var roi = new Rect(x, y, width, height);

        using var cropped = new Mat(src, roi);
        using var ms = cropped.ToMemoryStream();
        PreviewImage = new Bitmap(ms);
    }

    private void ResetZoom()
    {
        ZoomLevel = 1.0;
        OffsetX = 0;
        OffsetY = 0;
    }
}
