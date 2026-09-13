task_id: battle-tuning-profile-20260913
担当: 実際のClaude Code。Issue #1045。
状態: queued（未起動）。先行reproducible-battle-qa-20260913/a2の終了と統合確認後にChatGPTが引継ぎを作る。

目的: 固定QAで移動速度・旋回速度・軍団陣形間隔・士気回復量をコード変更せず比較できる、検証用調整プリセットを追加する。今回機能スイッチと性能測定は実装しない。
読む資料: docs/ops/battle-tuning-performance-plan-20260913.mdの現状とSPEED-07、docs/ops/claude-small-task-template.md。
読むコード: Assets/Scripts/Game/FleetMovement.cs(maxSpeed/rotationSpeed)、FleetMorale.cs(recoveryRate)、CorpsFormation.cs(spacing)、ReproducibleBattleQaSession.csとAssets/Editor/ReproducibleBattleQaMenu.cs。関連する設定型があればその定義だけ読む。

実装範囲: 既存Inspector項目を利用し、固定QA用設定と開始前編集/適用、実効値ログ、再試行/終了時の復元、必要な試験。既存QA設定が対応済みなら不足だけ補う。通常のアセット・セーブ・ゲーム既定値を変更しない。共通設定の正本を二重化しない。
受入条件: 既定プリセットは先行QAの実効値と一致。1項目だけ変えた比較で他項目は不変。設定名・値をログで特定。NaN/Infinity/不正値を拒否。終了/再試行/準備失敗/シーン離脱に対応。正確な性能や自然会戦の合格とは主張しない。
今回の区切り: 設定型と適用/復元接続と最小試験がコンパイル可能になった時点で編集を止め、未完了項目を列挙してChatGPTへ返す。小さな変更で収まれば全件をこの一回で実装してよい。

役割: コード編集はClaude、コマンドによるコンパイル/試験はChatGPT。Read/Grep/Glob/Edit/Writeのみ。未実行試験を合格としない。無関係な未コミット変更とv5を保護。commit/push/画面操作禁止。仕様2は対象外。
終了: 全編集停止、変更ファイルと必要な試験手順をdocs/ops/battle-tuning-profile-20260913-result.mdへ記録。claude-status.jsonを今回task/実行時に与えるattemptでreview_ready・editing_stopped=trueとする。段階変更も状態へ記録。時計が無ければ創作しない。
