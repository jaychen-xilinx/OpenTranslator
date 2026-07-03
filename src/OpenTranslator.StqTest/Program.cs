using LLama;
using LLama.Common;
using LLama.Sampling;
using System.Diagnostics;

namespace OpenTranslator.StqTest;

/// <summary>
/// STQ 量化兼容性诊断程序
///
/// 目的：独立于主应用（WinUI），在纯控制台环境下验证
///       LLamaSharp 0.27.0 内置的 llama.cpp 能否加载 Hy-MT2 的 STQ 量化 GGUF 模型。
///
/// 如果输出 "unknown quantization type" 或类似错误 → 需切换到 llama.cpp PR #22836 STQ 分支
/// 如果成功输出翻译结果 → STQ 兼容，可继续 MVP 开发
/// </summary>
public static class Program
{
    // 模型文件路径（自动向上查找 models/ 目录）
    private static readonly string ModelPath = ResolveModelPath();

    // Hy-MT2 官方推荐推理参数
    private const float Temperature = 0.7f;
    private const float TopP = 0.6f;
    private const int TopK = 20;
    private const float RepetitionPenalty = 1.05f;
    private const int MaxTokens = 256;  // 测试用，故意缩短以加快验证

    // 测试用翻译提示词（Hy-MT2 官方格式）
    private const string TestSourceText = "Hello, how are you today?";
    private const string TestPrompt = "将以下文本翻译为英语，注意只需要输出翻译后的结果，不要额外解释：\n你好，今天怎么样？";

    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        PrintBanner();
        PrintRuntimeInfo();

        // 第一步：检查模型文件
        if (!File.Exists(ModelPath))
        {
            PrintError($"模型文件不存在: {ModelPath}");
            PrintInfo("请将 Hy-MT2-1.8B-Q4_K_M.gguf 放置到项目 models/ 目录下");
            return;
        }

        var fileInfo = new FileInfo(ModelPath);
        PrintInfo($"模型文件: {Path.GetFileName(ModelPath)} ({FormatBytes(fileInfo.Length)})");

        // 第二步：加载模型
        PrintSection("步骤 1/3: 加载 GGUF 模型");
        LLamaWeights? weights = null;
        LLamaContext? context = null;
        StatelessExecutor? executor = null;

        var swTotal = Stopwatch.StartNew();

