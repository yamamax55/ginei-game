# ハイエク『隷属への道』参考設計（EPIC #HAYK）

> 参照元：F.A.ハイエク『隷属への道』（The Road to Serfdom, 1944）。  
> 中央計画経済がいかに政治的自由を蝕み全体主義へ収斂するか——「計画経済の滑り坂」と
> 「分散知識の問題」を核とした自由主義の古典的警告。  
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略＋既に巨大な政治・経済純ロジック層）にとって
> **役に立つ視点**だけを抽出し、EPIC `#HAYK` として issue 化する提案。  
> 著作権注意：固有名・文章・固有設定は流用せず、**世界観の構造パターンとメカニクスのみ**を参考にする。

---

## 0. なぜ「隷属への道」が本システムに役立つか

当プロジェクトは政治・経済の**純ロジック層を大量に保有**している（[CLAUDE.md] 参照）：

| 既存（カバー範囲） | 対応モジュール |
|---|---|
| 合意と統治力（協力×人口） | `ConsentRules`/`Polity`（#836） |
| 王朝腐敗・天命喪失・改革者 | `DynastyRules`/`Regime`（#867） |
| 文民統制・クーデターリスク | `CivilianControlRules`（GOV-4） |
| 秘密警察・弾圧・支持ペナルティ | `SecurityRules`/`SecurityApparatus`（#166） |
| 制約権力・課税同意・抵抗権 | `MagnaCartaRules`/`Charter`（#624） |
| 三権分立・専制リスク | `SeparationOfPowersRules`（#171） |
| 市場均衡・生活水準→支持 | `MarketRules`/`Good`/`Market`（#179-182） |
| 財政・国債・為替・税負担 | `FiscalRules`/`FiscalState`（#161-163） |
| 国家状態統合（安定度・崩壊判定） | `FactionStateRules`/`FactionState` |
| 政党・選挙 | `PartyRules`/`Party`（GOV-6） |
| 人物ライフサイクル・席次vs実力 | `SeniorityRules`/`CareerPipelineRules`（LIFE-5-7） |
| 内部勢力・省益 | `Party.factions`/`MinistryRules`（GOV-5） |

**しかし、これらは各メカニクスを独立して持つが、ハイエクが固有に描く以下が欠けている**：

| 隷属への道が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **計画経済ドリフト**：各介入が次の介入を必要とするラチェット | `Regime.corruption` は道徳的腐敗。**経済介入の累積→政治的支配の必然的拡大**という動学ループが無い |
| **計算問題**：価格なき計画は情報を失い生産性が落ちる | `MarketRules` は均衡を出す。**中央計画時の効率ペナルティ**（計算不可能性）が無い |
| **なぜワルモノが上に立つか**：全体主義体制での指導者選別圧力 | `LeadershipElectionRules` は民主型の得票。**権威主義体制が原則的指導者を排除し冷酷な者を選ぶ**メカニクスが無い |
| **法の一般性**：一般的・等しい法 vs 特定集団への恣意的命令 | `MagnaCartaRules` は抵抗権・課税同意。**法の一般性指数**（どれだけ法が普遍的に適用されるか）が無い |
| **経済的自由と政治的自由の連動**：経済統制度→政治的自由喪失→合意低下 | `FiscalRules.TaxBurdenPenalty` は税負担の不満。**経済統制の度合いが政治的自由に波及**する連結規則が無い |

**結論**：隷属への道は当プロジェクトの政治・経済層に  
①**計画ドリフトのラチェット**・②**計算問題の効率損失**・③**権威主義選別**・④**法の一般性**・⑤**経済・政治自由の連動**  
という5つの欠落軸を与える。いずれも**既存システムを後退させず接続する additive 設計**。  
特に「帝国の中央集権型政体」「計画経済を採る勢力」「長期独裁政権」の描写を深める。

---

## 1. 役に立つ視点（要約）

ハイエクの論点を、**本システムに効く形**で1行ずつ：

