@echo off
echo ========================================
echo   OpenTranslator 发布脚本
echo ========================================
echo.

:: 设置项目路径
set PROJECT_PATH=src\OpenTranslator.App.Wpf\OpenTranslator.App.Wpf.csproj
set OUTPUT_DIR=publish\OpenTranslator-v0.1.0

:: 清理旧发布文件
echo [1] 清理旧发布文件...
if exist "%OUTPUT_DIR%" rmdir /s /q "%OUTPUT_DIR%"
mkdir "%OUTPUT_DIR%"

:: 发布单文件版本
echo [2] 发布单文件可执行版本...
dotnet publish "%PROJECT_PATH%" ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true ^
    -o "%OUTPUT_DIR%\bin"

if errorlevel 1 (
    echo 发布失败！
    pause
    exit /b 1
)

:: 复制模型目录（如果存在）
echo [3] 复制模型文件...
if exist "models" (
    mkdir "%OUTPUT_DIR%\models"
    xcopy "models\*.gguf" "%OUTPUT_DIR%\models\" /Y /Q
    echo 模型文件已复制
) else (
    echo 未找到模型文件，需要用户自行下载
)

:: 复制图标文件
echo [3.1] 复制图标文件...
if exist "ico.png" (
    copy "ico.png" "%OUTPUT_DIR%\bin\icon.png" /Y
    echo 图标文件已复制
)

:: 创建配置文件
echo [4] 创建默认配置...
(
echo {
echo   "DefaultModel": "Hy-MT2-1.8B-Q4_K_M",
echo   "SourceLanguage": "auto",
echo   "TargetLanguage": "zh",
echo   "AutoDetectLanguage": true,
echo   "Theme": "System",
echo   "StartMinimized": false,
echo   "HotKeys": {
echo     "TranslateHotKey": "Ctrl+Alt+B",
echo     "ReplaceHotKey": "Ctrl+Alt+V",
echo     "ScreenshotHotKey": "Ctrl+Shift+O",
echo     "DictionaryHotKey": "Ctrl+D"
echo   },
echo   "ModelsDirectory": "models",
echo   "MainWindowLayout": 0
echo }
) > "%OUTPUT_DIR%\bin\config.json"

:: 创建使用说明
echo [5] 创建使用说明...
(
echo # OpenTranslator 使用说明
echo.
echo ## 快速开始
echo.
echo 1. 下载模型文件
echo    - 从 HuggingFace 下载 Hy-MT2-1.8B-Q4_K_M.gguf
echo    - 放置到 models 目录下
echo    - 国内用户可使用 hf-mirror.com 镜像加速
echo.
echo 2. 运行程序
echo    - 双击 OpenTranslator.exe 启动
echo    - 首次启动会自动加载模型
echo.
echo ## 功能说明
echo.
echo - 主窗口翻译：输入文本后点击翻译按钮
echo - 划词翻译：选中文本后按 Ctrl+Alt+Q
echo - 原地替换：选中文本后按 Ctrl+Shift+T
echo.
echo ## 模型下载地址
echo.
echo HuggingFace:
echo https://huggingface.co/tencent/Hy-MT2-1.8B-GGUF
echo.
echo 魔搭社区（国内推荐）:
echo https://www.modelscope.cn/models/Tencent-Hunyuan/Hy-MT2-1.8B-GGUF/file/view/master/Hy-MT2-1.8B-Q4_K_M.gguf?status=2
echo.
echo ## 系统要求
echo.
echo - Windows 10/11 x64
echo - 内存：8GB+ (推荐 16GB)
echo - 显卡：可选，支持 CUDA 加速
) > "%OUTPUT_DIR%\README.txt"

:: 创建模型下载脚本
echo [6] 创建模型下载脚本...
(
echo @echo off
echo echo 正在下载 Hy-MT2-1.8B-Q4_K_M 模型...
echo echo.
echo echo 国内用户请使用魔搭社区直接下载：
echo https://www.modelscope.cn/models/Tencent-Hunyuan/Hy-MT2-1.8B-GGUF/file/view/master/Hy-MT2-1.8B-Q4_K_M.gguf?status=2
echo echo.
echo echo 下载完成后，将文件放置到 models 目录下。
echo pause
) > "%OUTPUT_DIR%\download-model.bat"

:: 打包
echo [7] 打包发布文件...
cd publish
powershell -Command "Compress-Archive -Path 'OpenTranslator-v0.1.0' -DestinationPath 'OpenTranslator-v0.1.0-win-x64.zip' -Force"
cd ..

echo.
echo ========================================
echo   发布完成！
echo ========================================
echo.
echo 输出目录: publish\OpenTranslator-v0.1.0-win-x64.zip
echo.
echo 包含内容:
echo   - OpenTranslator.exe (单文件可执行)
echo   - models 目录 (如有模型文件)
echo   - config.json (默认配置)
echo   - README.txt (使用说明)
echo   - download-model.bat (模型下载脚本)
echo.
pause