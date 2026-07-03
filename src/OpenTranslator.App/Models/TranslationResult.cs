namespace OpenTranslator.Models;

/// <summary>
/// 翻译结果
/// </summary>
public class TranslationResult
{
    /// <summary>翻译后的文本</summary>
    public string TranslatedText { get; set; } = string.Empty;

    /// <summary>源语言代码</summary>
    public string SourceLanguage { get; set; } = string.Empty;

    /// <summary>目标语言代码</summary>
    public string TargetLanguage { get; set; } = string.Empty;

    /// <summary>原始文本</summary>
    public string SourceText { get; set; } = string.Empty;

    /// <summary>推理耗时（毫秒）</summary>
    public long InferenceTimeMs { get; set; }

    /// <summary>使用的模型名称</summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>翻译时间戳</summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;
}