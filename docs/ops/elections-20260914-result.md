# 国政選挙・地方選挙の整備 — 結果報告（elections-20260914 / a1）

- 基点：`integration/local-cloud-20260914` HEAD `e1749c93`
- 実施者：Claude（Read/Grep/Glob/Edit/Write のみ。コンパイル・試験・実画面は **未実行**＝ChatGPT 側で実施）
- 時刻：時計を読むツールが無いため記録しない（照合日時は ChatGPT が追記）
- commit/push、シーン/プレハブ、実セーブの編集：**なし**。既存の untracked は触っていない。

## 1. できたこと（要約）

| 仕様 | 状態 |
|---|---|
| 1 民主政体のみ国政選挙・既存ルール再利用 | 実装。`ElectoralSystemRules.IsElectoral`／`ElectionScheduleRules`／`ElectionRules.Aggregate`・`WinnerByPlurality`／`PartyOrganizationRules`／`LeadershipElectionRules`／`PoliticianRules.RegionVotes`／`CoalitionRules.NeedsCoalition`／`GovernmentRegistry` を使用。新しい集計エンジンは作っていない（新規は議席の端数配分 `SeatAllocationRules` のみ＝既存に無かった） |
| 2 下院4年全改選・上院6年/3年半数改選・支持率と議席の分離・整数議席・総数保存・端数/同点ID順・非改選保持・同年再処理なし | 実装＋EditMode 試験 |
| 3 地方＝星系知事選（4年）・民主のみ・現地の民意・実在の適格政治家のみ・再選可・候補ゼロは不成立＋理由＋再実施年・兼任禁止・占領で失職と日程再評価 | 実装＋EditMode 試験 |
| 4 開票→議席→首相就任（既存の宰相職へ）・過半なしは少数政権/組閣未成立を明示・知事→既存の総督職（星系スコープ）・年次任命で上書きしない・指揮権を与えない | 実装（Game 配線は未コンパイル） |
| 5 候補/党首不足の調査と初期編成 | 調査：政治家を生む窓口は `SeedDemoCivilService`（デモ）と主人公の政界転身だけ。党への配属窓口は `PartyOrganizationRules`。→ 無所属の政治家を党へ入党・党首選で党首を選ぶ処理を実装。新規戦役のデモに地方政治家を勢力あたり4名追加（既存人物の職種/官位は一切変更しない） |
| 6 保存 | `FactionStateSave.hasPolitics/politics` に政党・議席・日程・直近開票・政府・知事選を保存。旧セーブは null。読込は穴埋めのみで選挙しない。人物不在/死亡は空席＋理由 |
| 7 政治オブザーバ表示 | 国政/地方の見出し・次回日程・議席・首相・状態と理由・直近開票・星系名と知事・不成立理由・非民主の対象外を表示。既存のスクロールバー付き。クリック入口は既存の「観測→政治→政治」（新設なし） |
| 8 試験 | EditMode 4ファイル・PlayMode 1ファイルを追加（**未実行**） |

## 2. 設計判断

