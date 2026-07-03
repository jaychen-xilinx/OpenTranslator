# OpenTranslator Code Wiki

## 目录

- [1. 项目概述](#1-项目概述)
- [2. 技术栈](#2-技术栈)
- [3. 项目架构](#3-项目架构)
- [4. 目录结构](#4-目录结构)
- [5. 核心模块详解](#5-核心模块详解)
- [6. 关键类与函数说明](#6-关键类与函数说明)
- [7. 依赖关系](#7-依赖关系)
- [8. 配置与运行](#8-配置与运行)
- [9. 开发指南](#9-开发指南)

---

## 1. 项目概述

### 1.1 项目简介

OpenTranslator 是一款基于本地大语言模型的离线翻译工具，采用 WPF 框架构建，支持 37 种语言互译。项目核心使用腾讯混元 Hy-MT2 翻译模型，通过 LLamaSharp 调用 llama.cpp 进行本地推理，完全离线运行，保护用户隐私。

### 1.2 核心特性

- **完全离线**：所有翻译在本地完成，无需联网
- **多模型支持**：支持 Hy-MT2 1.8B / 7B / 30B-A3B 等多种规格模型
- **划词翻译**：全局热键 (Ctrl+Alt+Q) 触发选中文本翻译，悬浮窗显示结果
- **原地替换**：热键 (Ctrl+Shift+T) 翻译后直接替换选中文本
- **翻译历史**：SQLite 本地存储翻译记录，支持搜索和收藏
- **智能语言检测**：基于 Unicode 范围的自动语言识别
- **硬件自适应**：根据 CPU/GPU/内存配置自动推荐最优模型
- **双布局模式**：支持左右布局和上下布局切换

### 1.3 支持语言

支持 37 种语言，包括：中文、英语、日语、韩语、法语、德语、西班牙语、葡萄牙语、俄语、阿拉伯语、泰语、越南语、印地语、繁体中文、粤语、藏语、维吾尔语、蒙古语、哈萨克语等。

---

## 2. 技术栈

### 2.1 开发框架

| 技术 | 版本 | 用途 |
|------|------|------|
| .NET | 8.0 | 运行时框架 |
| WPF | - | UI 框架 (Windows 桌面) |
| C# | 12.0 | 开发语言 |

### 2.2 核心依赖库

| 库名 | 版本 | 用途 |
|------|------|------|
| LLamaSharp | 0.27.0 | .NET 封装的 llama.cpp 绑定 |
| LLamaSharp.Backend.Cpu | 0.27.0 | CPU 后端原生库 |
| CommunityToolkit.Mvvm | 8.3.2 | MVVM 工具包 |
| Microsoft.Extensions.DependencyInjection | 8.0.1 | 依赖注入容器 |
| Microsoft.ML.OnnxRuntime | 1.20.0 | ONNX 运行时（语言检测） |
| sqlite-net-pcl | 1.9.172 | SQLite 数据库 ORM |
| System.Management | 8.0.0 | WMI 硬件检测 |

### 2.3 AI 模型

| 模型 | 参数 | 大小 | 显存需求 |
|------|------|------|----------|
| Hy-MT2-1.8B-Q4_K_M | 1.8B | ~1.5GB | 2GB |
| Hy-MT2-1.8B-2bit | 1.8B | 574MB | 1GB |
| Hy-MT2-1.8B-1.25bit | 1.8B | 440MB | 512MB |
| Hy-MT2-7B-Q4_K_M | 7B | ~3.8GB | 4GB |
| Hy-MT2-30B-A3B | 30B MoE | ~16GB | 16GB |

### 2.4 模型推理参数（Hy-MT2 推荐值）

| 参数 | 默认值 | 说明 |
|------|--------|------|
| temperature | 0.7 | 温度，控制随机性 |
| top_p | 0.6 | 核采样阈值 |
| top_k | 20 | Top-K 采样 |
| repetition_penalty | 1.05 | 重复惩罚 |
| max_tokens | 4096 | 最大生成 token 数 |
| context_size | 4096 | 上下文窗口大小 |

---

## 3. 项目架构

### 3.1 整体架构图

```
┌─────────────────────────────────────────────────────────┐
│                      UI 层 (WPF)                        │
│  ┌─────────────┐  ┌─────────────┐  ┌───────────────┐   │
│  │ MainWindow  │  │SettingsWin  │  │SelectionPopup │   │
│  └─────────────┘  └─────────────┘  └───────────────┘   │
└────────────────────────┬────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────┐
│                   ViewModel 层                          │
│  ┌──────────────────────────────────────────────────┐   │
│  │              MainViewModel                        │   │
│  │  - 翻译命令绑定                                   │   │
│  │  - 历史记录管理                                   │   │
│  │  - 模型切换                                      │   │
│  │  - 语言选择                                      │   │
│  └──────────────────────────────────────────────────┘   │
└────────────────────────┬────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────┐
│                    Service 层                           │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  │
│  │TranslationSvc│  │ ModelManager │  │ HotKeyService│  │
│  └──────────────┘  └──────────────┘  └──────────────┘  │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  │
│  │SelectionTran │  │HistoryService│  │HardwareDetect│  │
│  └──────────────┘  └──────────────┘  └──────────────┘  │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  │
│  │LanguageDetect│  │ModelDownloader│ │AppConfigSvc  │  │
│  └──────────────┘  └──────────────┘  └──────────────┘  │
└────────────────────────┬────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────┐
│                    Core Engine 层                       │
│  ┌──────────────────────────────────────────────────┐   │
│  │              LlamaCppEngine                      │   │
│  │  - LLamaWeights / LLamaContext                   │   │
│  │  - StatelessExecutor                             │   │
│  │  - 推理参数配置                                   │   │
│  └──────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────┐   │
│  │              PromptBuilder                       │   │
│  │  - Hy-MT2 Chat Template                         │   │
│  │  - 翻译提示词构建                                │   │
│  │  - 多模型模板适配                                │   │
│  └──────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────┘
```

### 3.2 架构设计原则

1. **分层架构**：清晰的 UI → ViewModel → Service → Engine 四层分离
2. **依赖注入**：使用 Microsoft.Extensions.DependencyInjection 管理对象生命周期
3. **面向接口**：核心服务均定义接口，便于测试和替换实现
4. **MVVM 模式**：UI 与业务逻辑通过数据绑定解耦
5. **异步优先**：所有耗时操作（模型加载、翻译推理）均采用 async/await

---

## 4. 目录结构

### 4.1 项目根目录

```
f:\OpenTranslator\
├── models/                          # 模型文件目录
│   ├── Hy-MT2-1.8B-Q4_K_M.gguf     # 1.8B Q4 模型
│   ├── Hy-MT2-7B-Q4_K_M.gguf       # 7B Q4 模型
│   └── .cache/                      # 模型下载缓存
├── references/                      # 参考资料
│   ├── Hy-MT2-main/                 # Hy-MT2 官方源码参考
│   ├── 开发计划.md
│   ├── 竞品分析.md
│   └── 迁移计划.md
├── src/
│   ├── OpenTranslator.App/          # WinUI 3 版本（已弃用，存在稳定性问题）
│   └── OpenTranslator.App.Wpf/      # WPF 版本（主版本）
├── docs/                            # 文档目录（本文件所在目录）
├── OpenTranslator.sln               # Visual Studio 解决方案
└── OpenTranslator.code-workspace    # VS Code 工作区
```

### 4.2 WPF 项目源码结构

```
OpenTranslator.App.Wpf/
├── App.xaml / App.xaml.cs           # 应用入口，DI 容器配置
├── AssemblyInfo.cs                  # 程序集信息
├── GlobalUsings.cs                  # 全局 using
├── MainWindow.xaml / .cs            # 主窗口
├── OpenTranslator.App.Wpf.csproj    # 项目文件
│
├── Helpers/                         # 辅助工具类
│   ├── Constants.cs                 # 全局常量（模型定义、语言列表、推理参数）
│   ├── NativeMethods.cs             # Win32 API P/Invoke 声明
│   └── PromptBuilder.cs             # 翻译提示词构建器
│
├── Models/                          # 数据模型
│   ├── AppConfig.cs                 # 应用配置
│   ├── HardwareInfo.cs              # 硬件信息
│   ├── LanguagePair.cs              # 语言对
│   ├── ModelInfo.cs                 # 模型信息
│   ├── TranslationHistoryItem.cs    # 翻译历史项
│   └── TranslationResult.cs         # 翻译结果
│
├── Services/                        # 业务服务
│   ├── Interfaces/                  # 服务接口
│   │   ├── IHardwareDetector.cs
│   │   ├── ILanguageDetector.cs
│   │   ├── IModelManager.cs
│   │   └── ITranslationEngine.cs
│   ├── AppConfigService.cs          # 配置管理服务
│   ├── HardwareDetector.cs          # 硬件检测服务
│   ├── HotKeyService.cs             # 全局热键服务
│   ├── LanguageDetector.cs          # 语言检测服务
│   ├── LlamaCppEngine.cs            # llama.cpp 翻译引擎
│   ├── ModelDownloader.cs           # 模型下载器
│   ├── ModelManager.cs              # 模型管理器
│   ├── SelectionTranslator.cs       # 划词翻译服务
│   ├── TranslationHistoryService.cs # 翻译历史服务
│   └── TranslationService.cs        # 翻译服务（业务协调层）
│
├── ViewModels/                      # 视图模型
│   └── MainViewModel.cs             # 主窗口 ViewModel
│
└── Views/                           # 视图（窗口）
    ├── SelectionPopupWindow.xaml / .cs  # 划词翻译悬浮窗
    └── SettingsWindow.xaml / .cs        # 设置窗口
```

---

## 5. 核心模块详解

### 5.1 翻译引擎模块 (LlamaCppEngine)

**位置**: [LlamaCppEngine.cs](file:///f:/OpenTranslator/src/OpenTranslator.App.Wpf/Services/LlamaCppEngine.cs)

**职责**:
- 封装 LLamaSharp 进行模型加载和推理
- 管理模型生命周期（加载/卸载/切换）
- 执行翻译推理并返回结果
- 提供推理进度和状态事件

**核心实现**:
- 使用 `SemaphoreSlim` 实现线程安全，避免 async 锁问题
- 双信号量设计：`_semaphore` 保护推理，`_loadSemaphore` 保护模型加载
- 使用 `StatelessExecutor` 进行无状态推理
- 支持 GPU 加速（通过 `GpuLayerCount` 配置）

**状态机**:
```
NotLoaded → Loading → Ready → Unloading → NotLoaded
                    ↓
                   Error
```

### 5.2 翻译服务模块 (TranslationService)

**位置**: [TranslationService.cs](file:///f:/OpenTranslator/src/OpenTranslator.App.Wpf/Services/TranslationService.cs)

**职责**:
- 业务协调层，封装翻译流程
- 自动语言检测
- LRU 翻译缓存（默认 1000 条）
- 配置读取与应用

**翻译流程**:
1. 验证输入文本非空
2. 从配置读取默认语言
3. 自动检测源语言（如果设置为 auto）
4. 查询翻译缓存
5. 调用翻译引擎执行翻译
6. 结果存入缓存
7. 返回翻译结果

### 5.3 模型管理模块 (ModelManager)

**位置**: [ModelManager.cs](file:///f:/OpenTranslator/src/OpenTranslator.App.Wpf/Services/ModelManager.cs)

**职责**:
- 模型发现与枚举
- 模型下载（委托给 ModelDownloader）
- 模型加载与切换
- 模型完整性验证
- 模型目录自动发现

**模型目录查找策略**:
1. 程序基目录下的 `models/` 文件夹
2. 逐级向上查找包含 `.gguf` 文件的 `models/` 文件夹
3. 都没找到则在基目录创建空的 `models/` 文件夹

### 5.4 全局热键模块 (HotKeyService)

**位置**: [HotKeyService.cs](file:///f:/OpenTranslator/src/OpenTranslator.App.Wpf/Services/HotKeyService.cs)

**职责**:
- 注册/注销全局热键
- 管理热键消息窗口
- 分发热键事件

**实现方式**:
- 通过 Win32 API `RegisterHotKey` 注册全局热键
- 创建隐藏的消息窗口接收 `WM_HOTKEY` 消息
- 窗口类名包含进程 ID，避免多实例冲突
- 支持 `MOD_NOREPEAT` 防止自动重复触发

**支持的热键**:

| 热键 ID | 功能 | 默认快捷键 |
|---------|------|------------|
| HOTKEY_ID_TRANSLATE (1) | 划词翻译 | Ctrl+Alt+Q |
| HOTKEY_ID_REPLACE (2) | 原地替换翻译 | Ctrl+Shift+T |
| HOTKEY_ID_SCREENSHOT (3) | 截图翻译（预留） | Ctrl+Shift+O |
| HOTKEY_ID_DICTIONARY (4) | 词典查询（预留） | Ctrl+D |

### 5.5 划词翻译模块 (SelectionTranslator)

**位置**: [SelectionTranslator.cs](file:///f:/OpenTranslator/src/OpenTranslator.App.Wpf/Services/SelectionTranslator.cs)

**职责**:
- 响应全局热键触发
- 获取当前选中文本
- 执行翻译并显示悬浮结果窗
- 支持原地替换（复制译文 + 模拟 Ctrl+V）

**选中文本获取方式**:
- 通过事件让 UI 线程在剪贴板上操作
- 保存原剪贴板 → 清空 → 模拟 Ctrl+C → 读取 → 恢复剪贴板
- 最多重试 2 次

### 5.6 翻译历史模块 (TranslationHistoryService)

**位置**: [TranslationHistoryService.cs](file:///f:/OpenTranslator/src/OpenTranslator.App.Wpf/Services/TranslationHistoryService.cs)

**职责**:
- SQLite 本地存储翻译历史
- 支持关键词搜索
- 收藏/取消收藏
- 分页加载
- 数据库索引优化

**数据库表结构**: `TranslationHistoryItem`

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | int (PK, AutoInc) | 主键 |
| SourceText | string | 源文本 |
| TranslatedText | string | 译文 |
| SourceLanguage | string | 源语言代码 |
| TargetLanguage | string | 目标语言代码 |
| ModelName | string | 使用的模型 |
| CreatedAt | DateTime | 创建时间 |
| IsFavorite | bool | 是否收藏 |

**索引**:
- `idx_history_created` (CreatedAt DESC) - 加速时间排序查询
- `idx_history_favorite` (IsFavorite) - 加速收藏查询

### 5.7 语言检测模块 (LanguageDetector)

**位置**: [LanguageDetector.cs](file:///f:/OpenTranslator/src/OpenTranslator.App.Wpf/Services/LanguageDetector.cs)

**职责**:
- 自动检测文本的语言
- ONNX fasttext 模型（可选）
- Unicode 范围特征检测（回退方案）

**检测优先级**:
1. ONNX 模型检测（如果模型文件存在且加载成功）
2. 基于 Unicode 字符范围的规则检测

**支持检测的语言**：中文、日语、韩语、俄语、阿拉伯语、泰语、印地语、希伯来语、英语

### 5.8 硬件检测模块 (HardwareDetector)

**位置**: [HardwareDetector.cs](file:///f:/OpenTranslator/src/OpenTranslator.App.Wpf/Services/HardwareDetector.cs)

**职责**:
- 检测 CPU 核心数
- 检测系统总内存
- 检测 GPU 信息（厂商、显存、CUDA 支持）
- 根据硬件配置推荐最优模型和设备

**推荐策略**:

| 硬件条件 | 推荐模型 | 设备 |
|----------|----------|------|
| VRAM ≥ 16GB | Hy-MT2-30B-A3B | GPU |
| VRAM ≥ 8GB | Hy-MT2-7B-Q4_K_M | GPU |
| VRAM ≥ 4GB | Hy-MT2-1.8B-Q4_K_M | GPU |
| VRAM ≥ 2GB | Hy-MT2-1.8B-2bit | GPU |
| RAM ≥ 16GB | Hy-MT2-7B-Q4_K_M | CPU |
| RAM ≥ 8GB | Hy-MT2-1.8B-Q4_K_M | CPU |
| 其他 | Hy-MT2-1.8B-1.25bit | CPU |

### 5.9 提示词构建模块 (PromptBuilder)

**位置**: [PromptBuilder.cs](file:///f:/OpenTranslator/src/OpenTranslator.App.Wpf/Helpers/PromptBuilder.cs)

**职责**:
- 构建符合 Hy-MT2 规范的翻译提示词
- 管理不同模型的 Chat Template
- 支持多种翻译模式（普通、术语表、风格控制、结构化等）

**支持的 Chat Template**:

| 模板类型 | 适用模型 | BOS Token | 停止词 |
|----------|----------|-----------|--------|
| Dense18B | 1.8B/0.5B/4B Dense | `<｜hy_begin▁of▁sentence｜>` | `<｜hy_place▁holder▁no▁2｜>` |
| Dense7B | 7B Dense | `<|startoftext|>` | `<|eos|>` |
| V3MoE | 7B/30B-A3B MoE | `<｜hy_begin▁of▁sentence｜>` | `<｜hy_eos｜>` |

**支持的提示词类型**:
- 基础翻译（DefaultTranslateZh）
- 术语表翻译（GlossaryZh）
- 风格控制翻译（StyleZh）
- 分隔符保持翻译（DelimitersZh）
- 结构化数据翻译（StructuredZh）
- 摘要（SummarizeZh）
- 重写（RewriteZh）
- 润色（PolishZh）
- 反向翻译检测（ReverseCheckZh）

---

## 6. 关键类与函数说明

### 6.1 LlamaCppEngine

**类定义**: `public class LlamaCppEngine : ITranslationEngine, IDisposable`

**重要方法**:

| 方法 | 签名 | 说明 |
|------|------|------|
| InitializeAsync | `Task InitializeAsync(string modelPath, HardwareInfo hardwareInfo)` | 加载模型并初始化引擎 |
| TranslateAsync | `Task<TranslationResult> TranslateAsync(string text, string sourceLang, string targetLang)` | 执行翻译 |
| GenerateAsync | `Task<string> GenerateAsync(string prompt)` | 自定义 prompt 生成 |
| UnloadAsync | `Task UnloadAsync()` | 卸载模型释放资源 |
| GetStatus | `EngineStatus GetStatus()` | 获取当前引擎状态 |

**重要事件**:
- `LoadProgressChanged` - 模型加载进度
- `StatusChanged` - 引擎状态变更

### 6.2 TranslationService

**类定义**: `public class TranslationService`

**重要方法**:

| 方法 | 签名 | 说明 |
|------|------|------|
| TranslateAsync | `Task<TranslationResult> TranslateAsync(string text, string? sourceLang = null, string? targetLang = null)` | 执行翻译（含自动检测和缓存） |
| DetectLanguage | `string DetectLanguage(string text)` | 检测语言 |
| GetDetectionConfidence | `double GetDetectionConfidence()` | 获取检测置信度 |
| GetEngineStatus | `EngineStatus GetEngineStatus()` | 获取引擎状态 |

**内部类**: `TranslationCache` - LRU 缓存实现

### 6.3 MainViewModel

**类定义**: `public class MainViewModel : INotifyPropertyChanged`

**重要属性**:

| 属性 | 类型 | 说明 |
|------|------|------|
| SourceText | string | 源文本 |
| TranslatedText | string | 译文 |
| SourceLanguage | LanguagePair | 源语言 |
| TargetLanguage | LanguagePair | 目标语言 |
| IsTranslating | bool | 是否正在翻译 |
| CanTranslate | bool | 是否可翻译 |
| EngineStatus | EngineStatus | 引擎状态 |
| CurrentModel | ModelInfo? | 当前模型 |
| HistoryItems | ObservableCollection\<TranslationHistoryItem\> | 历史记录 |

**重要命令**:

| 命令 | 说明 |
|------|------|
| TranslateCommand | 执行翻译 |
| SwapLanguagesCommand | 交换源/目标语言 |
| ClearTextCommand | 清空文本 |
| CopyResultCommand | 复制译文 |
| SearchHistoryCommand | 搜索历史 |
| DeleteHistoryCommand | 删除历史记录 |
| ToggleFavoriteCommand | 切换收藏 |
| ClearHistoryCommand | 清空历史 |

### 6.4 PromptBuilder

**静态类**: `public static class PromptBuilder`

**重要方法**:

| 方法 | 签名 | 说明 |
|------|------|------|
| BuildTranslationPrompt | `string BuildTranslationPrompt(string text, string sourceLang, string targetLang, Dictionary<string, string>? glossary = null)` | 构建翻译提示词 |
| BuildStyleTranslationPrompt | `string BuildStyleTranslationPrompt(string text, string targetLang, string style)` | 构建风格控制提示词 |
| BuildStructuredPrompt | `string BuildStructuredPrompt(string text, string targetLang, string formatType, string contextLabel)` | 构建结构化翻译提示词 |
| DetectTemplateFromModelName | `ModelTemplateType DetectTemplateFromModelName(string modelName)` | 根据模型名推断模板类型 |
| SetModelTemplate | `void SetModelTemplate(ModelTemplateType templateType)` | 设置当前使用的模板 |
| GetStopTokens | `string[] GetStopTokens()` | 获取当前模板的停止词列表 |
| WrapChatTemplate | `string WrapChatTemplate(string userMessage)` | 用 chat template 包装用户消息 |

### 6.5 HotKeyService

**类定义**: `public class HotKeyService : IDisposable`

**重要方法**:

| 方法 | 签名 | 说明 |
|------|------|------|
| Start | `bool Start()` | 启动热键服务 |
| Stop | `void Stop()` | 停止热键服务 |
| ReregisterHotKeys | `bool ReregisterHotKeys()` | 重新注册热键（配置变更后） |

**重要事件**:
- `TranslateHotKeyPressed` - 划词翻译热键触发
- `ReplaceHotKeyPressed` - 原地替换热键触发
- `ScreenshotHotKeyPressed` - 截图翻译热键触发
- `DictionaryHotKeyPressed` - 词典热键触发

### 6.6 NativeMethods

**静态类**: `public static partial class NativeMethods`

**Win32 API 分类**:

| 类别 | 函数/常量 |
|------|-----------|
| 键盘钩子 | SetWindowsHookEx, UnhookWindowsHookEx, CallNextHookEx |
| 全局原子 | GlobalAddAtom, GlobalDeleteAtom |
| 窗口操作 | GetForegroundWindow, SetForegroundWindow, WindowFromPoint, GetCursorPos |
| 模拟输入 | keybd_event, VK_CONTROL, KEYEVENTF_KEYUP |
| 全局热键 | RegisterHotKey, UnregisterHotKey, WM_HOTKEY, MOD_ALT/CONTROL/SHIFT |
| 窗口创建 | RegisterClassEx, CreateWindowEx, DefWindowProc, DestroyWindow |
| 进程通信 | SendMessage, PostMessage, WM_COPY |

---

## 7. 依赖关系

### 7.1 服务依赖注入关系

```
App (DI 容器)
│
├── AppConfigService (Singleton)
│
├── ITranslationEngine → LlamaCppEngine (Singleton)
│   └── 依赖: PromptBuilder, LLamaSharp
│
├── IModelDownloader → ModelDownloader (Singleton)
│   └── 依赖: HttpClient
│
├── IModelManager → ModelManager (Singleton)
│   ├── 依赖: IModelDownloader
│   └── 依赖: ITranslationEngine
│
├── HardwareDetector (Singleton)
│   └── 依赖: System.Management (WMI)
│
├── ILanguageDetector → LanguageDetector (Singleton)
│   └── 依赖: Microsoft.ML.OnnxRuntime
│
├── TranslationService (Singleton)
│   ├── 依赖: ITranslationEngine
│   ├── 依赖: ILanguageDetector
│   ├── 依赖: AppConfigService
│   └── 内部: TranslationCache
│
├── ITranslationHistoryService → TranslationHistoryService (Singleton)
│   └── 依赖: SQLite-net-pcl
│
├── HotKeyService (Singleton)
│   ├── 依赖: AppConfigService
│   └── 依赖: NativeMethods (Win32 API)
│
├── SelectionTranslator (Singleton)
│   ├── 依赖: TranslationService
│   └── 依赖: AppConfigService
│
└── MainViewModel (Singleton)
    ├── 依赖: TranslationService
    ├── 依赖: IModelManager
    ├── 依赖: AppConfigService
    ├── 依赖: HardwareDetector
    └── 依赖: ITranslationHistoryService
```

### 7.2 数据流向（翻译流程）

```
用户输入文本
    │
    ▼
MainViewModel.TranslateCommand
    │
    ▼
TranslationService.TranslateAsync
    │
    ├─→ 检查缓存 (TranslationCache)
    │      ├─ 命中 → 直接返回
    │      └─ 未命中 → 继续
    │
    ├─→ 语言检测 (LanguageDetector)
    │      └─ (如果 sourceLang == "auto")
    │
    └─→ 调用引擎 (LlamaCppEngine.TranslateAsync)
            │
            ├─ PromptBuilder.BuildTranslationPrompt
            │      └─ 构建 Hy-MT2 格式提示词
            │
            ├─ StatelessExecutor.InferAsync
            │      └─ LLamaSharp → llama.dll 推理
            │
            └─ CleanResult (清理停止词)
    │
    ▼
返回 TranslationResult
    │
    ▼
MainViewModel 更新 UI
    ├── TranslatedText = result.TranslatedText
    ├── LastInferenceTimeMs = result.InferenceTimeMs
    └── 保存到历史 (TranslationHistoryService.AddAsync)
```

### 7.3 模型切换流程

```
用户选择新模型
    │
    ▼
MainViewModel.CurrentModel setter
    │
    ▼
MainViewModel.SwitchModelAsync
    │
    └─→ ModelManager.SwitchModelAsync
            │
            ├─ 如果当前有模型已加载
            │   └─→ UnloadCurrentModelAsync
            │         └─ ITranslationEngine.UnloadAsync
            │
            └─→ LoadModelAsync
                  ├── HardwareDetector.DetectHardware
                  └── ITranslationEngine.InitializeAsync
                        ├── LLamaWeights.LoadFromFile
                        ├── LLamaWeights.CreateContext
                        ├── new StatelessExecutor
                        └── PromptBuilder.SetModelTemplate
```

---

## 8. 配置与运行

### 8.1 配置文件

**路径**: `{程序目录}/config.json`

**配置项说明**:

```json
{
  "DefaultModel": "Hy-MT2-1.8B-Q4_K_M",
  "SourceLanguage": "auto",
  "TargetLanguage": "zh",
  "AutoDetectLanguage": true,
  "Theme": "System",
  "StartMinimized": false,
  "HotKeys": {
    "TranslateHotKey": "Ctrl+Alt+Q",
    "ReplaceHotKey": "Ctrl+Shift+T",
    "ScreenshotHotKey": "Ctrl+Shift+O",
    "DictionaryHotKey": "Ctrl+D"
  },
  "ModelsDirectory": "models",
  "MainWindowLayout": 0
}
```

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| DefaultModel | string | Hy-MT2-1.8B-Q4_K_M | 默认加载的模型名称 |
| SourceLanguage | string | auto | 默认源语言 |
| TargetLanguage | string | zh | 默认目标语言 |
| AutoDetectLanguage | bool | true | 是否自动检测语言 |
| Theme | string | System | 主题 (System/Light/Dark) |
| StartMinimized | bool | false | 是否最小化启动 |
| HotKeys.TranslateHotKey | string | Ctrl+Alt+Q | 划词翻译热键 |
| HotKeys.ReplaceHotKey | string | Ctrl+Shift+T | 原地替换热键 |
| ModelsDirectory | string | models | 模型目录路径 |
| MainWindowLayout | int | 0 | 布局模式 (0=水平, 1=垂直) |

### 8.2 模型文件放置

模型文件需放置在 `models/` 目录下，支持自动发现。

**支持的模型文件名**:
- `Hy-MT2-1.8B-Q4_K_M.gguf`
- `Hy-MT2-1.8B-2bit.gguf`
- `Hy-MT2-1.8B-1.25bit.gguf`
- `Hy-MT2-7B-Q4_K_M.gguf`
- `Hy-MT2-30B-A3B.gguf`

**模型下载地址**:

| 模型 | HuggingFace | 魔搭社区 ModelScope |
|------|-------------|---------------------|
| 1.8B Q4_K_M | https://huggingface.co/tencent/Hy-MT2-1.8B-GGUF | https://www.modelscope.cn/models/Tencent-Hunyuan/Hy-MT2-1.8B-GGUF/file/view/master/Hy-MT2-1.8B-Q4_K_M.gguf?status=2 |
| 7B Q4_K_M | https://huggingface.co/tencent/Hy-MT2-7B-GGUF | https://www.modelscope.cn/models/Tencent-Hunyuan/Hy-MT2-7B-GGUF/file/view/master/Hy-MT2-7B-Q4_K_M.gguf?status=2 |

### 8.3 编译运行

**前置要求**:
- Windows 10/11 x64
- .NET 8.0 SDK
- 至少 8GB RAM（推荐 16GB+）

**编译命令**:
```bash
cd f:\OpenTranslator
dotnet build src/OpenTranslator.App.Wpf/OpenTranslator.App.Wpf.csproj -c Release
```

**运行命令**:
```bash
dotnet run --project src/OpenTranslator.App.Wpf/OpenTranslator.App.Wpf.csproj
```

**或者使用 Visual Studio**:
1. 打开 `OpenTranslator.sln`
2. 设置 `OpenTranslator.App.Wpf` 为启动项目
3. 按 F5 运行

### 8.4 数据库

**路径**: `{程序目录}/data/translation_history.db`

**类型**: SQLite

**ORM**: sqlite-net-pcl

---

## 9. 开发指南

### 9.1 新增翻译引擎实现

1. 实现 `ITranslationEngine` 接口
2. 在 `App.xaml.cs` 的 `ConfigureServices` 中注册
3. 替换现有注册即可

```csharp
// 示例：新增 ONNX 翻译引擎
public class OnnxTranslationEngine : ITranslationEngine
{
    // 实现所有接口方法...
}

// 在 ConfigureServices 中替换
services.AddSingleton<ITranslationEngine, OnnxTranslationEngine>();
```

### 9.2 新增语言支持

在 [Constants.cs](file:///f:/OpenTranslator/src/OpenTranslator.App.Wpf/Helpers/Constants.cs) 的 `SupportedLanguages` 数组中添加：

```csharp
new() { Code = "xx", NameZh = "语言名", NameEn = "Language Name" }
```

### 9.3 新增模型支持

在 [Constants.cs](file:///f:/OpenTranslator/src/OpenTranslator.App.Wpf/Helpers/Constants.cs) 的 `PredefinedModels` 数组中添加：

```csharp
new()
{
    Name = "模型标识名",
    DisplayName = "显示名称",
    FileName = "模型文件名.gguf",
    FileSizeBytes = 1_500_000_000L,
    DownloadUrl = "https://.../model.gguf",
    ParametersCount = "参数量",
    RequiredVRamMB = 2048,
    RequiredRamMB = 8192
}
```

如果新模型使用不同的 Chat Template，还需要在 [PromptBuilder.cs](file:///f:/OpenTranslator/src/OpenTranslator.App.Wpf/Helpers/PromptBuilder.cs) 中：
1. 添加新的 `ModelTemplateType` 枚举值
2. 添加对应的模板常量（BOS、EOS、停止词等）
3. 更新 `DetectTemplateFromModelName` 方法的识别逻辑
4. 更新 `GetStopTokens` 和 `WrapChatTemplate` 的 switch 分支

### 9.4 新增热键功能

1. 在 [NativeMethods.cs](file:///f:/OpenTranslator/src/OpenTranslator.App.Wpf/Helpers/NativeMethods.cs) 添加热键 ID 常量
2. 在 [HotKeyService.cs](file:///f:/OpenTranslator/src/OpenTranslator.App.Wpf/Services/HotKeyService.cs) 添加事件和注册逻辑
3. 在 [AppConfig.cs](file:///f:/OpenTranslator/src/OpenTranslator.App.Wpf/Models/AppConfig.cs) 添加配置项
4. 在 [MainWindow.xaml.cs](file:///f:/OpenTranslator/src/OpenTranslator.App.Wpf/MainWindow.xaml.cs) 绑定事件处理

### 9.5 已知限制与注意事项

1. **WinUI 3 版本弃用**：`OpenTranslator.App` (WinUI 3) 存在稳定性问题（TextBox 崩溃、PRI 资源缺失等），目前主版本为 WPF 版本
2. **剪贴板操作**：必须在 UI 线程（STA 线程）执行，否则会静默失败
3. **LLamaSharp 版本**：必须使用 0.27.0 版本，与预编译的 llama.dll 兼容
4. **Hy-MT2 模型**：需要使用 STQ 内核分支编译的 llama.cpp（PR #22836）
5. **WPF 单线程**：所有 UI 更新必须在 Dispatcher 线程上执行
6. **热键冲突**：如果热键被其他程序占用，会注册失败并在状态栏提示

### 9.6 调试技巧

- 查看控制台输出：所有服务都有详细的 `Console.WriteLine` 日志
- 模型加载失败：检查 `StatusText` 属性和控制台输出
- 热键问题：查看 `HotKeyService.LastError` 属性
- 推理问题：检查 `[LlamaCppEngine]` 前缀的调试输出，包含 prompt 和原始输出

---

*文档生成时间: 2026-07-01*
*项目版本: 0.1.0*
