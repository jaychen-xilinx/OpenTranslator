# OpenTranslator 分发指南

## 概述

本文档说明如何将 OpenTranslator 分发给其他用户使用。

---

## 方案一：单文件可执行版本（推荐）

### 适用场景
- 分发给普通用户（无开发环境）
- 快速部署，无需安装

### 发布步骤

#### 1. 运行发布脚本

```batch
# 在项目根目录执行
publish.bat
```

脚本会自动完成：
1. 编译 Release 版本
2. 打包为单文件可执行程序
3. 复制模型文件（如有）
4. 创建配置文件和使用说明
5. 打包为 ZIP 文件

#### 2. 发布产物

输出文件：`publish\OpenTranslator-v0.1.0-win-x64.zip`

包含内容：
```
OpenTranslator-v0.1.0/
├── bin/
│   ├── OpenTranslator.exe       # 主程序（单文件，约 80-100MB）
│   ├── config.json              # 默认配置
│   └── runtimes/                # 原生库（自动包含）
├── models/                      # 模型文件（可选）
│   └── Hy-MT2-1.8B-Q4_K_M.gguf
├── README.txt                   # 使用说明
└── download-model.bat           # 模型下载辅助脚本
```

#### 3. 分发方式

**方式 A：直接分发 ZIP**
- 将 ZIP 文件发送给用户
- 用户解压后直接运行

**方式 B：GitHub Release**
```bash
# 1. 创建 Git 标签
git tag v0.1.0
git push origin v0.1.0

# 2. 在 GitHub 创建 Release
# 3. 上传 ZIP 文件作为发布资产
```

**方式 C：网盘分发**
- 百度网盘 / 阿里云盘 / 腾讯微云
- 注意：模型文件较大（1.5GB），建议单独提供下载链接

---

## 方案二：源码分发（面向开发者）

### 适用场景
- 分发给有 .NET 开发能力的用户
- 用户需要修改或定制功能

### 分发内容

完整项目源码，包含：
- 源代码（src/）
- 项目文件（.csproj, .sln）
- 文档（docs/）
- 参考资料（references/）

**不包含**：
- models/ 目录（太大，需单独下载）
- bin/ 和 obj/ 编译产物

### 步骤

#### 1. 清理编译产物

```batch
dotnet clean
```

#### 2. 创建 .gitignore（确保已配置）

```gitignore
# 编译产物
bin/
obj/
publish/

# 模型文件（大文件）
models/*.gguf
models/*.bin

# 用户数据
data/
config.json

# IDE 配置
.vs/
.vscode/
*.user
*.suo
```

#### 3. 打包源码

```batch
# 排除大文件
powershell -Command "Compress-Archive -Path 'src','docs','references','*.sln','*.md' -DestinationPath 'OpenTranslator-source-v0.1.0.zip' -Force"
```

#### 4. 用户使用流程

用户收到源码后：
```batch
# 1. 解压源码
# 2. 恢复依赖
dotnet restore

# 3. 下载模型
# 手动下载或使用脚本

# 4. 编译运行
dotnet build
dotnet run --project src/OpenTranslator.App.Wpf
```

---

## 方案三：GitHub 开源（推荐长期维护）

### 适用场景
- 开源项目
- 需要持续更新和社区贡献

### 步骤

#### 1. 创建 GitHub 仓库

```bash
git init
git add .
git commit -m "Initial commit: OpenTranslator v0.1.0"
git branch -M main
git remote add origin https://github.com/your-username/OpenTranslator.git
git push -u origin main
```

#### 2. 添加 README.md

在项目根目录创建 README.md，内容参考：

```markdown
# OpenTranslator

基于腾讯混元 Hy-MT2 模型的本地离线翻译工具。

## 特性
- 完全离线，无需联网
- 支持 37 种语言
- 划词翻译（Ctrl+Alt+Q）
- 原地替换翻译（Ctrl+Shift+T）
- 翻译历史记录

## 快速开始
1. 下载 Release 版本
2. 下载模型文件
3. 运行 OpenTranslator.exe

## 模型下载
[模型下载说明...]

## 系统要求
- Windows 10/11 x64
- 8GB+ RAM
```

#### 3. 创建 Release

在 GitHub 仓库：
1. 点击 "Releases" → "Create a new release"
2. 填写版本号（如 v0.1.0）
3. 编写发布说明
4. 上传编译好的 ZIP 文件
5. 发布

#### 4. 大文件处理（模型文件）

模型文件（1.5GB）不适合放在 Git 仓库，建议：

**方案 A：GitHub Releases 分离**
- 程序包：轻量（不含模型）
- 模型包：单独下载链接

