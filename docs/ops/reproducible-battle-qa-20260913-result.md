# 再現可能な会戦QA環境（仕様1のみ）結果レポート

- task_id: reproducible-battle-qa-20260913
- attempt_id: a2
- 担当: 実装＝Claude Code（CLI）／ビルド・試験の実行＝ChatGPT
- 着手時刻: 2026-09-13T20:32:25.5817092+09:00（依頼文に記載された runner の実測値をそのまま使用）
- 完了時刻: **取得できていない**（Claude 側に時計ツールが無い。runner の実時刻を参照してください）
- git: commit / push なし。仕様2には手を付けていない。共有画面（Unity）の操作もしていない。

## 0. 結論（先に）

- 実装は終わった。**ビルドも試験もまだ1件も実行していない**（このセッションで使えたのは Read/Grep/Glob/Edit/Write だけ）。
- 下の §5 の手順で実行してもらうまで、**どの試験も合格とは言えない**。
- 既存の結果はそのまま残している。前タスクの既存試験 32 件（MoraleSource 6／MoraleLockRout 9／FleetFormationHold 17）は**未実行の状態を変えていない**し、試験ファイルにも手を入れていない。通常会戦での原因つき観測（会戦イベントの自然発火）も**未完了のまま**。新しい固定QAは会戦イベントを止めているので、**自然発火の確認には使えない**。

## 1. 何を作ったか

退却・不退転・陣形変更を短い時間で再現するための、Editor 専用の入口を作った。

| 層 | ファイル | 役割 |
|---|---|---|
| Core（純ロジック） | `Assets/Scripts/Core/Combat/BattleQaPreset.cs` | 艦隊ごとの明細（艦隊ID・軍団・指揮権限・艦艇数・定数・提督・初期士気・位置・向き・陣形・AI）と、プリセット本体。固定ID順に並べる。AIの使い方（AI停止／通常AI／混在）は艦隊ごとの設定から自動で決める。矛盾チェックあり |
| Core | `BattleQaPresetCatalog.cs` | 3つのプリセットを数値で明示。既定の seed は 20260913、区別の確認用の別値は 7 |
| Core | `BattleQaSnapshot.cs` | 準備完了時の初期スナップショット。許容誤差つきで比較し、seed の違いも差として出す。明細との照合もする |
| Core | `BattleQaRunLog.cs` | 結果ログ（run_id・プリセット名・seed・ゲーム内時間を毎行に付ける）。上限は 400 行で、あふれた行数を数える。判定（合格／不合格／未判定）。記録喪失の注記 |
| Core | `BattleQaJudgeRules.cs` | 許容誤差つきの判定。前提が崩れたときは**未判定**を返し、合格にはしない |
| Game（`#if UNITY_EDITOR`） | `Assets/Scripts/Game/ReproducibleBattleQaSession.cs` | セッション本体。使い捨てシーンに実コンポーネントの艦隊を組み、一時停止・開始・観測・終了・再試行を担う。状態を元に戻す処理もここ |
| Editor | `Assets/Editor/ReproducibleBattleQaMenu.cs` | メニュー（準備×3・seed 切替・開始・再試行・終了・結果ログを出力）。既存の陣形保持記録と士気の原因台帳にもつなぐ |
| 接続（最小） | `Assets/Editor/FormationObservationQaMenu.cs` | `internal static bool Truncated` を1行だけ追加（400行を超えて記録を捨てたかを外から読むため）。既存の挙動は変えていない |
| 試験 | `Assets/Tests/EditMode/BattleQa{PresetCatalog,Snapshot,RunLog,JudgeRules}Tests.cs` | 純ロジック 36 件 |
| 試験 | `Assets/Tests/PlayMode/ReproducibleBattleQaPlayModeTests.cs` | 実コンポーネントの試験 10 件 |
| meta | 上の新規 12 ファイルぶんの `.meta` | GUID が既存と重複していないことを grep で確認済み |

**セッション本体を Game アセンブリに置いた理由**：PlayMode 試験のアセンブリ（`Ginei.Tests.PlayMode`）は Editor アセンブリを参照できない。メニューと試験で**同じ実装**を使うため Game に置き、ファイル全体を `#if UNITY_EDITOR` で囲んだ。Player ビルドにはコンパイルされない。Core の5ファイルは挙動を持たないデータと判定だけで、製品のどこからも呼ばれない（TestHarness でも回せるように、条件コンパイルはしていない）。

