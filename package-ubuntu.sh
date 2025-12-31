#!/bin/bash

# Ubuntu 打包脚本
# 用法: ./package-ubuntu.sh [linux-x64|linux-arm64] [版本号] [deb|zip|both]

Arch="${1:-linux-x64}"
Version="${2:-7.16.7}"
PackageType="${3:-both}"
ProjectPath="v2rayN/v2rayN.Desktop/v2rayN.Desktop.csproj"

echo "[*] 开始打包 Ubuntu 版本: $Arch"
echo "[*] 版本号: $Version"
echo "[*] 打包类型: $PackageType"

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

# 确保可执行文件有执行权限
chmod +x "$PUBDIR/v2rayN" 2>/dev/null || true

# 根据架构确定 Debian 架构名称
if [ "$Arch" = "linux-x64" ]; then
    DebArch="amd64"
    ZipArch="linux-64"
elif [ "$Arch" = "linux-arm64" ]; then
    DebArch="arm64"
    ZipArch="linux-arm64"
else
    echo "[ERROR] 不支持的架构: $Arch (支持: linux-x64, linux-arm64)"
    exit 1
fi

# 创建 ZIP 包
if [ "$PackageType" = "zip" ] || [ "$PackageType" = "both" ]; then
    echo "[*] 创建 ZIP 包..."
    echo "[INFO] 跳过核心文件下载，只打包应用程序"

    # 创建 ZIP 包
    ZipFileName="v2rayN-${ZipArch}.zip"
    OutputArch="v2rayN-${ZipArch}"
    ZipPath="./$OutputArch"
    rm -rf "$ZipPath" || true
    mkdir -p "$ZipPath"

    cp -rf "$PUBDIR"/* "$ZipPath/"
    chmod +x "$ZipPath/v2rayN" 2>/dev/null || true

    echo "[*] 压缩 ZIP 包..."
    if command -v 7z &> /dev/null; then
        7z a -tZip "$ZipFileName" "$ZipPath" -mx1
    elif command -v zip &> /dev/null; then
        zip -r "$ZipFileName" "$ZipPath"
    else
        echo "[ERROR] 未找到 7z 或 zip 命令"
        exit 1
    fi

    echo "[*] ZIP 包创建完成: $ZipFileName"
fi

# 创建 DEB 包
if [ "$PackageType" = "deb" ] || [ "$PackageType" = "both" ]; then
    echo "[*] 创建 DEB 包..."
    echo "[INFO] 跳过核心文件下载，只打包应用程序"

    PackagePath="v2rayN-Package-${ZipArch}"
    rm -rf "$PackagePath" || true
    mkdir -p "${PackagePath}/DEBIAN"
    mkdir -p "${PackagePath}/opt"

    # 复制文件到包目录
    cp -rf "$PUBDIR" "${PackagePath}/opt/v2rayN"
    echo "When this file exists, app will not store configs under this folder" > "${PackagePath}/opt/v2rayN/NotStoreConfigHere.txt"

    # 创建 control 文件
    cat >"${PackagePath}/DEBIAN/control" <<-EOF
Package: v2rayN
Version: $Version
Architecture: $DebArch
Maintainer: https://github.com/2dust/v2rayN
Depends: libc6 (>= 2.34), fontconfig (>= 2.13.1), desktop-file-utils (>= 0.26), xdg-utils (>= 1.1.3), coreutils (>= 8.32), bash (>= 5.1), libfreetype6 (>= 2.11)
Description: A GUI client for Windows and Linux, support Xray core and sing-box-core and others
EOF

    # 创建 postinst 脚本
    cat >"${PackagePath}/DEBIAN/postinst" <<'POSTINST_EOF'
#!/bin/bash
if [ ! -s /usr/share/applications/v2rayN.desktop ]; then
    cat >/usr/share/applications/v2rayN.desktop <<'DESKTOP_EOF'
[Desktop Entry]
Name=v2rayN
Comment=A GUI client for Windows and Linux, support Xray core and sing-box-core and others
Exec=/opt/v2rayN/v2rayN
Icon=/opt/v2rayN/v2rayN.png
Terminal=false
Type=Application
Categories=Network;Application;
DESKTOP_EOF
fi

update-desktop-database
POSTINST_EOF

    # 设置权限
    chmod 0755 "${PackagePath}/DEBIAN/postinst"
    chmod 0755 "${PackagePath}/opt/v2rayN/v2rayN" 2>/dev/null || true
    chmod 0755 "${PackagePath}/opt/v2rayN/AmazTool" 2>/dev/null || true

    # 设置所有目录和文件权限
    find "${PackagePath}/opt/v2rayN" -type d -exec chmod 755 {} +
    find "${PackagePath}/opt/v2rayN" -type f -exec chmod 644 {} +
    chmod 755 "${PackagePath}/opt/v2rayN/v2rayN" 2>/dev/null || true
    chmod 755 "${PackagePath}/opt/v2rayN/AmazTool" 2>/dev/null || true

    # 构建 DEB 包（不需要 sudo，因为我们只是构建，不安装）
    echo "[*] 构建 DEB 包..."
    if ! dpkg-deb -Zxz --build "$PackagePath" 2>/dev/null; then
        # 如果失败，尝试使用 fakeroot
        if command -v fakeroot &> /dev/null; then
            fakeroot dpkg-deb -Zxz --build "$PackagePath"
        else
            echo "[ERROR] 无法构建 DEB 包，请安装 dpkg-deb 或 fakeroot"
            exit 1
        fi
    fi

    DebFileName="v2rayN-${ZipArch}.deb"
    if [ -f "${PackagePath}.deb" ]; then
        mv "${PackagePath}.deb" "$DebFileName"
        echo "[*] DEB 包创建完成: $DebFileName"
    else
        echo "[ERROR] DEB 包创建失败"
        exit 1
    fi
fi

echo "[*] 打包完成！"
if [ "$PackageType" = "zip" ] || [ "$PackageType" = "both" ]; then
    echo "[*] ZIP 文件: v2rayN-${ZipArch}.zip"
fi
if [ "$PackageType" = "deb" ] || [ "$PackageType" = "both" ]; then
    echo "[*] DEB 文件: v2rayN-${ZipArch}.deb"
fi

