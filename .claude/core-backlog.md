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

## キュー（上から順に消化）

### 軍事・戦術
- [ ] DesertionRules：脱走。長期戦・低士気・補給切れで兵が静かに消える（戦闘によらない損耗）。`MercenaryRules`（金銭離反）・`DisciplineRules`（抗命）とは別＝無言の退出
- [ ] SalvageRules：戦場回収。会戦後の残骸・漂流艦から戦力/資源を回収（勝者が戦場を制する利得）。`BoardingRules.PrizeValue`（拿捕）とは別＝戦闘後の回収
- [ ] OperationPlanRules：作戦立案。参謀の `operation` 能力（現状未使用）から作戦計画の質→会戦初期条件（配置・補給・予備隊）の優劣を出す。`CommandStaffRules`（配置・能力補完）へ委譲し重複しない

### 戦争犯罪・諜報
- [ ] AtrocityRules：虐殺・軌道爆撃（ヴェスターラント型）。実行/黙認の選択が支持・正統性・敵のプロパガンダ素材へ波及＝短期の戦果と引き換えの長期の汚点。`PropagandaRules` を read-only で参照可
- [ ] CounterIntelligenceRules：防諜。敵スパイ網の摘発・二重スパイ・偽情報の流し込み（敵の意思決定を汚染）。`EspionageRules`（自分の諜報）とは別＝守りと毒
- [ ] CodebreakingRules：暗号解読。通信傍受の蓄積で敵企図の先読み確率が上がる、暗号更新でリセット。`ReconRules`（物理探知）とは別＝信号情報
- [ ] ResistanceRules：占領地レジスタンス。未統合の占領地で破壊工作・情報漏れが起き、弾圧か懐柔かの統治コストを迫る。`GovernanceRules`（安定度収束）を read-only で参照可＝抵抗活動の解決
- [ ] TerrorRules：テロ（地球教型）。民間目標への攻撃が恐怖と過剰反応（弾圧→支持低下）を誘う＝テロの本当の武器は報復の自滅。`AssassinationRules`（要人狙い）とは別＝無差別と恐怖

### 政治・社会
- [ ] RegencyRules：摂政・幼君（エルウィン・ヨーゼフ型）。幼君の正統性は時間で痩せ、摂政の実権と野心が育つ。`PowerRules`（傀儡一般）とは別＝未成年継承の特殊力学
- [ ] PlebisciteRules：住民投票（バーラト和約型）。領土帰属・自治を票で決める、組織動員と監視の公正度で結果が振れる。`PartyRules`（選挙一般）とは別＝一回性の領土・体制投票
- [ ] AmnestyRules：恩赦。内戦・反乱後の和解＝処罰（正義）と統合（実利）のトレードオフ、許しすぎは再発を招く。`CaptivityRules`（個別処遇）とは別＝集団和解
- [ ] PurgeRules：粛清（リップシュタット後/スターリン型）。敵対派閥の一掃で統制は上がるが人材プールが毀損し恐怖が萎縮を生む。`CoupRules`（粛清結末）とは別＝政策としての粛清
- [ ] ConfiscationRules：財産没収（門閥解体型）。既得権の資産を国庫へ＝財政は潤うが資産家層の忠誠が崩れ亡命・抵抗を生む。`RedistributionRules`（税による再分配）とは別＝一回性の収奪
- [ ] DemagogueRules：扇動政治家（トリューニヒト型）。責任回避と敵作りで支持を集め、危機では姿を消して生き残る＝有能な無能の生存力。`PartyRules`（党勢）を read-only で参照可
- [ ] MartyrdomRules：殉教の政治（ハイネセン/キルヒアイス型）。英雄の死は生前より強い動員力を持ち、遺志の独占解釈が後継の正統性を決める。`ReputationRules`（生者の名声）とは別＝死者の力
- [ ] CivilWarRules：内戦（リップシュタット型）。国内が二分＝経済崩壊・対外無防備・勝者総取り。`CoupRules`（クーデター＝短期決着）とは別＝長期の分裂戦争
- [ ] HostageRules：人質外交。要人の身柄が交渉材料になる（価値＝階級・血縁）、処刑は交渉力と引き換えに外聞を失う。`CaptivityRules`（捕虜処遇）へ委譲し重複しない＝交渉の力学のみ
- [ ] MigrationRules：平時移民。経済格差・思想的自由を求めて人口が国境を越える＝頭脳流出/流入。`RefugeeRules`（戦火の強制移動）とは別＝自発的移動

