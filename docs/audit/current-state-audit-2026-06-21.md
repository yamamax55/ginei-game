---
type: audit
date: 2026-06-21
status: current
tags: [audit, current]
---

# 現状監査 — 縦スライスの育成ループが閉じた（2026-06-21）

> **これが最新の現状監査。以後はこの1本を参照する**（過去の `current-state-audit-2026-06*` は失効＝参照非推奨）。
> 検証＝master の**実コードを直接 grep/精読**して裏取りした記録（推測でなく file:line）。本コンテナは dotnet/Unity とも無いため EditMode/Play は未実行（回帰は CI の TestHarness ループが担保）。
> 上位の方針は [[game-improvements]] / [[vertical-slice-roadmap]]。リスクの定性整理は [[game-critique]]（定性論として有効・ただし下記「解消済み」を反映して読む）。

---

## 0. 総括

**看板機能（軍人立身出世）の空回りは解消された。** 数日前まで赤だった「会戦の結果が出世と戦闘力へ届かない」配線が、master の実コードでは**もう繋がっている**。

> 会戦で勝つ → 武勲が貯まる → 主命を達成する → 提督が成長する → それがセーブで残る。
> **4X の育成ループが初めて end-to-end で閉じた。** 数日前には無かった、質的に大きい前進。

残る勝負どころは **新システムでなく**、①初見導線（チュートリアル）ゼロ、②構造的不均衡（Core 約15万行 : Game 127ファイル）のまま量を増やし続けるリスク、③物語に沿った **v1 スコープの確定**。

| 領域 | 判定 | 一言 |
|---|---|---|
| アーキテクチャ規律 | ✅ | asmdef4分割・単一窓口・直列化チェック自動化・観測層がCore生成に自動追従 |
| テスト基盤 | ✅ | Unity無し `TestHarness` で 1,020+本。回帰の壁が厚い |
| **会戦→武勲（P1-a）** | ✅ | **解消済**。`BattleManager.WriteHandoffResultAndReturn` が `ReportProtagonistBattle`、主命成否は `wonBattle \|\| Random`＝勝てば達成の主因 |
| **提督XP永続（P1-b）** | ✅ | **解消済**。`GrowthRegistry.GainExperience` で永続＋`CampaignSaveData.AdmiralGrowthSave`（安定キー=`admiralName`）でセーブ往復 |
| 叙勲/成長の取りこぼし | ✅ | `GrantBattleGrowthAndMedals` で潜行会戦経路の穴も封鎖 |
| S6 イベント提示UI | ✅ | `StrategyEventPanel` 実在 |
| S5 税レバー | ✅(設計通り) | デバッグ専用は**意図的**（`GalaxyView.Input.cs:68` 通常プレイはAI委任＝タイクン化回避）。穴ではない |
| CoreStateInspector 立身出世登録（P2-c） | 🟡 | `Register("立身出世", …)` 未配線（規約上の軽微な穴。執務机 Alt+J では可視） |
| 初見導線 | 🔴 | tutorial/onboarding 無し（HelpOverlay は H キーのみ） |
| 構造的不均衡 | 🔴 | Core 1,372ファイル/約15.2万行 : Game 127ファイル。**質は改善したが量の不均衡は不変** |
| 物語・世界観 | 🟢 | 独自IP化（[[星骸の諸侯]]）の骨格は良。肉付け（章=各18〜26行）はこれから |

---

## 1. 解消済み（実コードで裏取り）

監査 [[current-state-audit-2026-06-post2476]] で最優先（赤）とした2点は、現在の master で配線済み：

- **会戦→武勲（P1-a #2477）**：`Assets/Scripts/Game/BattleManager.cs`
  - `WriteHandoffResultAndReturn` が `ReportProtagonistBattle(winner)` を呼び、主人公の戦果を武勲インボックスへ。
  - 主命成否は `SovereignMandateRules.IsOpen(...) && (wonBattle || Random.value < mandateSuccessChance)`＝**勝利が達成の主因**（乱数はフォールバックに降格）。
  - `GrantBattleGrowthAndMedals` で**潜行会戦（戦略書き戻し経路）でも成長・叙勲**するよう穴を封鎖。
- **提督XP永続（P1-b #2477）**：
  - `GrowthRegistry.GainExperience(admiralData.GetInstanceID(), arch, amount, ...)` で会戦XPを永続（旧 `tempGrowth` 破棄は消滅）。
  - セーブ往復：`Assets/Scripts/Core/Society/CampaignSaveData.cs` に `AdmiralGrowthSave`（安定キー=`admiralName`・`ContentDatabase` で復元）。新規キャンペーンでは `GrowthRegistry.Clear()`（戦役固有＝持ち越さない）。

→ 「乱数で進む出世を、戦って勝ち取る出世へ」は**達成済み**。スコアカードで言えば「看板機能の動作 ★☆→★★★★」「プレイ可能性 ★★→★★★」。

## 2. 残課題（優先順）

### P-1 初見導線ゼロ（最優先・小さく効く）
- tutorial/onboarding 無し。`HelpOverlay` は H キーのみ・初回自動表示なし。
- 最小手：`PlayerPrefs` 判定で初回のみ操作ヒント1枚＋1ループの段階開示（既存 `HelpOverlay`/`GineiUITK` 流用）。

### P-2 v1 スコープの確定（物語に「何を切るか」を決めさせる）
- いま物語・人物に着手したのは、無限に広がる Core に**初めて輪郭を与える**好機。
- 方針：**物語の序章〜第二章が触れる勢力・人物・会戦・制度だけを「届く体験」にし、触れない Core は観測層止まり（凍結）に明示降格**。
- 具体：v1 の遊べる勢力を2〜3に絞る（例：[[黎明評議会]]＝主人公側／[[碧晶公国]]＝因縁の敵／[[灯心自由市ファロス]]＝弱小プレイヤーの足場）。残りは `FactionData` 追加で後から増える設計＝凍結コストはゼロ。

### P-3 Core量産の凍結（不均衡を悪化させない）
- `/core-wave`・参考EPIC自動生成は「アイデアの鉱脈が無限に湧く」装置。1200:1 の不均衡をこれ以上広げない。
- SAP/MRP級は既に凍結済（`theme:凍結`）。**同じ判断を新規Core全般へ広げ**、当面は「配線・肉付け・切る」に投資する。

### P-4 CoreStateInspector 立身出世登録（規約整合・軽微）
- `Assets/Scripts/Game/CoreStateInspector.cs` に `Register("立身出世", () => ProtagonistCareerDirector.Instance)` 1行（観測層規約どおり）。執務机 Alt+J で可視のため体験上の穴ではない。

---

## 3. 評価の更新（前回比）

> 前回（[[current-state-audit-2026-06-post2476]]）：「世界一よく設計された、**まだ遊べない**ゲーム」。
> 今回：「**縦の1本が、もう体験として手応えを持ち始めたゲーム**」。

質的転換は起きた。次の一手は **新システムではなく**、初見導線・v1スコープ確定・Core凍結という「完成へ寄せる」3点。

## 関連
- [[game-improvements]] — 一本を磨く完成方針
- [[vertical-slice-roadmap]] — 縦スライスS1〜S6の着地順
- [[game-critique]] — リスクの定性整理（本書の「解消済み」を反映して読む）
- [[星骸の諸侯]] — 多勢力の世界観ハブ（v1スコープ確定の基準）
- [[core-orphan-audit]] — 届かない Core の棚卸し（全配線は禁忌）
