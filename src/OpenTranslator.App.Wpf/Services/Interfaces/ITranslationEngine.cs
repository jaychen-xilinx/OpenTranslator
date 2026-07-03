using OpenTranslator.Models;

namespace OpenTranslator.Services.Interfaces;

public enum EngineStatus { NotLoaded, Loading, Ready, Error, Unloading }

/// <summary>
/// 翻译引擎接口 - 封装模型推理的核心抽象
/// </summary>
public interface ITranslationEngine
{
    /// <summary>初始化引擎并加载模型</summary>
    Task InitializeAsync(string modelPath, HardwareInfo hardwareInfo);

    /// <summary>执行翻译</summary>
    Task<TranslationResult> TranslateAsync(string text, string sourceLang, string targetLang);

    /// <summary>自定义生成（用于摘要、重写等场景）</summary>
    Task<string> GenerateAsync(string prompt);

    /// <summary>卸载模型释放资源</summary>
    Task UnloadAsync();

    /// <summary>获取引擎状态</summary>
    EngineStatus GetStatus();

    /// <summary>模型加载进度事件</summary>
    event EventHandler<double>? LoadProgressChanged;

    /// <summary>引擎状态变更事件</summary>
    event EventHandler<EngineStatus>? StatusChanged;
}