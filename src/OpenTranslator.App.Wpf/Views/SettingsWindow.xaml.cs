using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using OpenTranslator.Services;

namespace OpenTranslator.App.Wpf.Views;

public partial class SettingsWindow : Window
{
    private readonly AppConfigService _configService;
    private readonly HotKeyService _hotKeyService;
    private bool _isRecording = false;
    private string _recordingTarget = "";

    public SettingsWindow()
    {
        InitializeComponent();
        _configService = App.Services.GetRequiredService<AppConfigService>();
        _hotKeyService = App.Services.GetRequiredService<HotKeyService>();
        LoadConfig();
    }

    private void LoadConfig()
    {
        var config = _configService.GetConfig();
        LayoutComboBox.SelectedIndex = (int)config.MainWindowLayout;
        ModelPathText.Text = config.ModelsDirectory ?? "未设置";
        TranslateHotKeyBox.Text = FormatHotKey(config.HotKeys.TranslateHotKey);
        ReplaceHotKeyBox.Text = FormatHotKey(config.HotKeys.ReplaceHotKey);
    }

    private string FormatHotKey(string hotKey)
    {
        return hotKey?.Replace("+", " + ") ?? "未设置";
    }

    private string ParseHotKey(KeyEventArgs e)
    {
        var modifiers = "";
        var key = "";

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            modifiers += "Ctrl+";
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            modifiers += "Shift+";
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
            modifiers += "Alt+";

        switch (e.Key)
        {
            case Key.D0: key = "0"; break;
            case Key.D1: key = "1"; break;
            case Key.D2: key = "2"; break;
            case Key.D3: key = "3"; break;
            case Key.D4: key = "4"; break;
            case Key.D5: key = "5"; break;
            case Key.D6: key = "6"; break;
            case Key.D7: key = "7"; break;
            case Key.D8: key = "8"; break;
            case Key.D9: key = "9"; break;
            case Key.NumPad0: key = "Num0"; break;
            case Key.NumPad1: key = "Num1"; break;
            case Key.NumPad2: key = "Num2"; break;
            case Key.NumPad3: key = "Num3"; break;
            case Key.NumPad4: key = "Num4"; break;
            case Key.NumPad5: key = "Num5"; break;
            case Key.NumPad6: key = "Num6"; break;
            case Key.NumPad7: key = "Num7"; break;
            case Key.NumPad8: key = "Num8"; break;
            case Key.NumPad9: key = "Num9"; break;
            case Key.F1: key = "F1"; break;
            case Key.F2: key = "F2"; break;
            case Key.F3: key = "F3"; break;
            case Key.F4: key = "F4"; break;
            case Key.F5: key = "F5"; break;
            case Key.F6: key = "F6"; break;
            case Key.F7: key = "F7"; break;
            case Key.F8: key = "F8"; break;
            case Key.F9: key = "F9"; break;
            case Key.F10: key = "F10"; break;
            case Key.F11: key = "F11"; break;
            case Key.F12: key = "F12"; break;
            case Key.Left: key = "Left"; break;
            case Key.Right: key = "Right"; break;
            case Key.Up: key = "Up"; break;
            case Key.Down: key = "Down"; break;
            case Key.Space: key = "Space"; break;
            case Key.Tab: key = "Tab"; break;
            case Key.Enter: key = "Enter"; break;
            case Key.Back: key = "Back"; break;
            case Key.Delete: key = "Delete"; break;
            case Key.Home: key = "Home"; break;
            case Key.End: key = "End"; break;
            case Key.PageUp: key = "PageUp"; break;
            case Key.PageDown: key = "PageDown"; break;
            case Key.Insert: key = "Insert"; break;
            case Key.Escape: key = "Esc"; break;
            default:
                if (e.Key >= Key.A && e.Key <= Key.Z)
                    key = e.Key.ToString().ToUpper();
                break;
        }

        if (string.IsNullOrEmpty(modifiers) && string.IsNullOrEmpty(key))
            return "";

        return modifiers + key;
    }

    private void TranslateHotKeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        if (!_isRecording)
        {
            _isRecording = true;
            _recordingTarget = "translate";
            TranslateHotKeyBox.Text = "...";
            return;
        }

        var hotKey = ParseHotKey(e);
        if (!string.IsNullOrEmpty(hotKey))
        {
            TranslateHotKeyBox.Text = FormatHotKey(hotKey);
            _isRecording = false;
            _recordingTarget = "";
        }
    }

    private void ReplaceHotKeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        if (!_isRecording)
        {
            _isRecording = true;
            _recordingTarget = "replace";
            ReplaceHotKeyBox.Text = "...";
            return;
        }

        var hotKey = ParseHotKey(e);
        if (!string.IsNullOrEmpty(hotKey))
        {
            ReplaceHotKeyBox.Text = FormatHotKey(hotKey);
            _isRecording = false;
            _recordingTarget = "";
        }
    }

    private void ResetTranslateHotKeyBtn_Click(object sender, RoutedEventArgs e)
    {
        TranslateHotKeyBox.Text = "Ctrl + Alt + B";
    }

    private void ResetReplaceHotKeyBtn_Click(object sender, RoutedEventArgs e)
    {
        ReplaceHotKeyBox.Text = "Ctrl + Alt + V";
    }

    private void BrowseModelButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "选择模型目录"
        };

        if (dialog.ShowDialog() == true)
        {
            var config = _configService.GetConfig();
            config.ModelsDirectory = dialog.FolderName;
            _configService.SaveConfig(config);
            ModelPathText.Text = dialog.FolderName;
            MessageBox.Show("模型目录已更新，重启程序后生效。", "设置", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        var config = _configService.GetConfig();

        if (LayoutComboBox.SelectedIndex >= 0)
        {
            config.MainWindowLayout = (Models.LayoutMode)LayoutComboBox.SelectedIndex;
        }

        var translateHotKey = TranslateHotKeyBox.Text.Replace(" ", "");
        var replaceHotKey = ReplaceHotKeyBox.Text.Replace(" ", "");

        bool needRestart = false;
        if (config.HotKeys.TranslateHotKey != translateHotKey)
        {
            config.HotKeys.TranslateHotKey = translateHotKey;
            needRestart = true;
        }
        if (config.HotKeys.ReplaceHotKey != replaceHotKey)
        {
            config.HotKeys.ReplaceHotKey = replaceHotKey;
            needRestart = true;
        }

        _configService.SaveConfig(config);

        if (needRestart)
        {
            MessageBox.Show("快捷键已更新，重启程序后生效。", "设置", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        Close();
    }
}