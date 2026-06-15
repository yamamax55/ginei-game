# 太閤立志伝V 参考設計（EPIC #TKO）— 軍人立志伝レイヤー

> 参照元：『太閤立志伝V』（コーエーテクモ）。戦国の自走する箱庭世界を、**一人の人物の視点から生きる**ライフシム。
> 武士・浪人・商人・忍・医者…と職業を自由に渡り歩き、無名から立身出世する「なりきり」の代表作。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略。すでに巨大な世界シム純ロジック層を保有）に、
> **「士官学校から始まる軍人立志伝」＝一人称キャラRPG層**として効く視点だけを抽出し、EPIC `#TKO` として issue 化する提案。
> 着眼点（ユーザー指定）：**幼年学校→士官学校→大学校から始まり、卒業・任官・配属を経て、武勲・人脈・稟議の実績で階級を駆け上がる。**
> 世界シム（経済/政治/外交/戦争）は自走し、主人公は一個の軍人として置かれる（視点固定）。職業は軍人路線に絞る。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**ライフシム／立身出世のメカニクス・構造パターンのみ**を参考にする。

---

## 0. なぜ「太閤立志伝V」が本システムに役立つか

当プロジェクトは**人物のキャリアを「マクロ・群体」として回す純ロジックを大量に保有**している（[CLAUDE.md]）：

| 既存（マクロ・群体としての人材シム） | カバー範囲 |
|---|---|
| `MilitaryAcademyRules`/`OfficerAcademyRules`/`SchoolAgeRules`/`SchoolPostingRules` | 幼年→士官→大学校の多段選抜・修業年・学校配属ゲート（POPを選抜→任官） |
| `CareerPipelineRules`（LIFE-5/6/7） | 出自経路（士官学校/科挙/有力者/技術者）でネームド化・**学閥/同期**の結束 |
| `SeniorityRules` | 席次→序列・merit・年功（昇進の母体） |
| `WarCollegeCareerRules` | 大学校エリート街道（入校/卒業/恩賜の軍刀/昇進） |
| `RankSystem`/`CommandCapacityRules` | 階級ラダーと「指揮できる規模」 |
| `PatronageRules` | 猟官・恩顧（縁故 vs 実力の忠誠/能力トレードオフ） |
| `ProtagonistRules`＋`AdmiralData.isProtagonist`（GON-6） | 主人公＝AI非制御の固定提督（視点固定の核） |
| 列伝#784/殿堂#785・人材管理HCM #992 | 人物名簿・後継・評価のマクロ統合 |

**しかしこれらはすべて「神の視点で人材プールを群体として回す」マクロシム**であり、太閤立志伝Vが固有に描く以下が**欠けている**：

| 太閤立志伝V が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **一人称の視点固定**＝あなたは国家でなく「一人の人物」。世界は自分を中心に回らない | god-view の戦略SLG。`ProtagonistRules` は「AI非制御の提督」止まりで、**主人公の人生を生きるシェル**が無い |
| **無名からの立身出世＝一代記**（入校→初陣→叙勲→昇進→晩年） | キャリアイベントは `WarCollegeCareerKind` 等で群体処理。**主人公個人の生涯を編む一代記**が無い |
| **個人の武勲が出世を駆動**（自分の手柄で身を立てる） | 昇進は `SeniorityRules` の席次・年功・merit のマクロ。**主人公自身の行動（会戦戦果/建白）→功績→昇進**の個人回路が無い |
| **人脈・師弟・恩義・遺恨の二者関係**（誰に仕え誰と縁を結ぶか） | `CareerPipelineRules` は学閥/同期の**集団結束**。**人物対人物の二者関係グラフ**（上官/同期/部下/恩義/遺恨）が無い |
| **一人称の動詞**（具申する・配属を願う・転任を希望する） | 稟議は `RingiDirector` が**勝手に発生**。主人公が**自分で起案/具申/裁可する動詞**が無い（観測層 `RingiObserverOverlay` は見るだけ） |
| **自由意志の岐路**（仕える・去る・寝返る・旗揚げ） | `CivilWarRules`/`BattleAllegianceRules` はマクロ。**主人公個人の下野/亡命/独立の選択**が無い |
| **執務机の視点UI**（自分の階級・配属・人脈・受信した命令を一望） | 観測オーバーレイは勢力横断のマクロ。**「主人公一人」にフィルタした一人称シェル**が無い |