## 2. 通常プレイを変えていないこと

- 既存の Game／Core の製品コードは**1行も変えていない**。変えた既存ファイルは Editor の `FormationObservationQaMenu.cs` だけで、読み取り用のプロパティを1つ足しただけ。
- 調整値や計算式（撤退しきい値、不退転の持続、陣形コスト、士気の回復など）には触れていない。プリセットは Core の定数を**読むだけ**。
- シーン・プレハブ・セーブ・実提督 SO には書き込まない。軍団長データは `ScriptableObject.CreateInstance` で作り（`DontSave`）、終了時に破棄する。
- 結果を書き換えて合格にすることはしない。命令は実際の経路だけを使う：`ActiveCommandState.Issue`／`Squadron.RequestFormation`・`ReleaseFormationHold`／`BattlefieldCommandManager` 自身の総退却判断／交戦規定 `FleetStrength.stance`。被弾はすべて `FleetWeapon` の実射撃による。

## 3. プリセットの中身

座標は XY、向きは度で表す（0＝+Y＝`Transform.up`）。艦艇数は「現在/定数」。

### 3.1 退却（AI＝通常AI／軍団長AIあり）
| ID | 立場 | 軍団 | 艦艇数 | 位置 | 向き |
|---|---|---|---|---|---|
| 1 | 軍団長 | QA退却軍団 | 3200/10000 | (0,0) | 0 |
| 2 | 隷下 | QA退却軍団 | 3200/10000 | (-8,0) | 0 |
| 3 | 隷下 | QA退却軍団 | 3200/10000 | (8,0) | 0 |
| 4 | 独立 | なし | 10000/10000 | (-30,0) | 0 |
| 5 | 軍団長 | QA別軍団 | 10000/10000 | (30,0) | 0 |
| 6 | 隷下 | QA別軍団 | 10000/10000 | (38,0) | 0 |
| 11 | 敵 | なし | 10000/10000 | (0,50) | 180 |

- 軍団長の統率と功名心はどちらも 50、初期士気はすべて 100。
- 艦艇数の比 0.32 には意味がある。軍団総退却のしきい値 0.35（`CorpsRetreatRules`）は**下回り**、個艦が自分で撤退する比 0.3（`FleetAI.retreatRatio`）は**上回る**。こうすることで、撤退の原因を軍団総退却に切り分けられる。観測中に 0.3 を下回った艦は、判定を**未判定**に落とす。
- QA からは命令を出さない。見るのは次の3点：軍団長AIが総退却を出すか → 軍団の3隊すべてが撤退状態になり、敵から離れる向きへ実際に 1.0 以上動くか → 独立の1隊と別軍団の2隊が巻き込まれないか。

### 3.2 不退転（AI＝全艦停止／軍団長AIなし）
| ID | 役 | 艦艇数 | 初期士気 | 位置 | 向き |
|---|---|---|---|---|---|
| 21 | 不退転・被弾継続 | 100000 | 30 | (-20,0) | 180（撃ち手に背を向ける） |
| 22 | 不退転・被弾停止 | 100000 | 30 | (20,0) | 180 |
| 31 | 撃ち手・継続（敵） | 100000 | 100 | (-20,8) | 180（21を向く） |
| 32 | 撃ち手・停止（敵） | 100000 | 100 | (20,8) | 180（22を向く） |

- 標的が背を向けているのは、撃ち返させないため。撃ち返すと撃ち手が被弾で敗走し、被弾が途切れてしまう。これを初期条件の段階で防いでいる。2組の間隔は 40 で、隣の組は射程 10 の外にある。
- 流れ：開始時に 21 と 22 へ不退転を発令する → 効果中は被弾させ続ける → 効果が切れたら、32 の交戦規定を「射撃管制」にして 22 への被弾を止める（31 は撃ち続ける）→ 0.5 秒の猶予のあと 8 秒間、被弾と敗走を観測する。
- 判定の内容：
  - 効果中に敗走しないか（21・22）
  - 効果中に実際に被弾したか（21・22）
  - 終了後、21 は被弾が続き 22 は止まっているか（艦艇数の減り方で区別する）
  - 21 で通常の敗走が戻るか。効果中に士気が下限の 1 まで届かなかった場合は**未判定**にする
