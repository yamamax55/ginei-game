# battle-tuning-profile-20260913 / a1 結果（SPEED-07 検証用調整プリセット・Issue #1045）

担当：Claude Code（編集のみ）。★コンパイル・試験は**すべて未実行**（Read/Grep/Glob/Edit/Write のみの環境）。合格は主張しない。時刻は取得手段が無いため記載しない。

## 1. 調査で確定したこと
- 固定QA（`ReproducibleBattleQaSession`）の艦隊は `AddComponent` で組む＝プレハブ/シーンの直列化値は入らない。先行QAの実効値は**スクリプト既定**：`FleetMovement.maxSpeed`=3.5／`rotationSpeed`=42／`FleetMorale.recoveryRate`=0.7／`routedRecoveryDelay`=4（Game 層で書き換える箇所なし＝grep 確認。起動処理が変えるのは `maxMorale`/`morale` のみ）。
- `FleetMorale.recoveryRate` は**通常時と敗走回復中の両方**の回復量。`routedRecoveryDelay` は**敗走時だけ**の回復待ち（通常時の回復に待ちは無い）。プリセットでは別項目にした。
- 速度・回頭の実移動は `maxSpeed/rotationSpeed × GetMobilityFactor()`（private）。プリセットとログは**基準項目**（補正前）を扱う。
- ★**軍団陣形間隔は固定QAでは `CorpsFormation.spacing` が効かない**：`CorpsFormation` は "Battle" シーンにだけ自動生成され、QAの使い捨てシーンには無い（手動の軍団隊形命令専用でもある）。固定QAで軍団AIが使う間隔は `BattlefieldCommandManager.ApplyCorpsFormationAndPost` の `Mathf.Max(CorpsMinSpacing(private const 7), 2×最大占有半径+3)`＝Inspector 項目が無い。→ 製品コードを変えずには上書きできないため、**今回は「未接続」として指定時に準備失敗で明示**（黙って無視しない）。

## 2. 実装
| ファイル | 内容 |
|---|---|
| `Assets/Scripts/Core/Combat/BattleQaTuningProfile.cs`（新規＋.meta） | `BattleQaTuningField`（移動速度/回頭速度/士気回復量/敗走回復待ち/軍団隊形間隔）・`BattleQaTuningMode`（未指定/絶対値/倍率）・`BattleQaTuningOverride`・`BattleQaTuningProfile`（設定名・版 `CurrentVersion=1`・`Default`＝全項目未指定・`With`・`Validate`（NaN/Infinity/範囲外/版違い/未接続を拒否）・`ValidateResolved`（適用値の有限・範囲）・`Describe`）。**値の正本を持たない**＝既定は実コンポーネントの実効値のまま、比較は倍率/絶対値の上書きだけ。 |
| `Assets/Scripts/Core/Combat/BattleQaTuningCatalog.cs`（新規＋.meta） | 既定＋1項目だけの比較用（移動速度×1.5／回頭速度×1.5／士気回復量×2／敗走回復待ち×0.5）。バランス提案ではない。 |
| `Assets/Scripts/Game/ReproducibleBattleQaSession.cs` | `Begin(preset, tuning)` を追加（従来の `Begin(preset)` は既定で委譲＝既存呼出し不変）。準備でプリセット検証の直後に**設定名・版をログ→不正なら何も組まず準備失敗**。Awake/Start 後・初期スナップショット前に `ApplyTuning`：全艦隊×4項目の基準（適用前の実効値）→適用値を**先に全件検証してから**書く（部分適用なし）・艦隊ごとにログ。`AppliedTuning`（記録）・`TuningReadbackMismatches()`（盤面の読み戻し）を公開し、開始時と観測完了時に読み戻しをログ。`Retry` は同じ調整を新しい艦隊へ再適用。復元レポートに「QA艦隊のみ適用・艦隊破棄で消滅」を明記。画面左上に調整プリセットを表示。 |
| `Assets/Editor/ReproducibleBattleQaMenu.cs` | `Ginei/QA: 固定会戦 調整プリセット/` に5択（チェック表示）。次の準備から効く（準備済みセッションには効かない旨をログ）。準備時のログに `Describe` を出す。 |
| `Assets/Tests/EditMode/BattleQaTuningProfileTests.cs`（新規＋.meta） | 既定＝未指定、方式ごとの解決、1項目だけ変わる、`With` の非破壊、NaN/Infinity/0/負/版違いの拒否、適用値あふれ、軍団隊形間隔の未接続明示、設定名・版・項目名の記述、一覧の整合。10件。 |
| `Assets/Tests/PlayMode/ReproducibleBattleQaTuningPlayModeTests.cs`（新規＋.meta） | ①従来入口の既定が先行QAの実効値（スクリプト既定）と一致・名前/版ログ ②4項目それぞれ1項目だけ×1.5で他項目・初期スナップショット不変 ③不正5種（NaN/Infinity/負/適用値あふれ/軍団隊形間隔）で準備失敗＋timeScale・Random.state・台帳・艦隊・シーンが戻る ④再試行で同じ調整を再適用（基準は前実行を引きずらない）・終了後の既定準備に漏れない。4件。 |

不変：既存の `ReproducibleBattleQaPlayModeTests`（10件）と製品ルール・生存/士気 assert は**未変更**。`FleetMovement`/`FleetMorale`/`CorpsFormation`/`BattlefieldCommandManager` の製品コード・アセット・セーブ・既定値は未変更。commit/push なし。仕様2は範囲外。機能スイッチ(SPEED-08)・性能測定(SPEED-09)は未着手。`docs/catalog/core-modules-catalog.md` は既存 BattleQa 系も未収載のため追記していない。

## 3. 必要な試験手順（ChatGPT 実行）
1. Unity コンパイル（error CS 0件）。新規ファイル4本＋.meta が取り込まれていること（古い生成 csproj に注意）。
2. EditMode：`Ginei.Tests.BattleQaTuningProfileTests`（10件）＋既存 `BattleQaPresetCatalogTests`/`BattleQaSnapshotTests`/`BattleQaRunLogTests`/`BattleQaJudgeRulesTests`。可能なら `TestHarness` で `dotnet test`（Core 新規2本が Stubs で通るか）。
3. PlayMode：`Ginei.Tests.ReproducibleBattleQaTuningPlayModeTests`（新規4件）。
4. PlayMode 回帰：`ReproducibleBattleQaPlayModeTests` 10件＋既存32件（先行と同じ全42件）＝`Begin(preset)` の委譲と準備手順の追加で既存が崩れないこと。
5. （任意・画面）Editor で調整プリセットを選び「準備」→ Console の結果ログに設定名・版・艦隊ごとの基準→適用値が出ること。★画面操作はユーザー指定時のみ。

## 4. 未完了・確認事項
- **軍団隊形間隔の比較は未実装**（上記1の理由）。実装するには製品側に比較用の入口が要る：案＝`BattlefieldCommandManager` の `CorpsMinSpacing` を同じ既定値7の `public` 調整項目にし（直列化チェック `Tools/serialized-value-check.sh BattlefieldCommandManager` 実施）、QAから当てる。ただし実間隔は占有半径からの算出値と大きい方になるため、「最小間隔」を比較するのか「実間隔」を比較するのかの仕様決定が必要。→ **questions** に記載。
- 速度/回頭は基準項目の比較。機動補正後の実効速度は private のためログに出していない（必要なら別票）。
- 試験・コンパイルはすべて未実行。