**結論**：太閤立志伝Vは、本作の巨大なマクロ人材シムに**「地上の一軍人の顔」＝一人称キャラRPG層**を与える。
欠落軸は **①視点固定の一人称シェル ②個人武勲→昇進の出世回路 ③人物二者関係グラフ（人脈/師弟/恩義） ④一人称の動詞（具申/配属希望） ⑤一代記クロニクル ⑥岐路（下野/亡命/独立）** の6本。
**既存の学校・席次・学閥・階級は作り直さず接続するだけ**（additive）＝群体シムの上に「一人を生きる」薄い層を載せる。

---

## 1. 役に立つ視点（要約）

太閤立志伝Vの設計を、**本システムに効く形**で1行ずつ：

1. **世界はあなたを中心に回らない＝自走する箱庭を一人称で体験する**。→ 本作の自走する世界シム（経済/政治/戦争）は完成済み。**視点を god から一軍人へ落とす**だけで「なりきり」が立ち上がる。`ProtagonistRules` の延長。
2. **無名から身を立てる＝立身出世が物語の背骨**。→ 銀英伝そのものが下級士官の昇進譚（ラインハルト/ヤン）。**士官学校入学を起点とする一代記**が原作と完全に共鳴。
3. **自分の手柄で出世する＝武勲が階級を上げる**。→ 会戦#106 の戦果・稟議の建白実績を**主人公個人の功績**として積み、`SeniorityRules`/`RankSystem` の昇進に効かせる。
4. **誰に仕え誰と縁を結ぶかが運命を決める＝人脈・師弟・恩義**。→ `CareerPipelineRules` の学閥/同期を**二者関係グラフ**へ展開し、推挙・引き立て・遺恨の人事力学を生む。
5. **プレイヤーは動詞を持つ＝具申し、願い出る**。→ 今ある稟議（起案者→決裁者）を**操作化**し、下級士官は上官へ建白し、昇進すれば裁可する側に回る。観測層 #RingiObserver の能動化。
6. **生き方は一つでない＝仕える/去る/寝返る/旗揚げ**。→ `CivilWarRules`/`BattleAllegianceRules`/`DiplomacyRules` を**主人公個人の岐路**として束ね、忠臣にも梟雄にもなれる自由意志。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**学校選抜（`MilitaryAcademyRules`/`SchoolAgeRules`）・席次年功（`SeniorityRules`）・学閥（`CareerPipelineRules`）・階級（`RankSystem`）・会戦（Battle）を作り直さない**。
> TKO はそれらの上に**「主人公一人を生きる」一人称層を足し、接続する**だけ（additive）。職業ミニゲームは展開しない（軍人路線に絞る＝タイクン化回避）。

### ★★★ 最優先（真の欠落・太閤立志伝Vの signature）

#### TKO 主人公の士官学校入学〜任官〜配属（一代記の起点）
- 主人公1名（`isProtagonist`）を `MilitaryAcademyRules`/`SchoolAgeRules` の選抜パイプに**生徒として混ぜる**。入校→多段選抜→卒業→初任官（初期 `rankTier`）→艦隊/部署へ配属まで、**個人の起点**を一本の流れにする。
- 接続：`MilitaryAcademyRules`/`SchoolAgeRules`/`SchoolPostingRules`/`WarCollegeCareerRules`/`ProtagonistRules`/`FleetRoster`。新ロジックは「主人公を選抜に注入し、結果を個人イベントへ束ねる」`ProtagonistCareerRules`（純ロジック・test-first）。**群体選抜は流用、個人軌跡だけ新設**。

