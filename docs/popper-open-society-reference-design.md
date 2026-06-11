# ポパー『開かれた社会とその敵』参考設計（EPIC #POPR）

> 参照元：カール・ポパー著『開かれた社会とその敵』。プラトン・ヘーゲル・マルクスの「歴史主義」批判と、制度的自己修正能力を持つ「開放社会」の擁護。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略＋既に大規模な政体・社会シミュ層）にとって**役に立つ視点**だけを抽出し、EPIC `#POPR` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**政治哲学のメカニクス構造パターンのみ**を参考にする。

---

## 0. なぜ「開かれた社会とその敵」が本システムに役立つか

当プロジェクトは政体・社会シミュのロジック層を**大量に保有**している：

| 既存（政体・制度） | カバー範囲 |
|---|---|
| `DynastyRules`/`Regime`（#867） | 腐敗の漸進・正統性の崩壊・易姓革命・改革 |
| `ConsentRules`/`Polity`（#836） | 権力は借り物・協力の撤収・統治不能 |
| `CivilianControlRules`（GOV-4 #145） | 文民統制/軍部優位/党軍の制度型 |
| `CoupRules`（#215-219） | クーデター成功率・後処理正統性コスト |
| `FactionData.ideology`（専制/民主…） | イデオロギー軸（政策傾向・思想一致） |
| `FactionStateRules`/`FactionState` | 王朝/統治体/組織/共同体の合成・`IsCollapsing` |
| `LoyaltyRules`/`Allegiance`（#817） | 条件付き忠誠・寝返りカスケード |
| `Organization`/`SuccessionRules`（#812） | カリスマの日常化・継承と制度存続 |

**しかし、これらは「王朝サイクル・腐敗・崩壊の動学」であり**、ポパーが固有に描く以下の4軸が**欠けている**：

| ポパーが固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **開放度（自己修正能力）軸** | `ideology` は左右・専制/民主の傾向。**「制度的に誤りを修正できるか」という構造的能力**は別次元。民主でも硬直化した低開放状態はある。今は無い |
| **漸進的改革 vs 全体改造の二様** | `DynastyRules.Reform`/`Revolution` はあるが、**リスク/リターン分布（小利確実 vs 大利高分散）を区別しない**。改革モード選択が無い |
| **誤り蓄積と脆性崩壊** | `Regime.腐敗` は漸進腐敗。**閉鎖体制が誤りを修正できず蓄積し、臨界で突然崩壊する**脆性の動学が薄い |
| **寛容のパラドックス** | **「不寛容な勢力を無限に容認すると開放社会が内側から乗っ取られる」**機構が無い |
| **歴史主義の罠** | **「歴史の必然を信じる体制は適応を拒む→脆性を増す」**イデオロギー×剛性の結合が無い |

**結論**：ポパーは既存の「腐敗→崩壊」サイクルに**「開放度という構造軸」「誤り蓄積の脆性崩壊」「漸進的改革 vs 全体改造の設計選択」「寛容のパラドックス」**という4欠落軸を与える。そして**銀英伝のテーマ中核（共和制民主主義 vs 専制帝国）をゲームメカニクスと世界観loreの両面から強化**する。

---

## 1. 役に立つ視点（要約）

ポパーの世界観を、**本システムに効く形**で1行ずつ：

