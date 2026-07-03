namespace OpenTranslator.Models;

/// <summary>
/// 模型信息
/// </summary>
public class ModelInfo
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
    public string ParametersCount { get; set; } = string.Empty;
    public long RequiredVRamMB { get; set; }
    public long RequiredRamMB { get; set; }
    public bool IsDownloaded { get; set; }
    public bool IsLoaded { get; set; }
}

public class ModelDownloadProgress
{
    public string ModelName { get; set; } = string.Empty;
    public long TotalBytes { get; set; }
    public long DownloadedBytes { get; set; }
    public double ProgressPercent => TotalBytes > 0 ? (double)DownloadedBytes / TotalBytes * 100 : 0;
    public string Status { get; set; } = "Pending";
    public string? ErrorMessage { get; set; }
}