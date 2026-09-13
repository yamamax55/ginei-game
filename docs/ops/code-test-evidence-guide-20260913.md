# 開発案内図・試験選択・証拠運用

2026-09-13 ChatGPTによる静的確認。ファイル実在と記載した入口を確認した索引であり、各機能の完成証明ではない。実装担当はClaude Code。進行中の固定会戦QAの追加ファイルは統合レビュー時に追記する。

## コードの案内図
以下はリポジトリ相対パス。関連Issueは履歴上の調査先であり、各ファイル全体の実装Issueを意味しない。

| 対象 | 読み始めるファイル | 役割・注意 | 関連Issue |
|---|---|---|---|
| 軍団・艦隊データ | Assets/Scripts/Core/Fleet/StrategicFleet.cs、CorpsAssignmentRules.cs（同ディレクトリ） | corpsId・isCorpsFlagship・commanderPersonId。軍団旗艦指定と各艦隊の旗艦を混同しない | #67 指揮系統 |
| 軍団表示・命令 | Assets/Scripts/Game/CorpsOrganizationPanel.cs、CorpsFormation.cs | 編成表示、FormCorps、ReleaseManualOrder。指揮権限も確認 | #2253 AI・命令調整 |
| 艦隊操作・移動 | Assets/Scripts/Game/FleetCommander.cs、FleetStandardOrder.cs、FleetAI.cs、FleetMovement.cs | プレイヤー標準命令とAIの優先関係。FleetStandardOrderは全AIの共通入口ではない | #2253 |
| 配下艦艇・旗艦表示 | Assets/Scripts/Game/FlagshipMarker.cs、Assets/Scripts/Core/Fleet/EscortSlotAssignmentRules.cs | マーカーは表示。旗艦の戦闘上の生存/識別は別経路も調べる | #2389（配下艦艇調査） |
| 士気・不退転 | Assets/Scripts/Game/FleetMorale.cs、Assets/Scripts/Core/Combat/MoraleAuditLog.cs、MoraleLockRules.cs（同ディレクトリ） | ApplyWithAuditで原因記録。敗走中の回復待ちと通常回復を区別 | #2253、#2175（過去観測） |
| 決裁・稟議 | Assets/Scripts/Game/DecisionBoardPanel.cs、DecisionDeck.cs、DecisionCampaignDirector.cs、Assets/Scripts/Core/Government/DecisionResolutionRules.cs | ボード→DecisionDeck.Resolve→Settleを確認済み。全自動処理経路の統一までは今回未調査 | #67、#141 |
| 保存・再開 | Assets/Scripts/Data/CampaignSaveManager.cs、Assets/Scripts/Core/Society/CampaignSaveData.cs | SaveSession/LoadSession。軍団所属・艦艇数・指揮官・稟議の往復確認 | 各機能Issueへ関連付け |

更新規則: 変更した入口、依存、対応試験を各作業の統合時に更新する。行番号の固定やファイル全体の転載は避ける。

## 変更に応じた試験の選択
Assets/Tests/EditMode と Assets/Tests/PlayMode の実在ファイルを起点とする。表は最小候補であり、依存変更や失敗があれば関連範囲へ広げる。全体コンパイルは省略しない。

| 変更 | EditMode候補（ファイル名） | PlayMode・実機 |
|---|---|---|
| 軍団・指揮権限・退却 | CorpsAssignmentRulesTests、CorpsRetreatRulesTests、CommandOrderSourceTests | 固定会戦で各艦隊の実移動と所属外非影響 |
| 士気・不退転 | MoraleLockRulesTests、MoraleAuditLogTests、MoraleShockRulesTests | MoraleSourcePlayModeTests、MoraleLockRoutPlayModeTests。自然発火は別観測 |
| 陣形・保持 | FleetFormationOrderRulesTests、CorpsFormationOrderRulesTests | FleetFormationHoldPlayModeTests、描画と操作感 |
| 艦隊メニュー表示 | FleetDestinationSortRulesTests、FleetCommandLabelSaveTests（関連ロジック変更時） | 文字、スクロール、クリック、選択結果 |
| 保存・艦艇数 | CampaignSaveRoundTripTests、FleetShipCountSaveTests、FleetCommandLabelSaveTests | 保存再開と戦略/戦術往復 |
| 稟議・決裁 | RingiCompletionTests、RingiAuthorityRoundTripTests、DecisionQueueTests | RingiFlowPlayModeTests、複数入口からの一度だけ執行 |

TestHarnessはCore中心でGame/UIの代用にしない。Unityが生成した古いcsprojだけの合格を、新規ソースのコンパイル確認としない。

## 不具合証拠の単位
証拠一式を outputs/qa/<case-id>/<run-id>/ に保存する運用とする。ゲーム側の自動収集は未実装。現時点ではChatGPTが既存ログ・試験XML・差分をまとめる。秘密情報や不要な個人情報を含めない。

必須項目:
- Issue・case-id・run-id、日時、報告者、期待結果、実結果、再現手順。
- 対象HEADだけでなく未コミット差分、対象ファイル一覧とハッシュ。Unity版、プラットフォーム、ビルド識別。
- 艦隊ID・軍団ID・指揮権限・旗艦、艦艇数、士気、命令元と現在命令、座標/向き、ゲーム内時間。
- preset・seed・AI/イベント設定。取得不能は「未取得」と明記し推定値を埋めない。
- 操作/イベント前後の値、ログ・XML・画像への相対リンク。ログ容量超過・欠落を明記。
- 固定試験/自然会戦/強制発火を明示。再現回数と成功回数。

## 安定版の扱い
今回、検証済み配布物を確認していないため安定版は未指定。HEADやビルド成功だけで安定版に昇格しない。
候補は開発ツリーと別フォルダの outputs/releases/<build-id>/ に実行ファイル・必要データ・変更記録・ハッシュ・対応セーブのコピー・検証証跡を保存する。既存セーブを上書きしない。
昇格条件: 起動、編成→移動→会戦→結果反映→保存再開の必須確認、既知不具合一覧。旧版を保持し、セーブ互換性を確認する。開発版と同じ保存先を使う場合は同時実行せず、保存先の分離実装をClaudeへ依頼してから運用する。現時点で分離済みとは報告しない。

## 原因単位のIssue整理
症状が似ているだけでは統合しない。再現ログと実行経路により同一原因が確認された時だけ原因Issueへ関連付ける。右クリック、軍団命令、AIは別の再現ケースとして残す。原因未確定は仮説として記録する。
既存Issueの本文・履歴を保持し、重複候補→原因確認→修正→入口別回帰確認の順に進める。未検証を一括クローズしない。

## Claudeへの後続作業票（未送信・重複起動禁止）
- SPEED-04 共通入口の監査: 艦隊命令と決裁を別票にし、呼出元→権限判定→状態変更→効果適用を列挙する。既に共通化済みなら作り直さない。抜け道が実証された一経路だけ修正し、同一命令二重実行・指揮系統外命令の回帰を確認。
- SPEED-05 証拠自動収集: 既存MoraleAuditLogと固定QAログを再利用して上記必須情報を一式で出力。失敗しても通常プレイを壊さず、ファイル欠落・容量制限を明示。保存先・個人情報をレビュー。
- SPEED-06 安定版候補: 保存先の分離を確認/必要なら実装し、既知不具合と必須試験を確認後に別フォルダへビルド。未検証ビルドはcandidateと表示。実機確認はユーザー指定時間で行う。

実行順: 既存SPEED-01～03の後。各票の必要性を再確認し、小さな単位でClaude実装→編集停止→ChatGPTレビュー/試験へ進める。
