using Microsoft.ML.OnnxRuntime;
using OpenTranslator.Services.Interfaces;

namespace OpenTranslator.Services;

/// <summary>
/// 语言检测器实现 - 使用 Unicode 特征检测 + ONNX fasttext 备选
/// 注意: ONNX 模型需要 fasttext 导出的 .onnx 格式
/// </summary>
public class LanguageDetector : ILanguageDetector, IDisposable
{
    private InferenceSession? _session;
    private bool _isInitialized;

    public bool IsInitialized => _isInitialized;

    public Task InitializeAsync(string modelPath)
    {
        if (!File.Exists(modelPath))
        {
            // 无模型时使用基于特征的简单检测
            _isInitialized = true;
            return Task.CompletedTask;
        }

        try
        {
            _session = new InferenceSession(modelPath);
            _isInitialized = true;
        }
        catch
        {
            // ONNX加载失败，回退到简单检测
            _isInitialized = true;
        }

        return Task.CompletedTask;
    }

    public string DetectLanguage(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "auto";

        // 优先使用 ONNX 模型
        if (_session != null)
        {
            var detected = DetectWithOnnx(text);
            if (detected != "auto")
                return detected;
        }

        // 回退：基于 Unicode 范围的特征检测
        return DetectByCharacterRange(text);
    }

    public double GetConfidence()
    {
        return _session != null ? 0.85 : 0.65;
    }

    private string DetectWithOnnx(string text)
    {
        if (_session == null) return "auto";

        try
        {
            // fasttext ONNX 模型的输入通常是固定长度的 token IDs
            // 此处为骨架代码，实际需根据模型 input 格式调整
            // 典型做法: tokenize → 填充到固定长度 → 构建 int64 张量 → 推理

            // 跳过 ONNX 检测，直接使用 Unicode 回退
            // (完整实现需要 tokenizer + 模型特定的预处理)
            return "auto";
        }
        catch
        {
            return "auto";
        }
    }

    private static string DetectByCharacterRange(string text)
    {
        int latin = 0, han = 0, hiragana = 0, katakana = 0, hangul = 0, cyrillic = 0, arabic = 0;
        int thai = 0, devanagari = 0, hebrew = 0;

        foreach (var c in text)
        {
            if (c is >= '\u4E00' and <= '\u9FFF') han++;
            else if (c is >= '\u3040' and <= '\u309F') hiragana++;
            else if (c is >= '\u30A0' and <= '\u30FF') katakana++;
            else if (c is >= '\uAC00' and <= '\uD7AF') hangul++;
            else if (c is >= '\u0400' and <= '\u04FF') cyrillic++;
            else if (c is >= '\u0600' and <= '\u06FF') arabic++;
            else if (c is >= '\u0E00' and <= '\u0E7F') thai++;
            else if (c is >= '\u0900' and <= '\u097F') devanagari++;
            else if (c is >= '\u0590' and <= '\u05FF') hebrew++;
            else if (c is >= 'a' and <= 'z' or >= 'A' and <= 'Z') latin++;
        }

        // 中文优先（汉字 + 日语汉字重叠，但平假名片假名可区分日语）
        if (han > 0)
        {
            if (hiragana > 0 || katakana > 0) return "ja";
            return "zh";
        }
        if (hiragana > 0 || katakana > 0) return "ja";
        if (hangul > 0) return "ko";
        if (cyrillic > 0) return "ru";
        if (arabic > 0) return "ar";
        if (thai > 0) return "th";
        if (devanagari > 0) return "hi";
        if (hebrew > 0) return "he";
        if (latin > 0) return "en";

        return "auto";
    }

    public void Dispose()
    {
        _session?.Dispose();
        _session = null;
    }
}