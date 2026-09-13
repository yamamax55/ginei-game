# SPEED-07 残件：軍団隊形間隔の実接続 結果（battle-tuning-spacing-20260913 / a2）

- 担当：Claude（コード）／ChatGPT（コンパイル・試験）
- 時刻：Claude は時計を取得できないため記載なし（null）
- ★**コンパイル・試験はまだ実行していない**。先行の Core46／Unity46 合格は、この変更を入れる**前**の基準。今回の変更の合格を示すものではない。
- commit/push・画面操作なし。無関係な未コミット差分（肖像/階級章の .meta、フォント、VolumeProfile 等）には触れていない。

## 1. 経緯
a1 は 22:22 に ENOTFOUND で途中終了した（終了報告なし）。a2 では a1 の途中変更をすべて読み直し、確定仕様と照らし合わせた。**実装・試験とも仕様を満たしており、足りない部分は無かった**。そのため a2 ではコードを編集していない。やり直し・巻き戻し・重複定義もしていない。a2 で書いたのは本レポートと `claude-status.json` だけ。
※ 変更ファイルの一覧は内容の確認から作った（git diff は実行できる道具が無く、見ていない）。

## 2. 変更ファイルと理由（a1 で入り、a2 で確認済み）
| ファイル | 内容・理由 |
|---|---|
| `Assets/Scripts/Core/Combat/CorpsSpacingRules.cs`（新規＋.meta） | 間隔の算出を純ロジックに切り出した。`DefaultMinSpacing=7`（元の private const と同値）・`FootprintMargin=3`・`FootprintFloor`・`IsValidMinSpacing`・`EffectiveMinSpacing`（不正値は7）・`Resolve(min, maxFoot)`＝`Max(min, 2×maxFoot+3)`。実間隔を指定値に強制する入口は作っていない（重なりを作らない）。 |
| `Assets/Scripts/Core/Combat/CorpsSpacingResult.cs`（新規＋.meta） | 指定最小間隔・最大占有半径・占有下限・実間隔を分けて持つ。`FootprintBound`（占有下限が勝つ）と `Describe()`（「★占有半径の下限が勝つ＝最小間隔をこれ以下へ調整しても実間隔は変わらない」または「最小間隔が効いている」）。 |
| `Assets/Scripts/Game/BattlefieldCommandManager.cs` | private const を `public float corpsMinSpacing = CorpsSpacingRules.DefaultMinSpacing`（[Header]/[Tooltip] 付き）に置き換えた。`ApplyCorpsFormationAndPost` は `CorpsSpacingRules.Resolve(EffectiveMinSpacing(corpsMinSpacing), maxFoot)` で算出し、軍団ごとに記録する（`TryGetCorpsSpacing`・Awake でクリア）。既定値のままなら算出式は以前と同じ。 |
| `Assets/Scripts/Core/Combat/BattleQaTuningProfile.cs` | `軍団隊形間隔`→`BattlefieldCommandManager.corpsMinSpacing`。未接続として拒否していた処理を、実接続に置き換えた。`RequiresCorpsCommandManager` を追加。`CurrentVersion` を 1→2 に上げた（解釈を変えたため）。 |
| `Assets/Scripts/Core/Combat/BattleQaTuningCatalog.cs` | 比較プリセット「軍団隊形間隔×2」（`CorpsSpacingComparisonScale=2`）を追加。 |
| `Assets/Scripts/Game/ReproducibleBattleQaSession.cs` | `ApplyTuning`：**QA が作った `commandManager` だけ**に最小間隔を当てる。基準値・適用値を記録し（`CorpsMinSpacingBaseline`/`Applied`）、不正な適用値は準備失敗にする。軍団長AIを使わないプリセットに指定されたら「適用先が無い」として準備失敗にする。読み戻し（`TuningReadbackMismatches`）の対象にも含めた。開始後は `LogCorpsSpacingWhenResolved` が軍団ごとに「指定最小間隔・占有下限・実間隔」をログに残す。復元レポートには「QA艦隊とQAの軍団長AIのみに適用・破棄で消滅」と明記。static なグローバル上書きや reflection は使っていない。 |
| `Assets/Editor/ReproducibleBattleQaMenu.cs` | 調整プリセットのメニューに「軍団隊形間隔（最小間隔）×2」（index 5）を追加。 |
| `Assets/Tests/EditMode/CorpsSpacingRulesTests.cs`（新規＋.meta） | 6件。既定値が元の定数と一致／既定なら以前の式と一致／下限を超える最小間隔で実間隔が広がる／下限未満では下限を保ち、さらに下げても実間隔が変わらない（指定値と実間隔を分けて保持）／等値の境界／不正値は7に戻る。 |
| `Assets/Tests/EditMode/BattleQaTuningProfileTests.cs` | 「未接続を明示」のテストを「実接続＋不正値（NaN/0/負/Infinity）の拒否＋比較一覧に含まれる」に置き換えた。計10件。 |
| `Assets/Tests/PlayMode/ReproducibleBattleQaTuningPlayModeTests.cs` | 不正値テストに軍団間隔の0/NaN と、適用先の無いプリセット（不退転）を追加。**実接続の結果を確認する2件を追加**：①陣形変更プリセットで実際に開始し、軍団長AIの算出記録と隷下に配られた実スロット（`FleetAI.corpsSlotLocal`）を読む。既定＝`Max(7, 2×盤面の最大占有半径+3)`、下限＋5の最小間隔で実スロットが比例して広がる、下限の0.5倍と0.25倍では実間隔・実スロットとも下限のまま、を確認する。ログの文言も確認する。②再試行で新しい軍団長AIに再適用される（基準値は7のまま）／終了後に新しく作った軍団長AIは既定7／既定で準備し直すと7で算出される。プロパティの代入だけで合格にはならない。計6件。 |

