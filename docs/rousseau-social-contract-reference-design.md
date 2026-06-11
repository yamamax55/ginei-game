# ルソー『社会契約論』参考設計（EPIC #ROUS）

> 参照元：ジャン＝ジャック・ルソー『社会契約論』（Du Contrat Social, 1762）。
> 一般意志・人民主権・立法者・市民宗教・政体規模論を中心とした近代民主主義の原典。
> 本ドキュメントは、当プロジェクト（Ginei）にとって**役に立つ純ロジック軸**だけを抽出し、EPIC `ROUS` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**政治哲学のメカニクス構造のみ**を参考にする。

---

## 0. なぜ「社会契約論」が本システムに役立つか

当プロジェクトは正統性・統治・腐敗の**マクロロジックを大量に保有**している：

| 既存（カバー範囲） | 対応システム |
|---|---|
| 権力は借り物＝合意で成り立つ実効統治力 | `ConsentRules`/`Polity`（#836/#837） |
| 天命と腐敗サイクル・易姓革命 | `DynastyRules`/`Regime`（#867） |
| 制約権力・権利→正統性・抵抗権 | `ConstitutionRules`/`Constitution`（#170） |
| 政府形態（文民統制/君主/軍部…） | `CivilianControlRules`/`CivilianControlType`（GOV-4 #145） |
| カリスマ→制度化・継承 | `Organization`/`SuccessionRules`（#812/#814/#816） |
| 政党・議席・与党首班 | `PartyRules`/`Party`（GOV-6 #159） |
| 省庁省益（縦割り摩擦） | `MinistryRules.SectionalismFriction`（GOV-5 #158） |
| 国家状態の合成 | `FactionStateRules`/`FactionState`（統合層） |

**しかし、これらに「統治が公益を向いているか」を測る直接の尺度が無い**：

| 社会契約論が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **一般意志 vs 全体意志** = 公益統治か派閥捕獲か | `SectionalismFriction`/`Party.institutionalInterest` は局所値。**「政府が共通善を向いているか」を測る合成指標が無い** |
| **立法者のパラドックス** = 制度を作る者は制度の外にいる | `Organization.InvestInstitution` は継続的投資。**建国の憲法制定権力（一回性の初期化）** が無い |
| **政体の規模依存性** = 小国→直接民主制、大帝国→君主制 | `CivilianControlType` は列挙のみ。**版図×人口 → 政体適合度のスコア関数** が無い |
| **市民宗教** = 政府が意図的に作る政治的結束信仰 | `ReligionRules` は有機的な信仰均衡。**製造された市民的連帯（形骸化→正統性崩壊）** が無い |
| **委任不可能な人民の意志** = 代表者が超えてはならない一線 | `ConstitutionRules.ResistanceTriggered` は制度的。**一般意志への適合度が閾値を割ると抵抗権が自動発火する接続** が無い |

**結論**：社会契約論は当プロジェクトに、①**一般意志汚染指標**（公益統治か派閥捕獲かの実効尺度）・②**立法者パラドックス**（建国の憲法制定権力）・③**政体規模適合**（版図が政体を強制する法則）・④**市民宗教**（政府製造の結束信仰とその崩壊）という4つの欠落軸を与える。
既存 SOC-1〜SOC-5（#349〜#353）は世界観・概念設計の段階。本 ROUS は**純ロジック・test-first の実装層**を担う（重複しない）。

---

## 1. 役に立つ視点（要約）

