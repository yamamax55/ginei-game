#!/usr/bin/env bash
# Tools/hooks/stop-meta-check.sh
# Stop hook。このセッションで追加/変更した Assets 配下の .cs（および新規 .unity/.prefab 等）に
# 対応する .meta が欠けていれば警告して exit 2（Claude に生成を促す）。
# core-wave の「meta生成漏れ」（Wave28 の教訓）を構造的に潰す。
# git の作業ツリー差分のみ走査＝高速・既存の無関係な欠落は対象にしない。

set -uo pipefail
ROOT="${CLAUDE_PROJECT_DIR:-$(git rev-parse --show-toplevel 2>/dev/null || pwd)}"
cd "$ROOT" 2>/dev/null || exit 0
[ -d Assets ] || exit 0

# 追加/変更された Assets 配下のファイル（.meta 自身は除く）
CHANGED="$(git status --porcelain -- Assets 2>/dev/null | awk '{print $2}' | grep -v '\.meta$' || true)"
[ -z "$CHANGED" ] && exit 0

MISSING=""
while IFS= read -r f; do
  [ -z "$f" ] && continue
  [ -f "$f" ] || continue
  if [ ! -f "${f}.meta" ]; then
    MISSING="${MISSING}  ・${f}\n"
  fi
done <<< "$CHANGED"

if [ -n "$MISSING" ]; then
  printf '⚠ .meta 欠落（このセッションで追加/変更したファイル）。Unity が GUID 不整合を起こす前に .meta を生成してください：\n%b\n生成例: printf "fileFormatVersion: 2\\nguid: %%s\\n" "$(python -c \"import uuid;print(uuid.uuid4().hex)\")" > <file>.meta' "$MISSING" >&2
  exit 2
fi
exit 0
