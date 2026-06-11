# モンテスキュー『法の精神』参考設計（EPIC #MONT）

> 参照元：モンテスキュー『法の精神』（De l'esprit des lois, 1748）。共和制・君主制・専制の三類型と、各政体を支える「原動力」、風土×法の適合、中間権力による専制防止、通商と温和政治を体系化した政治哲学の古典。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略＋既に巨大な政治・社会純ロジック層）にとって**役に立つ視点**だけを抽出し、EPIC `#MONT` として issue 化する提案。
> 著作権注意：固有名・文章・固有設定は流用せず、**政治哲学のメカニクス／世界観の構造パターンのみ**を参考にする。

---

## 0. なぜ「法の精神」が本システムに役立つか

当プロジェクトは政治・社会の**純ロジックを大量に保有**している（[CLAUDE.md] 参照）：

| 既存（政治・社会層） | カバー範囲 |
|---|---|
| `SeparationOfPowersRules` (#171) | 三権分立・`TyrannyRisk`・`IsGridlocked` |
| `DynastyRules`/`Regime` (#867) | 腐敗→正統性低下→改革/易姓革命 |
| `ConsentRules`/`Polity` (#836) | 被支配者の協力と非協力（ガンジー型） |
| `FactionStateRules`/`FactionState` | 王朝/統治体/組織/共同体の合成 |
| `FeudalRules`/`Fief` (#168/169) | 徴募・領主反乱リスク・門地開放 |
| `GovernanceRules` (#109) | 安定度・思想一致・統合・産出係数 |
| `ReligionRules` (#172-175) | 改宗圧力・異端・聖戦・社会効果 |
| `LogisticsRules` (#844) | 版図の地理的一体化度（連結成分） |
| `MarketRules`/`FiscalRules` | 市場均衡・財政・為替 |
| `CoupRules` (#215-219) | クーデター成功率・事後正統性 |

**しかし、これらは「国家の構造と均衡」を扱いながら、法の精神が固有に描く以下が欠けている**：

| 法の精神が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **政体の原動力（徳・名誉・恐怖）** | 各政体を支える**心理的動機**が未モデル。`Regime`/`Polity` は腐敗と合意を追うが「なぜ市民が服従するか」の政体別原理が無い |
| **風土と政体の相性** | `LogisticsRules` は地理的結合度のみ。**惑星環境/文化的気質 × 政体の適合係数**が無い |
| **中間権力（貴族・中間団体）** | `FeudalRules` は封建徴募と反乱だけ。**三権と民衆の間に立つ緩衝体**としての中間権力がモデル化されていない |
| **法の適合性（慣習・風土・経済との整合）** | `GovernanceRules` は思想一致だけ。**法が地元の慣習・産業・歴史に合っているか**という適合係数が無い |
| **諸政体の腐化回路（型別の崩壊経路）** | `DynastyRules` は汎用腐敗。**共和制→寡頭/衆愚・君主制→専制・専制→崩壊**という政体固有の退化経路が無い |
| **通商が政治を温和にする（商業平和論）** | `MarketRules` は均衡のみ。**交易量が多いほど戦争忌避/温和政治が強まる**フィードバック回路が無い |

**結論**：法の精神は当プロジェクトの政治・社会シミュに**「なぜ人は服従するか」という原動力の次元**と、**①政体原動力 ②風土適合 ③中間権力 ④法適合性 ⑤型別腐化回路 ⑥通商平和**という6つの欠落軸を与える。三権分立 (#171) の「原典」として、既存システムを後退させず**深みを与える**。

---

## 1. 役に立つ視点（要約）

法の精神の世界観を、**本システムに効く形**で1行ずつ：

1. **政体は「原動力」で持つ**——共和制は市民の徳（自己犠牲）、君主制は臣下の名誉（誇り）、専制は恐怖。原動力が腐ると政体は崩れる。→ 既存 `Regime`/`FactionState` に**服従動機の軸**を足す。
2. **風土が法と政体を決める**——寒冷地は自由の気風、熱帯は服従の気風。政体は風土に適合してこそ安定する。→ `GovernanceRules` に**惑星環境×政体適合係数**を追加。
3. **中間権力（貴族・中間団体）が専制を防ぐ**——王と民の間に緩衝体が無ければ君主制は専制に滑落する。三権分立だけでは不十分。→ `SeparationOfPowersRules`/`FeudalRules` に**緩衝体強度**を接続。
4. **法は風土・慣習・経済に適合すべき**——輸入された法が地元の精神と合わないと正統性が低下し安定を損なう。→ `GovernanceRules.IdeologyModifier` の拡張。
5. **各政体は固有の経路で腐化する**——共和制は富で徳が失われ寡頭制へ、君主制は名誉が消えて専制へ。腐化経路は政体によって違う。→ `DynastyRules`/`Regime` の型分岐拡張。
6. **通商は戦争を不合理にする**——互いに依存すると戦争のコストが利益を超える。貿易が多いほど温和な政治が選ばれる。→ `MarketRules`×`DiplomacyRules`×`WarGoalRules`。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`SeparationOfPowersRules`(#171)・`DynastyRules`(#867)・`ConsentRules`(#836)・`FeudalRules`(#168/169)・`GovernanceRules`(#109)・`MarketRules`(#179) を作り直さない**。MONT はそれらに**欠落軸を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・法の精神の signature）

#### MONT 政体の原動力（徳・名誉・恐怖）— `GovernmentPrincipleRules`
- **原動力モデル**：`GovernmentPrinciple` (enum: 徳/名誉/恐怖/未定) + `PrincipleStrength` (0..1)。
- 原動力の強さは `FactionState.stability` に基礎寄与：徳は市民の自発的参加、名誉は階層的競争、恐怖は即効だが腐食する（`PrincipleDecayRate`が恐怖のみ高い）。
- **服従コスト**：`ConsentRules.ControlStrength` の算出に `PrincipleStrength` を乗算——原動力が高いほど少ない力で多くを動かせる（ガンジー#836と連携）。
- 接続：`FactionState`/`Regime`/`ConsentRules`/`OfficeRules`（政体からの役職適合判定への係数）。

#### MONT 諸政体の腐化回路（型別崩壊経路）— `PolityCorrputionRules`
- `DynastyRules.Tick`（汎用腐敗）に**政体別分岐**を足す：共和制→`WealthCorruptsVirtue`（富が増えるほど徳が低下→寡頭制 or 衆愚制）、君主制→`HonorErosionToTyranny`（名誉コードが失われると専制滑落）、専制→`FearCollapseAcceleration`（恐怖は内部から崩れ急加速）。
- `PolityDegeneracy` (0..1)：現政体が腐化した版へどれだけ近づいているかの指標。閾値超えで `EventEngine` に「政体転換リスク」発火。
- 接続：`DynastyRules`/`Regime`/`FactionStateRules`/`EventEngine`(#116)。純ロジック・test-first。

### ★★ 高（既存への重要な補完）

#### MONT 風土と政体の相性 — `ClimatePolityFitRules`
- `ClimateType` (enum: 寒冷/温帯/熱帯/宇宙的極端) × `GovernmentType` (共和/君主/専制) → 適合係数 (0..1)。
- 惑星/星系 `Province` に `climate` フィールドを追加（既定=温帯=後方互換）。
- 適合係数は `GovernanceRules.EquilibriumStability` の係数として乗算（基準値非破壊）。
- 接続：`GovernanceRules`/`Province`/`Planet`/`FactionState`。純ロジック・test-first。

#### MONT 中間権力の緩衝強度 — `IntermediatePowerRules`
- 既存 `FeudalRules`/`Fief` は徴募と反乱だけ——**君主（国家）と市民の間の緩衝体**としての機能が欠けている。
- `IntermediatePowerStrength` (0..1)：貴族・ギルド・都市自治・宗教法人など中間団体の強さ。
- 高い → `SeparationOfPowersRules.TyrannyRisk` を低下させる（構造的抑止）。低い → 君主制は専制へ滑落しやすくなる（`PolityCorruptionRules.HonorErosionToTyranny` 加速）。
- 接続：`SeparationOfPowersRules`(#171)・`FeudalRules`(#168)・`PolityCorruptionRules`(MONT)。

#### MONT 法の適合性 — `LegalFitnessRules`
- `LegalFitness` (0..1)：勢力の現行法・制度が、惑星環境・住民思想・産業構造と整合している度合い。
- 算出：`ClimatePolityFit`×政体 + `GovernanceRules.IdeologyModifier`×思想一致 + 産業適合 → 加重平均。
- 高い → `GovernanceRules.OutputFactor` へ正の係数。低い → 反乱圧力 `RebelPressure` 増加（正統性不足）。
- 接続：`GovernanceRules`/`Province`/`FactionState`。純ロジック・test-first。

### ★ 中（マクロ動学の補完）

#### MONT 通商と温和政治（商業平和論） — `CommerceModeratesWarRules`
- `CommercePeaceFactor` (0..1)：双方勢力の貿易量（`MarketRules`/`GalaxyMap`回廊上の交易量）が多いほど高い。
- `CommercePeaceFactor` が高いほど `WarGoalRules.WarWeariness` が速く蓄積し、戦争が不合理になる。専制政体は交易を抑圧するので低い（`GovernmentPrinciple` 恐怖と連動）。
- タイクン化回避：係数のみ——マイクロ操作を増やさない。
- 接続：`MarketRules`(#179)/`DiplomacyRules`(#189)/`WarGoalRules`(#192)/`GovernmentPrincipleRules`(MONT)。

#### MONT（lore）世界観の開示データ
- 「法の精神とは恐怖でなく徳にある」「風土が人を作り、人が法を作る」「通商は平和の根」などの啓蒙思想を **コード新設せず** `DisclosureLedger`（FND-4）への**lore データ入力**として追加。
- 世界観EPIC（啓蒙/ニーチェ/秘史）と接続。
- 接続：`DisclosureLedger`(FND-4)。コード新設なし。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 三権分立そのものの実装 | **`SeparationOfPowersRules`(#171)が既にカバー**（`CheckBalance`/`TyrannyRisk`/`IsGridlocked`）。MONT は深みを足すのみ |
| 封建制の実装 | **`FeudalRules`(#168/169)がカバー**。中間権力はその**機能面の補完**のみ |
| 選挙・政党システム | **`PartyRules`(GOV-6)が既にカバー**。MONT は選挙論ではなく政体原動力論 |
| 宗教×政治の連携 | **`ReligionRules`(#172-175)がカバー**。MONT は宗教を「中間権力の一種」として接続するだけ |
| 立憲主義そのもの | **`ConstitutionRules`(#170)がカバー**（`ConstrainedAuthority`/`RightsLegitimacy`）。MONT は法適合性として補完 |
| 詳細な気候シミュ | 星系環境の物理シミュは作らない（タイクン化回避）。係数のみ |

---

## 3. EPIC #MONT の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。既存政治ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章は不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #1438**。GitHub issue 起票済み（#1439〜#1457）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **MONT-1** | #1439 | 政体の原動力（`GovernmentPrincipleRules`：徳/名誉/恐怖×PrincipleStrength→服従コスト係数） | `FactionState`/`Regime`/`ConsentRules`。純ロジック |
| **MONT-2** | #1440 | 諸政体の腐化回路（`PolityCorruptionRules`：共和制→寡頭、君主制→専制、専制→崩壊の型別分岐） | `DynastyRules`/`Regime`/`EventEngine`。純ロジック |
| **MONT-3** | #1443 | 風土と政体の相性（`ClimatePolityFitRules`：惑星環境×政体→安定度係数） | `GovernanceRules`/`Province`/`Planet`。純ロジック |
| **MONT-4** | #1446 | 中間権力の緩衝強度（`IntermediatePowerRules`：中間団体強度→TyrannyRisk低下・専制滑落抑制） | `SeparationOfPowersRules`/`FeudalRules`。純ロジック |
| **MONT-5** | #1449 | 法の適合性（`LegalFitnessRules`：風土×思想×産業整合→正統性係数/反乱圧力） | `GovernanceRules`/`Province`/`FactionState`。純ロジック |
| **MONT-6** | #1453 | 通商と温和政治（`CommerceModeratesWarRules`：交易量→厭戦加速・専制政体は交易抑圧で低下） | `MarketRules`/`DiplomacyRules`/`WarGoalRules`。純ロジック |
| **MONT-7** | #1457 | （lore）法の精神・啓蒙の世界観開示データ（`DisclosureLedger`） | `DisclosureLedger`(FND-4)。コード新設なし |

### 推奨着手順
`MONT-1`（政体の原動力＝最も固有で欠落の大きい signature）→ `MONT-2`（腐化回路＝原動力が失われた先）→ `MONT-3`（風土適合＝政体の前提条件）→ `MONT-4`（中間権力＝三権分立の補完）→ `MONT-5`（法適合性＝横断係数）→ `MONT-6`（通商平和＝外交への接続）→ `MONT-7`（lore）。

> いずれも既存政治・社会シミュを**後退させず接続**する additive 設計。三権分立(#171)の原典として既存システムに深みを与える。
