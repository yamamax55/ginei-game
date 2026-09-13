# 不退転と敗走判定の更新順調査・修正
Task: morale-lock-rout-consistency-20260912
Issue: #2175、統合検証 #2253
担当: 実装Claude / レビュー・Unity・GitHub ChatGPT

Unity Play OFF。前taskの編集を受領し、仕様1の残QA3項目は今回実機で確認できた。ただし新たに不退転中の敗走フラグが毎フレーム近く反転する疑いがあるため、仕様1最終承認前に調査・必要な最小修正を行う。仕様2は触らない。

必読証跡: docs/ops/fleet-formation-observation-review-20260912.md の末尾、fleet-formation-targetloss-rout-runtime-20260912.txt、fleet-formation-support-runtime-20260912.txt。
再現: QA自軍2円陣保持→QA敵1へ攻撃→Sinkで敵1消失→再開、元目的地への移動完了後保持したまま自律戦闘→敵2との被弾で自然敗走→不退転発動後、敗走/解除が1フレーム間隔で反復。

確認済み: IsRouted=morale<=0。UpdateMoraleでactiveMoraleLock時moraleを1へ戻すがChangeMoraleは床0。被弾直後から次Updateまでの観測窓が原因候補であって、まだ全経路の確定原因ではない。

依頼:
1. 不退転効果の有効期間・発動/終了順序・士気変更経路を調査。今回の反転との対応を確定する。
2. 効果中は被弾後やUpdate順に依存せず敗走しないという#2175の仕様へ一貫させる。陣形保持・支援中断・退却AIが同じ有効敗走状態を見ること。既存の通常敗走や軍団総退却の優先順位は保護。効果終了後の通常判定も確認。
3. 最小の修正と意味のある回帰試験。効果中の被弾→AI更新前のIsRouted、陣形保持、支援命令、効果終了、効果なし自然敗走を含む。必要ならEditor観測にactiveMoraleLockと士気値を追加し、値の自然増減だけでログを増やさない。
4. 完了時に状態JSON更新、検証根拠・未検証・変更ファイルを結果文書へ書き、editing_stopped=trueで返す。状態JSONは実時計・revision更新、作業中も節目で更新。

ChatGPTはゲームコード未変更。既存セーブ・v5・無関係差分を保護。commit/pushなし。仕様2・配下艦艇改良は範囲外。
