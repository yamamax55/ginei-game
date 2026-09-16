---
type: ops-result
task_id: bureaucracy-annual-core-20260916
attempt_id: a1
base: 89d07aa5
follow_up: bureaucracy-staffing-consistency-fix-20260916/a1, bureaucracy-legacy-staff-move-guard-20260916/a1
---

# 官僚人事台帳の年次処理（純Core）— 結果

Issue #141 の次段階。**年次処理を純 Core で実装**した。UI・`GalaxyView` の配線は行っていない（作業票どおり）。

## 1. 変更ファイル

| ファイル | 変更 |
|---|---|
| `Assets/Scripts/Core/Government/CivilServiceAnnualRules.cs`（＋meta） | 新規。年次処理の唯一の入口 `TickYear`＋結果の純データ（`CivilServiceAnnualReport`/`CivilServiceAnnualEntry`/`CivilServiceAnnualKind`）＋調整値 `CivilServiceAnnualParams` |
| `Assets/Scripts/Core/Government/CivilServicePostRules.cs` | 追加のみ：失職の整理専用API `RetireIfIneligible`（既存の判定・数値・順序は一切不変） |
| `Assets/Tests/EditMode/CivilServiceAnnualRulesTests.cs`（＋meta） | 新規。純Core試験 14 件 |
| `docs/catalog/core-modules-catalog.md` | 波別ログに1行追記 |
| `docs/ops/bureaucracy-annual-core-20260916-result.md` | 本文書（新規） |
| `docs/ops/claude-status.json` | 本 task_id/attempt_id・`review_ready`・`editing_stopped=true` へ更新 |

既存試験は1件も書き換えていない（`CivilServicePostRulesTests` は未編集）。内閣・`GovernmentRegistry`・軍・国庫・実セーブ・シーン・プレハブには触れていない。

## 2. 実装した仕様（作業票の確定仕様との対応）

1. **順序**＝`TickYear` が「①失職整理 → ②上位の段から昇任 → ③一般官僚の空席補充」を1回ずつ回す。`HashSet<int> acted` で**同一人物の成功は1年に1回まで**。
2. **失職整理**＝`CivilServicePostRules.RetireIfIneligible`（新規・退職専用）。対象は既存 `PersonProblem` が返す事由（死亡・拘束/不在・他勢力・在野・軍人・政治家・名簿消失）だけ。履歴は `CivilServiceStatus.退職`、`Ministry.staffIds` からも外す。承認は要さないが、**`PersonProblem` が null の正常な在任者は false を返して何も変えない**＝この入口では免じられない（解任は従来どおり承認つきの `Execute`）。
3. **昇任**＝事務次官級→局長級→課長級の順に、各省の空席を**直下の段からのみ**埋める。候補は必ず既存 `Execute`（＝`Check` と同じ判定）を通し、承認者は事務次官級＝`CabinetAppointmentRules.FormalPremier`（正式首相）、局長級以下＝所管大臣（`FindPost(...大臣).holderId`）。**権限者が不在なら埋めずに見送り**、資格不足の候補は見送って次の候補へ（権限も資格も迂回しない）。
4. **昇任の候補順**＝考課平均↓ → 現職の在職年（`year - appointedYear`）↓ → 官位↓（`JapaneseCourtRankRules.Compare`） → 人物ID↑。乱数なし＝決定論。
5. **配属**＝昇任のあと、各省の総配属定員（`Ministry.staffSlots`）の空きぶんだけ、未配属の適格な文民（`PersonProblem` が null ＝非政治家・非軍人・同勢力・自由・存命）を一般官僚へ。所管大臣の承認つきで必ず `Execute` を通す。候補順は考課平均↓ → 官位↓ → 人物ID↑。
6. **結果**＝`CivilServiceAnnualReport`（`entries`＝退職/昇任/配属/見送り＋省ID・省名・人物ID・段・理由、件数 `retiredCount`/`promotedCount`/`assignedCount`/`skippedCount`、`noticesDropped`）。呼出側はこれだけで通知を組める。`st`/`tree`/`roster` が null・空でも例外にせず見送りとして返し、台帳を壊さない（`pol` が null なら整理だけ進み昇任・配属は見送り）。
7. 自動処理は新しい権限を生まない（承認は必ず既存 `ApprovalAuthority` 経由＝`Execute` の中で再判定される）。

