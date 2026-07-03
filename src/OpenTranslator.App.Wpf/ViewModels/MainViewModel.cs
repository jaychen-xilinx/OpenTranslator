using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using OpenTranslator.Helpers;
using OpenTranslator.Models;
using OpenTranslator.Services;
using OpenTranslator.Services.Interfaces;

namespace OpenTranslator.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly TranslationService _translationService;
    private readonly IModelManager _modelManager;
    private readonly AppConfigService _configService;
    private readonly HardwareDetector _hardwareDetector;
    private readonly ITranslationHistoryService _historyService;

    public event PropertyChangedEventHandler? PropertyChanged;

    private const int PageSize = 20;
    private bool _isInitializing;

    public MainViewModel(
        TranslationService translationService,
        IModelManager modelManager,
        AppConfigService configService,
        HardwareDetector hardwareDetector,
        ITranslationHistoryService? historyService = null)
    {
        _translationService = translationService;
        _modelManager = modelManager;
        _configService = configService;
        _hardwareDetector = hardwareDetector;
        _historyService = historyService ?? new TranslationHistoryService();

        Languages = new ObservableCollection<LanguagePair>(Constants.SupportedLanguages);
        SourceLanguage = Languages.First(l => l.Code == "auto");
        TargetLanguage = Languages.First(l => l.Code == "zh");

        HistoryItems = new ObservableCollection<TranslationHistoryItem>();

        var config = _configService.GetConfig();
        TargetLanguage = Languages.FirstOrDefault(l => l.Code == config.TargetLanguage) ?? TargetLanguage;

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        _isInitializing = true;
        try
        {
            await _historyService.InitializeAsync();
            await LoadHistoryAsync();
            await AutoLoadModelAsync();
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private async Task AutoLoadModelAsync()
    {
        try
        {
            var allModels = _modelManager.GetAvailableModels();
            var downloadedModels = allModels.Where(m => m.IsDownloaded).ToList();
            AvailableModels = new ObservableCollection<ModelInfo>(downloadedModels);

            if (!downloadedModels.Any())
            {
                StatusText = "未找到模型，请先下载模型";
                return;
            }

            var config = _configService.GetConfig();
            var defaultModel = downloadedModels.FirstOrDefault(m => m.Name == config.DefaultModel)
                               ?? downloadedModels.First();

            StatusText = $"正在加载模型 {defaultModel.DisplayName}...";
            EngineStatus = EngineStatus.Loading;
            IsModelSwitching = true;

            await _modelManager.LoadModelAsync(defaultModel.Name);

            _currentModel = defaultModel;
            OnPropertyChanged(nameof(CurrentModel));
            OnPropertyChanged(nameof(CurrentModelDisplay));
            EngineStatus = EngineStatus.Ready;
            StatusText = $"{defaultModel.DisplayName} 已就绪";
        }
        catch (Exception ex)
        {
            EngineStatus = EngineStatus.Error;
            StatusText = $"模型加载失败: {ex.Message}";
        }
        finally
        {
            IsModelSwitching = false;
        }
    }

    private async Task SwitchModelAsync(ModelInfo? model)
    {
        if (model == null || _isModelSwitching) return;

        try
        {
            IsModelSwitching = true;
            StatusText = $"正在切换到 {model.DisplayName}...";
            EngineStatus = EngineStatus.Loading;

            await _modelManager.SwitchModelAsync(model.Name);

            // 保存到配置
            var config = _configService.GetConfig();
            config.DefaultModel = model.Name;
            _configService.SaveConfig(config);

            // 更新 AvailableModels 中的加载状态
            foreach (var m in AvailableModels)
            {
                m.IsLoaded = m.Name == model.Name;
            }

            EngineStatus = EngineStatus.Ready;
            StatusText = $"{model.DisplayName} 已就绪";
        }
        catch (Exception ex)
        {
            EngineStatus = EngineStatus.Error;
            StatusText = $"模型切换失败: {ex.Message}";
        }
        finally
        {
            IsModelSwitching = false;
        }
    }

    private string _sourceText = string.Empty;
    public string SourceText
    {
        get => _sourceText;
        set { _sourceText = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanTranslate)); }
    }

    private string _translatedText = string.Empty;
    public string TranslatedText
    {
        get => _translatedText;
        set { _translatedText = value; OnPropertyChanged(); }
    }

    private LanguagePair _sourceLanguage = null!;
    public LanguagePair SourceLanguage
    {
        get => _sourceLanguage;
        set { _sourceLanguage = value; OnPropertyChanged(); }
    }

    private LanguagePair _targetLanguage = null!;
    public LanguagePair TargetLanguage
    {
        get => _targetLanguage;
        set { _targetLanguage = value; OnPropertyChanged(); }
    }

    private bool _isTranslating;
    public bool IsTranslating
    {
        get => _isTranslating;
        set { _isTranslating = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanTranslate)); }
    }

    public bool CanTranslate => !IsTranslating && !string.IsNullOrWhiteSpace(SourceText);

    private string _statusText = "就绪";
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    private long _lastInferenceTimeMs;
    public long LastInferenceTimeMs
    {
        get => _lastInferenceTimeMs;
        set { _lastInferenceTimeMs = value; OnPropertyChanged(); OnPropertyChanged(nameof(InferenceTimeDisplay)); }
    }

    public string InferenceTimeDisplay => LastInferenceTimeMs > 0
        ? $"推理耗时: {LastInferenceTimeMs}ms"
        : string.Empty;

    private EngineStatus _engineStatus = EngineStatus.NotLoaded;
    public EngineStatus EngineStatus
    {
        get => _engineStatus;
        set { _engineStatus = value; OnPropertyChanged(); OnPropertyChanged(nameof(EngineStatusDisplay)); }
    }

    public string EngineStatusDisplay => EngineStatus switch
    {
        EngineStatus.NotLoaded => "未加载",
        EngineStatus.Loading => "加载中...",
        EngineStatus.Ready => "就绪",
        EngineStatus.Error => "错误",
        EngineStatus.Unloading => "卸载中...",
        _ => "未知"
    };

    public ObservableCollection<LanguagePair> Languages { get; }

    private ObservableCollection<ModelInfo> _availableModels = [];
    public ObservableCollection<ModelInfo> AvailableModels
    {
        get => _availableModels;
        set { _availableModels = value; OnPropertyChanged(); }
    }

    private ModelInfo? _currentModel;
    public ModelInfo? CurrentModel
    {
        get => _currentModel;
        set
        {
            if (_currentModel?.Name == value?.Name) return;
            _currentModel = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentModelDisplay));
            if (!_isInitializing && value != null)
            {
                _ = SwitchModelAsync(value);
            }
        }
    }

    public string CurrentModelDisplay => _currentModel?.DisplayName ?? "未加载模型";

    private bool _isModelSwitching;
    public bool IsModelSwitching
    {
        get => _isModelSwitching;
        set { _isModelSwitching = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanTranslate)); }
    }

    private ObservableCollection<TranslationHistoryItem> _historyItems = [];
    public ObservableCollection<TranslationHistoryItem> HistoryItems
    {
        get => _historyItems;
        set { _historyItems = value; OnPropertyChanged(); }
    }

    private string _historySearchKeyword = string.Empty;
    public string HistorySearchKeyword
    {
        get => _historySearchKeyword;
        set { _historySearchKeyword = value; OnPropertyChanged(); }
    }

    private bool _isHistoryLoading;
    public bool IsHistoryLoading
    {
        get => _isHistoryLoading;
        set { _isHistoryLoading = value; OnPropertyChanged(); }
    }

    private bool _hasMoreHistory = true;
    public bool HasMoreHistory
    {
        get => _hasMoreHistory;
        set { _hasMoreHistory = value; OnPropertyChanged(); }
    }

    private int _historyTotalCount;
    public int HistoryTotalCount
    {
        get => _historyTotalCount;
        set { _historyTotalCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(HistoryCountDisplay)); }
    }

    public string HistoryCountDisplay => $"共 {HistoryTotalCount} 条记录";

    private TranslationHistoryItem? _selectedHistoryItem;
    public TranslationHistoryItem? SelectedHistoryItem
    {
        get => _selectedHistoryItem;
        set
        {
            _selectedHistoryItem = value;
            OnPropertyChanged();
            if (value != null)
            {
                LoadHistoryItemToEditor(value);
            }
        }
    }

    public ICommand TranslateCommand => new RelayCommand(async _ => await TranslateAsync());
    public ICommand SearchHistoryCommand => new RelayCommand(async _ => await SearchHistoryAsync());
    public ICommand LoadMoreHistoryCommand => new RelayCommand(async _ => await LoadMoreHistoryAsync());
    public ICommand DeleteHistoryCommand => new RelayCommand(async p => await DeleteHistoryAsync(p as TranslationHistoryItem));
    public ICommand ToggleFavoriteCommand => new RelayCommand(async p => await ToggleFavoriteAsync(p as TranslationHistoryItem));
    public ICommand ClearHistoryCommand => new RelayCommand(async _ => await ClearHistoryAsync());
    public ICommand SwapLanguagesCommand => new RelayCommand(_ => SwapLanguages());
    public ICommand ClearTextCommand => new RelayCommand(_ => ClearText());
    public ICommand CopyResultCommand => new RelayCommand(_ => CopyResult());

    public async Task TranslateAsync()
    {
        if (string.IsNullOrWhiteSpace(SourceText)) return;

        IsTranslating = true;
        StatusText = "翻译中...";

        try
        {
            var result = await _translationService.TranslateAsync(
                SourceText,
                SourceLanguage.Code,
                TargetLanguage.Code);

            TranslatedText = result.TranslatedText;
            LastInferenceTimeMs = result.InferenceTimeMs;
            StatusText = "翻译完成";

            await SaveToHistoryAsync(result);
        }
        catch (Exception ex)
        {
            TranslatedText = $"翻译失败: {ex.Message}";
            StatusText = "翻译失败";
        }
        finally
        {
            IsTranslating = false;
        }
    }

    private async Task SaveToHistoryAsync(TranslationResult result)
    {
        try
        {
            var historyItem = TranslationHistoryItem.FromResult(result);
            await _historyService.AddAsync(historyItem);

            if (string.IsNullOrEmpty(HistorySearchKeyword) && HistoryItems.Count < PageSize)
            {
                HistoryItems.Insert(0, historyItem);
                HistoryTotalCount = await _historyService.GetTotalCountAsync();
            }
        }
        catch
        {
        }
    }

    public void SwapLanguages()
    {
        if (SourceLanguage.Code == "auto") return;
        (SourceLanguage, TargetLanguage) = (TargetLanguage, SourceLanguage);
        (SourceText, TranslatedText) = (TranslatedText, SourceText);
    }

    public void ClearText()
    {
        SourceText = string.Empty;
        TranslatedText = string.Empty;
    }

    public void CopyResult()
    {
        if (!string.IsNullOrWhiteSpace(TranslatedText))
        {
            Clipboard.SetText(TranslatedText);
        }
    }

    private async Task LoadHistoryAsync()
    {
        if (IsHistoryLoading) return;

        IsHistoryLoading = true;
        try
        {
            HistoryItems.Clear();
            var items = await _historyService.SearchAsync(HistorySearchKeyword, 0, PageSize);
            foreach (var item in items)
            {
                HistoryItems.Add(item);
            }
            HasMoreHistory = items.Count >= PageSize;
            HistoryTotalCount = await _historyService.GetTotalCountAsync(HistorySearchKeyword);
        }
        finally
        {
            IsHistoryLoading = false;
        }
    }

    private async Task SearchHistoryAsync()
    {
        await LoadHistoryAsync();
    }

    private async Task LoadMoreHistoryAsync()
    {
        if (IsHistoryLoading || !HasMoreHistory) return;

        IsHistoryLoading = true;
        try
        {
            var items = await _historyService.SearchAsync(
                HistorySearchKeyword,
                HistoryItems.Count,
                PageSize);

            foreach (var item in items)
            {
                HistoryItems.Add(item);
            }
            HasMoreHistory = items.Count >= PageSize;
        }
        finally
        {
            IsHistoryLoading = false;
        }
    }

    private async Task DeleteHistoryAsync(TranslationHistoryItem? item)
    {
        if (item == null) return;

        try
        {
            await _historyService.DeleteAsync(item.Id);
            HistoryItems.Remove(item);
            HistoryTotalCount = await _historyService.GetTotalCountAsync(HistorySearchKeyword);
        }
        catch
        {
        }
    }

    private async Task ToggleFavoriteAsync(TranslationHistoryItem? item)
    {
        if (item == null) return;

        try
        {
            await _historyService.ToggleFavoriteAsync(item.Id);
            item.IsFavorite = !item.IsFavorite;
            OnPropertyChanged(nameof(HistoryItems));
        }
        catch
        {
        }
    }

    private async Task ClearHistoryAsync()
    {
        try
        {
            await _historyService.ClearAllAsync();
            await LoadHistoryAsync();
        }
        catch
        {
        }
    }

    private void LoadHistoryItemToEditor(TranslationHistoryItem item)
    {
        SourceText = item.SourceText;
        TranslatedText = item.TranslatedText;

        var sourceLang = Languages.FirstOrDefault(l => l.Code == item.SourceLanguage);
        if (sourceLang != null) SourceLanguage = sourceLang;

        var targetLang = Languages.FirstOrDefault(l => l.Code == item.TargetLanguage);
        if (targetLang != null) TargetLanguage = targetLang;
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class RelayCommand : ICommand
{
    private readonly Func<object?, Task>? _executeAsync;
    private readonly Action<object?>? _executeSync;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null)
    {
        _executeAsync = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _executeSync = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public async void Execute(object? parameter)
    {
        if (_executeAsync != null)
            await _executeAsync(parameter);
        else
            _executeSync?.Invoke(parameter);
    }

    public void RaiseCanExecuteChanged()
        => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
