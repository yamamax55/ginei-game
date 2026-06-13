---
name: worldview-epic
description: 参考作品/世界観テーマをEPIC化する定型パイプライン（設計書→GitHub EPIC＋子issue起票→roadmap §5-2 追記→コミット）。「○○をEPIC化して」「参考EPIC作って」「世界観のイシュー化」などで使う。引数＝作品名/テーマ（＋任意の着眼点）。引数「次」で docs/reference-epic-backlog.md の先頭未処理を取る。
---

# 世界観・参考作品EPIC化スキル

正本の手順書は `docs/reference-epic-pipeline.md`。**必ず最初に読み、その7ステップに従う**。
実例（型の写経元）：`docs/spice-and-wolf-reference-design.md`（#1071）／`docs/almagest-reference-design.md`（#1054）。

## 入力の解決
- 引数に作品名/テーマがあればそれを対象にする。
- 引数が「次」または空なら `docs/reference-epic-backlog.md` の**先頭の「未」行**を対象にする。バックログも空なら、ユーザーに候補を尋ねて終了。

## 実行（手順書の要点）
1. **調査**（必要なら Web 検索）→ **欠落軸分析**：CLAUDE.md の既存純ロジック一覧と突き合わせ「既存カバー＝不採用／欠落軸＝採用」を表にする。
2. **設計書** `docs/<slug>-reference-design.md`：章立て固定（§0 なぜ役立つか／§1 役に立つ視点／§2 メカニクス★優先度＋接続先＋❌不採用表／§3 子Issue表＋着手順）。
3. **起票**：EPIC（`[EPIC][分野] <作品> 参考 — <要約>`、label: type:epic + type:design + area:worldbuilding + area:*）→ 子issue（`[XXX-n] …`、label: type:feature + priority:med + area:*、本文＝親EPIC/設計書§2/狙い/接続先/完了条件）→ EPIC 本文へ子番号を edit で反映。
4. **書き戻し**：設計書§3へ番号反映、`docs/roadmap.md` §5-2 へ**新規行を挿入**（⚠既存行を絶対に置換しない）、バックログ該当行を「済 #n」へ。
5. **コミット**：`docs: <作品>参考EPIC #<n>（XXX-1〜N）— <要約>`（add は設計書/roadmap/backlog のみ）。

## ガードレール（手順書 §1 の大原則）
- 著作権：固有名・文章・キャラ不使用。メカニクス/構造パターンのみ。注意書きを設計書とEPIC両方に。
- 重複新設しない（additive・後退させない）。タイクン化回避。不採用表を必ず書く。
- プレフィックス3〜4字は roadmap/設計書 grep で一意確認。
- 純ロジックの子issueは完了条件に「EditModeテスト」必須。
- 複数作品の連続実行時：調査は並列可・**起票は直列**。