1. **選挙の年は統一クロックの暦**（`GalaxyView.ElectionYear()`＝`GameDate.FromSeconds(Clock)`）。調査で `campaignYear` は `SetupPersonnel` のたびに開始年（796）へ戻る（読込・会戦からの復帰でずれる）ことが分かった。保存した日程（例 SE804）と比較するので、画面の日付と一致するクロック暦を使う。`campaignYear` 自体は他システムへの影響が大きいので変更していない（残件）。
2. **首相＝既存の宰相職**（`civilOffices`・内政/国家）。別の首相職を足すと宰相と首相が並立するため、民主政で両院が構成済みの勢力では `RunCivilAppointmentTick` が官位の銓衡をせず、`MaintainElectedPremier`（首相欠缺時のみ現議席で再組閣）に切り替える。官位（従五位下）の解任ゲートは当選者に課さない。非民主・未構成の勢力は従来どおり。
3. **知事＝既存の総督職**（`governorOffices`・scopeKey=星系ID）。民主政で知事選の日程を組んだ勢力では `RunGovernorAppointmentTick` がその星系を銓衡しない。あわせて所有勢力以外の総督/知事職に残った在任者を外す（`DismissForeignGovernors`＝占領時の旧権限除去）。
4. **初回選挙**：両院が未構成なら、その年に両院の全議席を選び、日程をその年から組み直す（下院 +4年、上院 +3年で区分 A から半数改選）。既存 `PoliticsTickRulesTests` の日程（804/803）はそのまま。
5. **票**：国政＝星系ごとの人口×党支持×（安定なら与党↑・荒廃なら野党↑、±0.2）を `ElectionRules.Aggregate` で合算。地方＝`PoliticianRules.RegionVotes`（人口×人気×地盤）×党の地力（0.5＋支持）×政権評価（±0.3）×現職補正（±0.25）×思想一致（+0.2）。人気等は人物の人望・民望・悪名・情報から**その場で推定**（保存しない・人物を書き換えない）。
6. **資格**：生存・自由・在野でない・同勢力・`isPolitician`・文民（役職が文民専用のため）。拘束中は党籍を保つが首相/知事にはなれない。主人公の政治家提督（軍人）は対象外。
7. **組閣**：下院第一党（議席最多・同数は党ID小）の党首が適格なら首相。150/300 のようにちょうど半分は過半数（151）に届かず **少数政権**（連立は未実装と理由に明記）。党首がいなければ **組閣未成立**＝首相空席（捏造しない）。上院選挙では首相は変わらない。
8. **兼任**：首相は知事選に出られず、知事が首相になったらその年に辞職→補欠選挙。1人が複数星系の知事を持たない。
9. **二重処理防止**：議院は区分ごとの直近改選年、知事選は `lastAttemptYear`、組閣は変化時のみ通知。同じ年に Tick が二度走っても議席・役職・通知は変わらない。
10. **保存**：`PoliticsState` をそのまま `FactionStateSave.politics` に載せ、`hasPolitics` で有無を判定（JsonUtility は null のクラスを空の既定値で書くため）。読込時 `ElectionCycleRules.NormalizeLoaded` が年0の日程・定数0の議院・中身の無い政府を null に戻す。`GovernmentRegistry` は保存されない既存仕様なので、`SeedGovernment` の最後に `RestoreElectedOffices` で保存済みの首相/知事を役職へ戻す（人物不在・死亡等なら空席＋理由・知事は翌年に補欠選挙の日程）。
11. **非民主へ移行**：政府を「対象外」にして選出首相を宰相職から外し、知事は失職（台帳は対象外として残す）。以後は既存の任命経路。再び民主化したら知事選は1年猶予で再開。
12. **通知量**：知事選の不成立は勢力ごとに1通へまとめる。

## 3. 変更ファイル

新規（Core）
- `Assets/Scripts/Core/Government/ElectionResultTypes.cs`（＋.meta）— `CabinetStatus`/`LocalElectionStatus`/`PartySeatCount`/`ChamberSeats`/`PartyVoteResult`/`NationalElectionRecord`/`GovernmentFormation`/`LocalCandidateResult`/`LocalElectionState`/`RegionalElectorate`/`LocalConstituency`
- `Assets/Scripts/Core/Government/SeatAllocationRules.cs`（＋.meta）
- `Assets/Scripts/Core/Government/ElectionCycleRules.cs`（＋.meta）— `ElectionCycleParams`/`NationalYearOutcome` を含む
- `Assets/Scripts/Core/Government/LocalElectionRules.cs`（＋.meta）— `LocalElectionParams`/`LocalElectionEventKind`/`LocalElectionEvent` を含む

変更（Core）
- `Assets/Scripts/Core/Government/PoliticsState.cs` — 議席・政府・直近開票・知事選・`localsSeeded` を追加（保存対象に）
- `Assets/Scripts/Core/Government/PoliticsTickRules.cs` — 結果に `upperClassUp`（改選区分）を追加（既存の挙動は不変）
- `Assets/Scripts/Core/Society/CampaignSaveData.cs` — `FactionStateSave.hasPolitics/politics`
- `Assets/Scripts/Core/Society/CampaignSerializer.cs` — 書き出し/読み戻し＋穴埋め

変更（Game）
- `Assets/Scripts/Game/GalaxyView.Politics.cs` — `RunPoliticsTick` を選挙つきに置換、選挙配線（首相/知事の反映・通知・停止・復元・再組閣）
- `Assets/Scripts/Game/GalaxyView.Government.cs` — `GovernorOfficeOf`、`SeedGovernment` 末尾で `RestoreElectedOffices`、民主政の宰相/総督銓衡スキップ、`DismissForeignGovernors`
- `Assets/Scripts/Game/GalaxyView.Personnel.cs` — 新規戦役のデモに地方政治家（勢力×4名）
- `Assets/Scripts/Game/PoliticsObserverOverlay.cs` — 国政/地方選挙の表示、`DumpTextForTest`
- `Assets/Scripts/Game/GovernmentObserverOverlay.cs` — 首班を選挙結果（組閣済みなら）から表示
- `Assets/Scripts/Game/CoreStateInspector.cs` — glossary に6語

試験
- `Assets/Tests/EditMode/SeatAllocationRulesTests.cs`（＋.meta）7件
- `Assets/Tests/EditMode/ElectionCycleRulesTests.cs`（＋.meta）11件
- `Assets/Tests/EditMode/LocalElectionRulesTests.cs`（＋.meta）11件
- `Assets/Tests/EditMode/ElectionSaveRoundTripTests.cs`（＋.meta）4件
- `Assets/Tests/PlayMode/PoliticsObserverElectionPlayModeTests.cs`（＋.meta）1件

