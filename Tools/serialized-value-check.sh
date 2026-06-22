#!/usr/bin/env bash
# Tools/serialized-value-check.sh
# 直列化優先トラップ検出（CLAUDE.md ★Inspector直列化の優先 / 過去バグ #2548）
#
# MonoBehaviour の public 調整値は、.unity/.prefab に直列化された値がスクリプト既定に勝つ。
# ＝スクリプトの既定値を書き換えても、その値がシーン/プレハブに直列化されていれば効かない。
# 調整値を「実際に効かせる」前に、本ツールでその値がシーン/プレハブに固定されていないかを必ず確認する。
# 直列化されていれば → そのファイル（テキストYAML）を直す。されていなければ → スクリプト既定が効く。
#
# 使い方:
#   Tools/serialized-value-check.sh <ClassName> [fieldName]
#   例:  Tools/serialized-value-check.sh CameraController
#        Tools/serialized-value-check.sh CameraController maxZoom
#
# 終了コード: 直列化された該当フィールドが1つでも見つかれば 1（hook/CI 判定用）、無ければ 0。

set -uo pipefail

CLASS="${1:-}"
FIELD="${2:-}"
if [ -z "$CLASS" ]; then
  echo "usage: $0 <ClassName> [fieldName]" >&2
  exit 2
fi

# リポジトリルート（Assets のある階層）へ
ROOT="$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
cd "$ROOT" || exit 2
if [ ! -d Assets ]; then echo "Assets が見つからない（root=$ROOT）" >&2; exit 2; fi

# .cs と guid を解決（テストは除外）
CS="$(find Assets -name "${CLASS}.cs" -not -path '*/Tests/*' 2>/dev/null | head -1)"
GUID=""
if [ -n "$CS" ] && [ -f "${CS}.meta" ]; then
  GUID="$(grep -m1 '^guid:' "${CS}.meta" | awk '{print $2}')"
fi

echo "=== 直列化チェック: ${CLASS}${FIELD:+ .$FIELD} ==="
if [ -n "$CS" ]; then echo "script : $CS"; fi
if [ -n "$GUID" ]; then echo "guid   : $GUID"; else echo "guid   : (未解決＝m_EditorClassIdentifier で照合)"; fi

# スクリプト側の既定値（比較用）
if [ -n "$CS" ]; then
  echo "--- スクリプト既定（初期化子つき public フィールド） ---"
  if [ -n "$FIELD" ]; then
    grep -nE "public[[:space:]].*\b${FIELD}\b" "$CS" | sed 's/^/  /' || echo "  (該当なし)"
  else
    grep -nE "^[[:space:]]*public[[:space:]].*=.*;" "$CS" | sed 's/^/  /' | head -40 || echo "  (初期化子つき public フィールドなし)"
  fi
fi

# 直列化している .unity/.prefab を収集（guid 一致 ＋ m_EditorClassIdentifier フォールバック）
declare -A SEEN
FILES=()
if [ -n "$GUID" ]; then
  while IFS= read -r f; do [ -n "$f" ] && { [ -z "${SEEN[$f]:-}" ] && FILES+=("$f") && SEEN[$f]=1; }; done < <(grep -rlF "guid: $GUID" --include='*.unity' --include='*.prefab' Assets 2>/dev/null)
fi
while IFS= read -r f; do [ -n "$f" ] && { [ -z "${SEEN[$f]:-}" ] && FILES+=("$f") && SEEN[$f]=1; }; done < <(grep -rlE "m_EditorClassIdentifier:.*[.:]${CLASS}$" --include='*.unity' --include='*.prefab' Assets 2>/dev/null)

echo "--- 直列化された値（.unity/.prefab） ---"
FOUND=0
for f in "${FILES[@]:-}"; do
  [ -z "$f" ] && continue
  OUT="$(awk -v guid="$GUID" -v cls="$CLASS" -v field="$FIELD" -v fname="$f" '
    function flush(   i,line,name){
      if (hit){
        for(i=0;i<n;i++){
          line=buf[i]
          if (line ~ /^  [A-Za-z_][A-Za-z0-9_]*:/ && line !~ /^  m_/ && line !~ /^  serializedVersion:/){
            name=line; sub(/:.*/,"",name); sub(/^  /,"",name)
            if (field=="" || name==field) print "  " fname ":  " substr(line,3)
          }
        }
      }
      n=0; hit=0; delete buf
    }
    /^--- / { flush() }
    {
      buf[n++]=$0
      if (guid!="" && index($0,"guid: " guid)) hit=1
      if ($0 ~ ("m_EditorClassIdentifier:.*[.:]" cls "$")) hit=1
    }
    END { flush() }
  ' "$f")"
  if [ -n "$OUT" ]; then echo "$OUT"; FOUND=1; fi
done

echo "--- 判定 ---"
if [ "$FOUND" = "1" ]; then
  echo "⚠ 直列化されています。スクリプト既定を変えても効きません。上記 .unity/.prefab の値を直してください。"
  exit 1
else
  if [ ${#FILES[@]} -eq 0 ] 2>/dev/null; then
    echo "✓ ${CLASS} を直列化したシーン/プレハブは見つかりません（スクリプト既定が効く／配置されていない）。"
  else
    echo "✓ 該当フィールド${FIELD:+（$FIELD）}は直列化されていません（後から足したフィールド等＝スクリプト既定が効く）。"
  fi
  exit 0
fi
