更新: 2026-09-10T20:31:16.5699322+09:00

# 仕様1 fix1 レビュー・Unity実機検証結果／fix2依頼

対象Issue: https://github.com/yamamax55/ginei-game/issues/2253
次のtask_id: fleet-formation-hold-20260910-fix2
担当: プログラム修正はClaude Code。ChatGPTはレビュー・Unity実機・GitHub。

## 判定
仕様1は未合格。仕様2へ進まない。UnityはPlay OFFに戻した。既存セーブSHA256は前後不変。

## ChatGPTが実行した試験
Unity 6000.6.0f1 Test Runner / PlayModeでFleetFormationHoldPlayModeTests全14件を実行。11合格・3失敗、所要16.647秒。
証跡: docs/ops/fleet-formation-hold-fix1-playmode-20260910.xml
ClaudeのTestHarness9676件合格は別の試験。PlayMode合格と混同しない。

失敗:
- CorpsRetreatOrdered_RejectsNewHoldForDirectOrderedFleet (line 261): 撤退中を期待、実際は受理。
- Rout_ReleasesHold_ButNotMovement (line 444): 保持解除を期待、実際は保持true。
- SupportRequest_AcceptedFormationBecomesHold (line 369): 支援要請後10秒でも保持false。

合格には実AI周期の保持、保持なしのAI対照、実到着後保持、保持解除後AI復帰、拒否時保持・費用不変、同一陣形無料、所属変更解除、総退却時既存保持解除、撤退移動中の新規指定拒否、軍団AIによる上書き防止を含む。

## レビューで見えた切り分けポイント（原因確定ではない）
1. 総退却例外艦テストは軍団長AIState=撤退を待つだけ。個別AIの撤退と軍団総退却発令を同一視していないか。実IsCorpsRetreatOrderedの成立を前提assertし、直接命令・移動の継続を確認してから新規保持拒否/ポイント不変をassertする。
2. 敗走テストは士気を0にして5フレーム待つが、実IsRouted成立をassertしていない。FleetMoraleはlastCombatTimeから4秒経過した非交戦艦を直ちに回復しうる。実際の敗走状態とFleetAI.Updateの処理順、テスト間時刻や初期化を確認。条件を成立させ、保持だけ解除・直接命令継続・実移動を検証する。
3. 支援要請は承諾そのものを確認せず保持だけ待っている。受付→判断（検討/承諾/拒否/失効）→実行結果のどこで止まったか記録。時計、初期化、EffectiveLeadership、士気、資格、費用を確認。判定を飛ばして直接保持を設定する試験に戻さない。
テストの前提不備ならテストを直し、ゲーム側なら最小修正。単に期待値を緩めたり失敗ケースを削除しない。14件を個別・一括で成立させる。

## 会戦画面で確認した事実
QA指揮権限会戦でQA自軍2に右クリック陣形メニューから円陣を指定。通知とHUDの「円陣（保持・直接命令）」を確認。移動命令を出し、画面座標およそ(777,887)から(1263,850)への移動と戦闘後も保持を確認。ただし実機での到着完了/Override終了は未判定（PlayModeの実到着ケースは合格）。
続いて遠い目的地へ直接移動命令を再発令し、QA「陣形保持 保持中の艦を敗走させる」を実行。対象QA自軍2/直接命令であることをモーダルで確認。ゲーム停止中の次フレームで「陣形保持を解除しました（敗走）」通知を確認。再開後、対象が追撃で旗艦撃沈となり消失したため、直接移動の継続については判定不可。安全な距離で敗走条件を維持できるQAを用意するとよい。
再開ボタンは短いドラッグでは反応しない回があり、改めて通常クリックすると再開・停止できた。入力取りこぼしは既知別件として区別。
Play終了時の「Some objects were not cleaned up」警告、Test Runner開始前のFont MaterialのDestroy edit-mode警告は別記。今回の3失敗の原因と断定しない。

## 修正後の引継ぎ
上記3件の原因と修正・前提を結果文書へ。必要最小限の実機手順（撃沈を避ける前提、選択軍団だけの一括指定、軍団解除と艦隊保持の分離、総退却中拒否、支援要請承諾）を記載。Unity試験を未実行ならその旨明記し、編集停止してChatGPTへ引き継ぐ。
状態ファイルは新task_idで受理/段階変更/テスト結果/編集停止時に更新。仕様2・3とQA分類はこの依頼で実装しない。既存セーブ・v5・他の未コミット変更を保護し、commit/pushしない。