using OpenTranslator.Models;

namespace OpenTranslator.Helpers;

/// <summary>
/// 翻译提示词构建器（基于 Hy-MT2 官方提示词模板 + Chat Template）
/// 参见: https://github.com/Tencent-Hunyuan/Hy-MT2
///
/// 注意: Hy-MT2 不同大小的模型使用不同的 chat template：
///   - 1.8B / 0.5B / 4B (Dense): 使用 hy_dense_1_8b 模板
///   - 7B / 30B (Dense): 使用 hy_dense_7b 模板
///   - 7B / 30B-A3B (MoE): 使用 hy_v3 模板
/// </summary>
public static class PromptBuilder
{
    // ========== 模板类型 ==========
    public enum ModelTemplateType
    {
        Dense18B,
        Dense7B,
        V3MoE
    }

    // ========== Dense 1.8B/0.5B/4B 模板 ==========
    // 参考: Hy-MT2-main/train/llama_factory_support/hy_dense_template.py
    public const string Dense18B_BosToken = "<｜hy_begin▁of▁sentence｜>";
    public const string Dense18B_UserPrefix = "<｜hy_User｜>";
    public const string Dense18B_AssistantPrefix = "<｜hy_Assistant｜>";
    public const string Dense18B_StopToken = "<｜hy_place▁holder▁no▁2｜>";

    // ========== Dense 7B 模板 ==========
    // 参考: Hy-MT2-main/train/llama_factory_support/hy_dense_template.py
    public const string Dense7B_BosToken = "<|startoftext|>";
    public const string Dense7B_UserSuffix = "<|extra_0|>";
    public const string Dense7B_StopToken = "<|eos|>";

    // ========== V3 MoE (7B/30B-A3B) 模板 ==========
    // 参考: Hy-MT2-main/train/llama_factory_support/hy_v3_template.py
    public const string V3_BosToken = "<｜hy_begin▁of▁sentence｜>";
    public const string V3_UserPrefix = "<｜hy_User｜>";
    public const string V3_AssistantPrefix = "<｜hy_Assistant｜>";
    public const string V3_StopToken = "<｜hy_eos｜>";

    // ========== 当前使用的模板（默认 Dense 1.8B） ==========
    private static ModelTemplateType _currentTemplate = ModelTemplateType.Dense18B;

    public static void SetModelTemplate(ModelTemplateType templateType)
    {
        _currentTemplate = templateType;
    }

    /// <summary>根据模型名称自动推断模板类型</summary>
    public static ModelTemplateType DetectTemplateFromModelName(string modelName)
    {
        var lower = modelName.ToLowerInvariant();
        if (lower.Contains("7b") && !lower.Contains("moe") && !lower.Contains("a3b"))
            return ModelTemplateType.Dense7B;
        if (lower.Contains("30b") || lower.Contains("moe") || lower.Contains("a3b") || lower.Contains("v3"))
            return ModelTemplateType.V3MoE;
        return ModelTemplateType.Dense18B;
    }

    /// <summary>获取当前模板的停止词列表</summary>
    public static string[] GetStopTokens()
    {
        return _currentTemplate switch
        {
            ModelTemplateType.Dense18B => [Dense18B_StopToken, "</s>", "<|endoftext|>"],
            ModelTemplateType.Dense7B => [Dense7B_StopToken, "</s>", "<|endoftext|>"],
            ModelTemplateType.V3MoE => [V3_StopToken, "</s>", "<|endoftext|>"],
            _ => [Dense18B_StopToken, "</s>"]
        };
    }

    /// <summary>使用当前 chat template 包装用户消息</summary>
    public static string WrapChatTemplate(string userMessage)
    {
        return _currentTemplate switch
        {
            ModelTemplateType.Dense18B => $"{Dense18B_BosToken}{Dense18B_UserPrefix}{userMessage}{Dense18B_AssistantPrefix}",
            ModelTemplateType.Dense7B => $"{Dense7B_BosToken}{userMessage}{Dense7B_UserSuffix}",
            ModelTemplateType.V3MoE => $"{V3_BosToken}{V3_UserPrefix}{userMessage}{V3_AssistantPrefix}",
            _ => $"{Dense18B_BosToken}{Dense18B_UserPrefix}{userMessage}{Dense18B_AssistantPrefix}"
        };
    }
    // ========== 基础翻译提示词（Hy-MT2 官方格式） ==========

    // 中文默认翻译格式
    // 官方格式: 将以下文本翻译为 {target_lang}，注意只需要输出翻译后的结果，不要额外解释：
    private const string DefaultTranslateZh = "将以下文本翻译为 {0}，注意只需要输出翻译后的结果，不要额外解释：\n{1}";

    // 英文默认翻译格式
    // 官方格式: Translate the following text into {target_lang}. Note that you should only output the translated result without any additional explanation:
    private const string DefaultTranslateEn = "Translate the following text into {0}. Note that you should only output the translated result without any additional explanation:\n{1}";

    // ========== 术语表翻译（Hy-MT2 官方格式） ==========

    // 中文术语表格式
    // 官方格式:
    //   参考下面的翻译：
    //   {text} 翻译成 {text}
    //   将以下文本翻译为 {target_lang}，注意只需要输出翻译后的结果，不要额外解释：
    private const string GlossaryZh = "参考下面的翻译：\n{0}\n将以下文本翻译为 {1}，注意只需要输出翻译后的结果，不要额外解释：\n{2}";

    // 英文术语表格式
    private const string GlossaryEn = "Reference the following translations:\n{0}\nTranslate the following text into {1}. Note that you must only output the translated result without any additional explanation:\n{2}";

    // ========== 风格控制翻译（Hy-MT2 官方格式） ==========

