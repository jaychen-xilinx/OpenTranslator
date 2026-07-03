namespace OpenTranslator.Services.Interfaces;

/// <summary>
/// 语言检测器接口 - 自动检测文本语言
/// </summary>
public interface ILanguageDetector
{
    /// <summary>初始化检测模型</summary>
    Task InitializeAsync(string modelPath);

    /// <summary>检测文本语言</summary>
    string DetectLanguage(string text);

    /// <summary>获取检测置信度</summary>
    double GetConfidence();

    /// <summary>是否已初始化</summary>
    bool IsInitialized { get; }
}