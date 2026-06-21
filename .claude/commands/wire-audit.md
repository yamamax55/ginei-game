# /wire-audit — Core→Game 配線監査（届くべき未配線を棚卸し）

`Assets/Scripts/Core` の純ロジック（`public static class *Rules` 等）を **Game層からの参照有無で分類**し、
「プレイヤーに届くべきなのに届いていない」モジュールを Tier 付きで洗い出す。
`/core-wave`（**作る**）の対＝**届ける**側の常設コマンド。配線そのものはしない（別タスク・人手の裁可で昇格）。

> 出自を分解できる **serena**（意味的参照グラフ）が前提。素の grep は「テキスト一致のノイズ」と
> 「推移的配線（X→Y→GalaxyView）」を取りこぼすため、判定の核には必ず serena を使う。
> 既存の手作業監査＝[docs/core-orphan-audit.md](../../docs/core-orphan-audit.md) をこのコマンドで更新・自動化する。

## 出力（成果物）
1. `docs/core-orphan-audit.md` を最新の分類で**更新**（サマリ数値＋Tier 表）。
2. `.claude/wire-backlog.md` に**配線候補キュー**を Tier 付きで書き出す（`core-backlog.md` の配線版。無ければ新規作成）。
3. レポートで **Tier A の上位5件**（届けると「選択が結果を変える」体験になるもの）を理由つきで提示。

## 分類カテゴリ（参照の出自で判定）
各 `*Rules` の参照元を serena `find_referencing_symbols` で取り、出自で仕分ける：
- **Game参照あり** … `Assets/Scripts/Game/` から（推移的オーケストレータ経由含む）参照される＝**届いている／配線済**。
- **Core島**（最重要シグナル） … 他 Core から実利用されるが **Game参照ゼロ**＝サブシステム内では生きているが盤面に出ない。**配線の最有力候補**。
  - 例（本監査の実証）：`EspionageRules` は他Core約13本が実利用するが Game参照ゼロ＝諜報が一切盤面に出ない。
- **真の孤児** … 参照がテスト＋docコメント（`<see cref>`）のみ＝計算すらされていない。
  - 例：`CommandChainRules` はテストと `Office.cs` のdocコメントのみ。

> ★`<see cref>` のdocコメント参照・テスト参照は**配線判定ではノイズ**。serena の参照結果から
> `Assets/Tests/` と「コメント行（`///`）内の `<see cref>`」を除外して数えること（grep の素の件数を信じない）。

## 手順（厳守）

1. **列挙（Glob・安価）**：`Assets/Scripts/Core/**/*.cs` から `public static class *Rules`（および主要な
   `*State`/`*Ledger` 等のドメイン型）を列挙する。総数を記録（前回監査＝約329 *Rules）。

2. **一次フィルタ（grep・安価＝serena コールを絞るため）**：各クラス名を **`Assets/Scripts/Game/` に限定**して
   Grep し、**Game テキストヒット数**を取る。
   - ヒット>0 → 「Game参照あり（配線済の見込み）」へ暫定分類（数件は手で正しさを確認）。
   - **ヒット=0 → 配線候補**。次段の serena 精査へ回す（これで高価な serena 呼び出しを候補集合だけに限定）。

3. **精査（serena・候補集合のみ）**：Game ヒット0の各クラスに `find_referencing_symbols` を実行し、
   参照元を {Game／Core／テスト／docコメント} に仕分ける：
   - Core 実利用あり（テスト・docコメントを除いて他 Core から呼ばれる）→ **Core島**。
   - テスト＋docコメントのみ → **真の孤児**。
   - （一次フィルタの漏れで）実は Game 参照あり → 「Game参照あり」へ訂正。
   - **規模が大きいときは並列化**：候補をサブエージェント（既定3〜5体）に分配し、各自が担当クラス群に
     `find_referencing_symbols` をかけて {Core島／真の孤児} の分類だけを返す（親が集約）。`/core-wave` と同流儀。

4. **Tier 付け**（プレイヤー体感への近さ × 設計意図。既存監査の枠を踏襲）：
   - **Tier A（届けるべき）**＝軍事/兵站/諜報/政体駆動/外交など**主要ループに絡み「選択が結果を変える」**もの。
     Core島で Tier A 相当が最優先。
   - **Tier B（深み）**＝宗教/文化/司法/労働市場/人物職分など、配線で体感が増すが必須でないもの。
   - **Tier C（意図的 Core-first・配線しない）**＝経済の業種 archetype（`ChemicalRules` 等100+）・金融銘柄系・
     世界観フレーバー。**個別配線するとタイクン化＝終盤ラグ**。集約・観測（J/E/Q 等）で背景化が正。
   - ★**全件配線は禁忌**（CLAUDE.md スケーラビリティ規律／orphan監査 §0 但し書き）。Tier C は breadcrumb として残す。

5. **誤読防止チェック**（必ず実施）：「Game参照ゼロ」でも、配線済オーケストレータ経由で**推移的に効く**ものがある
   （例：`FiscalRules`→`CampaignRules.TickFiscalYear`→`GalaxyView`）。Core島と判定したものは、その主呼び出し元 Core を
   1段だけ `find_referencing_symbols` で辿り、**鎖の先に Game があれば「配線済（推移）」へ訂正**する（最低限の推移チェック）。

6. **記録更新**：
   - `docs/core-orphan-audit.md` のサマリ数値（総数／Game参照率／Core島数／真の孤児数）と Tier 表を更新。
     更新日と「serena 精査ベース（テスト・docコメント除外）」である旨を明記。
   - `.claude/wire-backlog.md` に Tier A→B の候補を `- [ ] <Rules名>｜届かない効果｜配線先(オーケストレータ)｜Tier` 形式で
     書き出す（Tier C は「配線しない（観測で背景化）」節に分けて列挙）。

7. **コミット**：現在ブランチへ `wire-audit：配線監査を更新（Core島N件・真孤児M件・Tier A上位K件）` でコミット。

## 配線へ渡すとき（このコマンドの外＝別タスクの安全網）
`/wire-audit` は**監査だけ**。実際に配線する別タスクでは必ず次の2つを使う（CLAUDE.md の罠を踏まないため）：
- **直列化 pre-check**：調整値を効かせる配線は、対象フィールドを **`.unity`/`.prefab` から先に grep**（直列化値がスクリプト既定に勝つ＝#2548）。直列化されていれば**そのファイルを直す**。
- **Game層 compile 検証**：`dotnet test`(TestHarness) は Core しか見ない。Game を触る配線は **unity-mcp `Unity_ReadConsole`**（または GameCI）でコンパイルエラーを確認する（Game層の沈黙クラッシュ防止）。
- 配線の作法：**Core純ロジックは不変・既存窓口を呼ぶだけ**／`GalaxyView` の暦境界Tick（日次/年次）へ相乗り（毎フレーム再計算しない）／観測層 glossary に1行追記／`dotnet test` green を維持。

## 禁止事項
- 配線そのものをこのコマンドで行うこと（監査と候補提示まで。実装は人手裁可で別タスクへ昇格）。
- Tier C（経済業種・金融銘柄・フレーバー）を配線候補に積むこと（集約・観測で背景化が設計意図＝終盤ラグ回避）。
- 素の grep 件数だけで「配線済」と判定すること（テスト・docコメントのノイズと推移配線を serena で必ず確認）。
- 全 `*Rules` に無差別 serena を流すこと（一次 grep フィルタで Game ヒット0に絞ってから精査＝コスト規律）。
