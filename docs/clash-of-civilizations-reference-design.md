# ハンチントン『文明の衝突』参考設計（EPIC #HUNT）

> 参照元：サミュエル・ハンチントン『文明の衝突』（1996年）。冷戦後の世界秩序を「国家対国家」ではなく
> **「文明圏対文明圏」**で読む地政学論。文明断層線・引き裂かれた国・核国（コア・ステート）・
> 骨肉の国シンドローム（Kin-country syndrome）が主要概念。
> 本ドキュメントは当プロジェクトへの**役立つ構造パターン**のみを抽出する提案。
> 著作権注意：固有名・文章・固有設定は不使用。**メカニクス／世界観の構造パターンのみ**参考。

---

## 0. なぜ『文明の衝突』が本システムに役立つか

### 既存の外交・文化モジュール（カバー範囲）

| 既存モジュール | カバー範囲 |
|---|---|
| `DiplomacyRules`/`DiplomacyState`（#189 DIP-1） | 勢力ペアの外交状態・opinon・条約 |
| `CultureRules`/`Culture`（#194） | 個別文化圏の同化圧力・民族主義・分離独立リスク |
| `ReligionRules`/`Religion`（#172-175） | 宗教の改宗圧力・聖戦・異端 |
| `LoyaltyRules`/`Allegiance`（#817） | 関ヶ原型の一諸侯の旗幟（loyalty/intrigue/寝返り） |
| `FactionState`/`FactionStateRules` | 勢力レベル合成（王朝/統治体/組織/共同体） |
| `GovernanceRules`/`Province`（#109） | 星系・惑星の安定度・統合度 |
| `LogisticsRules`（#844） | 版図の一体化度（連結成分） |

**現状は「勢力×勢力」の二者関係（bilateral）と「個別勢力の内政」が主軸**。
"文明圏"という**勢力より上位の集団的アイデンティティ**が不在。

### 『文明の衝突』が固有に持つ視点 × 当プロジェクトでの欠落

| 文明の衝突の固有視点 | 当プロジェクトでの欠落 |
|---|---|
| **文明圏（Civilization Group）** ＝ 勢力を束ねる上位集合 | `CultureRules` は個別文化・民族。複数勢力が共有する「文明アイデンティティ」が無い |
| **骨肉の国シンドローム** ＝ 同文明の他勢力が紛争に引き込まれる | `LoyaltyRules` は一戦場の旗幟。**超国家的な連帯圧力**（kin-country rallying）が無い |
| **核国（コア・ステート）** ＝ 各文明圏の代表国が外交を主導 | `DiplomacyRules` は対称的二者関係。文明内の**リーダーシップ争い**が無い |
| **引き裂かれた国** ＝ 文明変更を試みる国の内部分裂 | `CultureRules.NationalismFactor` は現状维持の民族主義。**文明移行の内的抵抗**が無い |
| **断層線の不安定性** ＝ 文明境界の回廊は紛争が拡大しやすい | `StrategyRules` は回廊を均質に扱う。**文明境界回廊の不安定修正子**が無い |
| **普遍主義への抵抗** ＝ 自文明の価値を普遍と主張する勢力は異文明に拒否される | `GovernanceRules` の占領統合は同/異文明で差がない |

**結論**：文明の衝突は当プロジェクトの外交・文化層に
**「勢力を超えた文明圏」という第三の層**を与える。
①文明圏メンバーシップ ②同文明連帯（kin-country） ③核国リーダー争い
④引き裂かれた国 ⑤断層線の不安定性 ⑥普遍主義抵抗
——この6軸が欠落であり、既存の bilateral 外交に**多極・超国家**の奥行きを足す。

---

## 1. 役に立つ視点（要約）

1. **文明は勢力より長命**。勢力（国家）が生まれ滅びても文明は続く。
   → 当プロジェクトの歴史の長さ（老衰・王朝交代・易姓革命）と共鳴。
   　`DynastyRules`/`SuccessionRules` が個別政権の興亡を扱うのに対し、
   　文明圏は「下の水流」として安定する（接続先#812/#814）。

2. **同文明の紛争には「引き込み」が起きる**。断層線の局地戦が大戦に波及する。
   → `BattleAllegianceRules`（会戦中の旗幟）の**戦略スケール版**として機能。
   　諸侯の参陣を「文明連帯圧力」で動機づける（`LoyaltyRules` 接続）。

