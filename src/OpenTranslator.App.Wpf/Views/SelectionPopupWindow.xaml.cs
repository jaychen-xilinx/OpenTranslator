using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OpenTranslator.Helpers;
using OpenTranslator.Models;
using OpenTranslator.Services;
using Microsoft.Extensions.DependencyInjection;

namespace OpenTranslator.App.Wpf.Views;

public partial class SelectionPopupWindow : Window
{
    private string _sourceText = string.Empty;
    private string _targetLangCode = "zh";
    private TranslationService? _translationService;
    private bool _isClosing;

    public SelectionPopupWindow()
    {
        InitializeComponent();
        MouseLeftButtonDown += (s, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        };

        Loaded += SelectionPopupWindow_Loaded;
    }

    private void SelectionPopupWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _translationService = App.Services.GetRequiredService<TranslationService>();
        }
        catch { }

        SourceLangComboBox.ItemsSource = Constants.SupportedLanguages;
        TargetLangComboBox.ItemsSource = Constants.SupportedLanguages;

        var autoLang = Constants.SupportedLanguages.First(l => l.Code == "auto");
        var targetLang = Constants.SupportedLanguages.FirstOrDefault(l => l.Code == _targetLangCode) ?? Constants.SupportedLanguages.First(l => l.Code == "zh");

        SourceLangComboBox.SelectedItem = autoLang;
        TargetLangComboBox.SelectedItem = targetLang;
    }

    public void Show(string sourceText, string resultText, long? inferenceTimeMs)
    {
        _sourceText = sourceText;
        SourceTextBlock.Text = sourceText;
        ResultTextBlock.Text = resultText;

        if (inferenceTimeMs.HasValue)
            TimeTextBlock.Text = $"耗时 {inferenceTimeMs.Value}ms";
        else
            TimeTextBlock.Text = string.Empty;

        PositionNearMouse();
        Show();
        Activate();
    }

    private async void SourceLangComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SourceLangComboBox.SelectedItem is LanguagePair lang && !string.IsNullOrWhiteSpace(_sourceText))
        {
            await RetranslateAsync();
        }
    }

    private async void TargetLangComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TargetLangComboBox.SelectedItem is LanguagePair lang)
        {
            _targetLangCode = lang.Code;
            if (!string.IsNullOrWhiteSpace(_sourceText))
            {
                await RetranslateAsync();
            }
        }
    }

    private async Task RetranslateAsync()
    {
        if (_translationService == null || SourceLangComboBox.SelectedItem is not LanguagePair sourceLang || TargetLangComboBox.SelectedItem is not LanguagePair targetLang)
            return;

        try
        {
            ResultTextBlock.Text = "翻译中...";
            TimeTextBlock.Text = string.Empty;

            var result = await _translationService.TranslateAsync(
                _sourceText,
                sourceLang.Code,
                targetLang.Code);

            ResultTextBlock.Text = result.TranslatedText;
            TimeTextBlock.Text = $"耗时 {result.InferenceTimeMs}ms";
        }
        catch (Exception ex)
        {
            ResultTextBlock.Text = $"翻译失败: {ex.Message}";
        }
    }

    private void PositionNearMouse()
    {
        var mousePos = Mouse.GetPosition(null);
        var dpiScale = VisualTreeHelper.GetDpi(this);
        double screenX = mousePos.X * dpiScale.DpiScaleX;
        double screenY = mousePos.Y * dpiScale.DpiScaleY;

        double left = screenX + 10;
        double top = screenY + 10;

        double screenWidth = SystemParameters.PrimaryScreenWidth;
        double screenHeight = SystemParameters.PrimaryScreenHeight;

        if (left + Width > screenWidth)
            left = screenWidth - Width - 10;
        if (top + Height > screenHeight)
            top = screenHeight - Height - 10;

        Left = left / dpiScale.DpiScaleX;
        Top = top / dpiScale.DpiScaleY;
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(ResultTextBlock.Text))
        {
            Clipboard.SetText(ResultTextBlock.Text);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _isClosing = true;
        base.OnClosing(e);
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        if (!_isClosing)
            Close();
    }
}
