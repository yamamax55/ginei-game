# プレイアビリティ改善ループ 運用メモ（playtest-improve）

> 「遊べるゲームか」をコード監査で洗い出し→改善→再監査するループを、GitHub Actions で
> セッション非依存に自走させる仕組みの運用メモ。時刻はすべて **JST（UTC+9）**。

## 何が・どこに
- ワークフロー：`.github/workflows/playtest-improve.yml`（schedule＋workflow_dispatch）
- 手順（プロンプト）：`.claude/commands/playtest-improve.md`
- 改善候補の台帳：`.claude/playtest-findings.md`（`[Core]`=自動実装／`[Game]`=人手）
- 退避カタログ：`docs/core-modules-catalog.md`

## 周期・時間
- **毎時17分**に1サイクル（cron `17 * * * *`。分が同じなので JST でも毎時17分）。
- 1サイクル ≈ **12分**（concurrency で多重実行は防止）。
- 頻度変更は `playtest-improve.yml` の cron 1行だけ。

## 認証・コスト
- トークン：サブスク連携（`secrets.CLAUDE_CODE_OAUTH_TOKEN || secrets.CC`）。**従量課金なし**。
  日次枠を超えたら自動停止（無課金）。`core-wave`(毎時)/`auto-implement`(4h) と枠を共有。
- 1サイクルの目安コスト ≈ $2.8 相当（サブスク枠に対する消費）。

## 安全弁
- 検証ゲートは **TestHarness のみ**（`cd TestHarness && dotnet test`）。green でなければマージしない。
- Unity を CI 実行できないため、**Game層（MonoBehaviour/UI）の大改変は自動マージしない**＝`[Game]` findings に記録して人手レビュー。
- 自動実装は `[Core]`（純ロジック＋EditMode テスト）に限定（後方互換・既存挙動非破壊）。

## 役割分担（重要）
- **agent**：監査→`[Core]`実装→findings 更新→`dotnet test` green→**ローカルコミットまで**。
- **ワークフロー**：agent のコミットがあれば → TestHarness gate → ブランチ作成→push→`gh pr create`→`gh pr merge --delete-branch`。
  （headless では gh の権限承認ができないため、PR 作成/マージは agent でなくワークフローが行う。）

## 既知の落とし穴（このループ構築時に踏んだもの）
1. `claude-code-action` は `permissions: id-token: write` が必須（無いと OIDC 認証失敗）。
2. トークンの secret 名は `CC`（`CLAUDE_CODE_OAUTH_TOKEN` は未設定）。`|| secrets.CC` で吸収。
3. CLAUDE.md が肥大（巨大な単一行カタログ）すると CI ワーカーの文脈を潰す → `docs/core-modules-catalog.md` に分離済み。
4. `.gitignore` の `.claude/*` で `playtest-findings.md` が無視され `git add -A` が黙ってスキップ → `!.claude/playtest-findings.md` で除外解除済み。
5. agent に `gh pr create/merge` をさせると headless の権限拒否で失敗 → ワークフロー側へ移譲済み。

## 操作
- 見る：Actions「Playtest Improve 自動ループ」／`auto/playtest-*` PR／`.claude/playtest-findings.md`。
- 止める：Actions でワークフロー Disable、または cron をコメントアウト。
- 手動で1回：Actions → Run workflow。