3. **「核国」が代表権を握ると秩序が安定する**。競合する核国候補が対立すると文明内が分裂。
   → `FactionState`（安定度・結束）から核国スコアを導出し、
   　外交の非対称性（同文明内の盟主交渉力）に反映（DIP-2/3 接続）。

4. **文明変更は外部より内部の抵抗が激しい**。「引き裂かれた国」は外交的孤立と内乱リスクを両方抱える。
   → `ConsentRules`（合意）×`CultureRules`（文化移行抵抗）の新合成。

5. **断層線の回廊は平和が持ちにくい**。条約締結コストが上がり、破棄リスクも高い。
   → `DiplomacyRules` の条約安定度修正子として作用（additive・作り直さない）。

6. **普遍主義の強度が占領統合の難易を決める**。「自分の価値が正しい」と強く主張する勢力は
   異文明の星系を統合しにくい。
   → `GovernanceRules.EquilibriumStability` の修正子として作用（additive）。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`DiplomacyRules`・`CultureRules`・`LoyaltyRules`・`GovernanceRules` を作り直さない**。
> HUNT はこれらに**文明圏という上位レイヤー**を additive に足すだけ。

### ★★★ 最優先（真の欠落・ハンチントンの signature）

#### HUNT 文明圏データ（`CivilizationGroup`）と勢力帰属
- **`CivilizationGroup`**：複数勢力を束ねる上位集合（名称・共有イデオロギー重心・`FactionData` のリスト）。
- **`CivilizationRules`**（純ロジック・test-first）：`GroupOf(faction)`・`InSameCivilization(a,b)`・
  `AreAdjacentCivilizations(a,b,map)`（境界回廊が存在するか）。
- `FactionData` に任意フィールド `civilizationId`（未設定＝文明帰属なし＝後方互換）。
- 接続：他の全 HUNT issue の基盤。`DiplomacyRules` の affinity 修正子に流用。

#### HUNT 骨肉の国シンドローム（同文明連帯圧力）
- 文明圏内の一勢力が交戦状態に入ると、**同文明の他勢力に連帯圧力**（solidarity pressure）が掛かる。
  圧力の強さ＝「攻撃者が異文明か」×「文明圏の結束度（核国の求心力）」×「距離」。
- **`CivilizationSolidarityRules`**（純ロジック）：`SolidarityPressure`・`RallyLikelihood`・
  `ApplyRally`（`Allegiance.loyalty` を修正→ `LoyaltyRules.ResolveCascade` で旗幟決定）。
- 接続：`LoyaltyRules`（旗幟）×`DiplomacyRules`（参戦→条約コスト）×`EventEngine`（連帯通知）。

### ★★ 高（外交と内政に奥行きを与える）

#### HUNT 核国と文明代表権スコア
- 各文明圏内で最も`FactionState.Stability`・`LogisticsRules.CohesionFactor`が高い勢力が**核国スコア最大**。
  競合する核国候補が対立すると文明圏の連帯が割れる。
- **`CoreStateRules`**（純ロジック）：`CoreStateScore(faction,civ,campaignState)`・
  `IsCore(faction,civ,...)`・`CompeteForCore(civ,...)`（複数候補がいれば代表権分裂フラグ）。
- 接続：`FactionState`×`LogisticsRules`（入力）→ `DiplomacyRules` の外交交渉力修正子（出力）。

#### HUNT 引き裂かれた国（文明変更の試みと内部分裂）
- 自勢力の文明帰属を変更しようとすると、①住民（`Province.nativeIdeology`）の反発、
  ②旧文明勢力からの外交圧力、③内部勢力（`Ministry`/`Party`）の路線対立が同時に発生。
- **`TornStateRules`**（純ロジック）：`TransitionStress(faction,targetCiv,...)`（内部亀裂度0..1）・
  `InternalFractureRisk`（亀裂→`ConsentRules.IsUngovernable` 閾値を下げる）・
  `ExternalRejection`（旧文明からの opinion ペナルティ）。
- 接続：`ConsentRules`×`CultureRules`（抵抗）×`DiplomacyRules`（拒絶）×`GovernanceRules`（不安定化）。

#### HUNT 断層線と回廊不安定性
- 異文明勢力が両端を所有する回廊＝**断層線回廊**：戦争リスク上昇・条約破棄リスク上昇・
  FTLブロック閾値が低い（敵意の潜在水準が高い）。
