using Microsoft.UI.Xaml;
using OpenTranslator.Models;
using WinRT;

namespace OpenTranslator;

internal static class MsgBoxHelper
{
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    public static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);
}

/// <summary>
/// 程序入口 - 负责初始化 Windows App SDK Bootstrap 并启动应用
/// </summary>
public static class Program
{
    [System.Runtime.InteropServices.DllImport("Microsoft.WindowsAppRuntime.Bootstrap.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int MddBootstrapInitialize(
        uint majorMinorVersion,
        string versionTag,
        int packageVersion);

    [System.Runtime.InteropServices.DllImport("Microsoft.WindowsAppRuntime.Bootstrap.dll")]
    private static extern void MddBootstrapShutdown();

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    [STAThread]
    static int Main(string[] args)
    {
        // 全局未处理异常兜底，写入 crash.log
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            try
            {
                File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"),
                    $"[{DateTime.Now}] AppDomain UnhandledException: {e.ExceptionObject}\n");
            }
            catch { }
        };
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            try
            {
                File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"),
                    $"[{DateTime.Now}] TaskScheduler UnobservedTaskException: {e.Exception}\n");
            }
            catch { }
        };

        try
        {
            var dllPath = Path.Combine(AppContext.BaseDirectory, "Microsoft.WindowsAppRuntime.Bootstrap.dll");
            var hModule = LoadLibrary(dllPath);
            if (hModule == IntPtr.Zero)
            {
                MsgBoxHelper.MessageBox(IntPtr.Zero,
                    $"无法加载 Bootstrap DLL\n路径: {dllPath}\n错误码: {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}",
                    "OpenTranslator 错误", 0x10);
                return 1;
            }

            // 请求 Windows App SDK 2.2 运行时 (0x00020002 = 2.2)
            int hr = MddBootstrapInitialize(0x00020002, "", 0);
            if (hr < 0)
            {
                MsgBoxHelper.MessageBox(IntPtr.Zero,
                    $"Windows App SDK Bootstrap 初始化失败 (HRESULT: 0x{hr:X8})\n" +
                    "请确保已安装 Windows App SDK 2.2 运行时",
                    "OpenTranslator 错误", 0x10);
                return 1;
            }

            try
            {
                ComWrappersSupport.InitializeComWrappers();
                Application.Start((p) =>
                {
                    var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                        Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
                    System.Threading.SynchronizationContext.SetSynchronizationContext(context);
                    _ = new App();
                });
                return 0;
            }
            finally
            {
                MddBootstrapShutdown();
            }
        }
        catch (Exception ex)
        {
            MsgBoxHelper.MessageBox(IntPtr.Zero, $"启动失败:\n{ex}", "OpenTranslator 错误", 0x10);
            return 1;
        }
    }
}
