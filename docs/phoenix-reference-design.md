# 手塚治虫『火の鳥』参考設計（EPIC #PHNX）

> 参照元：手塚治虫『火の鳥』。過去から遥か未来まで時代を行き来しながら、文明の興亡と生死の循環を描く大河漫画。
> 「滅びては再生する」文明のサイクルと、時代を超えて繰り返される「業（カルマ）」の構造パターンを活用する。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略）にとって**役に立つ視点**だけを抽出し、EPIC として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**世界観の構造パターン・メカニクスのみ**を参考にする。

---

## 0. なぜ「火の鳥」が本システムに役立つか

当プロジェクトは政治・社会・経済のマクロロジックを広くカバーしている。しかし「**文明スケールの崩壊と再建**」という縦軸が薄い。

### 既存（カバー範囲）

| 既存システム | カバー範囲 |
|---|---|
| `DynastyRules`/`Regime`（#867） | 王朝の天命喪失・易姓革命＝政体レベルの交代 |
| `SuccessionRules`/`Organization`（#812） | 英雄死後の組織存続＝人物スケールの継承 |
| `DisclosureLedger`（FND-4） | 秘史・真相・予言の開示チェーン（静的データ入力） |
| `LifecycleRules`/`Calendar`（LIFE-1/2） | 通常寿命の年齢・死亡 |
| `DemographicsRules`/`Population`（LIFE-3） | 通常の人口動態（出生/老化/死亡コホート） |
| `ResearchRules`/`ResearchProject`（#123-127） | 研究進歩（一方向的進展・政体偏り） |
| `HopeRules`/`Community`（#852） | 希望の喪失→末人（フロストパンク型） |
| `CampaignRules`/`FactionState` | 全体の統合層（国家状態の合成） |

### 火の鳥が固有に持つ視点 × 当プロジェクトでの欠落

| 火の鳥の構造パターン | 当プロジェクトでの欠落 |
|---|---|
| **文明フェーズ**（興隆→頂点→衰退→崩壊→再建のサイクル） | `DynastyRules.Revolution` は政体交代のみ。技術水準・文明レベル自体が崩壊しゼロから再建される仕組みが無い |
| **技術暴走リスク**（研究の積み上げが制御不能になり文明を破滅させる） | `ResearchRules` は進歩のみ計上。「危険閾値」「技術的暴走による文明崩壊」が無い |
| **歴史的循環パターン検出**（同じ業・過ちが時代を超えて繰り返される） | `DisclosureLedger` は静的秘史の保持のみ。「過去の崩壊パターンとの一致を動的に検出」する仕組みが無い |
| **壊滅的人口崩壊と回復**（戦争・疾病・飢饉の複合で生存者が激減→ゼロから再建） | `DemographicsRules` は通常動態。絶滅寸前→回復という極端な崩壊曲線が無い |
| **技術退行**（崩壊後に技術水準が失われゼロから再建） | `ResearchRules` は単調増加前提。崩壊によって技術が喪失されるルートが無い |
| **宇宙的観察者の視点**（文明の盛衰を超えて存在し続ける存在が歴史の反復を見る） | `DisclosureLedger` の開示loreとして欠けている（コード新設不要） |

**結論**：火の鳥は当プロジェクトに「**文明スケールの崩壊・再生サイクル**」という縦軸を与える。これは既存の `DynastyRules`（政体）・`DemographicsRules`（通常人口）・`ResearchRules`（一方向進歩）には存在しない**文明レベルの縦軸**として真の欠落である。戦略ゲームの長期プレイに「技術的停滞・崩壊・再建」というドラマを与え、フェザーン（#160）・覇権交代（#228）・王朝サイクル（#867）を「文明スケール」で見直す鍵となる。

---

## 1. 役に立つ視点（要約）

火の鳥の世界観構造を、**本システムに効く形**で1行ずつ：

