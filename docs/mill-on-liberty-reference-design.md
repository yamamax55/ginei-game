# ミル『自由論』参考設計（EPIC #MILL）

> 参照元：J.S.ミル『自由論』(On Liberty, 1859)。
> 「危害原理」と「言論・思想の自由」——「自由は多数派からも守られなければならない」「検閲は真理を殺す」——を近代自由主義の礎として論証。
> 本ドキュメントは、当プロジェクト（Ginei＝巨大な社会・政治シミュ層）にとって**役に立つ視点**だけを抽出し、EPIC `#MILL` として issue 化する提案。
> 著作権注意：固有名・文章・引用句は使用せず、**社会哲学のメカニクス構造パターンのみ**を参考にする。

---

## 0. なぜ「自由論」が本システムに役立つか

当プロジェクトは統治・社会の純ロジックを大量に保有している：

| 既存（カバー範囲） | カバー内容 |
|---|---|
| `SecurityRules` | 反乱抑圧・クーデター検出・弾圧支持ペナルティ (`RepressionSupportPenalty`) |
| `MovementRules` / `ConsentRules` | 非暴力抵抗・協力撤退・弾圧の可視化 (`mediaReach` パラメータ) |
| `DynastyRules` | 正統性の腐敗・天命喪失サイクル |
| `CivilianControlRules` | 文民統制・クーデターリスク |
| `EspionageRules` | 情報収集・破壊工作・検知リスク |
| `GovernanceRules` | 安定度・統合度・産出係数 |
| `ConstitutionRules`（#170） | 制約権力・権利→正統性・立憲君主制 |
| `JusticeRules`（#918-923） | 功利主義/ロールズ/リバタリアン等の正義観 |

**しかし、これらは「抑圧の物理的コスト」（弾圧→支持低下）と「正統性の腐敗サイクル」までしかモデル化していない。** ミル『自由論』が固有に持つ以下の視点が**欠けている**：

| 自由論が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **情報環境の自由度（検閲水準）が情報品質・正統性・腐敗速度に与える影響** | `SecurityRules.DissentSuppression` は「反乱者を黙らせる物理コスト」のみ。**言論の自由が情報エコシステム全体の質を決める**回路が無い |
| **多数派専制（法律でなく社会的同調圧力による抑圧）** | 国家の物理力による抑圧のみモデル。**多数派の社会的圧力**（法なし）が自由を侵食する動学が無い |
| **少数意見の制度的価値（挑戦されない信念は「死んだ教条」になる）** | 意見は数で勝敗が決まるのみ。**少数意見が多数意見を「生きた真理」に保つ**という逆説的機能が無い |
| **危害原理（抑圧の正当性閾値・過剰抑圧の加速コスト）** | 抑圧コストはあるが「どの程度が正当か」という評価軸が無い。過剰抑圧が正統性を非線形に破壊する回路が薄い |
| **個性と「実験」の社会価値（多様性→適応力・イノベーション）** | `CultureRules.AssimilationPressure` は同化のみ。**多様な個性の並存が社会全体の革新・適応力を高める**経路が薄い |

**結論**：自由論は既存の抑圧コストモデルに「**情報環境の自由度**」という新軸を与え、**①検閲 ②世論ダイナミクス ③危害原理 ④個性の価値**という4つの欠落軸を埋める。`SecurityRules`/`ConsentRules`/`DynastyRules`/`EspionageRules` に**情報の次元を横断接続**する。

---

## 1. 役に立つ視点（要約）

ミル『自由論』の世界観を、**本システムに効く形**で1行ずつ：

1. **言論を抑えると真理が「死んだ教条」になる** — 挑戦されない信念は根拠を忘れ、体制のイノベーション能力が衰える。→ `DynastyRules`（腐敗）×情報品質に新軸。
2. **多数派専制は法律がなくても成立する** — 社会的同調圧力が個人の自由を侵食する（ミルの核心）。→ `ConsentRules`/`MovementRules` に「社会的圧力」の次元を足す。
3. **危害原理 — 他者への危害を防ぐ目的以外で自由を制限することは正当化できない** — 過剰抑圧は正統性を非線形に破壊する。→ `SecurityRules` に正当性閾値を接続。
4. **少数意見は多数の愚を防ぐ知識の保険** — 一つの異論が全員正しいと思っていた前提を覆す。→ 情報品質モデルに「意見多様性ボーナス」を入れる。
5. **自由な実験の積み重ねが文明を進歩させる** — 同質社会は適応が遅い。→ `CultureRules`×`ResearchRules` に多様性係数。
6. **（lore）自由は手段でなく目的だ** — 幸福計算で削れる「費用」ではなく文明の条件。→ 開示エンジンの世界観軸・秘史接続。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`SecurityRules`/`ConsentRules`/`DynastyRules`/`MovementRules`/`ConstitutionRules` を作り直さない。** MILL はそれらに**情報・世論の次元を足し横断接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・自由論の signature）

#### MILL 情報環境と検閲（`InformationEnvironment`/`CensorshipRules`）
- **情報自由度 `pressFreedomy` (0..1)** — 体制の言論管理水準。「抑圧のスイッチ」でなく連続量。
- **短期 vs 長期の非対称**: 検閲で反乱シグナルを遮断（短期安定）↔ 情報品質劣化・`EspionageRules.InfoGain` 低下・正統性腐敗加速（長期コスト）。
- **「死んだ教条」コスト**: `pressFreedomy` が低いほど `DynastyRules.Tick` の腐敗速度が加速（真理が挑戦されず体制が実態を見失う）。
- 接続: `SecurityRules`（物理的抑圧と分離）×`EspionageRules`（情報品質係数）×`GovernanceRules`（安定度）×`DynastyRules`（腐敗加速）。
- 純ロジック新設 → EditMode テスト必須。

