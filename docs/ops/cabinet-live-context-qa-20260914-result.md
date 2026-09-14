# 閣僚決裁：実 GalaxyView コンテキストの接続検証（#2768 #141 #67）

- task_id: cabinet-live-context-qa-20260914 / attempt_id: a1
- 基点: 93f617c8
- 担当: プログラム担当 Claude（Read/Grep/Glob/Edit/Write のみ。git・試験実行・シーン/プレハブ/セーブの変更なし）

## 結論
- 前タスクの PlayMode 試験（`CabinetDecisionBridgePlayModeTests`）は Core の合成をフックへ注入していて、実 GalaxyView からの組み立てを通していなかった。この不足を補う PlayMode 試験 `CabinetLiveContextPlayModeTests`（4件）を追加した。
- コードを読んだ範囲では接続の不具合は見つからなかった（`CabinetDecisionContextOf` は `StateOf(f).politics`・`ministries[FactionIndex]`・`TopMinistryIdOf`・`ElectionRoster()`・`ElectionYear()`＝統一クロックから組み立てている）。**試験は未実行**のため、実際に合格するかは未確認。
- 試験から本番経路へ入る入口が無かったため、狭い QA 入口を1つ追加した。操作者の人物を固定するだけで、権限の結果は渡さない（下記）。

## 実際に通す経路（試験の設計。未実行）
1. 無効な GameObject に載せた `GalaxyView` に `BindElectionQaWorld` で固定の同盟を接続（共和制・2党・星系2・文民政治家30名）。`Start` と実セーブファイルは走らせない。
2. 本番の `SeedGovernmentForQa`（要職・二官八省）→ `RunPoliticsTickForQa`（総裁選・国政選挙・組閣 `RunCabinetAndPartyExecutives`・党三役）を通し、実際の省庁・人物・`CabinetState` を作る。省 id は決め打ちせず、実省庁ツリーから所掌が財政の省を探す。
3. `GalaxyView.SwapActiveForQa(view)` で観測世界に接続し、本番の `DecisionAuthorityDirector` を `AddComponent` する（Awake が本番の `DecisionDeck.AuthorityCheck = Evaluate` を差す）。
4. 操作者は `GalaxyView.BindPlayerCharacterForQa(実名簿の人物)` で固定する。これで `PlayerCharacter()` → `EvaluateEffectKey` → `EvaluateFor` → `CabinetDecisionContextOf` という本番の経路を通る。
5. 上申は `DecisionDeck.Resolve` → 本番フック → `EscalateShared` → `Escalate` で登録する。`StrategySession.Clock` を `reviewSeconds` 越えまで進めて1フレーム待ち、本番の `Director.Update` → `Conclude` → `EvaluateDecider` → `ConcludeApproval` を通す。

| 試験 | 内容 |
|---|---|
| `LiveContext_MatchesGalaxyView_MinisterDecides_OthersEscalateToRealMinister` | `CabinetDecisionContextOf` の組み立てが実データと同じか：politics・省庁ツリーは同一参照、top id、名簿（軍人＋文民）、年＝797、4省。実在の大蔵大臣の `EvaluateFor(tax.hike)` が裁可し、根拠に所管省名が入る。大蔵政務官と他省の大臣は実在の大蔵大臣へ上申（id と名前）。`TryPreviewAuthority` の根拠が裁可時の判定と一致。大臣本人は本番フック経由で直接裁可でき、効果は1回だけ |
| `SecretaryEscalation_ApprovedByLiveDirector_AppliesExactlyOnce` | 政務官の裁可 → 上申（決裁者＝実大臣、根拠＝見込み表示と同じ）。審査時間の前は結論が出ない。実 Clock で審査時間を越えると Director.Update が承認し、効果は1回。「［上申の裁可］」の通知は1通。確定後に本番フックが戻る。次のフレームや再解決で効果が重ならない |
| `MinisterDismissedDuringReview_LiveDirectorSendsBack_WithoutEffect` | 上申の審査中に首相が大蔵大臣を解任（`CabinetAppointmentRules.Dismiss`）。審査の結果は承認に届く（Favor ≥ 閾値を前提として確認）が、承認直前の再判定で権限なしとなり、効果なしで差し戻す（settled・applied・対象外・理由に「権限」・「［差し戻し］」1通・承認の通知0通）。再解決しても効果なし |
| `DelegatedVice_LosesAuthority_WhenLiveClockPassesDelegationEnd` | 大臣が副大臣へ所管決裁を SE797 まで委任。委任中は副大臣が本番フックで裁可できる（効果1回）。実 Clock を SE798 へ進めると、政治 Tick を回さず委任の記録が残ったままでも決裁できなくなる（上申先＝実大臣、根拠に「期限」、context.year＝798）。本番フックは上申にし、Director の承認で効果1回。前の案件の効果は重ならない |