1. **文明は栄え滅び再生する**——政体の交代ではなく技術水準・人口自体がゼロに崩壊し、再建の時代が始まる。→ `DynastyRules.Revolution` の政体交代を**文明スケールへ拡張**する `CollapseRules`。
2. **技術の暴走が文明を滅ぼす**——研究の進歩は必ずしも繁栄でなく、制御を超えた技術が破滅を招く。→ `ResearchRules` に「**危険閾値**」を設け、暴走で崩壊フェーズへ移行。
3. **同じ業が時代を超えて繰り返される**——支配欲・過信・短期的利益が文明を毎回同じパターンで崩壊させる。→ 現在の状態変数が過去の崩壊パターンと一致したとき `DisclosureLedger` の開示をトリガー。
4. **壊滅からの再建は通常の人口動態とは別**——絶滅寸前の少数生存者から始まる回復は、コホート推移とは異なる特殊曲線。→ `DemographicsRules` への崩壊・回復軸の追加。
5. **崩壊は技術も喪失させる**——後継文明は過去の技術を再発見する。→ `ResearchRules` の一方向進歩仮定に「退行」軸を加える `TechRegressionRules`。
6. **観察者の視点が歴史に意味を与える**——不死・超長寿の観察者が「これは以前にも起きた」と知ることで歴史に輪廻の意味が宿る。→ コード新設なし、`DisclosureLedger` への lore 入力（PHNX-6）。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`DynastyRules`/`DemographicsRules`/`ResearchRules` を作り直さない**。PHNX はそれらに**欠落軸を足し接続する**（additive）。タイクン化回避＝マイクロ操作を増やさず、係数・フェーズ・閾値で駆動し創発帰結を出す。

### ★★★ 最優先（真の欠落・火の鳥の signature）

#### PHNX 文明フェーズ（`CivilizationPhase` enum ＋ `CollapseRules`）
- **文明フェーズ**：`Rising`/`Mature`/`Declining`/`Collapsed`/`Rebuilding` の5段階を勢力レベルで追跡。
- **崩壊トリガー**：TechRisk が閾値超 or 複合崩壊条件（`FactionState.IsCollapsing` + 版図崩壊 + 人口激減）で `Collapsed` へ遷移。
- **再建フェーズ**：`Rebuilding` では産出・研究・統治力が大幅制限（`RebuildingProductionFactor`）。時間経過で `Rising` へ。
- **接続**：`DynastyRules.Revolution`（政体）→ 文明スケールへ拡張。`FactionStateRules.Tick`（統合層）からフェーズを駆動。`CampaignRules.EffectiveStability` に文明フェーズ係数を乗算。

#### PHNX 技術暴走リスク（`TechRiskRules`/`TechRisk`）
- **`TechRisk`**（0〜1）：研究の累積進歩で上昇、統治品質（`GovernanceRules.OutputFactor`）・正統性（`DynastyRules.Regime.legitimacy`）で抑制。
- **閾値**：`TechRisk ≥ 0.8` で警告イベント発火（`EventEngine`）、`≥ 1.0` で崩壊トリガー（`CollapseRules`）。
- **計算式**：`ΔTechRisk = researchProgress × techRiskFactor − stability × suppressionFactor`（基準非破壊・実効値パターン）。
- **接続**：`ResearchRules.Tick` の副産物として蓄積。`CollapseRules` のトリガー条件に参加。EventEngine で「技術暴走警告」を発火。

#### PHNX 歴史的循環パターン検出（`CyclicPatternRules`/`HistoricalPattern`）
- **`HistoricalPattern`**：過去に崩壊した勢力の状態スナップショット（TechRisk/正統性/版図一体化/希望）を保持。
- **`CyclicPatternRules.MatchScore`**：現在の `FactionState` と過去の崩壊パターンとの類似度（0〜1）を算出。
- **開示トリガー**：`MatchScore ≥ threshold` で `DisclosureLedger.TryReveal`（「この軌跡は以前にも起きた」開示チェーン）。
- **接続**：`DisclosureLedger`（FND-4）× `EventEngine`（#116）。コード量小・接続効果大。

### ★★ 高（既存システムへの崩壊軸の拡張）

#### PHNX 壊滅的人口崩壊と回復（`ExtinctionRules`/`CollapseRules` 拡張）
- **崩壊時**：`Collapsed` フェーズで `DemographicsRules` の通常動態ではなく `ExtinctionRules.CrashTick` を適用（人口が急激に減少・コホート破壊）。
- **回復曲線**：`Rebuilding` フェーズでは生産年齢人口が少ないため `DependencyRatio` が極端に悪化→ `OutputFactor` が著しく低い。時間で回復（S字カーブ）。
- **接続**：`DemographicsRules` × `CollapseRules`。`FiscalRules.WelfareCost`（人口オーナス連動）×`CampaignRules`。

