# /core-wave — Core純ロジック量産を1Wave実行する

Unity不要環境（スマホ・クラウド）で `TestHarness`（`dotnet test`）により担保できる
**Core純ロジックモジュール**を1Wave（最大7本＋テスト7本）量産する。

## 手順（厳守）

1. **バックログ確認**：`.claude/core-backlog.md` を読み、未着手（`[ ]`）の上から最大7件を選ぶ。
   **キューが空なら何も実装せず「バックログ枯渇」を報告して終了**（勝手にテーマを発明しない）。
2. **重複チェック**：選んだテーマが `Assets/Scripts/Core/` の既存クラスおよび CLAUDE.md の
   モジュール一覧と重複しないか確認。既存系統がある場合は「別系統」である理由を doc コメントに明記し、
   重複するなら実装せずバックログから除外して理由を記す。
3. **実装規約**（既存 Wave1〜4 のスタイルに完全一致させる）：
   - 置き場所：`Assets/Scripts/Core/`（純ロジック）＋`Assets/Tests/EditMode/`（テスト）。
   - `namespace Ginei`・1ファイル1クラス・非 MonoBehaviour・日本語 doc コメント。
   - 調整値は `readonly struct XxxParams` に集約し `static Default` を提供（マジックナンバー禁止）。
   - 決定論：乱数は外から `roll` を渡す。基準値非破壊（実効値パターン）＝倍率/増分を返す。
   - 入力は `Mathf.Clamp01`/`Mathf.Max` でクランプ。null は安全に処理。
   - C# 9.0 水準（Unity 6 準拠）。TestHarness の `Stubs/UnityStubs.cs` にある `Mathf` API のみ使用。
   - Game 層型（`GameSettings`/`FleetRegistry` 等）は参照禁止。Core の既存型は read-only
     （`Population` のような純データの増減操作は可＝既存の `DemographicsRules.Tick` 流儀）。
4. **テスト併記**：各モジュールに EditMode テストを書く（境界・クランプ・全分岐・決定論・null安全）。
   既定パラメータの具体値で期待値を固定する（既存テストの流儀）。
5. **検証**：`cd TestHarness && dotnet test -v q` で**全テストパス**を確認。失敗したら直してから進む。
   （dotnet が無ければ `apt-get install -y dotnet-sdk-8.0`）
6. **.meta 生成**：新規 `.cs` 全部に一意 GUID の `.meta` を作る：
   `printf 'fileFormatVersion: 2\nguid: %s\n' "$(cat /proc/sys/kernel/random/uuid | tr -d '-')" > <file>.cs.meta`
7. **記録更新**：
   - CLAUDE.md「テスト基盤（EditMode）」セクション末尾の Wave 列挙に新 Wave を1文追記し、
     末尾の「全Nテスト」を実数に更新。
   - `.claude/core-backlog.md` の消化済みテーマを `[x]` にし、Wave番号・日付を「済み」へ移す。
8. **コミット＆プッシュ**：現在のブランチへ
   `WaveN：<テーマ要約>のCore純ロジックM本を量産（test-first・配線待ち）` でコミットし push。
   既存 PR があればそのまま反映される（PR 本文の更新は任意）。

## 禁止事項
- バックログ外のテーマの無断実装（キュー追加は人間がやる）。
- 既存モジュールの変更（量産は**追加のみ**。既存を直したくなったら報告して止まる）。
- Game 層（MonoBehaviour/UI）への配線（配線は別タスク）。
- テスト失敗のままのコミット。
