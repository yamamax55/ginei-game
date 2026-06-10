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

## キュー（上から順に消化）

### 軍事・戦術

### 戦争犯罪・諜報

### 政治・社会

### 経済

### 戦略・人物

### 軍事・即応態勢

### 抑止・外交

### 統治・制度
- [ ] CoalitionRules：連立政権。単独過半数なき議会の連立形成＝政策は最小公倍数に薄まり、小党が拒否権を持つ。`PartyRules`（党勢・首班）を read-only で参照可＝連立の安定度
- [ ] ImpeachmentRules：弾劾・不信任。合法的な政権打倒経路＝要件（証拠・議席）と成否、失敗した弾劾は政権を強化する。`CoupRules`（非合法打倒）の制度版
- [ ] TermLimitRules：任期制限。権力の時間制約と「非常時だから」の延長誘惑＝一度破ると慣習が死ぬ（共和制の死）。`ConstitutionRules`（制約一般）とは別＝時間軸の制約
- [ ] EmergencyPowersRules：国家緊急権。非常大権は危機を速く処理するが、解除されない非常事態が常態化する（全権委任法の罠）。`MartialLawRules`（治安戒厳）とは別＝憲法停止の力学
- [ ] FederalismRules：連邦制。中央と地方の権限配分＝分権は多様性と引き換えに統一行動が鈍る。`SeparationOfPowersRules`（水平の分立）に対する垂直の分立
- [ ] CitizenshipRules：市民権。参政権・公職資格の範囲＝拡大は統合を進め、二級市民の存在は火種を残す。`CultureRules`（民族同化）とは別＝法的地位の付与
- [ ] PatronageRules：猟官制・恩顧主義。官職を支持者に配って忠誠を買う＝政権は安定するが行政能力が劣化する。`SeniorityRules`（席次vs実力）とは別＝忠誠vs能力の人事

### 宮廷・人物
- [ ] CourtFavorRules：宮廷の寵愛・讒言。君主の寵を競う廷臣＝寵臣の専横と讒言による失脚、君主の眼力が防波堤。`PowerRules`（実権構造）とは別＝寵という通貨
- [ ] AmbitionRules：野心（ロイエンタール型）。実力者の野心は功績とともに育ち、主君の猜疑と共鳴して反逆の自己成就予言になる。`LoyaltyRules`（忠誠の解決）とは別＝野心と猜疑のスパイラル
- [ ] IllnessRules：病臥（ラインハルト型）。英雄の病＝執務不能・戦線離脱・病状の隠蔽と漏洩、死期が見えた政権の継承レース。`LifecycleRules`（加齢死亡）とは別＝突発的な健康イベント
- [ ] ScandalRules：醜聞。汚職・私行の露見が要人を失脚させる＝もみ消しは成功すれば無傷・失敗すれば倍返し。`SecurityRules`（体制側の監視）とは別＝個人の失脚力学

### 社会
- [ ] SerfdomRules：農奴制と解放。身分制労働の生産は安定だが上限が低い＝解放は短期ショックと引き換えに流動性と忠誠を得る（帝国の農奴解放）。`RedistributionRules`（税の再分配）とは別＝身分の再編
- [ ] GenerationalMemoryRules：戦争記憶の世代風化。戦争を知る世代が退場すると好戦論が再生する＝記憶の半減期が開戦閾値を下げる。`WarGoalRules.WarWeariness`（進行中の厭戦）とは別＝世代スケールの忘却
- [ ] BreadAndCircusesRules：パンとサーカス。娯楽と配給の供給が政治的不満をガス抜きする＝効くが依存し、途切れた時の反動が大きい。`HopeRules`（希望の実体）とは別＝慰撫の代替財
- [ ] VeteranPoliticsRules：退役軍人の政治力。傷痍軍人・戦友会が圧力団体化＝恩給は財政を食い、冷遇は街頭の不満になる（在郷軍人会）。`RetirementRules`（個人の退役）とは別＝集団の政治力
- [ ] StrikeRules：労働運動。賃金・待遇闘争が生産を止める＝弾圧か妥協か、組織率と景気で交渉力が振れる。`MarketRules`（生活水準）を read-only で参照可＝集団行動の解決

### 経済
- [ ] ReserveCurrencyRules：基軸通貨特権。自国通貨が決済標準だと赤字を刷って埋められる（法外な特権）＝信認が崩れた日に全部返ってくる。`FiscalRules`（為替係数）とは別＝通貨覇権の力学
- [ ] RationingRules：配給制。戦時の物資配給＝公平感が士気を支え、不公平の露見は闇市と不満を肥やす。`BlackMarketRules`（統制の影）と対になる政策側
- [ ] WarIndustryRules：軍産複合体。軍需が利益集団化して講和に抵抗する＝戦争の長期化が「合理的」になる構造。`StockMarketRules`（企業一般）とは別＝戦争利得のロビー力学

### 戦略・探査
- [ ] ExplorationRules：未知宙域探査（G-2 #119 のCore部分）。未探索星系の発見進捗・探査艦の能力・発見roll＝`ColonizationRules.CanColonize` が引数で受けている「探索済み」の供給源。偵察艦の固有機能の戦略版

