# クレフェルト『補給戦』参考設計（EPIC #1361）

> 参照元：マーティン・ファン・クレフェルト『補給戦——ヴァレンシュタインからパットンまでの兵站の歴史』(1977)。
> 近現代の主要戦役を通じて**補給が作戦の可能範囲を決める**ことを実証的に論じた兵站研究の古典。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略＋既に豊富な補給・兵站純ロジック層）にとって**役に立つ視点だけ**を抽出し、EPIC `#CRV` として issue 化する提案。
> **著作権注意：固有名・文章・キャラクター・固有設定は流用せず、兵站メカニクス／作戦構造のパターンのみを参考にする。**

---

## 0. なぜ「補給戦」が本システムに役立つか

当プロジェクトは補給・兵站に関する**多くの純ロジックを既に保有**している：

| 既存（カバー範囲） | 何をカバーするか |
|---|---|
| `SupplyRules.IsSupplied`/`TickFront` | 補給線の接続（二値）・前線枯渇タイマー |
| `CommerceRaidingRules`（L-3 #95） | 補給線の切断（通商破壊）|
| `ForageRules`（SUN-3 #1128） | 現地調達：占領・通過星系からの自律補給 |
| `CulminatingPointRules`（SUN-4 #1129） | 攻勢終末点：補給距離比例の戦力効率低下 |
| `FrictionRules`（CLZ-1 #1133） | 摩擦：命令深度×補給比×士気→実行成功確率 |
| `MilitaryColonyRules`（SGZ-5 #1107） | 屯田制：占領地への永続農業拠点 |
| `ResourceProductionRules`/`ResourceStockpile` | 資源産出・勢力備蓄 |

**しかし、これらは補給の「有無」「切断」「短期調達」「距離逓減」をカバーするにとどまり**、クレフェルトが実証的に示した以下の補給構造上の問題が**欠けている**：

| 『補給戦』固有の視点 | 当プロジェクトでの欠落 |
|---|---|
| **前進補給基地（デポ）による到達限界の延伸** | SUN-4 は問題（過伸張）を定義するが、デポによる**解決手段**（補給基点の前送り）が無い |
| **尾高比——兵站消費は艦隊規模で超線形増加** | `SupplyRules` は消費量を艦隊数で線形扱い。大艦隊が補給系を飽和させる構造が無い |
| **攻勢/防御補給非対称——侵攻側は防衛側の2〜3倍消費** | 攻勢/防御で補給消費量が変わらず、防衛戦略の補給優位が表現されない |
| **回廊スループット上限——補給回廊には輸送容量がある** | 回廊は単に「繋がっているか」だけで最大スループットが無い。大軍が回廊を「詰まらせる」構造が無い |

**結論**：クレフェルトは当プロジェクトの補給レイヤーに、SUN-3/SUN-4が先行定義した「現地調達」「距離逓減」を**前提に乗りながら**、①デポによる到達延伸・②尾高比・③攻勢防御非対称・④回廊容量という4つの欠落軸を**additive に**与える。いずれも既存ロジックを後退させず接続するだけ。

---

## 1. 役に立つ視点（要約）

クレフェルトの主要知見を、**本システムに効く形**で1行ずつ：

1. **「兵站が戦略の地平線を決める」** — 作戦は補給が届く場所までしか及ばない。戦略・作戦の自由度は補給体制の産物。→ デポと回廊容量で「届く範囲」に実体を与える（SUN-4 補完）。
2. **「デポが作戦を可能にする」** — ロンメルの失敗は戦術的失敗ではなく、前進デポを設置できなかったための作戦的失敗。→ `DepotRules`＝前進デポが補給基点を前送りし到達限界を延伸。
3. **「大軍はより豊かでなく、より貧しく動く」** — 近代以降、軍は大型化するほど補給の超線形コストに縛られ、機動の自由が下がった。→ `LogisticsBurdenRules`＝尾高比の超線形スケーリング。
4. **「攻撃側は防衛側の数倍を消費する」** — 前進・陣地構築・損耗補充で攻撃側の補給消費は圧倒的に重い。防衛側は補給効率で優位に立つ。→ `SupplyModeRules`＝補給消費の攻防非対称。
5. **「鉄道の容量が戦略を縛った」** — WWI の動員計画が変更不能だった最大理由は鉄道スケジュール。回廊（鉄道＝超光速航路）にも輸送容量がある。→ `CorridorCapacityRules`＝回廊の補給スループット上限。
6. **「補給体制の構築が作戦開始に先行する」** — 作戦の準備は弾薬・食料・燃料の備蓄から始まる。大攻勢の前にデポを設置するのは戦略的先手。→ SUN-4/CLZ-1 の「問題」側に対し CRV は「解決手段」を供給する。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`SupplyRules`/`ForageRules`(SUN-3)/`CulminatingPointRules`(SUN-4)/`FrictionRules`(CLZ-1) を作り直さない**。CRV はそれらに欠落軸を接続するだけ（additive）。

