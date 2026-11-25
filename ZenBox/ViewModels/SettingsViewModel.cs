using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ZenBox.Services;

namespace ZenBox.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ConfigService _configService;
    private readonly Action _goHomeCallback;

    [ObservableProperty]
    private string _moviesFolderPath;

    [ObservableProperty]
    private string _musicFolderPath;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasStatusMessage;

    public SettingsViewModel(ConfigService configService, Action goHomeCallback)
    {
        _configService = configService;
        _goHomeCallback = goHomeCallback;

        var current = _configService.Current;
        _moviesFolderPath = current.MoviesFolderPath;
        _musicFolderPath = current.MusicFolderPath;
        _startWithWindows = current.StartWithWindows;
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        _configService.SetStartWithWindows(value);
        ShowConfirmation(value
            ? "Đã bật chế độ tự mở ZenBox khi bật máy tính Windows 11."
            : "Đã tắt chế độ tự mở cùng Windows 11.");
    }

    [RelayCommand]
    private void ChooseMoviesFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Chọn thư mục lưu Phim của bạn",
            Multiselect = false
        };

        if (Directory.Exists(MoviesFolderPath))
        {
            dialog.InitialDirectory = MoviesFolderPath;
        }

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            MoviesFolderPath = dialog.FolderName;
            _configService.Current.MoviesFolderPath = dialog.FolderName;
            _configService.Save();
            ShowConfirmation("Đã cập nhật thư mục Phim thành công!");
        }
    }

    [RelayCommand]
    private void ChooseMusicFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Chọn thư mục lưu Nhạc của bạn",
            Multiselect = false
        };

        if (Directory.Exists(MusicFolderPath))
        {
            dialog.InitialDirectory = MusicFolderPath;
        }

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            MusicFolderPath = dialog.FolderName;
            _configService.Current.MusicFolderPath = dialog.FolderName;
            _configService.Save();
            ShowConfirmation("Đã cập nhật thư mục Nhạc thành công!");
        }
    }

    [RelayCommand]
    private void GoHome()
    {
        _goHomeCallback();
    }

    private void ShowConfirmation(string message)
    {
        StatusMessage = message;
        HasStatusMessage = true;
    }
}
