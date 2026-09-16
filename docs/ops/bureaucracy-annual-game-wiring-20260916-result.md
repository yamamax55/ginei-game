---
type: ops-result
task_id: bureaucracy-annual-game-wiring-20260916
attempt_id: a2
base: 83bdbb33
follows: bureaucracy-annual-core-20260916/a1, bureaucracy-staffing-consistency-fix-20260916/a1, bureaucracy-legacy-staff-move-guard-20260916/a1
---

# 官僚人事の年次処理を実ゲームへ接続 — 結果

Issue #141 の次段階。完成済みの `CivilServiceAnnualRules`（純Core）を **`GalaxyView` の年次進行へ接続**し、結果を**人事通知**へ流した。画面・操作UI・稟議化は行っていない（作業票どおり）。

## 1. 変更ファイル

| ファイル | 変更 |
|---|---|
| `Assets/Scripts/Game/GalaxyView.Personnel.cs` | 年次 Tick の末尾 1 か所：`RunMinistryStaffingTick()` → `RunCivilServiceAnnualTick()` に差し替え（他は未変更） |
| `Assets/Scripts/Game/GalaxyView.Government.cs` | 年次人事の配線本体（`RunCivilServiceAnnualTick`＋移行/同期/掃除/通知のヘルパ）／`SeedGovernment` の順序変更／`RunMinistryStaffingTick` を「台帳の無い勢力だけのシード」へ限定／QA hook 1 本 |
| `Assets/Scripts/Core/Government/CivilServicePostRules.cs` | 追加のみ：移行専用API `MigrateExistingStaff`（既存の判定・数値・順序・public シグネチャは一切不変） |
| `Assets/Tests/EditMode/CivilServicePostRulesTests.cs` | 追加のみ：移行APIの試験 4 件（既存は未編集） |
| `Assets/Tests/PlayMode/CivilServiceAnnualIntegrationPlayModeTests.cs`（＋meta） | 新規。Game 接続の結合試験 2 本 |
| `docs/catalog/core-modules-catalog.md` | #141 の行に「年次配線の呼出元」「移行専用API」を追記 |
| `docs/ops/bureaucracy-annual-game-wiring-20260916-result.md` | 本文書（新規） |
| `docs/ops/claude-status.json` | 本 task_id/attempt_id・`review_ready`・`editing_stopped=true` へ更新 |

軍・国庫・内閣（`CabinetState`）・`GovernmentRegistry`・シーン・プレハブ・実セーブ・`CampaignSaveManager`/`CampaignSerializer` は未変更。保存先は既存の `FactionState.civilService` のみ（二重保存先を作っていない）。

## 2. 確定仕様との対応

1. **年次の実行位置**＝`RunAnnualLifecycleTick` の末尾（`RunCourtAuthorityTick`→`RunBureaucracyTick`→`RunCivilAppointmentTick`→`RunGovernorAppointmentTick` の後）で `RunCivilServiceAnnualTick()` を1回。勢力ループは `DemoFactions` で各勢力1回、`CivilServiceAnnualRules.TickYear` が唯一の年次人事入口。年は**暦年 `ElectionYear()`**（統一クロック）を使う＝`campaignYear` と違いシーンを組み直しても在職年・委任期限とずれない。
2. **初期化と移行**＝`FactionState.civilService` が null の勢力だけ、その場で空の台帳を1回作る。続けて現在の `Ministry.staffIds` を**同じ省の一般官僚として**台帳へ写す（新設の `CivilServicePostRules.MigrateExistingStaff`）。別の省へは移さず、段も上げない。死亡・拘束・他勢力・在野・軍人・政治家・名簿消失は登録せず、理由を**同文でまとめて**集計し通知へ出す。登録できなかった人物の `staffIds` は触らない（＝状態の一部だけを壊さない）。
3. **台帳がある場合**＝`CivilServicePostRules.SyncStaffing(tree, st)` で台帳を正として `Ministry.staffIds` を復元してから回す。開幕/読込時も `SeedGovernment` が `RestoreCivilServiceStaffing()`（`NormalizeLoaded`＋`SyncStaffing`）で同じことをする＝在任・段・就任年・履歴を失わず、次の年次で重複配属しない（`CivilServiceAnnualRules` 側も `FindServing`/`OccupiedStaffCount` で二重配属を弾く）。
4. **シードの上書き防止**＝`RunMinistryStaffingTick`（旧・承認なしの全空席補充）は **台帳を持つ勢力を `continue` でスキップ**。省庁ツリーの冪等シード（`SeedMinistries`）は `SeedGovernment` の先頭へ出して常に実行する。台帳の無い旧状態の初回だけ、従来どおり現在の配属を作ってから（2. の）移行の材料になる。
5. **通知**＝`NotificationCategory.人事` に**勢力ごと1件**。`{勢力} 官僚の年次人事：退職n・昇任n・配属n・見送りn（既存の配属を台帳へ移行 n名）／{人物名（省名 職位）事由}…／見送り：{理由}×n…`。変更明細は最大 **5** 件（`MaxCivilServiceChangeNotices`）、見送りは同文をまとめて最大 **3** 種（`MaxCivilServiceSkipNotices`）。載せなかったぶんは「ほかn件」で丸める（黙って捨てない）。変化も見送りも無ければ通知しない。重要度は退職を含む年だけ `注意`、ほかは `情報`。
6. **無承認の空席補充を年次から外した**＝`RunMinistryStaffingTick` の呼び出しは `SeedGovernment`（開幕/読込）だけ。年次で空席を埋めるのは承認つきの `Execute` を通る `CivilServiceAnnualRules` のみ。死亡/捕虜の後始末（裁量人事ではない）は `PurgeUnavailableStaff` に切り出し、**台帳の在任者は触らない**（彼らは `RetireIfIneligible` が理由と履歴つきで退職させる）＝台帳と配属が食い違わない。行政寄与（`MinistryCentralBonus`/`SystemAdminBonus`）の経路は未変更。
7. **保存**＝既存 `FactionState.civilService` と `CampaignSerializer` のまま。`CampaignSaveManager` は未変更。
8. **Core の権限・資格・定員・1省1職位・飛び級禁止・決定論は不変**（追加した `MigrateExistingStaff` も既存の判定関数 `PersonProblem`/`FindServing`/`MinistryRules.Get` の再利用だけで、新しい数値・新しい権限を作らない）。