1. **「一般意志は全体意志と違う」** = 個別利益の総和（全体意志）と共通善を志向する意志（一般意志）は別物。為政者が私益・派閥に従えば正統性を蝕む。→ 当プロジェクトの `FactionState.Stability` に「公益偏差」の次元を加える。
2. **「主権は委任できない」** = 民は行政を代表させられるが、主権（一般意志の表出）は委任不可。代表が主権を僭越すると抵抗権が発火する。→ `ConstitutionRules.ResistanceTriggered` の発火条件を一般意志汚染指標に接続。
3. **「立法者は制度の外にいる」** = 憲法を作る者はその憲法に従うことができない建国の逆説。一回性の制度初期化は継続的投資（`InvestInstitution`）とは質的に異なる。→ `LawgiverRules` で建国の憲法制定権力を独立モデル化。
4. **「政体は規模が決める」** = 小国は直接民主制、中規模国は選ばれた貴族制（共和制）、大帝国は君主制が自然。版図拡大が政体を変える。→ `PolityScaleRules` で版図×人口 → 政体適合度のスコア関数。
5. **「市民宗教は政府の道具」** = 有機的な信仰とは別に、政府が意図的に作る市民的連帯の信仰。凝集力を高めるが形骸化すると正統性の空洞化を招く。→ `CivicFaithRules`/`CivicFaith` で政治的人工信仰を独立モデル化。
6. **「腐敗は構造的必然」** = 政府は常に人民主権（一般意志）から離れ、自己保存の特殊意志に向かって堕落する。改革か革命かの選択は宿命。→ `DynastyRules.Regime` の腐敗に「派閥捕獲の速さ」という変数を接続。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`ConsentRules`/`DynastyRules`/`ConstitutionRules`/`CivilianControlRules`/`Organization` を作り直さない**。ROUS はそれらに**欠落軸を足し、接続する**だけ（additive）。SOC-1〜SOC-5（世界観概念設計）と重複しない（ROUS は実装層）。

### ★★★ 最優先（真の欠落・社会契約論の signature）

#### ROUS-1: `GeneralWillRules`/`GeneralWillState` — 一般意志汚染指標
- **一般意志捕獲度**（0..1）を算出する純ロジック。省益(`MinistryRules.SectionalismFriction` の平均)・党捕獲（与党 `Party.support` の集中度 = ジニ係数近似）・権力集中（`PowerActor.EffectivePower` の最大値）の加重和から派閥捕獲スコアを算出し、`GeneralWillIndex = 1 - captureScore` を返す。
- **接続**：`FactionState.Tick` に `GeneralWillIndex` を正統性の修正子として接続。`GeneralWillIndex` が低い（派閥捕獲が高い）ほど正統性が蝕まれる。`ConstitutionRules.ResistanceTriggered` の発火条件に `GeneralWillIndex < threshold` を追加。
- **test-first**：EditModeテスト必須（均等政党 → 高指数、独占政党 → 低指数、各入力ゼロ・最大の境界）。

#### ROUS-2: `LawgiverRules` — 立法者パラドックス（建国の憲法制定権力）
- **建国の一回性**：`LawgiverRules.CreateConstitution(Person lawgiver, Organization org, LawgiverParams)` が `org.institutionalization` を `constitutionalGift`（立法者の能力比例）ぶん底上げし `foundingLegitimacyBonus` を設定する。継続的な `InvestInstitution` とは独立した**初期化一回限りの底上げ**。
- **外部性制約**：立法者は憲法制定後、その組織の `GovernmentRegistry` に任命できない（`IsExternalToBodyPolitic` チェック）。自分が作った制度を自分で運用すると `LawgiverRules.FounderCapture` が発火し正統性ボーナスが消滅。
- **接続**：`Organization.InvestInstitution`（初期化後の継続投資の出発点）・`ConstitutionRules.Constitution`（`foundingLegitimacyBonus` を `RightsLegitimacy` の初期値に合流）。
- **test-first**：EditModeテスト必須（能力→初期化量の計算、外部性違反の検出、ボーナス消滅）。

### ★★ 高（既存に足りない軸を追加）

#### ROUS-3: `PolityScaleRules` — 政体規模適合
- **スコア関数**：`PolityScaleRules.ScaleFitness(int systemCount, float avgPopulation, CivilianControlType type)` → float (0..1)。scale = systemCount × avgPopulation（2パラメータ `ScaleParams` で調整可）に対して各政体の適合度曲線を返す。小 → 民主制が最適、中 → 共和制/選挙貴族制、大 → 君主制/中央集権。
- **ミスマッチペナルティ**：`MismatchPenalty(scale, type)` → 現政体が規模に合わない場合の正統性・安定度の損失（実効値パターン・基準値非破壊）。
- **接続**：`CivilianControlRules`（既存政体形態に対するスコア付与）・`GalaxyMap.systems.Count`（版図規模の入力）・`DemographicsRules.Population`（人口の入力）。
- **test-first**：EditModeテスト必須（境界条件・最適形態の自動選出・ペナルティ計算）。