## 3. 判断（設計上の選択と理由）

- **整理専用APIを `CivilServicePostRules` に置いた**：退職は台帳の書き込み（`End`＋`AppendCapped`＋`MinistryRules.RemoveOfficial`）であり、書き込みの窓口を年次側へ複製しないため。安全弁として「`PersonProblem` が null なら何もしない」を関数の契約にした（正常な在任者を自動処理で免じられない）。
- **昇任は上位の段から、1年1段**：先に上を埋めると直下に空席が生まれ、次の段の走査がその空席を見る＝繰り上がりが自然に連鎖する。`acted` と段順の両方で「同じ年に2段」を塞いだ。
- **省ごとの年の上限（`maxPromotionsPerMinistry`/`maxAssignmentsPerMinistry`）**：一度に全空席が埋まると人事が瞬時に飽和して面白くないうえ、通知が氾濫するため。既定は各2件。
- **見送りの明細に上限（`maxNotices`・既定60）**：承認者が長期不在だと見送りが毎年大量に出るため。超過ぶんは `noticesDropped` に数える＝黙って捨てない（`NotificationCenter` のリングバッファと同じ流儀）。件数 `skippedCount` は常に正確。
- **走査順は省ID昇順に固定**（`SortedMinistries`）＝保存順・挿入順に結果が左右されない。
- **配属の候補プールは勢力で1つだけ作る**（省ごとに作り直さない）＝終盤ラグ規律（N² を増やさない）。配属の拒否は本人固有の資格ではなく省側の事情（定員・承認）なので、その省の走査を打ち切る。
- **`AutoOrganize` 的な一括APIは作らなかった**：入口は `TickYear` 1本。`GalaxyView` の年次Tickから呼ぶだけで済む形にしてある（配線は次段階）。

## 4. 追加した試験（14件・純Core）

`Assets/Tests/EditMode/CivilServiceAnnualRulesTests.cs`

1. `Retire_RemovesOnlyIneligibleServingOfficials` — 7事由をまとめて整理し、正常な在任者だけ残る／配属・履歴・理由が一致
2. `RetireIfIneligible_KeepsHealthyOfficials_AndIsNullSafe` — 正常な在任者は退職させられない／未在職・台帳null・欠けた配列で何も作らない／死亡なら退職して配属から外れる
3. `Promote_FillsFromTopDown_AndNoOneRisesTwiceInAYear` — 局長級→事務次官級→課長級→局長級→一般官僚→課長級の繰り上がり／履歴は1人1件
4. `Promote_StopsAtPerMinistryCap` — 省ごとの年の上限
5. `Promote_OrdersByMeritThenTenureThenRankThenId` — 候補順の4段の優先を全部固定
6. `Promote_SkipsUnqualifiedCandidate_AndTakesTheNextOne` — 在職年不足は見送って次の候補へ
7. `Promote_SkipsWhenApproverIsAbsent` — 首相不在／所管大臣空席なら埋めず、台帳も動かない
8. `Assign_FillsVacantSlots_WithEligibleCiviliansInOrder` — 空席ぶんだけ入省・候補順・軍人/他勢力/在野/政治家は入らない
9. `Assign_RespectsCapApproverAndCapacity` — 省ごとの上限／大臣空席の省は埋めない／定員が埋まった省は動かない
10. `Assign_DoesNotTouchAlreadyServingOfficials` — 在任者は候補に入らない（1省1職位）
11. `TickYear_RunsRetireThenPromoteThenAssign_Deterministically` — 同じ入力で同じ順序・件数、処理順の固定
12. `Notices_AreCapped_AndDroppedCountsAreKept` — 見送りの明細上限と打切り件数（`skippedCount` は正確なまま）
13. `TickYear_IsNullSafe_AndChangesNothingWithoutInputs` — 台帳/省庁/名簿 null は見送りで返し台帳不変、政体 null は整理だけ進む
14. `TickYear_DoesNotTouchCabinetOrGovernmentRegistry` — 昇任しても内閣の職・履歴は動かない

