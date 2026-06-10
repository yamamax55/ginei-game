# Phase C コア実装設計（C-1 / C-2 / C-3）— 単一戦線シームレス1周

> Issue #33（EPIC）の最初のマイルストーン＝**単一戦線のシームレス1周**（C-1→C-2→C-3）の実装設計。
> 上位方針は `docs/phase-c-strategy.md`。本書は「どのクラス・データで・どう既存に接続するか」を具体化する。
> ★ 大物のため **Git ブランチ＋段階実装**。各段でコンパイル＆従来動作（Battle 単体会戦）を壊さないことを確認してから次へ。

## 0. スコープと不変条件
- 対象は **C-1（銀河グラフ＋時間制ワープ）/ C-2（シームレスズーム）/ C-3（回廊戦闘の起動・有界戦場）**。
- **まず1戦線だけ**通す。複数戦線・銀河時間の並行（C-4）以降は本書の範囲外（フックのみ用意）。
- **既存の単体会戦（Title→Battle→Result）を壊さない**。Phase C は新シーンに隔離し、既存フローと併存させる。
- 規約は CLAUDE.md 準拠（2D・XY平面・Z回転のみ・`Time.deltaTime`・新Input System・`namespace Ginei`・public調整値に`[Header]`/`[Tooltip]`）。

## 1. 全体アーキテクチャ（シームレス＝単一シーン）
- **新シーン `Galaxy`** を追加（Build Settings 登録）。戦略ビューと戦術戦闘を**同一シーン**で扱う＝シーン遷移しない（ロードはズーム演出で隠す、という方針はC-3の戦場生成を非同期にすることで実現）。
- カメラのズーム閾値で**モード**を切替（C-2）：
  - **戦略モード（引き）**：銀河グラフ全体。ノード/エッジ/艦隊アイコンを表示、戦略指示。
  - **戦術モード（寄り）**：交戦中の回廊にズームインすると、その戦場（個艦RTS）を操作。
- 戦術戦闘は**ワールド座標上の専用領域**（例：原点から大きくオフセットした区画）に生成し、戦略マップと座標的に分離する（同一シーンでも描画が混ざらない）。ズームイン時はカメラをその区画へ移動＋ズーム。

```
GalaxyScene
├─ GalaxyManager        … 銀河状態の保持・更新（ノード/エッジ/戦略艦隊/支配）
├─ GalaxyView           … ノード/エッジ/艦隊アイコンの描画（LineRenderer/Sprite）
├─ ZoomModeController   … ズーム閾値でモード切替、戦場へのカメラ寄せ
├─ CorridorBattleHost   … 交戦回廊に戦術戦場を生成/破棄し、結果を書き戻す
└─ (戦術戦闘領域)        … BattleSetup 相当で個艦艦隊を生成（既存戦術エンジン流用）
```

## 2. データモデル
> 静的な地図定義は ScriptableObject、実行時状態は MonoBehaviour/プレーンクラスで保持する（CLAUDE.md のデータ分離方針）。

### 2-1. 静的定義（ScriptableObject）
- **`StarSystemData`**（星系ノード定義）：`systemName` / `position`(銀河上の座標) / `systemType`(将来 L-1 の 工業/農業/鉱業/居住) / `initialOwner`(`FactionData`)。
- **`CorridorData`**（回廊エッジ定義）：`endpointA`/`endpointB`(StarSystemData) / `hasFortress`(bool, C-7) / `hazards`(不安定宙域の配置情報、C-3で `BlackHole` を流用) / `length`(ワープ時間係数)。
- **`GalaxyMapData`**（銀河全体）：`List<StarSystemData> systems` / `List<CorridorData> corridors` / プレイヤー勢力など初期条件。`Resources` 配下に置き `GalaxyManager` が読む（`ScenarioData`/`BattleSetup` と同じ解決パターン）。
- エディタ生成：`Ginei/Create Sample Galaxy`（`SampleScenarioCreator` と同様のワンクリック生成）で星系3〜4・回廊2〜3の最小銀河を作る。

### 2-2. 実行時状態
- **`StrategicFleet`**（戦略上の艦隊）：所属 `FactionData` / 提督群（`List<AdmiralData>`）/ 現在位置（ノード上 or エッジ上の進捗 t）/ 目的地 / 兵力（戦術の `FleetStrength.strength` 相当の集約値）。**戦術 `Squadron`/`FleetStrength` とは別物**で、戦闘時に相互変換する（§5）。
- **`GalaxyManager`**（static `Instance`、`DontDestroyOnLoad` は不要＝Galaxyシーン内）：全 `StrategicFleet`・星系支配・エッジ支配を保持。`Update` で時間経過＝ワープ移動を進める。
- 多勢力の敵対判定は既存 **`FactionRelations.IsHostile`** をそのまま流用（コード重複を作らない）。

## 3. C-1：銀河グラフと時間制ワープ移動
**目的**：星系=ノード/回廊=エッジのグラフ上を、艦隊が**移動時間あり**で進む。回廊以外は航行不能。

