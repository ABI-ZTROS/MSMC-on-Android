#!/usr/bin/env bash
# -----------------------------------------------------------------------------
# MSMC on Android · 资产装配脚本
# 用途: 在 CI 构建前，把 前端 dist / Termux bootstrap / 内置 JDK 装配进
#       src/MSMC.Android/Assets/，供 MSBuild 的 <AndroidAsset> 打包进 APK。
# 参数:
#   --jdk    同时装配 4 个内置 JDK（internal flavor 用）；缺省仅前端+Termux。
#   --skip-web  跳过前端构建（本地已构建好 dist 时省时）。
# 健壮性: 任何一步失败都不让整个 CI 崩 —— 缺失的 asset 在运行时走下载兜底。
# -----------------------------------------------------------------------------
set -euo pipefail

cd "$(dirname "$0")/.."
ROOT="$(pwd)"
ASSETS="$ROOT/src/MSMC.Android/Assets"
mkdir -p "$ASSETS"

BUILD_JDK=0
SKIP_WEB=0
for arg in "$@"; do
  case "$arg" in
    --jdk) BUILD_JDK=1 ;;
    --skip-web) SKIP_WEB=1 ;;
  esac
done

log() { echo "=== [ASSET] $* ==="; }

# ── 1. 前端 dist → www.zip ─────────────────────────────────────────────
if [ "$SKIP_WEB" -eq 0 ]; then
  log "构建前端"
  if [ -d "$ROOT/frontend" ] && [ -f "$ROOT/frontend/package.json" ]; then
    (cd "$ROOT/frontend" && npm install --no-audit --no-fund && npm run build)
    (cd "$ROOT/frontend" && rm -f dist/*.map 2>/dev/null || true)
    (cd "$ROOT/frontend/dist" && zip -qr "$ASSETS/www.zip" .)
    log "前端已装配 → $(du -h "$ASSETS/www.zip" | cut -f1)"
  else
    log "frontend 缺失，跳过（运行时占位页兜底）"
  fi
fi

# ── 2. Termux bootstrap（internal/external 均捆绑完整 Termux）──────────
log "下载 Termux bootstrap"
BOOT_URL="https://packages.termux.dev/apt/termux-main/bootstraps/bootstrap-aarch64.zip"
if curl -sSLf --max-time 300 -o "$ASSETS/termux-bootstrap.zip" "$BOOT_URL"; then
  log "Termux 已装配 → $(du -h "$ASSETS/termux-bootstrap.zip" | cut -f1)"
else
  log "Termux 下载失败（运行时将尝试在线兜底）"
  rm -f "$ASSETS/termux-bootstrap.zip"
fi

# ── 3. 内置 4×JDK（仅 --jdk，internal flavor）──────────────────────────
if [ "$BUILD_JDK" -eq 1 ]; then
  log "装配内置 JDK 17/21/25/26（best-effort，缺失版本运行时兜底）"
  mkdir -p "$ROOT/.jdk"

  # 一次性拉取包索引，解析各 openjdk 的真实 Filename
  PKGS_FILE="$ROOT/.jdk/Packages"
  curl -sSL --max-time 120 -o "$PKGS_FILE" \
    "https://packages.termux.dev/apt/termux-main/dists/stable/main/binary-aarch64/Packages" || true

  for major in 17 21 25 26; do
    OUT="$ASSETS/jdk$major.tar.gz"
    log "JDK $major"
    FILENAME=$(awk -v p="^Package: openjdk-$major\$" '
      $0 ~ p {f=1}
      f && /^Filename:/ {print $2; exit}' "$PKGS_FILE" 2>/dev/null || true)

    if [ -z "$FILENAME" ]; then
      log "JDK $major 不在 Termux 仓库（或索引缺失），跳过（运行时 pkg install 兜底）"
      continue
    fi

    if ! curl -sSLf --max-time 180 -o "$ROOT/.jdk/openjdk-$major.deb" \
        "https://packages.termux.dev/apt/termux-main/$FILENAME"; then
      log "JDK $major 下载失败，跳过（运行时兜底）"
      continue
    fi

    # 解包 .deb → 提取 usr/ → 打 tar.gz（布局 usr/lib/jvm/openjdk-N/…）
    if dpkg-deb -x "$ROOT/.jdk/openjdk-$major.deb" "$ROOT/.jdk/x-$major" 2>/dev/null; then
      JVM_BASE="$ROOT/.jdk/x-$major/data/data/com.termux/files"
      if [ -d "$JVM_BASE/usr/lib/jvm" ]; then
        (cd "$JVM_BASE" && tar czf "$OUT" usr)
        log "JDK $major 已装配 → $(du -h "$OUT" | cut -f1)"
      else
        log "JDK $major 解包后未找到 jvm 目录，跳过"
      fi
    else
      log "JDK $major 解包失败，跳过"
    fi
    rm -rf "$ROOT/.jdk/x-$major"
  done
fi

log "资产装配完成："
ls -la "$ASSETS" || true