# SPEED-08 残件：QA所有の援軍の実接続と戦況イベントの隔離 — task battle-qa-reinforcement-isolation-20260913 / a1

作成：Claude Code（実装）。レビュー・試験の実行：ChatGPT。Issue #1045。
**この文書の時点で、コンパイル（Unity）・EditMode・PlayMode・TestHarness はどれも実行していない**（Claude の道具は Read/Grep/Glob/Edit/Write だけ）。実行していないものは合格として扱わない。commit/push/reset・画面操作・セーブ/v5/アセットの書き換えはしていない。既存の未コミット差分には触れていない。joinEncirclement の一般修正・性能測定・仕様2 は対象外。

## 0. 工程

| 工程 | 内容 |
|---|---|
| research | BattleSetup の時限増援（`Update`→`SpawnReinforcement`→`SpawnFleet`）、BattleEventManager の対象勢力（`GameSettings.playerFaction`）と通知（`NotificationCenter.Push` 3か所）、QAセッション、既存の機能スイッチ試験を読んで確認。依存として `ShipNameRegistry`（`SpawnFleet` が旗艦名を払い出す static の台帳）、`FleetRoster`/`OrderOfBattle`（艦隊番号>0 のときだけ書かれる）、`StrategySession.Reinforcements`（`WarpReinforcementLedger`）を確認 |
| implementation | Core・Game・Editor の変更、EditMode/PlayMode 試験の追加・更新 |
| integration | 未実施（コンパイル・試験の実行は ChatGPT） |

## 1. 実経路（援軍）

```
QAセッション準備（援軍=ON）
  └ BattleSetup.AllowQaHostScene(QAシーン)
  └ QAシーンに BattleSetup を1つ置く（Awake＝QA許可シーンなので通常の初期化・台帳クリアはしない）→ 開始まで enabled=false
  └ fleetPrefab ＝ QA所有のテンプレート（非アクティブの親の下＝Awake が走らず索敵にも載らない）
  └ BattleSetup.ScheduleReinforcementForQa(entry, QA同盟陣営)  ← 新しい小さな入口（予約リストに1件足すだけ）
開始（StartRun）
  └ BattleSetup.enabled=true
  └ 製品の BattleSetup.Update が game-time で経過を数える（ReinforcementRules.IsDue）
  └ 製品の SpawnReinforcement → SpawnFleet（Instantiate・ApplyAdmiralData・陣営・旗艦名・陣形・武装/AI 有効化）→ 戦場端へ配置
  └ ReinforcementSpawned コールバック → QAが QAシーンへの帰属を確認し、固定ID・軍団を付けて記録
  └ 通知は NotificationSink（QAローカルログ）へ
```

- QAは艦隊を作らない。生成は製品のコードが行う。QAが作るのは「プレハブの代わりのテンプレート」「使い捨ての提督（統率50＝艦艇数は明細どおり）」「予約1件」だけで、どれも終了時に消す。
- 予約の `fleetNumber` は 0。0 より大きいと `SpawnFleet` が static の `FleetRoster`/`OrderOfBattle` に登録するため。固定ID（90）と軍団は、出現を受け取った時点で QA の艦隊コンポーネントにだけ付ける（既存QAの `BuildFleet` と同じやり方）。★この2項目だけは SpawnFleet の経路ではなくQAが付けている。
- `StrategySession.Reinforcements` は読むだけで、書かない。開始時と終了時に、参照が同じか・予約件数・締め件数を比べて復元レポートに残す。
- 通常の Battle シーンは変わらない。QA許可は既定で無効。`ReinforcementSpawned`/`NotificationSink` は既定 null＝従来どおり `NotificationCenter` に送る。新しい項目はプロパティと static だけで、直列化されない（Battle.unity の値は無関係）。

## 2. 変更ファイル

