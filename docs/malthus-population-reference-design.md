# マルサス『人口論』参考設計（EPIC #MALT）

> 参照元：トマス・ロバート・マルサス『人口論』（1798/1803）。人口は等比級数的に増え食糧は等差級数的にしか増えない——その非対称が「成長の天井」を生み、賃金は常に生存水準へ引き戻される。
> 本ドキュメントは、当プロジェクト（Ginei）にとって**役に立つ視点**だけを抽出し、EPIC `#MALT` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**人口・食糧・経済のメカニクス構造パターンのみ**を参考にする。

---

## 0. なぜ「人口論」が本システムに役立つか

当プロジェクトは人口・農業・経済の**マクロ純ロジックを既に保有**している：

| 既存（マクロ・抽象） | カバー範囲 |
|---|---|
| `DemographicsRules`/`Population` (#153) | 3コホート人口・出生/死亡率・生産ボーナス/オーナス・`Tick` |
| `LifecycleRules` (#151/152) | 年齢別死亡率カーブ・老衰判定 |
| `ResourceProductionRules` (#93) | 星系の農業/工業/鉱業/居住 `SystemType` 別産出 |
| `FiscalRules`/`FiscalState` (#161/162) | 福祉コスト・財政健全度・生活水準 |
| `FiscalClass`/`RedistributionRules` (#163) | 階級別税率・再分配・階級対立 |
| `GovernanceRules` (#109) | 安定度×産出倍率・反乱圧力 |

**しかし、これらは人口と食糧を独立したモジュールとして扱っており、マルサスが固有に描く以下が欠けている**：

| 人口論が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **食糧天井 (Carrying Capacity)** | `DemographicsRules.Tick` は出生/死亡率を食糧産出と無関係に計算。農業産出が人口の上限を決める天井関数が無い |
| **正のチェック（飢饉→死亡率↑）** | `LifecycleRules` の死亡率は年齢曲線のみ。食糧不足が死亡率を跳ね上げる「欠乏強度」変調が無い |
| **予防的チェック（欠乏予期→出生率↓）** | 出生率は人口動態の内部変数のみ。資源欠乏への予防的な出生抑制が無い |
| **生存賃金への収束** | `FiscalClass` は階級別賃金を持つが、人口密度が高まると賃金が生存水準へ収束する圧力が無い |
| **貧者救済のパラドックス (Poor Law Effect)** | `FiscalRules.WelfareCost` は福祉支出コストを持つが、「救済→人口増→賃金低下→救済効果消滅」のフィードバックループが無い |

**結論**：人口論は当プロジェクトの人口・農業・経済モジュールに**「食糧天井」と「人口の自己調整」という因果の骨格**を与える。既存の `DemographicsRules`×`ResourceProductionRules`×`FiscalClass` を**繋ぐ接着剤**として機能し、新規のマクロ構造は一切増やさない（additive）。

---

## 1. 役に立つ視点（要約）

人口論の世界観を、**本システムに効く形**で1行ずつ：

1. **人口は等比・食糧は等差** — 農業産出が伸びるより速く人口が増え、差が天井を生む。→ `Province.population` × `SystemType.農業` の産出で上限係数を決める `CarryingCapacityRules` が要る。
2. **正のチェック（飢饉・疫病）** — 天井を超えると死亡率が跳ね上がる。→ `DemographicsRules.VitalRates` に欠乏強度 `FoodStressRatio` からの変調を追加（基準値非破壊）。
3. **予防的チェック（道徳的抑制）** — 欠乏を予期して出生率が下がる。→ 同じ欠乏強度で出生率を抑制（二経路のチェック）。
4. **生存賃金への引力** — 余剰が人口増を誘い、人口増が賃金を生存水準へ引き戻す。→ 人口密度/天井比 → `FiscalClass` の実効賃金への下方圧迫係数。
5. **救済のパラドックス** — 貧者へ施せば人口が増え、長期的に賃金が元に戻る。→ `FiscalRules.WelfareCost` の高さが出生刺激係数を上げ、政策ジレンマを創発させる。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`DemographicsRules`/`ResourceProductionRules`/`FiscalRules` を作り直さない**。MALT はそれらに**欠落軸を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・人口論のsignature）

#### MALT 食糧天井（Carrying Capacity）
- **`CarryingCapacityRules`**（新 static 純ロジック）：農業 `SystemType` の産出量 + `Province.population` から**天井比 `FoodStressRatio`（0..∞、1.0=適正）**を返す純関数。
- `FoodStressRatio > 1.0` = 人口が食糧を超えて圧迫、`< 0.8` = 余裕あり（移民/人口増の動機）。
- 接続：`ResourceProductionRules.Produce` の農業産出 × `Province.population` → 純計算・完全 test-first。**`DemographicsRules` は一切書き換えない**。

#### MALT マルサスチェック（出生・死亡の変調）
- `DemographicsRules.VitalRates` を**実効値パターン**で変調：`FoodStressRatio` が高いほど死亡率↑（正のチェック）・出生率↓（予防的チェック）。基準値フィールドは非破壊。
- 係数式：`PositiveCheckFactor(stress)` / `PreventiveCheckFactor(stress)` を `CombatModifiers` 流儀の ModifierStack で積む（#106 公式に合流可）。
- 接続：`DemographicsRules` × `CarryingCapacityRules`。EditMode テスト必須（stress=0/1/1.5 の各係数を固定）。

### ★★ 高（マクロ均衡に動学の遊びを足す）

#### MALT 生存賃金の収束圧（Subsistence Wage Pressure）
- 人口/天井比 `FoodStressRatio` → `FiscalClass` の**実効賃金フロア**に下方圧力。余剰が出ると人口が増え、賃金が生存水準へ引き戻される。
- 接続：`CarryingCapacityRules.FoodStressRatio` × `RedistributionRules` / `FiscalClass.EffectiveTaxRate`。新フィールド増設なし — 係数として乗せるだけ。
- 実効値パターン（基準賃金非破壊）。

#### MALT 貧者救済のパラドックス（Poor Law Effect）
- 福祉水準 `FiscalState.welfare` が高いほど**出生刺激係数が上がる**。短期は `WelfareHopeBonus`（`HopeRules`）を上げるが、長期は `FoodStressRatio` 悪化→マルサスチェック→賃金低下で帳消し。
- 接続：`FiscalRules.WelfareCost` × MALT-2 チェック係数。**政策ジレンマの創発**（高福祉＝短期安定・長期人口過剰）。
- 実効値パターン（基準出生率非破壊）。新ロジック追加のみ。

### ★ 中（世界観lore）

#### MALT（lore）世界観の開示データ
- 「成長には天井がある（食糧が限界を決める）」「救済は人口を増やし、長期には意味を失う」「賃金は常に生存水準へ引力される」。
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への**lore データ入力**。既存開示パイプラインへのデータ追記のみ。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 人口動態そのもの（コホート/Tick） | **`DemographicsRules`/`Population` #153 がカバー**。MALT は変調係数を足すだけ |
| 年齢別死亡率カーブ | **`LifecycleRules` #151/152 がカバー**。死亡率変調は係数として重ねる |
| 農業産出それ自体 | **`ResourceProductionRules` #93 + `SystemType.農業` がカバー**。MALT は産出値を読むだけ |
| 戦争による人口減 | **戦闘システム/`BattleManager` が既に処理**。正のチェックは飢饉/疫病経路のみ足す |
| 新たな市場/財政クラス | `MarketRules`/`FiscalRules`/`FiscalClass` がカバー。MALT は係数接続のみ |
| 移民/植民の物理 | `ColonizationRules` #129 がカバー。天井が誘因を与えるが実装は既存を使う |

---

## 3. EPIC #MALT の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面配線。既存人口/農業/財政ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #1573**。GitHub issue 起票済み（#1574〜#1583）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **MALT-1** | #1574 | 食糧天井関数 `CarryingCapacityRules`（農業産出×人口→`FoodStressRatio`） | 新 `CarryingCapacityRules`。`ResourceProductionRules`×`Province.population` |
| **MALT-2** | #1575 | マルサスチェック（`FoodStressRatio`→出生率↓・死亡率↑の変調係数） | `DemographicsRules.VitalRates` × MALT-1。実効値パターン・EditModeテスト必須 |
| **MALT-3** | #1577 | 生存賃金の収束圧（人口密度→`FiscalClass`実効賃金への下方圧迫） | `RedistributionRules`/`FiscalClass` × MALT-1係数。基準賃金非破壊 |
| **MALT-4** | #1580 | 貧者救済のパラドックス（福祉水準→出生刺激→長期に帳消し） | `FiscalRules.WelfareCost` × MALT-2チェック係数。政策ジレンマの創発 |
| **MALT-5** | #1583 | （lore）開示データ（成長の天井/救済の逆説/生存水準への引力） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`MALT-1`（食糧天井関数＝最も固有で欠落の大きい signature）→ `MALT-2`（マルサスチェック＝天井を人口動態に接続）→ `MALT-3`（生存賃金圧迫＝財政層への伝播）→ `MALT-4`（救済パラドックス＝政策ジレンマの創発）→ `MALT-5`（lore）。

> いずれも既存人口/農業/財政ロジックを**後退させず接続**する additive 設計。農業型惑星/星系のある戦略マップに最も効く。
