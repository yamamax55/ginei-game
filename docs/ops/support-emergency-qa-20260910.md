# 敗走・総退却の実機再現補助

task_id: support-emergency-qa-20260910
Issue: https://github.com/yamamax55/ginei-game/issues/2253

ユーザーは敗走・総退却の検証続行を依頼。fix1を未合格のまま引き継ぐ。Unity Play OFFにした。

検証用会戦に限定し、支援移動中/攻撃中の敗走、士気正常で艦艇数減少による軍団総退却、直接命令の同条件の対照を確実に再現できるEditor QA補助を実装してください。通常ゲームやセーブを変えないこと。
- 本物のSupportRequestDirectorの受理→承諾、FleetAI.Update、BattlefieldCommandManagerの判断を通す。命令解除/撤退状態をQAが直接設定して合格にしない。
- 敗走は士気、総退却は残存比率など入力条件を変える。自然なUpdateで期待結果になるか観測。
- 支援実行中の前提（OverrideKind=支援要請、移動中または手動標的あり）が満たされてから誘発。なければ未成立と表示。
- 前、直後、数フレーム/秒後に時刻・艦名・軍団・士気・残存比・override・手動標的・AI状態・座標・移動先を記録。有限時間で自動停止して観測を逃さない。軍団総退却は他の生存隷下も確認。
- 直接命令も同条件で既存動作を維持するか対照。攻撃支援は中断後に追尾が残らないか確認。
- 可能なら実際のGameコンポーネントを使うPlayMode統合試験を追加・実行。縮小版判断テストだけを実機合格としない。Unityを使う必要があれば状態ファイルへ明記し、終了時Play OFF。
- 実不具合が見つかれば最小修正し、原因/差分/再試験を記録。

claude-status.jsonを新task_idで更新。完了時はediting_stopped=true、操作するメニュー名と結果文書を報告。ChatGPTが再読・Unity確認する。既存セーブ/v5/無関係差分を保護、commit/push不要。GitHub更新はChatGPT担当。