### 戦域・環境（第3次追加）
- [ ] SpaceWeatherRules：宙域気象。恒星嵐・重力波バーストが回廊を一時封鎖し通信を途絶させる動的イベント。`TerrainRules`（恒常の地形）とは別＝時限の環境イベント
- [ ] RelicRules：遺失技術。地球時代・前文明の遺産発掘＝一点物の技術ブースト、独占と公開の選択。`ResearchRules`（自前研究）・`DisclosureRules`（物語の開示）とは別＝発掘の利得
- [ ] DefenseLineRules：縦深防御線。複数陣地の防衛線＝前縁突破後の浸透と予備隊の反撃、一点突破か広正面か。`Fortress`（単一要塞）とは別＝線と縦深の防御
- [ ] PrivateerRules：私掠免許。民間武装を国家が公認して敵通商を襲わせる＝安価な戦力だが統制が利かず戦後は海賊化する。`PiracyRules`（無主の暴力）の制度化＝対になる公認側
- [ ] MedicalRules：救護・衛生。救護能力が損耗の「死亡/治療後復帰」比を変える＝衛生への投資は見えない兵力。`RepairRules`（艦の修理）の人員版＝別系統

### 統治・組織（第3次追加）
- [ ] BureaucracyBloatRules：官僚制肥大（パーキンソンの法則）。定員は仕事量と無関係に自己増殖し、管理コストが実務を圧迫する＝定期的な行革の必要。`MinistryRules`（編制ツリー）とは別＝人数の動態
- [ ] SecretSocietyRules：秘密結社（地球教型）。制度の裏で要職へ浸透する隠れた網＝発覚まで見えず、摘発は浸透度の一部しか剥がせない。`ReligionRules`（公然の宗教）・`EspionageRules`（国家の諜報）とは別＝非国家の隠密網
- [ ] FreePressRules：報道の自由。自由な報道は腐敗・失政を早期に露見させる（政権には痛いが体制には薬）＝統制は短期の静穏と長期の腐敗蓄積。`PropagandaRules`（発信側）とは別＝監視者の自由度
- [ ] LobbyRules：圧力団体。業界・地域・団体の陳情が政策を歪める＝個別最適の集積が全体最適を壊す。`PartyRules`（政党）・`WarIndustryRules`（軍需ロビー＝特化版）とは別＝一般化されたロビー力学
- [ ] PreferenceFalsificationRules：選好偽装（クーラン型）。抑圧下では本音が隠れ、世論調査も体制も「見かけの支持」しか見えない＝革命が突然に見える理由。`ConsentRules`（実際の協力）とは別＝表明と本音の乖離

### 経済・社会（第3次追加）
- [ ] PriceControlRules：価格統制。統制価格は紙の上の安さ＝品不足と行列を生み、闇価格との乖離が統制の失敗度を測る。`RationingRules`（量の配分）と対になる価格の統制
- [ ] LandReformRules：土地改革。地主の土地を小作へ再分配＝生産意欲と支持を買い、地主層の反発と短期混乱を払う。`SerfdomRules`（身分の解放）とは別＝資産の再分配
- [ ] FrontierRules：辺境気質。中央から遠い星系ほど自立の気風が育ち、統制は薄く独立志向が強い。`LogisticsRules`（物理的連結）とは別＝距離の文化的効果
- [ ] AsabiyyaRules：アサビーヤ（ハルドゥーン型）。建国世代の紐帯は繁栄の中で世代ごとに薄れ、爛熟した中枢は辺境の新興勢力に取って代わられる＝王朝の自然寿命。`DynastyRules`（天命・腐敗）とは別＝集団紐帯の世代減衰

### 覇権・体制（第3次追加）
- [ ] HegemonyRules：覇権移行（トゥキディデスの罠）。台頭国と覇権国の力の交差が開戦確率を最大化する＝追い越しの瞬間が最も危ない。`ArmsRaceRules`（軍拡の螺旋）とは別＝構造的な力の遷移
- [ ] OverextensionRules：過剰拡張（ポール・ケネディ型）。版図と公約が国力を超えると守るものが増えるほど弱くなる＝戦略的収縮の決断。`LogisticsRules`（一体化度）とは別＝負担と国力の比
- [ ] BurdenSharingRules：同盟の負担分担。集団防衛のただ乗り問題＝盟主が背負うほど他が払わない、強制すれば同盟が軋む。`TreatyRules`（条約）・`LoyaltyRules`（会戦の静観）とは別＝平時の費用分担
- [ ] CollectiveSecurityRules：集団安全保障（国際連盟型）。侵略者への全員制裁の建前＝個々の損得で参加が崩れる失敗モデル。`DiplomacyRules`（二国間）とは別＝多国間の約束の脆さ
- [ ] PartitionRules：戦後分割。敗戦国の領土を勝者間で分配＝取り分の不満が次の対立軸になり、分割線が将来の火種。`TreatyRules`（条約一般）とは別＝分配の力学
- [ ] PraetorianRules：親衛隊の両刃。君主直属の精鋭は守護者にして簒奪者＝厚遇するほど政治力を持ち、冷遇すれば守りが薄い。`CivilianControlRules`（軍全体の統制）とは別＝近衛の特殊問題
