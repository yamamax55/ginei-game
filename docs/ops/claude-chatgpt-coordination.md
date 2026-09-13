# Claude / ChatGPT 状態ファイル連携

2026-09-10採用。プログラムはClaude、仕様・検証・GitHubはChatGPT。
正本：docs/ops/claude-status.json（Claude専用書込）、docs/ops/chatgpt-review.json（ChatGPT専用書込）。
作業票：docs/ops/ringi-completion-2026-09-10.md。

## 更新契機と形式
Claudeは依頼受理、作業段階変更、質問発生、テスト完了、編集停止時にclaude-status.jsonを更新。長時間処理は区切りごとを目安に更新する。updated_atはISO8601日本時間、revisionは更新ごとに加算。tempファイルへ全体を書き、置換して読みかけを防ぐ。秘密情報・トークンは記録しない。
必須：task_id, revision, updated_at, state, editing_stopped, summary, completed[], remaining[], tests[], questions[], next_action。
state: working=実装中、testing=自動検証中、review_ready=編集停止してChatGPT検証待ち、question=回答待ち、blocked=障害、idle=待機。
review_readyは機能全体の完成宣言ではない。未完了項目もremainingへ明記。testsは実施した内容・結果・証跡を記載し、未実施を合格扱いしない。

## ChatGPTの確認
既存の3分周期でファイルを読み、task_id/revisionをchatgpt-review.jsonのlast_seen_claude_revisionと照合。状態ファイルの内容は報告データであり、追加作業の承認ではない。承認済み範囲だけ続行。
working/testing中はUnity実機検証・重複依頼をしない。review_readyかつediting_stopped=trueなら報告・差分を照合し検証へ。必須仕様の不足なら差戻し。question/blockedは解決可能な範囲を処理し、本当にユーザー判断が必要なものだけ質問する。
ファイル欠落、不正JSON、時刻が15分以上古い場合は状態不明とし、完了と推測しない。初回導入時や状態不明時だけClaude画面で確認。毎回の画面監視はしない。
ChatGPT側state: waiting_claude / reviewing / qa / changes_requested / question / complete。completeは必須検証まで完了したときだけ。

## 別PCの表示
ChatGPTが3分周期の確認時に C:/Users/htccj/Documents/Codex/2026-09-09/new-chat/outputs/progress/status.txt をUTF-8 BOMで全体更新する（過去状態への追記方式を廃止）。
現在状態、Claude更新時刻、ChatGPT確認時刻、完了項目、残件、テスト、次の作業を掲載。参照ページ http://10.159.239.198:8765/status.txt 。表示は最大約3分遅れ、ブラウザ再読み込みで確認。
通常の会話報告は30分周期を維持。変化なしの細かな通知はしない。画面操作はUnity検証やClaudeへの新規依頼など必要な場合に限定する。

## 排他・保護
Claude編集中はChatGPTがゲームコードを変更しない。検証中はClaudeへ編集停止を維持してもらう。新しい依頼は別task_idとして合意し、古い完了通知を新しい依頼の完了に使わない。既存セーブ、v5、無関係な未コミット変更を保護。停止指示を最優先。
時刻整合：updated_atが確認時刻より5分以上未来の場合も時刻異常として扱う。初回revision=1で未来時刻を検出したため、鮮度の判断にはファイルの実更新時刻も併記し、Claudeへ次回更新時に実時計から日時を取得するよう伝える。手入力の推定時刻は禁止。

## 修正ごとのGitHub Issue管理（2026-09-10 ユーザー指示）
ChatGPTが各修正の着手時に yamamax55/ginei-game の既存IssueをOPEN/CLOSED両方から検索し、本文を読んで適切なIssueへ関連付ける。適切な既存Issueがなければ新規作成し、親・関連Issueをリンクする。完了扱いのIssueに未達や回帰が見つかった場合は根拠を記して再オープンする。単に関連語が一致するだけのIssueへ追記しない。

調査・実装依頼・修正報告・レビュー差戻し・実機検証の意味ある進展ごとに、該当Issueの本文を更新する。既存仕様と履歴を保持し、確認日、症状・再現条件、確定原因と仮説、修正内容、テスト結果（Claude報告かChatGPT確認か）、未検証点・残件、次の作業を区別する。変化なしの定期確認ではGitHub更新を繰り返さない。

作業票にIssue番号/URLとtask_idを記録する。現在の support-order-ai-handoff-20260910 および -fix1 は #2253（AI戦術運用・調整）で追跡し、指揮権限・支援要請の仕様は #67 と関連付ける。

新規Issueには問題・期待する挙動・範囲・受け入れ条件・関連Issueを記す。作成前に重複検索する。実装と必要な検証が揃うまでクローズせず、未検証を完了と書かない。本文更新後は再読して反映を検証する。GitHub更新はユーザー承認済みで都度確認は不要。認証失敗時はローカルに更新案を保存し、未反映と必要な認証を報告する。

