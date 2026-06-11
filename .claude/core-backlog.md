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
- [x] Wave29 (2026-06-10)：TechTreeRules(#1065) / EndingBranchRules(#1061) / RoyalPresenceRules(#899) / CommandLegitimacyRules(#898)（並列4・全Issue由来・ALM技術ツリー/エンディング分岐＋会戦×政治・適合純ロジック子Issueプール枯渇＝自律ループの自然終端）

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
- [x] TechTreeRules：技術ツリー配線＝基礎技術→前提充足で新技術出現（#1065）。`ResearchRules`(研究進捗)とは別＝技術の前提依存グラフ（Wave29）
- [x] EndingBranchRules：エンディング分岐＝評判/同盟国数/イベント経験の条件評価で結末を分岐（#1061）。`DisclosureRules`(開示連鎖)とは別＝最終分岐の条件スコアリング（Wave29）

### Issue連動（第4次補充・2026-06-10／会戦×政治の純ロジック子Issue・適合プール終盤）
- [x] RoyalPresenceRules：君主の臨御＝親征（#899）。前線に立つ王は士気/戦力が格別に上がるが戦死/捕虜リスクを負い、立たぬ王は威信が下がる＝リスクとリターンの賭け。`ReputationRules`(名声)/`IllnessRules`(健康)とは別＝親征の損得（Wave29）
- [x] CommandLegitimacyRules：会戦指揮の正統性＝文民統制で服従/デバフ/部分的不服従/不服従（#898）。指揮権の正統性が将兵の服従度を決める＝なぜプレイヤーは会戦を指揮できるのか。`CivilianControlRules`(クーデター)とは別＝戦場での命令服従（Wave29）

- [x] Wave30 (2026-06-11)：AccusationCascadeRules(#1625) / ContraryPositionRules(#1624) / BubblePriceRules(#1622) / BoomFraudRules(#1621) / ManiaRules(#1620) / DebtDeflationRules(#1619) / FinancialContagionRules(#1615)（並列7・マッカイ狂気とバブルMNIA＋キンドルバーガー熱狂恐慌崩壊KNDB・親がBubble overload/DebtDeflationテストscale修正）
- [x] Wave31 (2026-06-11)：StandardizationRules(#1614) / LenderOfLastResortRules(#1613) / TransshipmentRules(#1612) / TransportCostRules(#1611) / CrisisCycleRules(#1610) / CeremonialismRules(#1603) / SocialProtectionRules(#1602)（並列7・レビンソンCNTR＋キンドルバーガーKNDB＋ヴェブレンVEBL＋ポランニーPOLA・親がStandardizationテスト算術修正）
- [x] Wave32 (2026-06-11)：OstentationRules(#1601) / InternationalOrderRules(#1599) / CompetitiveDemocracyRules(#1598) / EmulationRules(#1597) / FictitiousCommodityRules(#1596) / IntellectualCritiqueRules(#1595) / VeblenGoodsRules(#1593)（並列7・ヴェブレンVEBL完結＋ポランニーPOLA＋シュンペーターSCHU・全テスト一発green）
- [x] Wave33 (2026-06-11)：CreativeDestructionRules(#1581) / EntrepreneurRules(#1584) / BureaucratizationRules(#1587) / InnovationWaveRules(#1591) / EmpathyRules(#1578) / ImpartialObserverRules(#1582) / MoralStyleRules(#1586)（並列7・シュンペーターSCHU完結＋スミス道徳感情論TMS・親がMoralStyleの正規化/全0分岐2バグ修正）
- [x] Wave34 (2026-06-11)：CommercialIntegrityRules(#1590) / CarryingCapacityRules(#1574) / MalthusianCheckRules(#1575) / PoorLawRules(#1580) / MoralSproutsRules(#1564) / GovernanceStyleRules(#1568) / MoralForceRules(#1570)（並列7・スミスTMS完結＋マルサスMALT＋孟子MENC・全テスト一発green）
- [x] Wave35 (2026-06-11)：DefenseGuildRules(#1555) / CompetenceLegitimacyRules(#1565) / FrugalityDoctrineRules(#1567) / NonAggressionDoctrineRules(#1560) / WaterDoctrineRules(#1558) / SpontaneousOrderRules(#1556) / EmbeddednessRules(#1588)（並列7・墨子MOZI＋老子LAOZ＋ハイエクHAYK＋ポランニーPOLA完結・全テスト一発green）
- [x] Wave36 (2026-06-11)：EffectiveDemandRules(#1540) / MultiplierRules(#1542) / AnimalSpiritsRules(#1545) / LiquidityPreferenceRules(#1548) / ThriftParadoxRules(#1552) / PlanningDriftRules(#1541) / CalculationProblemRules(#1544)（並列7・ケインズKEYN完結＋ハイエクHAYK・全テスト一発green）
- [x] Wave37 (2026-06-11)：AuthoritarianSelectionRules(#1547) / LegalGeneralityRules(#1549) / EconomicFreedomRules(#1553) / WuWeiRules(#1546) / ReversalRules(#1550) / ContentmentRules(#1554) / ThoughtlessnessRules(#1530)（並列7・ハイエクHAYK完結＋老子LAOZ完結＋アーレントBNAL着手・親がWuWeiテスト対称性修正）
- [x] Wave38 (2026-06-11)：PluralityRules(#1532) / TotalitarianRules(#1535) / WarCrimesRules(#1536) / PoliticalEthicsRules(#1528) / PoliticalVocationRules(#1531) / PlebiscitaryRules(#1533) / PopulationMigrationRules(#1566)（並列7・アーレントBNAL完結＋ウェーバーWEBR完結＋孟子MENC完結・全テスト一発green）
- [x] Wave39 (2026-06-11)：ConquestGovernanceRules(#1139) / FearVsHatredRules(#1140) / AdvisorCandorRules(#1141) / VirtuFortunaRules(#1142) / FrictionRules(#1133) / TrinitarianTensionRules(#1135) / CenterOfGravityRules(#1136)（並列7・マキャヴェッリ君主論MKV完結＋クラウゼヴィッツ戦争論CLZ・全テスト一発green）
- [x] Wave40 (2026-06-11)：HerrschaftRules(#1525) / StatelessnessRules(#1526) / SuperfluousnessRules(#1524) / ImperialBlowbackRules(#1522) / TerrorPrincipleRules(#1519) / HistoricismTrapRules(#1521) / ToleranceParadoxRules(#1518)（並列7・ウェーバー支配類型＋アーレント全体主義TOTL完結＋ポパー開かれた社会POPR着手・全テスト一発green）
- [x] Wave41 (2026-06-11)：InstitutionalCorrectionRules(#1517) / PiecemealEngineeringRules(#1514) / OpennessRules(#1511) / PanoptismRules(#1507) / NormalizationRules(#1508) / ExaminationRules(#1509) / MesoiRules(#1495)（並列7・ポパー開かれた社会POPR完結＋フーコー規律PANO完結＋アリストテレスARIS着手・親がPanoptismテストclamp修正）
- [x] Wave42 (2026-06-11)：CommonGoodOrientationRules(#1499) / ChrematisticsRules(#1502) / CivicPhiliaRules(#1503) / TyrantToolkitRules(#1504) / SoftDespotismRules(#1492) / EqualityDriftRules(#1498) / AssociationRules(#1482)（並列7・アリストテレス政治学ARIS完結＋トクヴィルTOCQ完結・3000テスト突破=全3053・一発green）
- [x] Wave43 (2026-06-11)：FactionMultiplicityRules(#1473) / AmbitionCounterRules(#1476) / CompoundRepublicRules(#1481) / ExtendedRepublicRules(#1485) / ExecutiveEnergyRules(#1489) / RepresentativeFilterRules(#1494) / CensorshipRules(#1474)（並列7・フェデラリストFED完結＋ミル自由論MILL着手・全テスト一発green）
- [x] Wave44 (2026-06-11)：PublicOpinionRules(#1477) / HarmPrincipleRules(#1480) / LibertyCultureRules(#1487) / MajorityTyrannyRules(#1478) / MilitiaLoyaltyRules(#1483) / RinnovazioneRules(#1488) / FounderTrajectoryRules(#1493)（並列7・ミル自由論MILL完結＋トクヴィル多数者の専制＋マキャヴェッリ論考DISC・親が×識別子/Mathf using/HarmPrinciple期待値3件修正）
- [x] Wave45 (2026-06-11)：GeneralWillRules(#1462) / LawgiverRules(#1464) / PolityScaleRules(#1466) / CivicFaithRules(#1468) / AnarchyCostRules(#1459) / SecurityDilemmaRules(#1461) / CovenantRules(#1463)（並列7・ルソー社会契約論ROUS完結＋ホッブズ・リヴァイアサンLEVI完結・全テスト一発green）
- [x] Wave46 (2026-06-11)：AnacyclosisRules(#1442) / MixedConstitutionRules(#1445) / TycheRules(#1448) / UniversalHistoryRules(#1451) / InstitutionalMemoryRules(#1454) / GovernmentPrincipleRules(#1439) / PolityCorruptionRules(#1440)（並列7・ポリュビオス政体循環論POLY完結＋モンテスキュー法の精神MONT着手・親がFearDiminishingReturnsテスト比較修正）
- [x] Wave47 (2026-06-11)：ClimatePolityFitRules(#1443) / IntermediatePowerRules(#1446) / LegalFitnessRules(#1449) / CommerceModeratesWarRules(#1453) / PropertyOriginRules(#1447) / TrustMandateRules(#1450) / ModernizationProgramRules(#1431)（並列7・モンテスキュー法の精神MONT完結＋ロック統治二論LOCK＋孫子拡張SKUN着手・親がClimatePolityFitテストclamp修正）
- [x] Wave48 (2026-06-11)：FleetDoctrineRules(#1432) / NationalDeterminationRules(#1433) / SeaControlLeverageRules(#1434) / ForeignAdvisorRules(#1435) / DecisiveBattleWindowRules(#1436) / SovereigntyNormRules(#1428) / KontributionRules(#1420)（並列7・孫子拡張SKUN完結＋三十年戦争TYW着手・全テスト一発green）
- [x] Wave49 (2026-06-11)：KriegsherrRules(#1424) / WarPurposeDriftRules(#1426) / MultipartyPeaceRules(#1427) / PrestigeRules(#1406) / CapacityRules(#1409) / MetaLegitimacyRules(#1411) / CommitmentRules(#1414)（並列7・三十年戦争TYW完結＋項羽と劉邦KORY着手・全テスト一発green・ティック復旧後の再開Wave）

> **キュー枯渇（2026-06-10・Wave29完了）→ 第5次補充で再開（2026-06-11）**：新規EPIC群（経済思想・社会理論）の純ロジック子Issueが多数追加されたため補充・ループ再開。

### Issue連動（第5次補充・2026-06-11／経済思想・社会理論EPIC群の純ロジック子Issue・21件＝3Wave分）
# マッカイ『狂気とバブル』MNIA（#1617）／キンドルバーガー『熱狂、恐慌、崩壊』KNDB（#1608）
# レビンソン『コンテナ物語』CNTR（#1609）／ヴェブレン『有閑階級の理論』VEBL（#1589）
# ポランニー『大転換』POLA（#1585）／シュンペーター SCHU（#1579）
- [x] AccusationCascadeRules：崩壊後の告発カスケード＝スケープゴート自己増殖を制度強度で抑制（MNIA-4 #1625）。`PurgeRules`(政策粛清)/`TerrorRules`(恐怖)とは別＝マニア崩壊後の責任転嫁連鎖（Wave30）
- [x] ContraryPositionRules：逆張り迫害・勝利構造＝マニアピーク時の迫害コストと崩壊後の逆転（MNIA-3 #1624）。`PreferenceFalsificationRules`(選好偽装)とは別＝群集に逆らう者の損益反転（Wave30）
- [x] BubblePriceRules：バブル価格解離＝マニア強度で価格乗数が膨らみ崩壊後オーバーシュートで底割れ（MNIA-2 #1622）。`CorneringRules`(買い占め投機)/`MarketRules`(需給均衡)とは別＝信念が値を動かす解離（Wave30）
- [x] BoomFraudRules：ブーム詐欺と信頼崩壊＝熱狂期に詐欺出現確率が上がり収縮期に発覚（KNDB-5 #1621）。`EspionageRules`/`ScandalRules`とは別＝景気循環に同期する詐欺の生起・発覚（EventEngineへ接続想定の純ロジック部）（Wave30）
- [x] ManiaRules：信念感染モデル＝SIR型伝播でマニア強度を解く（経済/政治/宗教横断）（MNIA-1 #1620・`ManiaState`同梱）。`ReligionRules`(改宗圧)/`PropagandaRules`(世論)とは別＝感染症数理の信念版（Wave30）
- [x] DebtDeflationRules：フィッシャーの負債デフレーション＝価格下落→実質債務膨張→強制売却の自己強化ループ（KNDB-4 #1619）。`InflationRules`(通貨劣化)/`FiscalRules`(国債)とは別＝デフレ下の債務スパイラル（Wave30）
- [x] FinancialContagionRules：金融伝染＝取付け・暴落の星間波及と防火壁・相関崩壊リスク（KNDB-3 #1615）。`BankRules`(単体取付け)/`ChainFragilityRules`(生産網)とは別＝金融ショックの面的波及（Wave30）
- [x] StandardizationRules：規格化の外部性＝共通規格採用度で輸送コスト低減・外交経済インセンティブ（CNTR-3 #1614）。`TradeRules`(交易利得)とは別＝規格採用のネットワーク外部性（Wave31）
- [x] LenderOfLastResortRules：最後の貸し手＝Bagehot原則（高金利/優良担保/無制限貸出）とモラルハザードのトレードオフ（KNDB-2 #1613）。`BankRules`(信用創造)とは別＝危機時の中央銀行介入（Wave31）
- [x] TransshipmentRules：ハブ星系・積み替え能力＝hubCapacity投資で周辺コスト低減・産出倍率↑（CNTR-2 #1612）。`LogisticsRules`(版図一体化)とは別＝物流ハブの集約効果（Wave31）
- [x] TransportCostRules：輸送コスト係数＝回廊コストを連続値化し版図一体化を拡張（CNTR-1 #1611）。`LogisticsRules`(連結成分)とは別＝回廊ごとの連続コスト（Wave31）
- [x] CrisisCycleRules：危機サイクル状態機械＝変位→熱狂→恐慌→収縮の弧（MinskyPhase enum同梱）（KNDB-1 #1610）。`ArmsRaceRules`等の螺旋とは別＝ミンスキー型金融循環の相（Wave31）
- [x] CeremonialismRules：制度の儀礼性＝機能↓でも威信で存続する役職/省庁の廃止抵抗（VEBL-4 #1603）。`BureaucracyBloatRules`(人数肥大)とは別＝儀礼的威信による存続慣性（Wave31）
- [x] SocialProtectionRules：社会保護制度の内生的成長＝市場圧力への自己防衛・ラチェット効果（POLA-5 #1602）。`RedistributionRules`(税の再分配)とは別＝二重運動の保護側ラチェット（Wave31）
- [x] OstentationRules：誇示的浪費と正統性＝浪費→正統性↑/過剰で財政圧迫→長期崩壊（VEBL-3 #1601）。`CeremonyRules`(儀礼イベント)/`HonorsRules`(栄典)とは別＝威信のための浪費の両刃（Wave32）
- [x] InternationalOrderRules：多極経済秩序の相互支持と連鎖崩壊＝四本柱カスケード（POLA-4 #1599）。`BalanceOfPowerRules`(多極均衡)/`CollectiveSecurityRules`とは別＝経済秩序の柱の相互依存崩壊（Wave32）
- [x] CompetitiveDemocracyRules：競争的民主主義と経済置換＝置換ショック→扇動政治家→民主的品質劣化（SCHU-6 #1598）。`DemagogueRules`(扇動家)/`PartyRules`とは別＝シュンペーター型民主主義の経済起点（Wave32）
- [x] EmulationRules：金銭的模倣カスケード＝消費規範の下方波及→需要底上げ→安定低下（VEBL-2 #1597）。`MarketRules`(需給)とは別＝地位模倣による消費規範の伝播（Wave32）
- [x] FictitiousCommodityRules：擬制商品ストレス＝労働/土地の完全商品化が生む固有の制度リスク（POLA-3 #1596）。`MarketRules`/`LandReformRules`とは別＝擬制商品化の社会ストレス（Wave32）
- [x] IntellectualCritiqueRules：知識人階級と正統性侵食＝繁栄→知識人余剰→体制批判の経路（SCHU-5 #1595）。`FreePressRules`(報道)/`PropagandaRules`とは別＝繁栄が生む知識人の批判圧（Wave32）
- [x] VeblenGoodsRules：Veblen財と誇示的消費＝地位財の逆需要曲線（StatusGood同梱）（VEBL-1 #1593）。`MarketRules`(通常財需給)とは別＝価格が上がるほど需要が増える地位財（Wave32）

### Issue連動（第6次補充・2026-06-11／経済思想・政治哲学EPIC群の純ロジック子Issue・21件＝3Wave分）
# シュンペーターSCHU（#1579）／スミス道徳感情論TMS（#1576）／マルサス人口論MALT（#1573）
# 孟子MENC（#1561）／墨子MOZI（#1551）／老子LAOZ（#1543）／ハイエクHAYK（#1539）／ポランニーPOLA（#1585）
- [x] CreativeDestructionRules：創造的破壊＝新技術が旧市場を萎縮させる破壊面＋置換ショック（SCHU-1 #1581）。`InnovationDiffusionRules`(技術伝播)/`ResearchRules`(研究)とは別＝新陳代謝の破壊側（Wave33）
- [x] EntrepreneurRules：企業家類型と起業活動＝均衡破壊者(イノベーター)vs管理者の人物弁別（SCHU-2 #1584）。`PersonRules`(適材適所)/`FirmRules`(企業)とは別＝起業家精神の弁別（Wave33）
- [x] BureaucratizationRules：官僚化とイノベーション死＝成功→制度化→革新力喪失の自壊ループ（SCHU-3 #1587）。`BureaucracyBloatRules`(人数肥大)/`CeremonialismRules`(儀礼性)とは別＝成功が革新を殺す逆説（Wave33）
- [x] InnovationWaveRules：革新クラスターと景気波動＝コンドラチェフ型4フェーズ（SCHU-4 #1591）。`CrisisCycleRules`(ミンスキー金融循環)とは別＝技術革新の長期波動（Wave33）
- [x] EmpathyRules：共感評判エンジン＝行動→道徳評価→支持/忠誠/opinion修正子（TMS-1 #1578・スミス道徳感情論）。`ReputationRules`(名声)/`JusticeRules`(正義観)とは別＝共感に基づく道徳評価（Wave33）
- [x] ImpartialObserverRules：公平な観察者フィルター＝自己欺瞞バイアス→腐敗加速のブレーキ（TMS-2 #1582）。`RegimeRules`(腐敗)とは別＝内なる観察者による自制（Wave33）
- [x] MoralStyleRules：3徳統治スタイル軸＝慎慮/仁愛/正義→安定度修正子（TMS-3 #1586）。`GovernanceRules`(安定度)/`WangDaoRules`(王道覇道)とは別＝徳の統治スタイル（Wave33）
- [x] CommercialIntegrityRules：商業誠実性の信頼基盤＝繰り返し交易→信頼蓄積→opinion修正（TMS-4 #1590）。`TradeRules`(交易利得)/`DiplomacyRules`(opinion)とは別＝商業の信頼蓄積（Wave34）
- [x] CarryingCapacityRules：食糧天井関数＝農業産出×人口→FoodStressRatio（MALT-1 #1574・マルサス人口論）。`DemographicsRules`(人口動態)/`ResourceProductionRules`(資源)とは別＝食糧の収容限界（Wave34）
- [x] MalthusianCheckRules：マルサスチェック＝FoodStressRatio→出生率↓・死亡率↑の変調係数（MALT-2 #1575）。`DemographicsRules`(VitalRates)とは別＝食糧逼迫の人口抑制（Wave34）
- [x] PoorLawRules：貧者救済のパラドックス＝福祉→出生刺激→長期に賃金帳消し（MALT-4 #1580）。`RedistributionRules`(税再分配)/`SocialProtectionRules`(保護ラチェット)とは別＝救済の逆説（Wave34）
- [x] MoralSproutsRules：四端モデル＝仁/義/礼/智の住民道徳的感受性（MENC-1 #1564・孟子）。`MoralStyleRules`(統治スタイル)/`GovernanceRules`とは別＝住民の道徳的素地（MoralSprouts同梱）（Wave34）
- [x] GovernanceStyleRules：仁政と覇道の時間動態＝王道の長期持続性vs覇道の短期最強（MENC-3 #1568）。`WangDaoRules`(王道覇道の主義ドリフト)とは別＝仁政vs覇道の時間トレードオフ（Wave34）
- [x] MoralForceRules：浩然之気＝一貫した善政の積み重ね→道徳的気力蓄積→忠誠/カリスマ係数（MENC-4 #1570）。`FocusRules`(三密集中)/`ReputationRules`とは別＝善政の積み重ねが生む気力（MoralForce同梱）（Wave34）
- [x] DefenseGuildRules：守城専門集団＝非国家の防衛請負組織（MOZI-1 #1555・墨子）。`MercenaryRules`(傭兵)/`FortressRules`(要塞)とは別＝守城専門の請負ギルド（DefenseGuild同梱）（Wave35）
- [x] CompetenceLegitimacyRules：尚賢の正統性直結＝能力→体制正統性の保全倍率（MOZI-4 #1565）。`MeritPromotionRules`(功績昇進)/`PersonRules`とは別＝賢者登用が正統性を保つ（Wave35）
- [x] FrugalityDoctrineRules：節用の財政効率＝倹約ドクトリン→産出↑・貴族合意↓（MOZI-5 #1567）。`FiscalRules`(財政)/`RationingRules`(配給)とは別＝倹約の財政効率と貴族の不満（Wave35）
- [x] NonAggressionDoctrineRules：非攻ドクトリン＝自己拘束→外交信用・攻撃不可のトレードオフ（MOZI-2 #1560）。`DiplomacyRules`(条約)/`DeterrenceRules`とは別＝自己拘束による外交信用（Wave35）
- [x] WaterDoctrineRules：柔弱ドクトリン＝短期劣後・長期士気回復力（LAOZ-4 #1558・老子）。`Moraleの係数算出`/`ResilienceRules`系とは別＝柔よく剛を制す長期回復（Wave35）
- [x] SpontaneousOrderRules：自生的秩序の脆弱性＝強制介入→自生的秩序侵食→市場効率低下（HAYK-6 #1556・ハイエク）。`MarketRules`(需給)/`GovernanceRules`とは別＝介入が自生秩序を蝕む（Wave35）
- [x] EmbeddednessRules：市場の埋め込み度指標＝自由化で効率↑不安定↑のトレードオフ（POLA-1 #1588・ポランニー、MarketEmbeddedness同梱）。`SocialProtectionRules`(保護ラチェット)/`MarketRules`とは別＝市場の社会への埋め込み度（Wave35）

### Issue連動（第7次補充・2026-06-11／経済学・政治哲学EPIC群の純ロジック子Issue・21件＝3Wave分）
# ケインズKEYN（#1538）／ハイエクHAYK（#1539）／老子LAOZ（#1543）／アーレントBNAL（#1527）／ウェーバーWEBR／孟子MENC
- [x] EffectiveDemandRules：有効需要ギャップ＝需要不足が潜在産出を遊休させる（KEYN-1 #1540・ケインズ一般理論）。`MarketRules`(個別財需給)/`FiscalRules`とは別＝マクロの需要不足とOutputGap（Wave36）
- [x] MultiplierRules：財政乗数＝財政支出→所得連鎖の増幅 k=1/(1−c)（KEYN-2 #1542）。`FiscalRules`(国家財政)とは別＝支出の波及増幅（Wave36）
- [x] AnimalSpiritsRules：アニマルスピリッツ＝信認崩壊→投資凍結→需要不足の自己強化スパイラル（KEYN-3 #1545）。`StockMarketRules`(株価)/`CrisisCycleRules`とは別＝投資家心理の集合的崩壊（Wave36）
- [x] LiquidityPreferenceRules：流動性選好と金利下限＝ZLBで金融政策が無効化し財政のみ有効（KEYN-4 #1548）。`BankRules`/`FiscalRules`とは別＝流動性の罠（Wave36）
- [x] ThriftParadoxRules：節約のパラドックス＝全勢力一斉緊縮→需要連鎖崩壊→集合的行為問題（KEYN-5 #1552）。`MultiplierRules`(乗数)とは別＝合成の誤謬（Wave36）
- [x] PlanningDriftRules：計画経済ドリフト＝介入累積ラチェット→権威主義圧力（HAYK-1 #1541・ハイエク隷属への道）。`SocialProtectionRules`(保護ラチェット)とは別＝統制の累積が権威主義を呼ぶ（Wave36）
- [x] CalculationProblemRules：計算問題と中央計画の効率損失＝価格なき計画→生産性ペナルティ（HAYK-2 #1544）。`MarketRules`(価格発見)とは別＝計画経済の情報問題（Wave36）
- [x] AuthoritarianSelectionRules：なぜワルモノが上に立つか＝全体主義体制の指導者選別バイアス（HAYK-3 #1547）。`CoupRules`/`PurgeRules`とは別＝逆淘汰の選別圧（Wave37）
- [x] LegalGeneralityRules：法の一般性と恣意的命令＝RuleOfLawIndex→合意撤回・抵抗権連動（HAYK-4 #1549）。`MagnaCartaRules`/`ConstitutionRules`とは別＝法の一般性vs個別命令（Wave37）
- [x] EconomicFreedomRules：経済的自由と政治的自由の連動＝経済統制度→協力係数→安定度（HAYK-5 #1553）。`ConsentRules`(協力)/`SpontaneousOrderRules`とは別＝二つの自由の連動（Wave37）
- [x] WuWeiRules：無為ガバナンス＝少介入→自然安定・介入過剰の逆U字ペナルティ（LAOZ-1 #1546・老子）。`GovernanceRules`(安定度)とは別＝無為の治の逆U字（Wave37）
- [x] ReversalRules：反者道之動＝汎用逆U字 tipping-point 曲線（LAOZ-2 #1550）。`EscalationRules`(梯子)とは別＝物極まれば反るの汎用曲線（Wave37）
- [x] ContentmentRules：知足安定＝適正規模ボーナス・版図一体化の正側補完（LAOZ-3 #1554）。`LogisticsRules`(一体化)/`OverextensionRules`(過拡張)とは別＝知足の適正規模（Wave37）
- [x] ThoughtlessnessRules：悪の凡庸性＝hierarchyDepth×complianceNorm→moralAgencyFactorとAtrocityRisk（BNAL-1 #1530・アーレント）。`AtrocityRules`(虐殺の実行)とは別＝無思考による加担（BanalityState同梱）（Wave37）
- [x] PluralityRules：複数性と公的領域＝perspectiveDiversity・IsTotalitarian・ActionCapacity・AtomizationLevel（BNAL-2 #1532）。`FreePressRules`/`PreferenceFalsificationRules`とは別＝複数性の喪失（PoliticalSpace同梱）（Wave38）
- [x] TotalitarianRules：全体主義の動態＝atomization・terror・ideologySubstitution・TerrorLoopGain（BNAL-3 #1535）。`SecurityRules`(秘密警察)/`MartialLawRules`とは別＝全体主義の自己強化ループ（TotalitarianPressure同梱）（Wave38）
- [x] WarCrimesRules：組織犯罪の責任連鎖＝IndividualCulpability・CanClaimObedience・TrialOutcome（BNAL-4 #1536）。`TribunalRules`(法廷の正統性)/`AtrocityRules`とは別＝指揮系統の個人有責性（AccountabilityChain同梱）（Wave38）
- [x] PoliticalEthicsRules：心情倫理vs責任倫理＝政治的意思決定の倫理軸×帰結責任・原則コスト（WEBR-2 #1528・ウェーバー職業としての政治）。`MoralStyleRules`(スミス三徳)/`JusticeRules`とは別＝政治家の倫理類型（PoliticalEthicsType同梱）（Wave38）
- [x] PoliticalVocationRules：政治の職業化＝召命型vs生業型×党機械の官僚化・腐敗傾性（WEBR-3 #1531）。`PatronageRules`(猟官)/`PartyRules`とは別＝政治家の召命vs生業（VocationOrientation同梱）（Wave38）
- [x] PlebiscitaryRules：ツェーザリズムと人民投票的指導者＝大衆直接動員×LeadershipElectionRulesとの合成（WEBR-4 #1533）。`DemagogueRules`(扇動)/`PlebisciteRules`(住民投票)とは別＝人民投票的指導者民主主義（Wave38）
- [x] PopulationMigrationRules：足による投票＝仁政→人口吸引・苛政→人口流出（MENC-2 #1566・孟子）。`MigrationRules`(平時移民の引力)とは別＝仁政の質に応じた星系間人口移動（Wave38）

### Issue連動（第8次補充・2026-06-11／マキャヴェッリMKV・クラウゼヴィッツCLZの純ロジック子Issue・7件＝1Wave分）
# マキャヴェッリ『君主論』MKV（#1139-1142）／クラウゼヴィッツ『戦争論』CLZ（#1133/1135/1136）
- [x] ConquestGovernanceRules：征服地統治の三様＝旧秩序駆逐/植民/傀儡の3戦略×統合速度・裏切りリスクのトレードオフ（MKV-1 #1139・マキャヴェッリ君主論）。`GovernanceRules`(安定度)/`ColonizationRules`(入植)とは別＝征服地の統治戦略選択（Wave39）
- [x] FearVsHatredRules：恐怖と憎悪の回廊＝賢明な強制力（恐れられる）vs残虐な抑圧（憎まれる）の非線形モデル（MKV-2 #1140）。`TerrorRules`(恐怖の媒体増幅)/`MartialLawRules`とは別＝「恐れられても憎まれるな」の境界（Wave39）
- [x] AdvisorCandorRules：直言参謀と佞臣＝政治情報の品質→政策の現実乖離度（MKV-3 #1141）。`CounselRules`(献策の採択)/`ImpartialObserverRules`とは別＝諫言の質が政策の現実適合を決める（Wave39）
- [x] VirtuFortunaRules：ヴィルトゥーとフォルトゥーナ＝統治者の適応力（virtù）×外的偶然（fortuna）の統治修正子（MKV-4 #1142）。`GrowthRules`(成長)/`SenescenceRules`(衰え)とは別＝力量と運命の相互作用（Wave39）
- [x] FrictionRules：作戦摩擦モデル＝命令深度×補給×士気→実行成功確率（CLZ-1 #1133・クラウゼヴィッツ戦争論）。`OperationPlanRules`(計画の質)/`CommunicationsRules`(指揮遅延)とは別＝戦場の摩擦（計画と実行の乖離）（Wave39）
- [x] TrinitarianTensionRules：三位一体の緊張＝政府意志×軍事力×民衆支持の崩壊検知（CLZ-3 #1135）。`FactionStateRules`(国家状態合成)/`WarPoliticsRules`とは別＝クラウゼヴィッツの三位一体の均衡破綻（Wave39）
- [x] CenterOfGravityRules：重心分析＝銀河グラフ上の重心（CoG）星系/艦隊の同定とAI優先化（CLZ-4 #1136）。`ChokepointValueRules`(要衝価値)/`LogisticsRules`とは別＝戦略重心の同定（叩くべき一点）（Wave39）

### Issue連動（第9次補充・2026-06-11／全体主義・政治哲学EPIC群の純ロジック子Issue・21件＝3Wave分）
# アーレント全体主義TOTL／ポパー開かれた社会POPR／フーコー規律PANO／アリストテレス政治学ARIS／トクヴィルTOCQ／ウェーバーWEBR-1
- [x] HerrschaftRules：支配の三類型＝伝統的/カリスマ的/合法的×安定プロファイル・崩壊モード（WEBR-1 #1525・ウェーバー）。`CivilianControlRules`/`RegimeRules`とは別＝支配の正統性類型（HerrschaftType同梱）（Wave40）
- [x] StatelessnessRules：無権利者の創出＝国籍剥奪・法外人口クラス（TOTL-5 #1526・アーレント）。`CitizenshipRules`(市民権の段階)/`RefugeeRules`とは別＝権利を持つ権利の剥奪（Wave40）
- [x] SuperfluousnessRules：余剰性＝使い捨て人口が全体主義運動の吸収率を上げる（TOTL-4 #1524）。`DemographicsRules`/`TotalitarianRules`とは別＝余剰人間が運動の燃料になる（Wave40）
- [x] ImperialBlowbackRules：帝国主義の還流＝辺境の暴力が国内の急進化へフィードバック（TOTL-3 #1522）。`FrontierRules`/`OverextensionRules`とは別＝植民地暴力の本国還流（Wave40）
- [x] TerrorPrincipleRules：テロの原理化＝道具だった恐怖が目的に変わり粛清が自己増殖する（TOTL-2 #1519）。`TerrorRules`(テロの劇場性)/`PurgeRules`とは別＝恐怖が手段から目的へ転化（Wave40）
- [x] HistoricismTrapRules：歴史主義の罠＝必然論イデオロギーが適応拒否を生み脆性を増す（POPR-5 #1521・ポパー開かれた社会）。`DynastyRules`(天命)とは別＝歴史法則信仰が硬直を呼ぶ（Wave40）
- [x] ToleranceParadoxRules：寛容のパラドックス＝不寛容派の容認が乗っ取りリスクを生む抑制のジレンマ（POPR-4 #1518）。`PluralityRules`/`FreePressRules`とは別＝寛容が自らを滅ぼす逆説（Wave40）
- [x] InstitutionalCorrectionRules：誤り蓄積と脆性崩壊＝errorStock臨界→非線形崩壊確率（POPR-3 #1517）。`RegimeRules`(腐敗)/`AsabiyyaRules`とは別＝自己修正できない制度の誤り蓄積（Wave41）
- [x] PiecemealEngineeringRules：漸進的改革vs全体改造＝リスク分布の二様・改革モード選択（POPR-2 #1514）。`DynastyRules.Reform`/`LandReformRules`とは別＝漸進改革とユートピア改造のリスク差（Wave41）
- [x] OpennessRules：開放度スペクトル＝自己修正能力・誤り蓄積・適応速度（POPR-1 #1511・開かれた社会）。`SpontaneousOrderRules`/`FreePressRules`とは別＝開かれた社会vs閉じた社会の適応力（OpennessState同梱）（Wave41）
- [x] PanoptismRules：パノプティコン係数＝監視インフラ密度→事前抑止効果（PANO-1 #1507・フーコー）。`SecurityRules`(秘密警察)/`CensusRules`とは別＝見られている意識による自己規律（SurveillanceState同梱）（Wave41）
- [x] NormalizationRules：規律訓練と標準化＝訓練強度→信頼性↑・創発シナジー↓（PANO-2 #1508）。`VeterancyRules`/`DisciplineRules`とは別＝規律権力による標準化のトレードオフ（Wave41）
- [x] ExaminationRules：考課制度＝定期記録→昇進反映・反乱予兆検出精度向上（PANO-3 #1509）。`PerformanceReviewRules`(9-box)とは別＝フーコー的試験・記録の権力（Wave41）
- [x] MesoiRules：中間層安定化係数＝FiscalClass中間層シェア→政体安定倍率（ARIS-1 #1495・アリストテレス政治学）。`RedistributionRules`/`CoalitionRules`とは別＝中間層が分厚いほど政体が安定（Wave41）
- [x] CommonGoodOrientationRules：公益-私益政体品質スコア＝累進度・制度制約→腐敗加速係数（ARIS-2 #1499）。`JusticeRules`/`RegimeRules`とは別＝政体が公益志向か私益志向かの品質（Wave42）
- [x] ChrematisticsRules：収奪経済志向＝管理型/収奪型の動機区別→腐敗加速の別回路（ARIS-3 #1502）。`MonopolyRules`/`ExtractiveDecay`とは別＝アリストテレスの蓄財術批判（Wave42）
- [x] CivicPhiliaRules：市民的信頼と審議崩壊＝不平等・僭主圧力で低下→膠着増幅（ARIS-4 #1503）。`ConsentRules`/`SeparationOfPowersRules`とは別＝市民的友愛（ポリス的信頼）（Wave42）
- [x] TyrantToolkitRules：僭主維持術＝貧困化課税・出る杭排除・大型事業・密告・不信醸成の短長期効果（ARIS-5 #1504）。`SecurityRules`/`PurgeRules`とは別＝アリストテレスの僭主術カタログ（Wave42）
- [x] SoftDespotismRules：穏やかな専制＝行政後見国家・暴力なき受動化（TOCQ-4 #1492・トクヴィル）。`CoupRules`(暴力的奪取)とは別＝福祉的後見による自由の去勢（CoupRulesとの対称系）（Wave42）
- [x] EqualityDriftRules：平等化の潮流と身分侵食＝民主化圧力が階級/序列に与える長期係数（TOCQ-5 #1498）。`SerfdomRules`/`CitizenshipRules`とは別＝身分制を溶かす平等化の不可逆潮流（Wave42）
- [x] AssociationRules：中間団体・市民結社＝国家と個人の間の自発的緩衝体（TOCQ-2 #1482）。`PluralityRules`(複数性)/`LobbyRules`とは別＝トクヴィルの結社が専制を防ぐ（CivicAssociation同梱）（Wave42）

### Issue連動（第10次補充・2026-06-11／共和制・自由主義・社会契約EPIC群の純ロジック子Issue・21件＝3Wave分）
# フェデラリストFED／マキャヴェッリ論考DISC／ミル自由論MILL／ルソー社会契約ROUS／ホッブズLEVI／トクヴィルTOCQ-1
- [x] FactionMultiplicityRules：派閥増殖安定則＝HHI逆数→多数派暴政リスク低下・会派形成コスト（FED-1 #1473・フェデラリスト）。`CoalitionRules`/`PartyRules`とは別＝派閥が多いほど多数派専制が起きにくい（マディソン）（Wave43）
- [x] AmbitionCounterRules：野心相殺設計＝制度ポジションの自己利益→抑制均衡の能動的発動条件（FED-2 #1476）。`SeparationOfPowersRules`/`ImpeachmentRules`とは別＝「野心には野心を対抗させる」設計（Wave43）
- [x] CompoundRepublicRules：複合共和制と二層主権＝委譲/保留権限→垂直抑制強度→専制リスク低下（FED-3 #1481）。`FederalismRules`/`ConstitutionRules`とは別＝二層主権の垂直チェック（Wave43）
- [x] ExtendedRepublicRules：拡大共和国の安定＝版図規模×派閥多様性→安定補正（FED-4 #1485）。`LogisticsRules`/`PolityScaleRules`とは別＝大共和国ほど派閥が中和し安定（マディソン）（Wave43）
- [x] ExecutiveEnergyRules：行政エネルギーと単一執政＝執政の統一→決断速度×クーデター確率低下のトレードオフ（FED-5 #1489）。`CivilianControlRules`/`CoupRules`とは別＝単一執政の決断力（ハミルトン）（Wave43）
- [x] RepresentativeFilterRules：代表による派閥濾過＝選挙区規模→濾過強度→派閥的歪み低減（FED-6 #1494）。`PartyRules`/`PlebisciteRules`とは別＝代表制が直接民主の派閥熱を濾す（Wave43）
- [x] CensorshipRules：情報自由度と検閲水準＝短期安定vs長期腐敗の非対称（MILL-1 #1474・ミル自由論、InformationEnvironment同梱）。`FreePressRules`(腐敗発見)/`PropagandaRules`とは別＝検閲の短期安定と長期腐敗（Wave43）
- [x] PublicOpinionRules：世論ダイナミクスと多数派専制＝意見多様度→情報品質係数（MILL-2 #1477、OpinionField同梱）。`PropagandaRules`/`DemagogueRules`とは別＝世論場の多数派専制と情報品質（Wave44）
- [x] HarmPrincipleRules：危害原理の形式化＝過剰抑圧の加速コスト・正当性閾値（MILL-3 #1480）。`MartialLawRules`/`JusticeRules`とは別＝「他者への危害のみ規制しうる」ミルの原理（Wave44）
- [x] LibertyCultureRules：個性と実験の社会価値＝意見多様度→研究・適応力係数（MILL-5 #1487）。`OpennessRules`/`ResearchRules`とは別＝自由な個性が社会の実験室になる（ミル）（Wave44）
- [x] MajorityTyrannyRules：多数者の専制＝社会的同質化圧力・少数意見の封殺（TOCQ-1 #1478・トクヴィル、MinorityOpinion同梱）。`PluralityRules`/`PreferenceFalsificationRules`とは別＝多数派の社会的同調圧力（Wave44）
- [x] MilitiaLoyaltyRules：市民軍・志願兵・傭兵の忠誠軸＝徴募源→逆境時の離反確率差（DISC-2 #1483・マキャヴェッリ論考）。`MercenaryRules`/`ConscriptionRules`とは別＝徴募源別の逆境忠誠（Wave44）
- [x] RinnovazioneRules：リノヴァツィオーネ＝制度疲弊→刷新ウィンドウ→予防的自己刷新（DISC-3 #1488）。`DynastyRules.Reform`/`InstitutionalCorrectionRules`とは別＝危機前の予防的刷新（マキャヴェッリ「初心に立ち返る」）（Wave44）
- [x] FounderTrajectoryRules：建国者の自己廃絶テスト＝制度投資速度vs権力集中速度→共和制軌道or専制固定の評価（DISC-4 #1493）。`SuccessionRules`/`PublicPrivateSeparationRules`とは別＝建国者が制度を残すか権力を握るか（Wave44）
- [x] GeneralWillRules：一般意志汚染指標＝派閥捕獲vs公益統治の合成スコア（ROUS-1 #1462・ルソー社会契約、GeneralWillState同梱）。`CommonGoodOrientationRules`/`LobbyRules`とは別＝一般意志vs特殊意志の汚染（Wave45）
- [x] LawgiverRules：立法者パラドックス＝建国の憲法制定権力・一回性の制度初期化（ROUS-2 #1464）。`ConstitutionRules`/`FounderTrajectoryRules`とは別＝ルソーの立法者（制度を作る者は制度の外）（Wave45）
- [x] PolityScaleRules：政体規模適合＝版図×人口→政体適合度スコア・ミスマッチペナルティ（ROUS-3 #1466）。`ExtendedRepublicRules`/`OverextensionRules`とは別＝政体には適正規模がある（ルソーは小国を是とした）（Wave45）
- [x] CivicFaithRules：市民宗教＝政府製造の政治的結束信仰・形骸化→崩壊の動学（ROUS-4 #1468、CivicFaith同梱）。`ReligionRules`/`HopeRules`とは別＝ルソーの市民宗教（政治的結束の信仰）（Wave45）
- [x] AnarchyCostRules：無政府宙域の自然状態コスト＝崩壊後の宙域コスト・隣接不安定化・再統合インセンティブ（LEVI-1 #1459・ホッブズ、AnarchyState同梱）。`CivilWarRules`/`FrontierRules`とは別＝万人の万人に対する闘争のコスト（Wave45）
- [x] SecurityDilemmaRules：安全保障ジレンマ＝防衛目的の建艦が隣接を刺激し螺旋→「誰も望まない戦争」（LEVI-2 #1461・ホッブズ）。`ArmsRaceRules`(リチャードソン量の螺旋)とは別＝防衛動機が生む猜疑の構造（Wave45）
- [x] CovenantRules：コヴェナント＝保護契約の閾値＝防衛失敗で服従義務消滅→合意撤回へ転送（LEVI-3 #1463）。`ConsentRules`/`CovenantRules`とは別＝ホッブズの保護と服従の契約（守れない主権者への服従は消える）（Wave45）

### Issue連動（第11次補充・2026-06-11／古典政体論・近代化・社会契約EPIC群の純ロジック子Issue・21件＝3Wave分）
# ポリュビオスPOLY／モンテスキューMONT／ロックLOCK／孫子拡張SKUN（坂の上の雲型近代化）／三十年戦争TYW
- [x] AnacyclosisRules：六政体類型と腐落ベクトル＝RegimeForm enum・六形態の循環（POLY-1 #1442・ポリュビオス政体循環論）。`DynastyRules`/`RegimeRules`とは別＝王政→僭主→貴族→寡頭→民主→衆愚の循環（アナキュクローシス）（Wave46）
- [x] MixedConstitutionRules：混合政体の安定指数＝三成分混合比→腐落抵抗・シャノン混合度（POLY-2 #1445）。`SeparationOfPowersRules`/`CompoundRepublicRules`とは別＝王政/貴族政/民主政の混合が循環を止める（Wave46）
- [x] TycheRules：運命耐性＝制度品質×Tyche係数→EventEngineイベント効果変調（POLY-3 #1448）。`VirtuFortunaRules`(力量と運命)とは別＝制度の質が偶然の打撃を吸収する（Wave46）
- [x] UniversalHistoryRules：普遍史の因果波及＝星系間の事件連鎖・距離減衰（POLY-4 #1451）。`DisclosureRules`/`NotificationCenter`とは別＝ポリュビオスの「歴史は連関する」事件の波及（Wave46）
- [x] InstitutionalMemoryRules：歴史の教訓＝危機学習→制度知識蓄積・組織学習（POLY-5 #1454）。`GenerationalMemoryRules`(戦争記憶)/`InstitutionalCorrectionRules`とは別＝危機から学んで制度知が蓄積する（Wave46）
- [x] GovernmentPrincipleRules：政体の原動力＝徳/名誉/恐怖×原理強度→服従コスト係数（MONT-1 #1439・モンテスキュー法の精神）。`WangDaoRules`/`HerrschaftRules`とは別＝共和政=徳/君主政=名誉/専制=恐怖の駆動原理（Wave46）
- [x] PolityCorruptionRules：諸政体の腐化回路＝共和→寡頭・君主→専制・専制→崩壊の型別経路（MONT-2 #1440）。`RegimeRules`(腐敗)/`AnacyclosisRules`とは別＝政体類型ごとの固有の腐敗経路（Wave46）
- [x] ClimatePolityFitRules：風土と政体の相性＝惑星環境×政体原動力→安定度係数（MONT-3 #1443）。`TerrainRules`/`GovernanceRules`とは別＝モンテスキューの風土論（環境が政体適合を左右）（Wave47）
- [x] IntermediatePowerRules：中間権力の緩衝強度＝中間団体→専制リスク低下・専制滑落抑制（MONT-4 #1446）。`AssociationRules`/`CompoundRepublicRules`とは別＝モンテスキューの中間団体が君主政を専制から守る（Wave47）
- [x] LegalFitnessRules：法の適合性＝風土×思想×産業整合→正統性係数・反乱圧力（MONT-5 #1449）。`LegalGeneralityRules`/`GovernanceRules`とは別＝法がその社会に適合しているか（Wave47）
- [x] CommerceModeratesWarRules：通商と温和政治＝交易量→厭戦加速・専制政体は交易抑圧で低下（MONT-6 #1453）。`TradeRules`/`WarGoalRules`とは別＝モンテスキューの「商業は平和を促す（doux commerce）」（Wave47）
- [x] PropertyOriginRules：労働財産論と先占権＝コモンズ/請求権の強さ（LOCK-1 #1447・ロック統治二論）。`PropertyRightsRules`(財産保護)/`ColonizationRules`とは別＝ロックの労働が財産を生む先占権（Wave47）
- [x] TrustMandateRules：信託解消連鎖＝侵犯蓄積→信託解消→反乱正当化（LOCK-2 #1450）。`ConsentRules`/`CovenantRules`とは別＝ロックの政府は信託で抵抗権が信託違反で発動（Wave47）
- [x] ModernizationProgramRules：近代化プログラム＝研究×造船×人材の多面加速・改革連動（SKUN-1 #1431・坂の上の雲型）。`ResearchRules`/`ShipyardRules`とは別＝後発国の富国強兵の多面加速（Wave47）
- [x] FleetDoctrineRules：艦隊ドクトリン選択＝enum{決戦/漸減/通商破壊/現存艦隊}→AI行動重みとドクトリン相性（SKUN-2 #1432）。`SunziDoctrineRules`/`OperationalAptitudeRules`とは別＝海軍ドクトリンの選択と相性（Wave48）
- [x] NationalDeterminationRules：国家意志・後発国の底力＝劣位戦力比での戦闘効率補正・士気回復加速（SKUN-3 #1433）。`Moraleの係数`/`RoyalPresenceRules`とは別＝背水の劣勢国の底力（Wave48）
- [x] SeaControlLeverageRules：制海権×陸上作戦協調＝制海権保有→隣接惑星攻城・補給ボーナス（SKUN-4 #1434）。`LogisticsRules`/`PlanetSiegeRules`とは別＝制海権が陸上作戦を有利にする（Wave48）
- [x] ForeignAdvisorRules：外国顧問・軍事援助＝同盟条件下で研究・人材育成を加速（SKUN-5 #1435）。`ForeignAidRules`/`MentorshipRules`とは別＝お雇い外国人による近代化加速（Wave48）
- [x] DecisiveBattleWindowRules：決戦の機会窓口＝蓄積条件が揃ったとき決戦の機会が発火（SKUN-6 #1436）。`EscalationRules`/`CenterOfGravityRules`とは別＝決戦の好機の生起判定（EventEngineへ接続想定の純ロジック部）（Wave48）
- [x] SovereigntyNormRules：主権規範の醸成＝領土主権規範の成熟→宗教的干渉の正当性低下（TYW-5 #1428・三十年戦争/ウェストファリア）。`DiplomacyRules`/`InfluenceRules`とは別＝主権国家規範の確立（内政不干渉）（Wave48）

### Issue連動（第12次補充・2026-06-11／戦争・軍事社会EPIC群の純ロジック子Issue・21件＝3Wave分）
# 三十年戦争TYW／項羽と劉邦KORY／レマルク西部戦線RMK／革命戦争WAP／スペイン内戦SPW
- [x] KontributionRules：コントリビューション制＝占領地の組織的抽出・前進圧力・戦争の自己永続（TYW-1 #1420・三十年戦争）。`ForageRules`(現地調達)/`SanctionsRules`とは別＝占領地搾取が戦争を自己永続させる（戦争が戦争を養う）（Wave48）
- [x] KriegsherrRules：軍事請負将軍＝将軍が私的融資で軍を所有・財務レバレッジ→政治的要求（TYW-2 #1424）。`MercenaryRules`/`PraetorianRules`とは別＝ヴァレンシュタイン型の私兵を持つ将軍の政治力（Wave49）
- [x] WarPurposeDriftRules：開戦理由の腐食＝宗教→権力政治ドリフト・イデオロギー的同盟逆転（TYW-3 #1426）。`WarGoalRules`/`DiplomacyRules`とは別＝戦争目的が当初の大義から権力闘争へ変質する（Wave49）
- [x] MultipartyPeaceRules：多極講和の協調問題＝三者以上の包括パッケージ合意・膠着検知（TYW-4 #1427）。`WarGoalRules`(講和受諾)/`CoalitionRules`とは別＝ウェストファリア型の多国間講和の難しさ（Wave49）
- [x] PrestigeRules：声望モデル＝陣営の人材磁力（KORY-1 #1406・項羽と劉邦、PrestigeState同梱）。`ReputationRules`(個人の名声)/`Meritocracyの誘引`とは別＝陣営の声望が人材を引き寄せる磁力（Wave49）
- [x] CapacityRules：器量＝指導者が才人を活かせる容量（KORY-2 #1409、CapacityTolerance同梱）。`CommandStaffRules`/`AdvisorCandorRules`とは別＝劉邦型の「己より優れた者を使う」器量（Wave49）
- [x] MetaLegitimacyRules：大義名分の競合＝外部権威の代弁競合（KORY-3 #1411、MetaAuthority同梱）。`CommandLegitimacyRules`/`WarGoalRules`とは別＝義帝を奉じる型の上位権威の代弁争い（Wave49）
- [x] CommitmentRules：背水の陣＝撤退不能コミットで戦闘力最大化・敗北は壊滅（KORY-4 #1414）。`ForcedMarchRules`/`DeterrenceRules`(退路を焼く)とは別＝韓信型の背水の陣（決死の戦闘力）（Wave49）
- [ ] PsychologicalSiegeMoraleRules：四面楚歌＝物理包囲×心理孤立の士気崩壊加速（KORY-5 #1419）。`EncirclementRules`(物理包囲)/`PropagandaRules`とは別＝心理的孤立が士気崩壊を加速する
- [ ] MeritRetentionRules：功臣処遇ジレンマ＝勝利後の有力功臣の厚遇/転封/粛清の安定化帰結（KORY-6 #1422）。`PurgeRules`/`CompensationRules`とは別＝劉邦型の建国功臣の処遇ジレンマ
- [ ] CombatFatigueRules：累積戦闘疲弊＝会戦をまたぐ持続的士気劣化（RMK-1 #1403・レマルク西部戦線）。`FleetMorale`/`ReadinessRules`(疲労)とは別＝連戦による持続的な士気の摩耗
- [ ] KameradschaftRules：戦友紐帯＝一次集団の凝集ボーナスと崩壊（RMK-2 #1405）。`FriendshipRules`(個人の盟友)/`FleetMorale`とは別＝小隊レベルの戦友愛が戦闘力を支える
- [ ] StalemateRules：膠着戦況＝拮抗＝第3の戦闘結果（RMK-3 #1408）。`PursuitRules`/`AutoBattleSim`とは別＝勝敗でない第三の結果（塹壕戦の膠着）
- [ ] HomeFrontRules：前線-後方情報非対称＝プロパガンダ格差と乖離崩壊（RMK-4 #1412）。`PropagandaRules`/`PublicOpinionRules`とは別＝前線の現実と銃後の幻想の乖離
- [ ] GenerationalWoundRules：世代断絶＝失われた世代の指導者欠乏（RMK-5 #1416）。`GenerationalMemoryRules`/`LifecycleRules`とは別＝大量戦死が将来の指導者層を空洞化させる
- [ ] ReturneesContagionRules：帰還兵の厭戦伝播＝後方の希望の侵食（RMK-6 #1418）。`RefugeeRules`/`HopeRules`とは別＝帰還兵が銃後へ厭戦と幻滅を持ち込む
- [ ] ScorchedEarthStateRules：焦土作戦の状態＝自領土破壊→敵の現地調達/デポ無効化（WAP-1 #1410・革命戦争）。`ScorchedEarthRules`(損益・既存)とは別＝焦土の進行状態と敵補給の無効化（ScorchedEarthState同梱）
- [ ] HomelandResistanceRules：侵攻深度×抵抗逓増＝深く入るほど補給コスト増・反乱自動増幅（WAP-2 #1413、InvasionDepthState同梱）。`OverextensionRules`/`Insurgencyの土壌`とは別＝ロシア戦役型の縦深抵抗
- [ ] MassEngagementRules：大規模会戦の規模限界＝総兵員数→摩擦への乗算拡張（WAP-3 #1417）。`FrictionRules`(作戦摩擦・生成済み)とは別＝大軍ほど摩擦が増す規模の限界
- [ ] TradeSpaceForTimeRules：戦略的受動撤退ドクトリン＝正規軍が決戦を拒否し攻勢終末点を誘発（WAP-4 #1421）。`CulminatingPointRules`/`PursuitRules`とは別＝空間を時間で買う退却戦略
- [ ] AllianceDivergenceRules：連合の隠れた目標乖離＝戦後の利益相反スコア（SPW-4 #1398・スペイン内戦、AllianceDivergence同梱）。`BurdenSharingRules`/`PartitionRules`とは別＝連合内の戦後を見据えた目標の食い違い
