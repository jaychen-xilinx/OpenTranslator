using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using OpenTranslator.Services;
using OpenTranslator.App.Wpf.Views;
using OpenTranslator.Models;
using OpenTranslator.ViewModels;
using OpenTranslator.Helpers;

namespace OpenTranslator.App.Wpf;

public partial class MainWindow : Window
{
    private HotKeyService? _hotKeyService;
    private SelectionTranslator? _selectionTranslator;
    private SelectionPopupWindow? _popupWindow;
    private AppConfigService? _configService;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        
        var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.png");
        if (File.Exists(iconPath))
        {
            try
            {
                var bitmap = new BitmapImage(new Uri(iconPath));
                Icon = bitmap;
            }
            catch { }
        }
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _configService = App.Services.GetRequiredService<AppConfigService>();
            _hotKeyService = App.Services.GetRequiredService<HotKeyService>();
            _selectionTranslator = App.Services.GetRequiredService<SelectionTranslator>();

            if (_configService != null)
            {
                _configService.ConfigChanged += OnConfigChanged;
                ApplyLayout(_configService.GetConfig().MainWindowLayout);
            }

            if (_hotKeyService != null)
            {
                _hotKeyService.TranslateHotKeyPressed += OnTranslateHotKeyPressed;
                _hotKeyService.ReplaceHotKeyPressed += OnReplaceHotKeyPressed;
                var hotKeyOk = _hotKeyService.Start();
                var config = _configService?.GetConfig();
                var hotKeyStr = config?.HotKeys.TranslateHotKey ?? "Ctrl+Alt+Q";
                Console.WriteLine($"[MainWindow] 热键注册结果: {hotKeyOk}, 热键: {hotKeyStr}, 错误: {_hotKeyService.LastError}");

                if (!hotKeyOk && DataContext is MainViewModel vm)
                {
                    vm.StatusText = $"⚠ 热键 ({hotKeyStr}) 注册失败: {_hotKeyService.LastError}";
                }
            }

