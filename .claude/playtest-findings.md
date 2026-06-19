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
- [x] [Core] flow5（並列8・2026-06-13）：SpaceSuperiorityRules（制宙権）/OperationalTempoRules（作戦テンポOODA）/ForceConcentrationRules（兵力集中）/InterdictionRules（阻止攻撃）/StrategicReserveRules（戦略予備）/DeepStrikeRules（縦深打撃）/BridgeheadRules（橋頭堡）/CounterAttackRules（反撃＝攻勢限界での切り返し）を新規実装（各EditModeテスト付き）。
- [x] [Core] flow6（並列8・2026-06-13）：CombatStressRules（戦闘ストレス）/TacticalSurpriseRules（戦術奇襲）/DoctrineFlexibilityRules（ドクトリン柔軟性）/SchwerpunktRules（重点）/CommandClimateRules（指揮風土）/ManeuverWarfareRules（機動戦vs消耗戦）/ProbeAttackRules（威力偵察）/EconomyOfForceRules（兵力節用）を新規実装（各EditModeテスト付き）。
- [x] [Core] flow7（並列8・2026-06-14）：JammingWarfareRules（電子妨害）/SensorPicketRules（哨戒線早期警戒）/MineWarfareRules（機雷戦）/DecoyForceRules（囮陽動）/CarrierStrikeRules（空母打撃アウトレンジ）/RaidingPartyRules（襲撃隊一撃離脱）/CounterReconRules（対偵察＝敵を盲目に）/StandoffStrikeRules（長距離打撃アウトレンジ）を新規実装（各EditModeテスト付き）。
- [x] [Core] flow8（並列8・2026-06-14）：AmphibiousAssaultRules（強襲揚陸）/FieldFortificationRules（野戦築城）/BreakoutRules（包囲突破）/PsyOpsRules（心理戦）/CoalitionWarfareRules（連合作戦）/WarTerminationRules（戦争終結の機運）/SupplyPriorityRules（補給優先配分）/SurrenderRules（降伏の意思決定）を新規実装（各EditModeテスト付き）。
- [x] [Core] flow9（並列8・2026-06-14）：ReplacementFlowRules（損耗補充と練度希釈）/MaintenanceCycleRules（整備サイクル稼働率）/FuelLogisticsRules（燃料兵站行動半径）/AmmunitionLogisticsRules（弾薬補給継戦）/RepairPriorityRules（修理優先トリアージ）/TrainingPipelineRules（訓練パイプライン）/IntelFusionRules（情報統合）/DeceptionPlanRules（欺瞞計画）を新規実装（各EditModeテスト付き）。
- [x] [Core] flow10（並列8・2026-06-14）：WarEconomyRules（戦時経済軍需転換）/MobilizationTempoRules（動員速度先手）/StrategicBombingRules（戦略爆撃）/HomeFrontMoraleRules（銃後の士気）/WarProductionRules（戦時生産学習曲線）/ConscriptionWaveRules（徴兵波と社会負荷）/BlockadeEconomyRules（封鎖の経済影響）/WartimeInflationRules（戦時インフレ）を新規実装（各EditModeテスト付き）。
- [x] [Core] flow11（並列・2026-06-19）：諜報・特殊作戦・外交テーマ7本＝EspionageNetworkRules（諜報網HUMINT）/SabotageOpsRules（工作員の破壊工作）/DefectionRecruitmentRules（敵要人の引き抜き勧誘）/AllianceManagementRules（多国間同盟の結束・負担分担・離反）/NeutralPartyRules（フェザーン型中立勢力の力学）/CovertActionRules（否認可能な秘密工作とブローバック）/AssassinationOperationRules（要人除去工作の余波評価）を新規実装（各EditModeテスト付き・TestHarness 6650 green）。※当初候補の CounterIntelligenceRules は Intel/ に既存のため上書きせず温存。
- [x] [Core] flow12（並列8・2026-06-19）：占領統治・反乱テーマ8本＝OccupationGovernanceRules（占領統治の駐留コストと安定）/PacificationRules（平定作戦の掃討×民心）/PuppetGovernmentRules（傀儡政権の間接統治と自立化）/CollaboratorRules（協力者の動機と二重忠誠）/GuerrillaWarfareRules（非正規戦の継戦力学）/CounterInsurgencyRules（COIN＝住民隔離・諜報・支持遮断）/ResistanceMovementRules（地下抵抗組織の成長と士気）/RefugeeCrisisRules（戦災難民の発生と受入負荷）を新規実装（各EditModeテスト付き・TestHarness 6733 green）。
- [x] [Core] flow13（並列8・2026-06-19）：指揮統率・将官人事テーマ8本＝CommandReputationRules（将帥の声望＝武名と威信）/OfficerRivalryRules（将官の確執・派閥・連携）/ChainOfCommandRules（指揮系統の階層・継承・統制）/BattlefieldPromotionRules（戦場昇進と論功行賞）/CommanderFatigueRules（指揮官の疲労と判断力低下）/WarCouncilRules（作戦会議の質と集団思考）/FlagOfficerSelectionRules（将官選抜と登用適性）/HeroicLeadershipRules（陣頭指揮のカリスマと危険）を新規実装（各EditModeテスト付き・TestHarness 6815 green）。
- [x] [Core] flow14（並列8・2026-06-19）：宮廷政治・陰謀テーマ8本＝CourtIntrigueRules（宮廷陰謀＝讒言と権謀術数）/NoblePrivilegeRules（貴族特権と社会的歪み）/ImperialFavorRules（君寵とその不安定さ）/ConspiracyRules（謀反・クーデターの陰謀）/PoliticalPurgeRules（粛清と恐怖統治）/DynasticMarriageRules（政略結婚と血統政治）/FavoritismRules（依怙贔屓と組織の腐朽）/CourtFactionRules（宮廷派閥の党派抗争と勢力均衡）を新規実装（各EditModeテスト付き・TestHarness 6893 green）。
- [x] [Core] flow15（並列8・2026-06-19）：軍艦技術・工学テーマ8本＝WarshipDesignRules（軍艦設計の攻防走トレードオフ）/ShipArmorRules（装甲と貫通モデル＝避弾経始）/BeamWeaponRules（ビーム兵器の出力/減衰/過熱）/ShieldTechRules（防御スクリーンの吸収/崩壊/再生）/PropulsionRules（推進系の推力重量比と運動性能）/DamageControlRules（被弾後のダメコンと生存）/FireControlRules（射撃管制の照準/命中精度）/MissileWarfareRules（ミサイル飽和攻撃と迎撃）を新規実装（各EditModeテスト付き・TestHarness 6970 green）。
- [x] [Core] flow16（並列8・2026-06-19）：宙域・天体環境戦テーマ8本＝NebulaCombatRules（星雲戦＝視界不良と隠密近接）/AsteroidFieldRules（小惑星帯＝機動制限と遮蔽伏撃）/GravityWellRules（重力井戸＝スイングバイ/潮汐/捕獲）/StellarRadiationRules（恒星放射＝電子障害/被曝/逆光戦術）/HyperspaceJumpRules（亜空間跳躍＝充填/到着誤差/跳躍直後の無防備）/CorridorControlRules（航路回廊＝隘路の戦略支配/封鎖/通行料）/SolarFlareRules（恒星フレア＝EMP/通信途絶/フレア奇襲）/DebrisFieldRules（残骸宙域＝センサー擾乱/隠密/救助/サルベージ）を新規実装（各EditModeテスト付き・TestHarness 7049 green）。
- [x] [Core] flow17（並列8・2026-06-19）：メディア・世論・プロパガンダテーマ8本＝PropagandaCampaignRules（宣伝工作の浸透と飽和）/MediaControlRules（報道統制とメディア掌握）/WarJournalismRules（戦争報道と銃後の世論）/CultOfPersonalityRules（個人崇拝と神話崩壊）/NationalMythRules（建国神話と国民統合）/DissidentRules（反体制・異論と弾圧の逆説）/StateMediaRules（国営放送とパンとサーカス）/PatriotismRules（愛国心と旗の下集結効果）を新規実装（各EditModeテスト付き・TestHarness 7127 green）。
- [x] [Core] flow18（並列8・2026-06-19）：情報分析・偵察・センサーテーマ8本＝SensorNetworkRules（索敵網の構築と探知）/IntelAnalysisRules（生情報の分析と欺瞞看破）/ReconSatelliteRules（偵察衛星の前方偵察）/ThreatDetectionRules（脅威の探知・識別・警報）/IntelligenceEstimateRules（彼我戦力評価と不確実性）/SurveillanceRules（持続的監視と情報蓄積）/SignalTriangulationRules（電波測位と位置標定）/StealthDetectionRules（ステルスと探知の駆け引き）を新規実装（各EditModeテスト付き・TestHarness 7205 green）。
- [x] [Core] flow19（並列8・2026-06-19）：植民・開拓・辺境開発テーマ8本＝ColonyDevelopmentRules（植民地開発と自立）/FrontierExpansionRules（辺境拡大と過伸長）/ColonialAdministrationRules（植民地統治と現地登用）/ResourceExtractionRules（資源採掘と枯渇）/SettlementGrowthRules（入植地の成長と過密）/PlanetaryHabitabilityRules（惑星居住性とテラフォーミング）/OutpostRules（前哨基地の運営）/OrbitalInfrastructureRules（軌道インフラの建設と機能）を新規実装（各EditModeテスト付き・TestHarness 7282 green）。
- [x] [Core] flow20（並列8・2026-06-19）：艦隊運用・乗員・艦ライフサイクルテーマ8本＝ShipCommissioningRules（新造艦の戦力化）/FleetReadinessRules（艦隊即応態勢）/CrewProficiencyRules（個艦の乗員練度）/ShipDecommissionRules（旧式艦の退役判断）/NavalDrillRules（艦隊演習の錬成）/FleetTenderRules（工作艦・補給艦の前線支援）/VesselLifecycleRules（艦の経年変化バスタブ曲線）/CrewComplementRules（乗員定数と充足）を新規実装（各EditModeテスト付き・TestHarness 7358 green）。
- [x] [Core] flow21（並列8・2026-06-19）：要塞・拠点防衛・攻城テーマ8本（イゼルローン要塞型）＝FortressDefenseRules（要塞の総合防御力）/FortressArtilleryRules（要塞主砲トゥール・ハンマー型）/StrongpointRules（拠点の縦深防御と相互支援）/GarrisonDefenseRules（守備隊の籠城と降伏圧力）/FortressLogisticsRules（要塞兵站と持久力）/SiegeBatteryRules（攻城砲撃と突破口形成）/PlanetaryShieldRules（惑星規模の防御シールド）/FortressSortieRules（籠城からの出撃の駆け引き）を新規実装（各EditModeテスト付き・TestHarness 7435 green）。
- [x] [Core] flow22（並列8・2026-06-19）：外交交渉・条約・講和テーマ8本＝PeaceNegotiationRules（講和交渉の合意可能域）/TreatyTermsRules（条約条項の苛烈さと遺恨）/HostageExchangeRules（政治的人質の授受と信義）/TributeRules（朝貢・貢納の従属関係）/DiplomaticLeverageRules（外交的てこと圧力）/MediationRules（第三者調停と漁夫の利）/UltimatumRules（最後通牒の瀬戸際外交）/ProtectorateRules（保護国・従属国の独立移行）を新規実装（各EditModeテスト付き・TestHarness 7521 green）。
- [x] [Core] flow23（並列8・2026-06-19）：経済戦・通商・制裁テーマ8本＝TradeWarRules（貿易戦争の関税応酬）/EconomicSanctionRules（経済制裁と抜け穴）/SmugglingRules（密貿易フェザーン型）/CommercialTreatyRules（通商条約の互恵と非対称）/ResourceCartelRules（資源カルテルと裏切り）/MerchantConvoyRules（商船団の運航経済）/PrivateeringRules（私掠＝認可された通商破壊）/CurrencyWarRules（通貨戦争と金融制裁）を新規実装（各EditModeテスト付き・TestHarness 7608 green）。
- [x] [Core] flow24（並列8・2026-06-19）：社会不安・革命・民衆テーマ8本＝CivilUnrestRules（市民不安と治安悪化）/RiotControlRules（暴動鎮圧と過剰使用の反発）/LaborStrikeRules（労働争議と軍需波及）/ProtestMovementRules（抗議運動の動員と持続）/RevolutionaryCellRules（地下革命組織）/SocialDiscontentRules（相対的剥奪とJ字カーブ）/UprisingRules（民衆蜂起と治安部隊の離反）/MassMobilizationRules（大衆動員の臨界量とフリーライダー）を新規実装（各EditModeテスト付き・TestHarness 7694 green）。