## 3. 直列化値の調査（#2548）
- `BattlefieldCommandManager` の guid `bc3d2de444154a4a8f15b842347235bc` を `Assets/**/*.{unity,prefab,asset}` で grep → **該当0件**（実行時に自動生成され、シーン/プレハブには置かれていない）。そのため `corpsMinSpacing` はスクリプト既定の7が効き、既存アセットとの値ずれは無い。アセットは変更していない。
- `Tools/serialized-value-check.sh` は Bash を使えないため**実行していない**（上の grep で代用）。

## 4. 試験手順（ChatGPT 担当）
1. Unity コンパイル：error CS が0件であること（Core/Game/Editor/Tests）。
2. Core（TestHarness `dotnet test` または EditMode）：`CorpsSpacingRulesTests`（新規6）＋`BattleQaTuningProfileTests`（10）＋既存 36 → **期待 52 件**。
3. PlayMode：`ReproducibleBattleQaTuningPlayModeTests`（4→6件）＋既存 42 → **期待 48 件**。
4. ログで確認すること：「調整プリセット「軍団隊形間隔×2」版2」「BattlefieldCommandManager.corpsMinSpacing（最小間隔）=7→14」「軍団隊形間隔の算出…指定最小間隔= 占有下限= 実間隔=」、下限が勝つ場合は「占有半径の下限が勝つ」の文言。

## 5. 残件
- 上記のコンパイル・Core 52・PlayMode 48 は**すべて未実行**。
- `Tools/serialized-value-check.sh` は未実行（grep で代用）。
- `docs/catalog/core-modules-catalog.md` への `CorpsSpacingRules` の1行追記は未実施（今回は変更範囲を最小に留めた）。
- 仕様2・機能スイッチ（SPEED-08）・性能測定（SPEED-09）は対象外で未着手。画面での確認・自然会戦での確認は未検証。
- 旧レポート `battle-tuning-profile-20260913-result.md` は「版1・軍団間隔は未接続」のままで、現状と合っていない（書き換えていない）。

## ChatGPT 再開確認（2026-09-13）
保存済みの試験結果を再読し、Core 52/52・Unity PlayMode 48/48（失敗0・判定不能0）を確認した。上記の「未実行」はClaude報告作成時点の記録であり、その後のChatGPT検証で解消済み。今回は再実行していない。Coreは.NET 8が無いため.NET 10へのプロセス限定ロールフォワードで実行された証跡である。

残る受入確認：
- 基準と比較プリセットで、軍団に属する各艦隊の間隔・視認性を画面で比較する。
- 移動・旋回・陣形変更中に、配下艦艇の不自然な交差や密集が無いか観察する（最終スロットの非重複試験だけでは保証しない）。
- 占有半径の下限が効く条件では、設定値を下げても実間隔が縮まらない理由がログで分かることを確認する。
- 通常会戦での操作感を確認してから、既定の間隔を変更するか判断する。現時点で新しい既定値は確定しない。

証跡：outputs/qa/battle-spacing-a2/core.trx、playmode.xml（Codex作業フォルダ内）。共有画面の操作は時間確保待ち。現在のClaude機能スイッチ編集と重複してゲームコードやUnityを操作しない。