#### MILL 世論ダイナミクスと多数派専制（`PublicOpinionRules`/`OpinionField`）
- **意見場 `OpinionField`**: 支配的意見強度 + 意見多様度 `opinionDiversity` (0..1)。
- **多数派専制**: 少数意見への社会的圧力（法なし）= `socialConformityPressure`。`ConsentRules.Polity.cooperation` を介して協力を侵食（支持は下がらないのに自由が失われる）。
- **多様性ボーナス**: `opinionDiversity` が高いほど情報発見率↑（`EspionageRules.InfoGain` / `ResearchRules.ResearchOutput` に係数）。
- 接続: `ConsentRules`×`MovementRules`×`EventEngine`（少数意見が多数派前提を覆すイベント）。
- 純ロジック新設 → EditMode テスト必須。

### ★★ 高（危害原理・逆効果の形式化）

#### MILL 危害原理の形式化（`HarmPrincipleRules`）
- **正当性閾値**: 抑圧が正当なのは「他者への危害防止」のみ。`harmRatio`（実害脅威/全抑圧比）が低いほど → `Polity.legitimacy` 損失が非線形加速。
- **過剰抑圧のトリガ**: `harmRatio` < `overreachThreshold` を連続 N ターン → `DynastyRules.MandateLost` への係数。
- 接続: `SecurityRules`×`CivilianControlRules`×`ConsentRules`。

#### MILL 検閲の逆効果と「死んだ教条」の動学
- 高検閲 + 長期維持 → 支配思想が挑戦されなくなる → `Regime.virtue` 低下 → `DynastyRules.Tick` で腐敗加速（MILL-1 の長期係数として具体化）。
- **ミルの逆説**: 異論を潰した勝利は長期的には敗北（強さの源泉を失う）。`pressFreedomy` の履歴積分が腐敗速度に乗る。
- 接続: `CensorshipRules`（MILL-1）×`DynastyRules`×`FactionStateRules`。

### ★ 中（個性の価値・世界観lore）

#### MILL 個性と「実験」の社会価値（`LibertyCultureRules`）
- **多様性係数**: `opinionDiversity`（MILL-2）が高く `CultureRules.AssimilationPressure` が低い社会 → `ResearchRules.ResearchOutput` に正の係数（実験・発見を社会が許容する）。
- 同質化が進んだ社会は危機適応が遅い（「末人」`HopeRules` との接続 — 安定を選んで成長を捨てた末路）。
- 接続: `CultureRules`×`ResearchRules`×`HopeRules`。

#### MILL （lore）世界観の開示データ
- 「多数の専制は法より恐ろしい」「検閲が真理を守ると思った者が真理を殺した」「自由は目的であり手段ではない」。
- 接続: **コード新設せず** `DisclosureLedger`（FND-4）への**lore データ入力**。

---

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 物理的言論弾圧のコスト（逮捕・処刑）そのもの | **`SecurityRules.DissentSuppression`/`RepressionSupportPenalty` がカバー**。MILL は情報品質の次元のみ足す |
| 非暴力抵抗の動学 | **`MovementRules.Repress`/`PressurePolity` がカバー**。MILL は世論ダイナミクスの次元のみ足す |
| 言論の自由を守る法制度（憲法条文化） | **`ConstitutionRules.RightsLegitimacy`（#170）がカバー** |
| 民主的選挙・政党政治 | **`PartyRules`/`LeadershipElectionRules` がカバー** |
| 功利主義の最大多数計算 | **`JusticeRules.JusticeView.功利主義`（#918-923）がカバー** |
| 個人の内面の自由（思想の自由そのもの）を UIで可視化 | タイクン化・マイクロ操作。係数として背景的に効かせる |

---

## 3. EPIC #MILL の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。既存ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・引用は不使用、**社会哲学のメカニクス構造パターンのみ**参考。

> **EPIC = #1471**。GitHub issue 起票済み（#1474〜#1490）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **MILL-1** | #1474 | `InformationEnvironment`/`CensorshipRules` — 情報自由度と検閲水準（短期安定 vs 長期腐敗の非対称） | `SecurityRules`×`EspionageRules`×`GovernanceRules`×`DynastyRules` |
| **MILL-2** | #1477 | `PublicOpinionRules`/`OpinionField` — 世論ダイナミクスと多数派専制（意見多様度→情報品質係数） | `ConsentRules`×`MovementRules`×`EventEngine` |
| **MILL-3** | #1480 | `HarmPrincipleRules` — 危害原理の形式化（過剰抑圧の加速コスト・正当性閾値） | `SecurityRules`×`CivilianControlRules`×`ConsentRules` |
| **MILL-4** | #1484 | 検閲の逆効果と「死んだ教条」動学（高検閲→支配思想硬直→`DynastyRules`腐敗加速） | MILL-1×`DynastyRules`×`FactionStateRules` |
| **MILL-5** | #1487 | `LibertyCultureRules` — 個性と実験の社会価値（意見多様度→研究・適応力係数） | `CultureRules`×`ResearchRules`×`HopeRules` |
| **MILL-6** | #1490 | （lore）世界観の開示データ（「多数の専制」「検閲は真理を殺す」「自由は目的だ」） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`MILL-1 → MILL-2`（情報環境と世論＝最も固有で欠落の大きい signature）→ `MILL-3 → MILL-4`（危害原理と検閲の逆効果＝MILL-1/2 の係数として定義）→ `MILL-5`（個性・多様性＝研究・文化との接続）→ `MILL-6`（lore）。

> いずれも既存のセキュリティ・正統性・文化ロジックを**後退させず横断接続**する additive 設計。専制国家と自由体制の長期的帰結の差が「自然に現れる」のが理想。
