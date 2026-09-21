#!/usr/bin/env bash
# ── 斩妖录（Web 版）部署脚本（Vite 纯静态站）──────────────────
# 流程：门禁（vitest 单测）→ 生产构建（prebuild 资产同步 + tsc 类型检查 + vite）
# → 写 dist/.deploy-version 版本戳 →（可选）preview 冒烟 →（可选）rsync 推送。
#
# 用法（仓库根目录或任意子目录执行）：
#   ./scripts/deploy.sh                  # 仅本地：门禁＋构建＋版本戳，产物在 dist/
#   ./scripts/deploy.sh --smoke          # 构建后起 npm run preview 并 curl 断言 200
#   SERVER=user@host REMOTE_DIR=/srv/cnm \
#     ./scripts/deploy.sh --push         # 构建后 rsync 推送 dist/ 到服务器
#
# 说明：推送目标由环境变量显式给出，脚本不写死服务器路径；
# 未设 SERVER/REMOTE_DIR 时 --push 直接拒绝，避免误推。
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

SMOKE=0
PUSH=0
for arg in "$@"; do
  case "$arg" in
    --smoke) SMOKE=1 ;;
    --push)  PUSH=1 ;;
    *) echo "!! 未知参数: ${arg}（可用 --smoke / --push）" >&2; exit 1 ;;
  esac
done

# 防误推：目标必须由环境变量显式给出，缺失即报错退出（提前到构建前，fail fast）
if [ "$PUSH" = "1" ]; then
  if [ -z "${SERVER:-}" ] || [ -z "${REMOTE_DIR:-}" ]; then
    echo "!! --push 需要显式设置环境变量 SERVER（如 user@host）与 REMOTE_DIR（服务器上的 dist 目录）" >&2
    exit 1
  fi
fi

echo "==> 门禁：vitest 单测…"
npm test

echo "==> 生产构建…"
npm run build

# 版本戳：两行（git describe 一行 + UTC 构建时间一行），nginx 托管时可作部署溯源
VERSION="$(git describe --always --tags --dirty)"
printf '%s\n%s\n' "$VERSION" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" > dist/.deploy-version
# 无可达 tag 时 describe 退化为纯短 hash，给出提示便于识别未发版构建
if ! git describe --tags >/dev/null 2>&1; then
  echo "!! 当前仓库无可达 tag，版本戳为短 hash：${VERSION}" >&2
fi
echo "==> 产物就绪: dist/（版本 ${VERSION}）"

if [ "$SMOKE" = "1" ]; then
  # 与 .github/workflows/ci.yml 的 preview smoke 同款做法：起 preview（默认 4173
  # 端口），轮询 curl 断言 200 + HTML 后关闭
  echo "==> 冒烟：起 preview 并断言 200…"
  npm run preview &
  pid=$!
  code=0
  for _ in $(seq 1 30); do
    code=$(curl -s -o /tmp/cnm-preview-smoke.html -w '%{http_code}' http://127.0.0.1:4173/ || true)
    [ "$code" = "200" ] && break
    sleep 1
  done
  kill "$pid" 2>/dev/null || true
  wait "$pid" 2>/dev/null || true
  test "$code" = "200" || { echo "!! preview 未返回 200（实际: ${code}）" >&2; exit 1; }
  grep -qi '<html' /tmp/cnm-preview-smoke.html || { echo "!! preview 响应非 HTML" >&2; exit 1; }
  echo "==> 冒烟通过（HTTP 200 + HTML）"
fi

if [ "$PUSH" = "1" ]; then
  echo "==> 推送 dist/ → ${SERVER}:${REMOTE_DIR}…"
  rsync -az --delete --exclude '.deploy-version' dist/ "${SERVER}:${REMOTE_DIR}/"
  # 版本戳单独传，避免被 --delete 以外的增量逻辑干扰
  rsync -az dist/.deploy-version "${SERVER}:${REMOTE_DIR}/.deploy-version"
  echo "==> 部署完成（版本 ${VERSION}）"
else
  echo "==> 未推送（--push 需显式给出 SERVER/REMOTE_DIR 环境变量）"
fi
