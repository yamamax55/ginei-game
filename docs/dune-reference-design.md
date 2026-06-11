# ハーバート『デューン』参考設計（EPIC #DUN）

> 参照元：フランク・ハーバート著『デューン』シリーズ。砂漠惑星アラキスを舞台に、**唯一の航行物質（スパイス）の独占圧力・宗教×政治の大動員・救世主の誕生と危険性**を描く SF 叙事詩。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略）に**役立つ構造パターンのみ**を抽出し、EPIC `#DUN` として issue 化する提案。
> **著作権注意**：固有名（アラキス/スパイス/アトレイデス/ハルコンネン等）・文章・キャラクター・固有設定は流用しない。**メカニクス／世界観の構造パターンのみ**を参考。

---

## 0. なぜ『デューン』が本システムに役立つか

当プロジェクトはすでに広大な純ロジック層を持つ：

| 既存（カバー範囲） | モジュール |
|---|---|
| 資源生産・補給線・通商破壊 | `ResourceProductionRules`/`SupplyRules`/`CommerceRaidingRules`（L-1〜3 #92） |
| 宗教の改宗圧力・異端・聖戦圧力 | `ReligionRules`/`Religion`（#172-175） |
| 諜報・情報収集・破壊工作 | `EspionageRules`/`SpyNetwork` |
| カリスマ指導者→組織存続・継承 | `Organization`/`SuccessionRules`（#812/#814） |
| 封建的臣属・徴募・反乱 | `FeudalRules`/`Fief`（#168/#169） |
| 市場均衡・銀行・株 | `MarketRules`/`BankRules`/`StockMarketRules` |
| 王朝サイクル・天命・易姓革命 | `DynastyRules`/`Regime`（#867） |
| 外交状態・戦争目標 | `DiplomacyRules`/`WarGoalRules`（DIP-1/3） |
| 戦闘修正係数（実効値パターン） | `CombatModifiers`/`ModifierStack`（#106） |
| 惑星攻城・制空権・侵略 | `PlanetSiegeRules`/`Planet`（#131） |

**しかし、デューンが固有に描く以下の構造が欠けている**：

| デューンが固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **唯一の資源が全勢力の戦略機動を制約する（チョークポイント資源）** | `SupplyRules` は自勢力の補給。「これがなければ全勢力が動けない」という**インフラ独占圧力**が無い |
| **宗教的狂信が戦闘力を非線形に増幅する（聖戦乗数）** | `ReligionRules.HolyWarPressure` は他勢力への圧力。**自勢力の戦闘倍率**として機能しない |
| **諜報機関が事前に伝説を植え付け、後から回収する（人工預言）** | `EspionageRules` は現在情報の収集。**長期的な伝説植付け→社会工学的回収**の回路が無い |
| **惑星の生態系が長期戦略の資産／兵器となる（生態系状態）** | `Planet` は制空権と侵略進捗のみ。**生態系の状態が資源産出・防衛・戦略を変える**軸が無い |
| **フェイントがフェイントだと知られていることを知った上で行動する（多層欺瞞）** | `EspionageRules` は単層の情報優位。**カウンター欺瞞の再帰的積み重ね**が無い |
| **救世主的指導者が短期に圧倒的でも長期に組織を食いつくす（メシアのリスク）** | `SuccessionRules` は英雄死後の継承。**英雄が生きている間に進む制度的空洞化**が無い |

**結論**：デューンは当プロジェクトに①**チョークポイント経済**、②**宗教的狂信の戦闘変換**、③**社会工学としての預言植付け**、④**生態系戦略**、⑤**多層欺瞞**、⑥**メシアのリスク**という6つの欠落軸を提供する。既存の宗教/諜報/資源/惑星モジュールに**接続する additive 設計**。

---

## 1. 役に立つ視点（要約）

