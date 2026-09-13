# 支援要請の承諾後の命令維持

task_id: support-order-ai-handoff-20260910
担当: 実装Claude、仕様・検証ChatGPT

観測: QA他軍2への移動要請の送信・承諾通知は実機確認済み。指定先到達は未確認。SupportRequestDirector.Executeは移動でClearOrder→SetDestinationのみ。通常のFleetCommanderはBeginManualOverrideも呼ぶ。FleetAI.UpdateはmanualOverrideなしでは継続してSetDestinationする。承諾後のAI上書きが疑われる。

依頼: この差を調査し、承諾した移動を到着まで尊重するよう修正する。攻撃と陣形についても同じ競合を確認する。既存の軍団指揮系統と要請/直接命令の区別を維持。撃沈・敗走など緊急動作を不当に妨げず、完了後はAIへ復帰。最小範囲で修正し、必要な回帰テストを実行。UIで承諾しただけでは到達完了と扱わない。

検証: 対象艦隊の現在座標・指定先・AI状態・命令維持状態を読み取れる既存診断を活用。足りなければ読み取り専用診断を追加。指定先へ向かい、途中でAIが上書きしないことを検証できるようにする。

自動クリック補足: 最新スクリーンショットID付きクリックで停止/再開は成功。微小ドラッグでも通常OnClicked経路で成功。過去失敗原因は未確定で追加修正は不要。docs/ops/automatic-click-investigation-20260910.md参照。

既存セーブ・無関係な変更・v5を保護。commit/pushなし。claude-status.jsonに本task_idで受理と完了を記録。完了時はediting_stopped=true、検証証跡と未確認項目を明記する。
