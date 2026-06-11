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
- [ ] CreativeDestructionRules：創造的破壊＝新技術が旧市場を萎縮させる破壊面＋置換ショック（SCHU-1 #1581）。`InnovationDiffusionRules`(技術伝播)/`ResearchRules`(研究)とは別＝新陳代謝の破壊側
- [ ] EntrepreneurRules：企業家類型と起業活動＝均衡破壊者(イノベーター)vs管理者の人物弁別（SCHU-2 #1584）。`PersonRules`(適材適所)/`FirmRules`(企業)とは別＝起業家精神の弁別
- [ ] BureaucratizationRules：官僚化とイノベーション死＝成功→制度化→革新力喪失の自壊ループ（SCHU-3 #1587）。`BureaucracyBloatRules`(人数肥大)/`CeremonialismRules`(儀礼性)とは別＝成功が革新を殺す逆説
- [ ] InnovationWaveRules：革新クラスターと景気波動＝コンドラチェフ型4フェーズ（SCHU-4 #1591）。`CrisisCycleRules`(ミンスキー金融循環)とは別＝技術革新の長期波動
- [ ] EmpathyRules：共感評判エンジン＝行動→道徳評価→支持/忠誠/opinion修正子（TMS-1 #1578・スミス道徳感情論）。`ReputationRules`(名声)/`JusticeRules`(正義観)とは別＝共感に基づく道徳評価
- [ ] ImpartialObserverRules：公平な観察者フィルター＝自己欺瞞バイアス→腐敗加速のブレーキ（TMS-2 #1582）。`RegimeRules`(腐敗)とは別＝内なる観察者による自制
- [ ] MoralStyleRules：3徳統治スタイル軸＝慎慮/仁愛/正義→安定度修正子（TMS-3 #1586）。`GovernanceRules`(安定度)/`WangDaoRules`(王道覇道)とは別＝徳の統治スタイル
- [ ] CommercialIntegrityRules：商業誠実性の信頼基盤＝繰り返し交易→信頼蓄積→opinion修正（TMS-4 #1590）。`TradeRules`(交易利得)/`DiplomacyRules`(opinion)とは別＝商業の信頼蓄積
- [ ] CarryingCapacityRules：食糧天井関数＝農業産出×人口→FoodStressRatio（MALT-1 #1574・マルサス人口論）。`DemographicsRules`(人口動態)/`ResourceProductionRules`(資源)とは別＝食糧の収容限界
- [ ] MalthusianCheckRules：マルサスチェック＝FoodStressRatio→出生率↓・死亡率↑の変調係数（MALT-2 #1575）。`DemographicsRules`(VitalRates)とは別＝食糧逼迫の人口抑制
- [ ] PoorLawRules：貧者救済のパラドックス＝福祉→出生刺激→長期に賃金帳消し（MALT-4 #1580）。`RedistributionRules`(税再分配)/`SocialProtectionRules`(保護ラチェット)とは別＝救済の逆説
- [ ] MoralSproutsRules：四端モデル＝仁/義/礼/智の住民道徳的感受性（MENC-1 #1564・孟子）。`MoralStyleRules`(統治スタイル)/`GovernanceRules`とは別＝住民の道徳的素地（MoralSprouts同梱）
- [ ] GovernanceStyleRules：仁政と覇道の時間動態＝王道の長期持続性vs覇道の短期最強（MENC-3 #1568）。`WangDaoRules`(王道覇道の主義ドリフト)とは別＝仁政vs覇道の時間トレードオフ
- [ ] MoralForceRules：浩然之気＝一貫した善政の積み重ね→道徳的気力蓄積→忠誠/カリスマ係数（MENC-4 #1570）。`FocusRules`(三密集中)/`ReputationRules`とは別＝善政の積み重ねが生む気力（MoralForce同梱）
- [ ] DefenseGuildRules：守城専門集団＝非国家の防衛請負組織（MOZI-1 #1555・墨子）。`MercenaryRules`(傭兵)/`FortressRules`(要塞)とは別＝守城専門の請負ギルド（DefenseGuild同梱）
- [ ] CompetenceLegitimacyRules：尚賢の正統性直結＝能力→体制正統性の保全倍率（MOZI-4 #1565）。`MeritPromotionRules`(功績昇進)/`PersonRules`とは別＝賢者登用が正統性を保つ
- [ ] FrugalityDoctrineRules：節用の財政効率＝倹約ドクトリン→産出↑・貴族合意↓（MOZI-5 #1567）。`FiscalRules`(財政)/`RationingRules`(配給)とは別＝倹約の財政効率と貴族の不満
- [ ] NonAggressionDoctrineRules：非攻ドクトリン＝自己拘束→外交信用・攻撃不可のトレードオフ（MOZI-2 #1560）。`DiplomacyRules`(条約)/`DeterrenceRules`とは別＝自己拘束による外交信用
- [ ] WaterDoctrineRules：柔弱ドクトリン＝短期劣後・長期士気回復力（LAOZ-4 #1558・老子）。`Moraleの係数算出`/`ResilienceRules`系とは別＝柔よく剛を制す長期回復
- [ ] SpontaneousOrderRules：自生的秩序の脆弱性＝強制介入→自生的秩序侵食→市場効率低下（HAYK-6 #1556・ハイエク）。`MarketRules`(需給)/`GovernanceRules`とは別＝介入が自生秩序を蝕む
- [ ] EmbeddednessRules：市場の埋め込み度指標＝自由化で効率↑不安定↑のトレードオフ（POLA-1 #1588・ポランニー、MarketEmbeddedness同梱）。`SocialProtectionRules`(保護ラチェット)/`MarketRules`とは別＝市場の社会への埋め込み度
