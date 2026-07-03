# OpenTranslator

基于 HY-MT2 模型的离线桌面翻译工具，支持划词翻译和原地替换功能。

## ✨ 功能特性

- 🎯 **主窗口翻译**：输入文本即可翻译，支持自动检测源语言
- 🔍 **划词翻译**：选中任意文本后按快捷键，悬浮窗显示翻译结果
- 🔄 **原地替换**：翻译后直接将译文替换到选中位置
- 🎨 **双栏布局**：支持左右分栏和上下分栏切换
- ⌨️ **自定义快捷键**：所有快捷键均可在设置中自定义
- 📱 **后台运行**：支持最小化到托盘，随时响应快捷键
- 💾 **翻译历史**：自动保存翻译记录，方便查阅

## 🚀 快速开始

### 1. 下载模型

从以下地址下载 HY-MT2 模型文件（GGUF 格式）：

- **HuggingFace**: [tencent/Hy-MT2-1.8B-GGUF](https://huggingface.co/tencent/Hy-MT2-1.8B-GGUF)
- **魔搭社区（国内推荐）**: [Tencent-Hunyuan/Hy-MT2-1.8B-GGUF](https://www.modelscope.cn/models/Tencent-Hunyuan/Hy-MT2-1.8B-GGUF)

推荐下载 `Hy-MT2-1.8B-Q4_K_M.gguf`（约 1.2GB），将其放置到 `models/` 目录下。

### 2. 运行程序

#### 方式一：下载预编译版本

从 GitHub Releases 页面下载最新版本，解压后双击 `OpenTranslator.exe` 启动。

#### 方式二：从源码编译

```bash
# 克隆仓库
git clone https://github.com/yourusername/OpenTranslator.git
cd OpenTranslator

# 安装依赖并编译
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# 运行
./bin/OpenTranslator.exe
```

## 📖 使用说明

### 主窗口翻译

1. 在左侧输入框输入需要翻译的文本
2. 点击「翻译」按钮或按 Enter 键
3. 右侧显示翻译结果

### 划词翻译

1. 在任意应用中选中文本
2. 按默认快捷键 `Ctrl+Alt+B`
3. 悬浮窗显示翻译结果

### 原地替换

1. 在任意应用中选中文本
2. 按默认快捷键 `Ctrl+Alt+V`
3. 译文自动替换到选中位置

### 快捷键

| 功能 | 默认快捷键 | 可自定义 |
|------|-----------|---------|
| 划词翻译 | Ctrl+Alt+B | ✅ |
| 原地替换 | Ctrl+Alt+V | ✅ |
| 截图翻译 | Ctrl+Shift+O | ✅ |
| 词典查询 | Ctrl+D | ✅ |

## ⚙️ 配置

配置文件位于 `config.json`：

```json
{
  "DefaultModel": "Hy-MT2-1.8B-Q4_K_M",
  "SourceLanguage": "auto",
  "TargetLanguage": "zh",
  "AutoDetectLanguage": true,
  "Theme": "System",
  "StartMinimized": false,
  "HotKeys": {
    "TranslateHotKey": "Ctrl+Alt+B",
    "ReplaceHotKey": "Ctrl+Alt+V",
    "ScreenshotHotKey": "Ctrl+Shift+O",
    "DictionaryHotKey": "Ctrl+D"
  },
  "ModelsDirectory": "models",
  "MainWindowLayout": 0
}
```

## 🛠️ 系统要求

- Windows 10/11 x64
- .NET 8.0 Runtime（预编译版本已内置）
- 内存：8GB+（推荐 16GB）
- 显卡：可选，支持 CUDA 加速

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

## 📄 许可证

本项目采用 [MIT License](LICENSE) 开源协议。

## 🙏 致谢

- [HY-MT2](https://github.com/Tencent/Hy-MT2) - 腾讯混元机器翻译模型
- [LLamaSharp](https://github.com/SciSharp/LLamaSharp) - .NET 版 llama.cpp 封装
- [CommunityToolkit](https://github.com/CommunityToolkit/dotnet) - .NET 工具库