### ★★★ 最優先（クレフェルト固有・真の欠落）

#### CRV 前進補給基地（DepotRules）
- **`Depot`**（純データ）：`systemId`/`ownerFaction`/`capacity`（最大蓄積量）/`isEstablished`。
- **`DepotRules`**（static）：`EffectiveSupplyBase(fleet, map, depots, supplySystems)` — SUN-4 の `OverextensionFactor` 計算で補給基点として「自領土の補給システム **または** 到達済みの確立済みデポ」を選択し、最短距離ホップを引き下げる。`EstablishCost`（星系占領＋資源消費）/`DepotCapacityFactor`（デポの容量が前線戦力を制限）。
- **戦略的意義**：前進デポは「作戦半径を買う投資」。`ResourceStockpile` から建設コストを消費し、以後の侵攻の到達限界を伸ばす。デポが攻略されると到達限界が元に戻る（攻撃目標として機能）。
- 接続：SUN-4 `CulminatingPointRules.OverextensionFactor`（補給基点の差し替え）・`StrategyRules`・`ResourceStockpile`。

#### CRV 超線形兵站消費（LogisticsBurdenRules）
- **尾高比（tail-to-tooth ratio）**：「支援要員：戦闘要員」の比は規模と距離が増えると超線形に拡大する。
- **`LogisticsBurdenRules`**（static）：`ConsumptionRate(fleetStrength, distanceHops)` = `fleetStrength^1.3 × distanceHops^0.7 × baseRate`（調整定数公開・実効値パターン）。`BurdenFactor(fleet, supply)` — 消費が補給量を超えた場合の充足率（0..1）。
- **戦略的意義**：大艦隊の長征は補給コストが跳ね上がり非効率。少数精鋭の機動部隊が兵站的に有利＝大艦隊思想 vs 機動戦の戦略トレードオフ（銀英伝の艦隊運用に直結）。
- 接続：`SupplyRules.TickFront`（消費量の計算に差し込む）・`ResourceStockpile.TryConsume`・SUN-4。

### ★★ 高（核心的洞察・補給レイヤーを深化させる）

#### CRV 攻勢/防御補給非対称（SupplyModeRules）
- **`SupplyMode`**（enum）：`Advancing`（前進中）/ `Holding`（占領保持）/ `Withdrawing`（撤退中）。
- **`SupplyModeRules`**（static）：`ConsumptionMultiplier(mode)` — Advancing: 2.0 / Holding: 1.0 / Withdrawing: 0.5（調整定数公開）。`DetectMode(fleet, prevSystem, currentSystem)` — 前進/保持/撤退を位置差分から判定。
- **戦略的意義**：侵攻側は常に補給コストで不利（前進が続くほど）、防衛側は補給効率で優位。「持久防衛」が単純な退却でなく補給効率の高い選択になる。`WarGoalRules.WarWeariness`（政治的厭戦）とは別の**純軍事的防衛優位**。
- 接続：LogisticsBurdenRules (CRV-2) の `ConsumptionRate` へ乗算・`StrategicFleet`・`StrategyRules`。

