# /playtest-improve — プレイアビリティ改善ループを1サイクル実行

「遊べるゲームか」をコード監査で洗い出し→改善→再監査するループの**1サイクル**を回す。
セッションを跨いで `playtest-improve.yml`（定時 GitHub Actions）から無人実行される前提。

## 絶対方針（安全弁）
- **検証ゲートは TestHarness のみ**：`cd TestHarness && dotnet test` が **green** な変更だけをマージする。
- この環境では **Unity を実行できない**。よって **Game層（MonoBehaviour/シーン/UI）の挙動を変える大きな改変は自動マージしない**。
  - Game層の改善は **実装せず `.claude/playtest-findings.md` の `[Game]` に記録**して人手レビューへ回す。
  - 実装してよいのは **`[Core]` 項目＝純ロジック（`Assets/Scripts/Core/`）＋ EditMode テスト**、および既存配線点での**極小・低リスクな読み替え**のみ。
- 既存規約（`CLAUDE.md`）に厳密準拠：`namespace Ginei`／1ファイル1クラス／`readonly struct XxxParams`（トップレベル＋`static Default`）／決定論（乱数は `roll` 引数）／実効値パターン（基準値非破壊）／入力クランプ／LINQ 不使用／Mathf のみ。**Core から Game 型を参照しない**。
- 既存挙動を壊さない。後方互換（未配線の純ロジック追加は既定で無害）。

## 手順
1. **再監査（洗い出し）**：`.claude/playtest-findings.md` を読む。`[Core]` 未着手が **3件未満**なら、コードを監査して `[Core]`/`[Game]` 項目を補充する（未配線の高価値 Core、バランス係数、ロジック欠落、責務の分散などを具体的に）。
2. **実装（1〜2件）**：`[Core]` 未着手の上位を test-first で実装する。
   - 純ロジック → `Assets/Scripts/Core/Xxx.cs`、テスト → `Assets/Tests/EditMode/XxxTests.cs`、`.meta`（`fileFormatVersion: 2` ＋ 32hex guid）を両方に付ける。
   - 既存 Core への配線点（`BattleAiRules`/`ForceQualityRules`/`CombatModifiers` 等）がある場合のみ、実効値パターンで**極小**に橋渡しする。Game 層 .cs の編集は避ける（必要なら findings に回す）。
3. **検証**：`cd TestHarness && dotnet test -v q` で **全 green**。落ちたら自分で直す。green にできない変更は捨てる（マージしない）。
4. **記録（必須・省略禁止）**：`.claude/playtest-findings.md` を**必ず更新する**：実装した項目を `[x]（cycleN・YYYY-MM-DD）` に変え、「完了ログ」へ1行追記し、`[Core]` 残候補が3件未満になったなら監査で補充した項目も追記する。**この findings.md の変更は、実装したモジュール／テストと同じコミットに必ず含める**。
5. **ローカルコミットのみ（push/PR/merge はしない）**：
   - チェックアウト中の `master`（ワークフローが checkout 済み）に、変更（モジュール＋テスト＋`.meta`＋**更新した findings.md**）を `git add -A && git commit` で**ローカルコミット**する（メッセージ例：`playtest改善：…（cycleN）`）。コミット末尾に `https://claude.ai/code/...` 形式の行は付けてよいが**モデル識別子は書かない**。
   - **`git push` も `gh pr create` も `gh pr merge` も実行しない**。ブランチ作成・push・PR作成・自動マージは**この後のワークフローステップが機械的に行う**（headless では gh の権限承認ができず PR を作れないため、agent は git/gh のリモート操作をしない）。
   - 実装が複数あっても**1サイクル＝1コミット**にまとめてよい。
6. **空なら無変更で終了**：`[Core]` 候補が尽き、監査でも新規が出ないなら**コミットせず**何も変更せず終了（後段ステップは差分が無ければ PR を作らない）。

## やらないこと
- **`git push` / `gh pr create` / `gh pr merge` を agent が実行すること**（push・PR・マージは後段のワークフローステップが行う。agent はローカルコミットまで）。
- **findings.md の更新を省略すること**（実装したら同じコミットで必ず反映）。
- Game層の大改変・UI改変の自動マージ（findings 記録に留める）。
- TestHarness が落ちる変更のマージ。
- 1サイクルで3件を超える大量実装（小さく刻む）。
- モデル識別子をコミット/PR/コードへ書くこと。
