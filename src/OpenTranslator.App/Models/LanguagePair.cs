namespace OpenTranslator.Models;

/// <summary>
/// 语言对信息
/// </summary>
public class LanguagePair
{
    /// <summary>语言代码 (ISO 639-1)</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>语言中文名称</summary>
    public string NameZh { get; set; } = string.Empty;

    /// <summary>语言英文名称</summary>
    public string NameEn { get; set; } = string.Empty;
}