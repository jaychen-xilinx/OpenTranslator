using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OpenTranslator.Helpers;
using OpenTranslator.Models;
using WinRT.Interop;
using Color = Windows.UI.Color;

namespace OpenTranslator.Services;

/// <summary>
/// 划词翻译服务 - 通过全局热键触发，负责获取选中文本、执行翻译、显示悬浮结果窗
///
/// 注意：自重构起，全局快捷键统一由 HotKeyService 注册，本类不再维护 WH_KEYBOARD_LL 钩子，
///       避免与 RegisterHotKey 系统热键冲突。本类仅暴露 TranslateSelectionAsync / ReplaceSelectionAsync
///       供 HotKeyService 的事件处理调用。
/// </summary>
public class SelectionTranslator
{
    private readonly TranslationService _translationService;
    private readonly AppConfigService _configService;

    private SelectionPopupWindow? _popupWindow;

    /// <summary>翻译完成事件</summary>
    public event EventHandler<TranslationResult>? TranslationCompleted;

    /// <summary>快捷键触发事件（携带选中文本）</summary>
    public event EventHandler<string>? HotKeyTriggered;

    public SelectionTranslator(TranslationService translationService, AppConfigService configService)
    {
        _translationService = translationService;
        _configService = configService;
    }

    /// <summary>
    /// 触发划词翻译：获取选中文本并翻译，显示悬浮窗
    /// </summary>
    public async Task TranslateSelectionAsync()
    {
        try
        {
            var selectedText = await GetSelectedTextAsync();

            if (string.IsNullOrWhiteSpace(selectedText))
            {
                ShowPopup("未检测到选中文本", "请先选中文本后按 Alt+Q");
                return;
            }

            HotKeyTriggered?.Invoke(this, selectedText);

            var config = _configService.GetConfig();
            var result = await _translationService.TranslateAsync(
                selectedText,
                "auto",
                config.TargetLanguage);

            ShowTranslationPopup(result);
            TranslationCompleted?.Invoke(this, result);
        }
        catch (Exception ex)
        {
            ShowPopup("翻译失败", ex.Message);
        }
    }

    /// <summary>
    /// 触发原地替换翻译：获取选中文本、翻译并粘贴覆盖
    /// </summary>
    public async Task ReplaceSelectionAsync()
    {
        try
        {
            var selectedText = await GetSelectedTextAsync();

            if (string.IsNullOrWhiteSpace(selectedText))
            {
                ShowPopup("未检测到选中文本", "请先选中文本后按 Ctrl+Shift+T");
                return;
            }

            HotKeyTriggered?.Invoke(this, selectedText);

            var config = _configService.GetConfig();
            var result = await _translationService.TranslateAsync(
                selectedText,
                "auto",
                config.TargetLanguage);

            // 将译文写入剪贴板并模拟 Ctrl+V 粘贴覆盖原文
            await ReplaceTextAsync(result.TranslatedText);
            HidePopup();
        }
        catch (Exception ex)
        {
            ShowPopup("替换失败", ex.Message);
        }
    }

    /// <summary>
    /// 获取当前选中的文本（通过模拟 Ctrl+C 复制到剪贴板读取）
    /// </summary>
    private async Task<string> GetSelectedTextAsync()
    {
        string? originalClipboard = null;

        try
        {
            // 保存当前剪贴板内容，避免污染
            var dataPackage = Clipboard.GetContent();
            if (dataPackage != null && dataPackage.Contains(StandardDataFormats.Text))
            {
                originalClipboard = await dataPackage.GetTextAsync();
            }
        }
        catch { /* 忽略剪贴板读取错误 */ }

        string? selectedText = null;

        try
        {
            // 模拟 Ctrl+C 复制选中文本
            SimulateKeyPress(NativeMethods.VK_CONTROL, 0x43); // 0x43 = C

            // 等待剪贴板内容更新
            await Task.Delay(120);
            selectedText = await GetClipboardTextAsync();
        }
        catch { /* 忽略 */ }

        // 恢复原始剪贴板内容
        if (originalClipboard != null)
        {
            try
            {
                var package = new DataPackage();
                package.SetText(originalClipboard);
                Clipboard.SetContent(package);
            }
            catch { /* 忽略恢复错误 */ }
        }

        return selectedText ?? string.Empty;
    }

