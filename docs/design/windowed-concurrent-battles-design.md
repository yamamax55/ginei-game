---
type: design
tags: [design]
---

# 会戦のウィンドウ化＋複数同時潜行（設計）

> 戦略マップから複数の会戦へ「同時に潜行」し、それぞれを**別ウィンドウでライブ進行**できるようにする。
> 本作の宿願（銀英伝＝複数戦線が並行して進む）に対応。**完全同時ライブ**方針（ユーザー選択）。
> 既存のフルスクリーン会戦を壊さず、段階的に移行する。各段階は独立 PR＝実機 Unity で目視検証してから次へ。

## ゴール
1. 戦術マップ（会戦）を**ウィンドウ化**（全画面シーン置換をやめ、戦略マップの上に重なる会戦ウィンドウ）。
2. 戦略マップで**潜行すると新規ウィンドウが開く**（戦略マップは背後に生き続ける）。
3. **複数の会戦を同時に潜行**でき、各ウィンドウが**それぞれライブ進行**（独立した一時停止/倍速）。

## 現状の制約（なぜ大改修か）
調査結果（`docs/` 調査ログ参照）より、会戦コアは**グローバル単一**前提：
- **シーン置換**：`SceneLoader.LoadScene` は単一シーン置換。additive ロードは未使用。
- **`BattleHandoff` が static 単一スロット**＝同時に1会戦ぶんしか受け渡せない。
- **`FleetRegistry` が static 単一在庫**＝全旗艦/全配下艦を1リストで保持。`ShipCombat`（敵探索）・`LanchesterRules.LocalFirepower`・`ZoneOfControl`・`FleetAI`・`BattleManager`（勝敗集計）・`BattleAllegianceManager` が**この単一在庫を直接読む**。
- **`Time.timeScale` がグローバル**＝会戦の一時停止/倍速が全体に効く（独立できない）。
- **物理（Physics2D）がグローバル**＝別会戦の配下艦コライダが互いに干渉しうる。
- 現状でも会戦は戦略マップ上に複数「存在」できるが、**ライブ操作は常に1戦のみ**。残りは `AutoBattleSim`（ランチェスター積分）で抽象解決。

→ 真の同時ライブには「会戦ごとに独立したワールド・在庫・時間・描画」を持たせる必要がある。

## 設計の柱：`BattleContext`（会戦インスタンス）
1会戦＝1`BattleContext`。次を**所有**する：
- **専用 additive シーン**（`LoadSceneMode.Additive` ＋ `LocalPhysicsMode.Physics2D`）＝在庫/物理/GameObject を会戦ごとに**自然に隔離**（Unity が物理を per-scene に分ける）。
- **専用 `FleetRegistryScope`**（static `FleetRegistry` を会戦ごとのインスタンスへ）。
- **専用カメラ → RenderTexture**＝戦略マップ上の uGUI ウィンドウへ表示（`SystemMapWindow` の実証済みパターンを踏襲）。
- **専用 時間係数**（`BattleClock`＝realDt×自前 speed／pause。`Time.timeScale` に依存しない）。
- **専用 `BattleManager`/`BattleSetup`/`FleetCommander`**（そのシーン内に常駐し自分の Context だけを操作）。
- `BattleHandoff` 相当のデータ（参戦艦隊・防御側・攻城/通常）。

`BattleDirector`（戦略側 static/MonoBehaviour）が全 `BattleContext` を束ね、ウィンドウ生成/破棄・フォーカス・入力ルーティング・結果の戦略への反映を司る。

## 隔離の指針（終盤ラグ規律 PERF #1117 と整合）
- additive シーン＋per-scene physics で**会戦間の N² 干渉を作らない**（各会戦は自分の在庫だけ走査）。
- 同時ライブ数に**上限**（例：4）を設け、超過分は従来どおり `AutoBattleSim` 抽象解決＝シミュ LOD（見ていない/枠が無い会戦は粗く）。
- 描画 RenderTexture は**非フォーカス窓は低解像度/低更新**に落とす（観測解像度とシミュ解像度の分離）。

## 段階（各段階＝独立 PR・実機検証）
### Stage 1：会戦をウィンドウ化（単一ライブ）
- Battle を additive ＋ LocalPhysics でロードし、カメラを RenderTexture へ。戦略マップを背後に残す。
- 潜行で**会戦ウィンドウ**が開く（ドラッグ移動・×閉じ・`WindowChrome`/`SystemMapWindow` 流用）。
- 入力（クリック→ワールド座標）はそのウィンドウのカメラで変換。
- この段階は static `FleetRegistry`/`Time.timeScale` のまま＝**同時は1戦**。既存フルスクリーン経路はフォールバックとして温存。
- 受け入れ：潜行でウィンドウが開き、中で会戦が動き、閉じる/決着で戦略へ結果反映。

### Stage 2：会戦コアのインスタンス化（隔離の核）
- `FleetRegistry` を会戦ごとの `FleetRegistryScope` に（static は後方互換の既定スコープへ委譲）。各艦は自分の Context のスコープへ登録。
- `ShipCombat`/`LanchesterRules.LocalFirepower`/`ZoneOfControl`/`FleetAI`/`BattleManager`/`BattleAllegianceManager` を**所属 Context のスコープ参照**へ移行（敵探索が他会戦を拾わない）。
- `BattleClock`（per-battle time）を導入し、`FleetMovement`/`FleetWeapon`/各タイマーが `Time.deltaTime×timeScale` でなく自 Context の dt を読む（純計算は Core・test-first）。
- 受け入れ：2会戦を additive で同時にロードしても互いに干渉しない（敵を撃たない・時間が独立）。

### Stage 3：複数同時潜行＋ウィンドウマネージャ
- 複数の `BattleContext` を同時生成。`BattleDirector` が窓を並べ、フォーカス窓へ入力を流す。
- 窓ごとに 一時停止/倍速・最小化・タブ/一覧。同時ライブ上限と超過時の自動解決。
- 受け入れ：別々の戦線へ複数潜行→各窓が独立に進行→各々で指揮できる。

### Stage 4：統合・結果反映・HUD・仕上げ
- 各会戦の決着を戦略へ個別反映（`BattleHandoff` 経路をマルチ化／結果キュー）。
- 窓ごと HUD・通知の宛先（どの会戦の出来事か）・カメラ枠/ミニマップの会戦対応。
- 旧フルスクリーン経路の整理（または設定で選択）。

## 後方互換・リスク管理
- 各段階で既存のフルスクリーン会戦を**壊さない**（Stage 1 はフォールバック温存、Stage 2 の static facade は既定スコープで従来動作）。
- 純ロジック（`BattleClock` 等）は Core で test-first（TestHarness）。
- Unity 依存（additive/RenderTexture/入力）は実機目視（各 PR にテスト観点）。
- 触るホットファイルが多い（`FleetRegistry` 消費者）ため、Core 契約を先に凍結→消費者移行は逐次（`docs/ops/parallel-agent-workflow.md`）。