1. **計画経済の滑り坂**＝各介入が次の介入を必要とするラチェット。最終的に計画経済は政治的支配全体に至る。→ `FactionState` の政体ドリフトに**累積介入の動学**を加える。
2. **分散知識の問題（計算問題）**＝価格なしには中央計画者は必要な情報を集められず、生産性が低下する。→ `ResourceProductionRules`/`MarketRules` に**計画経済時の効率ペナルティ**を接続。
3. **なぜワルモノが上に立つか**＝全体主義体制は原則ある穏健派を弾き、目的のためなら手段を選ばぬ者を選別する。→ `LeadershipElectionRules`/`VacancyRules` に**体制選別バイアス**を接続。
4. **法の支配 vs 恣意的命令**＝法が一般的・予測可能でなくなると住民は合意を撤回する。→ `MagnaCartaRules`/`ConsentRules` に**法の一般性**という変数を追加。
5. **経済的自由は政治的自由の前提**＝経済統制度が高まるほど住民の協力は縮む。→ `ConsentRules`/`FactionStateRules` の係数として機能する連結ルール。
6. **自生的秩序の脆弱性**＝市場・慣習・規範は自然に生まれ、強制すると壊れ、修復コストは大きい。→ `MarketRules`/`CultureRules` に接続するlore。コード新設は最小。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`ConsentRules`/`MarketRules`/`FiscalRules`/`DynastyRules`/`SecurityRules` を作り直さない**。  
> HAYK はそれらに**欠落軸を足し、接続する**だけ（additive）。  
> タイクン化回避：新UIや新ループを増やさず、係数・規則として既存エンジンに差し込む。

### ★★★ 最優先（真の欠落・隷属への道のsignature）

#### HAYK 計画経済ドリフト（`PlanningDrift` / `PlanningDriftRules`）
- **介入累積モデル**：勢力が経済介入（配給・価格統制・産業国有化等）を重ねるほど  
  `interventionLevel`(0..1) が上昇し、上昇するほど次の介入を必要とする圧力 `ratchetPressure` が増す  
  （自己強化ラチェット＝自由に戻る摩擦が累積）。
- `PlanningDrift{interventionLevel, ratchetPressure}` + `PlanningDriftRules`:  
  `Tick(dt, voluntaryRetraction)`/`RatchetPressure(level)`/`AuthoritarianPressure(level)`/`IsLockIn(level, params)`
- `AuthoritarianPressure` は `DynastyRules.corruption` に累積し、`ConsentRules.Polity.cooperation` を侵食。
- `IsLockIn` 閾値超過で `SeparationOfPowersRules.IsGridlocked` との連動トリガー。
- 接続：`FactionState.Tick` → `PlanningDriftRules.Tick` → `DynastyRules` → `ConsentRules`
- **純ロジック・EditModeテスト必須。**

#### HAYK 計算問題と中央計画の効率損失（`CalculationProblemRules`）
- **情報損失モデル**：`interventionLevel`（HAYK-1 から）が高い勢力は価格シグナルを失い、  
  生産性が低下する：`EfficiencyFactor(interventionLevel)` = 1 − `maxLoss × f(interventionLevel)` (凹関数)。
- 完全市場(0) → ペナルティなし。完全計画(1) → `maxLoss`（既定 0.35）まで生産性を削る。
- 接続：`GovernanceRules.OutputFactor`/`ResourceProductionRules.Produce` の係数として差し込む。  
  `ShipyardRules.ProductionFactor` も同係数。  
  `MarketRules` の均衡価格は変えない（既存不変）。
- **純ロジック・EditModeテスト必須。**

### ★★★ 最優先（権威主義選別）

#### HAYK なぜワルモノが上に立つか（`AuthoritarianSelectionRules`）
- **指導者選別バイアス**：全体主義的政体（`CivilianControlType == 軍部優位 or 未分化`  
  かつ `PlanningDrift.interventionLevel` 高）では、候補者スコアを  
  「原則度（principle）」低い者＋「手段選ばず度（ruthlessness）」高い者が有利になるよう歪める。