        try
        {
            var modelParams = new ModelParams(ModelPath)
            {
                ContextSize = 2048,        // 测试用，减小上下文
                GpuLayerCount = 0,          // 纯 CPU 模式
                UseMemoryLock = false,
                UseMemorymap = true,
                BatchSize = 512,
                Threads = Math.Max(1, Environment.ProcessorCount / 2),
                BatchThreads = Math.Max(1, Environment.ProcessorCount / 2)
            };

            PrintInfo($"ContextSize=2048, Threads={modelParams.Threads}, GpuLayerCount=0 (CPU only)");

            PrintInfo("调用 LLamaWeights.LoadFromFile ...");
            var swLoad = Stopwatch.StartNew();
            weights = LLamaWeights.LoadFromFile(modelParams);
            swLoad.Stop();
            PrintOk($"模型加载成功 ({swLoad.ElapsedMilliseconds} ms)");

            PrintInfo($"  模型元数据: {weights.Metadata.Count} 项");
            // 输出一些关键元数据
            foreach (var kv in weights.Metadata.Take(5))
                PrintInfo($"    {kv.Key} = {kv.Value}");

            PrintInfo("创建 Context + StatelessExecutor ...");
            context = weights.CreateContext(modelParams);
            executor = new StatelessExecutor(weights, modelParams);
            PrintOk("推理执行器创建成功");
        }
        catch (Exception ex)
        {
            PrintError($"模型加载失败: {ex.Message}");
            PrintError($"异常类型: {ex.GetType().FullName}");
            if (ex.InnerException != null)
            {
                PrintError($"内部异常: {ex.InnerException.Message}");
                PrintError($"内部异常类型: {ex.InnerException.GetType().FullName}");
            }

            // 判断是否为 STQ 量化不兼容
            var msg = ex.Message + (ex.InnerException?.Message ?? "");
            if (msg.Contains("quantization", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("STQ", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("unknown type", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine();
                PrintSection("诊断结论");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  >> STQ 量化不兼容！");
                Console.ResetColor();
                PrintInfo("LLamaSharp 0.27.0 内置的 llama.cpp 不支持 Hy-MT2 的自研 STQ 量化内核。");
                PrintInfo("需要切换方案：");
                PrintInfo("  1. 克隆 llama.cpp 并 checkout PR #22836 (pr-stq 分支)");
                PrintInfo("  2. 编译 Windows native 库 (llama.dll)");
                PrintInfo("  3. 替换 LLamaSharp.Backend.Cpu 的 native 库");
            }
            else
            {
                Console.WriteLine();
                PrintSection("诊断结论");
                PrintInfo("模型加载失败，但原因与 STQ 量化无关。");
                PrintInfo("请检查模型文件完整性或内存是否充足。");
            }
            return;
        }

        // 第三步：执行翻译推理
        PrintSection("步骤 2/3: 执行翻译推理");

        try
        {
            var samplingPipeline = new DefaultSamplingPipeline
            {
                Temperature = Temperature,
                TopP = TopP,
                TopK = TopK,
                RepeatPenalty = RepetitionPenalty,
                Seed = 42
            };

            var inferenceParams = new InferenceParams
            {
                MaxTokens = MaxTokens,
                AntiPrompts = ["\n\n", "</s>"],
                SamplingPipeline = samplingPipeline
            };

            PrintInfo($"提示词: {TestPrompt}");
            PrintInfo($"推理参数: temperature={Temperature}, top_p={TopP}, top_k={TopK}, repeat_penalty={RepetitionPenalty}");
            PrintInfo($"MaxTokens={MaxTokens} (测试用，非完整 4096)");
            Console.WriteLine();
            PrintInfo("开始推理 ...");

            var swInfer = Stopwatch.StartNew();
            var sb = new System.Text.StringBuilder();
            int tokenCount = 0;

            await foreach (var token in executor.InferAsync(TestPrompt, inferenceParams))
            {
                sb.Append(token);
                tokenCount++;
                // 每 20 个 token 输出一次简单进度（避免 SetCursorPosition 干扰）
                if (tokenCount % 20 == 0)
                {
                    Console.Write($".");
                }
            }
            swInfer.Stop();

            Console.WriteLine(); // 清除行尾进度
            PrintOk($"推理完成 ({swInfer.ElapsedMilliseconds} ms, {tokenCount} tokens)");

            var result = sb.ToString().Trim().Replace("\r", "").TrimStart('\n');
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  翻译结果: {result}");
            Console.ResetColor();
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            PrintError($"推理失败: {ex.Message}");
            if (ex.InnerException != null)
                PrintError($"内部异常: {ex.InnerException.Message}");
        }

        // 第四步：总结
        swTotal.Stop();
        PrintSection("步骤 3/3: 诊断总结");
        PrintOk($"全部测试通过！STQ 量化模型与 LLamaSharp 0.27.0 兼容。");
        PrintInfo($"总耗时: {swTotal.ElapsedMilliseconds} ms");
        PrintInfo("可以继续 MVP 开发，无需切换 llama.cpp 版本。");

        // 清理
        Console.WriteLine();
        PrintInfo("清理资源 ...");
        executor = null;
        context?.Dispose();
        context = null;
        weights?.Dispose();
        weights = null;
        try { LLama.Native.NativeApi.llama_backend_free(); }
        catch (Exception ex) { PrintInfo($"backend_free 警告: {ex.Message}"); }
        PrintOk("清理完成");
    }

    // ========== 工具方法 ==========

    private static string ResolveModelPath()
    {
        // 策略：从当前工作目录向上查找 models/ 文件夹中的 Q4_K_M 模型
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            var path = Path.Combine(dir.FullName, "models", "Hy-MT2-1.8B-Q4_K_M.gguf");
            if (File.Exists(path)) return path;
            dir = dir.Parent;
        }
        // 兜底：项目根目录
        return Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "models", "Hy-MT2-1.8B-Q4_K_M.gguf");
    }

    private static void PrintBanner()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║       OpenTranslator - STQ 量化兼容性诊断 v0.1.0           ║");
        Console.WriteLine("║   验证 LLamaSharp 0.27.0 能否加载 Hy-MT2 STQ GGUF 模型    ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
    }

    private static void PrintRuntimeInfo()
    {
        PrintInfo($"运行时: .NET {Environment.Version}");
        PrintInfo($"平台: {Environment.OSVersion}");
        PrintInfo($"CPU 核心数: {Environment.ProcessorCount}");
        PrintInfo($"工作目录: {Directory.GetCurrentDirectory()}");
        PrintInfo($"模型搜索路径: {ModelPath}");
        Console.WriteLine();
    }

    private static void PrintSection(string title)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"─── {title} ───");
        Console.ResetColor();
    }

    private static void PrintInfo(string msg)
    {
        Console.WriteLine($"  [INFO] {msg}");
    }

    private static void PrintOk(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  [OK]   {msg}");
        Console.ResetColor();
    }

    private static void PrintError(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  [ERR]  {msg}");
        Console.ResetColor();
    }

    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            >= 1_000_000_000 => $"{bytes / 1_000_000_000.0:F1} GB",
            >= 1_000_000 => $"{bytes / 1_000_000.0:F1} MB",
            >= 1_000 => $"{bytes / 1_000.0:F1} KB",
            _ => $"{bytes} B"
        };
    }
}
