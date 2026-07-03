using System.Runtime.InteropServices;
using OpenTranslator.Helpers;
using OpenTranslator.Models;

namespace OpenTranslator.Services;

/// <summary>
/// 全局热键服务 - 使用 RegisterHotKey/UnregisterHotKey API
/// 统一管理所有全局快捷键注册（划词翻译、原地替换、截图、词典）
/// </summary>
public class HotKeyService : IDisposable
{
    private readonly AppConfigService _configService;
    private IntPtr _messageWindowHandle = IntPtr.Zero;
    private bool _isRegistered;
    private bool _disposed;

    // 窗口类名（包含 PID 避免多实例冲突）
    private static readonly string WindowClassName = $"OpenTranslator_HotKey_MsgWin_{Environment.ProcessId}";

    // 虚拟键码
    private const uint VK_Q = 0x51;  // Q
    private const uint VK_T = 0x54;  // T
    private const uint VK_O = 0x4F;   // O
    private const uint VK_D = 0x44;   // D

    /// <summary>
    /// 热键触发事件：划词翻译 (Alt+Q)
    /// </summary>
    public event EventHandler? TranslateHotKeyPressed;

    /// <summary>
    /// 热键触发事件：原地替换翻译 (Ctrl+Shift+T)
    /// </summary>
    public event EventHandler? ReplaceHotKeyPressed;

    /// <summary>
    /// 热键触发事件：截图翻译 (Ctrl+Shift+O) - 预留
    /// </summary>
    public event EventHandler? ScreenshotHotKeyPressed;

    /// <summary>
    /// 热键触发事件：词典查询 (Ctrl+D) - 预留
    /// </summary>
    public event EventHandler? DictionaryHotKeyPressed;

    /// <summary>
    /// 最后一次错误信息
    /// </summary>
    public string? LastError { get; private set; }

    public HotKeyService(AppConfigService configService)
    {
        _configService = configService;
    }

