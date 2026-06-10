# Core量産バックログ（/core-wave が消化するテーマキュー）

> `/core-wave` 実行のたびに、未着手（`[ ]`）の上から**7件**を実装し、完了したら `[x]` に変えて
> Wave番号と日付を追記する。**キューが空になったら量産を停止して報告する**（無限生成しない）。
> 新テーマの追加は自由（既存モジュールと重複しないこと＝CLAUDE.md の一覧と照合する）。

## 済み（参考）
- [x] Wave3 (2026-06-10)：ReconRules / Reputation / PropagandaRules / BlockadeRules / Fortress / MercenaryRules / TerrainRules
- [x] Wave4 (2026-06-10)：ConscriptionRules / VeterancyRules / RepairRules / BoardingRules / MobilizationRules / RefugeeRules / DisciplineRules

## キュー（上から順に消化）
- [ ] PursuitRules：追撃戦・殿軍。敗走側の損害は追撃で拡大、殿軍（しんがり）が犠牲で本隊を逃がす。`FleetAI` 撤退・`LoyaltyRules` とは別系統（会戦後の損害解決のみ）
- [ ] AssassinationRules：要人暗殺（地球教型）。警護水準×潜入度で成否（roll決定論）、成功時の継承ショック・正統性動揺。`EspionageRules`（諜報一般）・`LifecycleRules.Kill`（死亡処理）へ委譲し重複しない
- [ ] SanctionsRules：経済制裁・禁輸。相手経済の産出を締めるが、第三国経由の抜け穴（リーク率）で効果が漏れ、長期化で自国も痛む。`BlockadeRules`（軍事封鎖）とは別系統＝平時の経済戦
- [ ] WarPoliticsRules：出兵の政治（アムリッツァ型）。支持率低下時の人気取り出兵・選挙前の強硬論、勝てば支持回復・負ければ政権崩壊。`PartyRules`/`WarGoalRules` を read-only で参照可
- [ ] DisasterRules：災害・疫病・飢饉。人口/安定度を削り、対応の巧拙が正統性を試す（迅速な救援=支持↑・放置=支持↓）。`DemographicsRules`（平時動態）とは別系統
- [ ] EducationRules：教育投資。投資→世代遅延（タイムラグ）を経て人材の質・研究力が上がる。`ResearchRules`（研究そのもの）・`CareerPipelineRules`（出自）とは別系統
- [ ] ShipAgingRules：艦齢。就役からの経過で性能低下・維持費増、更新需要を生む。`RepairRules`（損傷回復）とは別＝経年劣化
- [ ] PiracyRules：宇宙海賊。治安（安定度）低下で交易路に海賊が湧き、討伐戦力を割くか被害を呑むか。`CommerceRaidingRules`（国家の通商破壊）とは別系統＝非国家アクター
- [ ] TradeRules：星間交易（フェザーン型）。勢力間の交易路が双方に利益、戦争で断絶、中立仲介者の利得。`MarketRules`（域内市場）とは別系統＝対外交易
- [ ] MartialLawRules：戒厳令。治安は即回復するが正統性・支持・希望を削る時限措置、長期化で副作用が本体を超える。`SecurityRules`（秘密警察）とは別系統＝公然の強権
- [ ] CeremonyRules：儀礼・戴冠・凱旋式。演出が正統性・士気を買う、財政コスト、敗勢下の空疎な式典は逆効果。`DynastyRules`（天命）を read-only で参照可
- [ ] GovernmentInExileRules：亡命政権。領土喪失後も正統性の残滓で抵抗を続けられる、承認国数・時間経過で減衰。`LogisticsRules`（版図一体化）とは別系統
- [ ] HonorsRules：勲章・栄典。叙勲が士気/忠誠を買うが、乱発するとインフレで価値が下がる。`MeritRankRules`（爵位＝実利）とは別系統＝名誉の通貨
- [ ] PrisonerExchangeRules：捕虜交換交渉。保有捕虜の数・質（階級）で交換レートが決まり、成立で双方の人材が還流。`CaptivityRules`（個別処遇）へ委譲し重複しない
