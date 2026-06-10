# /core-backlog-refill — GitHub Issues からバックログを補充する

`.claude/core-backlog.md` の未着手テーマが少なくなったとき、リポジトリの **GitHub Issues** から
Core純ロジック向きのテーマを選定してキューへ補充する。`/core-wave` の手順7.5から自動で呼ばれる
（単独実行も可）。

## 発動条件
- バックログ未着手（`- [ ]`）が **14件未満**（= 2Wave分を切った）。

## 手順

1. **Issue取得**：オープンIssueを新しい順に最大100件読む。
   - セッション内：GitHub MCP（`list_issues` / `search_issues`）。
   - GitHub Actions 内：`gh issue list --state open --limit 100 --json number,title,body,labels`
     （`GH_TOKEN` は workflow が供給）。
2. **選定基準**（すべて満たすものだけ採用）：
   - **Core純ロジック化できる**：数値モデル・状態遷移・確率/係数の解決が主体。
     本文に「純ロジック」「test-first」「Core 層」とあるものを最優先。
   - **UI/シーン/MonoBehaviour が主体のものは除外**（配線タスクは /core-wave の対象外）。
   - **(lore)・データ入力のみ・コード新設なし と明記されたものは除外**。
   - **既存と重複しない**：`Assets/Scripts/Core/` のクラス一覧・CLAUDE.md・バックログ既存行と照合。
     既存バックログの汎用テーマと同主題のIssueが見つかったら、**新行を足さず既存行へ
     Issue番号を追記**して仕様の出所にする（例：MegaprojectRules ← PIL-1 #1090）。
3. **テーマ化**：採用Issueごとに1行を生成してキュー末尾の新セクション
   `### Issue連動（第N次補充・YYYY-MM-DD）` に追記する。書式は既存と同じ：
   `- [ ] クラス名：一行説明（#Issue番号）。既存モジュールとの分担を1句で明記`
   - クラス名はIssue本文の指定（あれば）に従う。無ければ `XxxRules` で命名。
   - **親EPICまるごとではなく、子Issue単位**で取る（1テーマ=1Wave内で完結する粒度）。
4. **補充量**：1回の補充で **最大21件（3Wave分）**。新しい順に走査し、満たしたら打ち切る。
   採用ゼロ（適合Issueなし）なら「補充不能」を報告して量産を停止する（無理に発明しない）。
5. **コミット**：`バックログ補充：Issues から N テーマを生成（#xxx〜）` でコミットし push。

## 禁止事項
- Issue本文と無関係なテーマのでっち上げ（出所のないテーマは人間が追加する）。
- クローズ済み・実装済みIssueの採用（タイトルのモジュール名が `Core/` に既存なら実装済みとみなす）。
- 親EPIC（子Issueの束）をそのまま1テーマにすること。
