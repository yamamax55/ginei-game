# 緊急退却QA記録の再開待ち対応

Task ID: support-emergency-recorder-fix-20260910
Issue: https://github.com/yamamax55/ginei-game/issues/2253

ChatGPT実機結果: docs/ops/corps-retreat-battle-20260910.md と証跡を読むこと。PlayMode4件Passed、軍団Cは独立した前後座標と通知で他軍団2隊の後退を確認済み。Dほかは未確認。Unity Play OFF。

問題: SupportEmergencyQaMenuの記録がEditorUtility.DisplayDialogを開いている間とTime.timeScale=0の間にも実時間10秒で終了した。再開前に3行だけで終わり「中断なし/撤退なし」を表示する。検証を妨げている。

依頼（プログラムはClaude担当）:
- QAの記録時間は会戦が実際に進行した時間だけ積算する。Unity停止、ダイアログ、timeScale=0の待ち時間を消費しない。
- 最初の6フレームも実際に会戦が進行したフレームだけ対象にする。以後の記録間隔と10秒の観測期間の意味を明示し、倍速時の扱いも説明する。
- 誘発前/直後の前提記録は保持。記録中・再開待ち・完了・未観測を明確に区別し、未観測を不合格や合格と断定しない。
- QAからAI状態、override、移動先を直接書き換えない。誘発する入力条件以外の製品ロジックは変更しない。
- この問題に必要な検証のみ実施し、報告・差分・未実機項目を残す。
- 別件の入力取りこぼし・DamagePopup未解放警告は調査記録に残すだけで今回は触らない。

編集開始/テスト/編集停止時にclaude-status.jsonをこのtask_idで更新。実時計取得、revision加算。chatgpt-review.jsonはChatGPT専用で触らない。完了はreview_readyかつediting_stopped=true。既存セーブ・v5・無関係差分保護、commit/pushなし。結果文書をdocs/ops/support-emergency-recorder-fix-20260910-result.mdへ。