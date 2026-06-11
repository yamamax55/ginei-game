# 世界観・参考作品EPIC化 標準手順（reference-epic pipeline）

> 参考作品（ゲーム/小説/歴史/思想家）を「設計書＋GitHub EPIC＋子Issue＋roadmap追記」へ落とす定型。
> 実例：Almagest -Overture- #1054（ALM-1〜16）／狼と香辛料 #1071（SAW-1〜9）。
> 連続実行は `/worldview-epic <作品名>`（スキル）＋ [`reference-epic-backlog.md`](./reference-epic-backlog.md)（候補キュー）で回す。

## 0. 入力と成果物

**入力**：参考作品/テーマ（＋任意の着眼点。例「商人視点の経済」）。

**成果物（1作品=1サイクル=1コミット）**：
1. 設計書 `docs/<slug>-reference-design.md`
2. GitHub **EPIC issue 1件**＋**子issue N件**（プレフィックス `XXX-n`）
3. `docs/roadmap.md` §5-2 へ **1行追記**
4. [`reference-epic-backlog.md`](./reference-epic-backlog.md) の該当行を「済 #n」へ
5. コミット `docs: <作品>参考EPIC #<n>（XXX-1〜N）— <要約>`

## 1. 大原則（全工程で守る）

- **著作権**：固有名・文章・キャラクター・固有設定は流用しない。**メカニクス／世界観の構造パターンのみ**参考。設計書とEPIC本文の両方に注意書きを必ず入れる。
- **重複新設しない（additive）**：`CLAUDE.md`「テスト基盤」節の純ロジック一覧と突き合わせ、既存でカバー済みの要素は**不採用（接続のみ）**。既存システムを後退させる要素も不採用。
- **タイクン化回避**：マイクロ操作を増やさない（高位の決断→エンジン駆動→創発帰結）。
- **取捨選択を明示**：採用（★優先度つき）と**❌不採用（理由つき表）**を必ず書く。「全部入り」は失敗。
- **プレフィックス一意**：3〜4文字の略号（ALM/SAW…）。`docs/roadmap.md` と `docs/*-reference-design.md` を grep して衝突がないことを確認。

## 2. 手順（7ステップ）

### Step 1 — 調査（必要なら Web 検索）
作品のメカニクス・世界観構造を整理。**「本システムに効く形」で1行ずつ**要約する（〜6行）。

### Step 2 — 欠落軸分析
既存の純ロジック層（`CLAUDE.md`）と突き合わせ、2つの表を作る：
「既存（カバー範囲）」表 と 「作品が固有に持つ視点 × 当プロジェクトでの欠落」表。
→ **欠落軸だけが採用候補**。

### Step 3 — 設計書ドラフト
`docs/<slug>-reference-design.md`。章立ては固定（SAW/ALM に倣う）：
- **§0 なぜ役立つか**（Step 2 の2表＋結論）
- **§1 役に立つ視点**（1行×N・既存issueへの共鳴を明記）
- **§2 取り入れるべきメカニクス**（★★★/★★/★ 優先度・各項に**接続先の既存モジュール**を明記・末尾に**❌不採用表**）
- **§3 子Issue表**（番号は空欄でドラフト）＋**推奨着手順**

### Step 4 — EPIC 起票
```
gh issue create --title "[EPIC][<分野>] <作品> 参考 — <要約>" `
  --label "type:epic" --label "type:design" --label "area:worldbuilding" --label "area:<分野>" `
  --body-file <一時ファイル>
```
本文の型：`## 狙い`（設計書リンク）／`## ★著作権注意`／`## ★大原則：重複新設しない`（不採用列挙）／`## このEPICが束ねるもの（子Issue）`（番号は後で edit）／`## 完了条件（EPIC）`。

### Step 5 — 子issue 起票（直列で N 件）
```
gh issue create --title "[XXX-n] <タイトル>" `
  --label "type:feature" --label "priority:med" --label "area:<分野>" --body-file <一時ファイル>
```
本文の型：先頭に `親EPIC #<n> ／ 設計：docs/<slug>-reference-design.md §2` ＋ `## 狙い` `## 接続先` `## 完了条件`。
純ロジック新設の子は完了条件に **「EditModeテスト」** を必ず含める（test-first・TestHarness 検証）。
**state 型を新設し盤面（`CampaignState`/`StrategySession` 等）へ接続する子**は、完了条件に **「観測層への追従」** も含める＝`CLAUDE.md`「観測層」節の規約どおり、`CoreStateInspector` の用語集に新フィールドを1行・独立した新ルートなら `Register` を1行（既存ルート配下なら再帰表示で不要）。純ロジックのまま盤面未接続なら不要。

