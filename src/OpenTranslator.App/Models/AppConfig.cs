namespace OpenTranslator.Models;

/// <summary>
/// 主窗口布局模式
/// </summary>
public enum LayoutMode
{
    Horizontal,
    Vertical
}

/// <summary>
/// 应用配置
/// </summary>
public class AppConfig
{
    public string DefaultModel { get; set; } = "Hy-MT2-1.8B";
    public string SourceLanguage { get; set; } = "auto";
    public string TargetLanguage { get; set; } = "zh";
    public bool AutoDetectLanguage { get; set; } = true;
    public string Theme { get; set; } = "System";
    public bool StartMinimized { get; set; }
    public HotKeyConfig HotKeys { get; set; } = new();
    public string ModelsDirectory { get; set; } = "models";
    public LayoutMode MainWindowLayout { get; set; } = LayoutMode.Horizontal;
}

public class HotKeyConfig
{
    public string TranslateHotKey { get; set; } = "Alt+Q";
    public string ReplaceHotKey { get; set; } = "Ctrl+Shift+T";
    public string ScreenshotHotKey { get; set; } = "Ctrl+Shift+O";
    public string DictionaryHotKey { get; set; } = "Ctrl+D";
}