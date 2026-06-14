# プレイアビリティ改善バックログ（playtest-findings）

> 「遊べるゲームか」をコード監査で洗い出し→改善→再監査するループの作業台帳。
> `/playtest-improve`（`.claude/commands/playtest-improve.md`）が1サイクルごとにここを読み、
> **[Core]** 項目（TestHarness で検証できる純ロジック）を実装してブランチ→PR→自動マージする。
> **[Game]** 項目（Unity 実行や UI 目視が要る＝この環境で自動検証不可）は実装せず記録だけ残し、人手レビュー用に蓄積する。
>
> 記法：`- [ ] [Core] …` 未着手／`- [x] [Core] …（cycleN）` 完了。
> ゲートは **TestHarness のみ（`cd TestHarness && dotnet test`）**。Game層の自動マージはしない（マージ安全弁の方針）。

## 現状サマリ（2026-06-13 初回監査 / 2026-06-14 更新）
- Core 純ロジック **~1020 本**、うち Game 層に配線済みは一部。残り多数は盤面/UI 未配線＝プレイに効いていない。
- TestHarness ベースライン：**5723 テスト green**（cycle5 時点）。
- 標的選定（`ShipCombat.FindPrioritizedEnemyInArc`）は純距離ベースのみ＝集中砲火/仕留め/斬首/側背面の価値判断が無い（cycle5 で純Coreを補充）。
- AI 撤退（`FleetAI.retreatRatio`）は自軍兵力比のみ＝近接/火力/側背面/敵の手番を織り込んだ脅威評価が無い（cycle5 で純Coreを補充）。
- 会戦は旗艦＋配下艦・陣形・士気・側背面・AI 撤退まで配線済み。戦略は GalaxyView に内政/造船/暦/通知まで配線済み。
- **最大の伸びしろ＝「シミュ層の厚みが play に効いていない」**：戦術 Core（伏兵/陽動/追撃/艦載機/白兵/機雷/電子戦/偵察/練度…）が未配線で、会戦の駆け引きが平板になりがち。

## [Core] 改善候補（このループが実装する＝test-first・TestHarness gate）
- [ ] [Core] 標的優先度の配線：`TargetPriorityRules`（cycle5 追加）を `TacticalDoctrineRules` 隣の薄い窓口として整え、`ShipCombat.FindPrioritizedEnemyInArc` が将来この `PriorityScore`/`Prefer` を読めるよう純Core側の API を仕上げる（攻撃数の集計だけ Game 側が渡す形に）。配線そのものは [Game] 案件。
- [ ] [Core] 脅威ベース撤退の純ロジック：`ThreatAssessmentRules`（cycle5 追加）の `RetreatPressure`/`IsOverwhelmed` を `FleetAI` の `retreatRatio` と統合する判断窓口（兵力比×脅威圧力）を純Coreで足す（基準非破壊・既定で従来動作）。
- [ ] [Core] 集中砲火の局所優勢ブリッジ：`TargetPriorityRules.FocusModifier`（攻撃集中）と `LanchesterRules`（二乗則ダメージ）を突き合わせ、過剰集中（オーバーキル）を避けつつ局所優勢を作る配分の純関数を test-first で。
- [x] [Core] 戦術ドクトリン統合：未配線の戦術 Core（`AmbushRules`/`FeintRules`/`PursuitRules`/`ReconRules`/`VeterancyRules` 等）を会戦 AI が読める単一窓口 `TacticalDoctrineRules`（仮）に束ね、`BattleAiRules`/`ForceQualityRules` と同じ実効値パターンで倍率を返す。まず純ロジック＋テストを足し、配線は Game 側の1箇所からに留める。（cycle1・2026-06-13）
- [x] [Core] 会戦バランス：決着が単調化しないよう Lanchester/士気/側背面の係数を `CombatModifiers` 経由で見直し、極端な雪崩を抑える調整を test-first で（従来式との差分をテストで固定）。
- [x] [Core] 練度の play 反映：`VeterancyRules`（練度）を戦力比に効かせる実効倍率を `ForceQualityRules` 隣に橋渡し（基準非破壊）。歴戦艦隊が新兵より強い手応えを数値で作る。（cycle1・2026-06-13）
- [x] [Core] 偵察と戦場の霧：`ReconRules` の推定誤差を「敵戦力表示のブレ」に使える純関数 API に整え、AI の過大/過小評価（`OverconfidenceBiasRules`/`AvailabilityBiasRules`）と接続するブリッジ Rule を test-first で。
- [x] [Core] 撤退・追撃の収支：`PursuitRules`（追撃戦）の損害解決を会戦終了時に使える形に整え、`BattleWithdrawalRules` と責務分担した薄い橋渡しを足す。