## 5. 未実行（★重要）

**本作業票ではテスト実行が禁止のため、下記はいずれも未実行＝合格ではない。ChatGPT 側で実行して確認する。**

- `TestHarness` の `dotnet test`（新規 `CivilServiceAnnualRulesTests` を含む全件）
- Unity でのコンパイル確認（新規 Core 1本＋EditMode 試験1本。csproj の変更は不要＝既存グロブが拾う想定）

## 6. 残件（次段階）

- `GalaxyView` の年次Tick（`CalendarDispatcher` の onYear）から `CivilServiceAnnualRules.TickYear` を呼ぶ配線と、`CivilServiceAnnualReport` → `NotificationCenter`（カテゴリ＝人事）への通知。
- 観測層（`BureaucracyObserverOverlay`＝Alt+K）への年次結果の表示。
- 第2層「操作化」＝プレイヤー勢力の昇任・配属を稟議（`RingiPipeline`）へ載せるか、自動のままにするかの判断。

## ChatGPTレビュー・最終検証

- 初回実装後のレビューで、既存`Ministry.staffIds`だけにいる配属者を未配属と誤認する問題、実配属と台帳の定員集計差、配属失敗時の原子性を検出し修正した。
- 追加レビューで、台帳未移行の他省配属者を`配属`で移せる抜け道を検出し、同一省への初回台帳登録だけを許可して、他省への移動を台帳上の`異動`へ限定した。
- 関連Core 71/71合格。
- 隔離Unity 6.6.0f1 EditMode 49/49合格。失敗・スキップ・判定不能・コンパイルエラーはいずれも0。
- 実ゲームの年次Tick配線、通知、観測UI、稟議による操作化は次段階に残す。

---

# 追補：既存 `Ministry.staffIds` との整合（task_id `bureaucracy-staffing-consistency-fix-20260916` / attempt `a1`）

統合前の最小修正。年次官僚人事と**台帳へ移していない既存の配属**（`Ministry.staffIds`）が食い違いうる3点を塞いだ。

## A. 直した問題

| # | 問題 | 直し方 |
|---|---|---|
| 1 | 省全体の定員判定が `CivilServiceState` の在任数だけを見るため、既存 `staffIds` で満員でも配属・異動を許していた | 省全体の使用枠を `staffIds` と台帳在任者の**重複なし集合**で数える新API `CivilServicePostRules.OccupiedStaffCount(m, st, excludePersonId)` を導入し、④の定員判定をこれに差し替え |
| 2 | `CivilServiceAnnualRules.UnassignedCandidates` が台帳だけを見るため、既存 `staffIds` にいるが台帳へ未移行の人物を未配属と誤認し、別省へ動かしうる | 候補から「どこかの省の `staffIds` に既にいる人物」を除く（新API `CivilServicePostRules.FindStaffedMinistryId(tree, personId)`）。`Assign` の空き計算も `OccupiedStaffCount` へ |
| 3 | `Execute` が `MinistryRules.AssignOfficial` の失敗を無視して先に台帳を書き換えるため、予期しない不一致時に台帳と配属がずれうる | 台帳を書く前に④で書き込み可否（`CanAssignStaff`）を確認し、書けなければ拒否＝**両方とも変更しない**。⑤で配属を動かすのは**配属・異動のときだけ** |

## B. 仕様（確定した挙動）

