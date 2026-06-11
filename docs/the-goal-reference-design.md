# ゴールドラット『ザ・ゴール』参考設計（EPIC #GOL）

> 参照元：エリヤフ・ゴールドラット『ザ・ゴール』（制約理論 TOC: Theory of Constraints）。
> 工場再建の小説形式で**制約理論（TOC）**を説く経営科学の古典。
> ボトルネック識別→集中→従属→引き上げ→繰り返しの5ステップが核。
> 本ドキュメントは、当プロジェクト（造船#884・兵站#92 の容量制約を既に持つ）にとって**役に立つ視点**だけを抽出し、EPIC `#GOL` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**メカニクス／経営科学の構造パターンのみ**を参考にする。

---

## 0. なぜ「ザ・ゴール（TOC）」が本システムに役立つか

当プロジェクトは造船・兵站・財政の**マクロ容量制約を既に保有**している：

| 既存（カバー範囲） | カバー内容 |
|---|---|
| `ShipyardRules`/`Shipyard`/`BuildOrder`（#884 BUILD-1〜4） | 造船キュー・`buildPower`・`parallelCapacity`（容量制約の表現） |
| `SupplyRules`（#94 L-2） | 補給線の連結性・補給切れ判定 |
| `ResourceProductionRules`（#93 L-1） | 星系ごとの資源産出（安定度比例） |
| `FleetPoolRules`（#148） | 艦隊配分の上限管理 |
| `FiscalRules`/`FiscalState`（#161/162） | P&L・国債・為替・税収 |
| `EventEngine`/`GameEventDef`（#116） | 条件発火→選択肢→効果 |

**しかし、これらは個々のモジュールの「容量値」を保持するだけ**であり、TOCが固有に描く以下の仕組みが**欠けている**：

| ザ・ゴールが固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **ボトルネック識別（制約特定）** | `ShipyardRules.parallelCapacity` は固定容量だが、「複数の造船所・補給拠点を横断してどの工程がシステム全体の制約か」を判定するロジックがない |
| **スループット会計（T/I/OE）** | `FiscalRules` は損益計算書(P&L)ベース。「スループット(T)=流量・在庫(I)=滞留・業務費用(OE)=支出」の三指標がない。ローカル効率最大化≠システム最適化の認識フレームが欠ける |
| **DBR スケジューリング（ドラム・バッファ・ロープ）** | `ShipyardRules.Tick` は FIFO キューで先頭から処理。ボトルネック速度に合わせた**投入ペーシング（ロープ）**がない。制約前バッファの管理も未実装 |
| **従属事象の変動累積** | 独立した各ステップの確率的変動が従属連鎖でどう蓄積するかのモデルがない。「上流バッファは下流バッファで代替不可」の構造も未表現 |

**結論**：『ザ・ゴール』は「ローカル最適の罠」を解くシステム思考を与える。造船#884・兵站#92 が持つ容量制約の上に、**ボトルネック識別→集中→投資**という高位決断フレームを被せる。タイクン化回避：製造ライン細部（工程分解/WIP追跡）でなく「どのリソースがシステム制約か」を示すだけ。数値は純ロジック層に閉じ、盤面への反映は Campaign/EventEngine 経由。

---

## 1. 役に立つ視点（要約）

『ザ・ゴール』の構造を、**本システムに効く形**で1行ずつ：

