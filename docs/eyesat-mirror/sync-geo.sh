#!/usr/bin/env bash
set -euo pipefail

# 用途：
# - 从 GitHub（Loyalsoldier/v2ray-rules-dat）下载最新 geosite.dat / geoip.dat
# - 放到你的静态站点目录中，供 v2rayN 客户端从你的服务器下载
#
# 使用：
#   sudo mkdir -p /var/www/eyesat-vpn/geo
#   sudo bash sync-geo.sh /var/www/eyesat-vpn/geo
#
# 可配合 cron：
#   0 4 * * * root /path/to/sync-geo.sh /var/www/eyesat-vpn/geo >/var/log/sync-geo.log 2>&1

TARGET_DIR="${1:-}"
if [[ -z "$TARGET_DIR" ]]; then
  echo "Usage: $0 /path/to/your/static/geo"
  exit 1
fi

mkdir -p "$TARGET_DIR"

BASE="https://github.com/Loyalsoldier/v2ray-rules-dat/releases/latest/download"

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

echo "[+] Downloading geosite.dat ..."
curl -fL "$BASE/geosite.dat" -o "$tmp/geosite.dat"

echo "[+] Downloading geoip.dat ..."
curl -fL "$BASE/geoip.dat" -o "$tmp/geoip.dat"

echo "[+] Installing to $TARGET_DIR ..."
install -m 0644 "$tmp/geosite.dat" "$TARGET_DIR/geosite.dat"
install -m 0644 "$tmp/geoip.dat" "$TARGET_DIR/geoip.dat"

echo "[OK] Done."