- **`FaultLineRules`**（純ロジック）：`IsFaultLine(corridor,map,groupOf)`・
  `InstabilityFactor(corridor,...)`（1.0〜2.0 倍率）・
  `TreatyStability(treaty,corridor,...)`（断層線では条約の安定度が低い）。
- 接続：`StrategyRules`（IsFtlBlocked 閾値修正）×`DiplomacyRules`（条約安定度）。

### ★ 中（占領統合と世界観 lore）

#### HUNT 普遍主義指数と占領統合抵抗
- 勢力に `universalismScore`（0=多元主義・1=強い普遍主義）を持たせ、
  異文明星系の占領統合（`GovernanceRules.EquilibriumStability`）に修正を掛ける。
  「自分が正しい」と強く主張する勢力ほど異文明住民の反発が大きい。
- **`UniversalismRules`**（純ロジック）：`ResistanceFactor(universalism,civilizationDistance)`（0.8〜1.3 倍）・
  `IntegrationMod(ownerFaction,province,...)` → `GovernanceRules.Tick` に流し込む。
- 接続：`GovernanceRules`（統合修正）×`CultureRules`（assimilation 抵抗）×`FactionData`（新フィールド）。

#### HUNT (lore) 世界観開示データ（断層線・引き裂かれた国・普遍主義 lore）
- コード新設なし。`DisclosureLedger`（FND-4）への **lore データ入力**のみ。
- 「断層線では歴史は終わらない」「普遍主義は文明帝国主義になりうる」「引き裂かれた国の悲劇」
  を秘史・予言 category で登録。`EventEngine`（#116）の火種に。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 二者間の外交状態そのもの | **`DiplomacyRules`（#189）がカバー**。HUNT は修正子を足すだけ |
| 個別文化・民族の同化/分離 | **`CultureRules`（#194）がカバー** |
| 宗教の改宗・聖戦 | **`ReligionRules`（#172-175）がカバー** |
| 一戦場の旗幟・寝返り | **`LoyaltyRules`/`BattleAllegianceRules`（#817）がカバー** |
| 西洋/東洋の具体的文明名・宗教名 | **著作権・固有設定不使用の原則**。`CivilizationGroup` は汎用データ型 |
| 文明圏ごとの固有技術ツリー | **`ResearchRules`（#123-127）があり**、文明差は `UniversalismRules` の係数として反映 |

---

## 3. EPIC #HUNT の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #1869**。GitHub issue 起票済み（#1870〜#1875・#1878〜#1879）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **HUNT-1** | #1870 | 文明圏データ（`CivilizationGroup`）と勢力帰属（`CivilizationRules`） | 全HUNT issueの基盤。`FactionData.civilizationId`（後方互換）＋純ロジック新設 |
| **HUNT-2** | #1871 | 骨肉の国シンドローム（同文明連帯圧力 `CivilizationSolidarityRules`） | `LoyaltyRules`×`DiplomacyRules`×`EventEngine`。旗幟を文明スケールへ拡張 |
| **HUNT-3** | #1872 | 核国スコアと文明代表権争い（`CoreStateRules`） | `FactionState`×`LogisticsRules` → 外交交渉力修正子 |
| **HUNT-4** | #1873 | 引き裂かれた国（文明移行の内部分裂 `TornStateRules`） | `ConsentRules`×`CultureRules`×`DiplomacyRules`。文明変更の内的抵抗 |
| **HUNT-5** | #1875 | 断層線と回廊不安定性（`FaultLineRules`） | `StrategyRules.IsFtlBlocked` 閾値修正×`DiplomacyRules` 条約安定度 |
| **HUNT-6** | #1878 | 普遍主義指数と占領統合抵抗（`UniversalismRules`） | `GovernanceRules.EquilibriumStability`修正子×`CultureRules` |
| **HUNT-7** | #1879 | （lore）世界観開示データ（断層線・引き裂かれた国・普遍主義） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`HUNT-1`（文明圏データ＝全ての基盤）→ `HUNT-2`（骨肉の国＝最も固有・欠落大）
→ `HUNT-3`（核国＝文明内外交の非対称性）→ `HUNT-4`（引き裂かれた国＝内政接続）
→ `HUNT-5`（断層線＝戦略回廊接続）→ `HUNT-6`（普遍主義＝占領接続）→ `HUNT-7`（lore）。

> いずれも既存外交（`DiplomacyRules`）・文化（`CultureRules`）・忠誠（`LoyaltyRules`）を
> **後退させず接続**する additive 設計。
