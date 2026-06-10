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

### Issue連動（第2次補充・2026-06-10／ERP系の純ロジック子Issue）
# 会計 ACC（#975,#976）
- [ ] BalanceSheetRules：貸借対照表B/S＝資産=負債+純資産の整合（#975）。`LedgerRules`(仕訳・第1次補充)の集計表＝静的スナップショット。`FiscalRules`(歳入歳出)とは別
- [ ] IncomeStatementRules：損益計算書P/L＝収益−費用=損益・暦で締め（#976）。`LedgerRules`のフロー集計＝期間損益。`FiscalRules`とは別＝企業会計の様式
# 財務AI AFN（#1013-1016）
- [ ] FiscalPolicyRules：財務ガードレール＝債務上限/準備金下限/税率レンジの逸脱判定（#1013）。`FiscalRules`(財政の実体)へ被せる国策レイヤー＝逸脱検知のみ
- [ ] AutoTreasuryRules：自律財務運用＝準備金割れで自動起債/借換/支払（#1014・タッチレス）。`FiscalRules`/`FiscalPolicyRules`を入力に取る自動操縦＝AIの財務行動選択
- [ ] CashFlowForecastRules：CF予測＝あとNヶ月で債務超過になる早期警告（#1015）。`FiscalRules`の将来投影＝予測のみ（実体は動かさない）
- [ ] FinancialAnomalyRules：異常検知＝粉飾/横領/異常支出のフラグ（#1016）。会計の整合崩れ・統計外れ値の検出＝`LedgerRules`/`BalanceSheetRules`を read-only で監査
# 調達 PRC（#1004,#1005,#1006）
- [ ] SupplierRatingRules：サプライヤー評価＝信頼性/納期遵守/関係スコア（#1004）。納入実績の加重評価＝発注先選定の入力。`TradeRules`(交易利得)とは別
- [ ] SourcingAuctionRules：逆オークション＝RFQ→入札→落札の価格発見（#1005）。複数サプライヤーの競争見積で最安/最適を選ぶ＝`MarketRules`(連続市場)とは別＝離散入札
- [ ] SupplyContractRules：契約管理＝長期供給契約 vs spot・破棄=戦争/制裁（#1006・DIP-2接続）。契約の履行/破棄の損得＝`TreatyRules`の商業版・`DiplomacyRules`へ波及
# 生産網 SCM（#983,#984,#985,#987）
- [ ] BomRules：部品表BOM＝艦→部品→素材の多層展開（#983・BomRules.Explode）。1製品の所要部材を再帰展開＝`CoupledProductionRules`(連産レシピ)とは別＝分解の木
- [ ] MrpRules：MRP所要量計算＝在庫×生産予定→正味所要→計画オーダー・リードタイム（#984）。`BomRules`の展開を入力に正味所要を出す＝資材所要計画
- [ ] ProductionOrderRules：発注・生産オーダー＝PO/PrOを鉱業/工業/船渠へ割付（#985）。`MrpRules`の計画オーダーを能力へ配分＝`ShipyardRules`(就役)とは別＝手前の発注
- [ ] CapacitySchedulingRules：有限能力スケジューリング＝WIP・ボトルネック・スループット（#987）。`ProductionOrderRules`を有限設備へ詰める＝制約理論TOC
# 企業 FRM（#1023,#1024,#1025）
- [ ] ValueChainRules：バリューチェーングラフ＝森→木→製材→家の加工段連鎖（#1023・#182具現化）。星系の資源賦存から最終財までの付加価値の流れ＝`CoupledProductionRules`(単一工程)を繋ぐ網
- [ ] FirmRules：企業＝生産主体＝各加工段を営む企業の資本/生産能力/自社P&L（#1024・#184/#185具現化）。`StockMarketRules`(株価)・`MarketRules`(市場)を結ぶミクロ主体
- [ ] CapitalInvestmentRules：資本投下・投資判断＝利潤率で能力投資・銀行融資/増資（#1025）。`FirmRules`の拡大再生産＝`CapitalRules`(ピケティ格差)とは別＝企業の設備投資
# 所有 OWN（#1035,#1036,#1038）
- [ ] PublicPrivateSeparationRules：公私の分離＝国庫 vs 元首私財・制度化で分離（#1035）。私物化の度合い＝`RegimeRules`(腐敗)へ接続。`FiscalRules`(国庫)とは別＝所有の帰属
- [ ] PropertyRightsRules：私有財産の保護＝民法・保護強度→投資意欲（#1036・#170/#624接続）。財産権の強さが経済を動かす＝`ConfiscationRules`(没収)・`MagnaCartaRules`の財産版
- [ ] InheritanceRules：相続・継承＝資産の世代継承（#1038・継承法#646接続）。`SuccessionLawRules`(爵位/君主位)とは別＝資産・封土の相続（分割/長子で散逸か集中か）
# 人事 HCM（#995,#996）
- [ ] PerformanceReviewRules：人事評価9-box＝実績×潜在のマトリクス（#995）。昇進/配置の入力＝`SeniorityRules`(席次)とは別＝業績評価
- [ ] CompensationRules：報酬・授爵・賞罰＝Compensation→士気/忠誠（#996・#817接続）。`HonorsRules`(勲章インフレ)・`MeritRankRules`(軍功爵位)を束ねる報酬体系→`LoyaltyRules`へ
