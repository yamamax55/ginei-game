# プロジェクト規約 / Conventions（v2：実コード反映版）

> **このファイルの使い方**
> Unity Assistant の **Attach パネルにドラッグして読ませる**前提の規約書。
> 新規会話のたびに Attach するか、冒頭で「このConventions.mdの規約に従って」と宣言する。
>
> **v2 での更新**：実際のスクリプト一式（14クラス）を読み取り、現状のアーキテクチャ・依存関係・既知の重複に合わせて全面更新。

---

## 1. プロジェクト基本情報
- **エンジン**: Unity 6.4（6000.4.x） / **2D (URP)**
- **ジャンル**: PS版『銀河英雄伝説』風の戦術艦隊戦（戦術モード＝会戦が主対象）
- **名前空間**: 全クラス `namespace Ginei`
- **入力**: 新 Input System（`UnityEngine.InputSystem`）で**統一済み**。旧 `Input.xxx` は使わない。
- **UI**: uGUI + TextMeshPro
- **日本語表示は2系統あるので注意**：
  - **ワールド上ラベル**（艦の頭上の提督名・兵力）は legacy `TextMesh`。エディタでは `Assets/Fonts/msgothic.ttc` を読む（無ければ Arial にフォールバック＝日本語が化ける可能性）。
  - **UI（CommandMenu 等）**の TMP は `Resources` 内の `"JapaneseFont_TMP"`（`TMP_FontAsset`）をフォールバックに使う。
  - → 日本語を増やすときは、この **2系統のフォント**が両方整っているか確認する。

---

## 2. 絶対規約（必ず守る）
- **2D**。座標は XY 平面、**回転は Z 軸のみ**。
- **艦の正面は `Transform.up`**。
- 調整値は **`public`**（既存に倣い `[Header]` / `[Tooltip]` を付ける）。
- 移動・回転・タイマーは **フレームレート非依存**（`Time.deltaTime`）。
- **入力は新 Input System**。
- コメントは**日本語**で簡潔に。
- **既存スクリプトを壊さない**。1依頼=1機能。指示外のファイルを大きく書き換えない。
- **実効値パターンを守る**：速度などを一時的に下げるときは、基準値（public フィールド）を**上書きせず**、ローカル変数で実効値を計算する。
  （例：`FleetMovement` は交戦中、`rotationSpeed`/`maxSpeed` 自体は変えず、ローカルの `effectiveRotationSpeed`/`effectiveMaxSpeed` を `combatMobilityRatio` 倍して使う。この方式を踏襲する）

---

## 3. コーディング規約
- `namespace Ginei`。**1ファイル1クラス**、ファイル名=クラス名。
- 命名：クラス・メソッド・public定数は `PascalCase`、フィールド・ローカルは `camelCase`。
- public 調整フィールドには `[Header]` / `[Tooltip]` を付ける。
- 依存コンポーネントは **`[RequireComponent]` で明示**する。
  - `FleetWeapon` → `WeaponArc`
  - `FleetAI` → `FleetMovement` / `FleetWeapon` / `FleetStrength`
  - `FactionColor` → `FleetStrength`
- 他コンポーネント参照は `Awake`/`Start` でキャッシュし、**使用前に null チェック**。
- マジックナンバー禁止（`public` か `const`）。
- データ（提督・艦種など）は将来 `ScriptableObject` に分離する方針（現状は各コンポーネントの public フィールド）。

---

## 4. 既存アーキテクチャ（現状＝正。新規・修正はこれに合わせる）

### データ / 列挙
- `Faction.cs`：`enum Faction { 帝国, 同盟 }`
- `Formation`：`enum { 横陣, 縦陣, 方陣, 楔形 }`（**定義は `Squadron.cs` 内**）
- **陣営の置き場所は `FleetStrength.faction`**（独立コンポーネントではない）

### コンポーネント一覧

