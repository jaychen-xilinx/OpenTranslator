using OpenTranslator.Models;

namespace OpenTranslator.Helpers;

/// <summary>
/// 全局常量定义（基于 Hy-MT2 官方文档 2026.5.21）
/// 参见: https://github.com/Tencent-Hunyuan/Hy-MT2
/// </summary>
public static class Constants
{
    public const string AppName = "OpenTranslator";
    public const string AppVersion = "0.1.0";

    public const string ModelsFolder = "models";
    public const string DataFolder = "data";
    public const string ConfigFileName = "config.json";

    public const int MaxHistoryItems = 500;
    public const int MaxCacheItems = 1000;

    // llama.cpp STQ 内核 PR（Hy-MT2 GGUF 必须使用此分支编译）
    public const string LlamaCppPrBranch = "pull/22836/head:pr-stq";
    public const string LlamaCppRepo = "https://github.com/ggml-org/llama.cpp.git";

    // HuggingFace 国内镜像
    public const string HfMirror = "https://hf-mirror.com";

    // 模型定义（基于 Hy-MT2 官方 GGUF 发布）
    public static readonly ModelInfo[] PredefinedModels =
    [
        // === 1.8B 系列（推荐首选） ===
        new()
        {
            Name = "Hy-MT2-1.8B-Q4_K_M",
            DisplayName = "HY-MT2 1.8B Q4_K_M (推荐)",
            FileName = "Hy-MT2-1.8B-Q4_K_M.gguf",
            FileSizeBytes = 1_500_000_000L,
            DownloadUrl = "https://huggingface.co/tencent/Hy-MT2-1.8B-GGUF/resolve/main/Hy-MT2-1.8B-Q4_K_M.gguf",
            ParametersCount = "1.8B",
            RequiredVRamMB = 2048,
            RequiredRamMB = 8192
        },
        new()
        {
            Name = "Hy-MT2-1.8B-2bit",
            DisplayName = "HY-MT2 1.8B 2bit (574MB)",
            FileName = "Hy-MT2-1.8B-2bit.gguf",
            FileSizeBytes = 574_000_000L,
            DownloadUrl = "https://huggingface.co/tencent/Hy-MT2-1.8B-2bit-GGUF/resolve/main/Hy-MT2-1.8B-2bit.gguf",
            ParametersCount = "1.8B",
            RequiredVRamMB = 1024,
            RequiredRamMB = 4096
        },
        new()
        {
            Name = "Hy-MT2-1.8B-1.25bit",
            DisplayName = "HY-MT2 1.8B 1.25bit (440MB)",
            FileName = "Hy-MT2-1.8B-1.25bit.gguf",
            FileSizeBytes = 440_000_000L,
            DownloadUrl = "https://huggingface.co/tencent/Hy-MT2-1.8B-1.25bit-GGUF/resolve/main/Hy-MT2-1.8B-1.25bit.gguf",
            ParametersCount = "1.8B",
            RequiredVRamMB = 512,
            RequiredRamMB = 4096
        },

        // === 7B 系列（高质量） ===
        new()
        {
            Name = "Hy-MT2-7B-Q4_K_M",
            DisplayName = "HY-MT2 7B Q4_K_M (高质量)",
            FileName = "Hy-MT2-7B-Q4_K_M.gguf",
            FileSizeBytes = 3_800_000_000L,
            DownloadUrl = "https://huggingface.co/tencent/Hy-MT2-7B-GGUF/resolve/main/Hy-MT2-7B-Q4_K_M.gguf",
            ParametersCount = "7B",
            RequiredVRamMB = 4096,
            RequiredRamMB = 16384
        },

        // === 30B MoE 系列（服务端，可选） ===
        new()
        {
            Name = "Hy-MT2-30B-A3B",
            DisplayName = "HY-MT2 30B-A3B (MoE, 服务端)",
            FileName = "Hy-MT2-30B-A3B.gguf",
            FileSizeBytes = 16_000_000_000L,
            DownloadUrl = "https://huggingface.co/tencent/Hy-MT2-30B-A3B-GGUF/resolve/main/Hy-MT2-30B-A3B.gguf",
            ParametersCount = "30B-A3B",
            RequiredVRamMB = 16384,
            RequiredRamMB = 32768
        }
    ];

    // Hy-MT2 推荐推理参数
    public const float DefaultTemperature = 0.7f;
    public const float DefaultTopP = 0.6f;
    public const int DefaultTopK = 20;
    public const float DefaultRepetitionPenalty = 1.05f;
    public const int DefaultMaxTokens = 4096;

