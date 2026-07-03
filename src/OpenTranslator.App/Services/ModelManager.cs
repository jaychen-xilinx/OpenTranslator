using OpenTranslator.Helpers;
using OpenTranslator.Models;
using OpenTranslator.Services.Interfaces;

namespace OpenTranslator.Services;

/// <summary>
/// 模型管理器实现 - 管理模型下载、加载、切换
/// </summary>
public class ModelManager : IModelManager
{
    private readonly IModelDownloader _downloader;
    private readonly ITranslationEngine _engine;
    private readonly string _modelsDirectory;
    private ModelInfo? _currentModel;

    public event EventHandler<double>? ModelLoadProgressChanged;
    public event EventHandler<ModelDownloadProgress>? DownloadProgressChanged;

    public ModelManager(IModelDownloader downloader, ITranslationEngine engine, string? modelsDirectory = null)
    {
        _downloader = downloader;
        _engine = engine;
        _modelsDirectory = modelsDirectory ?? ResolveModelsDirectory();

        _engine.LoadProgressChanged += (_, p) => ModelLoadProgressChanged?.Invoke(this, p);
    }

    private static string ResolveModelsDirectory()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;

        var candidate1 = Path.Combine(baseDir, Constants.ModelsFolder);
        if (IsValidModelsDirectory(candidate1))
            return candidate1;

        var dir = new DirectoryInfo(baseDir);
        while (dir != null && dir.Parent != null)
        {
            var candidate = Path.Combine(dir.FullName, Constants.ModelsFolder);
            if (IsValidModelsDirectory(candidate))
                return candidate;
            dir = dir.Parent;
        }

        Directory.CreateDirectory(candidate1);
        return candidate1;
    }

    private static bool IsValidModelsDirectory(string path)
    {
        if (!Directory.Exists(path))
            return false;
        try
        {
            return Directory.EnumerateFiles(path, "*.gguf").Any();
        }
        catch
        {
            return false;
        }
    }

    public List<ModelInfo> GetAvailableModels()
    {
        var models = Constants.PredefinedModels.ToList();

        foreach (var model in models)
        {
            var filePath = Path.Combine(_modelsDirectory, model.FileName);
            model.IsDownloaded = File.Exists(filePath);
            model.IsLoaded = _currentModel?.Name == model.Name && _engine.GetStatus() == EngineStatus.Ready;
        }

        return models;
    }

    public ModelInfo? GetCurrentModel() => _currentModel;

    public bool IsModelDownloaded(string modelName)
    {
        var model = Constants.PredefinedModels.FirstOrDefault(m => m.Name == modelName);
        if (model == null) return false;

        var filePath = Path.Combine(_modelsDirectory, model.FileName);
        return File.Exists(filePath);
    }

    public async Task<ModelInfo> DownloadModelAsync(string modelName, IProgress<ModelDownloadProgress>? progress = null)
    {
        var modelInfo = Constants.PredefinedModels.FirstOrDefault(m => m.Name == modelName);
        if (modelInfo == null) throw new ArgumentException($"未知的模型: {modelName}");

        var filePath = await _downloader.DownloadModelAsync(modelInfo, progress);
        modelInfo.IsDownloaded = true;

        DownloadProgressChanged?.Invoke(this, new ModelDownloadProgress
        {
            ModelName = modelName,
            Status = "Completed"
        });

        return modelInfo;
    }

    public async Task LoadModelAsync(string modelName)
    {
        var modelInfo = Constants.PredefinedModels.FirstOrDefault(m => m.Name == modelName);
        if (modelInfo == null) throw new ArgumentException($"未知的模型: {modelName}");

        if (!IsModelDownloaded(modelName))
            throw new InvalidOperationException($"模型 {modelName} 尚未下载");

        var filePath = Path.Combine(_modelsDirectory, modelInfo.FileName);

        // 检测硬件以配置最优参数
        var detector = new HardwareDetector();
        var hardwareInfo = detector.DetectHardware();

        await _engine.InitializeAsync(filePath, hardwareInfo);

        _currentModel = modelInfo;
        modelInfo.IsLoaded = true;
    }

    public async Task SwitchModelAsync(string modelName)
    {
        if (_currentModel?.Name == modelName && _engine.GetStatus() == EngineStatus.Ready)
            return;

        // 卸载当前模型
        if (_currentModel != null && _engine.GetStatus() == EngineStatus.Ready)
        {
            await UnloadCurrentModelAsync();
        }

        await LoadModelAsync(modelName);
    }

    public async Task UnloadCurrentModelAsync()
    {
        if (_currentModel != null)
        {
            await _engine.UnloadAsync();
            _currentModel.IsLoaded = false;
            _currentModel = null;
        }
    }

    public async Task<bool> ValidateModelAsync(string modelName)
    {
        var modelInfo = Constants.PredefinedModels.FirstOrDefault(m => m.Name == modelName);
        if (modelInfo == null) return false;

        var filePath = Path.Combine(_modelsDirectory, modelInfo.FileName);
        return await _downloader.ValidateModelAsync(filePath, modelInfo.FileSizeBytes);
    }

    public string GetModelsDirectory() => _modelsDirectory;
}