### Step 6 — 番号の書き戻し
1. `gh issue edit <epic番号> --body-file` で子issue番号を EPIC 本文へ反映。
2. 設計書 §3 の表へ issue 番号を反映。
3. `docs/roadmap.md` §5-2 へ1行追記：`- **<作品> 参考：#<EPIC>（EPIC）＋XXX-1〜N（#a〜#b）** ＝<要約・三本柱・不採用・着手順>`。
> ⚠ **roadmap 追記は既存行を置換せず、必ず新しい行として挿入**する（#1071 起票時に直前の ALM 行を上書き融合させた事故の再発防止。Edit の old_string は前行の**末尾＋改行**を含め、new_string で改行を保って足す）。

### Step 7 — コミット＋バックログ更新
`reference-epic-backlog.md` の該当行を「済 #<n>」に。git add は**設計書・roadmap・backlog のみ**（無関係ファイルを巻き込まない）。
コミットメッセージ：`docs: <作品>参考EPIC #<n>（XXX-1〜N）— <要約>`。

## 3. 連続実行（バックログ駆動）

- 候補作品は [`reference-epic-backlog.md`](./reference-epic-backlog.md) に貯める（思いついたら1行追記）。
- `/worldview-epic 次` でバックログ先頭の未処理を取り、1作品=1サイクルで回す。
- **調査（Step 1-2）は作品間で独立＝並列ファンアウト可**（[`parallel-core-fanout.md`](./parallel-core-fanout.md) CCX-1 の方式で複数作品を同時調査）。ただし**起票（Step 4-6）は直列**＝issue 番号・roadmap 行・プレフィックスが競合するため。

### クラウド自動実行（GitHub Actions・PC非依存）
- **並列版 `.github/workflows/worldview-epic-parallel.yml`（master・現行）** が **30分ごと（UTC :11/:41）** に **fan-out/fan-in で N=5 並列**処理する：①`select`（直列1・backlogから5件選定＋プレフィックス割当）②`epicize`（並列5・各作品の設計書＋issue起票・**共有ファイルは触らずartifactで渡す**）③`integrate`（直列1・全fragmentを集約しroadmap/backlogを1push更新）。スループット約4倍（1時間で約10冊）。
- **競合回避の核**＝重い処理（設計・起票）は並列だが、共有ファイル（roadmap §5-2・backlog）は **integrate だけが書く**。roadmap は `<!-- reference-epic-auto-anchor -->` 直前に機械挿入＝行競合なし。issue採番（GitHub）と設計書（独立ファイル）は元々衝突しない。＝`parallel-core-fanout.md`（CCX-1）の「生成は並列・統合は直列」と同型。
- 旧 `worldview-epic.yml`（直列1件）は schedule停止・workflow_dispatch のみ（フォールバック）。同じ concurrency group `worldview-epic` で並列版と排他。
- **状態は専用ブランチ `auto/worldview-epics`** に commit/push して持ち回り（バックログ「済」更新が次回実行に見える）。たまった成果は PR で master へ取り込む。
- **冪等ガード**：対象選定＝「バックログ先頭から、`gh issue list --search "in:title <作品名> 参考"` で既存EPICが**無い**最初の未行」。既存EPICがあるのに未のままの行は、子issueの不足を補完してから済へ直す（途中失敗からの自動復旧）。
- 認証：Actions secret `CLAUDE_CODE_OAUTH_TOKEN`（`claude setup-token` で発行）。issue起票/pushは `GITHUB_TOKEN`。
- **自動補充** `.github/workflows/worldview-backlog-refill.yml`：6時間ごと（UTC :23）に残量チェックし、**「未」が16冊未満なら**本システムの欠落軸に効く名著を**最大20冊**バックログ末尾へ追記（閾値16＝消費2冊/時×6時間＋マージン）。重複除外＝既存行/既存設計書/既存EPIC/モジュール化済み思想家の主著。**数合わせ禁止＝確信のある作品だけ・0冊でも可**。同じ concurrency グループで EPIC化と直列。残量が閾値以上なら Claude を起動せず grep 判定だけでスキップ（無課金）。

## 4. 実例（この型の出荷実績）

| 作品 | EPIC | 子Issue | 設計書 |
|---|---|---|---|
| Almagest -Overture- | #1054 | ALM-1〜16（#1055〜#1070） | [`almagest-reference-design.md`](./almagest-reference-design.md) |
| 狼と香辛料 | #1071 | SAW-1〜9（#1072〜#1080） | [`spice-and-wolf-reference-design.md`](./spice-and-wolf-reference-design.md) |
