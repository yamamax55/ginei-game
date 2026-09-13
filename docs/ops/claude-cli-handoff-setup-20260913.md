# Claude Code CLI受け渡し設定

作成: 2026-09-13。ユーザー承認: 依頼と完了確認をファイル・CLIへ統一。プログラムはClaude担当。

## 受け渡し契約
ChatGPTが依頼Markdownを作成し、task_idとattempt_idを一意に付ける。依頼には目的、変更可能範囲、受入条件、参照Issue、禁止事項を記載する。既存の作業票・dirty tree・セーブ・v5を保護する。
Claude Codeはリポジトリを作業ディレクトリとして非対話モードで起動する。CLI専用セッションとデスクトップの会話履歴は自動共有を仮定しない。必要な仕様は必ず作業票から読む。
実行ごとに依頼、CLI標準出力(JSON)、標準エラー、終了コード、session_id、開始/終了時刻を保存する。認証情報やトークンは保存しない。
同一リポジトリの編集は1実行だけ。実行中の重複依頼・Unity検証は禁止。タイムアウト・古い状態・CLI終了だけでは完了と扱わない。曖昧な実行の自動再送は禁止。
完了はCLI成功、is_error=false、task_id/attempt_id一致、Claude状態review_ready/editing_stopped=true、変更と試験結果の照合を条件とし、実機検証は別に管理する。
権限エラーは失敗理由として返す。包括的な権限チェック無効化フラグは標準化しない。commit/pushと無関係なファイル変更は禁止。
既存claude-status.jsonは旧士気検証の履歴として保持。CLI接続試験で上書きしない。

## 初回接続試験
ゲームを変更しない。CLI認証を確認し、読み取り専用でこの作業票からtask_idを返させる。結果を保存して応答を検証する。
接続後、ランナーなどプログラムの実装が必要なら本契約をClaude Codeへ渡して実装させる。ChatGPTはコードを書かず、レビュー・試験を担当する。

## 現在の段階
CLI導入・認証確認中。自動受け渡しはまだ有効化していない。旧タスクの通常会戦観測は画面操作待ち。

公式資料: https://code.claude.com/docs/en/headless

## 導入結果
2026-09-13: WinGet公式パッケージ Anthropic.ClaudeCode 2.1.268 をインストールし --version を確認。ユーザーPATH追加済み（既存シェルは更新前のPATHのため絶対パスを使用）。
実行ファイル: C:/Users/htccj/AppData/Local/Microsoft/WinGet/Packages/Anthropic.ClaudeCode_Microsoft.Winget.Source_8wekyb3d8bbwe/claude.exe
認証状態: auth statusでloggedIn=falseを確認。auth login --claudeaiを開始し、本人のブラウザ承認待ち。API従量課金用consoleは選択していない。
未完了: 認証、ファイル入力/JSON出力の接続試験、Claudeによる排他ランナー実装、定期監視への接続。認証前に自動実装を開始しない。

## 2026-09-13 接続試験
CLI認証済み（Claude契約）。ファイルを標準入力へ渡す読み取り専用試験は終了コード0、is_error=false、期待したtask_id応答とsession_idを確認済み。証跡: C:/Users/htccj/Documents/Codex/2026-09-09/new-chat/outputs/claude-cli/smoke-20260913/。
受け渡しツールは実際のClaude CLIに実装依頼済み。範囲はnew-chat/outputs/claude-cli/runnerのみ。ゲームリポジトリは編集しない。CLIセッション1874de6b-6148-4c65-8054-23c90f68e37e。完了・自己試験・ChatGPTレビュー前は未有効。
関連Issue: https://github.com/yamamax55/ginei-game/issues/1043
定期確認はCLI記録を優先し、共有画面への入力は引き続き行わない。

## 設定完了・標準運用（2026-09-13）
ChatGPTが修正後ツールを実行し、ローカル51項目＋実際のClaude接続1項目、計52/52合格、終了コード0を確認した。実装はClaude Code。レビュー差戻しで状態情報の有効期限を24時間から既存規約の15分へ修正済み。
ランナー: C:/Users/htccj/Documents/Codex/2026-09-09/new-chat/outputs/claude-cli/runner/Invoke-ClaudeTask.ps1
使用方法・manifest形式: 同フォルダ README.md。
検証証跡: 同フォルダ chatgpt-validation.txt、chatgpt-validation-exit-code.txt。
今後の依頼は本ランナーへMarkdown作業票を渡し、OutputRoot配下のtask_id/attempt_id/status.jsonとstdout.jsonを照合する。定期確認はプロセスと状態ファイルを見る。CLI終了だけで完成としない。
通常編集には15分以内の編集停止状態と、依頼ハッシュ・ID・状態スナップショットが一致するhandoff manifestが必要。ChatGPTが状態を捏造して起動条件を通さない。デスクトップClaudeとCLIの同時編集はランナーのロックだけでは防げないので、切替時に旧タスクの停止を確認する。
固定の正規リポジトリパス C:/Users/htccj/Documents/ginei-game を使用し、別ドライブ名やジャンクション経由で同じリポジトリを渡さない。
この初期版はClaudeのツールをRead/Grep/Glob/Edit/Writeに限定。ビルド・試験のコマンド実行はChatGPTが担当する。CLIに包括的な権限無効化を渡さない。実ゲームの編集ジョブは今回起動していない。
旧士気タスクのclaude-status.jsonは古いため、そのまま新規編集を開始しない。通常会戦の原因観測も引き続き未完了・画面操作待ち。
