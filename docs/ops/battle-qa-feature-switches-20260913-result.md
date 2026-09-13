# SPEED-08 検証用の独立スイッチ（固定会戦QA）結果 — task battle-qa-feature-switches-20260913 / a1

作成：Claude Code（実装）。レビュー・試験の実行：ChatGPT。**この文書の時点でコンパイル・試験はどれも未実行**（Claude の道具は Read/Grep/Glob/Edit/Write のみ）。未実行のものは合格扱いにしない。commit/push・画面操作・セーブ書換えはしていない。仕様2・SPEED-09（性能測定）は対象外。

## 1. 最初に確かめた実経路と依存（静的確認）

| 機能 | 実経路 | QA（名前が Battle でない使い捨てシーン）での状況 | 今回の扱い |
|---|---|---|---|
| 自動包囲 | `BattlefieldCommandManager.ApplyBattleFlow` の②回り込み：`CorpsBattleFlowRules.ShouldAttemptEnvelopment`（前衛が交戦距離40以内・後衛あり・軍団長能力≥0.55・決定論 roll）→ 後衛の `FleetAI.enveloping/flankTarget` → `FleetAI.TryEnvelopOrCounter` が移動先にする | QAは `useCorpsCommandManager` のプリセットで自前の軍団長AIを置いている＝実経路あり | **接続**（新規 `autoEnvelopment`） |
| 援軍 | ①`BattleSetup` の時限増援（`ScenarioData.FleetEntry.reinforcementDelay`→`Update`→`SpawnReinforcement`）②戦略の援軍台帳 `StrategySession.Reinforcements`→`BattleManager.TakeArrivedReinforcements`→`BattleSetup.SpawnWarpReinforcement` | ①は `Awake` のシーン名ガードで停止し、`fleetPrefab`/`ScenarioData` も前提。②はキャンペーン所有の台帳で、`BattleManager` はQAの隔離で停止 | **未接続**（ON は理由つきで準備失敗） |
| 戦況イベント | `BattleEventManager`（`tickInterval` ごとに `EventEngine.Tick(Random.value)`→決裁デスク／通知は即時適用→士気変化は `MoraleChangeSource.戦況イベント`） | 自動生成はシーン名 Battle 限定。手で置いても `Awake` のガードで自壊・`Update` も止まる | **接続**（QAシーン限定の許可を追加） |

★`FleetAI.joinEncirclement`（旗艦の取り囲み参加）は **どこからも読まれていない**（Assets 全体を grep して宣言1件だけ）。切り替えても何も起きないので、自動包囲のスイッチには使っていない。この項目を直すかどうかは別の話として残す。

## 2. 実装（開始前の設定だけ・途中切替なし）