- 新規
  - `Assets/Scripts/Core/Combat/BattleQaReinforcementPlan.cs`（+.meta）：援軍の明細（純ロジック）。固定ID 90・艦艇数 1000・到着 2 ゲーム秒・出現半径 20・高さ -10。陣営はプリセットの味方、軍団は味方で最初に軍団を持つ艦隊の軍団。検証内容：ID の重複、到着時刻は 0.1 以上で有限、艦艇数、半径、陣営が味方と一致、軍団が味方に実在。`Describe(seed)` あり。
  - `Assets/Tests/EditMode/BattleQaReinforcementPlanTests.cs`（+.meta）
  - 本文書
- 変更
  - `Assets/Scripts/Core/Combat/BattleQaFeatureSwitches.cs`：援軍を接続済みにした（`IsConnected` は全項目 true）。**版を 1→2 に上げた**。`ReinforcementUnconnectedReason` を削除し、代わりに `ReinforcementConnectionNote` を置いた。`TargetName(援軍)` の値を変更。援軍ON は「切り分け」に分類され、固定合格とは比べない。
  - `Assets/Scripts/Core/Fleet/ShipNameRegistry.cs`：`Unretire(name)` を追加。QAの援軍が撃沈されて永久欠番になった名前を、後片付けで元に戻すためのもの。通常の処理からは呼ばない。
  - `Assets/Scripts/Game/BattleSetup.cs`：QA許可（`AllowQaHostScene`/`ClearQaHostScene`/`HasQaHostScene`）、`ScheduleReinforcementForQa`（QA許可シーン・到着遅延>0・fleetPrefab ありのときだけ受け付ける）、観測用の `PendingReinforcementCount`/`SpawnedReinforcementCount`/`ReinforcementElapsed`、`ReinforcementSpawned`/`NotificationSink` を追加。Awake の先頭に QA許可シーンなら return を追加。`SpawnReinforcement` は生成件数を数え、コールバックを呼び、通知を送り先経由にした。
  - `Assets/Scripts/Game/BattleEventManager.cs`：`NotificationSink`（3か所の Push を `Notify` 経由にした）。`SetTargetFaction`/`HasTargetFactionOverride`/`TargetFaction`（明示が無ければ従来どおり `GameSettings.playerFaction`）。`ApplyChoiceForQaVerification(eventId, choice)` は QA許可シーンの個体でだけ使える決定論の効果確認で、抽選・発火数・デスクには触れない。登録は1回だけにする処理（`RegisterEvents` のガード）と、id→定義の索引を追加。
  - `Assets/Scripts/Game/ReproducibleBattleQaSession.cs`：援軍の配線、QAローカル通知、対象勢力の明示、読み戻し・判定・後片付け・復元の証跡（詳しくは3〜5）。`reinforcementArrivalSeconds`（Header/Tooltip つき）を追加し、Retry で引き継ぐ。
  - `Assets/Editor/ReproducibleBattleQaMenu.cs`：援軍トグルの表示名だけ変更。
  - `Assets/Tests/EditMode/BattleQaFeatureSwitchesTests.cs`、`Assets/Tests/EditMode/ShipNameRegistryTests.cs`、`Assets/Tests/PlayMode/ReproducibleBattleQaFeatureSwitchPlayModeTests.cs`
  - `docs/ops/claude-status.json`
- .meta の guid 2件は手で付けた。一意かどうかは、既存 .meta を grep して一致0件で確認した。Unity が取り込むときにもう一度確認が要る。

## 3. 開始前の ON/OFF と記録・読み戻し

