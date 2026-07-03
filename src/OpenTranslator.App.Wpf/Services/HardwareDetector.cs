using System.Management;
using OpenTranslator.Models;
using OpenTranslator.Services.Interfaces;

namespace OpenTranslator.Services;

/// <summary>
/// 硬件检测器实现 - 检测CPU/GPU/RAM信息
/// </summary>
public class HardwareDetector : IHardwareDetector
{
    public HardwareInfo DetectHardware()
    {
        var info = new HardwareInfo
        {
            CpuCores = Environment.ProcessorCount,
            TotalMemoryMB = GetTotalMemoryMB(),
            Gpus = DetectGpu()
        };

        var (model, device) = GetRecommendedConfig(info);
        info.RecommendedModel = model;
        info.RecommendedDevice = device;

        return info;
    }

    public List<GpuInfo> DetectGpu()
    {
        var gpus = new List<GpuInfo>();

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
            foreach (var obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString() ?? "Unknown";
                var adapterRam = obj["AdapterRAM"] as uint? ?? 0;
                var driverVersion = obj["DriverVersion"]?.ToString() ?? "";

                var vendor = name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ? "NVIDIA"
                    : name.Contains("AMD", StringComparison.OrdinalIgnoreCase) || name.Contains("Radeon", StringComparison.OrdinalIgnoreCase) ? "AMD"
                    : name.Contains("Intel", StringComparison.OrdinalIgnoreCase) ? "Intel"
                    : "Unknown";

                gpus.Add(new GpuInfo
                {
                    Name = name,
                    Vendor = vendor,
                    VRamMB = adapterRam / (1024 * 1024),
                    SupportsCuda = vendor == "NVIDIA",
                    SupportsVulkan = true
                });
            }
        }
        catch
        {
            // WMI查询失败，返回空列表
        }

        return gpus;
    }

    public (string model, string device) GetRecommendedConfig(HardwareInfo info)
    {
        var hasGpu = info.Gpus.Count > 0;
        var bestGpu = info.Gpus.OrderByDescending(g => g.VRamMB).FirstOrDefault();

        // 有16GB+ VRAM的GPU → 推荐30B-A3B + GPU
        if (bestGpu != null && bestGpu.VRamMB >= 16384)
            return ("Hy-MT2-30B-A3B", "GPU");

        // 有8GB+ VRAM的GPU → 推荐7B + GPU
        if (bestGpu != null && bestGpu.VRamMB >= 8192)
            return ("Hy-MT2-7B-Q4_K_M", "GPU");

        // 有4GB+ VRAM的GPU → 推荐1.8B Q4 + GPU
        if (bestGpu != null && bestGpu.VRamMB >= 4096)
            return ("Hy-MT2-1.8B-Q4_K_M", "GPU");

        // 有2GB+ VRAM的GPU 或 8GB+ RAM → 推荐1.8B 2bit
        if (bestGpu != null && bestGpu.VRamMB >= 2048)
            return ("Hy-MT2-1.8B-2bit", "GPU");

        // 16GB+ RAM → 推荐7B + CPU
        if (info.TotalMemoryMB >= 16384)
            return ("Hy-MT2-7B-Q4_K_M", "CPU");

        // 8GB+ RAM → 推荐1.8B Q4 + CPU
        if (info.TotalMemoryMB >= 8192)
            return ("Hy-MT2-1.8B-Q4_K_M", "CPU");

        // 低配 → 推荐1.8B 1.25bit (440MB) + CPU
        return ("Hy-MT2-1.8B-1.25bit", "CPU");
    }

    private static long GetTotalMemoryMB()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem");
            foreach (var obj in searcher.Get())
            {
                var totalMemoryKB = obj["TotalVisibleMemorySize"] as ulong? ?? 0;
                return (long)(totalMemoryKB / 1024);
            }
        }
        catch { }
        return 8192; // 默认8GB
    }
}