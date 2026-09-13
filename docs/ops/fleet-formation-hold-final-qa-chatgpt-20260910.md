# 仕様1 最終QA・ChatGPT検証記録（進行中）

Task: fleet-formation-hold-final-qa-20260910
Issue: https://github.com/yamamax55/ginei-game/issues/2253

Claude revision3（2026-09-10 22:12:25 JST）の編集停止を確認。製品コード変更なしという報告。追加2ファイルは未追跡ファイルのためgit diffが空でも「変更なし」とは判断せず、内容を直接レビューした。

## UnityでChatGPTが実行した試験

- 一括17/17合格、0失敗。22:20:53〜22:21:01 JST、test-run duration 7.5793633秒。
- 新規：攻撃終了/標的消失、個別1/1合格（22:22:37終了）。
- 新規：停止/再開/倍速、個別1/1合格（22:23:23終了）。
- 新規：次会戦への状態残留、個別1/1合格（22:24:21終了）。
- 証跡：fleet-formation-hold-final-qa-all-20260910.xml と single-attack / single-speed / single-lifetime の各XML。
- TestRunner全25件のうち本対象17件。無関係8件は今回実行していない。

## レビューで確認した適用範囲

- 時計のpaused/speedおよびTime.timeScaleを試験前の値へ復元するSetUp/TearDown追加を確認。
- 攻撃終了は実際の標的オブジェクト破棄を通す。
- 会戦切替試験はオブジェクトの破棄/再生成。シーンロードを通さないため、実機画面遷移の代用にはしない。
- 倍速試験はTime.timeScaleを入力条件として変更。正規PauseManager/UI経由の確認は実機で実施する。また当該試験には敵がなく、AIが陣形変更を試みたこと単独の証明にはならない（敵つきの既存試験は別途合格）。
- QA倍速メニューはPauseManager.TogglePause/SetTimeScaleのみを呼ぶ。配属換えはcorpsNameだけ変更し保持解除結果を直接書かない。

## 実機で追加確認した結果

- 停止中の円陣指定：HUD・通知・保持内部状態が一致。QA自軍2のスキルPは75→50で25のみ消費。
- 配属換え：QA自軍2を他軍団へ変更すると、停止中でも保持解除。指定元は空、円陣自体は維持、P50のまま。「指揮系統の変更」の解除通知を確認。
- 3倍速再開：HUDのSPEED3.0と円陣（保持・直接命令）を確認。交戦中ダンプでも保持True、AI状態交戦、overrideなし。Time.timeScale3.0。戦況カード中は統一クロック/UI速度0.6へ減速していたため、一定3倍速だけの連続試験とは区別する。
- 実会戦決着→Strategyへ帰還→新規QA会戦：全6艦隊の保持False、軍団総退却Falseを確認。開始直後のAIが指定する陣形/指定元は正常なため「指定元なし」を全艦へ要求しない。
- 生ログ：fleet-formation-hold-final-qa-runtime-20260910.txt。

## 残る検証と判定

今回の追加レビューと実機確認範囲で仕様1の新たな不具合は未検出。ただし自然敗走の継続状態、支援要請の受諾通知と費用の同時照合、通常UIからの攻撃完了/標的喪失の実機通し確認は未完了（関連PlayMode試験は合格）。従来のQA士気0投入は即回復するケースがあり、自然敗走合格の根拠にはしない。

QA操作はPauseManagerの公開処理を呼ぶメニューを使用。自動クリックによる画面内再開ボタンの入力問題が解消したとは判断しない。
仕様1の最終判定は保留。仕様2未開始。

セーブSHA256確認：E850E1F3F11290EEB7E4620FFE80455CA6AF7C5BD9BBF4E5D67A34858DF71367（変更なし）。

## 2026-09-10 追検証：入力記録

Claudeの診断追加（input-review revision3）をレビューし、UnityでChatGPTが観測した。

- 今回のフル画面Battleでは、アクティブポーズ入力可、BattleViewport.Active=False、FleetCommanderの両関門は通過。BattleViewportが原因という仮説は今回の観測では成立しない。過去の別セッションまで否定するものではない。
- 再開ボタンを自動クリックして再開、同じボタンで再停止できた。F30406→30409でts0→1、F32730→32732でts1→0。いずれもPauseButton直下、通常のEventSystem→OnPointerClick→OnClicked→Toggleのスタックを確認。今回は直接判定の迂回ではない。
- 最初の艦隊選択クリックはUpdate観測に記録されず、通知TitleBarのドラッグも移動しなかった。これだけでOS入力未着とは断定できない。
- Unityメニュー相当位置pos(62,1252)でF35628/t447.41の左押下が記録され、次の艦隊クリック相当pos(716,210)でF42179/t480.28に左解放・選択数0→2を観測。画面では狙った自軍1隊と異なる他軍2隊が選ばれ、コマンドメニューが自動表示された。
- FleetCommander.HandleLeftSelectionInputは押下開始点と解放地点で矩形選択するため、この残留した押下が範囲選択に繋がった可能性が高い。入力元、イベント欠落が起きる場所、どのフォーカス移動で残ったかは未確定。
- 診断器はUpdateでのフレーム観測で、OS生イベントや全Pointerコールバック記録ではない。診断文中の「記録なし＝アプリに入力未着」や「フレーム差が大きい＝ドラッグ」の断定は採用しない。

証跡：fleet-formation-input-gates-20260910.txt、fleet-formation-input-trace-20260910.txt。今回製品コード変更なし。

次の実機検証ではUnityメニュー／ダイアログから復帰した直後の残留押下を切り分け、意図した1隊が選択されたことを確認してから発令する。仕様1の残る必須3項目（自然敗走継続、支援受諾通知と費用、攻撃完了／標的喪失）は依然未確認。仕様1は最終受入保留、仕様2未開始。

## 2026-09-12T12:57:17.2006528+09:00 仕様1の実機QA続行と観測補助の再開

Task: fleet-formation-hold-observation-20260910

ChatGPTの追加実機確認：空白クリックで選択を解除後、自軍1隊の選択、円陣保持（直接命令）、QA敵1への通常攻撃発令が成功。再開後、敵旗艦2隊の撃沈とBattleManager.CheckVictory経由のStrategy帰還をログ確認。ただし標的喪失直後の陣形保持状態は帰還前に取得できず、未合格。証跡 docs/ops/fleet-formation-direct-attack-runtime-20260910.txt。

Unity Play停止後、Claudeに短い状態変化をシーン帰還後も読める最小の検証記録を依頼済み。作業票 docs/ops/fleet-formation-hold-observation-20260910.md。9/12確認では状態ファイルが9/11 00:13:19のworking revision1で止まり、Claude/Unityとも起動していなかった。Claudeを開き、既存の同じ依頼が観測用ManualTargetFleet getter追加の段階にあることを画面確認。同task_idの続行を送信し、重複した新規依頼は作っていない。

FleetWeapon差分は読み取りgetter6行のみを確認。実装全体の完成・コンパイル・実機合格は未確認。自然敗走継続、支援受諾通知と費用、標的喪失後保持は残件。仕様1最終受入保留、仕様2未開始。既存セーブSHA256は従来値と一致。次はClaudeの新しい状態更新と編集停止を確認後にレビュー・実機検証。