| クラス | 付ける対象 | 責務 / 主なAPI・public | 依存 |
|---|---|---|---|
| `FleetCommander` | 入力統括（1個） | **選択の唯一の窓口** `SelectedFleets`（`List<Selectable>`）。左クリック=選択／右クリック=CommandMenu表示。`StartWaitingForMoveTarget()`→右クリックで移動先確定→全選択艦に `SetDestination` 後 `DeselectAll`。`SelectFleet` / `DeselectAll` / `GetMouseWorldPosition()` public。 | — |
| `CameraController` | Main Camera | パン（WASD/矢印/中ボタンドラッグ）・ズーム（ホイール, `minZoom`〜`maxZoom`、ズーム量で pan 速度補正）・**F=選択艦隊にフォーカス**（`SelectedFleets[0]`）・`minBounds`/`maxBounds` でクランプ。`FleetCommander` を Find でキャッシュ。 | — |
| `CommandMenu` | uGUI | 右クリックメニュー。選択状況に応じて「移動／攻撃／陣形変更（サブメニュー）／情報／選択」を**動的生成**。参照：`commander`/`menuRoot`/`buttonPrefab`/`menuRect`/`formationSubMenu`。`ChangeFormation(int)`。画面外クランプあり。 | — |
| `FleetHUDManager` | uGUI | 選択艦隊の **提督/陣営/兵力バー/陣形** を常時表示。`ChangeFormation(int)`。色：`empireColor`/`allianceColor`/`panelBgColor`。 | — |
| `FleetMovement` | 艦隊（旗艦） | `SetDestination(Vector2)`。回頭→加減速前進。**交戦中（`weapon.IsInCombat`）は `combatMobilityRatio` 倍に実効減速**。public：`maxSpeed`/`acceleration`/`deceleration`/`rotationSpeed`/`faceThreshold`/`arriveDistance`。 | （`FleetWeapon` を任意参照） |
| `Selectable` | 艦隊 | 選択状態と `selectionRing` の表示/非表示。`SetSelected(bool)`、`IsSelected`。 | — |
| `Squadron` | 艦隊（旗艦） | 旗艦周囲に配下艦を陣形配置（`SmoothDamp` 追従、向きは旗艦に同期）。`currentFormation`/`spacing`/`columns`/`smoothTime`/`memberShips`。数字1-4で陣形切替（テスト用）。**Formation enum の定義元**。 | — |
| `WeaponArc` | 艦隊 | `range`/`halfAngle`/`gizmoColor`。`bool IsInArc(Transform)`。`OnDrawGizmos` で扇形描画。 | — |
| `FleetWeapon` | 艦隊 | 射界内の敵を自動攻撃（`fireInterval`）。`SetManualTarget(FleetStrength)` で指定攻撃。**側背面ボーナス**（`flankMultiplier`、真後ろで最大）。`IsInCombat`。**`combatMobilityRatio` はここに置く**。ビーム演出（`beamColor`/`beamWidth`/`beamDuration`）。Zキーで強制発射（デバッグ）。 | `[RequireComponent] WeaponArc` |
| `FleetStrength` | 艦隊 | `admiralName`/`strength`/`maxStrength`/**`faction`**。`TakeDamage(int)`、0で `Die()`＝`Destroy`。頭上に legacy `TextMesh` で提督名・兵力表示（**生成名 `"StrengthDisplay"`**）。 | — |
| `FleetAI` | 敵艦隊 | `enum AIState { 接近, 交戦, 撤退 }`。`retreatRatio`/`searchInterval`。最近の敵へ接近→射界で交戦（その場停止）→兵力割合 < `retreatRatio` で撤退。 | `[RequireComponent] FleetMovement/FleetWeapon/FleetStrength` |
| `FactionColor` | 艦隊 | 陣営色で **子の SpriteRenderer 全部（`"SelectionRing"` は除外）/ TextMesh / `FleetWeapon.beamColor`** を一括着色。`imperialColor`/`allianceColor`。`[ContextMenu]`/`OnValidate` で再適用。 | `[RequireComponent] FleetStrength` |
| `SpaceBackground` | 背景オブジェクト | ParticleSystem で星を生成、パララックス（`parallaxFactor`）、カメラ背景色を宇宙色に。`starDensity`/`minStarSize`/`maxStarSize`/`starColorGradient`。SortingLayer `"Background"` / order -100。 | — |

---

## 5. 重要な依存・命名の約束（壊すと不具合になる）
- **選択リングの GameObject 名は `"SelectionRing"` にする**。`FactionColor` がこの名前で陣営色の対象から除外している。名前を変えると選択リングまで陣営色に塗られる。
- **`combatMobilityRatio` は `FleetWeapon` にある**。`FleetMovement` が `IsInCombat` と合わせて読む。移動側に重複定義しない。
- **頭上ラベル `"StrengthDisplay"`** は `FleetStrength` が実行時に子として生成する legacy `TextMesh`。
- **`Squadron.memberShips` を Inspector で明示的に割り当てる**こと。未設定だと `Start` で「全ての子」を自動収集するため、`StrengthDisplay` や `SelectionRing` まで配下艦扱いになる恐れがある（要注意ポイント）。
- **選択中艦隊の唯一の窓口は `FleetCommander.SelectedFleets`**。カメラ・HUD・メニュー・新規UIは必ずここを参照する。別の場所に選択管理を新設しない。

---

## 6. 既知の重複・改善余地（新規実装はここに「寄せる」。増やさない）
- **陣営カラーが3箇所に散在**：`FactionColor`（`imperialColor`/`allianceColor`）、`FleetHUDManager`（`empireColor`/`allianceColor`/`panelBgColor`）、`FleetWeapon.beamColor`。
  → 新たな陣営色の**ハードコードを増やさない**。将来は1箇所（例：`FactionPalette` という ScriptableObject か static クラス）に集約する。
- **`ChangeFormation(int)` が `CommandMenu` と `FleetHUDManager` に重複**。
  → 陣形変更ロジックは将来どちらか/共通メソッドに一本化したい。新規でも重複定義しない。
- **敵探索が `FindObjectsByType` 多用**（`FleetWeapon` は毎フレーム、`FleetAI` は `searchInterval` 間隔）。
  → 艦隊数が増えると重くなる。将来 `FleetRegistry`（艦隊が生成時に自身を登録するマネージャー）へ移行する。新規の探索処理も、できれば Registry 前提で設計する。

---

## 7. 用語対応（生成コードの命名に使う）
- 艦隊／小隊 = `Squadron` ／ 旗艦 = flagship・leader
- 兵力 = `strength` ／ 提督 = `admiralName`
- 陣形 = `Formation` ／ 射程 = `range` ／ 射角 = `arc`（`halfAngle`）
- 陣営 = `Faction`（実体は `FleetStrength.faction`）
- 交戦中 = `IsInCombat` ／ 会戦 = battle

---

## 8. NGリスト（やってほしくないこと）
- 旧 Input System API（`Input.xxx`）の使用
- 3D 前提のコード（全軸 `Quaternion` 回転、`Vector3` の z を使った移動など）
- Inspector で調整できないハードコード値
- **基準値（public）を直接上書きする一時的な能力変更**（実効値パターンを使う）
- **陣営色・陣形変更ロジックを新しい場所に重複実装**する
- **選択管理を `FleetCommander` 以外に新設**する
- 指示していないファイルの大規模リファクタ
- 既存コンポーネントと役割が重複する新規クラスの乱立

---

## 9. メンテ運用
新コンポーネント（`BattleManager` 等）や新しい依存・命名規約を足したら、**セクション4の表・セクション5の依存リストに追記**する。これにより以降の会話で Assistant が全体構成を踏まえて提案できる。