- **省全体の枠**＝`|staffIds ∪ 台帳の在任者|`（本人ぶんは除いて数える＝二重に数えない）。**どちら側だけで満員でも定員を超えない**。段別の定員（課長級・局長級・事務次官級）は段の概念が `staffIds` に無いため従来どおり台帳のみで数える。
- **配属・異動**＝`AssignOfficial` が成功できることを確かめてから台帳を確定する（失敗時は台帳も `staffIds` も不変。欠けた `records`/`history` も作らない）。`Check` も同じ判定を同じ順序で通る＝表示と実行が食い違わない。
- **昇任・降任**＝同じ省の中なので `staffIds` に触れない（他人を押し出さない・本人を入れ直さない）。台帳にあって `staffIds` に無い欠落の復元は従来どおり読込後の `SyncStaffing` の責務。
- **年次処理**＝既存の配属者を**黙って別省へ移さない**（候補から除外）。移動は台帳上の在任者に対する明示の異動（`Execute` の `異動`）だけ。

## C. 変更ファイル（追補ぶん）

| ファイル | 変更 |
|---|---|
| `Assets/Scripts/Core/Government/CivilServicePostRules.cs` | 追加：`OccupiedStaffCount`／`FindStaffedMinistryId`（public）・`CanAssignStaff`（private）。変更：④の省全体定員判定・⑤で配属を動かす条件（joining のみ）。段別定員・資格・承認・履歴・理由文言は不変 |
| `Assets/Scripts/Core/Government/CivilServiceAnnualRules.cs` | `Assign` の空き計算を `OccupiedStaffCount` へ・`UnassignedCandidates` に `tree` を渡して既存配属者を除外 |
| `Assets/Tests/EditMode/CivilServicePostRulesTests.cs` | 追加5件（既存試験は未変更） |
| `Assets/Tests/EditMode/CivilServiceAnnualRulesTests.cs` | 追加2件（既存試験は未変更） |

## D. 追加した試験（7件・純Core）

`CivilServicePostRulesTests`
1. `MinistryCapacity_CountsExistingStaffingAndLedgerWithoutDoubleCounting` — ①既存 `staffIds` だけで満員 ②台帳の在任者だけで満員 ③両方に同じ人物がいても1枠（＋`excludePersonId`・null 安全）
2. `Transfer_RejectedWhenTargetIsFullWithExistingStaffingOnly` — 既存配属だけで満員の省へは異動できず、台帳も配属も動かない（既存配属者を押し出さない）
3. `Assign_IsAtomic_WhenMinistryStaffingCannotAcceptThePerson` — `staffIds` へ書き込めない不整合時は台帳を確定しない（欠けた配列も作らない・`Check` も同じ結論）
4. `PromotionAndDemotion_DoNotMoveExistingStaffing` — 昇任・降任は `staffIds` を動かさず、欠落は空きができてから `SyncStaffing` で揃う
5. `FindStaffedMinistryId_ReportsExistingStaffing` — 台帳に無い配属も見える・null 安全

`CivilServiceAnnualRulesTests`

6. `Assign_ExcludesPeopleAlreadyInMinistryStaffing` — 考課最高でも既存配属者は候補外、元の省に残る（黙って移さない）
7. `Assign_SkipsMinistryFilledByExistingStaffingAlone` — 既存配属だけで満員の省を空と見ない

## E. 未実行（★重要）

**本作業票でもテスト実行は禁止のため未実行＝合格ではない。** `TestHarness` の `dotnet test` 全件と Unity コンパイル確認は ChatGPT 側で行う。

## F. 範囲外として手を付けなかった点

- ~~`Execute` の **`配属` そのもの**は、他省の `staffIds` にだけ居る（台帳に無い）人物を対象にしても従来どおり通す＝`AssignOfficial` の単一所属で元の省から外れる。これは UI／AI が人物と省を名指しで渡す**明示の操作**であり、ここを拒否すると台帳に無い既存配属者が `異動`（台帳の在任が前提）も使えず行き場を失うため。自動で暗黙に移していたのは年次処理の候補プールだけで、そこは #2 で塞いだ。~~ → **下の追補（`bureaucracy-legacy-staff-move-guard-20260916`）で閉じた**。
- `GalaxyView` 配線・観測層・操作化（稟議）は引き続き次段階。

---

# 追補2：`配属` による省またぎの抜け道を閉じる（task_id `bureaucracy-legacy-staff-move-guard-20260916` / attempt `a1`）