#### TKO 個人武勲→昇進の出世回路（自分の手柄で身を立てる）
- 主人公個人の**功績ポイント**（会戦戦果＝撃沈/旗艦撃破/防衛達成・稟議の採用建白・任務達成）を蓄積し、**昇進判定**（`SeniorityRules` 席次＋`RankSystem.NextRankTier`）に効かせる。年功だけでなく**手柄で抜擢**される回路。
- 接続：新 `MeritRecordRules`（個人功績の積分・純ロジック）×`BattleManager`（戦果ソース）×`SeniorityRules`/`RankSystem`（昇進）×`CommandCapacityRules`（昇進で指揮規模拡大）。マクロ昇進を**後退させず個人ボーナス**を足す。

#### TKO 人物二者関係グラフ（人脈・師弟・恩義・遺恨）
- ネームド人物どうしの**二者関係**（上官↔部下・同期・師弟・恩義・遺恨・親愛）を持たせる。`CareerPipelineRules` の学閥/同期（集団）を**ペア関係**へ展開し、推挙・引き立て・讒言・離反の人事力学を生む。
- 接続：新 `PersonRelationRules`/`PersonRelation`（純ロジック・有界＝N²回避でホット間引き）×`CareerPipelineRules`（学閥/同期を初期エッジに）×`PatronageRules`（恩顧）×`PersonRingiRules`（誰に建白するか）。**派閥システム#113 は作り直さず二者関係を下に敷く**。

### ★★ 高（一人称の動詞と人事の主体性）

#### TKO 一人称の動詞＝稟議の起案・具申（観測層の能動化）
- 主人公が**自分で建白を起案**し上官（`addresseeId`）へ具申する／昇進して決裁権を持てば部下の稟議を**裁可する側**に回る。今日追加した `RingiObserverOverlay`（見るだけ）を**操作化**する第2層。
- 接続：`PersonRingiRules`（起案/決裁の資格ゲート＝既存）×`DecisionDeck`（裁可UI）×`RingiObserverOverlay`（一覧）。**稟議の状態機械は再実装せず動詞だけ足す**。

#### TKO 配属・転任の希望と内示（キャリアの主体性）
- 主人公が**配属先を希望**（前線/参謀/後方）でき、人事（`SchoolPostingRules`/`OfficeRules`）が階級・席次・人脈で**内示**する。希望が通るかは関係グラフ（TKO-3）と功績（TKO-2）次第。
- 接続：`FleetRoster`/`SchoolPostingRules`/`OfficeRules`×TKO-2/TKO-3。新 `AssignmentRequestRules`（希望→内示の純ロジック）。

#### TKO 君主からの主命（拝命→遂行→達成/失敗）★★★
- 君主（`Person.isSovereign`）が主人公（臣下）へ**主命**（出陣/防衛/占領/鎮圧/巡察/練兵）を期限つきで下す。期限内の達成で**武勲**（TKO-2）と**恩義/引き立て**（TKO-3）、失敗で**遺恨**。太閤立志伝V/信長の野望の「主命」＝立身出世の駆動イベント。期限は通算月（int）で持ち暦内部に依存しない。
- 接続：新 `SovereignMandateRules`/`SovereignMandate`/`MandateKind`/`MandateStatus`。武勲は `MeritRecordRules`（達成＝`ExploitKind.任務達成`）、人脈は `PersonRelationRules`（恩義/親愛/遺恨）へ委譲。**並行系を作らない**。

#### TKO 月次評定（立身出世ループを毎月束ねる）
- 暦の**月境界**で評定を開き、(1)期限超過の主命を失敗確定 (2)保留中の武勲昇進を最大 N 段だけ確定（ペース制御） (3)未決の主命が無ければ確率で新主命を発令、を一括処理する。太閤立志伝Vの「定期的に主君に評価され命を受ける」リズム。
- 接続：新 `MonthlyCouncilRules`/`CouncilOutcome`（オーケストレータ＝数式を持たず委譲）。`SovereignMandateRules`/`MeritRecordRules` へ委譲。`CalendarDispatcher` の onMonth フックで Game 層（`GalaxyView`）が `Hold` を呼ぶ。考課（`MeritEvaluationRules`＝文官/位階の九等評定）とは別軸。