    private async Task<string?> GetClipboardTextAsync()
    {
        try
        {
            var package = Clipboard.GetContent();
            if (package != null && package.Contains(StandardDataFormats.Text))
            {
                return await package.GetTextAsync();
            }
        }
        catch { /* 忽略 */ }
        return null;
    }

    /// <summary>
    /// 替换选中文本（将文本写入剪贴板并模拟 Ctrl+V）
    /// </summary>
    private async Task ReplaceTextAsync(string newText)
    {
        try
        {
            var package = new DataPackage();
            package.SetText(newText);
            Clipboard.SetContent(package);

            // 等待剪贴板稳定
            await Task.Delay(60);

            // 模拟 Ctrl+V 粘贴
            SimulateKeyPress(NativeMethods.VK_CONTROL, 0x56); // 0x56 = V
        }
        catch { /* 忽略 */ }
    }

    private static void SimulateKeyPress(byte vk, byte scan)
    {
        NativeMethods.keybd_event(vk, scan, 0, UIntPtr.Zero);
        NativeMethods.keybd_event(vk, scan, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    /// <summary>
    /// 显示翻译结果悬浮窗口
    /// </summary>
    private void ShowTranslationPopup(TranslationResult result)
    {
        HidePopup();

        _popupWindow = new SelectionPopupWindow();
        _popupWindow.Show(result.SourceText, result.TranslatedText, result.InferenceTimeMs);
        _popupWindow.Closed += (s, e) => _popupWindow = null;
    }

    /// <summary>
    /// 显示提示信息
    /// </summary>
    private void ShowPopup(string title, string message)
    {
        HidePopup();

        _popupWindow = new SelectionPopupWindow();
        _popupWindow.Show(title, message, null);
        _popupWindow.Closed += (s, e) => _popupWindow = null;
    }

    /// <summary>
    /// 隐藏悬浮窗口
    /// </summary>
    public void HidePopup()
    {
        _popupWindow?.Hide();
        _popupWindow = null;
    }
}

/// <summary>
/// 划词翻译悬浮窗口 - 使用独立 Window + AppWindow 定位
/// 相比 WinUI Popup，独立窗口可在多显示器场景下精确定位，且不受父容器约束
/// </summary>
public sealed class SelectionPopupWindow
{
    private Window? _window;
    private AppWindow? _appWindow;
    private TextBlock _sourceTextBlock = null!;
    private TextBlock _translatedTextBlock = null!;
    private TextBlock _timeTextBlock = null!;
    private Button _copyButton = null!;
    private Button _replaceButton = null!;

    public event EventHandler? Closed;

    private const int WindowWidth = 420;
    private const int WindowHeight = 280;

    public SelectionPopupWindow()
    {
        _window = new Window
        {
            Title = "OpenTranslator 翻译结果",
            ExtendsContentIntoTitleBar = true,
            Content = CreateContent()
        };

        ConfigureAppWindow();
    }

    private UIElement CreateContent()
    {
        var root = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(255, 30, 30, 30)),
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(8)
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });       // 标题
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });       // 原文
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 译文
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });       // 按钮栏

        // 标题栏
        var titlePanel = new StackPanel { Orientation = Orientation.Horizontal };
        titlePanel.Children.Add(new TextBlock
        {
            Text = "翻译结果",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 14,
            Foreground = new SolidColorBrush(Colors.White),
            VerticalAlignment = VerticalAlignment.Center
        });

        var closeButton = new Button
        {
            Content = "✕",
            Width = 28,
            Height = 28,
            Padding = new Thickness(0),
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
            Foreground = new SolidColorBrush(Colors.White),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        closeButton.Click += (s, e) => Hide();
        titlePanel.Children.Add(closeButton);
        Grid.SetRow(titlePanel, 0);
        grid.Children.Add(titlePanel);

        // 原文
        _sourceTextBlock = new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
            TextWrapping = TextWrapping.Wrap,
            MaxLines = 3,
            Margin = new Thickness(0, 8, 0, 0)
        };
        Grid.SetRow(_sourceTextBlock, 1);
        grid.Children.Add(_sourceTextBlock);

        // 译文（可滚动）
        _translatedTextBlock = new TextBlock
        {
            FontSize = 16,
            Foreground = new SolidColorBrush(Colors.White),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        };
        var translatedScroll = new ScrollViewer
        {
            Content = _translatedTextBlock,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 8, 0, 8)
        };
        Grid.SetRow(translatedScroll, 2);
        grid.Children.Add(translatedScroll);

        // 底部按钮栏
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };

        _timeTextBlock = new TextBlock
        {
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        buttonPanel.Children.Add(_timeTextBlock);

        _copyButton = new Button
        {
            Content = "复制",
            Width = 60,
            Height = 30,
            FontSize = 12
        };
        _copyButton.Click += OnCopyClick;
        buttonPanel.Children.Add(_copyButton);

        _replaceButton = new Button
        {
            Content = "替换",
            Width = 60,
            Height = 30,
            FontSize = 12
        };
        _replaceButton.Click += OnReplaceClick;
        buttonPanel.Children.Add(_replaceButton);

        Grid.SetRow(buttonPanel, 3);
        grid.Children.Add(buttonPanel);

        root.Child = grid;
        return root;
    }

    private void ConfigureAppWindow()
    {
        if (_window == null) return;

        var hwnd = WindowNative.GetWindowHandle(_window);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        if (_appWindow != null)
        {
            // WindowsAppSDK 1.4 通过 OverlappedPresenter 控制置顶
            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsAlwaysOnTop = true;
            }
            _appWindow.Resize(new SizeInt32(WindowWidth, WindowHeight));
            _appWindow.Closing += (_, _) => Closed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// 根据鼠标位置定位窗口，自动避开屏幕边缘
    /// </summary>
    private void PositionWindow()
    {
        if (_appWindow == null) return;

        int x = 100, y = 100;

        if (NativeMethods.GetCursorPos(out var mousePos))
        {
            x = mousePos.X + 16;
            y = mousePos.Y + 16;

            // 获取鼠标所在显示器的工作区，避免窗口超出边界
            var displayArea = DisplayArea.GetFromPoint(
                new PointInt32(mousePos.X, mousePos.Y), DisplayAreaFallback.Primary);

            if (displayArea != null)
            {
                var workArea = displayArea.WorkArea;
                if (x + WindowWidth > workArea.X + workArea.Width)
                    x = workArea.X + workArea.Width - WindowWidth - 8;
                if (y + WindowHeight > workArea.Y + workArea.Height)
                    y = workArea.Y + workArea.Height - WindowHeight - 8;
                if (x < workArea.X) x = workArea.X + 8;
                if (y < workArea.Y) y = workArea.Y + 8;
            }
        }

        _appWindow.Move(new PointInt32(x, y));
    }

    public void Show(string sourceText, string translatedText, long? inferenceTimeMs)
    {
        if (_window == null) return;

        _sourceTextBlock.Text = sourceText;
        _translatedTextBlock.Text = translatedText;
        _timeTextBlock.Text = inferenceTimeMs.HasValue ? $"{inferenceTimeMs}ms" : string.Empty;

        PositionWindow();
        _window.Activate();
    }

    public void Hide()
    {
        if (_window != null)
        {
            _window.Close();
            _window = null;
            _appWindow = null;
        }
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var package = new DataPackage();
            package.SetText(_translatedTextBlock.Text);
            Clipboard.SetContent(package);
        }
        catch { /* 忽略 */ }
    }

    private async void OnReplaceClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var package = new DataPackage();
            package.SetText(_translatedTextBlock.Text);
            Clipboard.SetContent(package);

            await Task.Delay(60);

            // 模拟 Ctrl+V 粘贴到原前台窗口
            NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0x56, 0, UIntPtr.Zero);
            NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0x56, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);

            Hide();
        }
        catch { /* 忽略 */ }
    }
}