## [Game] 改善候補（Unity 実行/目視が要る＝記録のみ・人手対応）
- [ ] [Game] 会戦の操作フィードバック（攻撃/移動/陣形変更の手応え・通知）の充実。要 Unity 目視。
- [ ] [Game] 戦術 Core を実際の会戦挙動へ配線（AI が伏兵/陽動/追撃を「打つ」演出と判断）。`unity-test.yml`（実 Unity）で検証してから人手マージ。
- [ ] [Game] GalaxyView 2531：新任人材の性的指向は別軸で未実装（任意）。
- [ ] [Game] 初見プレイヤー向けの導線（チュートリアル/操作ヘルプの初回提示）。要目視。
- [ ] [Game] `ShipCombat.FindPrioritizedEnemyInArc` を `TargetPriorityRules.PriorityScore`/`Prefer` 駆動へ置換（cycle5 で純Core追加済み）。各標的の「既に狙っている味方艦数」を集計して渡す必要があり、配下艦の発砲ループに触れる＝Unity 目視で集中砲火/オーバーキルの挙動確認が要る。
- [ ] [Game] `FleetAI` の撤退判断を `ThreatAssessmentRules.RetreatPressure`/`IsOverwhelmed` 併用へ（cycle5 で純Core追加済み）。近接の敵火力・側背面被弾を脅威として集計し兵力比のみの撤退を補強。要 Unity 目視。

## 完了ログ
<!-- - [x] [Core] … （cycleN・YYYY-MM-DD） -->
- [x] [Core] TacticalDoctrineRules 新規作成（AmbushRules/VeterancyRules/ReconRules を Evaluate＋ShouldAmbush に統合・テスト11件）（cycle1・2026-06-13）
- [x] [Core] ForceQualityRules に CombatMultiplier(NcoCorps, proficiency, readiness, veterancyXp) オーバーロード追加・テスト3件（cycle1・2026-06-13）
- [x] [Core] flow1（並列8・2026-06-13）：FogOfWarRules（戦場の霧）/ScreeningRules（偵察幕）/SignalIntelligenceRules（通信諜報）/PursuitBattleRules（追撃の収支）/BattleTempoRules（会戦テンポの振り戻し）/SortieTimingRules（出撃好機）/AttritionExchangeRules（消耗交換比）/CommandDelayRules（指揮伝達遅延）を新規実装（各EditModeテスト付き）。
- [x] [Core] flow2（並列8・2026-06-13）：ManeuverEnvelopmentRules（機動包囲）/BattleLineRules（戦列の維持崩壊）/ReserveDeploymentRules（予備投入）/NightBattleRules（夜戦）/ChokeholdBattleRules（隘路戦＝イゼルローン型）/RallyRules（敗走兵再結集）/SuppressionFireRules（制圧射撃）/CombinedArmsRules（諸兵科連合）を新規実装（各EditModeテスト付き）。
- [x] [Core] flow3（並列8・2026-06-13）：SiegeAssaultRules（強襲vs兵糧攻め）/FeignedRetreatRules（偽装退却）/HighGroundRules（軌道高所優位）/DecapitationStrikeRules（斬首＝旗艦狙い）/BlockadeRunningRules（封鎖突破）/ConvoyDefenseRules（船団護衛）/EliteUnitRules（精鋭部隊）/MoraleContagionRules（士気伝播）を新規実装（各EditModeテスト付き）。
- [x] [Core] flow4（並列8・2026-06-13）：RammingRules（衝角特攻）/CounterBatteryRules（対砲戦）/PincerAttackRules（挟撃）/RefusedFlankRules（斜行陣）/WithdrawalCoveringRules（退却援護＝殿）/SalvoTimingRules（斉射タイミング）/EvasiveManeuverRules（回避機動）/BoardingActionRules（接舷白兵拿捕）を新規実装（各EditModeテスト付き）。
- [x] [Core] cycle5（2026-06-14）：TargetPriorityRules（射撃目標優先度＝集中砲火/仕留め/斬首/側背面のスコアリング・Prefer 決定論タイブレーク・テスト10件）/ThreatAssessmentRules（脅威評価＝敵火力×近接×側背面×交戦中割引・RetreatPressure/IsOverwhelmed で AI 撤退判断・テスト8件）を新規実装。標的選定が純距離ベースのみ・AI撤退が兵力比のみだったギャップを純Coreで補充（配線は [Game] 案件として記録）。TestHarness 5723 green。
