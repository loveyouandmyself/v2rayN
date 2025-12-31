#!/bin/bash
# 在 Ubuntu 系统上运行此脚本来完成 DEB 打包
# 用法: ./build-deb-on-ubuntu.sh

PackagePath="v2rayN-Package-linux-64"
Version="7.16.7"

if [ ! -d "$PackagePath" ]; then
    echo "[ERROR] 打包目录不存在: $PackagePath"
    echo "请先在 macOS 上运行: ./package-ubuntu.sh linux-x64 $Version deb"
    exit 1
fi

echo "[*] 开始构建 DEB 包..."

# 设置权限
chmod 0755 "${PackagePath}/DEBIAN/postinst"
chmod 0755 "${PackagePath}/opt/v2rayN/v2rayN" 2>/dev/null || true

# 设置所有目录和文件权限
find "${PackagePath}/opt/v2rayN" -type d -exec chmod 755 {} +
find "${PackagePath}/opt/v2rayN" -type f -exec chmod 644 {} +
chmod 755 "${PackagePath}/opt/v2rayN/v2rayN" 2>/dev/null || true

# 构建 DEB 包
if command -v dpkg-deb &> /dev/null; then
    dpkg-deb -Zxz --build "$PackagePath"
    if [ -f "${PackagePath}.deb" ]; then
        mv "${PackagePath}.deb" "v2rayN-linux-64.deb"
        echo "[*] DEB 包创建完成: v2rayN-linux-64.deb"
        ls -lh v2rayN-linux-64.deb
    else
        echo "[ERROR] DEB 包创建失败"
        exit 1
    fi
else
    echo "[ERROR] 未找到 dpkg-deb 命令，请安装: sudo apt-get install dpkg-dev"
    exit 1
fi

