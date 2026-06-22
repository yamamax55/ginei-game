#!/usr/bin/env bash
# Stop フック：Claude の応答完了を Slack Incoming Webhook へ通知する。
# Webhook URL は秘密のため**コミットしない**＝環境変数 SLACK_WEBHOOK_URL で渡す
# （リモート環境の env 設定、またはローカルの ~/.bashrc 等で export）。
# 未設定なら何もせず正常終了（無効化と同義）。cwd=プロジェクト直下前提。
set -u
[ -z "${SLACK_WEBHOOK_URL:-}" ] && exit 0
repo="$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
msg="✅ Claude の回答が完了しました（$(basename "$repo")）"
payload="$(printf '{"text":"%s"}' "$msg")"
curl -sS -m 5 -X POST -H 'Content-type: application/json' \
  --data "$payload" "$SLACK_WEBHOOK_URL" >/dev/null 2>&1 || true
exit 0
