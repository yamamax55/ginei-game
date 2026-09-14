# 閣僚稟議の起票→上申→実税率反映の接続検証（#2768 #141 #67）

- task_id: cabinet-ringi-effect-qa-20260914 / attempt_id: a1
- 基点: 16e04b05（統合済み）。担当: プログラム担当 Claude（Read/Grep/Glob/Edit/Write のみ）
- **試験は未実行**（git・Unity・dotnet を使えない作業環境のため）。以下はコードを読んだうえでの判断で、実際に動かしての確認はまだ。

## 実際に通す入口

1. 前回と同じ固定世界 `CabinetLiveContextPlayModeTests.BuildLiveWorld()` を再利用（同じ試験クラスに2件追加）。無効な GO の GalaxyView に `BindElectionQaWorld`→`SeedGovernmentForQa`→`RunPoliticsTickForQa` で実省庁・名簿・首相・大臣・政務官を作り、`SwapActiveForQa` で差し込み、本番の `DecisionAuthorityDirector` を置く。
2. 操作者は `BindPlayerCharacterForQa` で実在の大蔵政務官に固定（固定するのは人物だけ）。
3. 本番の `RingiDirector` を AddComponent（Awake で `DecisionDeck.Resolved` を購読）。`raiseInterval=float.MaxValue` にして、Update の定期起票が混ざらないようにした。
4. **追加した QA 入口 `RingiDirector.RaiseFromSituationForQa()`**：本番の `TryRaiseFromSituation` をそのまま1回呼ぶだけ。流れは MeasurePetitionSituation → PetitionAgendaRules.Next（重複・クールダウン・件数上限の判定）→ RaiseAgendaItem（Submit → Propagate(Random.value) → SendToDecision → PlanTarget → StampAttribution → Enqueue）。権限の結果・カード・成功結果を外から渡す口は作っていない。
5. 状況：`fs.treasury=0` にして財政難（増税・切迫度1.0）を作る。政治家箱の信認を最大にする（RingiFlow 試験と同じ条件）。事前に `PetitionAgendaRules.Next` が `tax.hike` を選ぶことを確かめる。
6. 乱数：`Random.state` を退避してから `Random.InitState(20260914)`。同じ入口を最大400回まで同期で繰り返し、決裁デスクに届くまで回す。TearDown で `Random.state` を戻す。
7. 裁可：`DecisionDeck.Resolve(id,0)`→本番の権限フックで上申→実 Clock を `reviewSeconds+1` 進める→本番 Director の Update（審査→`EvaluateDecider`→`ConcludeApproval`）→`Resolve`→`RingiDirector.OnResolved`（`ClaimForApply`→`Decide`→`ExecuteAndApply`→`ExecuteBoardAction`→`RecordResult`）。

## 効果の検証（追加試験2件）

### `RingiAgenda_SecretaryRaise_EscalatedToRealMinister_AppliesTaxRateExactlyOnce`
- 起票時点：カードの `effectKey=tax.hike`。対応する稟議が台帳にあり（勢力＝同盟・決裁待ち・1件だけ）。`friction` は実省庁の財政 `DomainFriction` と一致。`proposerId/Name` は政務官本人、`deciderId/Name` は実在の大蔵大臣。`authorityBasis` は空でなく、`EvaluateFor(政務官)` の根拠と一致。税率はまだ変わっていない。
- 上申：政務官の裁可は false で `escalated` になる。次のフレームでも未確定で、効果は0回、税率も稟議も変化なし。
- 承認：確定し、自前の購読カウントが1、`d.applied` が立つ。**実税率が `before + TaxStepFull × ExecutionFidelity(friction)` と一致**（上がったことも別に確認）。国庫は変わらない。稟議は `執行済`、カードは `実行` で記録され、`ResultLine` は「執行…」になる。「［上申の裁可］」「［執行］」の通知はそれぞれ1件だけ。
- 重複しないこと：もう一度 Resolve しても false。同じ状況で再度起票を試しても、クールダウン中なので増税カードは増えない。さらに Clock を進めて2フレーム回しても、税率・適用回数・執行済の稟議数（1件）・「［執行］」通知数（1件）は変わらない。

### `RingiAgenda_MinisterDismissedDuringReview_SendsBackWithoutTaxOrTreasuryChange`
- 同じ起票から上申したあと、首相が実在の大蔵大臣を解任（`CabinetAppointmentRules.Dismiss`）し、審査時間を越える。
- 確定はするが効果は0回。`applied=true`（適用の権利は使い切る）、outcome は `対象外`、detail に「権限」を含む。**税率・国庫とも変わらない**。**稟議は `却下`**（決裁待ち・執行済は0件）。通知は「［差し戻し］」1件、「［上申の裁可］」「［執行］」は0件。次のフレームで Resolve しても false で、何も変わらない。

## 見つかった接続不具合と最小修正