1. **開放社会 vs 閉鎖社会＝制度が批判と修正を許容するかどうか**。剣ではなく「制度的適応能力」で長期の勢力の命運が決まる。→ `DynastyRules`/`FactionState` に「開放度」の新軸として接続。銀英伝の帝国改革と共和制腐敗の物語に直結。
2. **漸進的社会工学こそ安全な改革路**。全体改造（革命）は失敗コストが崩壊。テスト可能・修正可能な小さな変化の積み重ね。→ `DynastyRules.Reform` の**改革モード選択**。EventEngine の選択肢提示。
3. **誤りを修正できない制度は臨界まで誤りを蓄積し突然崩壊する**。閉鎖体制の「急に崩れる」パターン＝脆性崩壊の数値モデル。→ `FactionStateRules.IsCollapsing` への新経路。
4. **寛容のパラドックス：開放社会は不寛容な勢力を無限に容認すると内側から崩れる**。開放性を守るために不寛容を抑制する逆説。→ 外交×内政の新ゲートウェイ。
5. **歴史主義（歴史法則への確信）は適応を拒否させる**。「我々は歴史の必然によって勝つ」という信念が閉鎖性を深め脆性を増す。→ `DynastyRules`/`FactionData.ideology` への修正子。
6. **銀英伝の主題と完全共鳴**：民主共和制が自己腐敗を修正できるか、帝国の改革は漸進的か全体改造かが物語の核心。→ `DisclosureLedger` lore の背骨に。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`DynastyRules`/`ConsentRules`/`FactionStateRules` を作り直さない**。POPR はそれらに**開放度という新軸と接続係数**を足す（additive）。

### ★★★ 最優先（真の欠落・ポパーのシグネチャー）

#### POPR 開放度スペクトル（`OpennessState`/`OpennessRules`）
- **`openness`(0..1)**：制度的に閉鎖的（0）↔ 批判と修正を許容する開放社会（1）。`FactionData.ideology`（専制/民主）とは**直交**—民主でも硬直化した低開放状態、専制でも適応的な高開放状態はありうる。
- **`errorStock`（誤り蓄積量）**：openness が低いほど誤りが修正されず蓄積する。`AccumulationRate(openness)` = 高 openness → 蓄積 < 修正 = net 減少、低 openness → 蓄積 > 修正 = net 増加。
- **`adaptationCapacity`**：openness 比例の自己修正速度。改革・批判の許容 → errorStock を削減。
- 接続：`FactionState` に新フィールド `OpennessState` を追加。`FactionStateRules.Tick` でターン進行。`DynastyRules.腐敗` の増速/抑制修正子として使用。

#### POPR 漸進的改革 vs 全体改造（`PiecemealEngineeringRules`）
- **`enum ReformMode { Piecemeal（漸進）, Utopian（全体改造） }`**。
- **`PiecemealOutcome(effort)`**：少量・確実・reversible。errorStock 小幅削減、openness 微増、失敗コスト小。
- **`UtopianOutcome(effort, openness, roll)`**：高分散—openness 高なら大利、低なら崩壊リスク急騰（`DynastyRules.Revolution` に繋げる）。
- **`RecommendMode(opennessState)`**：openness 高 → Piecemeal 推奨 / 低 → Utopian は賭け。`EventEngine` の選択肢として提示。
- 接続：`DynastyRules.Reform`/`Revolution` の**前段フィルタ**。新規 `PiecemealEngineeringRules`（純ロジック・test-first）。

### ★★ 高（閉鎖体制の脆性・容認の危険）

#### POPR 誤り蓄積と脆性崩壊（`InstitutionalCorrectionRules`）
- `errorStock` が `brittleThreshold` を超えると、次の危機で崩壊確率が非線形に跳ねる（**脆性**）。平時は普通に見えるが臨界後は急速崩壊。
- **`BrittlenessFactor(errorStock)`**：ステップ関数—閾値未満はほぼ 1.0、超過後に崩壊確率係数が急騰。
- **`CorrectionCapacity(openness)`**：高 openness → 修正速度 > 蓄積速度 → 永続健全。
- 接続：`OpennessRules` の補完モジュール。`FactionStateRules.Tick` で `BrittlenessFactor` を `IsCollapsing` 判定に渡す。既存の `DynastyRules.腐敗` 経路に**並列経路**として追加。

#### POPR 寛容のパラドックス（`ToleranceParadoxRules`）
- **`IntolerantPresence(faction)`**：勢力内に openness=0 を志向する下位勢力（内部勢力 #113 / `LoyaltyRules.Allegiance` から）が存在するか。
- **`CaptureRisk(openness, intolerantPresence)`**：openness 高 × 不寛容派存在 = 容認で内側から浸食される確率。
- **`ShouldSuppressForSurvival(captureRisk)`**：captureRisk 超閾値 → 不寛容派の抑制が「開放社会の自己防衛」として正当化される。ただし抑制は `openness` コストを払う（ジレンマ）。
- 接続：`FactionRelations`/`DiplomacyRules` → `LoyaltyRules` と `FactionStateRules`。

