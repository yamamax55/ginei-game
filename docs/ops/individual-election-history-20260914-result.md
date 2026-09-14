# 個人当選履歴と国政議員の対応付け — 結果報告（individual-election-history-20260914 / a1）

- 対象 Issue：#2768 #159 #165
- 基点：`integration/local-cloud-20260914` HEAD `ed1696ec`
- 実施者：Claude（Read/Grep/Glob/Edit/Write のみ）。**コンパイル・試験は実行していない**（ChatGPT 側で実行）。git 操作なし。

## 1. 設計

- **保存場所**：勢力の `PoliticsState.legislators`（`List<LegislatorRecord>`・人物IDで1件）。`FactionStateSave.politics` にそのまま乗る。人物DTO（`PersonSave`）は変更していない。
- **地方知事の当選は別履歴**：知事は既存の `LocalElectionState`。国政の記録には数えない。
- **`LegislatorRecord`**（新規）
  - 当選：`lowerWins`／`upperWins`（ゲーム内の開票ぶん）、`consecutiveWins`、`firstWinYear`／`lastWinYear`、`recordStartYear`、`lastLowerElectionId`／`lastUpperElectionId`
  - 開始前の経歴：`priorKnown`／`priorLowerWins`／`priorUpperWins`（シナリオが明示したときだけ）
  - 現在の資格：`seated`／`seatChamber`／`seatClass`（-1=下院、0=上院A、1=上院B）／`seatPartyId`／`seatElectedYear`／`seatElectionId`／`statusReason`
  - 累積：`TotalLowerWins`／`TotalUpperWins`／`TotalWins`（開始前の明示値を含む）
- **`LegislatorRosterRules`**（新規・唯一の窓口）
  - `AssignElection`：開票記録（`NationalElectionRecord`）を名簿へ反映する。
  - `Reconcile`：選挙の無い時点で議員資格を現況へ合わせる。
  - `SuspendAll`：非民主化で議席をすべて外す。
  - `SeedPriorHistory`：開始前の経歴を明示値で記録する。
  - `NormalizeLoaded`：読込データの穴埋め。
  - 照会：`NamedSeats`／`AggregateSeats`／`SeatedMembers`／`TotalWins`／`IsSeated`／`Find`
- **`PoliticsState`** に追加：`legislators`、`legislatorAssignedElectionIds`（反映済みの選挙ID・上限16）、`legislatorHistorySinceYear`（記録開始年）。
- **`ElectionCycleRules.NormalizeLoaded`** の末尾で `LegislatorRosterRules.NormalizeLoaded` を呼ぶ（1行追加のみ）。

### 候補配分の優先順位（党ごと・決定論）
1. 党首
2. 改選される議席の現職
3. 選挙力の高い順。選挙力＝`PoliticianRules.ElectoralStrength(ElectionCycleRules.ProfileOf(...))`（人望・民望・悪名・弁舌・党内基盤）で、出身星系が勢力の選挙区なら `HomeTurnoutBonus`(1.3) を掛ける
4. 人物IDの小さい順

- 党の処理順は党ID昇順。上院の全区分改選（初回）は区分A→区分Bの順に充てる。
- 同じ年に両院の選挙があるときは、Game 側で下院→上院の順に反映する。
- **候補から外す人**：もう一方の議院の議員、改選されない区分の議員、星系知事、非適格者（`IsEligiblePolitician`＝生存・自由・非在野・同勢力・文民政治家）、その党の党員でない人。
- **首相は下院議員を兼ねてよい**。首相と知事の兼任は、既存の知事側の処理で拒否する。
- 確定議席より候補が少なければ、残りは集計議席のまま。人物は生成しない。党別の確定議席（`ChamberSeats`）には一切書き込まない。

### 当選回数の数え方
- 数えるのは `AssignElection` で当選したときだけ。回数は選挙IDにつき1回。
- 二重に数えないための防御は2段：
  1. 勢力単位：反映済みIDのログ。
  2. 個人単位：最後に数えた選挙ID。ログが失われても二重に数えない。
- 次の場面では増えない：党の得票、党首選、組閣、役職任命、同じ年の再実行（`RunChamberElection` が null を返す）、読込。
- 連続当選：同じ議院・区分で改選された現職が再選されたら +1。それ以外の当選は 1。落選・失職・停止で 0。
- 落選の扱い：議席を外し、`statusReason` に理由を書く。累積の回数は残す。

### 議員資格の整理（`Reconcile`・当選回数は変えない）
- 死去、名簿に居ない、離反・在野、政治家（文民）でなくなった → 議席を外す
- 当選した党を離れた → 議席を外す（議席は党に帰属する扱い）
- 星系知事に就いた → 議員を辞職
- 拘束中は議席を保つ（党籍を保つ既存の `IsValidPartyMember` に揃えた）
- 外した議席は集計議席に戻る（補欠選挙は無い）