TearDown で戻すもの：`GalaxyView.Active`、`StrategySession.Campaign/Map/Provinces/Clock/Decisions`、`DecisionDeck.AuthorityCheck`（Director を DestroyImmediate した後）、`Resolved` の購読、`GovernmentRegistry`（保存していた任命へ）、QA で固定した操作者（null）。

## 変更一覧
| ファイル | 内容 |
|---|---|
| `Assets/Scripts/Game/GalaxyView.ElectionQa.cs` | QA 入口 `BindPlayerCharacterForQa(Person)` と private `qaPlayerCharacter` を追加（非保存。操作者を固定するだけで、権限の結果は渡さない） |
| `Assets/Scripts/Game/GalaxyView.Government.cs` | `PlayerCharacter()` の先頭に「QA で固定したときだけその人物を返す」1行を追加（本番では常に null で、従来どおり `ProtagonistCareerDirector`） |
| `Assets/Tests/PlayMode/CabinetLiveContextPlayModeTests.cs`（＋`.meta`・guid 重複なしを確認） | 新規の PlayMode 試験4件 |
| `docs/ops/cabinet-live-context-qa-20260914-result.md` | 本書 |
| `docs/ops/claude-status.json` | 作業状態 |

既存の仕様や試験は緩めていない。Core は変更していない。シーン・プレハブ・設定・実セーブ・ChatGPT レビュー JSON・他の作業票も編集していない。

## 未実行の試験（すべて未実行）
- Unity コンパイル（Game・PlayMode）：Game 層に変更あり（`GalaxyView` の partial 2ファイル）。
- PlayMode `CabinetLiveContextPlayModeTests`（新規4件）。
- 関連回帰：PlayMode `CabinetDecisionBridgePlayModeTests`・`CabinetPostsPlayModeTests`・`RingiFlowPlayModeTests`・`GalaxyView` の QA 入口を使う選挙系 PlayMode、EditMode `CabinetDecisionAuthorityRulesTests`、TestHarness 全件（Core は変更なし）。

## 区別：自動試験と実画面
- 自動試験で見るのは、数値と状態（context の各フィールド、`DecisionAuthorityResult`、カードの escalated/settled/applied/outcome、`Resolved` の回数、通知の件数）だけ。**いずれも未実行**。
- 実画面（右下の決裁デスク・決裁ボード・見込み表示のクリック操作）は確認していない。

## 前提と残件
- **前提（失敗したら前提として報告）**：政治家30名で、初年の自動組閣が大蔵省の大臣・副大臣・政務官と他省の大臣を埋めること。空席なら `Post()` が空席理由つきで失敗する。
- **操作者の固定**：本番の `PlayerCharacter()` は主人公ディレクタの主人公（軍人）を返すため、本番でプレイヤーが閣僚として決裁する状況は、主人公が文民政治家として入閣しない限り起きない。今回は人物だけを QA で固定した。主人公の政界転身と入閣の接続は未検証（次機能）。
- **RingiDirector**：稟議の起票（`RaiseAgendaItem` → `StampAttribution` → `EvaluateFor`）は、官僚機構の伝播が `Random.value` に依存し非公開のため、今回の試験では通していない。効果は `DecisionDeck.Resolved` の発火回数で数えている。RingiDirector による税の実適用（国庫・税率の変化）は未検証。
- **審査中の失職の種類**：試験したのは首相による解任だけ。死亡の場合は `Director.Update` が「決裁者不在」の分岐（ConcludeApproval の手前）で閉じる。知事就任・党首就任・野党への移籍による失職は Core 試験（`HolderLapseProblem`）で固定済みで、実盤面では未検証。
- 前タスクの残件（手動の閣僚任免・委任 UI、決裁カードへの所管省 ID、内政の複数省の省選択、外交省の不在、首相権限と元首権限の食い違い）は変わらない。
