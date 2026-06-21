# Game 層メモリ（`Assets/Scripts/Game` で作業する時に自動ロード）

> ルート `CLAUDE.md` が最上位。ここは MonoBehaviour/配線/UI 作業の落とし穴と安全網だけを濃縮。
> コンポーネント詳細＝`docs/components-catalog.md`。

## ★直列化トラップ（配線・調整の前に必ず）
シーン/プレハブに置かれた MonoBehaviour の `public` 調整値は **`.unity`/`.prefab` の直列化値がスクリプト既定に勝つ**（#2548）。調整値を効かせる前に **`bash Tools/serialized-value-check.sh <ClassName> [field]`** を実行：直列化されていれば（exit 1）**そのファイル（テキストYAML）を直す**。後付けフィールドはスクリプト既定が効く。ホットスポット＝`Battle.unity`（`CameraController`/`BattleSetup`）・`FleetUnit.prefab`（`Squadron`/`FleetAI`/`FleetMovement`）。※PostToolUse hook（`Tools/hooks/`）が編集時に自動警告。

## ★Game層 compile 検証（dotnet の盲点）
`dotnet test`(TestHarness) は **Core しか見ない**。Game の .cs を変えたら **unity-mcp `Unity_ReadConsole`** でコンパイルエラーを確認（取得不可なら報告に明記し人手確認へ）。Game層の1ファイルでもエラーだと Unity は旧アセンブリを黙って保持＝沈黙クラッシュの温床。

## 壊さない依存・命名（要点）
- 固定の子オブジェクト名（`SelectionRing`/`StrengthDisplay`/`MoraleLabel`/`WeaponArcLine`/`FlagshipMarker`/`FlagshipMarkerGlow`）を変えない・二重生成しない。
- 選択の窓口＝`FleetCommander.SelectedFleets` のみ。陣形変更＝`FleetCommander.ChangeFormation`。シーン間状態＝`GameSettings.Instance` のみ。
- Esc 優先順位チェーン（CommandMenu>キャンセル>UIWindowStack.CloseTopmost>システムメニュー）を崩さない。観測/モーダルは `UIWindowStack.Register/Unregister` するだけ＝Esc を直読みしない。
- 入力は `GameInput`（論理アクション）経由。`Keyboard.current` 直読みを増やさない。新規入力は `GameAction` に足す。
- 実行時生成 `Material` は `OnDestroy` で `Destroy`。legacy TextMesh のフォントは `FontProvider.JapaneseFont`（`Arial.ttf` は Unity6 で例外）。

## 配線の作法（Core→Game を届ける）
**Core純ロジックは不変・既存窓口を呼ぶだけ**。`GalaxyView` の暦境界Tick（日/年）に相乗り（毎フレーム再計算しない）。実効値パターンで最小橋渡し。新 state は `CoreStateInspector` の glossary に1行（独立ルートなら `Register`）。探索は serena-first。新規 .cs には `.meta`（Stop hook が欠落を警告）。