**方案 B：外部托管**
- HuggingFace（官方模型已托管）
- 国内镜像：hf-mirror.com
- 网盘链接

---

## 模型文件分发策略

模型文件较大，建议采用以下策略：

### 策略 1：不包含模型（推荐）

发布包仅包含程序，用户自行下载模型：

**优点**：
- 发布包轻量（~80MB）
- 用户可选择不同模型规格

**用户操作**：
```
1. 下载 OpenTranslator 程序包
2. 下载模型（选择一种渠道）：
   - 魔搭社区（国内推荐）：
     https://www.modelscope.cn/models/Tencent-Hunyuan/Hy-MT2-1.8B-GGUF/file/view/master/Hy-MT2-1.8B-Q4_K_M.gguf?status=2
   - HuggingFace：
     https://huggingface.co/tencent/Hy-MT2-1.8B-GGUF
3. 放置到 models/ 目录
4. 运行程序
```

### 策略 2：提供下载脚本

在发布包中包含 `download-model.bat`：

```batch
@echo off
echo ========================================
echo   Hy-MT2 模型下载助手
echo ========================================
echo.
echo 请选择下载方式：
echo.
echo [1] 魔搭社区 ModelScope（国内推荐）
echo [2] HuggingFace（国际线路）
echo [3] 百度网盘（备用）
echo.
set /p choice="请输入选项 (1/2/3): "

if "%choice%"=="1" (
    echo 正在打开魔搭社区...
    start https://www.modelscope.cn/models/Tencent-Hunyuan/Hy-MT2-1.8B-GGUF/file/view/master/Hy-MT2-1.8B-Q4_K_M.gguf?status=2
)
if "%choice%"=="2" (
    echo 正在打开 HuggingFace...
    start https://huggingface.co/tencent/Hy-MT2-1.8B-GGUF
)
if "%choice%"=="3" (
    echo 正在打开网盘链接...
    start [网盘链接]
)
echo.
echo 下载完成后，请将模型文件放置到 models 目录。
pause
```

### 策略 3：提供多个发布版本

| 版本 | 包大小 | 说明 |
|------|--------|------|
| OpenTranslator-lite.zip | ~80MB | 仅程序，用户自行下载模型 |
| OpenTranslator-full.zip | ~1.6GB | 包含 1.8B Q4 模型 |
| OpenTranslator-mini.zip | ~600MB | 包含 1.8B 2bit 模型 |

---

## 用户使用指南（分发时附带）

### 文件清单

分发时应包含以下说明文档：

#### README.txt（普通用户）

```
# OpenTranslator 使用说明

## 第一步：下载模型
从以下地址下载模型文件（选择一个）：
- HuggingFace: https://huggingface.co/tencent/Hy-MT2-1.8B-GGUF
- 魔搭社区（国内推荐）: https://www.modelscope.cn/models/Tencent-Hunyuan/Hy-MT2-1.8B-GGUF/file/view/master/Hy-MT2-1.8B-Q4_K_M.gguf?status=2

推荐下载：Hy-MT2-1.8B-Q4_K_M.gguf（约1.5GB）

将下载的文件放到 models 文件夹中。

## 第二步：运行程序
双击 OpenTranslator.exe 启动程序

## 第三步：使用功能
- 主窗口翻译：输入文本，点击"翻译"
- 划词翻译：选中任意文本，按 Ctrl+Alt+Q
- 原地替换：选中文本，按 Ctrl+Shift+T

## 系统要求
- Windows 10 或 Windows 11
- 内存：8GB 以上（推荐 16GB）
- 如有 NVIDIA 显卡可加速翻译

## 问题反馈
[GitHub Issues 链接或联系方式]
```

---

## 检查清单

发布前确认：

### 功能检查
- [ ] 程序可正常启动
- [ ] 模型加载成功
- [ ] 翻译功能正常
- [ ] 划词翻译热键工作
- [ ] 原地替换热键工作
- [ ] 配置保存正常

### 文件检查
- [ ] 所有依赖库已包含
- [ ] 原生库已包含
- [ ] 配置文件已生成
- [ ] 使用说明已包含

### 兼容性检查
- [ ] Windows 10 x64 测试
- [ ] Windows 11 x64 测试
- [ ] 无 GPU 环境测试（CPU 模式）
- [ ] 有 GPU 环境测试（CUDA 加速）

---

## 总结

推荐分发方案：

| 受众 | 方案 | 包大小 |
|------|------|--------|
| 普通用户 | 单文件 ZIP（不含模型） | ~80MB |
| 开发者 | 源码 + GitHub | - |
| 开源社区 | GitHub Releases | - |

模型分发：使用 HuggingFace 官方链接 + 魔搭社区 ModelScope 链接，不打包到程序中。