# ケネディ『大国の興亡』参考設計（EPIC #KEN）

> 参照元：ポール・ケネディ『大国の興亡』（The Rise and Fall of the Great Powers, 1987）。
> 500年にわたる大国の盛衰を「経済力⇔軍事力の転換」と「過剰拡張」で説明する歴史社会科学。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略）にとって
> **役に立つ視点**だけを抽出し、EPIC `#KEN` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、
> **社会科学の構造パターン（メカニクス）のみ**を参考にする。

---

## 0. なぜ「大国の興亡」が本システムに役立つか

当プロジェクトは国家経済・財政・軍事の純ロジック層を大量に保有している：

| 既存（マクロ・抽象） | カバー範囲 |
|---|---|
| `FiscalRules.IsDebtSpiral` | 赤字→国債→利払い複利膨張 |
| `FiscalRules.TaxBurdenPenalty` | 高税→支持低下 |
| `LogisticsRules.CohesionFactor` | 版図断片化→実効国力低下 |
| `CampaignRules.EffectiveStability` | 安定度×版図一体化＝実効国力指標 |
| `DynastyRules` / `FactionStateRules` | 正統性腐敗→王朝崩壊サイクル |
| `BalanceOfPowerRules`（SGZ-1） | 最強勢力台頭→弱小連衡 |
| `ResearchRules` | 技術革新の枠組み |
| `ShipyardRules.ProductionFactor` | 安定度比例の生産係数 |

**しかし、ケネディが固有に示す以下が欠けている：**

| 大国の興亡が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **過剰拡張比率**（軍事支出 / 産出）がある閾値を超えると成長を蝕む | `FiscalRules` は赤字→債務スパイラル。**均衡予算でも軍事比率が高すぎると成長ペナルティ**が生じる仕組みが無い |
| **生産的投資のクラウドアウト**（軍備が資本形成を食う） | `FiscalRules` は軍事 vs 福祉。**投資支出（将来産出力）を別カテゴリ**として軍備と競合させる回路が無い |
| **相対的国力份額**（世界 GDP に占めるシェア）で衰退を測る | `CampaignRules.EffectiveStability` は自国の安定度×版図。**全勢力合計に占める自勢力の份額**という相対指標が無い |
| **経済力→軍事力の転換ラグ**（経済が先に衰退し軍事が遅れて追随する） | 経済・軍事の指標は同期している。**「既存の軍艦は残るが新造が止まる」転換ラグと戦略的誤算窓口**が無い |
| **技術波動の早期採択優位**（波に乗った勢力が生産力で覇権を掴む） | `ResearchRules` 枠組みは存在。**波のタイミングと早期採択ボーナスを `ShipyardRules` へ接続する経路**が無い |

**結論**：ケネディは当プロジェクトの財政・経済・軍事に
**「過剰拡張が衰退のメカニズムである」という 5 つの欠落軸**を与える。
`FiscalRules`・`ResearchRules`・`CampaignRules` に横断的に効く純ロジック拡張として最適。

---

## 1. 役に立つ視点（要約）

大国の興亡の世界観を、**本システムに効く形**で1行ずつ：

1. **「経済力が軍事力の土台」＝産出量の相対份額が国際秩序の実質的決定因**。
   銀河の大勢力が「なぜ強いのか」を数値で説明できるようになる。
   → `CampaignRules.LeadingFaction` に份額ベースの根拠を与える。

2. **過剰拡張（インペリアル・オーバーストレッチ）＝軍事コミットが経済力を超えると、
   防衛費が成長を食いつぶす自己破壊的なスパイラルに入る**。
   銀河帝国型の巨大勢力が「なぜ崩れるのか」を決定論的に描ける。
   → `FiscalRules` 拡張の新軸。

3. **相対的衰退＝絶対成長でも份額が縮めば没落**。
   勝ち戦を続けながら長期で相対的に弱くなる逆説を実装できる。
   → `HegemonyRules` 新設の核。

4. **転換ラグ＝経済が先に崩れ、軍事が遅れて崩れる**。
   既存の艦隊はあるが新造が止まる「危険な窓口」に、傲慢な開戦動機が生まれる。
   → `PowerConversionRules` の骨格。

