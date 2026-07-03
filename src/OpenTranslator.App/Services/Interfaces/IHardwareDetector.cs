using OpenTranslator.Models;

namespace OpenTranslator.Services.Interfaces;

/// <summary>
/// 硬件检测器接口 - 检测CPU/GPU/RAM信息
/// </summary>
public interface IHardwareDetector
{
    /// <summary>检测硬件信息</summary>
    HardwareInfo DetectHardware();

    /// <summary>检测GPU信息</summary>
    List<GpuInfo> DetectGpu();

    /// <summary>获取推荐的模型和设备</summary>
    (string model, string device) GetRecommendedConfig(HardwareInfo info);
}