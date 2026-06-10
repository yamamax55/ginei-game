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
- [ ] ByproductGlutRules：副産物グルット＝連産の従産物が供給過剰で価格暴落（#1113）。`MarketRules`へ接続・連産×市場
- [ ] ChainFragilityRules：連鎖の脆さ＝単一ノード遮断の上流グルット/下流欠品カスケード（#1112）。`SupplyRules`（補給線）とは別＝生産網の伝播
- [ ] ContinuousOperationRules：連続運転の硬直＝稼働率・turndownコスト＝止められない戦時硬直（#1115）。`MobilizationRules`とは別＝プラントの慣性
- [ ] IntermediateBufferRules：中間体の貯蔵不能＝バッファ無しがショックを増幅（#1116）。`ResourceStockpile`（備蓄可能財）とは別＝貯められない中間財
# 孫子ドクトリン SUN EPIC（#1126-1130）
- [ ] ForageRules：現地調達＝占領地・通過星系からの自律補給「糧を敵に因る」（#1128）。`SupplyRules`（後方からの補給線）とは別＝前線の自律調達
- [ ] CulminatingPointRules：攻勢終末点＝補給距離比例の戦力効率低下（#1129）。`OverextensionRules`（版図と国力・バックログ）とは別＝作戦距離の戦力減衰
- [ ] SunziDoctrineRules：謀攻優先＝謀>交>兵>攻城のAIスコアリング（#1130）。AIの戦略選択の重み付け＝盤面非依存のplain引数
- [ ] DeceptionRules：戦略的欺瞞＝偽情報・陽動で敵AIの行動を歪める（#1126）。`FeintRules`（戦術の陽動・実装済み）とは別＝戦略AIの認識操作
# 経済・通貨（SAW-1 #1072・ACC-1 #974）
- [ ] CoinageRules：通貨改鋳と品位＝正貨の銀含有量・シニョリッジvs信用（#1072）。`InflationRules`（増発による物価）とは別＝硬貨の品位劣化
- [ ] LedgerRules：複式簿記＝勘定体系＋仕訳エンジン（Σ借方=Σ貸方・#974）。`FiscalRules`（歳入歳出の集計）とは別＝記帳の整合性エンジン
# 評判メタ（ALM-5 #1059）
- [ ] WangDaoRules：王道値/覇道値＋主義ドリフト＝統治スタイルの評判メタ層（#1059）。`ReputationRules`（個人の名声）とは別＝勢力の道(王道/覇道)
# Pillars of the Earth EPIC（#1091-1096）
- [ ] QualityScheduleRules：工期と品質のトレードオフ＝急造が崩落/火災/襲撃の確率を上げる（#1091）。`MegaProjectRules`（#1090実装済み）へ接続＝方針レバー
- [ ] TechBearerRules：技術は人に宿る＝工法保持者の死で喪失・引き抜き/亡命で伝播（#1092）。`InnovationDiffusionRules`（国家間の拡散・実装済み）とは別＝人という乗り物
- [ ] CharterRightsRules：利権と特許状＝市場開設権/採掘権の授与・取消・争奪（#1093）。`MagnaCartaRules`（王権制約一般）とは別＝個別利権の管轄争い
- [ ] CityGrowthRules：大事業が都市を育てる＝プロジェクト→人口流入→市場成立→Province成長（#1094）。`GovernanceRules`（安定度）とは別＝集積による成長
- [ ] SuccessionWarRules：継承戦争＝君主死×継承危機→請求者並立→旗幟カスケードの国家規模化（#1095）。`LoyaltyRules`（会戦の寝返り・実装済み）の適用範囲拡大
- [ ] ClericalCareerRules：聖職キャリア＝宗教組織の役職ラダー・理想vs野心（#1096）。`CareerPipelineRules`（武/官/技）の第4系統＝聖
# 艦艇設計（ALM-12 #1066）
- [ ] ArmamentDesignRules：艦艇再設計＝技術スロット装填・拡張性/搭載量の制約最適化（#1066）。`ShipClass`（戦艦/巡航/駆逐の固定枠）とは別＝設計の自由度
