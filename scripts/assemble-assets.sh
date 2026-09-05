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
    rm -f "$ASSETS/www.zip"   # 旧占位/损坏 zip 会导致 zip -u 失败，先删再建
    (cd "$ROOT/frontend/dist" && zip -qr "$ASSETS/www.zip" .)
    log "前端已装配 → $(du -h "$ASSETS/www.zip" | cut -f1)"
  else
    log "frontend 缺失，跳过（运行时占位页兜底）"
  fi
fi

# ── 2. Termux bootstrap（internal/external 均捆绑完整 Termux）──────────
# 官方 bootstrap 托管在 termux-packages 的 GitHub Releases（packages.termux.dev 的
# /bootstraps 目录已下线）。自动取最新 bootstrap tag，GitHub API 失败则用固定兜底 tag。
log "下载 Termux bootstrap"
BOOT_API="https://api.github.com/repos/termux/termux-packages/releases"
# awk 命中后 exit 会提前关管道 → curl SIGPIPE(23)；|| true 防 set -e 终止。
# tag_name 自带 "bootstrap-" 前缀，sub 去掉后由下方统一补回，避免双前缀。
BOOT_TAG=$(curl -sSL --max-time 30 "$BOOT_API" 2>/dev/null \
  | awk -F'"' '/"tag_name": "bootstrap-/ {sub(/^bootstrap-/, "", $4); print $4; exit}') || true
if [ -n "$BOOT_TAG" ]; then
  BOOT_TAG="bootstrap-$BOOT_TAG"
else
  log "GitHub API 失败，用固定兜底 tag"
  BOOT_TAG="bootstrap-2026.08.30-r1+apt.android-7"
fi
BOOT_URL="https://github.com/termux/termux-packages/releases/download/${BOOT_TAG}/bootstrap-aarch64.zip"
log "bootstrap 源: ${BOOT_TAG}"
if curl -sSLfL --max-time 900 -o "$ASSETS/termux-bootstrap.zip" "$BOOT_URL"; then
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
    # 从索引解析真实 Filename 与期望大小（用于校验截断下载）
    INFO=$(awk -v p="^Package: openjdk-$major\$" '
      $0 ~ p {f=1}
      f && /^Filename:/ {fn=$2}
      f && /^Size:/ {sz=$2; exit}
      END {print fn, sz}' "$PKGS_FILE" 2>/dev/null || true)
    FILENAME=$(echo "$INFO" | awk '{print $1}')
    EXPECTED_SIZE=$(echo "$INFO" | awk '{print $2}')

    if [ -z "$FILENAME" ]; then
      log "JDK $major 不在 Termux 仓库（或索引缺失），跳过（运行时 pkg install 兜底）"
      continue
    fi

    if [ -n "$EXPECTED_SIZE" ]; then
      log "期望大小 ${EXPECTED_SIZE}B"
    fi

    # 下载（必要时重试一次），并校验文件大小与索引一致，防截断
    OK=0
    for attempt in 1 2; do
      if curl -sSLf --max-time 600 -o "$ROOT/.jdk/openjdk-$major.deb" \
          "https://packages.termux.dev/apt/termux-main/$FILENAME"; then
        ACTUAL=$(stat -c %s "$ROOT/.jdk/openjdk-$major.deb" 2>/dev/null || echo 0)
        if [ -n "$EXPECTED_SIZE" ] && [ "$ACTUAL" != "$EXPECTED_SIZE" ]; then
          log "JDK $major 下载不完整 ${ACTUAL}B != ${EXPECTED_SIZE}B（尝试 $attempt/2）"
        else
          OK=1
          break
        fi
      else
        log "JDK $major 下载失败（尝试 $attempt/2）"
      fi
    done
    if [ "$OK" -ne 1 ]; then
      rm -f "$ROOT/.jdk/openjdk-$major.deb"
      log "JDK $major 下载未完成，跳过（运行时兜底）"
      continue
    fi

    # 解包 .deb → 提取 usr/ → 打 tar.gz
    if dpkg-deb -x "$ROOT/.jdk/openjdk-$major.deb" "$ROOT/.jdk/x-$major" 2>/dev/null; then
      JVM_BASE="$ROOT/.jdk/x-$major/data/data/com.termux/files"
      if [ -d "$JVM_BASE/usr/lib/jvm" ]; then
        # 统一规范布局 usr/lib/jvm/openjdk-N/：Termux 目录名是 java-N-openjdk，重命名对齐
        for d in "$JVM_BASE"/usr/lib/jvm/*; do
          [ -d "$d" ] || continue
          base=$(basename "$d")
          case "$base" in
            "openjdk-$major") : ;;
            "java-$major-openjdk"|"java-$major-jre"*) mv "$d" "$JVM_BASE/usr/lib/jvm/openjdk-$major" ;;
          esac
        done
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