- **Core `BattleQaFeatureSwitches`（新規）**：名前・版（1）・3つのスイッチ。既定 `FixedDefault` は **先行の固定QAと同じ状態＝自動包囲ON・援軍OFF・戦況イベントOFF**（全部ONではない）。`With`（1項目だけ変えたコピー）、`AutoName`、`Validate`（版違い・未接続の ON を拒否。援軍の理由は `ReinforcementUnconnectedReason`）、`Classification`（固定／切り分け／**自然イベントONモード**＝固定合格の比較対象外）、`Describe`。
- **`BattlefieldCommandManager.autoEnvelopment`（既定 true＝通常プレイは不変）**：false なら②の下令をせず（判断が成立した回数は `EnvelopmentSuppressed` に数える）、後衛へ回り込み目標も渡さない。カウンター遮蔽・決戦・総退却・スロット配布・FleetAI は止めない（AI全体停止とは別）。観測用に `EnvelopmentOrdersIssued` を追加。★OFF だと包囲の進捗が0のままなので、決戦（③）の窓は開きにくくなる（包囲を止めた結果として記録する）。シーン・プレハブへの直列化は無し（guid の grep で0件）。
- **`BattleEventManager`**：`AllowQaHostScene(Scene)`/`ClearQaHostScene()`/`HasQaHostScene`。許可したシーンの個体だけ、名前が Battle でなくても `Awake` のガードで自壊せず `Update` が動く。**既定は許可無し＝従来と同じ**。自動生成（`TryCreate`）はシーン名だけを見るので変わらない。観測用に `TickCount`/`FiredCount`/`NextTickAt`/`PendingDecisionCount` を追加。
- **`ReproducibleBattleQaSession`**
  - `Begin(preset, tuning, switches)` を追加（従来の入口は `FixedDefault`）。`Retry` はスイッチ（と抽選間隔の上書き）を引き継ぐ。
  - 準備：設定名・版・各スイッチ・分類をログ → `Validate` で不正なら何も組まずに準備失敗。戦況イベントON なら許可を立てて **QAシーンに BattleEventManager を1つだけ生成（開始まで無効）**、1フレーム後に自壊していないことを確認（自壊していたら準備失敗）。調整プリセット適用の後で `ApplyFeatureSwitches`（基準→適用値・対象・適用時刻＝開始前の実時間を記録）。
  - 他シーンの既存 BattleEventManager の停止など、**隔離の既定は変えていない**。戦況イベントON のときだけ隔離注記を「自然イベントONモード」に差し替える。
  - 開始：QA所有の会戦イベントを有効化（`eventTickIntervalOverride` が正ならQA個体の `tickInterval` にだけ当てる。★間隔を変えても無効化・強制発火にはならない）。開始時と観測完了時に `DescribeFeatureSwitchReadback`（autoEnvelopment・下令/見送り回数・回り込み中の艦隊数／QAシーンの BattleSetup 件数・稼働中の BattleManager 件数・QA所有の予約援軍0件／会戦イベントの有効・抽選・発火・未解決件数・次の抽選まで）。
  - 終了・再試行・準備失敗・シーン破棄（Play 停止含む）：片付けの前に `RecordSwitchDisposal`（**未解決の決裁は適用も自動採択もせず破棄**・発火件数・下令/見送り回数・援軍台帳は未変更）→ QA所有の会戦イベント（とそのUIが作った QA シーンの EventSystem）を破棄 → `RestoreGlobalState` で許可を解除し、復元レポートに載せる。
  - 画面：スイッチの内容・分類・適用時刻・OFF の項目を表示。
- **Editor メニュー**：「機能スイッチ/」に3つのトグル（自動包囲・援軍・戦況イベント）。次の準備から効く。結果ログの見出しでは、戦況イベントON なら「固定合格の比較対象外」と表示する。

## 3. 試験（追加のみ・どれも未実行）

- EditMode `BattleQaFeatureSwitchesTests`（8件）：既定＝先行QAの状態、1項目だけ変わる、自動名、援軍ON の拒否と理由、版違い、分類、記録内容、適用先の名前（joinEncirclement を使っていないこと）。
- PlayMode `ReproducibleBattleQaFeatureSwitchPlayModeTests`（7件）
  1. 既定は先行QAを再現（autoEnvelopment=true・会戦イベントなし・隔離注記が変わらない・ログに記録がある）。
  2. 自動包囲：試験用の小さな会戦（有能な軍団長・敵が交戦距離内）で、ON なら**実際に下令され FleetAI.enveloping が立つ**。OFF なら下令0・enveloping 無し・**判断成立の見送りが1回以上ある**。AI とスロット配布は動いたまま、他2つは変わらず、初期スナップショットも一致。終了後の既定は ON。前提が成立しなければ `Inconclusive`（合格にしない）。決定論 roll がシーン通番で変わるため、最大6回までやり直す。
  3. 戦況イベントON：QAシーンの実コンポーネントで、開始前は無効・抽選0回。開始後、**間隔を過ぎてから抽選が実際に回る**（発火したかどうかは合否にしない）。自動包囲と援軍は変わらない。終了で破棄と許可解除を確認し、Battle 以外に置いた個体がまた自壊すること（ガードが元に戻ったこと）も見る。
  4. 援軍ON：理由（未接続）つきで準備失敗。timeScale・Random.state が戻り、艦隊とシーンは残らない。
  5. 戦況イベント生成後の準備失敗（調整値あふれ）で、会戦イベントも許可も残らない。
  6. 終了せずにシーンを破棄しても、会戦イベントと許可が残らない。
  7. 再試行は同じスイッチを新しい対象へ当て直す（基準は通常値）。終了後の既定の準備に漏れない。
