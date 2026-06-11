# ジョミニ『戦争概論』参考設計（EPIC #JOM）

> 参照元：アントワーヌ＝アンリ・ジョミニ『戦争概論（Précis de l'Art de la Guerre）』。
> ナポレオン戦争を生きた戦略家が「戦争には幾何学的な普遍法則がある」と確信し体系化した、**作戦線・内線外線・決勝点**の軍事理論。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略）にとって**役に立つ視点**だけを抽出し、EPIC `#JOM` として issue 化する提案。
> 著作権注意：固有名・文章・固有設定は流用せず、**戦略メカニクス／幾何学的構造パターンのみ**を参考にする。

---

## 0. なぜ「ジョミニ『戦争概論』」が本システムに役立つか

当プロジェクトは軍事戦略の**哲学/政治レイヤー**をクラウゼヴィッツで、**欺瞞/諜報レイヤー**を孫子で既にカバーしている：

| 既存（カバー範囲） | issue |
|---|---|
| `FrictionRules`（作戦摩擦＝命令深度×補給×士気→実行成功確率） | CLZ-1 #1133 |
| `TrinitarianTensionRules`（政府意志×軍力×民支持の崩壊検知） | CLZ-3 #1135 |
| `CenterOfGravityRules`（CoG＝撃破すべき敵の重心を銀河グラフ上で同定） | CLZ-4 #1136 |
| `WarGoalRules`/`CasusBelli`/`WarWeariness`（厭戦→講和） | Wave2 純ロジック |
| `DeceptionRules`（欺瞞作戦） | SZT #1125系 |
| `GalaxyPathfinder`（Dijkstra経路探索） | 既実装 |
| `SupplyRules.SuppliedSystems`（補給接続・遮断） | 既実装 |
| `ZoneOfControl`（局地支配領域） | 既実装 |
| `LogisticsRules.CohesionFactor`（版図の連結成分） | 既実装 |

**しかし、これらは「何を攻めるか（CoG）」「なぜ戦争が起きるか（政戦）」「現地ではどう摩擦するか」という問いへの答えであり、ジョミニが固有に体系化した以下が欠けている：**

| ジョミニが固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **内線 vs 外線**（中央の勢力は複数の外縁脅威に内線で対処できる） | `LogisticsRules` は静的連結度。**現在の兵力配置が内線有利か外線不利かを動的に評価**する回路がない |
| **決勝点の地理的識別**（グラフの切断点・橋梁回廊を制することで戦域を支配する） | `CenterOfGravityRules`（CLZ-4）は**撃破すべき敵**を見つける。ジョミニの決勝点は**制すべき場所**（チョークポイント・ハブ星系）の特定 |
| **作戦線の方向性と脆弱性**（基地→目標の軸が敵に脅かされているか） | `SupplyRules` は到達可否。**特定の進軍軸が何箇所で脅かされているかの方向性分析**がない |
| **迂回機動・連絡線脅威**（敵の後方連絡線を迂回して基地/兵站を脅かす） | `FleetAI` は接近/交戦/撤退のみ。**戦略的迂回（敵の後方を突く経路計算）**がない |

**結論**：ジョミニは当プロジェクトの戦略AIと `GalaxyMap` に**「空間的文法」**——①内線外線評価、②決勝点識別、③作戦線脆弱性、④迂回機動——という4つの欠落軸を与える。CLZ-4重心の「何を狙うか」をJOMが「どこに立ち・どう動くか」で補完する構造。

---

## 1. 役に立つ視点（要約）

ジョミニの思想を**本システムに効く形**で1行ずつ：

1. **戦争には幾何学的な普遍法則がある** — 敵より先に決勝点を制し、内線で各個撃破する。→ `GalaxyMap` グラフ上で計算可能な**作戦AI**の骨格になる。
2. **内線優位は数的劣勢を補う** — 中央に位置する勢力は外縁を奔走する大勢力を各個撃破できる。→ 帝国vs同盟の「外縁線を回る遠征と中央突破」の対比を純ロジックで体現。
3. **決勝点は地図に読める** — グラフ理論で「ここを失うと経路が断たれる」星系/回廊を事前に計算できる。→ 切断点(articulation point)分析で**チョークポイントの価値を数値化**し、AI優先占領に直結。
4. **作戦線の健全性が遠征の生命線** — 基地から目標への軸上で何箇所が脅かされているかで進軍速度と損耗が変わる。→ `SupplyRules` に**方向性の脆弱度**を追加。
5. **迂回は最強の攻撃** — 敵の側背を突き連絡線を脅かす迂回機動は、正面突破より少ない損害で決定的効果を生む。→ `FleetAI` に**戦略的迂回経路計算**を与え、防衛軍の足を止める。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`GalaxyPathfinder`・`SupplyRules`・`ZoneOfControl`・`CenterOfGravityRules`（CLZ-4）は再実装しない**。JOM はそれらに**欠落軸を足して接続する**（additive）。

### ★★★ 最優先（ジョミニの signature・真の欠落）

#### JOM 内線優位評価（`InteriorLineRules`）

- **内線判定**：勢力Aが複数の敵対前線に対して「中央から各前線への最短経路コスト合計」が敵の「各前線間の経路コスト」より小さい場合、Aは内線優位。
- **外線ペナルティ**：外線勢力（多正面・周辺配置）は兵力の分散で実効戦闘力を下げる（`LogisticsRules.CohesionFactor` の方向性版）。
- 接続：`GalaxyMap` + `GalaxyPathfinder` + `StrategicFleetRegistry`。`CampaignRules.Tick` が毎ターン評価し `FactionState.Stability` へ修正子として流す。
- 純ロジック test-first。