- **グラフ表現**：`GalaxyMapData` を読み、ノード=星系座標、エッジ=隣接回廊。隣接リストを `GalaxyManager` が構築。
- **移動（時間制ワープ）**：`StrategicFleet.MoveTo(targetSystem)` は**隣接ノードのみ**許可（回廊が無い＝移動不可）。エッジ上を `progress += speed*Time.deltaTime/corridor.length` で進める。到着でノードへスナップ。経路が複数エッジなら経由ノードのキューで順送り。
- **指示UI（戦略ズーム時）**：`FleetCommander` の思想を踏襲しつつ**戦略用の新コントローラ**を別に置く（戦術の `FleetCommander.SelectedFleets` とは混ぜない）。左クリックで `StrategicFleet` 選択、目的ノードクリックで `MoveTo`。姿勢（陣形/進入口の向き）は C-3 の戦場生成で使用。
- **描画**：`GalaxyView` がエッジを `LineRenderer`、ノードを Sprite、艦隊を勢力色（`FactionData.color`）アイコンで描く。
- **完了条件**：艦隊がエッジ上を時間をかけて移動し、回廊の無い方向へは動けない。

> 接続：既存 `CameraController`（パン/ズーム）はそのまま戦略ビューのカメラに流用可。`FactionData.color` を勢力色の唯一の出所として使う。

## 4. C-2：シームレスなズーム制御（戦略↔戦術）
**目的**：同一シーンで、カメラのズーム閾値により 引き=戦略指示／交戦中の回廊へ寄る=戦術指示 を切替。

- **`ZoomModeController`**：`CameraController.orthographicSize` を監視し、`strategicThreshold`（これより引き＝戦略）と `tacticalThreshold`（これより寄り＝戦術）でヒステリシス付き切替。
- **戦術へ入れる条件**：カーソル/カメラ中心が**交戦中の回廊**にある時のみ。平時の回廊・星系へのズームは**閲覧のみ**（戦術操作は不可）。
- **展開/畳み込み**：
  - 寄る→ `CorridorBattleHost` が当該回廊の戦場を（未生成なら）生成し、戦略艦隊を**個艦戦隊に展開**（§5の変換）。カメラを戦場区画へ移動。
  - 引く→戦場はそのまま進行させつつ（C-3では一旦ポーズ可、C-4で常時進行）、カメラを銀河へ戻す。戦況サマリ（残存兵力など）を戦略艦隊に反映。
- **「ロードをズーム演出で隠す」**：戦場生成（プレハブ多数 Instantiate）は**ズームイン演出の数フレームに分散**（コルーチン）して体感ロードを隠す。`SceneLoader` のような全画面ロードは使わない（同一シーンのため）。
- **完了条件**：引き=戦略指示／交戦回廊へ寄り=戦術指示が、シーン遷移なしで切り替わる。閲覧ズームでは戦術操作不可。

> 入力競合：戦術モード中は既存 `FleetCommander`/`CommandMenu`/`PauseManager` をそのまま使う。戦略モード中はそれらを無効化し戦略コントローラのみ有効、という**モードゲート**を `ZoomModeController` が司る（Esc 優先順位は CLAUDE.md の規約を踏襲）。

## 5. C-3：回廊戦闘の起動・有界戦場・地形（迂回不可）
**目的**：敵対艦隊が同じ回廊を奪い合うと**有界戦場**を生成し、既存戦術エンジンを**“生の艦隊状態”から**起動、結果を戦略へ書き戻す。

- **戦闘の発生**：`GalaxyManager` が毎チェックで、同一回廊（エッジ）上に**敵対する `StrategicFleet`**（`FactionRelations.IsHostile`）が居合わせたら**交戦状態**にし、`CorridorBattleHost.BeginBattle(corridor, fleets)` を呼ぶ。
- **有界戦場**：
  - **壁＝航行不能**：戦場区画の外周に**侵入不可コライダー**（`EdgeCollider2D`/`BoxCollider2D`）を置く。艦の移動（`FleetMovement` は transform 直接移動）に対しては、`BlackHole` と同じ「LateUpdate で外力/クランプ」方式で**壁内にクランプ**する補助コンポーネント `BattlefieldBounds` を新設（`FleetMovement` 本体は触らない）。
  - **両端＝進入口**：各星系側の端をスポーン口にし、勢力ごとに自陣側からスポーン（C-5 援軍のワープインもここ）。
  - **迂回不可**：外周は壁＋グラフに裏エッジが無いことで担保（戦場内で回り込んでも戦線の外には出られない）。
  - **不安定宙域**：`CorridorData.hazards` に従い既存 **`BlackHole`（A-5）** を戦場内に配置（`AutoSpawnEnabled=false` で自動配置を切り、明示配置）。
