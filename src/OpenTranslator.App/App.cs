using Microsoft.UI.Xaml;
using OpenTranslator.Models;
using OpenTranslator.Services;
using OpenTranslator.Services.Interfaces;
using OpenTranslator.ViewModels;
using OpenTranslator.Views;

namespace OpenTranslator;

/// <summary>
/// 应用主类 - 负责服务容器初始化、主窗口与全局快捷键/划词翻译的生命周期管理
/// </summary>
public class App : Application, IDisposable
{
    private Window? _mainWindow;
    private SelectionTranslator? _selectionTranslator;
    private HotKeyService? _hotKeyService;

    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        ConfigureServices();
        this.UnhandledException += (s, e) =>
        {
            try
            {
                var logPath = Path.Combine(AppContext.BaseDirectory, "crash.log");
                var ex = e.Exception;
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"[{DateTime.Now}] App.UnhandledException: {ex}");
                sb.AppendLine($"  Type: {ex.GetType().FullName}");
                sb.AppendLine($"  Message: {ex.Message}");
                sb.AppendLine($"  StackTrace:");
                sb.AppendLine(ex.StackTrace);
                var inner = ex.InnerException;
                while (inner != null)
                {
                    sb.AppendLine($"  --- InnerException ---");
                    sb.AppendLine($"  Type: {inner.GetType().FullName}");
                    sb.AppendLine($"  Message: {inner.Message}");
                    sb.AppendLine($"  StackTrace:");
                    sb.AppendLine(inner.StackTrace);
                    inner = inner.InnerException;
                }
                File.AppendAllText(logPath, sb.ToString() + "\n\n");
            }
            catch { }
            e.Handled = true;
        };
        this.DebugSettings.BindingFailed += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[BindingFailed] {e.Message}");
        };
    }

    private static void ConfigureServices()
    {
        var configService = new AppConfigService();
        var downloader = new ModelDownloader();
        var engine = new LlamaCppEngine();
        var modelManager = new ModelManager(downloader, engine);
        var languageDetector = new LanguageDetector();
        var translationService = new TranslationService(engine, languageDetector, configService);
        var hardwareDetector = new HardwareDetector();
        var historyService = new TranslationHistoryService();

        Services = new ServiceProvider(
            configService, engine, modelManager,
            languageDetector, translationService,
            hardwareDetector, downloader, historyService);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _mainWindow = new MainWindow();
            _mainWindow.Closed += OnMainWindowClosed;
            _mainWindow.Activate();

            try
            {
                InitializeSelectionTranslator();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] 划词翻译初始化失败: {ex}");
            }

            try
            {
                InitializeHotKeyService();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WARN] 热键服务初始化异常 (应用继续运行): {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            MsgBoxHelper.MessageBox(IntPtr.Zero, $"OnLaunched 失败:\n{ex}", "OpenTranslator 错误", 0x10);
        }
    }

    private void OnMainWindowClosed(object sender, WindowEventArgs args)
    {
        Dispose();
    }

    private void InitializeSelectionTranslator()
    {
        var translationService = (TranslationService)Services.GetService(typeof(TranslationService))!;
        var configService = (AppConfigService)Services.GetService(typeof(AppConfigService))!;

        _selectionTranslator = new SelectionTranslator(translationService, configService);
        _selectionTranslator.TranslationCompleted += (s, result) =>
        {
            System.Diagnostics.Debug.WriteLine($"翻译完成: {result.TranslatedText}");
        };
        _selectionTranslator.HotKeyTriggered += (s, text) =>
        {
            System.Diagnostics.Debug.WriteLine($"快捷键触发，选中文本: {text}");
        };
    }

    private void InitializeHotKeyService()
    {
        var configService = (AppConfigService)Services.GetService(typeof(AppConfigService))!;

        _hotKeyService = new HotKeyService(configService);

        // 全局热键统一由 HotKeyService 接收，再驱动 SelectionTranslator 执行翻译/替换
        _hotKeyService.TranslateHotKeyPressed += async (s, e) =>
        {
            if (_selectionTranslator != null)
                await _selectionTranslator.TranslateSelectionAsync();
        };
        _hotKeyService.ReplaceHotKeyPressed += async (s, e) =>
        {
            if (_selectionTranslator != null)
                await _selectionTranslator.ReplaceSelectionAsync();
        };
        _hotKeyService.ScreenshotHotKeyPressed += (s, e)
            => System.Diagnostics.Debug.WriteLine("热键触发: Ctrl+Shift+O (截图翻译) - 预留功能");
        _hotKeyService.DictionaryHotKeyPressed += (s, e)
            => System.Diagnostics.Debug.WriteLine("热键触发: Ctrl+D (词典查询) - 预留功能");

        if (!_hotKeyService.Start())
        {
            System.Diagnostics.Debug.WriteLine($"热键服务启动失败: {_hotKeyService.LastError}");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("热键服务已启动");
        }
    }

    public void Dispose()
    {
        _hotKeyService?.Stop();
        _hotKeyService?.Dispose();
        _hotKeyService = null;

        _selectionTranslator?.HidePopup();
        _selectionTranslator = null;
    }
}

/// <summary>
/// 轻量级服务容器 - 按类型注册与查找单例
/// </summary>
public class ServiceProvider : IServiceProvider
{
    private readonly Dictionary<Type, object> _services = [];

    public ServiceProvider(
        AppConfigService config, ITranslationEngine engine, IModelManager modelManager,
        ILanguageDetector languageDetector, TranslationService translationService,
        HardwareDetector hardwareDetector, ModelDownloader downloader,
        ITranslationHistoryService? historyService = null)
    {
        _services[typeof(AppConfigService)] = config;
        _services[typeof(ITranslationEngine)] = engine;
        _services[typeof(IModelManager)] = modelManager;
        _services[typeof(ILanguageDetector)] = languageDetector;
        _services[typeof(TranslationService)] = translationService;
        _services[typeof(HardwareDetector)] = hardwareDetector;
        _services[typeof(ModelDownloader)] = downloader;
        if (historyService != null)
            _services[typeof(ITranslationHistoryService)] = historyService;
    }

    public object? GetService(Type serviceType)
        => _services.TryGetValue(serviceType, out var service) ? service : null;
}