#### JOM 決勝点識別（`DecisivePointRules`）

- **切断点（Articulation Point）分析**：銀河グラフから指定勢力の所有星系ネットワークを抽出し、除去するとネットワークが最も分断される頂点・橋梁回廊を特定する（標準的グラフアルゴリズム）。
- **決勝点スコア**：切断時の分断成分数×規模を指標化。スコア上位点が `FleetAI` の優先目標・`GalaxyView` の強調表示に直結。
- CLZ-4 `CenterOfGravityRules`（撃破すべき**敵の重心**）との相違：JOM-2 は**制すべき場所**（己の版図の要衝）。
- 接続：`GalaxyMap`。`StrategyRules`・`FleetAI` が参照。EditMode テスト必須。

### ★★ 高（作戦線・迂回機動）

#### JOM 作戦線脆弱性評価（`LineOfOperationsRules`）

- **作戦線定義**：`GalaxyPathfinder.FindPath` で求めた基地→目標の経路を「作戦線」とし、その各中継星系に対して敵が何系統から到達可能かを数える（脅威数カウント）。
- **脆弱度スコア**：中継ノードへの敵接触数の最大値。1を超えると作戦線が「危険」、`StrategyRules` が速度ペナルティを検討できる。
- `SupplyRules.SuppliedSystems`（到達可否）と直交：JOM-3 は**どの軸がどれだけ脅かされているか**の評価。
- EditMode テスト必須。

#### JOM 戦略的迂回機動（`TurningMovementRules`）

- **迂回経路計算**：敵の前線（`StrategyRules.IsFtlBlocked`）を迂回して敵の基地/作戦線中継点に到達する経路があるか探索（`avoidFrontlines=true` の迂回 Dijkstra）。
- **連絡線脅威スコア**：迂回成功時に脅かせる敵の中継点数を返す。`FleetAI` が撤退/接近より「迂回」状態を優先する条件に利用。
- 接続：`GalaxyPathfinder`（迂回フラグ拡張）×`StrategyRules`×`FleetAI`（新 AIState or 既存 `接近` の目標補正）。
- 純ロジック部分 EditMode テスト必須。

### ★ 中（世界観 lore）

#### JOM（lore）世界観の開示データ（幾何学的戦争観・普遍法則への意志）

- 「戦争は普遍幾何学に従う」「内線を確保せよ」「決勝点を先取せよ」という作戦哲学。
- クラウゼヴィッツ（不確実性重視・CLZ-5）と好対照なlore（確実性重視 vs 摩擦）。
- **コード新設なし**。`DisclosureLedger`（FND-4）への lore データ入力のみ。

### ❌ 不採用（重複・既存で十分・タイクン化回避）

| 不採用 | 理由 |
|---|---|
| 地形マイクロ（高地/平野/川渡りの個別補正） | タイクン化回避。`FrictionRules`（CLZ-1）で抽象化済み |
| 個別戦術フォーメーション（縦隊/横隊） | `FleetAI`/`FleetMovement` で十分。戦術バリエーションは既存の陣形システムが担う |
| 将軍の天才値（ジョミニ式 genius スコア） | `AdmiralData`（既存6能力）で代替。数値ゲーム化しない |
| 兵科比率（騎兵/歩兵/砲兵） | `ShipClass`（戦艦/巡航艦/駆逐艦）で代替済み |
| 攻勢終末点の再実装 | `CulminatingPointRules`（SZT#1125）が既に担う |
| 重心の再実装 | `CenterOfGravityRules`（CLZ-4 #1136）が担う。JOM は接続のみ |

---

## 3. EPIC #JOM の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ `FleetAI`/`GalaxyView` へ配線。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/幾何学的構造のみ**参考。

> **EPIC = #1341**。GitHub issue 起票済み（#1345/#1347/#1350/#1353/#1355）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **JOM-1** | #1345 | 内線優位評価（`InteriorLineRules`：中央配置→外縁前線への経路コスト比較→安定度修正子） | `GalaxyMap`×`GalaxyPathfinder`×`CampaignRules.Tick` |
| **JOM-2** | #1347 | 決勝点識別（`DecisivePointRules`：切断点解析→チョークポイントスコア→AI優先目標化） | `GalaxyMap`×`StrategyRules`×`FleetAI`。CLZ-4重心の地理的具体化 |
| **JOM-3** | #1350 | 作戦線脆弱性評価（`LineOfOperationsRules`：基地→目標の経路上の敵接触脅威カウント） | `GalaxyPathfinder`×`SupplyRules`（方向性拡張）×`FleetAI` |
| **JOM-4** | #1353 | 戦略的迂回機動（`TurningMovementRules`：前線迂回経路計算→連絡線脅威スコア→FleetAI状態） | `GalaxyPathfinder`（迂回フラグ）×`StrategyRules`×`FleetAI` |
| **JOM-5** | #1355 | （lore）世界観の開示データ（幾何学的戦争観・内線の法則・CLZ摩擦との対話） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順

`JOM-2`（決勝点識別＝切断点解析。最も固有でグラフ理論の核）→ `JOM-1`（内線評価＝経路コスト比較。JOM-2の基盤を流用）→ `JOM-3`（作戦線脆弱性＝SupplyRulesに方向性を追加）→ `JOM-4`（迂回機動＝FleetAIへの戦略的状態追加）→ `JOM-5`（lore）。

> いずれも既存の `GalaxyPathfinder`・`SupplyRules`・`CLZ-4` を**後退させず接続**する additive 設計。銀河規模での作戦AI強化に最も効く。
