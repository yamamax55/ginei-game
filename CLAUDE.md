# CLAUDE.md — 銀英伝風 戦術艦隊戦（プロジェクトメモリ）

> Claude Code が毎セッション開始時に自動で読み込む。プロジェクトルート（`Assets/` や `ProjectSettings/` のある階層）に置く。
> これは「プロジェクトの最上位ルール」。プロンプトと衝突したら、このファイルが優先。

## プロジェクト概要
- Unity 6.4（6000.4.x） / 2D (URP)。
- ジャンル：PS版『銀河英雄伝説』風の戦術艦隊戦（リアルタイム会戦）。将来は戦略レイヤー・複数勢力（諸侯型）へ拡張予定。
- 名前空間：全クラス `namespace Ginei`。
- 入力：新 Input System（`UnityEngine.InputSystem`）で統一。旧 `Input.xxx` は使わない。
- UI：uGUI + TextMeshPro。

## 絶対規約（必ず守る）
- 2D。座標は XY 平面、回転は Z 軸のみ。
- 艦の正面は `Transform.up`。
- 調整値は `public` にし、`[Header]` / `[Tooltip]` を付ける。
- 移動・回転・タイマーはフレームレート非依存（`Time.deltaTime`）。timeScale に追従させる。
- コメントは日本語で簡潔に。
- 既存スクリプトを壊さない。指示外のファイルを大きく書き換えない。大きな変更は段階的に。
- **実効値パターン**：能力を一時的に下げるときは基準値（publicフィールド）を上書きせず、ローカル変数で実効値を計算する（例：`FleetMovement` の交戦時減速）。

## コーディング規約
- 1ファイル1クラス、ファイル名=クラス名。命名：型/メソッド/public定数=PascalCase、フィールド=camelCase。
- 依存コンポーネントは `[RequireComponent]` で明示（`FleetWeapon`→`WeaponArc`、`FleetAI`→`FleetMovement`/`FleetWeapon`/`FleetStrength`、`FactionColor`→`FleetStrength`）。
- 他コンポーネント参照は Awake/Start でキャッシュし、使用前に null チェック。
- マジックナンバー禁止（`public` か `const`）。
- データ（提督・艦種・勢力など）は ScriptableObject に分離する方針。

## データ / 列挙
- `Faction.cs`：`enum Faction { 帝国, 同盟 }`（※将来 `FactionData`(ScriptableObject) へ移行し複数勢力化予定）。
- `Formation`：`enum { 横陣, 縦陣, 方陣, 楔形 }`（定義は `Squadron.cs` 内）。
- 陣営の置き場所は `FleetStrength.faction`。

