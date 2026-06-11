# カーネマン『ファスト&スロー』参考設計（EPIC #KAHN）

> 参照元：ダニエル・カーネマン『ファスト＆スロー——あなたの意思はどのように決まるか？』（原著 *Thinking, Fast and Slow*）。
> 行動経済学・認知心理学の基礎理論——**プロスペクト理論・判断ノイズ・過信バイアス・フレーミング効果・二重過程理論**——を銀英伝風戦略ゲームの意思決定モデルへ構造的に変換する。
> 著作権注意：固有名・文章・キャラクターは流用せず、**メカニクス／世界観の構造パターンのみ**を参考にする。

---

## 0. なぜ「ファスト＆スロー」が本システムに役立つか

当プロジェクトは意思決定の**合理的行為者モデル**を大量に保有している：

| 既存（カバー範囲） | カバー内容 |
|---|---|
| `LoyaltyRules`/`BattleAllegianceRules` | 忠誠・調略・寝返りカスケード |
| `ConsentRules`/`Polity` | 合意→非協力・統治崩壊 |
| `FactionLoyaltyRules` | 国家状態↔諸侯の忠誠の連結 |
| `FactionState`/`CampaignRules` | 社会シミュ層の合成と最上層 |
| `EventRules`/`EventEngine` | 条件発火→効果（確率的） |
| `FleetMorale` | 士気→戦闘能力係数 |
| `EspionageRules` | 情報収集・妨害工作 |
| `FleetAI` | AIの状態機械（接近/交戦/撤退） |
| `CombatModifiers`/`ModifierStack` | 能力→倍率の公式 |
| `JusticeRules`（サンデル#918-923） | 正義観×正統性 |

**しかし、これらはすべて「合理的行為者が完全情報の元で期待効用を最大化する」前提のもとに動作している。** カーネマンの研究が示す人間の意思決定のリアルがすっぽり抜けている：

| ファスト＆スロー固有の視点 | 当プロジェクトでの欠落 |
|---|---|
| **損失回避（プロスペクト理論）** | 利得より損失が痛い非対称性。守る者が攻める者より強く戦う。既存 `LoyaltyRules` の効用計算は対称（線形） |
| **判断ノイズ（バイアスと直交するランダム散乱）** | バイアスは系統的誤差、ノイズはランダム誤差——同じ状況で同じ将軍が別の結論を出す。全ロジックは確定論的または一様分布のroll |
| **過信バイアスと計画錯誤** | 自軍を過大評価し、遠征コストを過小評価する系統的傾向 → `FleetAI` は現実値で判断しバイアスなし |
| **フレーミング効果** | 「90%生存」と「10%戦死」は同じ事実だが反応が違う。`NotificationCenter` は中立情報 |
| **近接記憶バイアス（利用可能性ヒューリスティック）** | 最近の大勝敗が以降の確率判断を歪める → 現在はイベントが独立して発火 |
| **二重過程（速い直感 vs 遅い熟慮）** | 提督の認知スタイル差が戦術判断に影響 → `AdmiralData` は能力値6本のみ（スタイルなし） |

**結論**：カーネマンは既存の合理的行為者モデルに**認知バイアスと判断ノイズという人間くさい歪み**を加え、特に**①損失回避（守る者が強く戦う非対称性）②判断ノイズ（リアルな揺らぎ）③過信/計画錯誤（AIの無謀な遠征）**が最も実装価値が高い。タイクン化回避のため選別した6軸に絞る。

---

## 1. 役に立つ視点（要約）

本システムに効く形で1行ずつ：

1. **損失回避** — 「取られる」痛みは「得る」喜びの約2倍。占領地防衛の動機は領土拡張より強い → `LoyaltyRules.ResolveStance` の効用計算に参照点と非対称性を足す（#817 関ヶ原 SEKI と直結）。
2. **判断ノイズ** — 同じ将軍・同じ状況・別の結論。バイアス（系統的誤差）と直交するランダム散乱。現実感と予測不能性を与える → `EventRules`/`LoyaltyRules` のroll に「ノイズ幅」を加える。
3. **過信と計画錯誤** — 英雄が無謀な遠征を仕掛けるのはサバイバーシップバイアス。自軍過大評価・所要時間過小評価の系統的傾向 → `FleetAI` 接近閾値と `ShipyardRules` 建艦コスト見積もりに偏りを加える。
4. **フレーミング効果と宣伝戦** — 同じ損害でも「帝国の盾となった勇敢な戦死」と「連敗が続く」では忠誠・士気への影響が異なる → `EspionageRules`（諜報が世論を形作る）と `NotificationCenter`（情報の切り口）に接続。
5. **近接記憶バイアス** — 大規模会戦の直後は小さなリスクも大きく見える、その逆も然り。「最近の大事件」がその後の判断確率を歪める → `EventEngine` に事件後の偏り係数を加える。
6. **二重過程（速い直感型 vs 遅い熟慮型）** — 提督の認知スタイルを `AdmiralData` に追加：直感型（System 1）は素早い決断だが外れやすく、熟慮型（System 2）は遅いが精度が高い → `FleetAI` の判断遅延・精度に影響。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**既存ロジック（`LoyaltyRules`/`FleetAI`/`CombatModifiers`）を作り直さない**。KAHNはそれらに**認知バイアスの係数・揺らぎ・スタイル差**を additive に足すだけ。