5. **技術波動の早期採択が次の覇権国を決める**（産業革命→工業国覇権 等）。
   研究の先行投資が 10 年後の生産力差を生む。
   → `ResearchRules` × `ShipyardRules.ProductionFactor` 接続。

6. **覇権の移行は平和裏でなく危機を経る傾向がある**（権力移行理論）。
   相対份額が逆転する前後に対立が激化する構造を世界観 lore と接続できる。
   → `DisclosureLedger`（FND-4）の秘史データ入力。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`FiscalRules`・`DynastyRules`・`LogisticsRules`・`BalanceOfPowerRules`(SGZ-1) を作り直さない**。
> KEN はそれらに**欠落軸を足し、接続する**だけ（additive）。

### ★★★ 最優先（ケネディの signature・真の欠落）

#### KEN 過剰拡張比率（`OverstretchRules`）
- **`OverstretchRatio`**＝軍事支出 / 産出（`FiscalState.Expenditure` の軍事部分 / GDP相当）。
- **`OverstretchPenalty(ratio)`**：閾値（既定 0.15）を超えた分が `ShipyardRules.ProductionFactor` に負の乗数として加わる。
  均衡予算でも軍事負荷が高すぎると将来産出力を蝕む＝`FiscalRules.IsDebtSpiral` とは独立した衰退ルート。
- **`IsOverstretched(ratio)`**：boolean ゲート。`GovernanceRules.RebelPressure`・`CampaignRules.EffectiveStability` への警告フラグ。
- 接続：`FiscalRules`（支出）→ `OverstretchRules`（比率）→ `ShipyardRules.ProductionFactor`（成長ペナルティ）。
- 純ロジック・EditMode テスト必須。

#### KEN 生産的投資クラウドアウト（`CapitalFormationRules`）
- **投資支出カテゴリ** を `FiscalState` に追加（軍事/福祉/行政/投資の4分法）。
  投資 → `ProductivityGrowthRate`（次ターンの産出力成長率）。
- **`CrowdingOut(military, investment)`**：軍事支出が増えると投資が圧迫される線形係数。
  「兵器に使った金は工場に使えない」＝長期成長vs短期安全保障のジレンマ。
- 接続：`FiscalRules`（支出構成）→ `CapitalFormationRules`（投資率・成長率）→ `ShipyardRules`（将来生産力）。
- `FiscalRules` 既存フィールドを後退させない（additive 拡張）。
- 純ロジック・EditMode テスト必須。

### ★★ 高（相対指標・転換ラグ）

#### KEN 銀河覇権指数（`HegemonyRules`）
- **`WorldShare(faction, allFactions)`**：全勢力の産出合計に占める一勢力の份額（0..1）。
- **`IsHegemon(share, threshold=0.40)`**：boolean。`BalanceOfPowerRules`(SGZ-1) の既存連衡圧力をより精密な閾値で駆動できる。
- **`RelativeDeclineRate`**：前期份額 − 当期份額。マイナスは相対上昇。絶対成長でも份額縮小なら相対衰退。
- 接続：`CampaignRules`（全勢力 Tick）→ `HegemonyRules`（份額計算）→ `BalanceOfPowerRules`(SGZ-1)（連衡圧力のトリガー）。
- 純ロジック・EditMode テスト必須。

#### KEN 経済力→軍事力の転換ラグ（`PowerConversionRules`）
- **`EconomicPowerIndex(faction)`**：産出 × 版図一体化（`LogisticsRules.CohesionFactor`）。
- **`MilitaryPowerIndex(faction)`**：現有艦隊戦力（`FleetPool`）+ 造船中の艦（`ShipyardRules` キュー残）。
- **`ConversionLag(econ, mil)`**：軍事指標が経済指標を上回る比率。高いほど「過去の栄光で戦っている」状態。
  `IsInDangerousWindow(lag, threshold=0.20)` が true の勢力は
  予防戦争動機（`WarGoalRules` 動機スコア増幅）を持ちやすい。
- 接続：`OverstretchRules`(KEN-1) × `HegemonyRules`(KEN-3) × `WarGoalRules` × `DiplomacyRules`。
- 純ロジック・EditMode テスト必須。

### ★ 中（技術波動・世界観 lore）