    // 37种支持语言（Hy-MT2 官方列表）
    // 语言代码 + 完整中文名称（用于提示词中的 target_lang 占位符）
    public static readonly LanguagePair[] SupportedLanguages =
    [
        new() { Code = "auto", NameZh = "自动检测", NameEn = "Auto Detect" },
        new() { Code = "zh",      NameZh = "中文",           NameEn = "Chinese" },
        new() { Code = "en",      NameZh = "英语",           NameEn = "English" },
        new() { Code = "fr",      NameZh = "法语",           NameEn = "French" },
        new() { Code = "pt",      NameZh = "葡萄牙语",       NameEn = "Portuguese" },
        new() { Code = "es",      NameZh = "西班牙语",       NameEn = "Spanish" },
        new() { Code = "ja",      NameZh = "日语",           NameEn = "Japanese" },
        new() { Code = "tr",      NameZh = "土耳其语",       NameEn = "Turkish" },
        new() { Code = "ru",      NameZh = "俄语",           NameEn = "Russian" },
        new() { Code = "ar",      NameZh = "阿拉伯语",       NameEn = "Arabic" },
        new() { Code = "ko",      NameZh = "韩语",           NameEn = "Korean" },
        new() { Code = "th",      NameZh = "泰语",           NameEn = "Thai" },
        new() { Code = "it",      NameZh = "意大利语",       NameEn = "Italian" },
        new() { Code = "de",      NameZh = "德语",           NameEn = "German" },
        new() { Code = "vi",      NameZh = "越南语",         NameEn = "Vietnamese" },
        new() { Code = "ms",      NameZh = "马来语",         NameEn = "Malay" },
        new() { Code = "id",      NameZh = "印尼语",         NameEn = "Indonesian" },
        new() { Code = "tl",      NameZh = "菲律宾语",       NameEn = "Filipino" },
        new() { Code = "hi",      NameZh = "印地语",         NameEn = "Hindi" },
        new() { Code = "zh-Hant", NameZh = "繁体中文",       NameEn = "Traditional Chinese" },
        new() { Code = "pl",      NameZh = "波兰语",         NameEn = "Polish" },
        new() { Code = "cs",      NameZh = "捷克语",         NameEn = "Czech" },
        new() { Code = "nl",      NameZh = "荷兰语",         NameEn = "Dutch" },
        new() { Code = "km",      NameZh = "高棉语",         NameEn = "Khmer" },
        new() { Code = "my",      NameZh = "缅甸语",         NameEn = "Burmese" },
        new() { Code = "fa",      NameZh = "波斯语",         NameEn = "Persian" },
        new() { Code = "gu",      NameZh = "古吉拉特语",     NameEn = "Gujarati" },
        new() { Code = "ur",      NameZh = "乌尔都语",       NameEn = "Urdu" },
        new() { Code = "te",      NameZh = "泰卢固语",       NameEn = "Telugu" },
        new() { Code = "mr",      NameZh = "马拉地语",       NameEn = "Marathi" },
        new() { Code = "he",      NameZh = "希伯来语",       NameEn = "Hebrew" },
        new() { Code = "bn",      NameZh = "孟加拉语",       NameEn = "Bengali" },
        new() { Code = "ta",      NameZh = "泰米尔语",       NameEn = "Tamil" },
        new() { Code = "uk",      NameZh = "乌克兰语",       NameEn = "Ukrainian" },
        new() { Code = "bo",      NameZh = "藏语",           NameEn = "Tibetan" },
        new() { Code = "kk",      NameZh = "哈萨克语",       NameEn = "Kazakh" },
        new() { Code = "mn",      NameZh = "蒙古语",         NameEn = "Mongolian" },
        new() { Code = "ug",      NameZh = "维吾尔语",       NameEn = "Uyghur" },
        new() { Code = "yue",     NameZh = "粤语",           NameEn = "Cantonese" },
    ];

    /// <summary>生成 ModelScope 镜像下载地址（国内加速）</summary>
    public static string GetModelScopeUrl(string hfModelId, string fileName)
    {
        var hfRepo = hfModelId.Split('/')[^1];
        return $"https://modelscope.cn/models/Tencent-Hunyuan/{hfRepo}/resolve/main/{fileName}";
    }

    /// <summary>生成 hf-mirror 镜像下载地址（国内加速）</summary>
    public static string GetHfMirrorUrl(string hfUrl)
    {
        return hfUrl.Replace("https://huggingface.co/", $"{HfMirror}/");
    }
}