### ★ 中（歴史主義修正子・世界観lore）

#### POPR 歴史主義の罠（`HistoricismTrapRules`）
- **「歴史の必然によって我々が勝つ」**という確信 → 適応を拒む（批判・反証を受け入れない）→ `openness` に下方圧力。
- **`HistoricismPressure(ideology, regimeLegitimacy)`**：革命的正統性が強い体制 → `openness` が硬直化（下限クランプ効果）。
- **`RigidityModifier`**：`adaptationCapacity` へのペナルティ乗数。
- 接続：`FactionData.ideology` × `DynastyRules.Regime` → `OpennessRules` への外部修正子。コード少量（2〜3純関数）で既存系に接続。

#### POPR（lore）世界観の開示データ
- 「開放社会の哲学と制度の脆弱性」「歴史主義批判（歴史法則への確信が制度を硬直させる）」「エンディング分岐：開放社会の確立 or 全体主義の到来」。
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への**loreデータ入力**。銀英伝の主題に直結。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 王朝サイクル・腐敗・革命そのもの | **`DynastyRules`/`Regime` がカバー**。POPR は修正子として接続するだけ |
| 社会合意の撤収・統治不能 | **`ConsentRules`/`Polity` がカバー** |
| クーデター | **`CoupRules` がカバー** |
| 文民統制の制度型 | **`CivilianControlRules` がカバー** |
| プラトン/マルクス/ヘーゲルの固有思想体系を個別実装 | タイクン化回避・固有名使用回避。構造のみ（開放度/歴史主義修正子）で実装 |
| 哲人王・エリート統治の固有ルール新設 | **`GovernmentRegistry`/`OfficeRules`（階級ゲート付き役職）がカバー** |
| 正義の多元主義（功利/ロールズ/リバタリアン）新設 | **`JusticeRules`（#918-923）がカバー** |

---

## 3. EPIC #POPR の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。既存政体ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #1510**。GitHub issue 起票済み（#1511〜#1523）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **POPR-1** | #1511 | 開放度スペクトル（`OpennessState`/`OpennessRules`＝自己修正能力・誤り蓄積・適応速度） | `FactionState`新フィールド。`DynastyRules.腐敗` 修正子。test-first |
| **POPR-2** | #1514 | 漸進的改革 vs 全体改造（`PiecemealEngineeringRules`＝リスク分布の二様・改革モード選択） | `DynastyRules.Reform`/`Revolution` の前段フィルタ。`EventEngine`。test-first |
| **POPR-3** | #1517 | 誤り蓄積と脆性崩壊（`InstitutionalCorrectionRules`＝errorStock 臨界→非線形崩壊確率） | `FactionStateRules.IsCollapsing` の新経路。`OpennessRules` の補完。test-first |
| **POPR-4** | #1518 | 寛容のパラドックス（`ToleranceParadoxRules`＝不寛容派の容認→乗っ取りリスク→抑制のジレンマ） | `LoyaltyRules`×`FactionRelations`×`FactionStateRules`。test-first |
| **POPR-5** | #1521 | 歴史主義の罠 修正子（`HistoricismTrapRules`＝必然論イデオロギー→適応拒否→脆性増） | `FactionData.ideology`×`DynastyRules`→`OpennessRules` 外部修正子。test-first |
| **POPR-6** | #1523 | （lore）世界観の開示データ（開放社会哲学/歴史主義批判/エンディング分岐） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`POPR-1`（開放度軸の骨格を確立）→ `POPR-3`（脆性崩壊の動学＝POPR-1の補完）→ `POPR-2`（改革モード選択が開放度を動かす）→ `POPR-4`（寛容のパラドックス＝最も政治的に面白いメカニクス）→ `POPR-5`（修正子は他4つの後でよい）→ `POPR-6`（lore整理は最後）。

> いずれも既存の政体ロジックを**後退させず接続**する additive 設計。銀英伝の主題「民主 vs 帝国」に最も効く。
