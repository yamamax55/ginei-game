# アンダーソン『想像の共同体』参考設計（EPIC #NATN）

> 参照元：ベネディクト・アンダーソン『想像の共同体 ── ナショナリズムの起源と流行』（1983）。
> 「国民（nation）とは本質的に想像の産物である」——印刷資本主義・暦的同時性・植民地行政を通じて
> 帰属意識がいかに生産・伝播・コピーされるかを解析した政治社会学の古典。
> 本ドキュメントは、当プロジェクト（銀英伝風星間国家戦略）に固有の欠落軸を抽出し、
> EPIC `#NATN` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、
> **社会構造メカニクス・理論パターンのみ**を参考にする。

---

## 0. なぜ「想像の共同体」が本システムに役立つか

当プロジェクトは **国民・文化・ナショナリズム周辺のマクロロジックを既に保有** している：

| 既存（カバー範囲） | 実装箇所 |
|---|---|
| 同化圧力・分離独立リスク・ナショナリズム係数・亡命 | `CultureRules`/`Culture`（#194） |
| 住民思想・安定度・統合度・支配勢力の影響 | `GovernanceRules`/`Province`（#109） |
| 改宗圧力・異端・社会効果 | `ReligionRules`（#172-175） |
| 正統性・合意・協力・統治不能 | `ConsentRules`/`Polity`（#836） |
| 国家状態合成（王朝/統治体/組織/共同体） | `FactionStateRules`/`FactionState` |
| 情報収集・サボタージュ | `EspionageRules`/`SpyNetwork` |
| 人物ライフサイクル・キャリアパス・学閥 | `CareerPipelineRules`（LIFE-5/6/7） |

**しかし、これらは「国民意識が既に存在する」状態のマクロ均衡**であり、
アンダーソンが固有に示す以下の**生成プロセス**と**動学**が欠けている：

| アンダーソンが固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **印刷資本主義 → 国民意識の生産装置**：メディアネットワークが同一文化圏を製造し `NationalismFactor` を動的に生成する | `CultureRules.NationalismFactor` は係数として定義されるが、**どうやって国民意識が生まれるか**の生成メカニズムがない |
| **公定ナショナリズム（上からの国民形成）**：国家が言語政策・記念日・公教育で「国民」を積極的に製造する | `GovernanceRules` は統治安定だが、**国家による文化製造**（カテゴリーを命名することで実体を作る）の軸がない |
| **模倣的ナショナリズム（独立運動のテンプレート伝播）**：ある独立成功が周辺にコピー・模倣され連鎖する | `CultureRules.SeparatismRisk` は単独係数。**先行成功が周辺の閾値を下げる**連鎖伝播の動学がない |
| **カテゴリー統治（国勢調査・地図・記念物）**：国家が民族・言語・歴史を命名・ラベリングすることで人口を構成する | `Province.nativeIdeology` は初期値として固定。**国家がカテゴリーを再定義する**介入機能がない |
| **朝廷巡礼ネットワーク（キャリア移動が水平的連帯を生む）**：官僚・軍人が各地を歴任することで、地域を超えた「国民的」自己同一性を形成する | `CareerPipelineRules.CliqueBond` は同窓・同期の学閥。**地域横断キャリアが帰属意識を「星系」から「帝国全体」へ昇格させる**回路がない |

**結論**：アンダーソンは当プロジェクトの `CultureRules` に
「**ナショナリズムをどう生産するか**」という**生成ダイナミクスの欠落**を4つの欠落軸として与える。
また `CareerPipelineRules` に「**移動・巡歴が水平連帯を生む**」という第5の軸を追加する。
さらに世界観EPIC（開示エンジン）に「国民とは想像の産物」というloreを注入する。

---

## 1. 役に立つ視点（要約）

アンダーソンの論点を、**本システムに効く形**で1行ずつ：

