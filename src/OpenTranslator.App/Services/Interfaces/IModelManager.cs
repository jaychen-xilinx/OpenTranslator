using OpenTranslator.Models;

namespace OpenTranslator.Services.Interfaces;

/// <summary>
/// 模型管理器接口 - 管理模型下载、加载、切换
/// </summary>
public interface IModelManager
{
    /// <summary>获取所有可用模型信息</summary>
    List<ModelInfo> GetAvailableModels();

    /// <summary>获取当前加载的模型</summary>
    ModelInfo? GetCurrentModel();

    /// <summary>检查模型是否已下载</summary>
    bool IsModelDownloaded(string modelName);

    /// <summary>下载模型（支持断点续传）</summary>
    Task<ModelInfo> DownloadModelAsync(string modelName, IProgress<ModelDownloadProgress>? progress = null);

    /// <summary>加载指定模型</summary>
    Task LoadModelAsync(string modelName);

    /// <summary>切换模型</summary>
    Task SwitchModelAsync(string modelName);

    /// <summary>卸载当前模型</summary>
    Task UnloadCurrentModelAsync();

    /// <summary>验证模型文件完整性</summary>
    Task<bool> ValidateModelAsync(string modelName);

    /// <summary>获取模型存储目录</summary>
    string GetModelsDirectory();

    /// <summary>模型加载进度事件</summary>
    event EventHandler<double>? ModelLoadProgressChanged;

    /// <summary>下载进度事件</summary>
    event EventHandler<ModelDownloadProgress>? DownloadProgressChanged;
}