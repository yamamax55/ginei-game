#!/usr/bin/env bash
# Tools/hooks/post-edit-serialize-check.sh
# PostToolUse hook（Edit|Write|MultiEdit）。
# Game層 MonoBehaviour の public 調整値を編集したとき、その値が .unity/.prefab に
# 直列化されていれば「スクリプト既定を変えても効かない」と警告する（#2548 系の自動検出）。
#
# 仕様：編集テキストに `public <型> <field> = ...;` が含まれる場合のみ作動（低ノイズ）。
# 直列化された該当フィールドがあれば stderr に出して exit 2（Claude へ feedback）、無ければ exit 0。
# stdin に PostToolUse の JSON が渡る前提。jq 不使用（python で解析）。

set -uo pipefail
INPUT="$(cat)"
ROOT="${CLAUDE_PROJECT_DIR:-$(git rev-parse --show-toplevel 2>/dev/null || pwd)}"
export HOOK_INPUT="$INPUT"

PY="$(python - <<'PYEOF' 2>/dev/null
import os, json, re
try:
    d = json.loads(os.environ.get("HOOK_INPUT", "{}") or "{}")
except Exception:
    d = {}
ti = d.get("tool_input", {}) or {}
fp = ti.get("file_path", "") or ti.get("filePath", "")
texts = []
if isinstance(ti.get("content"), str): texts.append(ti["content"])
if isinstance(ti.get("new_string"), str): texts.append(ti["new_string"])
for e in (ti.get("edits") or []):
    if isinstance(e, dict) and isinstance(e.get("new_string"), str):
        texts.append(e["new_string"])
blob = "\n".join(texts)
# public <型...> <field> = ... ;  の field 名を拾う（プロパティ => は = の手前に無いので除外される）
fields = sorted(set(re.findall(r'public\s+[A-Za-z_][\w<>\[\],\s\.]*?\s+([A-Za-z_]\w*)\s*=', blob)))
print(fp)
for f in fields:
    print(f)
PYEOF
)"

[ -z "$PY" ] && exit 0
FILE="$(printf '%s\n' "$PY" | sed -n '1p' | tr '\\' '/')"
FIELDS="$(printf '%s\n' "$PY" | sed -n '2,$p')"

# Game層の .cs 編集で、public フィールド初期化を含むときだけ続行
case "$FILE" in
  *Assets/Scripts/Game/*.cs) : ;;
  *) exit 0 ;;
esac
[ -z "$FIELDS" ] && exit 0

CLASS="$(basename "$FILE" .cs)"
CHECK="$ROOT/Tools/serialized-value-check.sh"
[ -f "$CHECK" ] || exit 0

HITS=""
while IFS= read -r f; do
  [ -z "$f" ] && continue
  if bash "$CHECK" "$CLASS" "$f" >/dev/null 2>&1; then : ; else
    # exit 1 = 直列化されている
    val="$(bash "$CHECK" "$CLASS" "$f" 2>/dev/null | awk '/直列化された値/{flag=1;next} /判定/{flag=0} flag' | sed 's/^ *//')"
    HITS="${HITS}  ・${f}: ${val}\n"
  fi
done <<< "$FIELDS"

if [ -n "$HITS" ]; then
  printf '⚠ 直列化トラップ（%s）：以下の調整値は .unity/.prefab に直列化済み＝スクリプト既定を変えても効きません。直列化ファイル側を直してください（Tools/serialized-value-check.sh %s で詳細）。\n%b' "$CLASS" "$CLASS" "$HITS" >&2
  exit 2
fi
exit 0