#### PHNX 技術退行（`TechRegressionRules`）
- **現在の問題**：`ResearchRules` は技術レベルが単調増加する前提。崩壊後の再建文明が「前世代の技術を再発見する」軌跡が無い。
- **`TechLevel`**（絶対的な技術水準）：研究プロジェクトの累積で上昇。崩壊（`Collapsed`）で大幅後退（`regresionFactor` 倍）。
- **再発見ボーナス**：`Rebuilding` 中は一部の過去プロジェクトが「再発見」として低コストで再習得可能（`IsRediscovery`）。
- **接続**：`ResearchRules` × `CollapseRules`。`ShipyardRules.ProductionFactor` に `TechLevel` 係数を接続。

### ★ 中（lore・開示チェーン）

#### PHNX lore：宇宙的観察者の開示チェーン（コード新設なし）
- 「文明は繰り返す」「滅びは再生の始まり」「歴史の外から見れば全て一瞬」という世界観の深奥を `DisclosureLedger` のデータとして入力。
- 開示条件：`CollapseRules` による崩壊 → `CyclicPatternRules.MatchScore` による循環検出 → 「宇宙的観察者の視点」開示チェーン（連鎖開示）。
- 接続：**コード新設なし**。`DisclosureLedger`（FND-4）への lore データ入力のみ（CCX-6 方針に一貫）。

---

### ❌ 不採用（重複・既存で十分・著作権リスク・マイクロ操作）

| 不採用 | 理由 |
|---|---|
| 固有キャラ・固有名・固有設定の実装 | 著作権上の利用不可。構造パターンのみ参考 |
| 「血を飲むと不死」などの固有メカニクス | 著作権・ゲームシステムに合わない |
| 個人レベルの転生/輪廻追跡 | マイクロ操作の温床。`DisclosureLedger` の lore で十分 |
| 技術ツリーの可視的多段分岐 | タイクン化。`ResearchRules` の係数として背景的に効かせる |
| エピソード別のフラグ管理 | `EventEngine`（#116）で十分 |
| 文明ごとの個別史書生成 | `DisclosureLedger` で十分。重複新設しない |

---

## 3. EPIC #PHNX の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UI へ配線。既存ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**世界観の構造パターンのみ**参考。

> **EPIC = #2264**。GitHub issue 起票済み（#2266〜#2288）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **PHNX-1** | #2266 | 文明フェーズ（`CivilizationPhase`/`CollapseRules`）＝Rising/Mature/Declining/Collapsed/Rebuilding の5段階 | `DynastyRules.Revolution` → 文明スケール拡張。`FactionStateRules.Tick` から駆動。`CollapseRules` として test-first |
| **PHNX-2** | #2270 | 技術暴走リスク（`TechRiskRules`/`TechRisk`）＝研究進歩→危険蓄積、統治力で抑制、閾値超で崩壊トリガー | `ResearchRules` × `CollapseRules` × `EventEngine`。EditModeテスト必須 |
| **PHNX-3** | #2276 | 歴史的循環パターン検出（`CyclicPatternRules`/`HistoricalPattern`）＝崩壊パターンとの類似度→開示トリガー | `DisclosureLedger`（FND-4）× `EventEngine`（#116）。test-first |
| **PHNX-4** | #2280 | 壊滅的人口崩壊と回復（`ExtinctionRules`）＝崩壊フェーズでの人口急減曲線と再建S字回復 | `DemographicsRules`（LIFE-3）× `CollapseRules`（PHNX-1）。EditModeテスト必須 |
| **PHNX-5** | #2284 | 技術退行（`TechRegressionRules`）＝崩壊で技術水準が後退し再建文明が再発見する | `ResearchRules`（#123-127）× `CollapseRules`（PHNX-1）。EditModeテスト必須 |
| **PHNX-6** | #2288 | （lore）宇宙的観察者の開示チェーン（崩壊→循環検出→「歴史の反復」を示す開示連鎖） | `DisclosureLedger`（FND-4）データ入力のみ。コード新設なし |

### 推奨着手順

`PHNX-1`（文明フェーズ＝基盤・全体の骨格）→ `PHNX-2`（技術暴走＝崩壊の主因・最も固有）→ `PHNX-3`（循環パターン検出＝開示チェーンと接続）→ `PHNX-4`（人口崩壊＝DemographicsRules拡張）→ `PHNX-5`（技術退行＝ResearchRules拡張）→ `PHNX-6`（lore入力＝コード不要・最後）。

> PHNX-1 が他全ての基盤。`CollapseRules` が完成してから PHNX-2/4/5 を接続する。いずれも既存システムを**後退させず接続**する additive 設計。フェザーン（#160）・覇権交代（#228）・王朝サイクル（#867）に「文明スケールの縦軸」を供給する。