### 開始前の経歴
- 記録の無い人物は「不明／ゲーム内0」。表示は「記録開始SE◯◯（それ以前は不明）」。
- 年齢から実績を作らない。
- `SeedPriorHistory` は明示値があるときだけ使う API。**現在のシナリオ・デモに明示値の出所は無いので、どこからも呼んでいない**。
- 当選回数は、年功の素材として `TotalWins` で読めるようにしただけ。能力・階級・任命権・指揮権には接続していない。

## 2. Game 接続（`GalaxyView.Politics.cs`）
- **`RunPoliticsTick`**
  - `RunNationalYear` の後に、`AssignLegislators(lower)`→`AssignLegislators(upper)` を呼ぶ。地域票は `ElectorateOf` を1回だけ計算して使い回す。
  - 知事選の反映と `SyncElectedGovernors` の後に、`ReconcileLegislators(notify:true)` を呼ぶ。同じ年に知事に当選した議員は、ここで辞職になる。
- **`RefreshElectedGovernors`**（総督銓衡の時点）の末尾で `ReconcileLegislators(notify:true)`。政治 Tick の後に起きた死去・離反・知事就任で、議席を翌年まで残さない。
- **`RestoreElectedOffices`**（読込・再構築時）で `ReconcileLegislators(notify:false)`。当選回数は数えず、通知もしない。
- **`SuspendElections`** で `LegislatorRosterRules.SuspendAll`。履歴は残す。
- **通知**
  - 開票1回につき1通：「{勢力} 下院の当選議員（SE年）：人物 N名（新・再）・集計議席 M（・議席を失った現職）」。
  - 失職は人事の通知：「{勢力} {院}議員 {名} 失職（理由）」。
  - どちらも、既存試験が数える語（「選挙」「星系が勢力を離れた」）を含まない文面にした。
- **`CoreStateInspector`** の glossary に新フィールドの説明を追加。

## 3. 表示（`PoliticsObserverOverlay.cs`）
- 各議院の党別議席の行の下に、2つを追加した。
  - 内訳：「党名 人物 N・集計 M」
  - 実在議員の一覧：名前（党）、上院は区分、「当選X回（下・上）連続Y回 初当選SE 直近SE」、記録開始（または「シナリオ明示」）
- 実在議員が0人なら「実在の議員なし（全議席が集計議席）」と出す。
- 一覧は議院ごとに `maxLegislatorsShown`（public・既定40）で打ち切り、超えた人数を「ほか N名（表示上限）」と明示する。
- 既存の ScrollRect とスクロールバー（`UiScrollbars.Attach`）はそのまま。

## 4. 試験（未実行）

### EditMode `Assets/Tests/EditMode/LegislatorRosterRulesTests.cs`（14件）
小さい議院（下院2・上院4＝A2/B2）で固定する。

| 試験 | 固定すること |
|---|---|
| `FewCandidates_NamedNeverExceedSeats_RestIsAggregate_SeatTotalsUnchanged` | 既定の定数で候補3名：下院は人物3・集計297、上院は人物0・集計120。党別の確定議席は減らない |
| `Inaugural_LeaderThenScore_LowerFirst_UpperClassAThenB_OneChamberEach` | 下院{10,11}・上院A{12,13}・上院B{14,15}。全員1回、記録開始800、`priorKnown=false`。首相が下院議員 |
| `HomeRegion_BeatsSlightlyMorePopularCandidate` | 地盤の補正（人望60 対 人望50＋地盤 → 地盤の候補が当選） |
| `UpperClassA_Election_ReelectsClassA_KeepsClassBWithoutCounting` | 区分Aの現職は2回・連続2。非改選の区分Bは1回のまま |
| `LowerReelection_IncrementsWinsAndConsecutive` | 下院で再選 → 2回・連続2 |
| `SameElectionId_CountsOnce_EvenIfLogIsLostOrYearIsRerun` | 同じ開票IDの反映・ログ喪失・同年の再実行・党首選・再組閣のどれでも増えない |
| `Defeat_KeepsCumulativeHistory_ResetsConsecutive_AndPartyWithoutCandidatesIsAggregate` | 落選しても累積は保持・連続は0・理由あり。候補不足は集計議席 |
| `Reconcile_DeathAndDefectionVacate_CaptiveKeepsSeat_WinsUnchanged` | 死去・離反で議席を外し、拘束は保持。回数は不変。再実行で重ねて外さない |
| `Governor_VacatesSeat_AndIsNotCandidate_ShortfallBecomesAggregate` | 知事就任で辞職。次の改選で候補外になり、不足は集計議席 |
| `SuspendAll_ClearsSeats_KeepsHistory` | 非民主化で議席を外し、履歴は残る |
| `PriorHistory_OnlyWhenExplicit_AddsToTotals_WithoutSeat` | 開始前の経歴は明示時のみ。議席は与えない |
| `OldData_NullLists_NormalizeToEmpty_AllSeatsAggregate` | 旧データ（null のリスト）でも全議席が集計議席として動く |
| `Normalize_TrimsNamedAboveSeats_AndRemovesBrokenRecords` | 定数を超える実在議員は人物IDの大きい方から外す。null・重複の記録を除く |
| `JsonRoundTrip_PreservesRecords_AndLoadDoesNotCount` | `CampaignSerializer` 往復で全フィールド一致。読込後の同年再実行・同じIDの再反映で数えない |