### ★★★ 最優先（プロジェクトの合理的行為者仮定に最も大きな穴を開ける）

#### KAHN 損失回避と参照点 — `ProspectRules`/`ProspectState`（プロスペクト理論）
- **参照点（reference point）**：現在の兵力/領土を「ゼロ点」とし、そこからの得失を評価。
- **損失回避係数 `LossAversionFactor`（既定 2.0）**：損失の痛みは同量の利得の喜びの2倍。
- `ValueFunction(delta, referencePoint, lossAversion)` — プロスペクト価値関数（純関数）。
- `DefenseBonus(currentOwner)` — 守る者の実効兵力に損失回避係数を乗算（守る者が強く戦う）。
- **接続先**：`LoyaltyRules.ResolveStance`（実効忠誠に参照点補正）／`ConsentRules.ControlStrength`（統治への非協力は「現状失う」恐怖で増幅）／`FactionLoyaltyRules.BaselineLoyalty`（国家衰退は損失フレームで加速）。
- 純ロジック、EditMode test。

### ★★ 高（合理的行為者モデルを現実化する次の層）

#### KAHN 判断ノイズ — `JudgmentNoiseRules`（バイアスと直交するランダム散乱）
- **Occasion Noise**：同じ判断者・同状況でも結論が揺れる（例：同じ提督が同じ劣勢で「静観」か「撤退」か）。
- `OccasionNoise(baseValue, noiseSigma, roll)` — 正規分布状の揺らぎを基準値に加算。
- `NoiseLevel` — 判断ノイズの大きさ（提督の `intelligence` で低下・`HasStaff` で低下）。
- **接続先**：`LoyaltyRules.ResolveStance`（判断にノイズを加え寝返り閾値の揺らぎ）／`EventRules.IsEligible`（条件の確率的揺らぎ）／`LifecycleRules.ShouldDieOfAge`（roll の精度向上）。
- 純ロジック、EditMode test。

#### KAHN 過信バイアスと計画錯誤 — `OverconfidenceBiasRules`
- **過信係数 `OverconfidenceFactor`**：`AdmiralData.attack`/`leadership` が高い提督ほど自軍を過大評価し、脅威閾値をずらす。
- **計画錯誤係数 `PlanningFallacyFactor`**：建艦コスト・遠征日数の見積もりに系統的な楽観バイアスを加える。
- `BiasedStrengthEstimate(actualStrength, overconfidence)` / `BiasedCostEstimate(trueValue, fallacy)` — 純関数。
- **接続先**：`FleetAI`（接近閾値の補正 → AI が無謀な遠征を仕掛ける）／`ShipyardRules.Tick`（生産力見積もりの歪み）／`StrategyRules`（戦力評価に偏り）。
- 純ロジック、EditMode test。

#### KAHN フレーミング効果と宣伝戦 — `FramingRules`
- **フレーム型 `FrameType`**：`{損失強調, 利得強調, 中立}`。
- `FramedLoyaltyDelta(rawDelta, frame, audienceLoyalty)` — 同じ客観的変化でも損失フレームは効果 `LossAversionFactor` 倍、利得フレームは通常倍率。
- **宣伝 `PropagandaShift(faction, event, frame)`** — `EspionageRules` が情報操作として呼び出す。
- **接続先**：`EspionageRules`（情報操作の効果に枠組みを与える）／`LoyaltyRules`（フレームが寝返り確率に影響）／`NotificationCenter.Push`（通知の切り口が民心に影響）。
- 純ロジック、EditMode test。

### ★ 中（世界観の深みを加える）

#### KAHN 近接記憶バイアス — `AvailabilityBiasRules`（最近の大事件が確率判断を歪める）
- **顕著性スコア `SalienceScore`**：大会戦敗北・大勝利・英雄死去などのイベントが確率認知を歪める量。
- `AvailabilityWeight(eventSalience, timeSinceTurn)` — 時間減衰あり。
- `BiasedProbability(trueProbability, recentSalience)` — 顕著なイベント後は危険も機会も過大評価。
- **接続先**：`FleetAI`（大敗後は撤退閾値が高まる / 大勝後は過信に乗算）／`EventEngine`（イベントの発火重みに近接記憶を加算）。
- 純ロジック、EditMode test。

