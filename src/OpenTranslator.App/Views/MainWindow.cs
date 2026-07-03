using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OpenTranslator.Helpers;
using OpenTranslator.Models;
using OpenTranslator.Services;
using OpenTranslator.Services.Interfaces;
using OpenTranslator.ViewModels;

namespace OpenTranslator.Views;

/// <summary>
/// 主窗口 - 支持左右/上下布局切换的翻译界面
/// </summary>
public sealed class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly AppConfigService _configService;

    private TextBlock? _sourceTextBlock;
    private TextBlock? _resultTextBlock;
    private TextBlock? _statusTextBlock;
    private TextBlock? _modelStatusTextBlock;
    private Button? _sourceLangBtn;
    private Button? _targetLangBtn;
    private Button? _translateButton;
    private Button? _settingsButton;
    private MenuFlyout? _sourceLangFlyout;
    private MenuFlyout? _targetLangFlyout;
    private TextBlock? _charCountTextBlock;
    private Grid? _panelsGrid;
    private Border? _sourceBorder;
    private Border? _resultBorder;

    private LayoutMode _currentLayout;

    public MainWindow()
    {
        Title = "OpenTranslator - 智能翻译";

        try
        {
            var translationService = (TranslationService)App.Services.GetService(typeof(TranslationService))!;
            var modelManager = (IModelManager)App.Services.GetService(typeof(IModelManager))!;
            _configService = (AppConfigService)App.Services.GetService(typeof(AppConfigService))!;
            var hardwareDetector = (HardwareDetector)App.Services.GetService(typeof(HardwareDetector))!;
            var historyService = (ITranslationHistoryService?)App.Services.GetService(typeof(ITranslationHistoryService))
                ?? new TranslationHistoryService();

            _viewModel = new MainViewModel(
                translationService, modelManager, _configService, hardwareDetector, historyService);

            _currentLayout = _configService.GetConfig().MainWindowLayout;

            BuildUi();
            SubscribeViewModel();
            UpdateLanguageButtons();
            ApplyLayout(_currentLayout);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] 构造异常: {ex}");
            throw;
        }
    }

    private void BuildUi()
    {
        var rootGrid = new Grid
        {
            Background = new SolidColorBrush(Microsoft.UI.Colors.WhiteSmoke),
            Padding = new Thickness(24)
        };
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // ===== 第0行：顶部栏 =====
        var topBar = new Grid();
        topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleText = new TextBlock
        {
            Text = "OpenTranslator",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black)
        };
        Grid.SetColumn(titleText, 0);
        topBar.Children.Add(titleText);

        _modelStatusTextBlock = new TextBlock
        {
            Text = "模型未加载 | 就绪",
            FontSize = 12,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 16, 0)
        };
        Grid.SetColumn(_modelStatusTextBlock, 1);
        topBar.Children.Add(_modelStatusTextBlock);

        _settingsButton = new Button
        {
            Content = "⚙",
            Width = 36,
            Height = 36,
            FontSize = 16,
            CornerRadius = new CornerRadius(18),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _settingsButton.Click += OnSettingsClick;
        Grid.SetColumn(_settingsButton, 2);
        topBar.Children.Add(_settingsButton);

        Grid.SetRow(topBar, 0);
        topBar.Margin = new Thickness(0, 0, 0, 16);
        rootGrid.Children.Add(topBar);

        // ===== 第1行：语言选择栏 =====
        var langBar = new Grid();
        langBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        langBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        langBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _sourceLangBtn = CreateLanguageButton("自动检测");
        _sourceLangFlyout = CreateLanguageFlyout(lang =>
        {
            _viewModel.SourceLanguage = lang;
            UpdateLanguageButtons();
        }, includeAuto: true);
        _sourceLangBtn.Flyout = _sourceLangFlyout;
        Grid.SetColumn(_sourceLangBtn, 0);
        langBar.Children.Add(_sourceLangBtn);

        var swapBtn = new Button
        {
            Content = "⇄",
            Width = 44,
            Height = 40,
            FontSize = 16,
            Margin = new Thickness(12, 0, 12, 0),
            CornerRadius = new CornerRadius(20)
        };
        swapBtn.Click += (s, e) =>
        {
            _viewModel.SwapLanguages();
            UpdateLanguageButtons();
        };
        Grid.SetColumn(swapBtn, 1);
        langBar.Children.Add(swapBtn);

        _targetLangBtn = CreateLanguageButton("中文");
        _targetLangFlyout = CreateLanguageFlyout(lang =>
        {
            _viewModel.TargetLanguage = lang;
            UpdateLanguageButtons();
        }, includeAuto: false);
        _targetLangBtn.Flyout = _targetLangFlyout;
        Grid.SetColumn(_targetLangBtn, 2);
        langBar.Children.Add(_targetLangBtn);

        Grid.SetRow(langBar, 1);
        langBar.Margin = new Thickness(0, 0, 0, 12);
        rootGrid.Children.Add(langBar);

        // ===== 第2行：双面板容器（布局可切换） =====
        _panelsGrid = new Grid();

        // --- 原文输入面板 ---
        _sourceBorder = new Border
        {
            Background = new SolidColorBrush(Microsoft.UI.Colors.White),
            BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.LightGray),
            Padding = new Thickness(20)
        };

        var sourcePanel = new Grid();
        sourcePanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        sourcePanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sourcePanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var sourceScrollViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MinHeight = 200,
            MaxHeight = 500
        };

        _sourceTextBlock = new TextBlock
        {
            Text = "点击此处输入或粘贴要翻译的文本...",
            FontSize = 18,
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xA0, 0xA0, 0xA0)),
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true
        };
        var sourceClickableBorder = new Border
        {
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            Child = _sourceTextBlock
        };
        sourceClickableBorder.Tapped += (s, e) => ShowSourceEditorDialog();
        sourceScrollViewer.Content = sourceClickableBorder;
        Grid.SetRow(sourceScrollViewer, 0);
        sourcePanel.Children.Add(sourceScrollViewer);

        var sourceBottomBar = new Grid();
        sourceBottomBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        sourceBottomBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _charCountTextBlock = new TextBlock
        {
            Text = "0 字",
            FontSize = 12,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(_charCountTextBlock, 0);
        sourceBottomBar.Children.Add(_charCountTextBlock);

        var clearBtn = new Button
        {
            Content = "清空",
            FontSize = 12,
            Padding = new Thickness(12, 6, 12, 6),
            Height = 28
        };
        clearBtn.Click += (s, e) =>
        {
            _viewModel.ClearText();
            UpdateSourceDisplay();
            UpdateCharCount();
            UpdateTranslateButtonState();
        };
        Grid.SetColumn(clearBtn, 1);
        sourceBottomBar.Children.Add(clearBtn);

        Grid.SetRow(sourceBottomBar, 2);
        sourcePanel.Children.Add(sourceBottomBar);

        _sourceBorder.Child = sourcePanel;
        _panelsGrid.Children.Add(_sourceBorder);

        // --- 译文显示面板 ---
        _resultBorder = new Border
        {
            Background = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xF7, 0xF9, 0xFC)),
            BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.LightGray),
            Padding = new Thickness(20)
        };

        var resultPanel = new Grid();
        resultPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        resultPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        resultPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var resultScrollViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MinHeight = 200,
            MaxHeight = 500
        };

        _resultTextBlock = new TextBlock
        {
            Text = "译文将显示在这里",
            FontSize = 18,
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x60, 0x60, 0x60)),
            TextWrapping = TextWrapping.Wrap
        };
        resultScrollViewer.Content = _resultTextBlock;
        Grid.SetRow(resultScrollViewer, 0);
        resultPanel.Children.Add(resultScrollViewer);

        var resultBottomBar = new Grid();
        resultBottomBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        resultBottomBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var statusInPanel = new TextBlock
        {
            Text = string.Empty,
            FontSize = 12,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(statusInPanel, 0);
        resultBottomBar.Children.Add(statusInPanel);

        var copyResultBtn = new Button
        {
            Content = "复制译文",
            FontSize = 12,
            Padding = new Thickness(12, 6, 12, 6),
            Height = 28
        };
        copyResultBtn.Click += (s, e) => _viewModel.CopyResult();
        Grid.SetColumn(copyResultBtn, 1);
        resultBottomBar.Children.Add(copyResultBtn);

        Grid.SetRow(resultBottomBar, 2);
        resultPanel.Children.Add(resultBottomBar);

        _resultBorder.Child = resultPanel;
        _panelsGrid.Children.Add(_resultBorder);

        Grid.SetRow(_panelsGrid, 2);
        rootGrid.Children.Add(_panelsGrid);

        // ===== 第3行：底部栏 =====
        var bottomBar = new Grid();
        bottomBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bottomBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _statusTextBlock = new TextBlock
        {
            Text = "就绪",
            FontSize = 12,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.DarkGreen),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(_statusTextBlock, 0);
        bottomBar.Children.Add(_statusTextBlock);

        _translateButton = new Button
        {
            Content = "翻译",
            Width = 120,
            Height = 38,
            FontSize = 14,
            Background = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue),
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
            CornerRadius = new CornerRadius(6)
        };
        _translateButton.Click += OnTranslateClick;
        Grid.SetColumn(_translateButton, 1);
        bottomBar.Children.Add(_translateButton);

        Grid.SetRow(bottomBar, 3);
        bottomBar.Margin = new Thickness(0, 16, 0, 0);
        rootGrid.Children.Add(bottomBar);

        Content = rootGrid;

        UpdateTranslateButtonState();
        UpdateSourceDisplay();
        UpdateCharCount();
    }

    /// <summary>
    /// 应用布局模式
    /// </summary>
    public void ApplyLayout(LayoutMode layout)
    {
        if (_panelsGrid == null || _sourceBorder == null || _resultBorder == null) return;

        _panelsGrid.Children.Clear();
        _panelsGrid.RowDefinitions.Clear();
        _panelsGrid.ColumnDefinitions.Clear();

        switch (layout)
        {
            case LayoutMode.Horizontal:
                _panelsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                _panelsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                _sourceBorder.BorderThickness = new Thickness(1, 1, 0.5, 1);
                _sourceBorder.CornerRadius = new CornerRadius(12, 0, 0, 12);
                Grid.SetColumn(_sourceBorder, 0);
                Grid.SetRow(_sourceBorder, 0);

                _resultBorder.BorderThickness = new Thickness(0.5, 1, 1, 1);
                _resultBorder.CornerRadius = new CornerRadius(0, 12, 12, 0);
                Grid.SetColumn(_resultBorder, 1);
                Grid.SetRow(_resultBorder, 0);
                break;

            case LayoutMode.Vertical:
                _panelsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                _panelsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                _sourceBorder.BorderThickness = new Thickness(1, 1, 1, 0.5);
                _sourceBorder.CornerRadius = new CornerRadius(12, 12, 0, 0);
                Grid.SetColumn(_sourceBorder, 0);
                Grid.SetRow(_sourceBorder, 0);

                _resultBorder.BorderThickness = new Thickness(1, 0.5, 1, 1);
                _resultBorder.CornerRadius = new CornerRadius(0, 0, 12, 12);
                Grid.SetColumn(_resultBorder, 0);
                Grid.SetRow(_resultBorder, 1);
                break;
        }

        _panelsGrid.Children.Add(_sourceBorder);
        _panelsGrid.Children.Add(_resultBorder);

        _currentLayout = layout;
    }

    /// <summary>
    /// 切换布局
    /// </summary>
    public void ToggleLayout()
    {
        var newLayout = _currentLayout == LayoutMode.Horizontal
            ? LayoutMode.Vertical
            : LayoutMode.Horizontal;

        ApplyLayout(newLayout);

        _configService.UpdateConfig(c => c.MainWindowLayout = newLayout);
    }

    private Button CreateLanguageButton(string defaultText)
    {
        return new Button
        {
            Content = defaultText + "  ▾",
            Height = 40,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(16, 0, 16, 0)
        };
    }

    private MenuFlyout CreateLanguageFlyout(Action<LanguagePair> onSelected, bool includeAuto)
    {
        var flyout = new MenuFlyout();

        if (includeAuto)
        {
            var autoItem = new MenuFlyoutItem
            {
                Text = "自动检测",
                Tag = "auto"
            };
            autoItem.Click += (s, e) =>
            {
                var autoLang = _viewModel.Languages.FirstOrDefault(l => l.Code == "auto")
                    ?? new LanguagePair { Code = "auto", NameZh = "自动检测", NameEn = "Auto" };
                onSelected(autoLang);
            };
            flyout.Items.Add(autoItem);
            flyout.Items.Add(new MenuFlyoutSeparator());
        }

        foreach (var lang in _viewModel.Languages.Where(l => l.Code != "auto"))
        {
            var item = new MenuFlyoutItem
            {
                Text = $"{lang.NameZh}  ·  {lang.NameEn}",
                Tag = lang.Code
            };
            var langCapture = lang;
            item.Click += (s, e) => onSelected(langCapture);
            flyout.Items.Add(item);
        }
        return flyout;
    }

    private async void ShowSourceEditorDialog()
    {
        var dialog = new ContentDialog
        {
            Title = "编辑文本",
            Content = "TODO: 实现文本编辑",
            CloseButtonText = "关闭"
        };
        dialog.XamlRoot = Content.XamlRoot;
        await dialog.ShowAsync();
    }

    private void UpdateLanguageButtons()
    {
        if (_sourceLangBtn != null)
            _sourceLangBtn.Content = $"{_viewModel.SourceLanguage.NameZh}  ▾";
        if (_targetLangBtn != null)
            _targetLangBtn.Content = $"{_viewModel.TargetLanguage.NameZh}  ▾";
    }

    private void UpdateSourceDisplay()
    {
        if (_sourceTextBlock == null) return;

        if (string.IsNullOrWhiteSpace(_viewModel.SourceText))
        {
            _sourceTextBlock.Text = "点击此处输入或粘贴要翻译的文本...";
            _sourceTextBlock.Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xA0, 0xA0, 0xA0));
        }
        else
        {
            _sourceTextBlock.Text = _viewModel.SourceText;
            _sourceTextBlock.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black);
        }
    }

    private void UpdateCharCount()
    {
        if (_charCountTextBlock != null)
        {
            _charCountTextBlock.Text = $"{_viewModel.SourceText.Length} 字";
        }
    }

    private void UpdateResultDisplay()
    {
        if (_resultTextBlock == null) return;

        if (string.IsNullOrWhiteSpace(_viewModel.TranslatedText))
        {
            _resultTextBlock.Text = "译文将显示在这里";
            _resultTextBlock.Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x60, 0x60, 0x60));
        }
        else
        {
            _resultTextBlock.Text = _viewModel.TranslatedText;
            _resultTextBlock.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black);
        }
    }

    private void SubscribeViewModel()
    {
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.TranslatedText):
                UpdateResultDisplay();
                break;
            case nameof(MainViewModel.SourceText):
                UpdateSourceDisplay();
                UpdateCharCount();
                UpdateTranslateButtonState();
                break;
            case nameof(MainViewModel.IsTranslating):
                UpdateTranslateButtonState();
                UpdateModelStatusDisplay();
                break;
            case nameof(MainViewModel.StatusText):
                if (_statusTextBlock != null) _statusTextBlock.Text = _viewModel.StatusText;
                UpdateModelStatusDisplay();
                break;
            case nameof(MainViewModel.EngineStatus):
            case nameof(MainViewModel.EngineStatusDisplay):
                UpdateModelStatusDisplay();
                break;
        }
    }

    private void UpdateModelStatusDisplay()
    {
        if (_modelStatusTextBlock == null) return;

        var status = _viewModel.EngineStatus;
        string loadingHint = _viewModel.IsTranslating ? " | 翻译中..." : "";
        var text = $"{_viewModel.EngineStatusDisplay} | {_viewModel.StatusText}{loadingHint}";
        _modelStatusTextBlock.Text = text;

        _modelStatusTextBlock.Foreground = status switch
        {
            EngineStatus.Ready => new SolidColorBrush(Microsoft.UI.Colors.DarkGreen),
            EngineStatus.Loading => new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue),
            EngineStatus.Error => new SolidColorBrush(Microsoft.UI.Colors.Crimson),
            _ => new SolidColorBrush(Microsoft.UI.Colors.Gray)
        };
    }

    private void UpdateTranslateButtonState()
    {
        if (_translateButton == null) return;
        _translateButton.IsEnabled = !_viewModel.IsTranslating
            && !string.IsNullOrWhiteSpace(_viewModel.SourceText);

        _translateButton.Content = _viewModel.IsTranslating ? "翻译中..." : "翻译";
    }

    private async void OnTranslateClick(object sender, RoutedEventArgs e)
    {
        await _viewModel.TranslateAsync();
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(_configService, this);
        settingsWindow.Activate();
    }
}