文書
- `docs/catalog/core-modules-catalog.md`（1行）、本報告、`docs/ops/claude-status.json`

## 4. 必要な試験（すべて未実行）

1. Unity コンパイル（Core/Game/テスト）。特に Game 層は `dotnet test` では検出できない。
2. `cd TestHarness && dotnet test -v q`（Core＋EditMode 全件。新規 33 件を含む）
3. Unity EditMode：`SeatAllocationRulesTests`／`ElectionCycleRulesTests`／`LocalElectionRulesTests`／`ElectionSaveRoundTripTests`／既存 `PoliticsTickRulesTests`／`CampaignSerializerTests`／`ElectionScheduleRulesTests`
4. Unity PlayMode：`PoliticsObserverElectionPlayModeTests.Overlay_ShowsNationalAndLocalElections`＋既存の回帰
- 試験が固定している主な値：初回下院 150/113/37（剰余同点は得票順）、上院 A/B 各 30/23/7、区分A改選後 15+30 / 30+23 / 15+7＝120、150/300 は少数政権、225/300 は単独過半、議席同数は党ID小、候補ゼロは `NoCandidateReason`＋翌年、荒廃星系では国政の野党候補が当選、占領で失職＋新規編入は翌年、非民主で停止→民主化で翌年再開、旧セーブ null、空の既定オブジェクトは未設定へ。

## 5. 実機確認の手順（保存しない確認は新規戦役で）

1. タイトル→新規戦役（同盟＝共和制、帝国＝君主制）で Strategy を開始。
2. 速度を上げて最初の年境界（SE797）を越える。通知に「同盟 初の下院選挙」「初の上院選挙（全議席）」「首相に …」「…知事選：…が当選」、候補が尽きた星系があれば「知事選 不成立 n星系 → 再実施 SE798」が出ること。帝国には選挙通知が出ないこと。
3. 上メニュー「観測→政治→政治」（または O）：同盟に「■ 国政選挙」（下院300/上院120の議席・次回 SE801/SE800・政府の首相と状態/理由・直近開票）、「■ 地方選挙」（星系名・知事名・任期・次回・直近得票・理由）。帝国は「対象外」。長い本文がスクロールバーで最後まで見えること。
4. Alt+G（政府）で同盟の首班が選挙の首相、要職任命に「同盟宰相＝首相」「同盟総督（星系ごと）＝知事」が出ること。翌年以降も年次の銓衡で置き換わらないこと。
5. SE800（上院区分A）・SE801（下院）を越え、上院は区分Aだけ変わり区分Bが残ること、下院選挙後に組閣通知が出ること。
6. 検証用の保存を使う場合のみ：F5→F9 で議席・首相・知事・次回日程が同じで、読込直後に選挙通知が出ないこと（**実ユーザーセーブは使わない**）。

## 6. 残件・未完了（全体完成とは言えない点）

- **未実行**：コンパイル・EditMode・PlayMode・実画面のすべて。
- **保存しない QA メニュー**（Editor 専用）と **GalaxyView を通す PlayMode 接続試験**（年次 Tick→役職反映→年次任命で上書きされない）は作っていない。役職反映と上書き防止は Game 配線のみで、自動試験は純ロジック部分まで。
- **連立交渉は未実装**（過半なしは少数政権と明示するだけ）。内閣不信任・解散総選挙（`ElectionScheduleRules.TryDissolve`）は未配線。上院は組閣に影響しない。
- **`campaignYear` が開始年へ戻る既存の不整合**は未修正（選挙はクロック暦を使うので影響しない。加齢・叙勲など他の年次処理は従来どおり `campaignYear`）。
- 旧セーブ（本変更前の新規戦役）は政治家が勢力あたり2名だけ（首相＋知事1）＝残りの星系は不成立と表示される。デモ政治家の追加は新規戦役のみ。
- 党の綱領（`platform`）はデモで空＝思想一致の補正はデモでは効かない。政党の結成・分裂・鞍替えは無い。
- 首相の職名は既存の「{勢力}宰相」のまま（表示上は政治オブザーバで「首相」）。
- 政治家が既存の省庁配属（`RunMinistryStaffingTick`）にも入る既存挙動は変えていない（首長職ではないので兼任禁止の対象外と判断）。
- 文民の死亡（老衰）が名簿で起きるかは未確認（首相/知事の欠缺処理は死亡・拘束・離反のいずれでも働く）。
- 新規追加のデモ人物で以降の人物ID（卒業生の採番）が勢力あたり4ずつずれる（新規戦役のみ）。