### PlayMode `Assets/Tests/PlayMode/LegislatorHistoryPlayModeTests.cs`（1件）
`AnnualTick_SeatsNamedLegislators_RoundTrip_NoRecount_DeathVacates_OverlayShows`。既存の `ElectionGameIntegrationPlayModeTests` と同じ世界（SE797・同盟2党・政治家6名）で、実際の Game 年次経路を通す。
- 年次の政治 Tick → 宰相銓衡 → 総督銓衡の後の不変条件：党別の実在議員が議席以下、実在＋集計＝議席、1人1記録、知事は議員でない。下院の議席総数は300のまま。
- 首相は下院議員。知事は当選1回の履歴を持ち、理由に「知事」が入る。
- オーバーレイに「当選1回」「記録開始SE797」「集計」と首相の議員行が出る。
- 同じ年に再処理しても増えない。
- JSON 往復→再構築で記録と資格が一致し、読込時に名簿通知が出ない。読込後の同じ年の年次処理でも増えない。
- 首相以外の議員が死去すると、総督銓衡の時点で議席が外れ、履歴は残る。失職通知は1通。

### 期待する結果
- EditMode：新規14件が合格。既存の選挙系（`ElectionCycleRulesTests`／`ElectionSaveRoundTripTests`／`LocalElectionRulesTests` ほか）は無変化で合格。
- PlayMode：新規1件＋既存の `ElectionGameIntegrationPlayModeTests` 4件＋`PoliticsObserverElectionPlayModeTests`＋`RingiFlowPlayModeTests` が合格。
- 既存の PlayMode 試験が数える通知の語（「選挙」「初の下院選挙」「星系が勢力を離れた」）は、新しい通知に含めていない。

## 5. 変更ファイル
- 新規：`Assets/Scripts/Core/Government/LegislatorRecord.cs`（＋.meta）
- 新規：`Assets/Scripts/Core/Government/LegislatorRosterRules.cs`（＋.meta）
- 変更：`Assets/Scripts/Core/Government/PoliticsState.cs`（フィールド3つ）
- 変更：`Assets/Scripts/Core/Government/ElectionCycleRules.cs`（`NormalizeLoaded` に1行）
- 変更：`Assets/Scripts/Game/GalaxyView.Politics.cs`（配線）
- 変更：`Assets/Scripts/Game/PoliticsObserverOverlay.cs`（表示）
- 変更：`Assets/Scripts/Game/CoreStateInspector.cs`（glossary）
- 新規：`Assets/Tests/EditMode/LegislatorRosterRulesTests.cs`（＋.meta）
- 新規：`Assets/Tests/PlayMode/LegislatorHistoryPlayModeTests.cs`（＋.meta）
- 変更：`docs/catalog/core-modules-catalog.md`（1行）
- 本書と `docs/ops/claude-status.json`

シーン・プレハブ・実セーブ・設定・ChatGPT レビュー JSON・他の作業票は編集していない。

## 6. 未確認・残件
- **未実行**：Unity コンパイル（Core・Game・PlayMode）、EditMode/PlayMode、TestHarness `dotnet test`、実画面（Alt なし・O キーの政治オブザーバ）。
- **.meta の guid**：新規の .meta 4つは、Write したときのツール表示が「更新」だった。hook が先に作っていた可能性があり、手で書いた guid で上書きしている。Unity が別の guid で取り込み済みなら、そちらに合わせてほしい。
- **仕様判断の余地**
  - 知事選の候補は、議員を除外しない（既存の `LocalElectionRules` は不変）。知事に当選した議員はその場で辞職し、議席は次の改選まで集計議席に戻る（補欠選挙なし）。そのためデモ規模（政治家が少ない）では、実在の議員が減りやすい。
  - 同じ年に両院の選挙があると下院を先に充てるため、候補が少ないと上院が集計議席に偏る。
  - 党を離れた議員は議席を失う（議席は党に帰属する扱い）。無所属で残す方式は採っていない。
  - 離反者の履歴は、元の勢力の名簿に残る。移籍先で当選すると、移籍先に別の記録ができる（勢力をまたいだ通算はしない）。
  - 拘束中の議員は議席を保つ（党籍を保つ既存の方針に合わせた）。
- **シナリオの明示値**：`SeedPriorHistory` の API はあるが、`ScenarioData` などのデータ側には経歴の欄を足していない。今は全員「記録開始以前は不明」と表示される。
- **範囲外**：党三役・内閣・総裁選での当選回数の利用、補欠選挙、連立。