## 3. 追加した Core API（移行専用）

```
bool CivilServicePostRules.MigrateExistingStaff(
    List<Ministry> tree, Faction f, int ministryId, int personId,
    IList<Person> roster, int year, string reason, CivilServiceState st, out string problem)
```

- できるのは「**いまその省の `staffIds` にいる人物を、その省の一般官僚として台帳へ写す**」ことだけ。
- **できないこと**：新規採用（`staffIds` に居ない人物は拒否）・異動（台帳が別の省を指していれば拒否し省は移さない）・昇任（段は `一般官僚` 固定）。`Ministry.staffIds` には**書き込まない**。
- 承認を要さない代わりに、**現況に無いことは何もできない**＝`ApprovalAuthority` を迂回する抜け道にならない（権限者不在で `Execute` が使えない開幕でも、現況の書き写しだけは進む）。
- **冪等**：既に在任記録があれば false（同じ省なら `problem` も null＝異常ではない）。就けない人物は配属を壊さず理由を返す。記録には理由・就任年が残り、任命権者は `-1`（誰の任用でもない）。
- 定員は問わない（本人が既にその枠を使っており `OccupiedStaffCount` は増えないため）。

## 4. 判断（設計上の選択と理由）

- **年は `ElectionYear()`（暦年）**：台帳は保存されるので、シーン再構築で 0 に戻る `campaignYear` を使うと在職年が壊れる。内閣の委任期限・選挙日程と同じ尺にそろえた。
- **移行は「勝手な人事」にしない**：開幕の帝国のように内閣が無い勢力では承認権者が居らず `Execute` が通らない。そこで「現況の書き写し」だけを許す専用APIを足し、採用・異動・昇任は一切できない契約にした（作業票の許可どおり、Core への追加は最小の1本）。
- **政治家は台帳へ載せない**：`PersonProblem` の既存契約（政治家は職業官僚の職位に就けない）をそのまま使う。デモの旧シードは政治家も省へ配属していたため、移行では**理由をまとめて通知**するだけで `staffIds` からは外さない（既存表示・行政寄与を壊さない）。
- **`PurgeUnavailableStaff` で台帳の在任者を除外**：先に `staffIds` から消すと、同じ Tick 内で台帳だけ在任のままになる瞬間ができる。台帳側は `RetireIfIneligible` に一本化した。
- **通知は1勢力1件に圧縮**：承認権者が長期不在の勢力では見送りが毎年大量に出る。同文をまとめ、上限を超えたぶんは件数へ丸めた（`NotificationCenter` のリングバッファと同じ流儀）。
- **年に二度呼ばれた場合の追加ガードは置かなかった**：実行は `CalendarDispatcher.onYear`（年境界1回）で、兄弟の年次 Tick（`RunBureaucracyTick`/`RunCivilAppointmentTick`）と同じ流儀に合わせた。再実行しても台帳は重複しない（1省1職位・移行は冪等）が、昇任/配属の年の上限は呼び出し回数ぶん増える点だけ残件に記す。