- `AuthoritarianSelectionRules.SelectionBias(regime, candidates, params)` →  
  各候補の `EffectiveScore` を変調し `LeadershipElectionRules.Elect` / `VacancyRules.SelectSuccessor` に渡す。
- 候補スコアの変調：`biasedScore = baseScore × (1 − authBias × principleRatio) × (1 + authBias × ruthlessnessFactor)`。  
  `authBias`(0..1) は `PlanningDrift.AuthoritarianPressure` から導く。
- 接続：`LeadershipElectionRules`（GOV-7）/`VacancyRules`（LIFE-2）/`CivilianControlRules`（GOV-4）
- **純ロジック・EditModeテスト必須。**

### ★★ 高（法の一般性・経済政治連動）

#### HAYK 法の一般性と恣意的命令（`LegalGeneralityRules`）
- **法の一般性指数**：法がどれだけ「一般的（全員に等しく適用）・予測可能・恣意的でない」かを  
  `LegalGenerality{generality(0..1), equality(0..1), predictability(0..1)}` で表す。  
  `RuleOfLawIndex` = 三値の平均。
- `LegalGeneralityRules`:  
  `Erode(decree, targetGroup)` → 特定集団への恣意的命令で各値を削る。  
  `Restore(reformStrength)` → 法の普遍化で回復。  
  `ConsentPenalty(index)` → 低いほど `ConsentRules.Withdraw` を早める係数。  
  `ResistanceTrigger(index, params)` → 閾値割れで `MagnaCartaRules.ResistanceTriggered` 連動。
- 接続：`MagnaCartaRules`（#624）/`ConstitutionRules`（#170）/`SeparationOfPowersRules`（#171）/`ConsentRules`（#836）
- **純ロジック・EditModeテスト必須。**

#### HAYK 経済的自由と政治的自由の連動（`EconomicFreedomRules`）
- **連結係数**：`economicControl`(0..1) ＝ `PlanningDrift.interventionLevel`（HAYK-1）×`FiscalRules.TaxRate`の組み合わせ。  
  経済統制度が高まるほど `Polity.cooperation` が収縮し `FactionState.Stability` が低下する。  
  `FreedomFactor(economicControl)` = max(minFactor, 1 − strength × economicControl)。  
  `CooperationModifier(polity, economicControl)` → `ConsentRules.ControlStrength` への係数。
- この規則は**新データ構造を持たず純係数計算のみ**（超軽量）。
- 接続：`ConsentRules`（#836）/`FactionStateRules`（統合層）/`FiscalRules`（#161-163）/`PlanningDriftRules`（HAYK-1）
- **純ロジック・EditModeテスト必須。**

### ★ 中（自生的秩序・世界観lore）

#### HAYK 自生的秩序の脆弱性（`SpontaneousOrderRules`）
- **自生的秩序レベル**：市場・慣習・市民規範は自然に生まれ、  
  強制介入で `orderLevel` が低下し、修復は侵食より遅い（非対称）。  
  `SpontaneousOrder{orderLevel(0..1)}` + `SpontaneousOrderRules.Erode(interventionLevel, dt)`/`Restore(freedomFactor, dt)`/`MarketEfficiencyBonus(level)`。
- `MarketEfficiencyBonus` は `MarketRules.ClearingPrice` の精度に（将来）接続。  
  現時点では `CalculationProblemRules.EfficiencyFactor` に掛け算で合流。
- 接続：`MarketRules`（#179-182）/`CultureRules`（#194）/`CalculationProblemRules`（HAYK-2）
- **純ロジック・EditModeテスト必須。**

#### HAYK（lore）世界観の開示データ
- 「計画経済がいかに政治的支配に収斂するか」「自由なき経済は自由なき政治を生む」「自生的秩序が壊れると修復に数世代かかる」  
  を `DisclosureLedger` の秘史/真相エントリとして実装。
- **コード新設なし**。`SampleDisclosures` に追記するだけ。既存 `DisclosureLedger`（FND-4）に乗せる。