### 経済
- [ ] InflationRules：戦時インフレ。通貨増発で戦費は賄えるが物価上昇→実質賃金低下→不満蓄積＝見えない税。`FiscalRules`（国債・金利）とは別＝通貨価値の劣化
- [ ] BlackMarketRules：闇市。統制経済（動員・配給）の裏で必ず湧く、取り締まりは資源を食い黙認は統制を骨抜きに。`MobilizationRules`（統制そのもの）とは別＝統制の影
- [ ] ReconstructionRules：戦後復興。荒廃地への投資が回復を早め復興需要が経済を押す、放置は荒廃の固定化。`ColonizationRules`（新規入植）とは別＝既存地の再建
- [ ] ReparationsRules：賠償金（ヴェルサイユの罠）。過酷な賠償は敗者の経済を殺し復讐主義を育てる＝勝者の取り分と次の戦争の種のトレードオフ。`WarGoalRules`（講和条件一般）とは別＝賠償の長期帰結
- [ ] MonopolyRules：独占・財閥。市場支配が価格を吊り上げ政治を買収する、解体は効率と引き換えに反発。`MarketRules`（競争市場）とは別＝市場の失敗
- [ ] MegaprojectRules：巨大事業（要塞建造型）。長期巨額の段階投資＝中断の埋没費用、完成すれば戦略を変える。`ShipyardRules`（艦の量産）とは別＝一点物の巨大建造
- [ ] InnovationDiffusionRules：技術伝播。先進技術は交易・諜報・模倣で漏れる＝技術独占は時限。`ResearchRules`（自前研究）とは別＝他国からの流入

### 戦略・人物
- [ ] BufferStateRules：緩衝国（フェザーン型）。両大国の間で等距離外交・経済的不可欠性で自立を保つ小国の生存戦略、均衡が崩れた瞬間に併呑される。`DiplomacyRules`（二国関係）とは別＝三体問題の生存術
- [ ] ChokepointValueRules：要衝価値。回廊の戦略価値（迂回路の有無・経済流量・前線距離）を点数化＝AI・自動配備の判断材料。`GalaxyPathfinder`（経路探索）を read-only で参照可
- [ ] SenescenceRules：名将の衰え。加齢で能力の実効値が峠を越えて下る（全盛期→緩やかな下り坂）、本人は気づきにくい。`LifecycleRules`（死亡）・`RetirementRules`（停年制度）とは別＝能力曲線
- [ ] RivalryRules：宿敵（ヤン vs ラインハルト型）。好敵手の存在が互いの成長・士気を引き上げ、宿敵の死は勝者から張りを奪う。`GrowthRules`（経験成長）とは別＝関係性ボーナス
- [ ] FriendshipRules：盟友（キルヒアイス/双璧型）。深い信頼の僚友は共同作戦にボーナス、喪失・反目は深い痛手。`CommandStaffRules`（職制上の補佐）とは別＝個人的紐帯
- [ ] MentorshipRules：師弟（メルカッツ型）。老練の師が後進の成長を加速し、師の死で独り立ちが試される。`GrowthRules`（成長曲線）へ係数を渡す別系統＝伝授
- [ ] HistoriographyRules：歴史叙述。勝者が歴史を書く＝人物の後世評価は政権の都合で改竄され、政権交代で再評価される。`ReputationRules`（存命中の名声）とは別＝死後の評価戦
- [ ] TerraformingRules：テラフォーミング。非居住可能星系への長期投資で `habitable` 化＝入植先を作る。`ColonizationRules`（居住可能星系への入植）の前段＝別系統