## 5. 観測できる挙動の変化（レビュー時の注目点）

- **承認権者が居ない勢力の省庁は、年を追って空いていく**。年次の無承認補充を止めたのが原因で、意図した挙動（見送り理由が通知に出る）。内閣を置かない政体（帝国＝君主統帥など）では `局長級以下` の人事が承認されないため、`MinistryCentralBonus` はゆっくり下がる。これを埋めるのは次段階の操作化/稟議化の仕事。
- **読込後、省庁の配属は台帳の在任者だけになる**。旧シードが入れていた政治家は台帳に載らないため、セーブ→ロードで省から外れる（`GovernmentObserverOverlay` の「職業官僚 n/m名」が減る）。台帳を正とする以上の帰結。
- 太政官（最上位省）には大臣職が無いため、`局長級`以下は常に見送りになる（`事務次官級` は首相が承認者）。

## 6. 追加した試験（★未実行）

作業票によりテスト実行は禁止のため、**いずれも未実行**（＝合格ではない）。ChatGPT 側で実行して確認する。

| 試験 | 場所 | 見るところ |
|---|---|---|
| `Migrate_WritesLedgerForExistingStaff_WithoutApproval_AndIsIdempotent` | EditMode | 承認なしで写せる／段=一般官僚・就任年・任命権者-1・理由／`staffIds` 不変／2回目は何もしない／写した記録から通常の昇任が通る |
| `Migrate_CannotHire_OrMovePersonBetweenMinistries` | EditMode | 未配属は拒否（新規採用の抜け道なし）／別省在任は省を移さず拒否／存在しない省・台帳なしの理由 |
| `Migrate_RejectsIneligiblePeople_WithReasons_AndKeepsStaffing` | EditMode | 死亡・他勢力・軍人・政治家・名簿なしは登録せず理由を返し、配属は壊さない |
| `Migrate_WithMissingLedgerLists_InitializesOnlyWhenItSucceeds` | EditMode | 拒否では欠けた配列すら作らない／成功時だけ用意する |
| `AnnualTick_MigratesExistingStaff_PromotesWithApproval_AndSurvivesSaveLoad` | PlayMode | 初回の台帳初期化と移行（政治家除外・配属不変・通知1件）／同年の再処理で増えない／在職年3年で所管大臣の承認により4名昇任／JSON往復→再構築で段・履歴が戻り配属が台帳から復元／読込後の年次で重複しない |
| `AnnualTick_LeavesVacancyWithoutApprover_FillsApprovedMinistry_AndCapsNoticeDetails` | PlayMode | 大臣を解任した省は空きがあっても埋まらない／大臣のいる省へは承認つきで入省（任命権者＝大臣）／見送り理由が通知に出る／退職6件でも通知1件・明細5件＋「ほか1件」・重要度は注意 |

既存の `CivilServiceAnnualRulesTests`／`CivilServicePostRulesTests` は1件も緩めていない（追加のみ）。`GalaxyView` は PlayMode 試験でも無効な `GameObject` に載せて `Start` を走らせず、内閣は Core の共通入口で決め打ちに組む（選挙の乱数に依存しない）。

## 7. 未実行・残件

- **未実行**：`TestHarness` の `dotnet test` 全件（EditMode 追加4件を含む）／Unity の PlayMode 実行（追加2件）／Unity のコンパイル確認（`Ginei.Game` を変更しているため `Unity_ReadConsole` での確認が要る）。
- 次段階（本作業票の対象外）：
  - **操作化**＝プレイヤーが人事を起案・裁可する UI（`CivilServicePostRules.Check`/`Execute` を呼ぶだけ）と、`FleetRingiDirector` に倣った**稟議化**（`Petition` への載せ替え）。
  - **観測層**＝`BureaucracyObserverOverlay`（Alt+K）に段（一般官僚/課長級/局長級/事務次官級）と在任年数・履歴を表示する（現状は配属人数と文才のみ）。
  - 承認権者が長期不在の勢力向けの代替経路（例：君主統帥の政体では宰相/太政官が承認する）を Core の `ApprovalAuthority` に足すかの判断。
  - 年に二度呼ばれた場合の上限ガード（現状は `onYear` 1回に依存）。