#### KAHN 二重過程と提督の認知スタイル — `DualProcessRules`
- **認知スタイル `CognitiveStyle`**：`{直感型, 熟慮型, 混合型}`。
- 直感型（System 1）：決断が速い（判断遅延 `DecisionDelayFactor` 低）が精度は低い（ノイズ大）。
- 熟慮型（System 2）：決断が遅い（`DecisionDelayFactor` 高）が精度が高い（ノイズ小・過信バイアス小）。
- `DecisionDelayFactor(style)` / `NoiseMultiplier(style)` / `OverconfidenceMultiplier(style)` — 純関数。
- **接続先**：`AdmiralData`（`cognitiveStyle` フィールド追加）／`FleetAI`（決断の遅延/精度への反映）／`JudgmentNoiseRules`（ノイズ幅の調整）／`OverconfidenceBiasRules`（過信の強弱）。
- 純ロジック、EditMode test。

#### KAHN（lore）世界観開示データ — 「合理的行為者の虚構」「AIの限界」「直感と論理」
- **コード新設なし** `DisclosureLedger`（FND-4）への**loreデータ入力**。
- 「宇宙では計算機が戦略を立てるが、将軍は人間でなければならない」——直感の非合理性こそが意表をつく。
- 「確率の専門家でさえ判断ノイズを免れない」——帝国エリートの合理主義への皮肉（銀英伝のテーマと共鳴）。
- CCX-6（世界観Codex退避）方針に一貫。

### ❌ 不採用（重複・タイクン化・未配線段階）

| 不採用 | 理由 |
|---|---|
| アンカリング効果（交渉への数値バイアス） | `DiplomacyRules`/`TreatyRules` 数値は未配線段階（DIP-2）。タイミング不適 |
| サンクコスト効果 | `ConsentRules`（撤退コスト）と実質的に重複 |
| ヒューリスティックの全列挙実装 | タイクン化回避：認知バイアスは選別した5軸のみ（全種実装しない） |
| 確証バイアス（都合の良い情報しか見ない） | `EspionageRules`/`DisclosureLedger` に接続できるが新モジュール必要な割に薄い |
| 後知恵バイアス（結果を知った後の「わかっていた」） | 実装対象が不明確。`GameEventDef` のflavor textとして処理で足りる |
| 後悔回避（後悔を恐れるあまり何もしない） | `ConsentRules.IsUngovernable` で実質カバー |

---

## 3. EPIC #KAHN の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。既存ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #1831**。GitHub issue 起票済み（#1833〜#1853）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **KAHN-1** | #1833 | 損失回避と参照点（`ProspectRules`/`ProspectState`・プロスペクト理論） | `LoyaltyRules`×`ConsentRules`×`FactionLoyaltyRules`。守る者が強く戦う非対称性 |
| **KAHN-2** | #1834 | 判断ノイズ（`JudgmentNoiseRules`・バイアスと直交するランダム散乱） | `LoyaltyRules`×`EventRules`×`LifecycleRules`。同状況で異なる結論 |
| **KAHN-3** | #1837 | 過信バイアスと計画錯誤（`OverconfidenceBiasRules`） | `FleetAI`×`ShipyardRules`×`StrategyRules`。AIが無謀な遠征を仕掛ける |
| **KAHN-4** | #1840 | フレーミング効果と宣伝戦（`FramingRules`） | `EspionageRules`×`LoyaltyRules`×`NotificationCenter`。情報の枠組みが判断を変える |
| **KAHN-5** | #1844 | 近接記憶バイアス（`AvailabilityBiasRules`・最近の大事件が確率判断を歪める） | `FleetAI`×`EventEngine`。大敗翌日は小リスクも大きく見える |
| **KAHN-6** | #1849 | 二重過程と提督の認知スタイル（`DualProcessRules`・直感型 vs 熟慮型） | `AdmiralData`×`FleetAI`×`JudgmentNoiseRules`×`OverconfidenceBiasRules` |
| **KAHN-7** | #1853 | （lore）世界観開示データ（合理的行為者の虚構・AIの限界・直感と論理） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`KAHN-1`（損失回避＝最も実装価値が高く、関ヶ原#817に直結） → `KAHN-2`（判断ノイズ＝全判断系の基盤として横断的に効く） → `KAHN-3`（過信/計画錯誤＝AIの行動を現実化） → `KAHN-4`（フレーミング＝諜報×宣伝の効果に深みを与える） → `KAHN-5 → KAHN-6`（近接記憶バイアス→認知スタイル）→ `KAHN-7`（lore）。

> いずれも既存ロジックを**後退させず接続する**additive設計。関ヶ原型`LoyaltyRules`（#817）と群像・社会シミュ層に最も効く。
