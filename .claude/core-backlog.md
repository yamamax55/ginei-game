# Core量産バックログ（/core-wave が消化するテーマキュー）

> `/core-wave` 実行のたびに、未着手（`[ ]`）の上から**7件**を実装し、完了したら `[x]` に変えて
> Wave番号と日付を追記する。**キューが空になったら量産を停止して報告する**（無限生成しない）。
> 新テーマの追加は自由（既存モジュールと重複しないこと＝CLAUDE.md の一覧と照合する）。

## 済み（参考）
- [x] Wave3 (2026-06-10)：ReconRules / Reputation / PropagandaRules / BlockadeRules / Fortress / MercenaryRules / TerrainRules
- [x] Wave4 (2026-06-10)：ConscriptionRules / VeterancyRules / RepairRules / BoardingRules / MobilizationRules / RefugeeRules / DisciplineRules
- [x] Wave5 (2026-06-10)：PursuitRules / AssassinationRules / SanctionsRules / WarPoliticsRules / DisasterRules / EducationRules / ShipAgingRules

## キュー（上から順に消化）
- [ ] PiracyRules：宇宙海賊。治安（安定度）低下で交易路に海賊が湧き、討伐戦力を割くか被害を呑むか。`CommerceRaidingRules`（国家の通商破壊）とは別系統＝非国家アクター
- [ ] TradeRules：星間交易（フェザーン型）。勢力間の交易路が双方に利益、戦争で断絶、中立仲介者の利得。`MarketRules`（域内市場）とは別系統＝対外交易
- [ ] MartialLawRules：戒厳令。治安は即回復するが正統性・支持・希望を削る時限措置、長期化で副作用が本体を超える。`SecurityRules`（秘密警察）とは別系統＝公然の強権
- [ ] CeremonyRules：儀礼・戴冠・凱旋式。演出が正統性・士気を買う、財政コスト、敗勢下の空疎な式典は逆効果。`DynastyRules`（天命）を read-only で参照可
- [ ] GovernmentInExileRules：亡命政権。領土喪失後も正統性の残滓で抵抗を続けられる、承認国数・時間経過で減衰。`LogisticsRules`（版図一体化）とは別系統
- [ ] HonorsRules：勲章・栄典。叙勲が士気/忠誠を買うが、乱発するとインフレで価値が下がる。`MeritRankRules`（爵位＝実利）とは別系統＝名誉の通貨
- [ ] PrisonerExchangeRules：捕虜交換交渉。保有捕虜の数・質（階級）で交換レートが決まり、成立で双方の人材が還流。`CaptivityRules`（個別処遇）へ委譲し重複しない

### 軍事・戦術
- [ ] AmbushRules：伏兵・奇襲。探知に失敗した側が初撃ペナルティ（隊形不備・士気動揺）を受ける。`ReconRules`（探知そのもの）の結果を入力に取る別系統＝奇襲成立後の解決
- [ ] EncirclementRules：包囲。退路の遮断度で士気崩壊・降伏率・突囲損害が決まる（包囲殲滅戦）。`CaptivityRules.CaptureChance` は個人の捕虜化＝こちらは部隊規模の包囲解決
- [ ] FeintRules：陽動・欺瞞。偽目標へ敵戦力を引き付ける（吸引量）、見破られると逆に手薄を突かれる。`PropagandaRules`（世論向け）とは別＝軍事的欺瞞
- [ ] MinefieldRules：機雷原・宙雷。地帯拒否（通過損害・減速）、敷設密度と掃海の綱引き。`BlockadeRules`（艦隊封鎖）とは別＝無人の地帯拒否
- [ ] CarrierRules：艦載機（ワルキューレ）。航空打撃と防空網の綱引き、母艦撃破で艦載機が宿無し。`ShipClass`（戦艦/巡航/駆逐）に追加せず独立系統で
- [ ] CommunicationsRules：通信妨害・指揮遅延。距離・妨害で命令到達が遅れ、分断時は現場の自律性が頼り。`AutonomyRules`（ドクトリン）を read-only で参照可＝通信状態の解決のみ
- [ ] ElectronicWarfareRules：電子戦・ECM。能動妨害で敵の命中/探知を削る、対抗手段（ECCM）との競り。`TerrainRules`（受動的な環境）・`ReconRules`（自前センサー）とは別＝能動妨害
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