#### CRV 回廊補給スループット（CorridorCapacityRules）
- **回廊にも輸送容量がある**：通商回廊は容量が小さく、要衝回廊は大きい。
- **`CorridorCapacityRules`**（static）：`SupplyCapacity(corridor)` — `通商: capacityBase, 要衝: capacityBase × 2`（`CorridorType` を読む）。`AvailableSupplyRatio(corridor, totalDemand)` — 総需要が容量を超えると比例配分（0..1）。
- **戦略的意義**：大艦隊が狭い通商回廊を通って侵攻すると補給が飢える＝戦力を揃えても活かしきれない。「どの経路で攻めるか」（要衝回廊を通るか通商回廊を通るか）が補給的な作戦決定になる。DUN-1 (`StrategicChokePointRules`) の「資源独占」とは別の**補給容量からくる要衝の価値**。
- 接続：`Corridor.type`（`CorridorType`）・`SupplyRules`・`GalaxyMap`・DUN-1 #1266。

### ★ 中（世界観lore）

#### CRV（lore）補給哲学の開示データ
- コード新設なし。`DisclosureLedger`（FND-4）への**lore データ入力**のみ。
- 「兵站が戦略の地平線を決める」「大軍はより豊かでなく、より貧しく動く」「前進デポなき進撃は蛮勇に過ぎない」「回廊の幅が可能な作戦を縛る」。
- 接続：`DisclosureLedger`・CLZ-5 lore・SUN-6 lore。

### ❌ 不採用（重複・既存でカバー済み）

| 不採用 | 理由 |
|---|---|
| 現地調達 | **SUN-3 `ForageRules`** で実装済み・重複新設しない |
| 攻勢終末点・距離依存戦力低下 | **SUN-4 `CulminatingPointRules`** で実装済み・重複新設しない |
| 通商破壊・補給線切断 | **`CommerceRaidingRules`（L-3 #95）** でカバー |
| 摩擦による補給比率の影響 | **CLZ-1 `FrictionRules`** の `supplyRatio` 入力でカバー |
| 屯田制（占領地への恒久農業基地） | **SGZ-5 `MilitaryColonyRules`** でカバー（デポとは別物：デポは資材前送り、屯田は現地永続生産） |
| 輸送単位の物理シミュ（隊商・輸送艦の個別追跡） | タイクン化回避（高位の決断→エンジン駆動。個別隊商の微操作不要） |

---

## 3. EPIC #CRV の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。既存補給ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/兵站構造のみ**参考。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **CRV-1** | #1363 | 前進補給基地（`Depot`/`DepotRules`：デポを補給基点として `CulminatingPointRules` の到達限界を延伸） | SUN-4 #1129（`OverextensionFactor` の基点差し替え）・`ResourceStockpile`・`StrategyRules` |
| **CRV-2** | #1365 | 超線形兵站消費（`LogisticsBurdenRules`：艦隊規模^1.3 × 距離^0.7 の尾高比スケーリング） | `SupplyRules.TickFront`・`ResourceStockpile.TryConsume`・SUN-4 |
| **CRV-3** | #1366 | 攻勢/防御補給非対称（`SupplyModeRules`：侵攻×2.0/保持×1.0/撤退×0.5 の消費倍率） | CRV-2 `LogisticsBurdenRules`・`StrategicFleet`・`StrategyRules` |
| **CRV-4** | #1367 | 回廊補給スループット（`CorridorCapacityRules`：通商/要衝で輸送容量差・超過で補給配分） | `Corridor.type`・`SupplyRules`・DUN-1 #1266 |
| **CRV-5** | #1368 | （lore）補給哲学の開示データ（「兵站が戦略の地平線を決める」他を DisclosureLedger へ） | FND-4 `DisclosureLedger`。コード新設なし |

### 推奨着手順

`CRV-1`（デポ＝SUN-4 の補完・最も具体的なデポロジック）→ `CRV-2`（尾高比・スケーリング式の確定）→ `CRV-3`（攻防非対称・CRV-2 に乗算するだけ）→ `CRV-4`（回廊容量・既存 `Corridor` に接続）→ `CRV-5`（lore）。

> いずれも SUN-3/SUN-4/CLZ-1/SGZ-5 が先行して定義した層に**additive に接続**する設計。既存補給ロジックを後退させない。
