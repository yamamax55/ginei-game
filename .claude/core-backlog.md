# Core量産バックログ（/core-wave が消化するテーマキュー）

> `/core-wave` 実行のたびに、未着手（`[ ]`）の上から**7件**を実装し、完了したら `[x]` に変えて
> Wave番号と日付を追記する。**キューが空になったら量産を停止して報告する**（無限生成しない）。
> 新テーマの追加は自由（既存モジュールと重複しないこと＝CLAUDE.md の一覧と照合する）。

## 済み（参考）
- [x] Wave3 (2026-06-10)：ReconRules / Reputation / PropagandaRules / BlockadeRules / Fortress / MercenaryRules / TerrainRules
- [x] Wave4 (2026-06-10)：ConscriptionRules / VeterancyRules / RepairRules / BoardingRules / MobilizationRules / RefugeeRules / DisciplineRules
- [x] Wave5 (2026-06-10)：PursuitRules / AssassinationRules / SanctionsRules / WarPoliticsRules / DisasterRules / EducationRules / ShipAgingRules
- [x] Wave6 (2026-06-10)：PiracyRules / TradeRules / MartialLawRules / CeremonyRules / GovernmentInExileRules / HonorsRules / PrisonerExchangeRules
- [x] Wave7 (2026-06-10)：AmbushRules / EncirclementRules / FeintRules / MinefieldRules / CarrierRules / CommunicationsRules / ElectronicWarfareRules
- [x] Wave8 (2026-06-10)：DesertionRules / SalvageRules / OperationPlanRules / AtrocityRules / CounterIntelligenceRules / CodebreakingRules / ResistanceRules
- [x] Wave9 (2026-06-10)：TerrorRules / RegencyRules / PlebisciteRules / AmnestyRules / PurgeRules / ConfiscationRules / DemagogueRules
- [x] Wave10 (2026-06-10)：MartyrdomRules / CivilWarRules / HostageRules / MigrationRules / InflationRules / BlackMarketRules / ReconstructionRules（初の並列7実装＝実装フェーズ約7分）
- [x] Wave11 (2026-06-10)：ReparationsRules / MonopolyRules / MegaProjectRules(#1090) / InnovationDiffusionRules / BufferStateRules / ChokepointValueRules / SenescenceRules（並列7＝約5分）
- [x] Wave12 (2026-06-10)：TerraformingRules / RivalryRules / FriendshipRules / MentorshipRules / HistoriographyRules / ReadinessRules / ForcedMarchRules（並列7＝約4分）
- [x] Wave13 (2026-06-10)：MothballRules / RaidRules / ScorchedEarthRules / MutinyRules / EscalationRules / DeterrenceRules / ArmsRaceRules（並列7＝約5分）
- [x] Wave14 (2026-06-10)：ArmsControlRules / AppeasementRules / InfluenceRules / DebtDiplomacyRules / ForeignAidRules / TribunalRules / CensusRules（並列7＝約6分）
- [x] Wave15 (2026-06-10)：CoalitionRules / ImpeachmentRules / TermLimitRules / EmergencyPowersRules / FederalismRules / CitizenshipRules / PatronageRules（並列7＝約5分）
- [x] Wave16 (2026-06-10)：CourtFavorRules / AmbitionRules / IllnessRules / ScandalRules / SerfdomRules / GenerationalMemoryRules / BreadAndCircusesRules（並列7＝約6分・親修正1件）
- [x] Wave17 (2026-06-10)：VeteranPoliticsRules / StrikeRules / ReserveCurrencyRules / RationingRules / WarIndustryRules / ExplorationRules(G-2 #119) / SpaceWeatherRules（並列7＝約5分）
- [x] Wave18 (2026-06-10)：RelicRules / DefenseLineRules / PrivateerRules / MedicalRules / BureaucracyBloatRules / SecretSocietyRules / FreePressRules（並列7＝5体がsession上限・親が残3テストを補完して完成）
- [x] Wave19 (2026-06-10)：LobbyRules / PreferenceFalsificationRules / PriceControlRules / LandReformRules / FrontierRules / AsabiyyaRules / HegemonyRules（並列7＝約4分・Issue補充後の初Wave）
- [x] Wave20 (2026-06-10)：OverextensionRules / BurdenSharingRules / CollectiveSecurityRules / PartitionRules / PraetorianRules / CoupledProductionRules(#1110) / SpreadRules(#1111)（並列7・初のIssue由来テーマCPL含む・親がPartitionテスト混入を修正）
- [x] Wave21 (2026-06-10)：ByproductGlutRules(#1113) / ChainFragilityRules(#1112) / ContinuousOperationRules(#1115) / IntermediateBufferRules(#1116) / ForageRules(#1128) / CulminatingPointRules(#1129) / SunziDoctrineRules(#1130)（並列7・連鎖経済CPL完結＋孫子SUN・全Issue由来）
- [x] Wave22 (2026-06-10)：DeceptionRules(#1126) / CoinageRules(#1072) / LedgerRules(#974) / WangDaoRules(#1059) / QualityScheduleRules(#1091) / TechBearerRules(#1092) / CharterRightsRules(#1093)（並列7・全Issue由来・2回目補充後）
- [x] Wave23 (2026-06-10)：CityGrowthRules(#1094) / SuccessionWarRules(#1095) / ClericalCareerRules(#1096) / ArmamentDesignRules(#1066) / BalanceSheetRules(#975) / IncomeStatementRules(#976) / FiscalPolicyRules(#1013)（並列7・全Issue由来・Pillars完結＋会計B/S・P/LがLedger土台に）
- [x] Wave24 (2026-06-10)：AutoTreasuryRules(#1014) / CashFlowForecastRules(#1015) / FinancialAnomalyRules(#1016) / SupplierRatingRules(#1004) / SourcingAuctionRules(#1005) / SupplyContractRules(#1006) / BomRules(#983)（並列7・全Issue由来・財務AI完結＋調達＋生産網・親がParamsネスト修正1件）
- [x] Wave25 (2026-06-10)：MrpRules(#984) / ProductionOrderRules(#985) / CapacitySchedulingRules(#987) / ValueChainRules(#1023) / FirmRules(#1024) / CapitalInvestmentRules(#1025) / PublicPrivateSeparationRules(#1035)（並列7・全Issue由来・生産網SCM完結＋企業FRM）
- [x] Wave26 (2026-06-10)：PropertyRightsRules(#1036) / InheritanceRules(#1038) / PerformanceReviewRules(#995) / CompensationRules(#996) / SpyRoleRules(#1127) / BullwhipRules(#1114) / BalanceOfPowerRules(#1103)（並列7・全Issue由来・所有OWN/人事HCM/連鎖CPL完結＋孫子用間/三国志着手）

## キュー（上から順に消化）

### 軍事・戦術

### 戦争犯罪・諜報

### 政治・社会

### 経済

### 戦略・人物

### 軍事・即応態勢

### 抑止・外交

### 統治・制度

### 宮廷・人物

### 社会

### 経済

### 戦略・探査

### 戦域・環境（第3次追加）

### 統治・組織（第3次追加）

### 経済・社会（第3次追加）

### 覇権・体制（第3次追加）

### Issue連動（第1次補充・2026-06-10／オープンIssueから純ロジック子Issueを選定）
# 連鎖経済 CPL EPIC（#1110-1116）
# 孫子ドクトリン SUN EPIC（#1126-1130）
# 経済・通貨（SAW-1 #1072・ACC-1 #974）
# 評判メタ（ALM-5 #1059）
# Pillars of the Earth EPIC（#1091-1096）
# 艦艇設計（ALM-12 #1066）

### Issue連動（第2次補充・2026-06-10／ERP系の純ロジック子Issue）
# 会計 ACC（#975,#976）
# 財務AI AFN（#1013-1016）
# 調達 PRC（#1004,#1005,#1006）
# 生産網 SCM（#983,#984,#985,#987）
# 企業 FRM（#1023,#1024,#1025）
# 所有 OWN（#1035,#1036,#1038）
# 人事 HCM（#995,#996）

### Issue連動（第3次補充・2026-06-10／孫子SUN残・三国志SGZ・狼と香辛料SAW・Almagest ALM）
# 孫子 SUN残り＋連鎖経済CPL-4
# 三国志演義 SGZ（#1103-1107）
- [ ] CounselRules：献策システム＝参謀が策を提案→君主が採択→帰結修正子（#1104）。`CommandStaffRules`(能力補完)とは別＝策の提案と採否の力学
- [ ] PledgeRules：個人結盟と盟誓＝義兄弟型誓約・拘束力・離反ペナルティ（#1105桃園結義）。`LoyaltyRules`(会戦の旗幟)・`FriendshipRules`(紐帯)とは別＝制度的誓約
- [ ] AlienationStratagemRules：離間の計＝標的勢力ペアのopinion工作で同盟を崩す（#1106）。`EspionageRules`拡張・`DiplomacyRules`のopinionへ波及＝敵同士を仲違いさせる
- [ ] MilitaryColonyRules：屯田制・軍事農業植民地＝占領地自給で補給線依存を断つ（#1107）。`ForageRules`(一時的徴発)とは別＝恒久的な自給体制
# 狼と香辛料 SAW（#1073-1077）
- [ ] CoinageSpeculationRules：改鋳投機＝品位改定の噂→投機（#1073・戦わぬ経済戦）。`CoinageRules`(改鋳の実体)の投機版＝噂で相場が動く
- [ ] InformationAsymmetryRules：情報の非対称と風説の相場＝情報優位で裁定・噂で相場が動く（#1074）。`MarketRules`(均衡)とは別＝情報格差が生む利得
- [ ] SpatialArbitrageRules：空間裁定＝星系間の価格差をトレードで埋め価格収束（#1075）。`TradeRules`(交易利得)とは別＝裁定による価格収束の動学
- [ ] CorneringRules：買い占め・投機・バブル＝商品コーナリング動学（#1076）。`MonopolyRules`(構造的独占)とは別＝投機的な買い占めとバブル崩壊
- [ ] MerchantCreditRules：ネームド商人の信用・為替手形・レバレッジ・破産（#1077）。`BankRules`(銀行)とは別＝商人個人の信用とレバレッジ破産
# Almagest ALM（純ロジック子Issue）
- [ ] PlanetaryDefenseRules：惑星防衛3層＝防衛艦隊/防衛衛星/軌道部隊＋迎撃/防衛区別（#1070）。`PlanetSiegeRules`(攻城)の防御側＝層別の迎撃
- [ ] FleetCapRules：FCS.Cap＝配備可能艦数＝指揮容量Cap÷必要Cap・階級と二重（#1067）。`OrderOfBattle`(編制)とは別＝指揮容量の制約
- [ ] MeritPromotionRules：功績値→昇進→最大編成数（#1064）。`MeritRankRules`(軍功爵位)とは別＝功績が指揮できる部隊数を増やす
- [ ] OperationalAptitudeRules：作戦適性S〜E＝地形別（遭遇戦/拠点侵攻/拠点防衛）の提督適性（#1063）。`AdmiralData`能力の地形別補正＝得意な戦闘類型
- [ ] RefitPurchaseRules：改装/復元＋建造vs購入＝既存艦の改装と新造/購入の損得（#1068）。`ShipyardRules`(新造)・`ArmamentDesignRules`(設計)とは別＝改装の経済
- [ ] TechTreeRules：技術ツリー配線＝基礎技術→前提充足で新技術出現（#1065）。`ResearchRules`(研究進捗)とは別＝技術の前提依存グラフ
- [ ] EndingBranchRules：エンディング分岐＝評判/同盟国数/イベント経験の条件評価で結末を分岐（#1061）。`DisclosureRules`(開示連鎖)とは別＝最終分岐の条件スコアリング
