# 『ザ・フェデラリスト』参考設計（EPIC #FED）

> 参照元：ハミルトン・マディソン・ジェイ『ザ・フェデラリスト』（The Federalist Papers, 1787-1788）。アメリカ合衆国憲法の批准を促した85篇の論説。**派閥の逆説・野心相殺設計・複合共和制**という三本柱で、「自由を守りながら有効に統治する」共和主義的制度設計を体系化。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略＋既に巨大な政治・社会純ロジック層）にとって**役に立つ視点**だけを抽出し、EPIC `#FED` として issue 化する提案。
> 著作権注意：固有名・文章・固有設定は流用せず、**政治哲学のメカニクス／世界観の構造パターンのみ**を参考にする。

---

## 0. なぜ「ザ・フェデラリスト」が本システムに役立つか

当プロジェクトは政治・社会の**純ロジックを大量に保有**している（[CLAUDE.md] 参照）：

| 既存（政治・社会層） | カバー範囲 |
|---|---|
| `SeparationOfPowersRules` (#171) | 三権分立・`TyrannyRisk`・`IsGridlocked` |
| `ConstitutionRules`/`Constitution` (#170) | 制約権力・権利→正統性・立憲君主制 |
| `PartyRules`/`Party` (GOV-6) | 政党・支持・最小選挙・党首選出 |
| `PowerRules`/`PowerActor` (#164) | 実権集中・傀儡・影の支配者 |
| `CoupRules` (#215-219) | クーデター成功率・事後正統性 |
| `ConsentRules`/`Polity` (#836) | 被支配者の協力と非協力 |
| `FeudalRules`/`Fief` (#168/169) | 徴募・領主反乱・門地開放 |
| `FactionStateRules`/`FactionState` | 王朝/統治体/組織/共同体の合成 |
| `GovernmentRegistry`/`OfficeRules` (GOV-1/3) | 役職・任命・提案権限 |
| `LogisticsRules` (#844) | 版図の地理的一体化度 |
| MONT（`GovernmentPrincipleRules`/`PolityCorruptionRules`等） | 政体原動力・腐化回路・中間権力・法適合性 |
| LOCK（`PropertyOriginRules`/`TrustMandateRules`/`ForfeitureRules`） | 財産権起源・信託解消連鎖・自然法的正戦 |

**しかし、これらはロック・モンテスキューの「構造と腐敗」を扱いながら、フェデラリストが固有に描く以下が欠けている**：

| フェデラリストが固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **派閥増殖→安定（Federalist #10）** | `PartyRules` に政党はあるが「派閥の数・多様性が増えると多数派暴政リスクが下がる」公式が無い。大きな共和国ほど安定するマディソンの逆説がモデル化されていない |
| **野心相殺設計（Federalist #51）** | `SeparationOfPowersRules.CheckBalance` は抑制の存在を定義するが「各ブランチが自己利益から自ブランチの権限を守ろうとすることで憲法が自己強制される」というメカニズム（制度設計の核）が無い |
| **複合共和制・二層主権** | `LogisticsRules` は版図の地理的結合度のみ。中央と地域が**意図的に権限を分割し合う垂直的な抑制**（連邦↔州の双方向チェック）が無い。`FeudalRules` は徴募と反乱だけで委譲権限の設計論でない |
| **拡大共和国の安定（規模の逆説）** | `LogisticsRules.CohesionFactor` は版図が広がると低下（分散ペナルティ）。フェデラリストの「広い領土→派閥多様性→より安定」という**逆の方向性**がモデルに無い |
| **行政エネルギーと単一執政のトレードオフ** | `GovernmentRegistry` に役職はあるが「単一執政は意思決定が速く危機対応力が高い／集団指導は責任が拡散するが専制リスクは低い」という執政設計の二律背反が無い |
| **代表による派閥濾過** | 選挙はあるが「選挙区が大きいほど派閥的熱狂が洗練され、より広い公益を代表する人物が選ばれやすい」という代議制の濾過効果が無い |

**結論**：フェデラリストは当プロジェクトに**①派閥多様性→安定の公式 ②野心相殺による制度の自己強制 ③二層主権の垂直抑制**という3つの真の欠落軸を与える。MONT（政体の原動力）・LOCK（財産権と信託）と完全に直交し、「**どう設計すれば自由な共和制が自己維持できるか**」という制度設計論の核を供給する。

---

## 1. 役に立つ視点（要約）

フェデラリストの世界観を、**本システムに効く形**で1行ずつ：

1. **派閥は根絶できない——大きな共和国で無力化せよ（Federalist #10）**。派閥が多様で互いを相殺する社会では、どの派閥も多数派を独占できない。→ `FactionMultiplicityRules` という純ロジックで、`PartyRules`/`SeparationOfPowersRules` の TyrannyRisk 計算に多様性係数を注入。
2. **野心を持って野心を制する（Federalist #51）**。各ブランチのアクターが自ブランチの権力を守ろうとする自己利益そのものが憲法を強制する。→ 制度設計パラメータ `AmbitionCounterRules` が `SeparationOfPowersRules.CheckBalance` の「能動的強度」を算出。
3. **二重の安全保障——権力を縦にも横にも分割する（Federalist #51）**。三権分立（横）と連邦・州の分権（縦）を重ねることで暴政への経路を二重に遮断する。→ `CompoundRepublicRules` で中央↔地域の垂直抑制強度を計量し、`SeparationOfPowersRules.TyrannyRisk` へ係数接続。
4. **大きな共和国は小さな民主制より安定する（Federalist #10 逆説）**。多様な派閥、広い版図、多い代表者が熱狂を薄める。→ `ExtendedRepublicRules` で版図規模×派閥多様性→安定補正（`LogisticsRules` の凝集ペナルティと独立軸）。
5. **行政はエネルギーを必要とする（Federalist #70）**。統一された単一執政は迅速で責任が明確——分割された集団指導は摩擦が多いが専制化しにくい。→ `ExecutiveEnergyRules` で単一/集団の設計パラメータが決断速度と専制リスクを分岐。
6. **代議制は直接民主制より賢い（Federalist #10）**。選挙という濾過が「派閥的情熱」より「公益志向の人物」を代表へ送りやすい。→ 選挙濾過係数が `PartyRules`/`LeadershipElectionRules` の派閥的歪みを補正。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`SeparationOfPowersRules`(#171)・`ConstitutionRules`(#170)・`PartyRules`(GOV-6)・`LogisticsRules`(#844)・`FeudalRules`(#168/169)・`GovernmentRegistry`(GOV-3) を作り直さない**。FED はそれらに**欠落軸を足し、接続する**だけ（additive）。MONT/LOCK との重複も無い（設計哲学の次元が違う）。

### ★★★ 最優先（真の欠落・フェデラリストの signature）

#### FED 派閥増殖安定則（`FactionMultiplicityRules`）
- **核心の公式**：`FactionDiversity` = ハーフィンダール-ハーシュマン指数の逆数的指標（派閥が多く均等に分布→高い）。
- **tyranny resistance**：`MultiplicityFactor` = 1 - HHI_like_concentration（0..1）。`SeparationOfPowersRules.TyrannyRisk` にこれの逆数を掛け算（派閥が多様なほど暴政リスクが下がる）。
- **会派形成コスト**：多数派連合を作るには多くの派閥を説得する必要がある → `CoalitionCost(target_fraction, factions)` 関数。
- 接続：`SeparationOfPowersRules.TyrannyRisk`×`PartyRules.factions`×`PowerRules.EffectivePower`。純ロジック・test-first。

#### FED 野心相殺設計（`AmbitionCounterRules`）
- **機構**：各ブランチのアクターが「自分のブランチの権限を侵されると損をする」自己利益（`InstitutionalInterest`）を持つ → 互いに牽制する能動的インセンティブが生まれる。
- `BranchDefenseStrength(branch)` = そのブランチの保持者数 × 平均 `ambitionScore` × `institutionalLoyalty`。
- `CheckActivation` = `BranchDefenseStrength` が相手ブランチの `EncroachmentAttempt` を上回ると `SeparationOfPowersRules.CheckBalance` が発動（現在の `CheckBalance` は存在を前提とするが、**発動条件を持っていない**）。
- **設計パラメータ**：選任期間・罷免コスト・報酬が `InstitutionalInterest` の強度を決める → 任期が短い/罷免しやすい役職はブランチを守ろうとしない（弱い抑制）。
- 接続：`SeparationOfPowersRules`(#171)×`OfficeRules`(GOV-1/3)×`GovernmentRegistry`。純ロジック・test-first。

### ★★ 高（重要な補完・連邦論の核）

#### FED 複合共和制と二層主権（`CompoundRepublicRules`）
- **二層主権**：`SovereigntyLayer` (enum: 中央/地域/両層)。`DelegatedPowers`（地域→中央に委譲した権能リスト）と `ReservedPowers`（地域が保持する権能）。
- **垂直的抑制強度** `VerticalCheckStrength` = 地域主権の実質的強さ（0..1。完全中央集権=0、地域分権=1）。
- `VerticalCheckStrength` が高いほど：`SeparationOfPowersRules.TyrannyRisk` を追加低下（横の三権分立と縦の分権が**二重の安全網**を形成）。ただし過度な分権（>0.8）は `LogisticsRules.CohesionFactor` を低下（一体性の損失）。
- **委任設計の変更コスト**：委任から保留への再配分は `ReallocationFriction` でコストをかける（急な中央集権化は `SuccessionRules.Refactor` の「制度的衝撃」と同型）。
- 接続：`SeparationOfPowersRules`(#171)×`LogisticsRules`(#844)×`GovernanceRules`(#109)×`FeudalRules`(#168/169)。純ロジック・test-first。

#### FED 拡大共和国の安定（`ExtendedRepublicRules`）
- **逆説の公式**：版図規模（`LargestConnectedComponent` の規模）と `FactionDiversity`（FED-1）の積 → `ExtendedStabilityBonus` (0..1)。
- 大きな共和国ほど多様な利害が存在し、どの単一派閥も多数派を形成できない → 安定補正として `FactionState.Stability` に加算。
- `LogisticsRules.CohesionFactor`（版図の地理的分断ペナルティ）と**独立軸**：一体化が低くても多様性補正は働く（二つの効果は別経路）。ただし分断が極端（CohesionFactor < 0.3）なら ExtendedStabilityBonus も割り引く。
- 接続：`FactionMultiplicityRules`(FED-1)×`LogisticsRules`(#844)×`FactionStateRules`。純ロジック・test-first。

#### FED 行政エネルギーと単一執政（`ExecutiveEnergyRules`）
- **設計軸**：`ExecutiveUnity` (0..1)。1＝単一執政（ハミルトン推奨）、0＝集団指導制。
- `ExecutiveEnergy` = f(ExecutiveUnity) → 危機対応速度・軍事動員速度のボーナス（`BattleManager`/戦略レイヤーの決断遅延に係数接続）。
- `DiffuseAccountability` = 1 - ExecutiveUnity → 高いほど `CoupRules.CoupSuccessChance` が低下（複数人が権限を持つと一人によるクーデターが困難）、ただし `TyrannyRisk` も自然に低い。
- **基準値非破壊**：`ExecutiveUnity` は `Office.civiliansOnly` 等の設計パラメータから導出、既存の任命台帳を上書きしない。
- 接続：`GovernmentRegistry`(GOV-3)×`OfficeRules`(GOV-1)×`CivilianControlRules`(GOV-4)×`CoupRules`(#215-219)。純ロジック・test-first。

### ★ 中（濾過効果・世界観lore）

#### FED 代表による派閥濾過（`RepresentativeFilterRules`）
- **濾過公式**：選挙区の広さ（= 1選挙区当たりの有権者数）→ 単一派閥が候補者を支配しにくくなる → `FilterStrength`（0..1）。
- `FilterStrength` が高いほど選出代表者の `FactionBias`（派閥的歪み）が薄まり、より広い `PartyRules.support` 基盤を持つ代表が選ばれる → `GovernmentPrincipleRules`（MONT）の徳・名誉に正の補正。
- タイクン化回避：係数のみ——選挙区の物理的境界は管理しない（`PartyRules` の既存選挙計算に補正係数として乗算）。
- 接続：`PartyRules`(GOV-6)×`LeadershipElectionRules`(GOV-7)×`FactionMultiplicityRules`(FED-1)。純ロジック・test-first。

#### FED（lore）共和主義の実験と制度設計の秘史
- 「自由と統治を同時に実現した人類初の試み」「派閥は根絶できない——多様性で無力化せよ」「野心を持って野心を制せ」「大きな共和国こそ自由の砦」をゲーム内啓蒙秘史として `DisclosureLedger`（FND-4）へ入力。
- 条件例：`FactionMultiplicityRules.MultiplicityFactor` が初めて暴政を防いだ瞬間、`AmbitionCounterRules.CheckActivation` が機能した瞬間、`CompoundRepublicRules` の垂直抑制で独裁が阻止された瞬間など。
- 接続：`DisclosureLedger`(FND-4)。コード新設なし。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 三権分立そのものの実装 | **`SeparationOfPowersRules`(#171)が既にカバー**（CheckBalance/TyrannyRisk/IsGridlocked）。FED はその発動条件（野心相殺）と強度係数（派閥多様性・垂直分権）を足すのみ |
| 立憲主義・権利章典の実装 | **`ConstitutionRules`/`Constitution`(#170)が既にカバー**。FED-3の複合共和制は「権限の配分設計」であり重複しない |
| 選挙・政党システム本体 | **`PartyRules`(GOV-6)・`LeadershipElectionRules`(GOV-7)が既にカバー**。FED-6は係数の追加のみ |
| クーデター・革命の実装 | **`CoupRules`(#215-219)が既にカバー**。FED-5はCoupChanceへの係数接続のみ |
| 財政・税収の実装 | **`FiscalRules`/`FiscalState`(#161/162)が既にカバー** |
| 政体の原動力（徳/名誉/恐怖） | **MONT の`GovernmentPrincipleRules`が既にカバー**。FED はその補完係数として接続するだけ |
| 中間権力・法の適合性 | **MONT の`IntermediatePowerRules`/`LegalFitnessRules`が既にカバー** |
| 信託解消・自然権 | **LOCKの`TrustMandateRules`/`PropertyOriginRules`が既にカバー** |
| 各州ごとの詳細な地域シミュ | タイクン化回避——`VerticalCheckStrength` という係数で代替（物理的な州境管理は作らない） |

---

## 3. EPIC #FED の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。既存政治ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #1470**。GitHub issue 起票済み（#1473〜#1496）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **FED-1** | #1473 | 派閥増殖安定則（`FactionMultiplicityRules`・HHI逆数→多数派暴政リスク低下・会派形成コスト） | `SeparationOfPowersRules.TyrannyRisk`×`PartyRules.factions`×`PowerRules`。純ロジック |
| **FED-2** | #1476 | 野心相殺設計（`AmbitionCounterRules`・制度ポジションの自己利益→CheckBalance能動的発動条件） | `SeparationOfPowersRules`(#171)×`OfficeRules`(GOV-1)×`GovernmentRegistry`。純ロジック |
| **FED-3** | #1481 | 複合共和制と二層主権（`CompoundRepublicRules`・委譲/保留権限→垂直抑制強度→TyrannyRisk低下） | `SeparationOfPowersRules`×`LogisticsRules`(#844)×`GovernanceRules`(#109)×`FeudalRules`(#168/169)。純ロジック |
| **FED-4** | #1485 | 拡大共和国の安定（`ExtendedRepublicRules`・版図規模×派閥多様性→安定補正・CohesionFactorと独立軸） | `FactionMultiplicityRules`(FED-1)×`LogisticsRules`(#844)×`FactionStateRules`。純ロジック |
| **FED-5** | #1489 | 行政エネルギーと単一執政（`ExecutiveEnergyRules`・ExecutiveUnity→決断速度×CoupChance低下のトレードオフ） | `GovernmentRegistry`(GOV-3)×`OfficeRules`×`CivilianControlRules`(GOV-4)×`CoupRules`(#215-219)。純ロジック |
| **FED-6** | #1494 | 代表による派閥濾過（`RepresentativeFilterRules`・選挙区規模→FilterStrength→派閥的歪み低減） | `PartyRules`(GOV-6)×`LeadershipElectionRules`(GOV-7)×`FactionMultiplicityRules`(FED-1)。純ロジック |
| **FED-7** | #1496 | （lore）共和主義の実験・派閥の逆説・野心の制度化（`DisclosureLedger`） | `DisclosureLedger`(FND-4)。コード新設なし |

### 推奨着手順
`FED-1`（派閥多様性公式＝最も固有で欠落の大きい signature）→ `FED-2`（野心相殺＝FED-1を「なぜ機能するか」で裏付ける）→ `FED-3`（複合共和制＝垂直の次元を追加）→ `FED-4`（拡大共和国＝FED-1とFED-3を統合した逆説補正）→ `FED-5`（行政エネルギー＝執政設計の二律背反）→ `FED-6`（代表濾過＝選挙に係数接続）→ `FED-7`（lore はいつでも可）。

> いずれも既存政治・社会シミュを**後退させず接続**する additive 設計。MONT（政体原動力）・LOCK（財産権・信託）と三層を成し「自由な共和制が自己維持できるか」という制度設計論の核を担う。