- 22 が終了後に敗走したかどうかは、参考としてログに残すだけで判定には使わない。

### 3.3 陣形変更（AI＝混在：軍団3隊は通常AI、遠くの敵は停止／軍団長AIあり）
| ID | 立場 | 軍団 | 艦艇数 | 位置 | 向き |
|---|---|---|---|---|---|
| 1 | 軍団長 | QA陣形軍団 | 10000 | (0,0) | 0 |
| 2 | 隷下A（保持する側） | QA陣形軍団 | 10000 | (-10,0) | 0 |
| 3 | 隷下B（対照） | QA陣形軍団 | 10000 | (10,0) | 0 |
| 11 | 敵（AI停止） | なし | 200000 | (0,90) | 180 |

- 軍団の合計 30000 に対し敵は 200000（比 0.15）。軍団AIは劣勢と判断して**方陣**を勧めるので、保持させる円陣とは必ず食い違う。
- 流れ（AI を有効にする前に命令を出す）：
  1. 隷下Aへ直接命令で円陣を指定し、保持させる
  2. 3.5 秒観測する。対照の隷下Bが軍団AIに変えられ、隷下Aは円陣のまま残るかを見る
  3. 隷下Aへ「軍団AI」名義の指定を当て、「保持により拒否」になるかを見る（命令の優先順位）
  4. 保持を解除し、隷下Aが軍団AIの陣形に戻るかを見る
  5. 観測中ずっと、軍団の所属と軍団旗艦が変わらないかも見る
- 保持解除後に戻らなかった場合でも、スキルポイントが陣形変更の費用に足りていなければ**未判定**にする。費用の扱いは仕様2の論点なので、ここでは不合格と区別した。仕様2は実装していない。

## 4. 共通の約束をどう満たしたか

- **準備完了まで一時停止**：使い捨てシーンに `PauseManager` を置く。Start で既定の倍速が当たるので1フレーム待ち、`Pause()` で止める。武装・FleetAI・軍団長AI は準備中は無効にしておき、開始時に明細どおり有効にする。これで停止中に1発目が撃たれて初期値が変わることを防ぐ。開始は `SetTimeScale`→`Resume`、観測が終わったら `Pause()`。`IsPauseConsistent`（PauseManager の停止状態と timeScale==0 が一致するか）を画面とログに出す。
- **一度に1つの実行だけ**：他のシーンに艦隊がすでに登録されていたら、準備を失敗にする（通常会戦と混ざるため）。Title から Play して使う想定。
- **対象外の要因を止める**：シーン名が Battle ではないので、会戦イベント・勝敗判定・BattleSetup は自動では作られない。すでにある `BattleEventManager`／`BattleManager`／`GalaxyView`（戦略の暦 Tick＝生産・外交・財政など）／`RingiDirector`／`FleetRingiDirector`／`BattleDirector` は `enabled=false` にし、終了時に元に戻す。統一クロックはこの QA では進めない。止めた内容は、画面左上のパネルとログの「隔離：」行に出す。
- **AI の区別**：プリセットの AI の使い方は、艦隊ごとの設定から自動で決まる（退却＝通常AI、不退転＝AI停止、陣形変更＝混在）。ログと画面に出す。
- **決定論的な初期化**：`Random.InitState(seed)` は組み立てが終わったあと、準備完了の直前に呼ぶ（初回だけ作られる常駐物が乱数を使っても、結果が変わらないように）。保証するのは初期スナップショットと、開始時点の乱数の状態までで、**フレーム単位の戦闘結果が一致するとは保証しない**。
- **元に戻す**：Begin の時点で timeScale・Random.state・台帳の Enabled/MinAbsDelta・アクティブシーンを控える。次のすべての場合に戻す：
  - 終了（メニュー／`End`）
  - 再試行（前の実行を End してから準備し直す）
  - 準備失敗（`Fail` から End）
  - 準備の途中で中断
  - シーンの破棄や Play 停止（`OnDestroy` の保険。Editor メニュー側でも ExitingPlayMode で End する）
  
  使い捨てシーンは実行ごとに名前を変えて閉じる。
