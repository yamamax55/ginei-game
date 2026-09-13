# revision8 実機試験レビュー

対象: morale-lock-rout-consistency-20260912 / revision8。関連 #2253 / #2175。

## 確認結果
Unity Test Runnerで NaturalRecovery_RoutDoesNotClearWhileStillUnderFire を単独実行。2026-09-12 05:39:19 UTC終了、0/1合格、1失敗。5.0ゲーム秒の観測で非敗走の検出545回、最終士気1.014。これは545回の状態遷移を意味しない。証跡: docs/ops/morale-lock-r8-underfire-20260912.xml（399行assert）。UnityはPlay OFF。既存セーブSHA256不変。

## 原因の切り分け依頼
試験には実FleetAIがあり、autoFormation=falseだけでは特殊指揮は止まらない。ConsiderActiveCommandは低士気から特殊指揮を選択する。長くした観測にAIの不退転発動が混入し、正常な士気下限1で敗走が解けた可能性がある。ただし発動の記録は未確認で仮説。現段階で製品回帰とは断定しない。

Claudeに依頼: 失敗を調査し、自然回復試験2件では不退転が発動しない前提を明示して観測中もassertする。実TakeDamageとFleetMorale.Updateによる被弾・回復は残す。回復を無効にしたり結果の士気/敗走フラグを直接上書きして合格にしない。必要な最小限の試験設定で特殊指揮との干渉を分離し、原因・修正範囲を報告する。製品変更が必要なら実証した原因に限定する。B/Cの仕様追加は行わない。

編集後は状態ファイルのrevisionを上げ、editing_stopped=trueで引き継ぐ。仕様2、配下艦艇改善は着手しない。セーブ・v5・無関係差分を保護、commit/pushなし。

## 残件
補正後の自然回復2件単独、全9件、陣形保持17件。その後通常会戦で不退転中/終了後・無保護の継続被弾/被弾停止・陣形保持を確認する。仕様1は未承認。