前の追補（`bureaucracy-staffing-consistency-fix-20260916`）が **F の1点目として意図的に残した**「台帳未移行の既存配属者を `配属` で別の省へ移せる」抜け道を塞いだ。

## A2. 直した問題

`CivilServicePostRules.Execute/Check` の `CivilServiceAction.配属` は、台帳に在任記録が無ければ通るため、**別の省の `Ministry.staffIds` にだけ居る人物**でも受け付け、⑤の `MinistryRules.AssignOfficial`（単一所属）が元の省から黙って外していた。省を移す経路が `異動` と `配属` の二つあるのは、承認・履歴（`CivilServiceStatus.異動`）を伴わない移動を許すことになる。

## B2. 仕様（確定した挙動）

- **`配属` は、対象の人物が対象省とは別の省の `staffIds` に既に居るなら拒否する**。理由は既存配属先の省名つき（「既に 式部省 に配属されている（台帳に無い既存の配属）＝配属では別の省へ移せない（当該省の台帳へ登録してから異動）」）。**台帳も両省の `staffIds` も一切変更しない**（欠けた `records`/`history` も作らない）。
- **同じ省の `staffIds` に既に居る人物の `配属` は通る**（移動ではない＝その省の台帳へ初めて載せる登録）。`staffIds` は重複しない（`AssignOfficial` が既在席で false を返すのは④の `CanAssignStaff` で織り込み済み）。
- **省を移すのは台帳上の在任者に対する `CivilServiceAction.異動` だけ**。行き場の懸念（前追補 F の1点目）は、まず**同じ省へ `配属`（＝台帳への初回登録）→ その後 `異動`** で解消する＝どちらも承認と履歴を通る。
- 判定位置は①（職位の組み立て）の `配属` 分岐＝**在任重複・飛び級の判定の直後、②承認より前**。`Check` と `Execute` は従来どおり同じ判定を同じ順序で通る。
- **不変**：年次処理の候補除外（`UnassignedCandidates`）・定員集合（`OccupiedStaffCount`）・段別定員・資格（官位／考課／在職年）・内閣人事局の承認・履歴・既存試験の期待値。

## C2. 変更ファイル（追補2ぶん）

| ファイル | 変更 |
|---|---|
| `Assets/Scripts/Core/Government/CivilServicePostRules.cs` | ①の `配属` 分岐に既存配属先のガードを追加（既存 public API `FindStaffedMinistryId` を再利用＝新APIなし）＋クラスのドキュメントコメントに確定仕様を明記 |
| `Assets/Tests/EditMode/CivilServicePostRulesTests.cs` | 追加2件（既存試験は未変更） |

## D2. 追加した試験（2件・純Core）

1. `Assign_RejectedWhenPersonIsAlreadyStaffedInAnotherMinistry` — 式部省の既存配属者を兵部省へ `配属` すると拒否。理由に既存配属先と「異動」を含み、台帳（`records`/`history` は null のまま）も両省の `staffIds` も動かない。`Check` も同じ結論。
2. `Assign_AllowedWhenPersonIsAlreadyStaffedInTheSameMinistry` — 同じ省の既存配属者は `配属` で台帳へ初回登録でき、`staffIds` は重複しない。登録後は `異動` で式部省へ移せる（従来どおり）。

## E2. 未実行（★重要）

**本作業票でもテスト実行は禁止のため未実行＝合格ではない。** `TestHarness` の `dotnet test` 全件と Unity コンパイル確認は ChatGPT 側で行う。既存 public シグネチャの変更はなく（追加もなし）、変更は Core 1ファイル＋EditMode 試験1ファイルのみ＝csproj・スタブの同期は不要。

## F2. 範囲外として手を付けなかった点

- 年次処理（`CivilServiceAnnualRules`）は未変更。候補プールが既に「どこかの `staffIds` に居る人物」を除くため、年次経路から本ガードに当たることはない（当たれば見送りとして記録される）。
- `GalaxyView` 配線・観測層・操作化（稟議）は引き続き次段階。