- **ログ**：各行に `[run_id プリセット seed=… t=ゲーム内経過]` を付ける。run_id の形は `プリセット-s<seed>-n<通番>-<GUID8桁>`。設定・隔離・初期スナップショット・命令・判定・復元まで残す。
- **既存QAとの連携**：士気の原因台帳（`MoraleAuditLog`）は準備で有効にするが、**既存の記録は消さない**。開始時の件数を基準に「QA中に増えた件数とあふれた件数」を出す。陣形保持の変化記録（`FormationChangeRecorder`）は、メニューから準備したときに無ければ立ち上げ、終了時にメニューが立てたぶんだけ片付ける。どちらかが上限（400）を超えたら、結果ログの見出しに「★…を失った」と明記する（`BattleQaRunLog.LossNotice`）。

## 5. ChatGPT への実行手順と期待結果（★すべて未実行）

### 5.1 TestHarness（Unity なし・純ロジック）
```
cd TestHarness
dotnet test -v q
```
- 期待：ビルドエラー 0。既存の約 9,710 件に新しい EditMode 36 件（`BattleQaPresetCatalogTests` 10／`BattleQaSnapshotTests` 9／`BattleQaRunLogTests` 8／`BattleQaJudgeRulesTests` 9）が加わり、すべて合格すること。
- 絞って回す場合：`dotnet test -v q --filter "FullyQualifiedName~Ginei.Tests.BattleQa"`（期待 36 件合格）。

### 5.2 Unity のコンパイル（6アセンブリ）
- Unity のバッチモード（ユーザーの指定時間内で。共有画面は触らない）で、`error CS` が 0 件になること。特に `Ginei.Game`（`#if UNITY_EDITOR` 内の `FindObjectsByType<T>()`・`PauseManager` の API）、`Ginei.Editor`（`FormationObservationQaMenu.Truncated`／`FormationChangeRecorder`）、`Ginei.Tests.PlayMode`。
- **Player ビルドでもコンパイルが通ること**（`#if UNITY_EDITOR` で除外されている確認）。可能なら確認してほしい。

### 5.3 EditMode（Unity Test Runner）
例：`Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testFilter "Ginei.Tests.BattleQa" -testResults <out>.xml`
- 期待：36 件合格。

### 5.4 PlayMode（新規 10 件）
例：`-testPlatform PlayMode -testFilter "Ginei.Tests.ReproducibleBattleQaPlayModeTests" -testResults <out>.xml`

| 試験 | 期待 | 目安のゲーム時間 |
|---|---|---|
| Prepare_StaysPausedUntilStart_AndMatchesPreset | 準備完了・timeScale 0・PauseManager と一致・艦隊7件が登録・明細と一致・20フレーム経っても初期値が動かない | 数フレーム |
| SamePresetTwice_InitialValuesMatch_DifferentSeedIsDistinguished | 3プリセットそれぞれで、2回の初期化の差が 0・Random.state が一致・run_id が別。別 seed では差が「seed」の1件だけで、Random.state も違う | 数十フレーム |
| Retreat_CorpsRetreatMovesEveryMember_AndSparesBystanders | 総退却の判定・配下3隊・所属外3隊がすべて合格。盤面でも 1〜3 が撤退状態で変位 ≥1.0、4〜6 は撤退していない | 約 5〜10 秒 |
| MoraleLock_ActiveEndAfter_DistinguishesContinuedAndStoppedFire | 効果中の敗走なし×2・実被弾×2・終了後の被弾継続(21)・被弾停止(22)・21 の通常敗走の再開がすべて合格。両艦隊が生存 | 約 17 秒 |
| Formation_HoldBeatsCorpsAi_PriorityHolds_ReleaseReturnsToCorpsAi | 5つの判定がすべて合格。盤面で隷下Aは保持なし・最後に決めたのが軍団AI・陣形が対照と同じ | 約 5〜8 秒 |
| PrepareFailure_ForeignFleet_RestoresAndLeavesNothing | 準備失敗（理由に「既存の艦隊」）・Active が null・timeScale 1.5・Random.state・台帳が元に戻る・QA シーンが残らない | 数フレーム |
| AbortDuringPreparation_RestoresState | 準備中に中断しても元に戻る（「隔離の解除」が報告に出る） | 数フレーム |
| Retry_RebuildsSameInitialState_WithoutLeakingPreviousRun | 前の艦隊が破棄され、初期値と Random.state が1回目と一致し、保持が漏れず、一時停止に戻る | 約 2 秒 |
| End_AfterRunning_RestoresGlobalState | 実行中に終了しても timeScale・Random.state・台帳が戻り、艦隊 0・シーンなし | 約 1 秒 |
| SceneUnloadedWithoutEnd_StillRestores | 終了操作なしでシーンを閉じても元に戻る（「終了操作なしで破棄」がログに出る） | 数フレーム |

