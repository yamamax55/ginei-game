# Core 層メモリ（`Assets/Scripts/Core` で作業する時に自動ロード）

> ルート `CLAUDE.md` が最上位。ここは Core 純ロジック量産の作法だけを濃縮（重複は持たない）。
> 全モジュール網羅カタログ＝`docs/catalog/core-modules-catalog.md`。索引＝ルート CLAUDE.md。

## この層の絶対則
- **非 MonoBehaviour・純ロジック**。`namespace Ginei`（配置に依らずフラット）。1ファイル1クラス＝ファイル名。
- **Game層型を参照しない**（`GameSettings`/`FleetRegistry`/`AudioManager`/`SceneLoader`/MonoBehaviour 等）。依存方向は **Core←Data←Game の一方向・循環厳禁**。
- **決定論**：乱数は外から `roll`(0..1) 引数で受ける（`Date.now`/`Random` を内部で使わない）。
- **実効値パターン**：基準値を上書きせずローカルで実効値（倍率/増分）を計算して返す。
- 調整値は **`readonly struct XxxParams`（トップレベル）＋`static Default`**。マジックナンバー禁止。
- 入力は `Mathf.Clamp01`/`Mathf.Max` でクランプ・null 安全。**LINQ 不使用**・**C# 9.0 水準**・**TestHarness の Stubs にある `Mathf` API のみ**。
- ドメインサブフォルダ（`Combat`/`Economy`/`Government`/`Society`/`Personnel`/`Strategy`/`Diplomacy`/`Population`/`Foundation`/`Fleet`/`Intel`/`Time`）の主題に合う場所へ置く（配置ずれはコンパイル無害）。

## 探索は serena-first（grep+全読みをやめる）
1,375本規模。`get_symbols_overview`→`find_symbol(include_body)` で**当該シンボルだけ**読む。重複確認・既存窓口探しは `find_symbol`、配線状況は `find_referencing_symbols`（ルート規約：敵対判定/陣営色/ZOC/係数公式 等は既存の単一窓口へ委譲＝二重実装しない）。編集は `replace_symbol_body`。

## test-first（必須）
新規純ロジックは EditMode テスト併記（`Assets/Tests/EditMode/XxxTests.cs`）。境界・クランプ・全分岐・決定論・null安全を網羅し、**既定パラメータの具体値で期待値を固定**。検証＝`cd TestHarness && dotnet test -v q` が green。`docs/catalog/core-modules-catalog.md` に1行追記。

## スケーラビリティ規律（終盤ラグ回避）
個体粒度へ降りない（集約=FactionState/Province）。毎フレームでなく暦境界Tick。差分・収束・キャッシュ。N²の相手数を増やさない。無制限リストは上限・打ち切りは log で明示。