1. **システムの強さはボトルネックが決める**。造船所10か所の能力向上でも最弱1か所が全体スループットを制限する。→ `FleetPoolRules.Available` が高くても造船が詰まると意味がない構造を数値で示す。
2. **ローカル効率最大化がシステムを傷める**。非ボトルネックを100%稼働させると在庫・滞留が爆発する。→ 「遊び」が意図的であるべき理由＝`ShipyardRules` の `parallelCapacity` を無闇に増やしても効果がない場面。
3. **スループット(T)・在庫(I)・業務費用(OE)で測る**。P&Lの「コスト削減」ではなく「T最大化」が本当のゴール。→ `FiscalRules` に並ぶ別指標レンズとして、戦略層の「何を優先するか」を明確化。
4. **制約の前にバッファを置き、制約以外は意図的に遊ばせる（DBR）**。→ `ShipyardRules` の投入ペーシングに遊びを与える理論的根拠。
5. **従属事象の変動は累積する**。各ステップが独立に変動していても、順番に依存した工程では変動が足し合わさる。→ 補給線・造船チェーンの遅延モデルに現実的なばらつきを与える。
6. **5ステップのサイクル（識別→集中→従属→引き上げ→繰り返し）は終わらない**。あるボトルネックを解消すると次の制約が現れる。→ 銀河版「制約の移動」=攻城・前線拡大で補給がボトルネックになる動態。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`ShipyardRules`/`SupplyRules`/`ResourceProductionRules`/`FiscalRules` は作り直さない**。GOL はそれらに**欠落軸を additive に足し、接続する**だけ。

### ★★★ 最優先（真の欠落・TOC の signature）

#### GOL — `BottleneckRules` / `ProductionStep`（制約識別と集中）

- **`ProductionStep`**（純データ）：`name`/`capacity`(単位時間あたりの最大スループット)/`demand`(単位時間あたりの要求量)。
- **`BottleneckRules`**（static 純ロジック）：
  - `Identify(steps)` → ボトルネック `ProductionStep`（capacity が最小の工程）
  - `MaxThroughput(steps)` → float（= ボトルネックの capacity）
  - `EfficiencyAt(steps, step)` → float（demand/capacity。>1 = 過負荷）
  - `ExploitGain(steps, step, deltaCapacity)` → float（その工程を強化したときの全体スループット増分）
  - `ShouldElevate(steps, step)` → bool（そのステップが現在の制約か）
  - `Subordinate(steps, bottleneck)` → `IReadOnlyList<ProductionStep>`（非ボトルネック工程の推奨最大稼働率を返す＝ボトルネック速度に揃える）
- 接続：`ShipyardRules`・`SupplyRules` が自身を `ProductionStep` として公開 → `BottleneckRules` で横断評価。

#### GOL — `ThroughputAccounting` / `ThroughputState`（スループット会計）

- **`ThroughputState`**（純データ）：`throughput(T)`（販売収益−真の変動費）/`inventory(I)`（生産物・WIPに投じた資金）/`operatingExpense(OE)`（T を生み出すための費用）。
- **`ThroughputRules`**（static 純ロジック）：
  - `NetProfit(state)` = T − OE
  - `ProductivityIndex(state)` = T / OE（> 1 = 持続可能）
  - `InvestmentTurnover(state)` = T / I
  - `ReturnOnInvestment(state)` = NetProfit / I
  - `Delta(before, after)` → 改善前後の差分
- 接続：`CampaignRules.Tick` が各勢力の生産・在庫・費用から `ThroughputState` を組み立て、`FiscalRules` とは**別レンズ**として `CampaignState` に保持。

### ★★ 高（DBR・造船配線）

#### GOL — `DrumBufferRopeRules`（DBRスケジューリング）

- **`DrumRate(steps)`** → float（ボトルネック = ドラムの速度）。
- **`RecommendedBuffer(drumRate, safetyMultiplier)`** → float（制約前に置く推奨バッファ量）。
- **`enum BufferZone { Green, Yellow, Red }`**（バッファ消費状況）：
  - Green：残量 > バッファの 2/3（余裕あり）
  - Yellow：1/3〜2/3（監視）
  - Red：< 1/3（緊急補充）
- **`GetBufferZone(remaining, bufferSize)`** → `BufferZone`。
- **`ShouldExpedite(zone)`** → bool（Red → 緊急対応）。
- **`ShouldRelease(currentBuffer, bufferSize, drumRate)`** → bool（ロープ：バッファに余裕がある時だけ新規投入）。
- 接続：`ShipyardRules.Enqueue` の呼び出し側が `ShouldRelease` を問い合わせて投入を制御。

#### GOL — 造船キュー（#884）× 兵站（#92）への TOC 接続