            if (_selectionTranslator != null)
            {
                _selectionTranslator.ShowPopupRequested += OnShowPopupRequested;
                _selectionTranslator.GetSelectionRequested += OnGetSelectionRequested;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainWindow] 初始化失败: {ex.Message}");
        }
    }

    private void OnConfigChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (_configService != null)
            {
                ApplyLayout(_configService.GetConfig().MainWindowLayout);
            }
        });
    }

    private void ApplyLayout(LayoutMode mode)
    {
        var sourceBorder = (Border)PanelsGrid.Children[0];
        var targetBorder = (Border)PanelsGrid.Children[1];

        PanelsGrid.ColumnDefinitions.Clear();
        PanelsGrid.RowDefinitions.Clear();

        if (mode == LayoutMode.Horizontal)
        {
            PanelsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            PanelsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Grid.SetColumn(sourceBorder, 0);
            Grid.SetRow(sourceBorder, 0);
            sourceBorder.Margin = new Thickness(0, 0, 6, 0);

            Grid.SetColumn(targetBorder, 1);
            Grid.SetRow(targetBorder, 0);
            targetBorder.Margin = new Thickness(6, 0, 0, 0);
        }
        else
        {
            PanelsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            PanelsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid.SetColumn(sourceBorder, 0);
            Grid.SetRow(sourceBorder, 0);
            sourceBorder.Margin = new Thickness(0, 0, 0, 6);

            Grid.SetColumn(targetBorder, 0);
            Grid.SetRow(targetBorder, 1);
            targetBorder.Margin = new Thickness(0, 6, 0, 0);
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            if (_configService != null)
            {
                _configService.ConfigChanged -= OnConfigChanged;
            }
            if (_hotKeyService != null)
            {
                _hotKeyService.TranslateHotKeyPressed -= OnTranslateHotKeyPressed;
                _hotKeyService.ReplaceHotKeyPressed -= OnReplaceHotKeyPressed;
                _hotKeyService.Stop();
                _hotKeyService.Dispose();
            }
            if (_selectionTranslator != null)
            {
                _selectionTranslator.ShowPopupRequested -= OnShowPopupRequested;
                _selectionTranslator.GetSelectionRequested -= OnGetSelectionRequested;
            }
            _popupWindow?.Close();
        }
        catch { }
    }

    private async void OnTranslateHotKeyPressed(object? sender, EventArgs e)
    {
        try
        {
            if (_selectionTranslator != null)
            {
                await _selectionTranslator.TranslateSelectionAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainWindow] 划词翻译异常: {ex}");
        }
    }

    private async void OnReplaceHotKeyPressed(object? sender, EventArgs e)
    {
        try
        {
            if (_selectionTranslator != null)
            {
                await _selectionTranslator.ReplaceSelectionAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainWindow] 原地替换翻译异常: {ex}");
        }
    }

    /// <summary>
    /// 在 UI 线程上获取选中文本（优先使用 SendMessage WM_COPY）
    /// </summary>
    private string? OnGetSelectionRequested()
    {
        string? originalClipboard = null;
        string? selectedText = null;

        try
        {
            var foregroundWnd = NativeMethods.GetForegroundWindow();
            if (foregroundWnd == IntPtr.Zero)
            {
                Console.WriteLine("[MainWindow] 未找到前景窗口");
                return null;
            }

            if (Clipboard.ContainsText())
            {
                originalClipboard = Clipboard.GetText();
            }

            Clipboard.Clear();

            NativeMethods.SendMessage(foregroundWnd, NativeMethods.WM_COPY, IntPtr.Zero, IntPtr.Zero);
            System.Threading.Thread.Sleep(100);

            if (Clipboard.ContainsText())
            {
                selectedText = Clipboard.GetText();
            }

            if (string.IsNullOrWhiteSpace(selectedText))
            {
                Console.WriteLine("[MainWindow] SendMessage WM_COPY 失败，尝试模拟 Ctrl+C");
                ReleaseModifierKeys();
                System.Threading.Thread.Sleep(80);
                SimulateCtrlPlusKey(0x43);
                System.Threading.Thread.Sleep(200);

                if (Clipboard.ContainsText())
                {
                    selectedText = Clipboard.GetText();
                }
            }

            if (string.IsNullOrWhiteSpace(selectedText))
            {
                Console.WriteLine("[MainWindow] 模拟 Ctrl+C 失败，再次尝试 SendMessage");
                Clipboard.Clear();
                System.Threading.Thread.Sleep(50);
                NativeMethods.SendMessage(foregroundWnd, NativeMethods.WM_COPY, IntPtr.Zero, IntPtr.Zero);
                System.Threading.Thread.Sleep(150);

                if (Clipboard.ContainsText())
                {
                    selectedText = Clipboard.GetText();
                }
            }

            Console.WriteLine($"[MainWindow] 获取到选中文本: {(selectedText?.Length > 50 ? selectedText[..50] + "..." : selectedText)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainWindow] 获取选中文本异常: {ex.Message}");
        }
        finally
        {
            if (originalClipboard != null)
            {
                try
                {
                    Clipboard.SetText(originalClipboard);
                }
                catch { }
            }
        }

        return selectedText;
    }

    private void SimulateCtrlPlusKey(byte vkKey)
    {
        NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0, 0, UIntPtr.Zero);
        System.Threading.Thread.Sleep(10);
        NativeMethods.keybd_event(vkKey, 0, 0, UIntPtr.Zero);
        System.Threading.Thread.Sleep(10);
        NativeMethods.keybd_event(vkKey, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        System.Threading.Thread.Sleep(10);
        NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    private void ReleaseModifierKeys()
    {
        if ((NativeMethods.GetKeyState(NativeMethods.VK_CONTROL) & 0x8000) != 0)
            NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        if ((NativeMethods.GetKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0)
            NativeMethods.keybd_event(NativeMethods.VK_SHIFT, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        if ((NativeMethods.GetKeyState(NativeMethods.VK_MENU) & 0x8000) != 0)
            NativeMethods.keybd_event(NativeMethods.VK_MENU, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        System.Threading.Thread.Sleep(50);
    }

    private void OnShowPopupRequested(object? sender, (string title, string message, long? timeMs) e)
    {
        try
        {
            Dispatcher.Invoke(() =>
            {
                _popupWindow?.Close();
                _popupWindow = new SelectionPopupWindow();
                _popupWindow.Show(e.title, e.message, e.timeMs);
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainWindow] 显示悬浮窗异常: {ex}");
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow
        {
            Owner = this
        };
        settingsWindow.ShowDialog();
    }
}