#### KEN 技術波動と早期採択優位（`TechWaveRules`）
- **`TechWave`**：ゲーム内で一定間隔（既定 40〜60 年相当の game-time）ごとに到来する技術革新フロント。
- **`AdoptionBonus(researchLevel, waveFront)`**：先行研究済みの勢力が新波動期に `ShipyardRules.ProductionFactor` を +20% 修正子として受け取る。
  採択が遅れた勢力は次波動まで割り引き。
- 接続：`ResearchRules`（研究進捗）→ `TechWaveRules`（採択判定）→ `ShipyardRules.ProductionFactor`（生産係数）→ `HegemonyRules`（份額変動の加速）。
- `ResearchRules` 既存フィールドを後退させない。
- 純ロジック・EditMode テスト必須。

#### KEN（lore）覇権移行と秘史開示
- 「なぜ大帝国は繁栄の絶頂で没落し始めるのか」「産業革命を逃した勢力はなぜ滅んだのか」。
- コード新設せず `DisclosureLedger`（FND-4）への**lore データ入力**。
- 接続：**コード変更なし**。

### ❌ 不採用（重複・既存で十分・タイクン化回避）

| 不採用 | 理由 |
|---|---|
| 均衡連合（バランシング・コーリション）新設 | **SGZ-1 `BalanceOfPowerRules`** が「最強勢力→連衡」をカバー。KEN-3 がより精密なトリガーを供給する（接続のみ） |
| 国債・利払い複利膨張 | **`FiscalRules.IsDebtSpiral`** が完全にカバー。重複新設しない |
| 版図断片化→国力低下 | **`LogisticsRules.CohesionFactor`** がカバー |
| 王朝崩壊サイクル | **`DynastyRules`/`FactionStateRules`** がカバー |
| 政戦連接・厭戦ペナルティ | **`CLZ-2`（`WarGoalRules` 配線）** がカバー |
| 地政学的要衝の支配 | **`GEO` EPIC** で対応予定 |
| 大規模内政マイクロ（産業革命の逐次再現）| タイクン化回避。係数で背景的に効かせる（BUILD-2/ProductionFactor 方針に一貫） |
| 個別勢力の経済ビルドオーダー | タイクン化回避。`CampaignRules.Tick` の自動化で発現 |

---

## 3. EPIC #KEN の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UI へ配線。
> 既存財政・研究ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**社会科学の構造パターンのみ**参考。

> **EPIC = #1321**。GitHub issue 起票済み（#1324〜#1330）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **KEN-1** | #1324 | 過剰拡張比率（`OverstretchRules`）＝軍事支出/産出が閾値超で成長にペナルティ | `FiscalRules`→新`OverstretchRules`→`ShipyardRules.ProductionFactor` |
| **KEN-2** | #1327 | 生産的投資クラウドアウト（`CapitalFormationRules`）＝軍備が資本形成を食う | `FiscalState` 投資カテゴリ追加。`CapitalFormationRules`→`ShipyardRules` 成長係数 |
| **KEN-3** | #1328 | 銀河覇権指数（`HegemonyRules`）＝全勢力份額・相対的衰退レート | `CampaignRules`×`LogisticsRules`→新`HegemonyRules`→SGZ-1 `BalanceOfPowerRules` |
| **KEN-4** | #1329 | 経済力→軍事力の転換ラグ（`PowerConversionRules`）＝危険な窓口と予防戦争動機 | KEN-1/3×`WarGoalRules`×`DiplomacyRules` |
| **KEN-5** | #1330 | 技術波動と早期採択優位（`TechWaveRules`）＝波に乗った勢力の生産力跳躍 | `ResearchRules`→新`TechWaveRules`→`ShipyardRules.ProductionFactor`→`HegemonyRules` |

### 推奨着手順

`KEN-1`（過剰拡張の診断コア）→ `KEN-2`（クラウドアウト＝衰退のメカニズム）→ `KEN-3`（相対份額で衰退を可視化）→ `KEN-4`（転換ラグ×予防戦争）→ `KEN-5`（技術波動＝覇権の代替わりドライバ）。

> KEN-1〜2 で「過剰拡張とはなにか」を純ロジックで固め、
> KEN-3 で「衰退を相対的に測る物差し」を作ってから、
> KEN-4 で「危険な窓口＝傲慢な開戦」に配線し、
> KEN-5 で「次の覇権が技術から生まれる」サイクルを閉じる。
> いずれも既存財政・研究ロジックを**後退させず接続**する additive 設計。