    // 中文风格控制格式
    // 官方格式: 请将以下文本翻译为 {target_lang}。注意翻译的风格要严格符合【{target_style}】
    private const string StyleZh = "请将以下文本翻译为 {0}。注意翻译的风格要严格符合【{1}】\n{2}";

    // 英文风格控制格式
    private const string StyleEn = "Please translate the following text into {0}. Note that the translation style must strictly conform to [{1}]:\n{2}";

    // ========== 分隔符保持翻译（Hy-MT2 官方格式） ==========

    // 中文分隔符格式
    // 官方格式: 请将以下文本准确翻译为 {target_lang}。你必须在译文中保留等量的分隔符，绝对不可遗漏、转义或翻译该符号，并注意分隔符的位置。
    private const string DelimitersZh = "请将以下文本准确翻译为 {0}。你必须在译文中保留等量的分隔符，绝对不可遗漏、转义或翻译该符号，并注意分隔符的位置。\n{1}";

    // ========== 结构化数据翻译（Hy-MT2 官方格式） ==========

    // 中文结构化数据格式
    // 官方格式: 将 source_text 中的 format_type 格式数据翻译为 target_lang。
    //          1. 结构锁定：绝对保持原有数据结构、缩进和层级完全不变。
    //          2. 选择性翻译：仅翻译面向用户展示的可见文本内容。
    //          3. 禁止修改：严禁翻译或更改任何代码标签、键名、变量占位符或代码属性。
    private const string StructuredZh = "# 任务目标\n将下方 {0} 中的 {1} 格式数据翻译为 {2}。\n# 严格约束\n1. **结构锁定**：绝对保持原有的 {1} 数据结构、缩进和层级完全不变。\n2. **选择性翻译**：仅翻译面向用户展示的可见文本内容。\n3. **禁止修改**：**严禁**翻译或更改任何代码标签、键名 (Key)、变量占位符（如 `{{var}}`、`${{var}}`、`%s`、`%d` 等）或代码属性。\n# 数据输入\n{3}";

    // ========== 增强功能提示词 ==========

    // 摘要
    private const string SummarizeZh = "请将以下文本进行摘要，控制在 {0} 字以内，只输出摘要结果：\n{1}";

    // 重写
    private const string RewriteZh = "请将以下文本改写成{0}的风格，只输出改写后的结果：\n{1}";

    // 润色
    private const string PolishZh = "请润色以下文本，使其更加流畅自然，只输出润色后的结果：\n{0}";

    // 反向翻译质量检测
    private const string ReverseCheckZh = "将以下译文翻译回{0}，只输出回译结果：\n{1}";

    // ========== 公共构建方法 ==========

    /// <summary>构建默认翻译提示词（包含 chat template 包装）</summary>
    public static string BuildTranslationPrompt(string text, string sourceLang, string targetLang,
        Dictionary<string, string>? glossary = null)
    {
        var targetName = GetLanguageNameZh(targetLang);
        string userMessage;

        if (glossary is { Count: > 0 })
        {
            var glossaryItems = string.Join("\n", glossary.Select(kv => $"{kv.Key} 翻译成 {kv.Value}"));
            userMessage = string.Format(GlossaryZh, glossaryItems, targetName, text);
        }
        else
        {
            userMessage = string.Format(DefaultTranslateZh, targetName, text);
        }

        return WrapChatTemplate(userMessage);
    }

    /// <summary>构建风格控制翻译提示词</summary>
    public static string BuildStyleTranslationPrompt(string text, string targetLang, string style)
    {
        var targetName = GetLanguageNameZh(targetLang);
        return string.Format(StyleZh, targetName, style, text);
    }

    /// <summary>构建分隔符保持翻译提示词</summary>
    public static string BuildDelimiterTranslationPrompt(string text, string targetLang)
    {
        var targetName = GetLanguageNameZh(targetLang);
        return string.Format(DelimitersZh, targetName, text);
    }

    /// <summary>构建结构化翻译提示词</summary>
    /// <param name="contextLabel">上下文标签，如"【待翻译文本】"</param>
    /// <param name="formatType">格式类型，如"HTML"、"JSON"、"代码"</param>
    public static string BuildStructuredPrompt(string text, string targetLang,
        string formatType = "文本", string contextLabel = "【待翻译文本】")
    {
        var targetName = GetLanguageNameZh(targetLang);
        return string.Format(StructuredZh, contextLabel, formatType, targetName, text);
    }

    /// <summary>构建摘要提示词</summary>
    public static string BuildSummarizePrompt(string text, int maxLength = 100)
        => string.Format(SummarizeZh, maxLength, text);

    /// <summary>构建重写提示词</summary>
    public static string BuildRewritePrompt(string text, string style = "正式")
    {
        var styleDesc = style switch
        {
            "formal" => "正式、专业",
            "informal" => "口语化、随意",
            "literary" => "文学、优美",
            _ => "自然流畅"
        };
        return string.Format(RewriteZh, styleDesc, text);
    }

    /// <summary>构建润色提示词</summary>
    public static string BuildPolishPrompt(string text)
        => string.Format(PolishZh, text);

    /// <summary>构建反向翻译提示词（用于翻译质量校验）</summary>
    public static string BuildReverseCheckPrompt(string translatedText, string sourceLang)
    {
        var sourceName = GetLanguageNameZh(sourceLang);
        return string.Format(ReverseCheckZh, sourceName, translatedText);
    }

    /// <summary>获取语言完整中文名称（用于提示词中 target_lang 占位符）</summary>
    private static string GetLanguageNameZh(string code)
    {
        var lang = Constants.SupportedLanguages.FirstOrDefault(l => l.Code == code);
        return lang?.NameZh ?? code;
    }
}