- **戦術エンジンの起動（“生の艦隊状態”から）**：
  - 現状 `BattleSetup` は `ScenarioData`（アセット）から生成する。Phase C 用に **`BattleSetup` を拡張 or 併設**して、**ランタイムの艦隊リスト**（`StrategicFleet`→提督/勢力/兵力/陣形/スポーン位置）から生成できる入口を設ける（`ScenarioData` 非依存）。`FleetRegistry.Clear()` は**その戦場の開始時のみ**呼ぶ（単一戦線前提。複数戦線=C-4 では戦場ごとのスコープ分離が必要＝本書範囲外、フックだけ用意）。
  - 兵力変換：`StrategicFleet` の集約兵力 → 戦術 `FleetStrength.strength`／配下艦数（`Squadron.escortCount` 等）へ写像する `FleetStateMapper`（純ロジック）を新設。
- **結果の書き戻し**：戦術側の決着（`BattleManager` 相当）or 撤退・全滅で、生存 `FleetStrength` の兵力を `StrategicFleet` に戻す。勝者勢力が回廊/星系の支配を取る。`BattleManager` の勝敗評価（`HasHostilePair`/`DetermineWinner`）は流用可。
- **完了条件**：敵対艦隊が同回廊で衝突→有界戦場が生成→個艦戦闘→結果が戦略（支配・残存兵力）へ反映。外周の回り込み・迂回は不可。

## 6. 既存コードとの接続まとめ
| 既存 | Phase C での使い方 |
|---|---|
| `BattleSetup` | ランタイム艦隊状態からの生成入口を拡張（`ScenarioData` 非依存パスを追加）。`FleetRegistry.Clear()` は戦場開始時のみ。 |
| `FleetRegistry` | 単一戦線では現状のまま（全艦＋`FactionRelations`）。複数戦線=C-4 で戦場スコープ分離（要改修・本書範囲外）。 |
| `FactionData` / `FactionRelations` | 戦略・戦術とも敵対判定の唯一の窓口。色も `FactionData.color`。 |
| `BlackHole`(A-5) | 戦場内ハザードとして明示配置（自動配置 off）。 |
| `CameraController` | 戦略/戦術カメラの土台。`ZoomModeController` が orthographicSize を監視。 |
| `FleetCommander`/`CommandMenu`/`PauseManager` | 戦術モード中のみ有効（モードゲート）。 |
| `GameSettings` / `SaveData` | 戦略途中状態の保存は **#19** で対応（本書では保存フックの所在だけ示す）。 |
| `BattleManager` | 戦場の勝敗評価ロジック（`HasHostilePair`/`DetermineWinner`）を回廊戦闘の決着に流用。 |

## 7. 段階ビルド手順（各段でコンパイル＆既存単体会戦の無事を確認）
1. **C-1a**：`StarSystemData`/`CorridorData`/`GalaxyMapData` ＋ `Ginei/Create Sample Galaxy`。`Galaxy` シーンに `GalaxyManager`/`GalaxyView` を置き、グラフを描画（移動なし）。
2. **C-1b**：`StrategicFleet` ＋ 時間制ワープ移動＋戦略選択/目的地指示。
3. **C-2**：`ZoomModeController` でモード切替（戦術はまだ空＝閲覧のみ）。
4. **C-3a**：`CorridorBattleHost` ＋ `BattlefieldBounds`（壁/進入口）＋ ランタイム生成入口（`BattleSetup` 拡張）。固定2艦隊で戦闘起動。
5. **C-3b**：`FleetStateMapper`（兵力変換）＋ 結果書き戻し（支配・残存兵力）＋ ハザード配置。
6. 通し確認：戦略で艦隊を動かし→交戦回廊へズームイン→戦闘→ズームアウトで戦況反映、の1周。

## 8. リスク・未決事項
- **同一シーンでの戦略/戦術の座標分離**：戦場区画のオフセット運用が破綻しないか（カメラ境界・背景パララックス）。→ まず大きめオフセットで分離、必要なら戦場を別ルートに隔離。
- **`FleetRegistry` の単一性**：複数戦線（C-4）で必ず詰まる。C-3 までは単一前提で進め、C-4 着手時に**戦場スコープ化**（インスタンス化 or 戦場IDでフィルタ）へ改修する前提を明記。
- **`BattleSetup` の二系統化**（アセット駆動／ランタイム駆動）が散らからないよう、生成本体は共通化し入口だけ分ける。
- **モードゲートの入力競合**（戦略コントローラ↔`FleetCommander`/`PauseManager`/`CommandMenu`）。Esc 優先順位の規約を崩さない。
- **【要・作者判断】**：ズーム閾値の具体値、戦場サイズ、兵力↔個艦の変換比、銀河の初期配置。
- セーブ（#19）・援軍（C-5）・補給（C-6/兵站L）・自動解決（C-8）・要塞（C-7）は**本書のフック**（進入口・回廊データ・結果書き戻し）の上に乗る前提。

---

### 変更履歴
- v0.1：C-1〜C-3（単一戦線シームレス1周）の実装設計。既存コード接続・段階手順・リスクを提示。**【要・作者判断】箇所は確定待ち。**