#### TKO 一代記クロニクル（主人公の生涯の編纂）
- 主人公の生涯イベント（入校・初陣・叙勲＝恩賜の軍刀・昇進・婚姻・武勲・死）を**時系列で一本に編む**個人年代記。列伝#784 を**一人にフォーカスした自伝**として生成。
- 接続：`NotificationCenter`（イベント源）×列伝#784/殿堂#785×`ChronicleObserverOverlay`。**コード新設は最小**＝主人公イベントの収集と整形のみ（`ProtagonistCareerRules` の出力を束ねる）。

### ★ 中（自由意志の岐路・一人称UIシェル・lore）

#### TKO 岐路＝下野・亡命・独立（忠臣にも梟雄にも）
- 主人公個人の**重大な岐路**：現組織に仕え続ける／下野（軍を辞す）／亡命（敵勢力へ）／旗揚げ（独立勢力）。後段フェーズ＝まず仕える路線を固めてから。
- 接続：`BattleAllegianceRules`（寝返り）×`CivilWarRules`（内戦/独立）×`DiplomacyRules`（亡命受入）×`LoyaltyRules`。**マクロ離反ロジックを主人公個人の選択肢として束ねる**だけ。

#### TKO 一人称UIシェル（主人公の執務机の視点）
- god-view から「主人公の執務机」へ切替えるUIシェル：自分の**階級・配属・人脈・武勲・受信した命令/稟議・一代記**を一望。既存の観測オーバーレイ群を**主人公一人にフィルタ**して束ねる Game 層UI。
- 接続：`RingiObserverOverlay`/`PersonObserverOverlay`/`MilitaryObserverOverlay` 等を主人公フィルタで再利用。**新規オブザーバを増やさず束ねる**（WindowChrome/UIWindowStack の作法）。

#### TKO（lore）立身出世の開示データ
- 「世界はあなたを中心に回らない」「無名から身を立てる」「仕える者と去る者」。トルストイ#（英雄史観の解体）・チ。#1254（主人公不在）と対の**「それでも一人を生きる」**視点。
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への lore データ入力。

### ❌ 不採用（重複・既存で十分・路線外）

| 不採用 | 理由 |
|---|---|
| 学校選抜・修業年・卒業判定の新規実装 | **`MilitaryAcademyRules`/`OfficerAcademyRules`/`SchoolAgeRules`/`SchoolPostingRules` がカバー**。TKO-1 は注入・接続のみ |
| 席次・年功・merit 昇進ロジックの新規実装 | **`SeniorityRules`/`RankSystem` がカバー**。TKO-2 は個人功績ボーナスを足すだけ |
| 学閥・同期・派閥システムの新規実装 | **`CareerPipelineRules`/#113 内部勢力 がカバー**。TKO-3 は二者関係を下に敷くだけ |
| 猟官・恩顧の新規実装 | **`PatronageRules` がカバー**。TKO-3 が接続 |
| 稟議の状態機械・伝播・執行 | **`RingiPipeline`/`WorkflowRules`/`PetitionFlowRules` がカバー**。TKO-4 は動詞だけ足す |
| 職業ミニゲーム（鍛冶/茶/医術/商い等の太閤V固有遊戯） | **軍人路線に絞る**（ユーザー指定）＝タイクン化回避。実装しない |
| 自由職業（商人/技術者/忍 等への転職） | 軍人立志伝に絞る。商人は商社#1022・技術者はテクノクラート`CareerPipelineRules` が群体で既存 |
| 複数主人公・人物乗り換え | まず**視点固定**（`ProtagonistRules`）に絞る。乗り換えは後段（UI/視点管理コスト大） |
| 会戦そのもの・陣形・戦術 | **Battle シーン／#72/#104/#106 がカバー**。TKO は一指揮官として参加するだけ |
| カリスマの日常化・英雄死後の組織存続 | **#812 `Organization` がカバー**。一代記の死は接続のみ |

