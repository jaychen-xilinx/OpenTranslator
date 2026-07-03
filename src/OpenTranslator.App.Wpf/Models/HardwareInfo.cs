namespace OpenTranslator.Models;

/// <summary>
/// 硬件检测信息
/// </summary>
public class HardwareInfo
{
    public int CpuCores { get; set; }
    public long TotalMemoryMB { get; set; }
    public List<GpuInfo> Gpus { get; set; } = [];
    public string RecommendedModel { get; set; } = "Hy-MT2-1.8B";
    public string RecommendedDevice { get; set; } = "CPU";
}

public class GpuInfo
{
    public string Name { get; set; } = string.Empty;
    public string Vendor { get; set; } = string.Empty;
    public long VRamMB { get; set; }
    public bool SupportsCuda { get; set; }
    public bool SupportsVulkan { get; set; }
}