- **Inconclusive の扱い**：前提が崩れて判定が「未判定」になった試験は Inconclusive で止まる。例：不退転の効果中に士気が下限に届かない、退却で艦艇数比が 0.3 を割り原因を切り分けられない。**合格として数えないでほしい**。出力にはログ全文が添えられる。
- **回帰**：既存の PlayMode 32 件（`MoraleSourcePlayModeTests` 6／`MoraleLockRoutPlayModeTests` 9／`FleetFormationHoldPlayModeTests` 17）もあわせて回してほしい。今回これらのファイルと製品コードには手を入れていないので、結果が変わらないのが期待値。ただし**未実行のままでは合格ではない**。
- **順序の依存**：新しい試験は `UnityTearDown` でセッションを End し、timeScale と台帳の設定を戻す。一方、他のクラスの試験が艦隊を残したまま終わると、こちらの準備は「既存の艦隊」で失敗する。その場合は他クラスの後始末が漏れているサインとして報告してほしい。

### 5.5 メニューでの手動確認（Unity の画面操作が要る＝ユーザーの専有時間まで待つ）
1. Title シーンで Play する。
2. `Ginei/QA: 固定会戦 準備：退却（Play中）` を実行する → 画面左上のパネルで段階が「準備完了」になり、timeScale が 0、一時停止一致が True になることを確かめる。
3. `開始（準備完了後）` を実行する → 観測完了で自動的に一時停止する。
4. `結果ログを出力` を実行する → run_id・seed・判定・隔離・台帳の件数とあふれた件数・陣形保持記録の上限超過が Console に出る。
5. `再試行` → 準備完了 → 開始、で同じように動くこと。`終了（状態を戻す）` のあと timeScale が元の値に戻ること。
6. `seed を別値にする` をオンにして準備し、ログの seed が 7 になること。
7. 不退転・陣形変更でも同じ手順を繰り返す。Play 中に停止しても、次の Play に状態を持ち越さないこと。

## 6. できていないこと・注意点

- **ビルド・EditMode・PlayMode・TestHarness はどれも未実行**。コンパイルが通ることもまだ確認していない。
- 固定QAの盤面には、艦隊の絵（スプライト）を置いていない。見えるのは頭上ラベルと、左上の状態パネル（IMGUI）だけで、カメラも用意していない。
- `PauseManager` をQAシーンで生成するので、Start で自動生成されるポーズUI（Canvas）と、EventSystem が無ければその EventSystem がQAシーン内にでき、シーンと一緒に消える。`GameSettings.Instance` が初めて作られることもある（常駐シングルトンで、通常プレイでも自動生成されるもの）。
- 不退転の「21 で通常の敗走が戻る」は、効果中に士気が下限まで届くことが前提。届くかどうかは実際の被弾量で決まるので、届かなければ未判定（Inconclusive）になる。初期士気 30 と背面被弾で届きやすくはしてあるが、届く保証はない。
- 退却の変位しきい値（1.0）や観測時間（発令待ち最大 6 秒＋観測 4 秒）は、セッション側の public 調整値。実際に動かして短すぎたり長すぎたりしたら、調整が必要になる可能性がある。
- 運用規約にある `docs/catalog/components-catalog.md`・`core-modules-catalog.md`・CLAUDE.md 索引への追記は、今回の指示（追加ファイル・関連テスト・最小の接続変更に限る）の範囲外なので**していない**。必要なら別途指示してほしい。
- 前タスクから残っている項目（通常会戦での原因つき観測、旧ログの仮説、仕様2・配下艦艇の改善）はそのまま残している。