---

## 3. EPIC #TKO の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。既存の学校/席次/学閥/階級は**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**ライフシム/立身出世の構造のみ**参考。職業は軍人路線に絞る。

> **EPIC = #2477**。GitHub issue 起票済み（#2478〜#2485）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **TKO-1** | #2478 | 主人公の士官学校入学〜任官〜配属（一代記の起点・`ProtagonistCareerRules`） | `MilitaryAcademyRules`/`SchoolAgeRules`/`SchoolPostingRules`/`ProtagonistRules`/`FleetRoster`。群体選抜に主人公を注入し個人軌跡を束ねる |
| **TKO-2** | #2479 | 個人武勲→昇進の出世回路（`MeritRecordRules`・手柄で抜擢） | `BattleManager`戦果×`SeniorityRules`/`RankSystem`/`CommandCapacityRules`。マクロ昇進に個人功績ボーナス |
| **TKO-3** | #2480 | 人物二者関係グラフ（人脈/師弟/恩義/遺恨・`PersonRelationRules`） | `CareerPipelineRules`学閥/同期×`PatronageRules`×`PersonRingiRules`。N²回避の有界グラフ |
| **TKO-4** | #2481 | 一人称の動詞＝稟議の起案・具申（`RingiObserverOverlay` の操作化） | `PersonRingiRules`×`DecisionDeck`×`RingiObserverOverlay`。状態機械は再実装せず動詞だけ |
| **TKO-5** | #2482 | 配属・転任の希望と内示（`AssignmentRequestRules`） | `FleetRoster`/`SchoolPostingRules`/`OfficeRules`×TKO-2/3。希望→内示 |
| **TKO-6** | #2483 | 一代記クロニクル（主人公の生涯イベントを時系列編纂） | `NotificationCenter`×列伝#784/殿堂#785×`ChronicleObserverOverlay`。収集/整形のみ |
| **TKO-7** | #2484 | 岐路＝下野・亡命・独立（主人公個人の選択・後段） | `BattleAllegianceRules`×`CivilWarRules`×`DiplomacyRules`×`LoyaltyRules` |
| **TKO-8** | #2485 | 一人称UIシェル（主人公の執務机の視点・既存オブザーバを主人公フィルタで束ねる） | `RingiObserverOverlay`/`PersonObserverOverlay`/`MilitaryObserverOverlay`＋WindowChrome/UIWindowStack |
| **TKO-9** | #2486 | 君主からの主命（`SovereignMandateRules`・拝命→遂行→達成/失敗→武勲・恩義） | `Person.isSovereign`×`MeritRecordRules`（達成＝任務達成）×`PersonRelationRules`（恩義/遺恨） |
| **TKO-10** | #2487 | 月次評定（`MonthlyCouncilRules`・主命の決裁/武勲昇進/新主命の発令を毎月束ねる） | `SovereignMandateRules`/`MeritRecordRules`×`CalendarDispatcher` onMonth |

### 推奨着手順
`TKO-1`（起点＝主人公を世界に置く・最も基盤）→ `TKO-2`（武勲→昇進＝立身出世の背骨）→ `TKO-3`（人脈＝人事力学）→ `TKO-9`（主命＝出世の駆動イベント）→ `TKO-10`（月次評定＝ループの心臓）→ `TKO-4`（具申＝一人称の動詞）→ `TKO-6`（一代記＝体験の編纂）→ `TKO-5`（配属希望）→ `TKO-8`（UIシェルで束ねる）→ `TKO-7`（岐路＝自由意志・後段）。
> 純ロジック backbone の実装済み：**TKO-1/2/3/5/9/10**（Core・test-first）。Game層配線（TKO-4/6/8）・後段（TKO-7）は残務。

> いずれも既存のマクロ人材シム（学校/席次/学閥/階級/稟議/会戦）を**後退させず接続**する additive 設計。
> 世界シムは自走したまま、その上に**「一人の軍人を生きる」薄い一人称層**を載せる＝銀英伝＝人物駆動の核心を強化する。
