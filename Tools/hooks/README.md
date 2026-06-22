# Tools/hooks — Claude Code 自動ゲート

対話セッションの「忘れると壊れる手順」を harness が機械実行する hook 群。
**`.claude/settings.local.json` は gitignore（共有されない）**ため、本スクリプトのみコミットし、
配線は各マシンで下記を `.claude/settings.local.json` に追記する（headless CI には入れない＝
CI は別途 TestHarness ゲートで担保済み・blocking hook の暴走を避ける）。

## スクリプト
- **post-edit-serialize-check.sh**（PostToolUse / Edit|Write|MultiEdit）
  Game層 MonoBehaviour の `public` 調整値を編集し、その値が `.unity`/`.prefab` に直列化済みなら警告（#2548 系）。
  編集テキストに `public <型> <field> = …;` を含む時だけ作動＝低ノイズ。直列化検出で exit 2（Claude へ feedback）。
- **stop-meta-check.sh**（Stop）
  このセッションで追加/変更した `Assets/` 配下のファイルに `.meta` が欠けていれば警告（core-wave の meta 漏れ対策）。
  git 作業ツリー差分のみ走査＝高速。欠落で exit 2。

両者とも cwd=プロジェクト直下前提の相対パス起動・`CLAUDE_PROJECT_DIR`/`git rev-parse` でルート解決・python で JSON 解析（jq 不要）。

## 配線（`.claude/settings.local.json` に追記）
```json
{
  "hooks": {
    "PostToolUse": [
      { "matcher": "Edit|Write|MultiEdit",
        "hooks": [ { "type": "command", "command": "bash Tools/hooks/post-edit-serialize-check.sh" } ] }
    ],
    "Stop": [
      { "hooks": [ { "type": "command", "command": "bash Tools/hooks/stop-meta-check.sh" } ] }
    ]
  }
}
```
※ 配線後はセッション再読込で有効化。`bash` が PATH にあること（Windows は Git Bash）。