1. **国民は想像の産物**——誰も全員と会わないのに「同じ仲間」と感じる。その感覚を生むのはメディアと制度の設計。→ `CultureRules.NationalismFactor` の動的生成源として機能する。
2. **印刷資本主義が文化圏を製造する**——同一言語で書かれた印刷物が「同時に」届く地域が一つの「国民」になる。星系間情報ネットワークが文化的連帯圏を生む宇宙類比。
3. **公定ナショナリズム＝国家が「国民」を作る**——独立運動が先か、国家の言語政策が先か。上からの文化製造（`FactionState.inclusiveness` の文化版）。
4. **独立成功は伝染する（模倣的ナショナリズム）**——一つの独立運動の成功が「可能性」を示し、周辺の分離独立リスクを高める連鎖。`SeparatismRisk` に「先行独立効果」を足す。
5. **国家が民族を作る（カテゴリーの政治性）**——国勢調査で民族ラベルを付けることで、以前は存在しなかった集団が「実体」化する。`Province.nativeIdeology` への国家介入回路。
6. **巡礼ネットワーク＝移動が連帯を生む**——各地を歴任した官僚・軍人は地域閥でなく「帝国全体」を自己同一性の基盤にする。帝国統一性 vs 地域分権の摩擦に接続。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`CultureRules`/`GovernanceRules`/`CareerPipelineRules` を作り直さない**。
> NATN はそれらに**欠落する生成ダイナミクスを足し、接続する**だけ（additive）。
> タイクン化回避：「どの星系でどの政策を打つか」のマイクロ操作ではなく、
> 高位の決断（情報網整備/公定ナショナリズム採用）→エンジン駆動→創発帰結（分離 or 統一）。

### ★★★ 最優先（真の欠落・アンダーソンのシグネチャ）

#### NATN 国民意識の生成メカニズム（情報ネットワーク → 帰属意識の動的生産）
- **星系間メディアネットワーク**（`MediaNetwork`）：接続済み星系の数・密度が「情報圏」を形成。
  同一情報圏に属する星系が「同じ国民」という帰属意識（`NationalismFactor`）を高める。
- 情報圏の密度は `GalaxyMap` のトポロジーと `SupplyRules.SuppliedSystems`（補給線＝情報線の類比）から計算。
- 勢力の「情報網整備」投資 → 網の密度上昇 → `NationalismFactor` 上昇 → `SeparatismRisk` 低下という高位の決断連鎖。
- 接続：新 `MediaNetworkRules`/`MediaNetwork` 純ロジック（test-first）→ `CultureRules.NationalismFactor` の**入力源**。
  `SupplyRules`（補給線=情報線）× `GalaxyMap` のネットワーク密度計算。

#### NATN 公定ナショナリズム（上からの国民形成・言語政策・記念日制定）
- **国家による文化介入**：勢力が「言語統一政策」「記念日制定」「公教育設計」を採択すると、
  省内 `Province.nativeIdeology` が時間をかけて所有勢力のイデオロギーへ引き寄せられる。
- `GovernanceRules.IdeologyModifier` の**能動的版**：政策なしは自然収束、政策ありは強制収束（速いが反発リスク）。
- 接続：新 `OfficialNationalismRules`/`NationalismPolicy`（enum）純ロジック → `GovernanceRules` × `Province.nativeIdeology` 介入。
  `FactionState.inclusiveness` の文化軸として機能（収奪↔包摂に**文化製造の軸**を足す）。

### ★★ 高優先（動学の遊びを足す）

#### NATN 模倣的ナショナリズムの連鎖伝播（独立成功がテンプレートとして拡散）
- 「隣の星系が独立した」という情報がある星系の分離独立リスクを**閾値効果**として高める。
  先行成功が「可能性の実証」を示し、周辺の `SeparatismRisk` に連鎖乗算。
- `GalaxyMap` 上で隣接関係 + 情報伝播速度（`MediaNetwork` 密度に依存）で波及範囲を決定。
- 接続：新 `NationalismContagionRules` 純ロジック → `CultureRules.SeparatismRisk` への修正子
  × `GalaxyMap`（隣接）× `EventEngine`（独立成功イベントのトリガー）。

#### NATN カテゴリー統治（国家による民族ラベリング・住民分類の再定義）
- 国家が「民族」「言語」「宗教」カテゴリーを制定・変更することで `Province.nativeIdeology` を再定義できる。
  現状は初期値固定だが「国家がカテゴリーを命名することで実体が生まれる」介入回路を追加。
- 統合が高い星系では再定義が定着、低い星系では反発（安定度低下・分離リスク増）。
- 接続：新 `PopulationCategorizationRules` 純ロジック → `Province` × `GovernanceRules.OnOccupied` 拡張
  × `DemographicsRules.Population`（人口ベース）。

### ★ 中優先（構造補完）