1. **「資源の独占＝銀河の支配」**——一種の物質だけが航行を可能にする。誰が産出星系を持つかで全勢力の戦略機動が決まる。→ 既存 `SupplyRules` に**チョークポイント圧力層**を足す。
2. **信仰の熱が戦いの熱になる**——教義ではなく体験的確信が戦士を「死を恐れない」状態に変える。宗教と軍事は別システムではなく**乗算**。→ `ReligionRules`×`CombatModifiers`（#106）に直結。
3. **伝説は埋めておき後で掘り出す**——諜報は現在の情報だけでなく、将来の社会を形成する**前払いの物語**でもある。→ `EspionageRules`×`ReligionRules`×`EventEngine` の新回路。
4. **惑星は変えられる——変えることが戦略になる**——生態系を変えることで資源産出・居住適性・防衛特性を数十年かけて操作する。→ `Planet`×`GovernanceRules`×`ResourceProductionRules` を接続。
5. **敵はこちらのフェイントを読んでいる——だからそれを利用する**——多層欺瞞は「相手が賢いこと」を前提にした戦略。→ `EspionageRules` の再帰的拡張。
6. **救世主は組織を救い、そして殺す**——カリスマが全を決めると機構が退化し、そのカリスマが去った瞬間に崩壊が始まる。英雄崇拝の逆説。→ `Organization`×`SuccessionRules`（#812）の危険側面。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**既存 `SupplyRules`/`ReligionRules`/`EspionageRules`/`Planet` を作り直さない**。DUN はそれらに**欠落軸を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・デューンの signature）

#### DUN チョークポイント資源独占（唯一の航行資源が戦略機動を支配する）
- **概念**：特定の `ResourceType` をチョークポイント指定すると、それが枯渇・独占された勢力は`StrategicFleet` の FTL 移動速度が制限される（`SupplyRules` の補給切れの上位版）。
- **独占圧力**：産出星系を一勢力が独占すると他勢力が外交・経済・軍事で「支払い」を強いられる。→ 交渉レバレッジの発生源。
- 接続：新 `StrategicChokePointRules`/`ChokePointState`（pure logic・test-first）→ `SupplyRules`×`LogisticsRules`×`DiplomacyRules`×`StrategicFleet`。

#### DUN 狂信的動員乗数（宗教的確信を戦闘力へ変換する）
- **概念**：`ReligionRules` の社会均衡とは別に、**ファナティシズム強度**（0〜1）が `CombatModifiers` の攻撃/防御/士気に乗算する。強度は `ReligionRules` の同化圧力・迫害・聖戦宣言で上昇し、統治腐敗や敗戦で下落。
- **非線形性**：高強度では強大だが、強度が急落したとき（神話の崩壊）に急速に士気崩壊→ `FleetMorale.IsRouted` と接続。
- 接続：新 `FanaticismRules`/`FanaticState`（pure logic・test-first）→ `ReligionRules`×`CombatModifiers`（#106）×`FleetMorale`。

### ★★ 高（欠落軸を埋め、既存に経路を開く）

#### DUN 人工預言・伝説植付け（諜報機関による社会工学）
- **概念**：`EspionageRules` の拡張として、**伝説植付けミッション**（`PlantedLegend`）を持つ。潜伏期間後に `EventEngine` のイベント条件として使用可能（「古くから伝わる予言が成就した」という認知を引き起こす）。
- **回収**：植えた勢力が意図的なイベントを発火すると `ConsentRules.Withdraw` の逆（協力急増）や `FanaticismRules` 強度の上昇として回収できる。
- 接続：新 `ManufacturedProphecyRules`/`PlantedLegend`（pure logic・test-first）→ `EspionageRules`×`ReligionRules`×`EventEngine`×`ConsentRules`。

#### DUN 生態系状態と惑星環境戦略（長期的な惑星改造）
- **概念**：`Planet` に **`EcologyState`**（習潤度/大気安定度/生態多様性）を付与。値によって `ResourceProductionRules` の出力係数・`GovernanceRules` の安定度・`ColonizationRules` の入植コストが変わる。
- **テラフォーミング**：長い時間をかけて `EcologyState` を改善する投資（即効性なし＝タイクン化回避）。逆に**環境破壊戦**（資源過剰採掘・生態破壊）で敵惑星の産出を低下させる。
- 接続：新 `EcologyState`/`TerraformingRules`（pure logic・test-first）→ `Planet`×`GovernanceRules`×`ResourceProductionRules`×`PlanetSiegeRules`。

#### DUN 多層欺瞞（フェイント内フェイント・カウンター謀略の積み重ね）
- **概念**：`EspionageRules` に**欺瞞ラベル付き工作**（`DeceptionLayer`）を追加。相手が欺瞞を「看破」するとそれ自体が想定内（第2層）として機能する再帰モデル。層数と各層の信憑性が最終的な相手の行動誤誘導率を決める。
- 接続：新 `DeceptionLayerRules`/`DeceptionStack`（pure logic・test-first）→ `EspionageRules`×`LoyaltyRules`×`DiplomacyRules`。