- 回帰：既存の Core 52件・Unity(PlayMode) 48件は変更していない。期待値は Core 60件・PlayMode 55件。**未実行**。

## 4. 未完・注意

- **コンパイル（Unity）・EditMode・PlayMode・TestHarness はすべて未実行。**
- 援軍は未接続のまま。接続するには、BattleSetup の時限増援をQAシーンから呼べる最小の入口を作るか、QA所有の援軍台帳を分けるか、どちらかの設計判断が要る（質問1）。
- 戦況イベントON のとき、製品の `NotificationCenter.Push`（表示用の共有リングバッファ）はそのまま動くので、QA中の通知が通知履歴に残る。キャンペーンの状態は変えないが、通知を抑えるかどうかは判断待ち（質問2）。
- 戦況イベントの士気効果は `GameSettings.playerFaction` の艦隊に掛かる（QA艦隊は同盟）。発火したかどうか・効果が出たかどうかは試験の合否にせず、ログと士気の原因台帳で読む。
- `FleetAI.joinEncirclement` は読まれていない項目のまま（質問3）。
- `docs/catalog/core-modules-catalog.md` と `components-catalog.md` には追記していない（既存の BattleQa 系も載っていないため、先行作業と揃えた）。`Tools/serialized-value-check.sh` は未実行（guid の grep で代わりに確認し、0件）。
- 画面での確認・自然会戦での確認は未検証。

## 5. 変更ファイル

- 新規：`Assets/Scripts/Core/Combat/BattleQaFeatureSwitches.cs`(+.meta)、`Assets/Tests/EditMode/BattleQaFeatureSwitchesTests.cs`(+.meta)、`Assets/Tests/PlayMode/ReproducibleBattleQaFeatureSwitchPlayModeTests.cs`(+.meta)、本文書
- 変更：`Assets/Scripts/Game/BattlefieldCommandManager.cs`、`Assets/Scripts/Game/BattleEventManager.cs`、`Assets/Scripts/Game/ReproducibleBattleQaSession.cs`、`Assets/Editor/ReproducibleBattleQaMenu.cs`、`docs/ops/claude-status.json`
- .meta の guid は手で付けた（Unity が取り込むときに衝突がないか確認が要る）。

### 2026-09-13 23:21 独立機能スイッチの途中検証
実Claude task battle-qa-feature-switches-20260913/a1は23:12:29にexit0・ID一致・編集停止で終了。ChatGPTがCore60/60・Unity PlayMode55/55（失敗/判定不能/skip0）を確認した。Coreは.NET8不在のため.NET10へのプロセス限定ロールフォワード。証跡outputs/qa/battle-switches-a1/core.trx、playmode.xml。
自動包囲の実下令ON/OFFと戦況イベントの実抽選・後片付けが対象。援軍は未接続を理由に準備拒否する試験であり、実接続の完成ではない。QAイベントが共有通知履歴に残る問題、QA同盟艦隊とGameSettings勢力の不一致も追加レビュー対象。次はBattleSetupの実増援経路をQA所有条件で使い、キャンペーン台帳を変更しない接続と試験をClaudeへ依頼する。画面/自然イベント発火/30分プレイ評価は未完了。IssueはOPENを維持。