## 既存コンポーネント（現状＝正。これに合わせる）
| クラス | 対象 | 責務 / 主なAPI |
|---|---|---|
| `FleetCommander` | 入力統括(1) | 選択の唯一の窓口 `SelectedFleets`(`List<Selectable>`)。左ク=選択／右ク=CommandMenu。`SetDestination` 配布後 `DeselectAll`。`GetMouseWorldPosition()`。 |
| `CameraController` | Main Camera | パン(WASD/矢印/中ドラッグ)・ズーム(ホイール,min/maxZoom)・F=選択艦隊フォーカス・min/maxBounds クランプ。 |
| `CommandMenu` | uGUI | 右クリックメニュー。選択状況で 移動/攻撃/陣形変更/情報/選択 を動的生成。`ChangeFormation(int)`。 |
| `FleetHUDManager` | uGUI | 選択艦隊の 提督/陣営/兵力バー/陣形 を表示。`ChangeFormation(int)`。 |
| `FleetMovement` | 艦隊(旗艦) | `SetDestination(Vector2)`。回頭→加減速前進。交戦中(`weapon.IsInCombat`)は `combatMobilityRatio` 倍に実効減速。public: maxSpeed/acceleration/deceleration/rotationSpeed/faceThreshold/arriveDistance。 |
| `Selectable` | 艦隊 | 選択状態と `selectionRing` 表示。`SetSelected(bool)`/`IsSelected`。 |
| `Squadron` | 艦隊(旗艦) | 配下艦を陣形配置(SmoothDamp追従、向きは旗艦同期)。currentFormation/spacing/columns/smoothTime/memberShips。Formation enum 定義元。 |
| `WeaponArc` | 艦隊 | range/halfAngle/gizmoColor。`IsInArc(Transform)`。OnDrawGizmos で扇形描画。 |
| `FleetWeapon` | 艦隊 | 射界内の敵を自動攻撃(fireInterval)。`SetManualTarget(FleetStrength)`。側背面ボーナス(flankMultiplier)。`IsInCombat`。`combatMobilityRatio` はここに置く。ビーム演出。`[RequireComponent] WeaponArc`。 |
| `FleetStrength` | 艦隊 | admiralName/strength/maxStrength/faction。`TakeDamage(int)`、0で `Die()`=Destroy。頭上ラベル(legacy TextMesh、生成名 `"StrengthDisplay"`)。 |
| `FleetAI` | 敵艦隊 | `enum AIState{接近,交戦,撤退}`。retreatRatio/searchInterval。`[RequireComponent] FleetMovement/FleetWeapon/FleetStrength`。 |
| `FactionColor` | 艦隊 | 陣営色で子SpriteRenderer全部(`"SelectionRing"`除外)/TextMesh/beamColor を着色。imperialColor/allianceColor。 |
| `SpaceBackground` | 背景 | ParticleSystem で星生成、パララックス。SortingLayer `"Background"`/order -100。 |

## 壊すと不具合になる依存・命名
- 選択リングの GameObject 名は `"SelectionRing"`（`FactionColor` がこの名前で着色対象から除外）。
- `combatMobilityRatio` は `FleetWeapon` にある。`FleetMovement` が `IsInCombat` と共に読む。移動側に重複定義しない。
- 頭上ラベル `"StrengthDisplay"` は `FleetStrength` が実行時に生成する legacy `TextMesh`。
- `Squadron.memberShips` は Inspector で明示割り当て推奨（未設定だと全ての子を集め、ラベルや選択リングまで配下艦扱いになる恐れ）。
- 選択中艦隊の唯一の窓口は `FleetCommander.SelectedFleets`。別の場所に選択管理を新設しない。

## 既知の重複（新規はここに寄せる・増やさない）
- 陣営カラーが3箇所に散在（`FactionColor` / `FleetHUDManager` / `FleetWeapon.beamColor`）。新たな陣営色のハードコードを増やさない。将来1箇所へ集約。
- `ChangeFormation(int)` が `CommandMenu` と `FleetHUDManager` に重複。一本化したい。
- 敵探索が `FindObjectsByType` 多用（`FleetWeapon` 毎フレーム、`FleetAI` 間隔つき）。将来 `FleetRegistry`（生成時に自己登録）へ移行する。

## 日本語表示の注意
- 頭上ラベルは legacy `TextMesh`：エディタでは `Assets/Fonts/msgothic.ttc` を読む（無いと化ける）。
- UI の TMP は `Resources` の `"JapaneseFont_TMP"`(TMP_FontAsset) をフォールバックに使う。

## NG リスト
- 旧 Input API、3D前提コード（全軸Quaternion回転・zを使った移動）、Inspector非公開のハードコード。
- 基準値を直接上書きする一時的な能力変更（実効値パターンを使う）。
- 陣営色・陣形変更ロジックの重複実装、`FleetCommander` 以外への選択管理新設。

## 運用
- 新コンポーネントを作ったら、この「既存コンポーネント」表に1行追記する。
- 変更は Git でコミットしながら進める（権限「Don't Ask」運用のため、レビュー・巻き戻し前提）。
- 詳細な機能ロードマップは別ファイルの「機能追加ロードマップ」を参照（必要時に手動で読み込ませる）。
