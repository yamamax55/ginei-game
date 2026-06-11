# リデルハート『戦略論』参考設計（EPIC #LDH）

> 参照元：B.H.リデルハート『戦略論（Strategy: The Indirect Approach）』（1954年初版／1967年改訂）。
> 古今東西の軍事作戦を分析し、**直接攻撃はほぼ失敗し、間接アプローチがほぼ成功する**という実証主義的結論を導く。
> 本ドキュメントは、当プロジェクト（銀英伝風の星間国家戦略＋既に大きな戦略純ロジック層）にとって
> **役に立つ視点だけを抽出**し、EPIC `#LDH` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**作戦理論の構造パターンのみ**を参考にする。

---

## 0. なぜ『戦略論』が本システムに役立つか

### 既存カバー範囲

| 既存モジュール | カバー内容 |
|---|---|
| `GalaxyPathfinder.FindPath` | Dijkstra 最短経路（コスト最小） |
| `FleetAI`（接近/交戦/撤退） | 敵への直線的接近・交戦・撤退AI |
| `ZoneOfControl`/`HostileIntensityAt` | ZOC 内での移動減速（局所的存在脅威） |
| `FleetMorale.IsRouted`・`FleetStrength.BeginRetreat` | 士気崩壊→退却（単一部隊） |
| `WarGoalRules`（CLZ #1132連動） | 厭戦・戦争目標・講和条件 |
| `StrategyRules.IsFtlBlocked` | 前線回廊の通行制限 |
| `CombatModifiers.FlankFactor` | 側背面攻撃ボーナス（方向依存） |
| `EspionageRules` | 諜報（情報収集・妨害） |
| CLZ #1132（クラウゼヴィッツ） | 摩擦・攻勢終末点・重心・政戦連接 |

### リデルハートが固有に持つ視点 × 当プロジェクトの欠落

| 『戦略論』の視点 | 当プロジェクトでの欠落 |
|---|---|
| **最小予期線**：敵が最も予期しない方向・経路から打つ | `GalaxyPathfinder` は「最短/最安」しか評価しない。**経路が敵の期待からどれだけ外れているか**の指標がない |
| **最小抵抗線**：物理的・心理的に最も薄い点を突く | `FleetAI` は敵兵力密度で経路を変えない（単純直進） |
| **心理的瓦解（Dislocation）**：複数方向からの同時脅威が意思決定を麻痺させ、物理的破壊より先に崩壊を引き起こす | `FleetMorale` は単一部隊の士気。**複数の脅威方向から同時に圧力を受ける**ことによる加速崩壊がない |
| **分散強要（Fleet in Being）**：攻撃せず「存在するだけ」で敵の兵力を特定地点に縛る | `ZoneOfControl` は局所的な移動減速。**戦略レベルで敵の自由な増援・再配置を阻む**存在脅威がない |
| **奇襲方向係数**：想定外の方向から攻撃するほど防御側の対応が遅れ、効果が倍増する | `flankMultiplier` は側背面のみ。**「敵が最後に予期した位置から来る」という心理的奇襲**への係数がない |

**結論**：『戦略論』は当プロジェクトの戦略AIとCombat層に対し、
**①間接経路評価（最小予期線）②心理的瓦解（複合脅威崩壊）③戦略的存在脅威（分散強要）④奇襲方向係数**
という4つの欠落軸を与える。いずれも既存 CLZ/ZOC/FleetMorale の**後退なし additive 接続**で実装できる。

---

## 1. 役に立つ視点（要約）

