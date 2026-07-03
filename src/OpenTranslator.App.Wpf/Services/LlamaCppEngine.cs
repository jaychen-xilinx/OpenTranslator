using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;
using OpenTranslator.Helpers;
using OpenTranslator.Models;
using OpenTranslator.Services.Interfaces;

namespace OpenTranslator.Services;

public class LlamaCppEngine : ITranslationEngine, IDisposable
{
    public event EventHandler<double>? LoadProgressChanged;
    public event EventHandler<EngineStatus>? StatusChanged;

    private LLamaWeights? _weights;
    private LLamaContext? _context;
    private StatelessExecutor? _executor;
    private EngineStatus _status = EngineStatus.NotLoaded;

    // 使用 SemaphoreSlim 替代 lock，避免在 async 方法中同步阻塞导致死锁/线程池饥饿
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    // 模型加载属于改变引擎状态的操作，需要独立锁保护，避免与推理并发
    private readonly SemaphoreSlim _loadSemaphore = new(1, 1);
    private string _currentModelPath = string.Empty;

    private const float DefaultTemperature = 0.7f;
    private const float DefaultTopP = 0.6f;
    private const int DefaultTopK = 20;
    private const float DefaultRepetitionPenalty = 1.05f;
    private const int DefaultMaxTokens = 4096;
    private const uint DefaultSeed = 42;

    public EngineStatus GetStatus() => _status;

    private void SetStatus(EngineStatus status)
    {
        _status = status;
        StatusChanged?.Invoke(this, status);
    }

    public async Task InitializeAsync(string modelPath, HardwareInfo hardwareInfo)
    {
        await _loadSemaphore.WaitAsync();
        try
        {
            if (_status == EngineStatus.Loading || _status == EngineStatus.Ready)
                throw new InvalidOperationException("引擎已在加载或已就绪");

            SetStatus(EngineStatus.Loading);
            LoadProgressChanged?.Invoke(this, 0);

            try
            {
                // 模型加载是 CPU/IO 密集型操作，放到后台线程避免阻塞 UI
                await Task.Run(() =>
                {
                    _currentModelPath = modelPath;

                    var modelParams = new ModelParams(modelPath)
                    {
                        ContextSize = 4096,
                        GpuLayerCount = hardwareInfo.Gpus.Count > 0 ? 32 : 0,
                        UseMemoryLock = false,
                        UseMemorymap = true,
                        BatchSize = 512,
                        Threads = hardwareInfo.CpuCores,
                        BatchThreads = hardwareInfo.CpuCores
                    };

                    _weights = LLamaWeights.LoadFromFile(modelParams);
                    _context = _weights.CreateContext(modelParams);
                    _executor = new StatelessExecutor(_weights, modelParams);
                });

                // 根据模型名称自动设置对应的 chat template
                var modelFileName = Path.GetFileNameWithoutExtension(modelPath);
                var templateType = PromptBuilder.DetectTemplateFromModelName(modelFileName);
                PromptBuilder.SetModelTemplate(templateType);
                Console.WriteLine($"[LlamaCppEngine] 模型 {modelFileName} 使用模板: {templateType}");

                SetStatus(EngineStatus.Ready);
                LoadProgressChanged?.Invoke(this, 100);
            }
            catch
            {
                SetStatus(EngineStatus.Error);
                throw;
            }
        }
        finally
        {
            _loadSemaphore.Release();
        }
    }

