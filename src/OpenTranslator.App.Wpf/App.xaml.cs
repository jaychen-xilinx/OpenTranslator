using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Threading;
using OpenTranslator.Services;
using OpenTranslator.Services.Interfaces;
using OpenTranslator.ViewModels;

namespace OpenTranslator.App.Wpf;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        Console.WriteLine("[App] 启动中...");

        try
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            Services = services.BuildServiceProvider();
            Console.WriteLine("[App] 服务配置完成");

            var mainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainViewModel>()
            };
            Console.WriteLine("[App] MainWindow 创建完成，准备显示");
            mainWindow.Show();
            Console.WriteLine("[App] MainWindow 已显示");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[App] 启动失败: {ex}");
            MessageBox.Show($"启动失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Console.WriteLine($"[App] Dispatcher 未处理异常: {e.Exception}");
        e.Handled = true;
        MessageBox.Show($"发生错误: {e.Exception.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Console.WriteLine($"[App] 未处理异常: {e.ExceptionObject}");
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Console.WriteLine($"[App] 未观察到的任务异常: {e.Exception}");
        e.SetObserved();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<AppConfigService>();
        services.AddSingleton<ITranslationEngine, LlamaCppEngine>();
        services.AddSingleton<IModelDownloader, ModelDownloader>();
        services.AddSingleton<IModelManager, ModelManager>();
        services.AddSingleton<HardwareDetector>();
        services.AddSingleton<ILanguageDetector, LanguageDetector>();
        services.AddSingleton<TranslationService>();
        services.AddSingleton<ITranslationHistoryService, TranslationHistoryService>();
        services.AddSingleton<HotKeyService>();
        services.AddSingleton<SelectionTranslator>();

        services.AddSingleton<MainViewModel>();
    }
}
