# 仕様1の実機検証を妨げる入力経路の調査
Task: fleet-formation-input-review-20260910
Issue: https://github.com/yamamax55/ginei-game/issues/2253

Unity Play OFFを確認して依頼。仕様1は17/17と追加3件個別合格。結果はfleet-formation-hold-final-qa-chatgpt-20260910.md。仕様2は未開始。

停止中の現会戦で自動入力による艦隊選択/通知ドラッグが応答せず、通常UIによる残検証が進まない。Unityメニューとネイティブダイアログはクリックが通る。前の会戦では陣形メニュー指定は成功したため、恒常的故障とはまだ断定しない。最新入力診断はfleet-formation-input-recheck-20260910.txt。EventSystem1、有効、DynamicUpdate、focusedTrue、activePauseAllowedTrue、modal/systemUIガードFalse。再開ボタンのEventSystem/直接到達は0。ただしこれはボタン内部計数のみなので、生のマウス入力自体が来ていない証明ではない。押下/離上/短時間クリックの取りこぼし、選択解除条件、UI重なりを区別すること。

依頼: まず既存入力実装と診断をレビューし、原因確定に必要な最小の観測手順を提示。必要ならEditor/QA限定でraw down/up/位置/フレーム、EventSystemのdown/up/click、選択判定とガード理由を有限履歴に記録する補助のみ追加可。製品側の修正は原因根拠がある場合のみ最小限。結果状態を強制設定して合格にしない。自動入力制約ならその旨と操作案を記す。無根拠に入力設定を変えない。合格済み試験の反復や新規大量試験を避ける。
残る本来のQA: 自然敗走継続、支援受諾通知と費用、攻撃終了/標的喪失後保持。これらへの具体的UI手順も記す。
セーブ/v5/無関係変更保護。commit/pushしない。作業受理時に新task_idでclaude-status.json更新、終了時review_ready/editing_stopped=true。ChatGPTはその間Unity実機操作を行わない。