- 援軍の明細は ON/OFF どちらでも準備の2フレーム目に決める。OFF は予約しないが、同じ到着時刻を比較用に記録する。`Begin` の直後（同じフレーム）に `reinforcementArrivalSeconds` を設定すれば反映される。
- ON のときの準備：明細（固定ID・陣営・軍団・艦艇数・到着 game 時刻・出現半径/高さ・陣形・seed）をログに出す。1フレーム後に**読み戻し**で確認する（BattleSetup がある・予約=1・生成=0・QA受領=0・QAシーンの予定外の艦隊=0）。1つでも違えば準備失敗。
- 出現時のログ：固定ID・軍団・旗艦名（軍団旗艦かどうか）・艦艇数・陣営・到着 game 時刻（開始からの経過と BattleSetup の経過の両方）・位置・AI 有効・seed。
- `DescribeFeatureSwitchReadback` の援軍行：BattleSetup の有効/予約/生成/経過、QA受領数、早着数、QAシーンの予定外の艦隊数、到着予定、援軍艦隊の詳細、稼働中の BattleManager 数、戦略台帳の予約件数。
- 観測完了時、**援軍=ON のときだけ**次の2つを判定に足す。OFF（既定）のログは従来と同じ。
  - 「援軍の到着（時限増援の実生成・1回だけ）」：QA受領=1・BattleSetup の生成=1・予約残り=0・早着=0・予定外の艦隊=1。到着時刻の前に観測が終わった場合は未判定。
  - 「援軍が味方として動く」：陣営＝QA同盟、味方と敵対しない、敵と敵対する、AI/武装/移動が有効、出現位置からの変位 ≥0.1。出現後の観測が2秒未満なら未判定。

## 4. 後片付け・復元（retry / end / 準備失敗 / シーン破棄 / Play 停止）

- End・Retry・準備失敗（どれも End を通る）：コールバックと送り先を外す → 援軍艦隊・QA所有の BattleSetup（未到着の予約ごと）・テンプレートの親を `DestroyImmediate` → 一時提督を破棄 → QAが払い出した旗艦名を `Release`＋`Unretire` → `BattleSetup.ClearQaHostScene()`。
- シーン破棄・Play 停止（`OnDestroy`。Play 停止は Editor メニューから End も呼ぶ）：オブジェクトは Unity に消させ、BattleSetup が残っていればコールバックを外して無効化する。旗艦名の返却と QA許可の解除は同じように行う。
- 旗艦名を戻しても既存の割り当ては壊れない。`Assign` は使用中・永久欠番の名前を返さないので、QAが受け取った名前はQA前にどちらでもなかったことが保証されている。
- 復元レポートに次を追加した：「援軍のQA許可 解除（許可残り=…）」、「共有の通知履歴 LastSeq a→b（QA中の増加 n 件…履歴の Clear はしない）」、「戦略の援軍台帳 未変更=…（予約・締め）」。static の `LastSharedNotificationDelta`/`LastStrategyLedgerUnchanged` にも残す。
- Random.state・timeScale・士気の原因台帳の設定・隔離の復元は従来どおり。セーブ・アセットには書かない。

## 5. 戦況イベントの隔離

- QA所有の `BattleEventManager` は準備時に `SetTargetFaction(AlliedFaction)` と `NotificationSink = QAローカル` を設定する。`AlliedFaction` はプリセットの味方陣営で、全プリセットで同盟。`GameSettings.playerFaction` は書き換えない。援軍・戦況イベントを ON にしたのにプリセットに味方がいなければ、準備失敗にする。
- 通知は `QaNotifications`（上限200件。超えた分は件数だけ残す）と結果ログ（「QA通知（共有の通知履歴へは送らない）」）にだけ残す。援軍の「増援到着」も同じ送り先に送る。`NotificationCenter` の履歴を Clear して帳尻を合わせることはしていない。
- 通常の Battle の個体は送り先 null・明示なし＝従来どおり（共有の通知履歴・`GameSettings.playerFaction`）。
- 決定論の効果確認（`ApplyChoiceForQaVerification`）は試験から明示的に呼ぶだけで、セッションは呼ばない。抽選数・発火数に数えないので、自然発生の確認とは区別される。自然イベントONモードの分類（固定合格の比較対象外）は変えていない。

## 6. 試験（Claude が作成・どれも未実行）

