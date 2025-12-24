# v2rayN

A GUI client for Windows, Linux and macOS, support [Xray](https://github.com/XTLS/Xray-core)
and [sing-box](https://github.com/SagerNet/sing-box)
and [others](https://github.com/2dust/v2rayN/wiki/List-of-supported-cores)

[![GitHub commit activity](https://img.shields.io/github/commit-activity/m/2dust/v2rayN)](https://github.com/2dust/v2rayN/commits/master)
[![CodeFactor](https://www.codefactor.io/repository/github/2dust/v2rayn/badge)](https://www.codefactor.io/repository/github/2dust/v2rayn)
[![GitHub Releases](https://img.shields.io/github/downloads/2dust/v2rayN/latest/total?logo=github)](https://github.com/2dust/v2rayN/releases)
[![Chat on Telegram](https://img.shields.io/badge/Chat%20on-Telegram-brightgreen.svg)](https://t.me/v2rayn)

## How to use

Read the [Wiki](https://github.com/2dust/v2rayN/wiki) for details.

## Telegram Channel

[github_2dust](https://t.me/github_2dust)

## 运行与打包

前置：安装 .NET 8 SDK，并确保系统有 `wget`、`7z`（打包脚本需要）。

运行/调试
- Avalonia 跨平台（macOS/Linux/Windows）：`dotnet run --project v2rayN/v2rayN.Desktop/v2rayN.Desktop.csproj -c Debug`
- Windows WPF：`dotnet run --project v2rayN/v2rayN/v2rayN.csproj -c Debug`（仅 Windows 环境）

发布构建（示例）
- macOS 自包含：`dotnet publish v2rayN/v2rayN.Desktop/v2rayN.Desktop.csproj -c Release -r osx-x64 -p:SelfContained=true`
- Windows 自包含：`dotnet publish v2rayN/v2rayN/v2rayN.csproj -c Release -r win-x64 -p:SelfContained=true`

打包脚本（仓库根目录）
- macOS `.dmg`：`./package-osx.sh osx-64 <发布输出目录> <版本号>`
- 通用 zip：`./package-release-zip.sh osx-64 <发布输出目录>`
- Debian `.deb`：`./package-debian.sh linux-64 <发布输出目录> <版本号>`
- RHEL/Fedora/Ubuntu/Debian RPM：`./package-rhel.sh [版本号] [--arch x64|arm64|all ...]`

说明
- `<发布输出目录>` 为 `dotnet publish` 的 `publish` 目录，例如 `v2rayN/v2rayN.Desktop/bin/Release/net8.0/osx-x64/publish`。
- `package-osx.sh`/`package-debian.sh`/`package-release-zip.sh` 会先从 `v2rayN-core-bin` 下载核心并组装产物。
- `package-rhel.sh` 会自动 `dotnet publish`、下载核心/规则，再用 rpmbuild 生成 RPM（支持 `--arch`/`--with-core`/`--netcore` 等）。
