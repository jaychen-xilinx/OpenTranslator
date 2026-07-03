using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Windows.ApplicationModel.DataTransfer;
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

        // 初始化语言列表
        Languages = new ObservableCollection<LanguagePair>(Constants.SupportedLanguages);
        SourceLanguage = Languages.First(l => l.Code == "auto");
        TargetLanguage = Languages.First(l => l.Code == "zh");

        // 初始化历史记录列表
        HistoryItems = new ObservableCollection<TranslationHistoryItem>();

        // 加载配置
        var config = _configService.GetConfig();
        TargetLanguage = Languages.FirstOrDefault(l => l.Code == config.TargetLanguage) ?? TargetLanguage;

        // 异步初始化历史服务并加载历史
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await _historyService.InitializeAsync();
        await LoadHistoryAsync();
        await AutoLoadModelAsync();
    }

    private async Task AutoLoadModelAsync()
    {
        try
        {
            var models = _modelManager.GetAvailableModels();
            var downloaded = models.FirstOrDefault(m => m.IsDownloaded);

            if (downloaded != null)
            {
                StatusText = $"正在加载模型 {downloaded.Name}...";
                EngineStatus = EngineStatus.Loading;

                await _modelManager.LoadModelAsync(downloaded.Name);

                EngineStatus = EngineStatus.Ready;
                StatusText = $"{downloaded.Name} 已就绪";
            }
            else
            {
                StatusText = "未找到模型，请先下载模型";
            }
        }
        catch (Exception ex)
        {
            EngineStatus = EngineStatus.Error;
            StatusText = $"模型加载失败: {ex.Message}";
        }
    }

    // ========== 绑定属性 ==========

    private string _sourceText = string.Empty;
    public string SourceText
    {
        get => _sourceText;
        set { _sourceText = value; OnPropertyChanged(); }
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
        Services.Interfaces.EngineStatus.NotLoaded => "未加载",
        Services.Interfaces.EngineStatus.Loading => "加载中...",
        Services.Interfaces.EngineStatus.Ready => "就绪",
        Services.Interfaces.EngineStatus.Error => "错误",
        Services.Interfaces.EngineStatus.Unloading => "卸载中...",
        _ => "未知"
    };

    public ObservableCollection<LanguagePair> Languages { get; }

    // ========== 历史记录属性 ==========

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

    // ========== 命令 ==========

    public ICommand TranslateCommand => new RelayCommand(async _ => await TranslateAsync());

    public ICommand SearchHistoryCommand => new RelayCommand(async _ => await SearchHistoryAsync());

    public ICommand LoadMoreHistoryCommand => new RelayCommand(async _ => await LoadMoreHistoryAsync());

    public ICommand DeleteHistoryCommand => new RelayCommand(async p => await DeleteHistoryAsync(p as TranslationHistoryItem));

    public ICommand ToggleFavoriteCommand => new RelayCommand(async p => await ToggleFavoriteAsync(p as TranslationHistoryItem));

    public ICommand ClearHistoryCommand => new RelayCommand(async _ => await ClearHistoryAsync());

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

            // 自动保存到历史记录
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

            // 如果是首页且没有搜索关键词，添加到列表顶部
            if (string.IsNullOrEmpty(HistorySearchKeyword) && HistoryItems.Count < PageSize)
            {
                HistoryItems.Insert(0, historyItem);
                HistoryTotalCount = await _historyService.GetTotalCountAsync();
            }
        }
        catch
        {
            // 历史记录保存失败不影响翻译流程
        }
    }

    public void SwapLanguages()
    {
        if (SourceLanguage.Code == "auto") return;
        (SourceLanguage, TargetLanguage) = (TargetLanguage, SourceLanguage);
        // 交换后重新翻译
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
            var dataPackage = new DataPackage();
            dataPackage.SetText(TranslatedText);
            Clipboard.SetContent(dataPackage);
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
            // 删除失败，忽略
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
            // 操作失败，忽略
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
            // 清空失败，忽略
        }
    }

    private void LoadHistoryItemToEditor(TranslationHistoryItem item)
    {
        SourceText = item.SourceText;
        TranslatedText = item.TranslatedText;

        // 设置语言
        var sourceLang = Languages.FirstOrDefault(l => l.Code == item.SourceLanguage);
        if (sourceLang != null) SourceLanguage = sourceLang;

        var targetLang = Languages.FirstOrDefault(l => l.Code == item.TargetLanguage);
        if (targetLang != null) TargetLanguage = targetLang;
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// 简单的命令实现
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public async void Execute(object? parameter) => await _execute(parameter);

    /// <summary>显式触发 CanExecute 重新评估</summary>
    public void RaiseCanExecuteChanged()
        => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