    /// <summary>
    /// 启动热键服务
    /// </summary>
    /// <returns>是否所有热键注册成功</returns>
    public bool Start()
    {
        if (_isRegistered)
            return true;

        try
        {
            if (!CreateMessageWindow())
            {
                System.Diagnostics.Debug.WriteLine($"[HotKeyService] CreateMessageWindow 失败: {LastError}");
                return false;
            }

            var ok = RegisterHotKeys();
            System.Diagnostics.Debug.WriteLine($"[HotKeyService] RegisterHotKeys 结果: {ok}, LastError={LastError}");
            return ok;
        }
        catch (Exception ex)
        {
            LastError = $"Start 异常: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[HotKeyService] Start 异常: {ex}");
            return false;
        }
    }

    /// <summary>
    /// 停止热键服务
    /// </summary>
    public void Stop()
    {
        UnregisterHotKeys();
        DestroyMessageWindow();
    }

    /// <summary>
    /// 创建隐藏的消息窗口用于接收 WM_HOTKEY 消息
    /// </summary>
    private bool CreateMessageWindow()
    {
        try
        {
            var moduleHandle = NativeMethods.GetModuleHandle(null);
            if (moduleHandle == IntPtr.Zero)
            {
                LastError = $"获取模块句柄失败: {Marshal.GetLastWin32Error()}";
                return false;
            }

            _wndProcDelegate = new NativeMethods.WndProcDelegate(WindowProc);

            var wndClass = new NativeMethods.WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.WNDCLASSEX)),
                style = 0,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
                cbClsExtra = 0,
                cbWndExtra = 0,
                hInstance = moduleHandle,
                hIcon = IntPtr.Zero,
                hCursor = IntPtr.Zero,
                hBrush = IntPtr.Zero,
                lpszMenuName = IntPtr.Zero,
                lpszClassName = WindowClassName,
                hIconSm = IntPtr.Zero
            };

            // 先清理已存在的窗口类（避免 ERROR_CLASS_ALREADY_EXISTS）
            NativeMethods.UnregisterClass(WindowClassName, moduleHandle);

            // 注意：RegisterClassEx 和 CreateWindowEx 均使用 CharSet.Unicode（NativeMethods 中已统一）
            ushort classAtom = NativeMethods.RegisterClassEx(ref wndClass);
            if (classAtom == 0)
            {
                int err = Marshal.GetLastWin32Error();
                Console.WriteLine($"[HotKeyService] RegisterClassEx 失败: Win32 错误 {err}");
                if (err == 1410) // ERROR_CLASS_ALREADY_EXISTS
                {
                    Console.WriteLine($"[HotKeyService] 窗口类已存在，继续使用");
                }
                else
                {
                    LastError = $"注册窗口类失败: Win32 错误 {err}";
                    return false;
                }
            }
            else
            {
                Console.WriteLine($"[HotKeyService] RegisterClassEx 成功, atom={classAtom}");
            }

            _messageWindowHandle = NativeMethods.CreateWindowEx(
                NativeMethods.WS_EX_TOOLWINDOW,
                WindowClassName,
                "OpenTranslator HotKey Window",
                NativeMethods.WS_POPUP,
                0, 0, 0, 0,
                IntPtr.Zero,
                IntPtr.Zero,
                moduleHandle,
                IntPtr.Zero);

            if (_messageWindowHandle == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                Console.WriteLine($"[HotKeyService] CreateWindowEx 失败: Win32 错误 {err}");
                LastError = $"创建消息窗口失败: {err}";
                return false;
            }

            Console.WriteLine($"[HotKeyService] CreateWindowEx 成功, hwnd={_messageWindowHandle}");
            return true;
        }
        catch (Exception ex)
        {
            LastError = $"创建消息窗口异常: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// 销毁消息窗口
    /// </summary>
    private void DestroyMessageWindow()
    {
        if (_messageWindowHandle != IntPtr.Zero)
        {
            NativeMethods.DestroyWindow(_messageWindowHandle);
            _messageWindowHandle = IntPtr.Zero;

            var moduleHandle = NativeMethods.GetModuleHandle(null);
            if (moduleHandle != IntPtr.Zero)
            {
                NativeMethods.UnregisterClass(WindowClassName, moduleHandle);
            }
        }
    }

    /// <summary>
    /// 注册所有热键（包括翻译热键 Alt+Q 和 Ctrl+Shift+T）
    /// </summary>
    private bool RegisterHotKeys()
    {
        bool allSuccess = true;

        // 注册 Alt+Q (划词翻译)
        if (!RegisterSingleHotKey(NativeMethods.HOTKEY_ID_TRANSLATE,
            NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT, VK_Q))
        {
            allSuccess = false;
        }

        // 注册 Ctrl+Shift+T (原地替换翻译)
        if (!RegisterSingleHotKey(NativeMethods.HOTKEY_ID_REPLACE,
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT, VK_T))
        {
            allSuccess = false;
        }

        // 注册 Ctrl+Shift+O (截图翻译 - 预留)
        if (!RegisterSingleHotKey(NativeMethods.HOTKEY_ID_SCREENSHOT,
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT, VK_O))
        {
            allSuccess = false;
        }

        // 注册 Ctrl+D (词典查询 - 预留)
        if (!RegisterSingleHotKey(NativeMethods.HOTKEY_ID_DICTIONARY,
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_NOREPEAT, VK_D))
        {
            allSuccess = false;
        }

        _isRegistered = allSuccess;
        return allSuccess;
    }

    /// <summary>
    /// 注册单个热键
    /// </summary>
    private bool RegisterSingleHotKey(int id, uint modifiers, uint vk)
    {
        if (_messageWindowHandle == IntPtr.Zero)
        {
            LastError = "消息窗口未创建";
            return false;
        }

        try
        {
            if (!NativeMethods.RegisterHotKey(_messageWindowHandle, id, modifiers, vk))
            {
                int error = Marshal.GetLastWin32Error();
                LastError = error == 1409
                    ? $"热键 (ID={id}) 已被其他程序注册"
                    : $"注册热键 (ID={id}) 失败: {error}";

                System.Diagnostics.Debug.WriteLine($"[HotKeyService] RegisterHotKey ID={id} 失败: Win32 错误 {error}");
                return false;
            }

            System.Diagnostics.Debug.WriteLine($"[HotKeyService] RegisterHotKey ID={id} 成功");
            return true;
        }
        catch (Exception ex)
        {
            LastError = $"RegisterHotKey (ID={id}) 异常: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[HotKeyService] RegisterHotKey (ID={id}) 异常: {ex}");
            return false;
        }
    }

    /// <summary>
    /// 注销所有热键
    /// </summary>
    private void UnregisterHotKeys()
    {
        if (_messageWindowHandle == IntPtr.Zero)
            return;

        NativeMethods.UnregisterHotKey(_messageWindowHandle, NativeMethods.HOTKEY_ID_TRANSLATE);
        NativeMethods.UnregisterHotKey(_messageWindowHandle, NativeMethods.HOTKEY_ID_REPLACE);
        NativeMethods.UnregisterHotKey(_messageWindowHandle, NativeMethods.HOTKEY_ID_SCREENSHOT);
        NativeMethods.UnregisterHotKey(_messageWindowHandle, NativeMethods.HOTKEY_ID_DICTIONARY);

        _isRegistered = false;
    }

    /// <summary>
    /// 窗口消息处理过程
    /// </summary>
    private IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            int hotKeyId = wParam.ToInt32();

            switch (hotKeyId)
            {
                case NativeMethods.HOTKEY_ID_TRANSLATE:
                    TranslateHotKeyPressed?.Invoke(this, EventArgs.Empty);
                    return IntPtr.Zero;

                case NativeMethods.HOTKEY_ID_REPLACE:
                    ReplaceHotKeyPressed?.Invoke(this, EventArgs.Empty);
                    return IntPtr.Zero;

                case NativeMethods.HOTKEY_ID_SCREENSHOT:
                    ScreenshotHotKeyPressed?.Invoke(this, EventArgs.Empty);
                    return IntPtr.Zero;

                case NativeMethods.HOTKEY_ID_DICTIONARY:
                    DictionaryHotKeyPressed?.Invoke(this, EventArgs.Empty);
                    return IntPtr.Zero;
            }
        }

        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    /// <summary>
    /// 重新注册热键（用于配置更改后）
    /// </summary>
    public bool ReregisterHotKeys()
    {
        if (!_isRegistered)
            return Start();

        UnregisterHotKeys();
        return RegisterHotKeys();
    }

    // WndProc 委托必须作为字段保持引用，防止 GC 回收
    private NativeMethods.WndProcDelegate? _wndProcDelegate;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
                Stop();
            _disposed = true;
        }
    }

    ~HotKeyService()
    {
        Dispose(false);
    }
}

/// <summary>
/// 热键服务接口（用于依赖注入）
/// </summary>
public interface IHotKeyService : IDisposable
{
    bool Start();
    void Stop();
    event EventHandler? TranslateHotKeyPressed;
    event EventHandler? ReplaceHotKeyPressed;
    event EventHandler? ScreenshotHotKeyPressed;
    event EventHandler? DictionaryHotKeyPressed;
}
