using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OpenTranslator.Models;
using OpenTranslator.Services;
using WinRT.Interop;

namespace OpenTranslator.Views;

/// <summary>
/// 设置窗口 - 包含布局切换、热键设置、外观等配置项
/// </summary>
public sealed class SettingsWindow : Window
{
    private readonly AppConfigService _configService;
    private readonly MainWindow _mainWindow;
    private AppConfig _config;

    private RadioButton? _horizontalLayoutRadio;
    private RadioButton? _verticalLayoutRadio;
    private ComboBox? _themeComboBox;
    private TextBox? _translateHotKeyTextBox;
    private TextBox? _replaceHotKeyTextBox;
    private TextBox? _modelsDirTextBox;
    private Button? _saveButton;
    private Button? _cancelButton;
    private TextBlock? _statusTextBlock;

    public SettingsWindow(AppConfigService configService, MainWindow mainWindow)
    {
        _configService = configService;
        _mainWindow = mainWindow;
        _config = _configService.GetConfig();

        Title = "设置";

        ConfigureWindow();
        BuildUi();
        LoadSettings();
    }

    private void ConfigureWindow()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        if (appWindow != null)
        {
            appWindow.Resize(new Windows.Graphics.SizeInt32(480, 520));

            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
            }
        }
    }

    private void BuildUi()
    {
        var rootScroll = new ScrollViewer
        {
            Background = new SolidColorBrush(Microsoft.UI.Colors.WhiteSmoke),
            Padding = new Thickness(24)
        };

        var rootStack = new StackPanel
        {
            Spacing = 24
        };

        // ===== 界面设置 =====
        var appearanceSection = CreateSection("界面设置");

        var layoutPanel = new StackPanel { Spacing = 8 };
        layoutPanel.Children.Add(new TextBlock
        {
            Text = "主窗口布局",
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black)
        });

        var layoutRadioPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 24
        };

        _horizontalLayoutRadio = new RadioButton
        {
            Content = "左右布局",
            GroupName = "Layout"
        };
        _horizontalLayoutRadio.Checked += OnLayoutChanged;
        layoutRadioPanel.Children.Add(_horizontalLayoutRadio);

        _verticalLayoutRadio = new RadioButton
        {
            Content = "上下布局",
            GroupName = "Layout"
        };
        _verticalLayoutRadio.Checked += OnLayoutChanged;
        layoutRadioPanel.Children.Add(_verticalLayoutRadio);

        layoutPanel.Children.Add(layoutRadioPanel);
        appearanceSection.Children.Add(layoutPanel);

        var themePanel = new StackPanel { Spacing = 8, Margin = new Thickness(0, 16, 0, 0) };
        themePanel.Children.Add(new TextBlock
        {
            Text = "主题",
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black)
        });

        _themeComboBox = new ComboBox
        {
            Width = 200,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _themeComboBox.Items.Add("跟随系统");
        _themeComboBox.Items.Add("浅色模式");
        _themeComboBox.Items.Add("深色模式");
        themePanel.Children.Add(_themeComboBox);

        appearanceSection.Children.Add(themePanel);
        rootStack.Children.Add(appearanceSection);

        // ===== 快捷键设置 =====
        var hotkeySection = CreateSection("快捷键设置");

        var translateHotKeyPanel = CreateHotKeyRow("划词翻译", "Alt+Q");
        _translateHotKeyTextBox = (TextBox)((Grid)translateHotKeyPanel.Children[1]).Children[0];
        hotkeySection.Children.Add(translateHotKeyPanel);

        var replaceHotKeyPanel = CreateHotKeyRow("原地替换翻译", "Ctrl+Shift+T");
        _replaceHotKeyTextBox = (TextBox)((Grid)replaceHotKeyPanel.Children[1]).Children[0];
        hotkeySection.Children.Add(replaceHotKeyPanel);

        var hotkeyHint = new TextBlock
        {
            Text = "提示：修改热键后需重启程序生效",
            FontSize = 12,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray),
            Margin = new Thickness(0, 8, 0, 0)
        };
        hotkeySection.Children.Add(hotkeyHint);

        rootStack.Children.Add(hotkeySection);

        // ===== 模型设置 =====
        var modelSection = CreateSection("模型设置");

        var modelsDirPanel = new StackPanel { Spacing = 8 };
        modelsDirPanel.Children.Add(new TextBlock
        {
            Text = "模型目录",
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black)
        });

        var modelsDirRow = new Grid();
        modelsDirRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        modelsDirRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _modelsDirTextBox = new TextBox
        {
            PlaceholderText = "模型文件存放目录",
            IsReadOnly = false
        };
        Grid.SetColumn(_modelsDirTextBox, 0);
        modelsDirRow.Children.Add(_modelsDirTextBox);

        var browseBtn = new Button
        {
            Content = "浏览",
            Margin = new Thickness(8, 0, 0, 0),
            Height = 32
        };
        browseBtn.Click += OnBrowseModelsDirClick;
        Grid.SetColumn(browseBtn, 1);
        modelsDirRow.Children.Add(browseBtn);

        modelsDirPanel.Children.Add(modelsDirRow);
        modelSection.Children.Add(modelsDirPanel);

        rootStack.Children.Add(modelSection);

        // ===== 底部按钮 =====
        var bottomPanel = new Grid();
        bottomPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bottomPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bottomPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bottomPanel.Margin = new Thickness(0, 8, 0, 0);

        _statusTextBlock = new TextBlock
        {
            Text = string.Empty,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(_statusTextBlock, 0);
        bottomPanel.Children.Add(_statusTextBlock);

        _cancelButton = new Button
        {
            Content = "取消",
            Width = 80,
            Height = 36,
            Margin = new Thickness(0, 0, 8, 0)
        };
        _cancelButton.Click += OnCancelClick;
        Grid.SetColumn(_cancelButton, 1);
        bottomPanel.Children.Add(_cancelButton);

        _saveButton = new Button
        {
            Content = "保存",
            Width = 80,
            Height = 36,
            Background = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue),
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White)
        };
        _saveButton.Click += OnSaveClick;
        Grid.SetColumn(_saveButton, 2);
        bottomPanel.Children.Add(_saveButton);

        rootStack.Children.Add(bottomPanel);

        rootScroll.Content = rootStack;
        Content = rootScroll;
    }

    private static StackPanel CreateSection(string title)
    {
        var panel = new StackPanel
        {
            Background = new SolidColorBrush(Microsoft.UI.Colors.White),
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(8),
            BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.LightGray),
            BorderThickness = new Thickness(1),
            Spacing = 12
        };

        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black)
        });

        return panel;
    }

    private static StackPanel CreateHotKeyRow(string label, string defaultValue)
    {
        var panel = new StackPanel { Spacing = 8 };

        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 13,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.DarkGray)
        });

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var textBox = new TextBox
        {
            Text = defaultValue,
            PlaceholderText = "例如：Ctrl+Shift+T",
            Height = 32
        };
        Grid.SetColumn(textBox, 0);
        row.Children.Add(textBox);

        panel.Children.Add(row);
        return panel;
    }

    private void LoadSettings()
    {
        if (_horizontalLayoutRadio != null)
            _horizontalLayoutRadio.IsChecked = _config.MainWindowLayout == LayoutMode.Horizontal;
        if (_verticalLayoutRadio != null)
            _verticalLayoutRadio.IsChecked = _config.MainWindowLayout == LayoutMode.Vertical;

        if (_themeComboBox != null)
        {
            _themeComboBox.SelectedIndex = _config.Theme switch
            {
                "Light" => 1,
                "Dark" => 2,
                _ => 0
            };
        }

        if (_translateHotKeyTextBox != null)
            _translateHotKeyTextBox.Text = _config.HotKeys.TranslateHotKey;

        if (_replaceHotKeyTextBox != null)
            _replaceHotKeyTextBox.Text = _config.HotKeys.ReplaceHotKey;

        if (_modelsDirTextBox != null)
            _modelsDirTextBox.Text = _config.ModelsDirectory;
    }

    private void OnLayoutChanged(object sender, RoutedEventArgs e)
    {
        if (_horizontalLayoutRadio?.IsChecked == true)
        {
            _mainWindow.ApplyLayout(LayoutMode.Horizontal);
        }
        else if (_verticalLayoutRadio?.IsChecked == true)
        {
            _mainWindow.ApplyLayout(LayoutMode.Vertical);
        }
    }

    private void OnBrowseModelsDirClick(object sender, RoutedEventArgs e)
    {
        // 简单提示，完整的文件对话框需要更多 WinRT 互操作
        if (_statusTextBlock != null)
        {
            _statusTextBlock.Text = "请手动输入模型目录路径";
            _statusTextBlock.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray);
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        _mainWindow.ApplyLayout(_config.MainWindowLayout);
        Close();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        try
        {
            _config.MainWindowLayout = _horizontalLayoutRadio?.IsChecked == true
                ? LayoutMode.Horizontal
                : LayoutMode.Vertical;

            if (_themeComboBox != null)
            {
                _config.Theme = _themeComboBox.SelectedIndex switch
                {
                    1 => "Light",
                    2 => "Dark",
                    _ => "System"
                };
            }

            if (_translateHotKeyTextBox != null)
                _config.HotKeys.TranslateHotKey = _translateHotKeyTextBox.Text.Trim();

            if (_replaceHotKeyTextBox != null)
                _config.HotKeys.ReplaceHotKey = _replaceHotKeyTextBox.Text.Trim();

            if (_modelsDirTextBox != null)
                _config.ModelsDirectory = _modelsDirTextBox.Text.Trim();

            _configService.SaveConfig(_config);

            if (_statusTextBlock != null)
            {
                _statusTextBlock.Text = "设置已保存";
                _statusTextBlock.Foreground = new SolidColorBrush(Microsoft.UI.Colors.DarkGreen);
            }

            DispatcherQueue.TryEnqueue(() =>
            {
                Task.Delay(800).Wait();
                Close();
            });
        }
        catch (Exception ex)
        {
            if (_statusTextBlock != null)
            {
                _statusTextBlock.Text = $"保存失败: {ex.Message}";
                _statusTextBlock.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Crimson);
            }
        }
    }
}