### 軍事・即応態勢
- [ ] ReadinessRules：即応態勢。警戒水準は維持費を食い、緩めると奇襲に弱い（休暇中の艦隊は出遅れる）。`VeterancyRules`（長期の熟練）とは別＝短期の警戒状態
- [ ] ForcedMarchRules：強行軍。到着を早める代わりに疲労蓄積＝到着直後の戦闘ペナルティ、無理を重ねると落伍。`StrategicFleet`（移動そのもの）は不変＝疲労係数の算出のみ
- [ ] MothballRules：予備役艦隊・モスボール。退蔵保管は維持費を大きく削るが、再就役に時間と整備費がかかる。`ShipAgingRules`（経年劣化）とは別＝保管状態の管理
- [ ] RaidRules：縦深襲撃。敵後方の補給拠点・造船所への一撃離脱＝占領せず破壊して戻る、深入りほど帰還リスク。`CommerceRaidingRules`（船団狩り）とは別＝固定目標への強襲
- [ ] ScorchedEarthRules：焦土戦術。撤退時に自領の資産を焼いて敵の利得を消す＝侵攻は鈍るが自国民の恨みと復興費が残る。`ReconstructionRules`（再建側）と対になる破壊側
- [ ] MutinyRules：艦隊反乱。部隊単位の集団的な命令拒否・艦の乗っ取り（待遇・思想・敗勢が引き金）。`DisciplineRules`（個別の抗命）・`CoupRules`（国家転覆）の中間スケール

### 抑止・外交
- [ ] EscalationRules：エスカレーション管理。偶発的な国境事件が梯子を昇って戦争に至る／降りる判断＝威信と実利の綱引き。`DiplomacyRules`（状態遷移）へ宣戦を委譲＝梯子の力学のみ
- [ ] DeterrenceRules：抑止。報復能力の顕示が開戦を思いとどまらせる＝能力×信憑性（脅しが信じられなければ無意味）。`ArmsRaceRules`（軍拡の螺旋）とは別＝開戦判断への写像
- [ ] ArmsRaceRules：軍拡競争。相手の建艦に建艦で応える安全保障のジレンマ＝双方が貧しくなりながら相対優位は変わらない。`ShipyardRules`（建艦そのもの）を read-only で参照可
- [ ] ArmsControlRules：軍縮条約（ワシントン体制型）。建艦上限・査察・秘密再軍備の発覚リスク＝信頼の制度化と裏切りの誘惑。`TreatyRules`（条約一般）とは別＝軍備の検証問題
- [ ] AppeasementRules：宥和政策。譲歩が平和を買うか侵略の食欲を育てるか＝相手の性格（現状維持/拡張主義）を見誤ると破滅（ミュンヘンの教訓）。`DiplomacyRules` の opinion とは別＝譲歩の学習効果
- [ ] InfluenceRules：勢力圏。直接領有せず経済・軍事顧問・政治介入で他国を影響下に置くグラデーション。`DiplomacyState`（属国＝形式）とは別＝非公式な浸透度
- [ ] DebtDiplomacyRules：債務外交。貸し込んだ債権が政治的レバレッジになる＝返せない借り手は港を差し出す。`BankRules`（信用創造）・`FiscalRules`（自国財政）とは別＝対外債権の武器化
- [ ] ForeignAidRules：対外援助。敵対勢力の災害救援・friendly な開発援助が opinion と勢力圏を買う＝善意と買収の二重底。`DisasterRules`（国内救援）とは別＝越境する援助
- [ ] TribunalRules：戦犯裁判。戦後の勝者の裁き＝正義の执行と報復の間、過酷なら遺恨・寛大なら不処罰の不満。`AtrocityRules`（罪そのもの）の後段＝裁きの政治

### 統治・制度
- [ ] CensusRules：国勢調査・統計精度。国家が自国を「見えて」いるか＝統計が粗いと徴税・徴募・政策が外れる（見えない国は治められない）。`DemographicsRules`（実際の人口）とは別＝認識とのズレ
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
