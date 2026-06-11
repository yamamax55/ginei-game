# マクニール『戦争の世界史』参考設計（EPIC #MCN）

> 参照元：ウィリアム・H・マクニール『戦争の世界史——技術と軍隊と社会』（原題 *The Pursuit of Power: Technology, Armed Force, and Society since A.D. 1000*、1982）。
> 中世欧州から20世紀まで、**軍事技術の革新が国家・社会・経済をどう再編したか**を通史で描く。
> 本ドキュメントは当プロジェクト（Ginei）にとって**役に立つ視点だけ**を抽出し、EPIC `#MCN` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**軍事メカニクス／世界観の構造パターンのみ**を参考にする。

---

## 0. なぜ「戦争の世界史」が本システムに役立つか

当プロジェクトは軍事・経済の**マクロ純ロジックを大量に保有**している（[CLAUDE.md] 参照）：

| 既存（カバー範囲） | 担当モジュール |
|---|---|
| 造船・建艦・戦力補充 | `ShipyardRules` / `BuildOrder` / `FleetPool` (#884/#148) |
| 財政・国債・為替・税 | `FiscalRules` / `FiscalState` (#161/163) |
| 補給線（接続・遮断） | `SupplyRules` / `LogisticsRules` (#92/#844) |
| 補給基地・超線形消費・回廊容量 | `DepotRules` / `LogisticsBurdenRules` / `CorridorCapacityRules` (CRV #1361) |
| 過剰拡張比率・経済力→軍事転換ラグ | `OverstretchRules` / `PowerConversionRules` (KEN #1321) |
| 技術波動（内部研究） | `ResearchRules` / `TechWaveRules` (KEN #1321) |
| 文民統制・クーデター | `CivilianControlRules` (#145) |
| 省庁・予算分配 | `MinistryRules` / `Ministry` (#158) |
| 捕虜・傭兵的な兵力流用 | — |
| 人口・徴募 | `DemographicsRules` (#153) |

**しかし、マクニールが固有に描く以下の軸が欠落している**：

| マクニールが固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **軍事技術の国境越え拡散**（逆工学・スパイ・亡命技術者） | `ResearchRules` は単一勢力の内部R&D。**他勢力の技術を盗む・コピーする・買う**回路が無い |
| **暴力の商品化（傭兵制）**—中世コンドッティエーレから近代PMCまで | `FleetPool` は国家軍のみ。**民間軍事プールの雇用・ロイヤルティ・離反リスク**が無い |
| **技術世代差と陳腐化**—旧型艦は新型に一方的に撃ち負ける | `ResearchRules` は技術を進めるが**フリートが旧式化して戦闘力が落ちる**ペナルティが無い |
| **軍産複合体（兵器産業の政治圧力）** | `MinistryRules.SectionalismFriction` は縦割り省益だが**造船利権が軍拡を推進する**構造が無い |
| **軍事標準化（規格統一→補給乗数）** | `SupplyRules` は接続/遮断だが**同型艦の混在vs統一が補給効率を変える**モデルが無い |
| **命令型 vs 市場型の動員体制差** | `CivilianControlType` は文民統制だが**命令経済の急速動員 vs 市場経済の持続動員**の効率差が無い |

**結論**：マクニールは当プロジェクトの軍事・経済層に、①**技術拡散**（研究を盗む/買う）②**傭兵制**（暴力を商品化）③**陳腐化**（旧型艦ペナルティ）という3つの真の欠落軸と、④軍産複合体 ⑤標準化 ⑥動員体制差という3つの補強軸を与える。KEN/CRVと完全に直交し、「作戦 → 軍事組織 → 技術 → 経済」の因果連鎖を閉じる。

---

## 1. 役に立つ視点（要約）

マクニールの世界観を**本システムに効く形**で1行ずつ：

1. **軍事技術は国境を越える**——先進国の技術は模倣・購入・スパイで後発国に拡散し、技術優位は時間で縮まる。→ `ResearchRules` に**拡散モデル**を足し、技術封鎖の外交価値を与える。
2. **暴力は商品である**——傭兵（コンドッティエーレ/スイス歩兵/近代PMC）は市場で取引され、賃金を払えば忠誠、払えなければ裏切る。→ `FleetPool` に**民間軍事プール**の軸を追加。
3. **技術世代が陳腐化を生む**——砲艦の登場で木造帆船は即座に無価値になった。旧型を維持するコストと更新するコストのトレードオフ。→ `CombatModifiers` に**陳腐化ペナルティ**係数を接続。
4. **軍産複合体は軍拡にバイアスをかける**——工廠が閉まらないよう政治家を動かす。造船利権が戦略目標でなく生産目標を優先させる。→ `MinistryRules` の省益モデルに**軍需産業利権**を接続。
5. **標準化は第二の補給革命**——ミニエー弾もマウザー小銃も、規格統一で補給線が単純化した。艦種を揃えれば兵站が軽くなる。→ `SupplyRules` に**標準化ボーナス**を追加。
6. **命令経済は速く動員し、市場経済は長く戦える**——ソ連型の突出した動員速度と民主主義型の持続可能な生産力の非対称。→ `CivilianControlType` × `ShipyardRules` の政体別効率差。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`ShipyardRules`/`ResearchRules`/`SupplyRules`/`FiscalRules`/KEN系/CRV系を作り直さない**。MCN はそれらに**欠落軸を足し、接続するだけ**（additive）。

### ★★★ 最優先（マクニールの signature・真の欠落）

#### MCN 軍事技術拡散（`TechDiffusionRules`）
- **技術ギャップが大きいほど伝播しやすい**——他勢力より進んでいる技術ほど「欲しがられる」が、スパイ難度も上がる。`TechGap(a, b, field)` = (aのR&Dレベル − bのR&Dレベル)。
- `DiffusionChance(gap, espionageLevel, armsTradeAllowed)` — 拡散確率（gap大=伝播しやすい/espionage高=チャンス増/条約あれば合法購入）。
- `Diffuse(donor, recipient, field)` — 受領側の当該分野R&Dレベルを `DonorLevel × DiffusionRatio` まで引き上げ（donor を超えない）。
- **技術封鎖**（技術輸出禁止条約・`DiplomacyState`）で `DiffusionChance` を0に落とせる。→ 技術封鎖が外交カードになる。
- 接続：`ResearchRules` × `EspionageRules` × `DiplomacyState`（DIP-1 #189）。
- **EditMode テスト必須**（pure logic・test-first）。

#### MCN 傭兵制（`MercenaryRules`）—暴力の商品化
- `MercenaryUnit`（非 MonoBehaviour 純データ）：`strength` / `dailyCost` / `loyalty`(0..1) / `employer`（雇用勢力）/ `origin`（出自勢力）。
- `HireCost(strength, durationDays)` — 傭兵を雇うコスト（`FiscalState.PrimaryBalance` に直撃）。
- `LoyaltyDecay(unpaidTurns)` — 未払いが続くほど忠誠低下（→`Allegiance.intrigue` 上昇）。
- `DefectionRisk(loyalty, rivalOffer)` — 敵勢力が高額提示すれば裏切り（`LoyaltyRules.ApplyIntrigue` に流し込む）。
- `ForeignPenalty` — 出自と雇用勢力が違う傭兵は思想一致ボーナスがなくステータスが低め（`CombatModifiers` 係数）。
- `CapacityCeiling(fiscalState)` — 財政健全度が低いと雇える傭兵上限が下がる。
- 接続：`FleetPool`（傭兵も同じプールに乗る）× `FiscalRules` × `LoyaltyRules` / `Allegiance` (#817)。
- **EditMode テスト必須**。

#### MCN 艦隊技術世代と陳腐化（`ObsolescenceRules`）
- `TechGeneration(fleetUnit, researchState)` — 艦隊の技術世代（建造時/最後にアップグレードした時点のR&Dレベル）。
- `TechGapPenalty(fleetGen, currentMax)` — 現在の最先端との差が大きいほど戦闘修正が下がる（0..1・`CombatModifiers.Mul` で掛ける）。基準値非破壊。
- `UpgradeCost(strength, gap)` — 旧型艦を現役水準に更新するコスト（造船費の数割）。
- `IsObsolete(gap, threshold)` — 差が閾値を超えると「旧式」ラベル。HUD表示用。
- `RefitYield(upgradeInvestment, gap)` — 投資量に応じた世代回復量（完全更新でなく漸進も選べる）。
- 接続：`ResearchRules` × `CombatModifiers`(#106) × `ShipyardRules`。
- **EditMode テスト必須**。

### ★★ 高（軍事組織の政治経済学）

#### MCN 軍産複合体（`MilitaryIndustrialRules`）
- `LobbyingPressure(shipyardCount, institutionalInterest)` — 造船所数×省益(`MinistryRules.SectionalismFriction`)が政治圧力を生む。
- `ProductionSubsidy(pressure)` — 圧力が高いほど建艦予算に加算バイアス（`FiscalRules` 歳出を引き上げ）。
- `OverkillRisk(allocatedFleets, strategicOptimum)` — 戦略的必要を超えた過剰建艦。`OverstretchRules`(KEN) の「造船ルート」に入力し財政過剰伸張を誘発。
- `CorruptionGain(pressure)` — 高圧力下では調達コストが膨張（`FiscalRules.Expenditure` 増）。
- 接続：`MinistryRules`(GOV-5) × `FiscalRules` × `ShipyardRules` × `OverstretchRules`(KEN)。
- **EditMode テスト必須**。

#### MCN 軍事標準化（`StandardizationRules`）
- `StandardizationLevel(faction, fleetRoster)` — 同一艦種（`ShipClass`）の艦隊の割合（0..1）。艦種が揃うほど高い。
- `SupplyEfficiencyBonus(level)` — 補給消費を `(1 - level × maxBonus)` で軽減（`SupplyRules.TickFront` の消費量に係数）。タイクン化回避＝最大係数を const で封じる。
- `ProductionSpeedBonus(level)` — 同型を量産するほど `ShipyardRules.Tick` の生産力に加算。
- `NormalizationCost(currentHeterogeneity)` — 異種混在フリートを統一するための段階的更新コスト。
- 接続：`ShipyardRules` × `SupplyRules` × `FleetRoster`(#146)。
- **EditMode テスト必須**。

### ★ 中（政体×動員体制）

#### MCN 命令型 vs 市場型の動員体制（`MobilizationDoctrineRules`）
- `MobilizationRate(controlType)` — `CivilianControlType` から引いた動員速度係数：命令型（党軍/君主統帥）= 高速バースト / 市場型（文民統制）= 低速だが持続可能。
- `SurgeCapacity(controlType, duration)` — 短期にフル動員できる比率（命令型が高い）。`ShipyardRules.ProductionFactor` に乗算。
- `SustainabilityDecay(surgeRatio, turns)` — 全力動員が続くほど生産効率が落ちる（コマンド経済の限界）。
- `MarketEnduranceFactor(controlType, fiscalHealth)` — 市場型は財政健全度が高ければ長期維持が得意。
- 接続：`CivilianControlRules`(#145) × `ShipyardRules` × `FactionState`（統合層）。
- **EditMode テスト必須**。

### ❌ 不採用（重複・既存で十分・タイクン化回避）

| 不採用 | 理由 |
|---|---|
| 技術波動（内部R&D加速） | **KEN `TechWaveRules` (#1321) がカバー**。MCN は**拡散（他勢力から）**のみ |
| 過剰拡張・財政軍事乗数 | **KEN `OverstretchRules`/`PowerConversionRules` (#1321) がカバー**。MCN はそこへ入力するだけ |
| 補給基地・超線形消費・回廊容量 | **CRV (#1361) がカバー**。MCN-5 標準化は `SupplyRules` への係数のみ |
| 兵器の輸出市場・通商ネットワーク | FRM#1022・SAW の商社/裁定モデルが近い。新EPIC化しない |
| 詳細な徴兵制（年齢/兵種別） | `DemographicsRules`/`FleetPool` で十分。タイクン化回避 |
| 艦種ツリーの精緻化（戦闘艦→超弩級等） | `ShipClass` (#80) の拡張で対応可。MCN新EPICとしない |
| 技術の地政学的クラスター（欧州vs東アジア） | マクロ背景。`ResearchRules.IdeologyBias` で十分 |

---

## 3. EPIC #MCN の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UI へ配線。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #1373**。GitHub issue 起票済み（#1377〜#1395）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **MCN-1** | #1377 | 軍事技術拡散（`TechDiffusionRules`・勢力間のR&D伝播・技術封鎖） | `ResearchRules` × `EspionageRules` × `DiplomacyState` |
| **MCN-2** | #1381 | 傭兵制（`MercenaryRules`・暴力の商品化・忠誠と離反リスク） | `FleetPool` × `FiscalRules` × `LoyaltyRules` / `Allegiance` |
| **MCN-3** | #1385 | 艦隊陳腐化（`ObsolescenceRules`・技術世代差→戦闘力ペナルティ） | `ResearchRules` × `CombatModifiers` × `ShipyardRules` |
| **MCN-4** | #1389 | 軍産複合体（`MilitaryIndustrialRules`・造船利権の省益→過剰建艦） | `MinistryRules` × `FiscalRules` × `OverstretchRules`(KEN) |
| **MCN-5** | #1393 | 軍事標準化（`StandardizationRules`・同型艦の比率→補給効率乗数） | `ShipyardRules` × `SupplyRules` × `FleetRoster` |
| **MCN-6** | #1395 | 命令型 vs 市場型の動員体制（`MobilizationDoctrineRules`） | `CivilianControlRules` × `ShipyardRules` × `FactionState` |

### 推奨着手順

`MCN-1 → MCN-2`（技術拡散＋傭兵制＝マクニールの2大 signature）→ `MCN-3`（陳腐化＝技術差を戦闘に直結させる）→ `MCN-4`（軍産複合体＝政治経済圧力の追加）→ `MCN-5`（標準化＝補給効率への接続）→ `MCN-6`（命令型/市場型＝政体差の完結）。

> いずれも**KEN/CRV を後退させず接続する additive 設計**。造船・研究・兵站の既存モジュールに「技術拡散・傭兵・陳腐化」という顔を乗せる。
