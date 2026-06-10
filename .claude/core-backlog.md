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
- [x] Wave27 (2026-06-10)：CounselRules(#1104) / PledgeRules(#1105) / AlienationStratagemRules(#1106) / MilitaryColonyRules(#1107) / CoinageSpeculationRules(#1073) / InformationAsymmetryRules(#1074) / SpatialArbitrageRules(#1075)（並列7・全Issue由来・三国志SGZ＋狼と香辛料SAW）
- [x] Wave28 (2026-06-10)：CorneringRules(#1076) / MerchantCreditRules(#1077) / PlanetaryDefenseRules(#1070) / FleetCapRules(#1067) / MeritPromotionRules(#1064) / OperationalAptitudeRules(#1063) / RefitPurchaseRules(#1068)（並列7・全Issue由来・狼と香辛料SAW完結＋Almagest ALM・親がmeta生成漏れ補完）

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
# 狼と香辛料 SAW（#1073-1077）
# Almagest ALM（純ロジック子Issue）
- [ ] TechTreeRules：技術ツリー配線＝基礎技術→前提充足で新技術出現（#1065）。`ResearchRules`(研究進捗)とは別＝技術の前提依存グラフ
- [ ] EndingBranchRules：エンディング分岐＝評判/同盟国数/イベント経験の条件評価で結末を分岐（#1061）。`DisclosureRules`(開示連鎖)とは別＝最終分岐の条件スコアリング

### Issue連動（第4次補充・2026-06-10／会戦×政治の純ロジック子Issue・適合プール終盤）
- [ ] RoyalPresenceRules：君主の臨御＝親征（#899）。前線に立つ王は士気/戦力が格別に上がるが戦死/捕虜リスクを負い、立たぬ王は威信が下がる＝リスクとリターンの賭け。`ReputationRules`(名声)/`IllnessRules`(健康)とは別＝親征の損得
- [ ] CommandLegitimacyRules：会戦指揮の正統性＝文民統制で服従/デバフ/部分的不服従/不服従（#898）。指揮権の正統性が将兵の服従度を決める＝なぜプレイヤーは会戦を指揮できるのか。`CivilianControlRules`(クーデター)とは別＝戦場での命令服従