1. **差し戻し・却下で閉じたカードの稟議が「決裁待ち」のまま台帳に残り続けていた**（`DecisionAuthorityDirector.CloseWithoutEffect`）
   - 原因：カードは `Settle`/`ClaimForApply` で閉じるが、`Resolved` を発火しないので `RingiDirector.OnResolved` が呼ばれず、稟議の状態を誰も動かしていなかった。`PetitionLedger.Prune` は進行中の稟議を落とさないため、差し戻しのたびに台帳の枠を使ったまま残り、観測層（Alt+I）には進行中として出続ける。
   - 修正：`CloseWithoutEffect` の最後に `ClosePetitionWithoutEffect(d)` を追加。`petitionId` と効果キーで台帳を選び（編制＝`FleetRingiDirector.Ledger`、それ以外＝`RingiDirector.Ledger`）、効果キーが一致したときだけ `RingiPipeline.Decide(pet,false)` で `却下` にする（決裁待ちのときだけ遷移）。効果は出さない。稟議を持たないカード（既存の Bridge/LiveContext 試験のカード）では何もしない。
2. **税など国家状態への効果を実際に適用したのに、カードの結果が `対象外`（「執行されませんでした」）になっていた**（`RingiDirector.ExecuteBoardAction`）
   - 原因：盤面を動かす命令ではないが効果は登録済みのキーのとき、`new PetitionActionResult(対象外, "")` を返していた。税率は `ExecuteAndApply` で既に変わっているのに、`ResultLine` が「執行されませんでした」になる。
   - 修正：実効量が 0 より大きければ `実行`（「実効 N% で執行」、amount=実効量）、0 なら `対象外`（「官僚機構で執行されませんでした」）。命令キー（動員・攻勢など）と未登録キーの扱いは変えていない。統治政策の経路は元から別処理。
3. **摩擦の出所が起票の他の処理と別の盤面を見ていた**（`RingiDirector.MinistryFriction`）
   - 原因：帰属・判断材料・執行は `GalaxyView.Active` を見るのに、摩擦だけ `FindAnyObjectByType<GalaxyView>()`（無効な GO は拾わない）を見ていた。本番では Active と同じ実体なので実害は確認していない。ただ QA 世界では既定値 0.4 に落ち、実省庁の省益を検証できなかった。
   - 修正：`GalaxyView.Active` を優先し、無いときは従来の Find にフォールバック。

## 変更ファイル
- `Assets/Scripts/Game/RingiDirector.cs`：`TryRaiseFromSituation` が決裁 id を返すように変更、QA 入口 `RaiseFromSituationForQa` を追加、`ExecuteBoardAction` の結果記録を修正、`MinistryFriction` を Active 優先に変更
- `Assets/Scripts/Game/DecisionAuthorityDirector.cs`：`CloseWithoutEffect` から紐づく稟議を却下で締める `ClosePetitionWithoutEffect` を追加
- `Assets/Tests/PlayMode/CabinetLiveContextPlayModeTests.cs`：UnityTest 2件と補助処理を追加。SetUp/TearDown に稟議台帳（`StrategySession.Petitions`）・`Random.state`・`GameSettings.playerFaction`・RingiDirector GO の退避と復旧を追加。既存の4件は変更なし（検証を緩めていない）
- `docs/ops/cabinet-ringi-effect-qa-20260914-result.md`（本書）、`docs/ops/claude-status.json`
- 新しい C# ファイルは作っていない（既存ファイルへの追加だけ）ので、.meta の追加は不要。Core・シーン・プレハブ・セーブ・ChatGPT レビュー JSON・他の作業票は変更なし

## 未実行の試験（ChatGPT で実施）
- Unity コンパイル（Game・PlayMode）
- PlayMode `CabinetLiveContextPlayModeTests`（既存4件＋新規2件）
- 関連の回帰：PlayMode `RingiFlowPlayModeTests`（全件。特に Approve/Decline の税、統治政策、Orphan、SaveLoad）、`CabinetDecisionBridgePlayModeTests`、`CabinetPostsPlayModeTests`。EditMode `RingiCompletionTests`/`RingiPipelineTests`。TestHarness 全件（Core 変更なし）
- 前提の注意：シード 20260914 で400回以内に浮上しなかった場合は、その旨のメッセージで失敗する（伝播の確率は heed 最大×(1−実省庁の摩擦)×正統性係数）

## 残件
- 実画面での決裁操作（右下カード／決裁ボード）の目視確認は別途必要。
- 死亡以外で審査中に失職するケース（知事・党首・離党）の実盤面検証。
- 主人公の政界転身・入閣と `PlayerCharacter` の接続。
- 既存の設計上の懸念（今回は未修正）：伝播で黙殺された稟議（`黙殺`）は `WorkflowRules.IsResolved` にも `IsActive` にも当たらないため、`PetitionLedger.Prune` で掃かれず台帳に溜まり続ける可能性がある（Core の変更になるので別の作業票で扱うのが妥当）。
