#!/bin/bash

# Windows 打包脚本
# 用法: ./package-windows.sh [win-x64|win-arm64] [版本号]

Arch="${1:-win-x64}"
Version="${2:-7.16.7}"
ProjectPath="v2rayN/v2rayN.Desktop/v2rayN.Desktop.csproj"

echo "[*] 开始打包 Windows 版本: $Arch"
echo "[*] 版本号: $Version"

# 清理之前的构建
echo "[*] 清理之前的构建..."
dotnet clean "$ProjectPath" -c Release
rm -rf "$(dirname "$ProjectPath")/bin/Release/net8.0" || true

# 恢复依赖
echo "[*] 恢复依赖..."
dotnet restore "$ProjectPath"

# 发布应用
echo "[*] 发布应用..."
dotnet publish "$ProjectPath" \
    -c Release \
    -r "$Arch" \
    -p:SelfContained=true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=true

# 获取发布目录
PUBDIR="$(dirname "$ProjectPath")/bin/Release/net8.0/${Arch}/publish"
if [ ! -d "$PUBDIR" ]; then
    echo "[ERROR] 发布目录不存在: $PUBDIR"
    exit 1
fi

echo "[*] 发布完成，输出目录: $PUBDIR"

# 创建打包目录
PackageName="v2rayN-${Arch}"
PackageDir="${PackageName}"
rm -rf "$PackageDir" || true
mkdir -p "$PackageDir"

# 复制发布文件
echo "[*] 复制发布文件..."
cp -rf "$PUBDIR"/* "$PackageDir/"

# 创建 ZIP 包
ZipFileName="${PackageName}.zip"
echo "[*] 创建 ZIP 包: $ZipFileName"
if command -v 7z &> /dev/null; then
    7z a -tZip "$ZipFileName" "$PackageDir" -mx9
elif command -v zip &> /dev/null; then
    zip -r "$ZipFileName" "$PackageDir"
else
    echo "[WARNING] 未找到 7z 或 zip 命令，跳过 ZIP 打包"
fi

echo "[*] 打包完成！"
echo "[*] 输出文件: $ZipFileName"
echo "[*] 输出目录: $PackageDir"

