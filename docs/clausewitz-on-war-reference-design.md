# クラウゼヴィッツ『戦争論』参考設計（EPIC #CLZ）

> 参照元：カール・フォン・クラウゼヴィッツ『戦争論（Vom Kriege）』（1832年没後出版）。
> プロイセン将軍による戦争哲学の集大成——「戦争は政治の継続」「摩擦」「重心」「三位一体」。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略＋既存の社会・政治・軍事純ロジック層）にとって
> **役に立つ視点だけを抽出し**、EPIC `#CLZ` として issue 化する提案。
> **著作権注意：固有名・文章・キャラクター・固有設定は流用せず、戦争哲学のメカニクス／構造パターンのみを参考にする。**

---

## 0. なぜ「戦争論」が本システムに役立つか

当プロジェクトは戦争の**マクロ・政治的次元をすでに広く保有**している：

| 既存（マクロ・政治） | カバー範囲 |
|---|---|
| `WarGoalRules`/`CasusBelli`/`WarWeariness`/`GoalLegitimacy`/`PeaceAcceptance`（Wave 2） | 戦争目標・開戦理由・厭戦・講和受諾の純ロジック |
| `CulminatingPointRules`（SUN-4） | 攻勢終末点（補給距離比例の戦力効率低下） |
| `DeceptionRules`（SUN-1） | 欺瞞作戦・情報操作による戦場の霧 |
| `FactionState`/`ConsentRules`/`HopeRules` | 民心・合意・政治正統性・希望/末人 |
| `DiplomacyRules`/`DiplomacyState`（DIP-1） | 戦争/平和の外交的状態遷移 |
| `CivilianControlRules`/`CoupRisk`（GOV-4） | 文民統制・クーデターリスク |

**しかし、これらは「政治」と「軍事」を並列に持ちながら、その相互作用の回路が薄い。**
クラウゼヴィッツが固有に与える以下の視点が欠けている：

| 『戦争論』が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **作戦摩擦**（計画と実行の乖離・偶発的失敗） | 複雑な作戦ほど計画通りに動かない確率モデルが無い。戦闘は現在ほぼ決定論的に解決される |
| **政戦連接**（戦争は政治の継続＝軍事手段を政治目標に比例させる） | `WarGoalRules` は純ロジックとして存在するが、`CampaignRules.Tick` や `FactionState` への配線が無く、政治目標が軍事行動の規模を制御できていない |
| **三位一体の緊張**（政府×軍×民衆が戦争で引き裂かれる） | 三者は個別に存在するが、戦時の整合崩壊（「政府は戦いたいが民衆は嫌だ」）を検出し帰結を出す専用ルールが無い |
| **重心ターゲティング**（敵の中枢を同定して集中打撃） | 星系グラフ上の「ここを落とせば敵は崩れる」という CoG 分析がAIに無い。個艦・個星系を叩くだけで戦略的急所の認識が無い |

**結論**：『戦争論』は既存の軍事・政治・社会モジュールに**「政治と軍事を繋ぐ回路」**を与える。
具体的には ①作戦摩擦（計画の劣化） ②政戦連接（`WarGoalRules`の配線） ③三位一体の緊張（戦時の三者崩壊検知） ④重心分析（AI戦略の急所認識）の4欠落軸。

---

## 1. 役に立つ視点（要約）

『戦争論』の世界観を、**本システムに効く形**で1行ずつ：