1. **「間接は直接に勝る」**：ほぼ全ての大規模直接攻撃は膠着か失敗。間接は消耗を避け心理的崩壊で決着する。→ 当プロジェクトのAI侵攻経路に「予期しにくさ」評価を追加する土台。
2. **最小予期線＝経路の非直線化**：敵にとって「まさかここから」が最も効果的。距離ではなく敵の期待密度で経路を評価する。→ `GalaxyPathfinder` への追加重み軸。
3. **心理的瓦解（Dislocation）**：二正面・三正面の脅威が意思決定を麻痺させ、実際の打撃前に組織が崩れる。→ `FleetMorale`・`BattleAllegianceRules` への複合脅威ペナルティ。
4. **Fleet in Being（分散強要）**：交戦しなくとも「攻撃できる位置にいる」だけで敵の自由を奪う。→ 戦略レイヤーの新概念：`FleetInBeingRules`。
5. **奇襲方向係数**：「敵が最後に期待した方向」と実際の攻撃方向の差が大きいほど効果が倍増する。→ `CombatModifiers` への directional surprise 拡張。
6. **歴史的実証（作戦史分析）**：数十の戦役分析から帰納される普遍則。→ ゲーム内の作戦史記録（史書#784/殿堂#785）と「間接度スコア」の組み合わせで後から再評価できるlore。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**CLZ #1132（クラウゼヴィッツ）・`GalaxyPathfinder`・`ZoneOfControl`・`FleetMorale` を作り直さない**。LDH はそれらに**欠落軸を足し接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・リデルハートの signature）

#### LDH 最小予期線スコアリング（`IndirectApproachRules`）
- **`IndirectApproachScore`**：戦略経路の「敵の予期しにくさ」スコア（0=最直接/1=最間接）。
  - 経路上の各回廊ごとに「その回廊に敵艦隊が期待している密度」を評価し、期待の低い経路ほど高スコア。
  - 敵密度は `StrategicFleetRegistry` の敵配置を `GalaxyMap` 上で評価（重力モデル：近隣星系の敵兵力を距離で重み付け）。
- **`BestIndirectRoute(map, reg, src, dst, indirectBias)`**：Dijkstra のエッジコストに間接スコアを混合（`indirectBias` で直接コストと間接スコアの比）。`GalaxyPathfinder.FindPath` の**拡張版（後退なし）**。
- 接続：`GalaxyPathfinder`（経路探索の重み拡張）×`StrategicFleetRegistry`（敵配置）。
- 純ロジック新設 → **EditMode テスト必須**。

#### LDH 心理的瓦解（`DislocationRules`）
- **`ThreatDirectionCount(fleet, allHostiles)`**：艦隊が異なる方向から受けている脅威の方向数をカウント（45°刻みで量子化）。
- **`DislocationPenalty(directionCount)`**：方向数が増えるほど士気低下倍率が加速（1方向=1.0・2方向=1.3・3方向以上=1.6、上限 `maxDislocationFactor`）。
- **会戦配線**：`FleetMorale.OnTakeDamage` でこの倍率を追加乗算（基準士気値は非破壊＝実効値パターン）。
- 接続：`FleetMorale`（士気崩壊加速）×`BattleAllegianceRules`（寝返りカスケードの早期発火）×`CombatModifiers`。
- 純ロジック新設 → **EditMode テスト必須**。

### ★★ 高（戦略レイヤーへの存在脅威の明示化）

#### LDH 分散強要（`FleetInBeingRules`）
- **Fleet in Being 概念**：攻撃しない艦隊がある星系近辺に布陣するだけで、敵はその星系を増援で守らざるを得ず自由な機動を失う。
- **`ThreatReach(fleet, map)`**：戦略艦隊が1ターン内に到達可能な星系セット（`GalaxyPathfinder` の到達圏）。
- **`ForcesDispersion(reg, map, faction)`**：敵の各星系が何艦隊分の「存在脅威」に晒されているかを集計し、**守備に縛られている推定兵力**を返す。
- **AI利用**：`GalaxyView` の戦略AIが「攻撃せず存在するだけ」の位置取りを選択する際の評価軸。
- 接続：`StrategyRules`（戦略行動評価）×`ZoneOfControl`（局所版と概念共有）×`GalaxyPathfinder`（到達圏）。
- 純ロジック新設 → **EditMode テスト必須**。

#### LDH 戦略AIへの間接経路選択配線
- `GalaxyView` の侵攻先選択・`FleetAI`（戦略モード）が `IndirectApproachRules.BestIndirectRoute` を参照。
- `indirectBias`（0=最短/1=最間接）を勢力ごとの AIプロファイルで設定可能（機動型提督は高め）。
- 接続：LDH-1（`IndirectApproachRules`）×`GalaxyView`×`FleetAI`。
- Game 層配線（pure logic なし・EditMode テスト不要）。

