# 镜像加速（Ubuntu 静态服务器）— geosite/geoip（可扩展到 Xray Core）

本目录用于记录：当用户侧从 GitHub 下载 `geosite.dat / geoip.dat`（以及后续 Xray Core）速度较慢时，如何在你自己的 Ubuntu 服务器上搭建**静态文件镜像源**，并让客户端改为从该镜像源下载。

> 当前代码已支持通过配置覆盖 geo 文件下载地址（`GeoSourceUrl`）。
> Xray Core 的“镜像下载”目前仍走 GitHub release（如需改为自建镜像，见下文“扩展：Xray Core 镜像”）。

---

## 1. 你需要镜像哪些文件？

### 1.1 Geo 规则库（必须）

- `geosite.dat`
- `geoip.dat`

客户端在生成配置时会出现类似 `geosite:google` 的规则，如果没有 `geosite.dat`，Xray 会报错并无法启动。

### 1.2（可选）Xray Core

如果你希望连 Core 也不从 GitHub 下载，可以镜像：

- `Xray-linux-64.zip`
- `Xray-linux-arm64-v8a.zip`
- `Xray-macos-64.zip`
- `Xray-macos-arm64-v8a.zip`
- `Xray-windows-64.zip`
- `Xray-windows-arm64-v8a.zip`

---

## 2. 推荐的镜像目录结构（Ubuntu）

以 `nginx` 为例，假设静态目录为 `/var/www/eyesat-vpn/`：

```
/var/www/eyesat-vpn/
  geo/
    geosite.dat
    geoip.dat
  xray/
    # 可选：按版本/架构存放 zip
    v1.8.24/
      Xray-linux-64.zip
      Xray-linux-arm64-v8a.zip
      Xray-macos-64.zip
      Xray-macos-arm64-v8a.zip
      Xray-windows-64.zip
      Xray-windows-arm64-v8a.zip
```

对外提供的 URL 示例：

- Geo：
  - `https://your-domain.example/geo/geosite.dat`
  - `https://your-domain.example/geo/geoip.dat`
- Xray（可选）：
  - `https://your-domain.example/xray/v1.8.24/Xray-macos-arm64-v8a.zip`

---

## 3. Nginx 配置示例

见：[`nginx.conf.example`](./nginx.conf.example)

---

## 4. 同步脚本（从 GitHub 拉到你的 Ubuntu 镜像目录）

见：[`sync-geo.sh`](./sync-geo.sh)

建议用 `cron` 每天/每周同步一次，或在你发布新版本时同步。

---

## 5. v2rayN 如何改为从你的服务器下载 geo 文件？

当前下载逻辑（代码）会优先使用配置里的 `GeoSourceUrl`：

- 默认：`https://github.com/Loyalsoldier/v2ray-rules-dat/releases/latest/download/{0}.dat`
- 如果你配置了 `GeoSourceUrl`：会用你配置的模板 URL

因此你只需要把 `GeoSourceUrl` 设置成你的静态服务器地址即可，例如：

```
https://your-domain.example/geo/{0}.dat
```

其中 `{0}` 会被替换为 `geosite` 或 `geoip`。

> 注意：你的服务器上需要存在 `geosite.dat` 和 `geoip.dat`，且路径与模板一致。

---

## 6. 扩展：Xray Core 镜像（后续可做）

目前 Xray Core 的更新逻辑使用 GitHub release API 获取最新版本和下载链接。

如果要完全切到你的镜像服务器，通常需要做两件事：

1. **让客户端从自定义地址下载 Core 压缩包**（支持 x64/arm64 & Win/Linux/macOS）。
2. **可选：自建一个“latest”元信息接口**（或固定某个版本），避免客户端去 GitHub API 查 `tag_name`。

我们可以在后续改造中新增一个配置项（例如 `CoreMirrorBaseUrl` / `XrayMirrorUrlTemplate`），并在下载 Core 时优先使用该配置项。


