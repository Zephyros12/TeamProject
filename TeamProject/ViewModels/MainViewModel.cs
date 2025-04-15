using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
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
    private Bitmap? _image;
    public Bitmap? Image
    {
        get => _image;
        set => this.RaiseAndSetIfChanged(ref _image, value);
    }

    public ObservableCollection<Defect> Defects { get; } = new();

    public ReactiveCommand<Unit, Unit> LoadImageCommand { get; }
    public ReactiveCommand<Unit, Unit> InspectCommand { get; }

    public string? CurrentImagePath { get; set; }

    private int _thresholdValue = 100;
    public int ThresholdValue
    {
        get => _thresholdValue;
        set => this.RaiseAndSetIfChanged(ref _thresholdValue, value);
    }

    public MainViewModel()
    {
        LoadImageCommand = ReactiveCommand.CreateFromTask(LoadImageAsync);
        InspectCommand = ReactiveCommand.Create(InspectImage);
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

        if (result != null && result.Length > 0 && File.Exists(result[0]))
        {
            CurrentImagePath = result[0];

            await using var stream = File.OpenRead(CurrentImagePath);
            Image = await Task.Run(() => Bitmap.DecodeToWidth(stream, 800));
        }
    }

    private void InspectImage()
    {
        if (string.IsNullOrEmpty(CurrentImagePath) || !File.Exists(CurrentImagePath))
            return;

        var (defects, resultImage) = DefectChecker.FindDefectsWithDraw(CurrentImagePath, ThresholdValue);

        using var ms = resultImage.ToMemoryStream();
        Image = new Bitmap(ms);

        Defects.Clear();
        foreach (var defect in defects)
        {
            Defects.Add(defect);
        }
    }
}