### ★ 中（世界観・lore 接続）

#### DUN 救世主のリスク（英雄崇拝が制度を空洞化する）
- **概念**：`Organization` / `SuccessionRules` の危険側面。カリスマ指導者（`AdmiralData.isProtagonist` または高 `leadership`）に過度な権限集中が続くと **`InstitutionalAtrophy`**（制度空洞化）が進む。指導者が去ったとき（死/退役/捕虜）に `SuccessionRules` の継承難易度が急上昇する。
- **均衡**：強い指導者が短期効果をもたらしつつも長期の脆弱性を蓄積する＝プレイヤーへの自覚的コスト。
- 接続：新 `MessiahRiskRules`（pure logic・test-first）→ `Organization`（#812）×`SuccessionRules`×`AutonomyRules`（#544-550）×`AdmiralData`。

#### DUN （lore）世界観の開示データ
- 「資源の独占が銀河を支配する」「預言は植えるもの」「救世主は解放と同時に滅びを運ぶ」。
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への**lore データ入力**。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 封建大院（家と家の対立、帝国と諸侯） | **`FeudalRules`/`Fief`（#168/#169）がカバー** |
| 宗教の聖戦宣言・改宗圧力 | **`ReligionRules.HolyWarPressure`/`ConversionPressure`（#172-175）がカバー** |
| ギルド的特権貿易（CHOAM型独占） | **`MarketRules`（#179-182）＋`EspionageRules` の組合せで対応可** |
| 認知専門家（メンタット=人間コンピュータ） | **`PersonRules`/`CareerPipelineRules`（テクノクラート系出自 LIFE-5/7）がカバー** |
| 基本的な予言・占術イベント | **`EventEngine`（#116）でサンプル追加のみで足りる。新EPIC化不要** |
| 遺伝的操作・優生プログラム | ゲームプレイへの接続が不明瞭＋タイクン化リスク。不採用 |
| 完全な惑星の生態工学シミュレーション | マイクロ操作増大でタイクン化。係数のみ（EcologyState で十分） |

---

## 3. EPIC #DUN の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UI へ配線。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #1264**。GitHub issue 起票済み（#1266〜#1280）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **DUN-1** | #1266 | チョークポイント資源独占（唯一の航行資源が全勢力の戦略機動を支配する） | 新 `StrategicChokePointRules`/`ChokePointState`。`SupplyRules`×`LogisticsRules`×`DiplomacyRules` |
| **DUN-2** | #1268 | 狂信的動員乗数（宗教的確信を戦闘力に変換する `FanaticismRules`） | 新 `FanaticismRules`/`FanaticState`。`ReligionRules`×`CombatModifiers`（#106）×`FleetMorale` |
| **DUN-3** | #1269 | 人工預言・伝説植付け（諜報による長期社会工学 `ManufacturedProphecyRules`） | `EspionageRules`×`ReligionRules`×`EventEngine`×`ConsentRules` |
| **DUN-4** | #1272 | 生態系状態と惑星環境戦略（`EcologyState`/`TerraformingRules`） | `Planet`×`GovernanceRules`×`ResourceProductionRules`×`PlanetSiegeRules` |
| **DUN-5** | #1276 | 多層欺瞞（フェイント内フェイント `DeceptionLayerRules`/`DeceptionStack`） | `EspionageRules`×`LoyaltyRules`×`DiplomacyRules` |
| **DUN-6** | #1280 | 救世主のリスク（英雄崇拝が制度を空洞化する `MessiahRiskRules`） | `Organization`（#812）×`SuccessionRules`×`AutonomyRules`（#544-550） |

### 推奨着手順
`DUN-1`（チョークポイント＝最も固有で戦略レイヤー直結）→ `DUN-2`（狂信動員＝宗教×戦闘の欠落軸）→ `DUN-3`（人工預言＝諜報の長期投資版）→ `DUN-4`（生態系＝惑星層の新軸）→ `DUN-5`（多層欺瞞＝既存諜報の深化）→ `DUN-6`（救世主リスク＝制度の長期危機）。

> いずれも既存システムを**後退させず接続**する additive 設計。戦略レイヤー（チョークポイント経済）・宗教×戦闘（動員乗数）・諜報（社会工学）の三方向から銀河国家戦略に奥行きを与える。
