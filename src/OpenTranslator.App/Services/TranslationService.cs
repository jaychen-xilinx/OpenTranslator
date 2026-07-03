using OpenTranslator.Helpers;
using OpenTranslator.Models;
using OpenTranslator.Services.Interfaces;

namespace OpenTranslator.Services;

/// <summary>
/// 翻译服务 - 协调翻译流程的业务逻辑层
/// </summary>
public class TranslationService
{
    private readonly ITranslationEngine _engine;
    private readonly ILanguageDetector _languageDetector;
    private readonly AppConfigService _configService;
    private readonly TranslationCache _cache;

    public TranslationService(
        ITranslationEngine engine,
        ILanguageDetector languageDetector,
        AppConfigService configService,
        TranslationCache? cache = null)
    {
        _engine = engine;
        _languageDetector = languageDetector;
        _configService = configService;
        _cache = cache ?? new TranslationCache(Constants.MaxCacheItems);
    }

    /// <summary>执行翻译</summary>
    public async Task<TranslationResult> TranslateAsync(string text, string? sourceLang = null, string? targetLang = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("翻译文本不能为空", nameof(text));

        var config = _configService.GetConfig();
        sourceLang ??= config.SourceLanguage;
        targetLang ??= config.TargetLanguage;

        // 自动检测源语言
        if (sourceLang == "auto")
        {
            sourceLang = _languageDetector.DetectLanguage(text);
        }

        // 检查缓存
        if (_cache.TryGet(text, sourceLang, targetLang, out var cached))
            return cached;

        // 执行翻译
        var result = await _engine.TranslateAsync(text, sourceLang, targetLang);

        // 存入缓存
        _cache.Set(text, sourceLang, targetLang, result);

        return result;
    }

    /// <summary>自动检测语言</summary>
    public string DetectLanguage(string text) => _languageDetector.DetectLanguage(text);

    /// <summary>检测语言置信度</summary>
    public double GetDetectionConfidence() => _languageDetector.GetConfidence();

    /// <summary>获取引擎状态</summary>
    public EngineStatus GetEngineStatus() => _engine.GetStatus();
}

/// <summary>
/// LRU翻译缓存
/// </summary>
public class TranslationCache
{
    private readonly int _maxSize;
    private readonly LinkedList<string> _lruList = new();
    private readonly Dictionary<string, CacheEntry> _cache = new();

    public TranslationCache(int maxSize = 1000)
    {
        _maxSize = maxSize;
    }

    public bool TryGet(string text, string sourceLang, string targetLang, out TranslationResult result)
    {
        var key = MakeKey(text, sourceLang, targetLang);
        if (_cache.TryGetValue(key, out var entry))
        {
            // 移到LRU头部
            _lruList.Remove(entry.Node);
            _lruList.AddFirst(entry.Node);
            result = entry.Result;
            return true;
        }
        result = null!;
        return false;
    }

    public void Set(string text, string sourceLang, string targetLang, TranslationResult result)
    {
        var key = MakeKey(text, sourceLang, targetLang);

        if (_cache.TryGetValue(key, out var existing))
        {
            existing.Result = result;
            _lruList.Remove(existing.Node);
            _lruList.AddFirst(existing.Node);
            return;
        }

        if (_cache.Count >= _maxSize)
        {
            var last = _lruList.Last!;
            _lruList.RemoveLast();
            _cache.Remove(last.Value);
        }

        var node = _lruList.AddFirst(key);
        _cache[key] = new CacheEntry { Result = result, Node = node };
    }

    private static string MakeKey(string text, string sourceLang, string targetLang)
        => $"{sourceLang}:{targetLang}:{text}";

    private class CacheEntry
    {
        public TranslationResult Result { get; set; } = null!;
        public LinkedListNode<string> Node { get; set; } = null!;
    }
}