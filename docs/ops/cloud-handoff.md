# Claudeクラウド移行チェックポイント

このブランチはローカルの開発途中変更を保存した移行用スナップショット。安定版・全件検証済み版ではない。

## 役割と範囲
プログラム実装はClaude Code、仕様整理・レビューはChatGPT。クラウドの小さな接続試験のみ許可。ゲームの大規模修正・masterへのマージ・自動デプロイは禁止。ローカル定期実装監視は停止中。クラウドでローカル用監視/通知/絶対パスのスクリプトを起動しない。外部Slack等への通知は行わない。

## 検証済みと未検証
- 先行の固定会戦・調整・機能スイッチ版は関連Core60/60、Unity PlayMode55/55合格。
- その後の battle-qa-reinforcement-isolation-20260913/a1 はClaudeが編集終了。援軍実接続、QAイベント通知先と対象勢力の隔離、関連試験を追加したが、コンパイル・試験は未実行。
- 次の確認はCore関連64件にShipNameRegistryTestsを加えた範囲、Unity PlayMode60件。結果レポートはdocs/ops/battle-qa-reinforcement-isolation-20260913-result.md。
- 軍団間隔の見た目・移動/旋回・通常会戦、自然イベント観測、30分プレイ評価は未確認。
- Coreはローカルでは.NET8不在のため.NET10ロールフォワードで実行。クラウドのTestHarnessは.NET8 SDK/Runtimeを利用できるか最初に確認。

## 接続試験
このブランチを基点に独立したClaudeブランチで、TestHarness/README.mdへクラウドでの関連Core試験手順を短く追記する。プログラムの振る舞い変更はしない。既存csprojとテスト名を読んで実際に動くコマンドを記載し、実行可能なら関連Core試験を実行。SDK等が不足なら未実行理由を正確に記録。Unity描画やPlayModeがクラウドで未設定なのに合格と書かない。結果はdocs/ops/cloud-smoke-result.mdへ対象コミット、変更、試験結果、残件を記録する。

## 保存対象
ゲームのAssets/Packages/ProjectSettings、既存追跡ファイル、開発文書を保存。ローカル一時出力・巨大な試験XML・稼働状態JSON・ログ・キャッシュ・個人認証情報はクラウド移行対象外。元PCには残す。