#### NATN 朝廷巡礼ネットワーク（キャリア横断が帰属意識を地域から帝国へ昇格させる）
- 複数の星系を歴任した官僚・軍人は「地域閥」でなく「帝国全体」を帰属基盤にする。
  `Person.hammockNumber`/`schoolId`（学閥）に加え、**歴任星系リスト**から帰属スケールを決定。
- 帝国規模の帰属意識を持つ人物は `FactionState.Stability` に正貢献するが、地域閥とのコンフリクトを生む。
- 接続：`CareerPipelineRules.CliqueBond` 拡張 × `Person` への `assignedProvinces` 追加 × `FactionState` へのフィードバック。

#### NATN （lore）世界観の開示データ（国民とは想像の産物・メディアが帝国を作る）
- コード新設なし。`DisclosureLedger`（FND-4 #495）への**loreデータ入力のみ**。
- 「帝国の絆は剣でなく共通の物語から生まれる」「情報網を失った文明圏は分裂する」
  「独立運動は発明されて、そして輸出された」——世界観EPICとしての洗練。
- 接続：`DisclosureLedger` × `EventEngine`（条件発火）× 開示EPIC（秘史/啓蒙）。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| ナショナリズム係数そのものの再実装 | `CultureRules.NationalismFactor` が既にカバー。NATN は入力源を足すだけ |
| 同化圧力・分離独立リスクの再実装 | `CultureRules.AssimilationPressure`/`SeparatismRisk` が既存。接続のみ |
| 宗教と国民感情の融合 | `ReligionRules.SocialEffect` が既にカバー |
| 言語・文化の多様性そのものの統計モデル | `CultureRules`/`Province.nativeIdeology` が既存。タイクン化回避（微操作になる） |
| 亡命・移住の物理モデル | `CultureRules.ExileLikelihood` が既存 |
| 「国民の物語」テキスト生成 | AIコンテンツ生成はスコープ外。loreは `DisclosureLedger` に留める |

---

## 3. EPIC #NATN の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。
> 既存文化・内政ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**社会構造メカニクス・理論パターンのみ**参考。

> **EPIC = #1874**。GitHub issue 起票済み（#1876〜#1893）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **NATN-1** | #1876 | 国民意識の生成メカニズム（`MediaNetworkRules`/`MediaNetwork`：情報圏密度 → `NationalismFactor` 動的生産） | `CultureRules.NationalismFactor` 入力源 × `GalaxyMap` トポロジー × `SupplyRules`（補給線＝情報線類比） |
| **NATN-2** | #1882 | 公定ナショナリズム（`OfficialNationalismRules`/`NationalismPolicy`：国家による文化製造・住民思想収束） | `GovernanceRules` × `Province.nativeIdeology` 介入 × `FactionState.inclusiveness` 文化軸 |
| **NATN-3** | #1884 | 模倣的ナショナリズムの連鎖伝播（`NationalismContagionRules`：独立成功の閾値効果が周辺 `SeparatismRisk` を高める） | `CultureRules.SeparatismRisk` 修正子 × `GalaxyMap` 隣接 × `EventEngine` |
| **NATN-4** | #1887 | カテゴリー統治（`PopulationCategorizationRules`：国家による民族ラベリング→ `Province.nativeIdeology` 再定義） | `Province` × `GovernanceRules.OnOccupied` 拡張 × `DemographicsRules` |
| **NATN-5** | #1890 | 朝廷巡礼ネットワーク（`CareerPilgrimageRules` 拡張：歴任星系 → 帰属スケール昇格 → `FactionState` フィードバック） | `CareerPipelineRules.CliqueBond` 拡張 × `Person` × `FactionState.Stability` |
| **NATN-6** | #1893 | （lore）世界観の開示データ（「国民とは想像の産物」「情報網が帝国を作る」「独立は輸出される」） | `DisclosureLedger`（FND-4 #495）。コード新設なし |

### 推奨着手順
`NATN-1`（情報圏生成＝最も固有で欠落の大きいシグネチャ）→
`NATN-2`（公定ナショナリズム＝国家の能動的文化介入）→
`NATN-3`（模倣的伝播＝分離独立の連鎖）→
`NATN-4`（カテゴリー統治）→
`NATN-5`（巡礼ネットワーク）→
`NATN-6`（lore・コードなし）。

> いずれも既存文化・内政ロジックを**後退させず接続する** additive 設計。
> `CultureRules`（#194）の `NationalismFactor`/`SeparatismRisk` に最も効く。