### EditMode（Core・TestHarness 対象）
- `BattleQaFeatureSwitchesTests`（8件のまま・**変更 2件**）
  - `Reinforcement_IsUnconnected_OnIsRejectedWithReason_OffIsAccepted` → `Reinforcement_IsConnectedToBattleSetup_OnIsAccepted_AndClassifiedAsIsolation` に更新：接続済み・版2・援軍ON を受け付ける・分類は切り分け・接続説明・版違いは引き続き拒否。
  - `TargetName_NamesRealComponentOrUnconnected` → `TargetName_NamesRealComponent` に更新。
- `BattleQaReinforcementPlanTests`（**新規 4件**）：プリセットに合わせた既定値（陣形変更＝陣形軍団／不退転＝軍団なし／全プリセットで整合）、味方陣営の解決（敵だけ・null は失敗）、不正値の拒否（ID 重複・到着0/NaN・艦艇数0・半径0・陣営違い・存在しない軍団・味方なし・プリセット null）、記録内容。
- `ShipNameRegistryTests`（**新規 1件**）：`Unretire` で欠番が解ける・使用中にはしない・プール順で再び払い出される・null 安全。
- 期待値：先行の関連 Core 60件 → **64件**（BattleQa 系：変更2・追加4）。これとは別に `ShipNameRegistryTests` が +1件。

### PlayMode（Unity）
- **削除 1件**：`ReinforcementOn_FailsWithUnconnectedReason_AndRestores`（未接続を理由に拒否する試験。実接続の試験に置き換えた）。
- **追加 6件**
  1. `ReinforcementOn_RealTimedSpawnOnceAtArrival_NoDuplicateOverTime_MovesAsAlly_EndCleansUp`：開始前の読み戻し（BattleSetup は QAシーン・無効・予約1・生成0・予定外0・自動包囲と戦況イベントは不変）→ 一時停止中は出ない → 到着時刻の前は0 → 到着で1回（早着0・到着時刻は arrival-0.05〜+0.5）→ 援軍の固定ID/軍団/陣営/艦艇数/旗艦名が払い出し済み・AI/武装が有効・味方と非敵対・敵と敵対・索敵在庫に+1 → 時間を流し続けても重複しない（観測完了で止まったら再開）・出現位置から動いた → 到着判定が合格 → 「増援到着」はQAローカルにあり共有履歴に無い → End で艦隊・BattleSetup・QA許可・旗艦名が残らない、戦略台帳は参照と件数が同じ、timeScale と Random.state が戻る → QA許可の無い BattleSetup は予約を受け付けない。
  2. `ReinforcementOff_SameArrivalTimePasses_NoFleetSpawned`：OFF でも同じ到着時刻を記録し、BattleSetup は0件。到着+1.5秒を過ぎても出現0・予定外の艦隊0・艦隊数は不変・BattleManager は稼働0。既定のログに援軍の判定が足されない。
  3. `ReinforcementOn_PrepareFailureAfterCreation_RemovesSetupTemplateAndPermission`：予約を作った後に調整値あふれで準備失敗 → BattleSetup・QA許可・シーンが残らない、timeScale と Random.state が戻る、戦略台帳は不変。
  4. `ReinforcementOn_SceneUnloadedAfterSpawn_RemovesFleetNameAndPermission`：到着 0.5 秒で出現させた後、End を呼ばずにシーンを破棄 → 艦隊・BattleSetup・QA許可・旗艦名が残らない。
  5. `ReinforcementOn_RetryReschedulesOnNewSetup_OldFleetRemoved_EndDoesNotLeak`：再試行で旧艦隊と旧 BattleSetup が即座に消える。新しい BattleSetup に予約1・出現0。到着時刻を引き継ぐ。終了後に既定で準備すると援軍は無い。
  6. `EventsOn_DeterministicEffect_TargetsQaAlliedFleets_NotificationsStayLocal`（**決定論の効果確認・自然発生の確認ではない**）：`GameSettings.playerFaction` を帝国にして（TearDown で戻す）、QAが書き換えていないこと・対象が同盟に明示されていることを確認。一時停止中に「補給線に不安／慎重」（士気-5）を1回適用 → 同盟艦隊だけ -5・帝国は不変、抽選/発火は0、QAローカルに通知1件・結果ログにあり共有履歴に無い。無効な id・選択肢は false。通常の個体は送り先 null・明示なし・対象は GameSettings・効果確認を受け付けない。