#### ROUS-4: `CivicFaithRules`/`CivicFaith` — 市民宗教（政府製造の政治的結束信仰）
- **構造**：`CivicFaith { investmentLevel, orthodoxy, cohesionBonus, schismRisk }` の純データ。`ReligionRules`（有機的宗教）とは独立した政治製造物。
- **操作**：`Invest(faith, dt, investFactor)` で `orthodoxy`・`cohesionBonus` を漸増（上限あり）。`Violate(faith, severity)` で `schismRisk` を急上昇し、`orthodoxy` が高いほど正統性崩壊が大きい（高凝集ほど脆い）。`CohesionEffect(faith)` → `Organization.cohesion` への加算値。`Ossification(faith)` → 高 orthodoxy ほど `InvestInstitution` の効果が落ちる（形骸化・改革不能化）。
- **接続**：`Organization.cohesion`（結束ボーナス）・`FactionState.Stability`（崩壊時の正統性ダメージ）・`ReligionRules`（市民宗教と有機宗教が競合・補完する場合の合成は呼び出し側で行う）。
- **test-first**：EditModeテスト必須（投資曲線、違反カスケード、骨化閾値、崩壊量の計算）。

### ★ 中（世界観lore）

#### ROUS-5: （lore）開示データ — 社会契約の謎・一般意志の問い・主権の歴史
- 「民は自由に生まれたが、いたるところで鎖につながれている」「一般意志は間違えない——ただし民が欺かれない場合に限り」「立法者は神に近い——しかし神であってはならない」などの思想命題を `DisclosureLedger`（FND-4 #495）への**loreデータ入力**として記述。**コード新設なし**。条件は FactionState の崩壊・一般意志汚染・建国イベントなど。

---

### ❌ 不採用（重複・既存で十分・タイクン化）

| 不採用 | 理由 |
|---|---|
| 社会契約そのもの（人民が契約を結ぶUIフロー） | **`ConsentRules.Polity` がカバー**。ROUS は接続のみ |
| 直接民主制UI（投票・住民投票の操作系） | タイクン化（マイクロ操作増加）。高位の決断→エンジン駆動 |
| 自然状態のモデル（無政府状態シミュ） | SOC-1（#349）が概念をカバー済み。コード化は `IsUngovernable` で十分 |
| 共和主義的徳（virtus）の能力システム | `AdmiralData.operation`/`intelligence` の実効値で十分 |
| 階級闘争・労働搾取 | マルクス（別EPIC候補）の領域。重複新設せず |
| 権力分立モデルそのもの | **`SeparationOfPowersRules`（#171）がカバー**済み |

---

## 3. EPIC #ROUS の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。
> SOC-1〜5（#349〜353）とは役割が異なる：SOC は世界観概念設計、ROUS は純ロジック実装層。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **ROUS-1** | #1462 | `GeneralWillRules`/`GeneralWillState` 一般意志汚染指標（派閥捕獲 vs 公益統治の合成スコア） | `MinistryRules`×`PartyRules`×`PowerActor` の局所値を合成→`FactionState`正統性に接続 |
| **ROUS-2** | #1464 | `LawgiverRules` 立法者パラドックス（建国の憲法制定権力・一回性の制度初期化） | `Organization.InvestInstitution`・`ConstitutionRules.Constitution` の初期化層 |
| **ROUS-3** | #1466 | `PolityScaleRules` 政体規模適合（版図×人口 → 政体適合度スコア・ミスマッチペナルティ） | `CivilianControlRules`×`GalaxyMap`×`DemographicsRules` の統合スコア関数 |
| **ROUS-4** | #1468 | `CivicFaithRules`/`CivicFaith` 市民宗教（政府製造の政治的結束信仰・形骸化→崩壊） | `Organization.cohesion`・`FactionState.Stability`・`ReligionRules` との独立並存 |
| **ROUS-5** | #1469 | （lore）開示データ（社会契約の謎・一般意志の問い・主権の歴史） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`ROUS-1`（一般意志汚染指標＝最も固有で欠落の大きい signature）→ `ROUS-2`（立法者パラドックス＝建国システムの根拠）→ `ROUS-3`（規模適合＝政体選択に客観スコアを与える）→ `ROUS-4`（市民宗教＝有機宗教と並行する政治製造物）→ `ROUS-5`（lore）。

> いずれも既存マクロ政治システムを**後退させず接続**する additive 設計。`FactionState`（国家状態の合成最上層）に最も効く。