    public async Task<TranslationResult> TranslateAsync(string text, string sourceLang, string targetLang)
    {
        await _semaphore.WaitAsync();
        try
        {
            EnsureReady();

            var prompt = PromptBuilder.BuildTranslationPrompt(text, sourceLang, targetLang);
            Console.WriteLine($"[LlamaCppEngine] 翻译请求: source={sourceLang}, target={targetLang}, text={text}");
            Console.WriteLine($"[LlamaCppEngine] Prompt: {repr(prompt)}");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = await RunInferenceAsync(prompt);
            sw.Stop();

            var cleanResult = CleanResult(result);
            Console.WriteLine($"[LlamaCppEngine] 翻译结果: {repr(cleanResult)}");

            return new TranslationResult
            {
                TranslatedText = cleanResult,
                SourceText = text,
                SourceLanguage = sourceLang,
                TargetLanguage = targetLang,
                ModelName = Path.GetFileNameWithoutExtension(_currentModelPath),
                InferenceTimeMs = sw.ElapsedMilliseconds
            };
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<string> GenerateAsync(string prompt)
    {
        await _semaphore.WaitAsync();
        try
        {
            EnsureReady();
            var result = await RunInferenceAsync(prompt);
            return CleanResult(result);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task UnloadAsync()
    {
        await _loadSemaphore.WaitAsync();
        try
        {
            SetStatus(EngineStatus.Unloading);

            _executor = null;
            _context?.Dispose();
            _context = null;
            _weights?.Dispose();
            _weights = null;

            // llama_backend_free 在某些场景（如进程退出时 native DLL 已卸载）
            // 会抛 BadImageFormatException，属无害警告，吞掉即可
            try { NativeApi.llama_backend_free(); }
            catch { /* native 库可能已卸载，忽略 */ }

            SetStatus(EngineStatus.NotLoaded);
        }
        finally
        {
            _loadSemaphore.Release();
        }
    }

    private async Task<string> RunInferenceAsync(string prompt,
        float temperature = DefaultTemperature,
        float topP = DefaultTopP,
        int topK = DefaultTopK,
        float repetitionPenalty = DefaultRepetitionPenalty,
        int maxTokens = DefaultMaxTokens)
    {
        if (_executor == null)
            throw new InvalidOperationException("模型未加载");

        var samplingPipeline = new DefaultSamplingPipeline
        {
            Temperature = temperature,
            TopP = topP,
            TopK = topK,
            RepeatPenalty = repetitionPenalty,
            Seed = DefaultSeed
        };

        var inferenceParams = new InferenceParams
        {
            MaxTokens = maxTokens,
            AntiPrompts = PromptBuilder.GetStopTokens(),
            SamplingPipeline = samplingPipeline
        };

        var sb = new System.Text.StringBuilder();
        int tokenCount = 0;
        await foreach (var token in _executor.InferAsync(prompt, inferenceParams))
        {
            sb.Append(token);
            tokenCount++;
            if (tokenCount <= 20)
            {
                System.Diagnostics.Debug.WriteLine($"[LlamaCppEngine] Token {tokenCount}: {repr(token)}");
            }
        }

        var rawResult = sb.ToString();
        System.Diagnostics.Debug.WriteLine($"[LlamaCppEngine] 生成token总数: {tokenCount}");
        System.Diagnostics.Debug.WriteLine($"[LlamaCppEngine] 原始输出前500字符: {repr(rawResult.Length > 500 ? rawResult.Substring(0, 500) : rawResult)}");
        System.Diagnostics.Debug.WriteLine($"[LlamaCppEngine] 停止词列表: {string.Join(", ", PromptBuilder.GetStopTokens().Select(s => repr(s)))}");

        return rawResult;
    }

    private static string repr(string s)
    {
        return "\"" + s.Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";
    }

    private void EnsureReady()
    {
        if (_status != EngineStatus.Ready)
            throw new InvalidOperationException($"引擎未就绪，当前状态: {_status}");
    }

    private static string CleanResult(string raw)
    {
        var result = raw.Trim().Replace("\r", "").TrimStart('\n');

        var stopTokens = PromptBuilder.GetStopTokens();
        int earliestStopIdx = int.MaxValue;
        foreach (var stopToken in stopTokens)
        {
            var idx = result.IndexOf(stopToken, StringComparison.Ordinal);
            if (idx >= 0 && idx < earliestStopIdx)
                earliestStopIdx = idx;
        }
        if (earliestStopIdx < int.MaxValue)
            result = result.Substring(0, earliestStopIdx);

        return result.Trim();
    }

    public void Dispose()
    {
        try { UnloadAsync().Wait(TimeSpan.FromSeconds(5)); }
        catch { }
        _semaphore.Dispose();
        _loadSemaphore.Dispose();
    }
}