- **変更 2件**（アサーションの追加だけ）：`Default_ReproducesPriorFixedQaState_AndLogsSwitches`（援軍の予約なし・QA許可なし）、`EventsOn_RealManagerRunsLotteryInQaScene_OthersUnchanged_EndRestoresGuard`（援軍のQA許可なし・対象勢力がQA同盟と一致・送り先がQAローカル）。
- 期待値：先行 PlayMode 55件 → **60件**（-1＋6）。`ReproducibleBattleQaPlayModeTests`・`ReproducibleBattleQaTuningPlayModeTests` は変更していない（復元レポートへの追記は既存アサーションの文字列を消していない）。

## 7. 隔離の方法（まとめ）

| 対象 | 方法 |
|---|---|
| 援軍の予約・生成 | QAシーン限定の許可＋QA所有の BattleSetup にだけ予約。戦略台帳は読むだけ（参照・件数の証跡） |
| 艦隊台帳・編制ツリー | 予約の艦隊番号0＝書かない。固定ID・軍団はQAの艦隊コンポーネントにだけ付ける |
| 旗艦名台帳 | QAの払い出し分を返却し、欠番化していれば解く |
| 通知 | QA所有の BattleEventManager/BattleSetup は送り先をQAローカルに。共有履歴は Clear しない（増加件数だけ証跡に残す） |
| 対象勢力 | QA個体にだけ明示。GameSettings は書き換えない |
| 既定 | 援軍OFF・QA許可なし・送り先 null＝通常 Battle と既定の固定QAは従来どおり |

## 8. 残件・注意

- **コンパイル（Unity）・EditMode・PlayMode・TestHarness はすべて未実行。** 試験の合否は未確認。
- QA所有の艦隊の戦闘まわりの通知（`FleetStrength.DestroyFlagship`、`BattlefieldCommandManager`・`CorpsFormation`・`FleetWeapon`・`FleetAI`・`Squadron` の直接 Push）は、今回の範囲外で共有の通知履歴に送られたまま。今回隔離したのは QA所有の BattleEventManager と援軍（BattleSetup）の通知だけ。この事情のため、試験では「共有履歴が1件も増えない」ではなく「QA所有の通知の文言が共有履歴に無い」を見ている。
- 援軍の固定ID・軍団は、SpawnFleet の後にQAが付けている（艦隊台帳に書かないため）。`SpawnFleet` の `IsPlayerControlled` は `GameSettings.playerFactionData`/陣営の enum を読み、同盟の援軍には `playerCommanded=true` が付く（QAの `BuildFleet` 艦隊は false）。挙動への影響は未確認。
- 援軍の実際の到着判定は BattleSetup の経過、QAの記録は開始からの Time.time 差で、最大1フレームずれる（許容 0.05 秒）。
- 援軍が軍団（陣形プリセットでは陣形軍団）に加わるため、プリセット側の判定（退却・陣形の合否）は変わりうる。援軍ON は分類上「切り分け」で、固定合格とは比べない。
- 「援軍が味方として動く」の判定はプリセットの観測時間によって未判定になりうる（陣形変更は観測完了が3.5〜7秒）。試験1では移動を直接確認している。
- `reinforcementArrivalSeconds` は `Begin` と同じフレームで設定しないと反映されない（API として弱い）。
- `docs/catalog/core-modules-catalog.md`・`components-catalog.md` には追記していない（既存の BattleQa 系も載っていないため、先行作業に揃えた）。`Tools/serialized-value-check.sh` は未実行。追加したのはプロパティ・static・非直列化の private フィールドだけ。
- 画面での確認、自然イベントの発火確認、30分プレイの評価はしていない。