1. **「戦争は政治の継続」＝軍事手段は政治目標の奴隷**。目標を超えた戦争はエスカレートして自壊する。→ `WarGoalRules.GoalLegitimacy` を `CampaignRules` に配線し、政治目標と軍事コミットの釣り合いを取る。
2. **摩擦（Friction）＝計画は敵・地形・偶発事・疲労に抵抗される**。現実は常に計画より遅く・高コストで・不確実だ。→ 新 `FrictionRules`：命令系列の長さ・補給状態・士気からくる作戦劣化係数（`CombatModifiers` に乗算）。
3. **三位一体（政府×軍×民衆）＝三者の利害が戦争を規定する**。民衆の情念が尽き・軍が消耗し・政府の目的が失われると戦争は終わる。→ 戦時の三者整合崩壊を `TrinitarianTensionRules` で検出し `WarGoalRules.PeaceAcceptance` へ送る。
4. **重心（CoG）＝敵の力の源泉を叩く**。それは首都かもしれず、主力艦隊かもしれず、同盟の結束点かもしれない。→ 新 `CenterOfGravityRules`：`GalaxyMap`×勢力状態から CoG 星系/艦隊を同定し、AI 戦略行動を優先順位づけ。
5. **防勢の相対的優位＝守る方が攻めるより強い（局地）**。しかし戦略的には攻めなければ目標を達成できない。→ 既存 `CombatModifiers` の防御補正に、「守備側ホーム補正」として接続。孫子の SUN-4（攻勢終末点）と相補。
6. **限定戦争/絶対的戦争＝政治目標の強度が戦争の規模を決める**。小さい目標のために全力を使うな。→ `WarGoalRules` と `FleetPool` を繋ぐ「戦争強度スケーラ」。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> **大原則：`WarGoalRules`/`CulminatingPointRules`/`DeceptionRules`/`FactionState` を作り直さない。**
> CLZ はそれらに**欠落回路を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・クラウゼヴィッツの signature）

#### CLZ-1 作戦摩擦 `FrictionRules`（計画と実行の乖離）
- **`Friction`（状態）**：`commandDepth`（命令階層の深さ）/ `supplyRatio`（補給充足率）/ `moraleRatio`（士気比率）/ `coordinatingFactions`（連合作戦の勢力数）。
- **`FrictionRules`（static）**：`BaseFriction`（値：0.0〜1.0）/ `FrictionFactor`（0.5〜1.0 の実効値乗数）/ `DegradeAction`（作戦計画の実行成功確率を返す）。
- 接続：`CombatModifiers`（`ModifierStack.Mul` で摩擦係数を乗算）× `StrategyRules.ResolveCorridorBattle`（自動解決の乱数幅を摩擦で拡大）× `FleetAI`（AI 行動の計画通りに動かない確率）。
- **純ロジック・test-first・基準値非破壊**（`CombatModifiers.FrictionFactor(Friction)` 関数を足す）。

#### CLZ-2 政戦連接 — `WarGoalRules`の戦役配線
- **`WarGoalRules`（Wave 2 純ロジック）**はすでに存在するが、`CampaignRules.Tick` に接続されていない。
- 配線：`WarGoalRules.WarWeariness` → `HopeRules.Shift`（希望低下） → `ConsentRules.PressurePolity`（非協力波及）。
- 配線：`WarGoalRules.GoalLegitimacy` → `FactionStateRules`の安定度係数。
- 配線：`WarGoalRules.PeaceAcceptance`（閾値超え）→ `DiplomacyRules.MakePeace`（自動講和をトリガー）。
- 配線：戦争強度スケーラ = 政治目標規模（`WarGoalRules.GoalLegitimacy`）が低いほど `FleetPool` への動員上限を制約（全力戦争と限定戦争の区別）。

### ★★ 高（既存への接続補強）

#### CLZ-3 三位一体の緊張 `TrinitarianTensionRules`
- **`TrinitarianState`**：`governmentWill`（政府の戦争継続意志＝`WarGoalRules.GoalLegitimacy`から）/ `militaryCapacity`（軍の残存戦力比＝`FleetPool` 充足率）/ `popularSupport`（民衆の支持＝`ConsentRules.ControlStrength`）。
- **`TrinitarianTensionRules`（static）**：`Alignment`（三者の調和度）/ `IsCollapsing`（政府×軍×民の一つが閾値割れ）/ `DominantTension`（最も低い次元）/ `PeacePressure`（崩壊度→`WarGoalRules.PeaceAcceptance`へ加算）。
- 接続：`CampaignRules.Tick` で毎ターン評価 → `WarGoalRules.PeaceAcceptance` 加算 → 閾値で自動講和トリガー（CLZ-2 と連携）。純ロジック・test-first。