### ❌ 不採用（重複・既存で十分・ゲームに不要）

| 不採用 | 理由 |
|---|---|
| 市場均衡メカニクスの再実装 | **`MarketRules`（#179）が既にカバー**。HAYK は係数を足すだけ |
| ケインズ政策 vs ハイエク政策の論争モデル | ゲームに不要。`FiscalRules` で財政は既にカバー |
| 社会主義/資本主義の単純二項イデオロギー対立 | **既存 `FactionData.ideology`（専制/民主/etc.）で対応済み** |
| 全体主義体制の具体的歴史モデル（特定国家） | 著作権・設計上不要。抽象係数で足りる |
| 経済自由化政策レバー（民営化・規制撤廃） | マイクロ操作＝タイクン化。`PlanningDrift.interventionLevel` を動かす高位決断で十分 |
| 独占禁止・競争政策の詳細ルール | **`MarketRules`/`CommerceRaidingRules` で対応**。新規不要 |
| 報道規制・プロパガンダの独立モデル | **`SecurityRules.DissentSuppression` + `NonviolenceRules.Repress` で対応** |

---

## 3. EPIC #HAYK の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UI へ配線。  
> 既存政治・経済ロジックは**接続のみ・重複新設しない**。  
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #1539**。GitHub issue 起票済み（#1541〜#1559）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **HAYK-1** | #1541 | 計画経済ドリフト（`PlanningDrift`/`PlanningDriftRules`＝介入累積ラチェット→権威主義圧力） | `FactionState.Tick`→`DynastyRules.corruption`→`ConsentRules` |
| **HAYK-2** | #1544 | 計算問題と中央計画の効率損失（`CalculationProblemRules`＝価格なき計画→生産性ペナルティ） | `GovernanceRules.OutputFactor`/`ResourceProductionRules`/`ShipyardRules`の係数 |
| **HAYK-3** | #1547 | なぜワルモノが上に立つか（`AuthoritarianSelectionRules`＝全体主義体制の指導者選別バイアス） | `LeadershipElectionRules`/`VacancyRules`/`CivilianControlRules` |
| **HAYK-4** | #1549 | 法の一般性と恣意的命令（`LegalGeneralityRules`＝`RuleOfLawIndex`→合意撤回・抵抗権連動） | `MagnaCartaRules`/`ConstitutionRules`/`SeparationOfPowersRules`/`ConsentRules` |
| **HAYK-5** | #1553 | 経済的自由と政治的自由の連動（`EconomicFreedomRules`＝経済統制度→協力係数→安定度） | `ConsentRules`/`FactionStateRules`/`FiscalRules`/HAYK-1 |
| **HAYK-6** | #1556 | 自生的秩序の脆弱性（`SpontaneousOrderRules`＝強制介入→自生的秩序侵食→市場効率低下） | `MarketRules`/`CultureRules`/HAYK-2 |
| **HAYK-7** | #1559 | （lore）世界観の開示データ（計画→全体主義の帰結・自由の不可分性・自生的秩序の修復コスト） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順

`HAYK-1`（計画ドリフト＝最も固有で欠落の大きい signature・後続の全issueの基礎）  
→ `HAYK-2`（計算問題＝HAYK-1の `interventionLevel` を使う効率損失）  
→ `HAYK-3`（権威主義選別＝HAYK-1の `AuthoritarianPressure` を `LeadershipElectionRules` に接続）  
→ `HAYK-4`（法の一般性＝独立した純ロジック・`ConsentRules` への新接続）  
→ `HAYK-5`（経済政治連動＝HAYK-1+FiscalRules の係数統合）  
→ `HAYK-6`（自生的秩序＝HAYK-2への軽量追加）  
→ `HAYK-7`（lore投入・コード不要）。

> いずれも既存政治・経済・統治ロジックを**後退させず接続**する additive 設計。  
> 特に「帝国の中央集権政体」「長期独裁政権」「計画経済勢力」の描写を深める。