- `ShipyardRules.ToProductionStep(Shipyard)` → `ProductionStep`（capacity = `buildPower × parallelCapacity`、demand = キュー長からの推測負荷）。
- `ShipyardRules.SystemBottleneck(yards)` → `ProductionStep`（勢力全造船所を横断してボトルネックを特定）。
- `SupplyRules.ToProductionStep(corridorId, supplyRate)` → `ProductionStep`（補給回廊を工程として包む）。
- `CampaignRules.ProductionChain(factionId)` → `IReadOnlyList<ProductionStep>`（生産→輸送→消費の全体チェーンを組み立てる）。

### ★ 中（従属事象の変動累積）

#### GOL — `DependentVarianceRules`（従属事象モデル ＋ EventEngine 接続）

- **`DependentVarianceRules`**（static 純ロジック）：
  - `AccumulatedVariance(sigmasPerStep)` → float（各ステップの σ から累積分散 = Σσᵢ² の平方根）
  - `ThroughputSigma(steps, sigmasPerStep)` → float（ボトルネックの σ が全体を支配）
  - `ShouldExpandBuffer(variance, currentBuffer)` → bool（変動が大きくなったらバッファ拡大推奨）
- 接続：`EventEngine` に「生産遅延」`GameEventDef` を登録 = 累積分散が閾値超でイベント発火 → 「造船所の工期遅延：ボトルネックを強化するか？」選択肢。

### ❌ 不採用（重複・タイクン化・既存で十分）

| 不採用 | 理由 |
|---|---|
| 詳細な製造工程分解・WIPトラッキング | タイクン化回避（製造ライン細部の操作は設計方針に反する） |
| クリティカルチェーン・プロジェクトスケジューリング | `ShipyardRules` の Queue + `buildPower` 設計で十分。新モジュール不要 |
| EVA（経済的付加価値）会計 | `FiscalRules` の P&L + 債務管理が既にカバー |
| スループット会計の専用リアルタイムUI | 戦略UIは別途設計（GOL はロジック層に閉じる） |
| ゴールドラットの後続作品（クリティカルチェーン/ザ・チョイス）のロジック追加 | 別 EPIC 化せず、本 EPIC に閉じる |

---

## 3. EPIC #GOL の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。既存造船・兵站ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/経営科学の構造のみ**参考。

> **EPIC = #1271**。GitHub issue 起票済み（#1274〜#1289）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **GOL-1** | #1274 | `BottleneckRules` / `ProductionStep` — 制約識別と集中（TOC 5ステップ純ロジック） | 新 `BottleneckRules`。`ShipyardRules`/`SupplyRules` が `ProductionStep` を公開する土台 |
| **GOL-2** | #1277 | `ThroughputAccounting` / `ThroughputState` — スループット会計（T/I/OE 三指標） | `FiscalRules` と並立する別レンズ。`CampaignRules.Tick` が計算・保持 |
| **GOL-3** | #1282 | `DrumBufferRopeRules` — DBR スケジューリング（ドラム＝ボトルネック速度・バッファ・ロープ＝投入ペーシング） | `ShipyardRules.Enqueue` の呼び出し側が `ShouldRelease` を参照 |
| **GOL-4** | #1285 | 造船キュー（#884）× 兵站（#92）への TOC 接続（`ShipyardRules.ToProductionStep`・`SupplyRules.ToProductionStep`・`CampaignRules.ProductionChain`） | GOL-1〜3 の実装後。既存ロジック改変せず拡張 |
| **GOL-5** | #1289 | `DependentVarianceRules` — 従属事象の統計的変動累積 ＋ `EventEngine`「生産遅延」イベント接続 | `EventEngine`（#116）×`BottleneckRules`（GOL-1）。盤面イベントへの橋渡し |

### 推奨着手順

`GOL-1 → GOL-2`（制約識別＋スループット会計＝TOCの測定・判断基盤）→ `GOL-3`（DBRスケジューリング＝投入制御の実装）→ `GOL-4`（既存造船・兵站への接続）→ `GOL-5`（変動モデル＋イベント連動）。

> GOL-1 はすべての基盤となる `ProductionStep` 型を定義するため必ず最初。GOL-4 は GOL-1〜3 の型を前提とするため後半。GOL-5 は任意のタイミングで着手可（独立性が高い）。