#### CLZ-4 重心分析 `CenterOfGravityRules`
- **`CenterOfGravityRules`（static）**：`IdentifyCoG(GalaxyMap, FactionData)` = 勢力の「落とされたら崩れる星系/艦隊」を同定。スコア＝（所有星系の接続度 `LogisticsRules.CohesionFactor`）×（首都フラグ）×（兵站ハブ性）。
- **`PrioritizeCoG(StrategicFleet, cogscore)`**：AI が攻略優先順を CoG スコアで並べ替え。
- 接続：`GalaxyView` AI の進軍目標選択で `CenterOfGravityRules.IdentifyCoG` を参照。`StrategyRules` の自動解決で CoG 陥落時にボーナスダメージ（連鎖的勢力低下）。純ロジック・test-first。

### ★ 中（補足・世界観）

#### CLZ-5 （lore）戦争哲学の開示データ
- **コード新設なし**：`DisclosureLedger`（FND-4）への lore データ入力のみ。
- 「戦争は政治の継続である」という概念の開示、「摩擦こそ現実と理想の差」、「重心を突けば崩れる」などを秘史・歴史断片として登録。
- 接続：`DisclosureLedger.Register`（条件：複数の戦争を経験後に開示）。CCX-6 世界観 codex 方針に一貫。

### ❌ 不採用（重複・タイクン化・後退）

| 不採用 | 理由 |
|---|---|
| 攻勢終末点 | **SUN-4 `CulminatingPointRules` がカバー**（重複新設しない） |
| 戦場の霧・欺瞞 | **SUN-1 `DeceptionRules` がカバー**。CLZ の摩擦（CLZ-1）は情報操作でなく**実行失敗モデル** |
| 個人将軍の戦略的天才 | 数値ゲーム化・タイクン化。既存 `AdmiralData`/`AutonomyRules` で十分 |
| 地形マイクロ（高地・川・森） | タイクン化回避。回廊グラフで十分 |
| 戦争の段階（序戦/主会戦/追撃）の演出 | 既存 BattleManager/`TimeFlowRules` が担う。重複新設しない |
| `WarGoalRules`/`DiplomacyRules` の再実装 | 両方とも既存。CLZ-2 は接続のみ |

---

## 3. 子Issue表（着手順）

> 純ロジック（CLZ-1/3/4）は TestHarness/EditMode で先に固定（test-first）→ 戦役ループ（CLZ-2）へ配線。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。
> **EPIC = #1132**。GitHub issue 起票済み（#1133〜#1137）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **CLZ-1** | #1133 | 作戦摩擦モデル（`FrictionRules`/`Friction`：命令深度×補給×士気→実行成功確率） | `CombatModifiers`×`StrategyRules`×`FleetAI`。純ロジック・test-first |
| **CLZ-2** | #1134 | 政戦連接 — `WarGoalRules`の戦役配線（厭戦→民心・目標正統性→安定・講和受諾→外交） | `WarGoalRules`×`CampaignRules.Tick`×`DiplomacyRules`×`FleetPool`動員上限 |
| **CLZ-3** | #1135 | 三位一体の緊張（`TrinitarianTensionRules`：政府意志×軍力×民支持の崩壊検知） | `WarGoalRules.PeaceAcceptance`×`CampaignRules.Tick`。純ロジック・test-first |
| **CLZ-4** | #1136 | 重心分析（`CenterOfGravityRules`：銀河グラフ上の CoG 星系/艦隊の同定とAI優先化） | `GalaxyView`AI×`StrategyRules`×`LogisticsRules`。純ロジック・test-first |
| **CLZ-5** | #1137 | （lore）戦争哲学の開示データ（「戦争は政治の継続」他を `DisclosureLedger` へ入力） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順

`CLZ-1`（摩擦＝最も固有で既存に差し込める・test-first）
→ `CLZ-3`（三位一体＝純ロジックを先に固める）
→ `CLZ-4`（重心＝AI改善の核）
→ `CLZ-2`（政戦連接＝既存 `WarGoalRules` の配線・戦役ループを閉じる）
→ `CLZ-5`（lore 入力）

> いずれも既存軍事・政治・社会モジュールを**後退させず接続**する additive 設計。
> `WarGoalRules`（Wave 2）への接続が開通することで、「なぜ戦争が始まり・エスカレートし・終わるか」の純ロジックが一本繋がる。
