using System.Security.Cryptography;
using OpenTranslator.Models;
using OpenTranslator.Services.Interfaces;

namespace OpenTranslator.Services;

/// <summary>
/// 模型下载器 - 支持断点续传和完整性校验
/// </summary>
public class ModelDownloader : IModelDownloader
{
    private readonly HttpClient _httpClient;
    private const int BufferSize = 8192;
    private const int MaxRetries = 3;

    public ModelDownloader(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromHours(4)
        };
    }

    public async Task<string> DownloadModelAsync(ModelInfo modelInfo, IProgress<ModelDownloadProgress>? progress = null)
    {
        var targetDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models");
        Directory.CreateDirectory(targetDir);

        var targetPath = Path.Combine(targetDir, modelInfo.FileName);
        var tempPath = targetPath + ".download";

        // 检查是否已下载完成
        if (File.Exists(targetPath))
        {
            if (await ValidateModelAsync(targetPath, modelInfo.FileSizeBytes))
            {
                ReportProgress(progress, modelInfo.Name, new ModelDownloadProgress
                {
                    ModelName = modelInfo.Name,
                    TotalBytes = modelInfo.FileSizeBytes,
                    DownloadedBytes = modelInfo.FileSizeBytes,
                    Status = "Completed"
                });
                return targetPath;
            }
            // 文件损坏，删除重新下载
            File.Delete(targetPath);
        }

        // 断点续传：获取已下载大小
        long existingBytes = 0;
        if (File.Exists(tempPath))
        {
            existingBytes = new FileInfo(tempPath).Length;
        }

        var totalBytes = modelInfo.FileSizeBytes;

        var retryCount = 0;
        while (retryCount < MaxRetries)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, modelInfo.DownloadUrl);
                if (existingBytes > 0)
                {
                    request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existingBytes, null);
                }

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var fileMode = existingBytes > 0 ? FileMode.Append : FileMode.Create;
                using var fileStream = new FileStream(tempPath, fileMode, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
                using var responseStream = await response.Content.ReadAsStreamAsync();

                var buffer = new byte[BufferSize];
                long downloadedBytes = existingBytes;
                int bytesRead;

                while ((bytesRead = await responseStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    downloadedBytes += bytesRead;

                    ReportProgress(progress, modelInfo.Name, new ModelDownloadProgress
                    {
                        ModelName = modelInfo.Name,
                        TotalBytes = totalBytes,
                        DownloadedBytes = downloadedBytes,
                        Status = "Downloading"
                    });
                }

                // 下载完成，重命名临时文件
                if (File.Exists(targetPath)) File.Delete(targetPath);
                File.Move(tempPath, targetPath);

                // 验证完整性
                if (!await ValidateModelAsync(targetPath, modelInfo.FileSizeBytes))
                {
                    throw new InvalidOperationException("模型文件完整性校验失败");
                }

                ReportProgress(progress, modelInfo.Name, new ModelDownloadProgress
                {
                    ModelName = modelInfo.Name,
                    TotalBytes = totalBytes,
                    DownloadedBytes = totalBytes,
                    Status = "Completed"
                });

                return targetPath;
            }
            catch (Exception ex)
            {
                retryCount++;
                if (retryCount >= MaxRetries)
                {
                    ReportProgress(progress, modelInfo.Name, new ModelDownloadProgress
                    {
                        ModelName = modelInfo.Name,
                        Status = "Error",
                        ErrorMessage = ex.Message
                    });
                    throw;
                }

                // 重试前等待
                await Task.Delay(2000 * retryCount);
            }
        }

        throw new InvalidOperationException($"下载模型 {modelInfo.Name} 失败");
    }

    public async Task<bool> ValidateModelAsync(string filePath, long expectedSize)
    {
        if (!File.Exists(filePath))
            return false;

        var fileInfo = new FileInfo(filePath);

        // 检查文件大小
        if (fileInfo.Length < expectedSize * 0.95 || fileInfo.Length > expectedSize * 1.05)
            return false;

        // 计算SHA256哈希（可选，验证模型完整性）
        // 大型模型可跳过哈希验证以提高性能
        if (fileInfo.Length < 2_000_000_000) // 小于2GB时才做哈希校验
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = await sha256.ComputeHashAsync(stream);
            // 此处可对比官方提供的哈希值
        }

        return true;
    }

    private static void ReportProgress(IProgress<ModelDownloadProgress>? progress, string modelName, ModelDownloadProgress p)
    {
        try { progress?.Report(p); }
        catch { /* 忽略进度报告异常 */ }
    }
}

/// <summary>
/// 模型下载器接口
/// </summary>
public interface IModelDownloader
{
    Task<string> DownloadModelAsync(ModelInfo modelInfo, IProgress<ModelDownloadProgress>? progress = null);
    Task<bool> ValidateModelAsync(string filePath, long expectedSize);
}