### ★ 中（Combat 層への奇襲係数）

#### LDH 奇襲方向係数（`CombatModifiers` 拡張）
- **`SurpriseDirectionFactor(attackDir, expectedDir)`**：敵が最後に「この方向から来る」と認識していた方向と実際の攻撃方向の角度差。差が大きいほど倍率加算（最大 `maxSurpriseFactor`・既定 1.25）。
- `expectedDir` の出所：`FleetWeapon.SetManualTargetFleet` で記録した敵の最終観測位置、または `FleetAI` の最終侵攻方向。
- 接続：`CombatModifiers`（係数追加）×`ShipCombat.ComputeDamage`（消費）×`flankMultiplier`（側背面と加算ではなく乗算で組み合わせ可）。
- 純ロジック追加 → **EditMode テスト必須**。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 攻勢終末点（Culminating Point）の再実装 | **CLZ #1132 CLZ-2 がカバー**。LDH はその前段（どう間接に攻めるか）のみ |
| 重心（Center of Gravity）概念の新設 | **CLZ #1132 CLZ-4 がカバー** |
| 政戦連接（War as continuation of politics）への接続 | **CLZ #1132 CLZ-3 がカバー** |
| 内線/外線作戦の幾何モデル | **Jomini 参考 EPIC（未）に分担**。LDH の間接は心理的視点であり幾何的位置取りはJomini側 |
| `FleetAI` の戦術AI全面リプレイス | 指示外ファイルの大規模書き換えに相当。段階的配線のみ |
| 消耗戦（Attrition）モデルの新設 | **`ApplyAttrition`/`FleetPoolRules` がカバー** |
| 地形差別化による間接経路の物理モデル | **SupplyRules/通商#94 がカバー**。LDH はAI評価軸のみ追加 |

---

## 3. EPIC #LDH の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。
> 既存戦略ロジックは**接続のみ・重複新設しない**（additive）。
> 著作権注意：固有名・文章・キャラは不使用、**作戦理論の構造パターンのみ**参考。

> **EPIC = #1338**。GitHub issue 起票済み（#1339〜#1351）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **LDH-1** | #1339 | 最小予期線スコアリング `IndirectApproachRules`（経路の「敵期待外れ度」評価） | `GalaxyPathfinder`×`StrategicFleetRegistry`。純ロジック新設・EditMode必須 |
| **LDH-2** | #1342 | 戦略AIへの間接経路選択配線（`GalaxyView`/`FleetAI`に `IndirectApproachRules` を繋ぐ） | LDH-1×`GalaxyView`×勢力AIプロファイル |
| **LDH-3** | #1344 | 心理的瓦解 `DislocationRules`（複数方向からの脅威による士気崩壊加速） | `FleetMorale`×`BattleAllegianceRules`×`CombatModifiers`。純ロジック新設・EditMode必須 |
| **LDH-4** | #1348 | 分散強要 `FleetInBeingRules`（Fleet in Being ＝ 攻撃しない存在脅威が敵を縛る） | `StrategyRules`×`GalaxyPathfinder`×`ZoneOfControl`。純ロジック新設・EditMode必須 |
| **LDH-5** | #1351 | 奇襲方向係数（`CombatModifiers` 拡張・最小予期線の会戦適用） | `CombatModifiers`×`ShipCombat.ComputeDamage`×`flankMultiplier`。EditMode必須 |

### 推奨着手順

`LDH-1`（最小予期線スコアリング＝真の欠落・純ロジック確定） →
`LDH-3`（心理的瓦解＝CLZ #1132 と組み合わさって「ダメージ前に崩れる」を体現） →
`LDH-4`（分散強要＝戦略AIの行動選択肢を豊かにする） →
`LDH-2`（AIへの配線＝LDH-1/4が揃ってから） →
`LDH-5`（奇襲係数＝CLZ-2攻勢終末点・flankMultiplierとの整合確認が要る）。

> いずれも既存 CLZ/ZOC/FleetMorale/GalaxyPathfinder を**後退させず接続**する additive 設計。
> 戦略AIの「思考の深さ」を直線的な強さではなく**経路の巧みさ**で表現する。
