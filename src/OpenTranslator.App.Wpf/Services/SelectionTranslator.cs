using System.Windows;
using OpenTranslator.Helpers;
using OpenTranslator.Models;

namespace OpenTranslator.Services;

/// <summary>
/// 划词翻译服务 - 通过全局热键触发，负责获取选中文本、执行翻译、显示悬浮结果窗
///
/// 注意：全局快捷键统一由 HotKeyService 注册，本类暴露 TranslateSelectionAsync / ReplaceSelectionAsync
///       供 HotKeyService 的事件处理调用。
/// </summary>
public class SelectionTranslator
{
    private readonly TranslationService _translationService;
    private readonly AppConfigService _configService;

    /// <summary>翻译完成事件</summary>
    public event EventHandler<TranslationResult>? TranslationCompleted;

    /// <summary>快捷键触发事件（携带选中文本）</summary>
    public event EventHandler<string>? HotKeyTriggered;

    /// <summary>请求显示悬浮窗事件</summary>
    public event EventHandler<(string title, string message, long? timeMs)>? ShowPopupRequested;

    /// <summary>请求获取选中文本事件（在 UI 线程触发）</summary>
    public event Func<string?>? GetSelectionRequested;

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
            var config = _configService.GetConfig();
            
            var selectedText = GetSelectionRequested?.Invoke() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(selectedText))
            {
                ShowPopup("未检测到选中文本", $"请先选中文本后按 {config.HotKeys.TranslateHotKey}", null);
                return;
            }

            HotKeyTriggered?.Invoke(this, selectedText);

            var result = await _translationService.TranslateAsync(
                selectedText,
                "auto",
                config.TargetLanguage);

            ShowPopup(result.SourceText, result.TranslatedText, result.InferenceTimeMs);
            TranslationCompleted?.Invoke(this, result);
        }
        catch (Exception ex)
        {
            ShowPopup("翻译失败", ex.Message, null);
        }
    }

    /// <summary>
    /// 触发原地替换翻译：获取选中文本、翻译并粘贴覆盖
    /// </summary>
    public async Task ReplaceSelectionAsync()
    {
        try
        {
            var config = _configService.GetConfig();
            
            var selectedText = GetSelectionRequested?.Invoke() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(selectedText))
            {
                ShowPopup("未检测到选中文本", $"请先选中文本后按 {config.HotKeys.ReplaceHotKey}", null);
                return;
            }

            HotKeyTriggered?.Invoke(this, selectedText);

            var result = await _translationService.TranslateAsync(
                selectedText,
                "auto",
                config.TargetLanguage);

            Application.Current?.Dispatcher.Invoke(() =>
            {
                try { Clipboard.SetText(result.TranslatedText); } catch { }
            });
            await Task.Delay(100);
            SimulateCtrlPlusKey(0x56);
        }
        catch (Exception ex)
        {
            ShowPopup("替换失败", ex.Message, null);
        }
    }

    private static void SimulateCtrlPlusKey(byte vkKey)
    {
        NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0, 0, UIntPtr.Zero);
        Thread.Sleep(10);
        NativeMethods.keybd_event(vkKey, 0, 0, UIntPtr.Zero);
        Thread.Sleep(10);
        NativeMethods.keybd_event(vkKey, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        Thread.Sleep(10);
        NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    private void ShowPopup(string title, string message, long? timeMs)
    {
        ShowPopupRequested?.Invoke(this, (title, message, timeMs));
    }
}