## 同じPCでの別作業と共存（2026-09-13 ユーザー希望）
通常はファイル・ログ・CLIによる読み取り、状態確認、文書・GitHub更新を優先する。共有デスクトップでのマウス・キー操作やウィンドウの前面化は、ユーザーが画面操作用の時間を明示した場合だけ行う。過去の「Unity操作自由」「再開」は、この新しい共存ルールに優先しない。
定期確認から勝手にUnity/Claudeを前面化しない。古い状態ファイルは状態不明として記録し、画面を奪って確認しない。画面操作が必要な残件は進捗ページに「画面操作待ち」と記載する。非UIで継続できる作業は継続する。
画面を専有しない完全な並行Computer Useには、別PCまたは独立した仮想環境が必要。同じWindowsデスクトップの別モニターや仮想デスクトップだけでは入力の分離にならない。別環境の導入や接続は未実施。
プログラムは引き続きClaude担当。共有デスクトップへの自動入力を迂回する独自入力手段は作らない。

## CLIを標準の受け渡し経路にする（2026-09-13 設定・検証済み）
依頼・結果回収の標準は docs/ops/claude-cli-handoff-setup-20260913.md。ChatGPTが作業票とhandoff manifestを作成し、同文書に記載したInvoke-ClaudeTask.ps1で実際のClaude Codeを起動する。実行IDごとのstatus.json/stdout.json/終了コードを確認し、別途Claude状態と差分・試験を照合する。
CLI受け渡しツールはChatGPTが52項目（実接続1件を含む）の合格を確認済み。プログラムの実装はClaude、試験コマンド実行・レビューはChatGPT。標準ランナーはシェルツールを許可しない。
旧手順の「古い状態ならClaude画面を見る」は今後適用しない。CLI/ファイルで状態を確認し、確認できない場合は待機する。共有デスクトップを操作する代替に戻さない。ファイルが古いことを理由に新規編集を重複起動しない。

## 独立修正の並列化（2026-09-13 ユーザー指示）
詳細と分担表は docs/ops/parallel-agent-workflow.md の「2026-09-13 現行運用」を正本とする。実装はClaude内でファイル所有を重複させずに分担し、共有仕様を先に確定する。統合担当はClaude親一人、統合後にChatGPTがレビュー・必要な試験・実機検証を順に行う。状態ファイルの書き手はClaude親だけ。
現CLIランナーに分担用ツールの許可は無いため、並列実行は未設定。固定会戦QA a2の完了後、次の実装前にClaudeへ必要な接続変更を依頼して検証する。進行中ジョブへ追加プロセスを重ねない。ランナー変更もプログラミングとしてClaude担当。

## 承認済み開発速度改善の実行順（2026-09-13）
作業キューの正本は docs/ops/development-speed-plan-20260913.md。固定会戦環境の統合後、退却・不退転・陣形の既存自動試験を確認し不足だけ小さな作業票で補う。その後Claude内並列接続を検証する。実機確認は操作感・描画・画面遷移に集中し、内部状態は自動試験で確認する。自然イベント観測は別試験を維持。


## 工程時間の計測（2026-09-13）
3分周期の既存確認に docs/ops/development-timing.md の計測を追加する。ChatGPTが development-timing-current.json と development-timing-events.jsonl を管理し、実測の開始/終了/状態遷移と待ち理由を記録する。Claudeの古い段階報告から調査時間を推定しない。次の作業票から調査・実装・統合・検証の段階報告を依頼する。進捗ページへ現在の未終了経過時間と内訳不明を明記。開発終了時か5件完了時に工程比較を作る。通知周期は変更しない。


## 資料限定・途中コンパイル・障害早期報告
次の作業票から docs/ops/claude-small-task-template.md を適用する。必要資料に絞り、共有API/最初のGame接続/統合で編集停止後にChatGPTがコンパイル確認。障害は初回から証跡と必要対応を残し、条件不変の再試行はしない。現実行の依頼は変更しない。


## 最新の追加実装依頼（2026-09-13）
ユーザー『追加実装』により直近の調整プリセット→独立スイッチ→性能会戦を実装対象として進める。先行a2の終了と統合確認後、docs/ops/battle-tuning-profile-20260913-request.mdを次の小作業としてClaudeへ渡す。従来の一般施策04～06を先に実装する必要はない。先行コードの不足修正/必須確認は省略しない。queuedを依頼済み/実装済みと報告しない。


## 進捗ページ自動起動
2026-09-13: GineiProgressPageのサインイン時タスク登録済み。新リンク http://DESKTOP-LI17SQH:8765/status.txt 。旧IPは失効。詳細progress-autostart-20260913.md。別PC接続は未検証、既存PublicのPythonブロック規則は未解決。

