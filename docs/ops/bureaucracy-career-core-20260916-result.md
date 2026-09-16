# 官僚任用・昇任・内閣人事局：Core第1段階（#141）結果

task_id: bureaucracy-career-core-20260916 / attempt_id: a2 / 基点 dfcde5d0
追補: task_id bureaucracy-career-capacity-fix-20260916 / attempt_id a2（定員判定の最小修正・§7）
追補: task_id bureaucracy-career-purity-fix-20260916 / attempt_id a1（状態非変更契約の最小修正・§8）

## 1. 変更ファイル

| ファイル | 内容 |
|---|---|
| `Assets/Scripts/Core/Government/CivilServiceState.cs`（新規・+meta） | `BureaucratGrade`（一般官僚/課長級/局長級/事務次官級）・`CivilServiceStatus`（在任/異動/昇任/降任/解任/退職）・`CivilServiceRecord`（省ID/人物ID/職位/就任年/退任年/任命者/理由/状態）・`CivilServiceState`（在任 `records`／退任済み `history`／`historyDropped`） |
| `Assets/Scripts/Core/Government/CivilServicePostRules.cs`（新規・+meta） | 共通の `Check`/`Execute` 入口（配属・異動・昇任・降任・解任）、`ApprovalAuthority`（内閣人事局の承認）、`GradeAuthority`、資格・定員の参照（`RequiredRank`/`RequiredMerit`/`RequiredTenureYears`/`SlotsFor`）、`NormalizeLoaded`/`SyncStaffing` |
| `Assets/Scripts/Core/Society/FactionState.cs` | `civilService` を1フィールド追加（politics と同じ扱い） |
| `Assets/Scripts/Core/Society/CampaignSaveData.cs` | `FactionStateSave` に `hasCivilService` / `civilService` を追加 |
| `Assets/Scripts/Core/Society/CampaignSerializer.cs` | 書き出しに2行、読み込みに旗つきの復元＋`NormalizeLoaded` |
| `Assets/Tests/EditMode/CivilServicePostRulesTests.cs`（新規・+meta） | 17件（下記 §4）＋§8 の3件＝20件 |
| `docs/catalog/core-modules-catalog.md` | 規約どおり1行追記 |

Data 層・Game 層・シーン・プレハブ・実セーブ・ChatGPT JSON は触っていない。git 操作なし。

## 2. 判断（なぜこう作ったか）

- **台帳は1つだけ**。省内職位は `CivilServiceState` にしか書かない。内閣の職（`CabinetPost`）・`GovernmentRegistry` の官職・軍の階級は**写さない**。人物が就いているかどうかは `FindServing` で台帳を引くだけ。
- **既存の仕組みを再利用**。資格の官位は `Person.courtRank`（`JapaneseCourtRankRules.Compare`）、考課は `Person.merit`（`OfficialMerit.AverageScore`）、省庁の配属は `Ministry.staffIds`（`MinistryRules.AssignOfficial`/`RemoveOfficial`＝単一所属もそちらの規則のまま）。新しい人物属性は足していない。
- **承認は `CabinetAppointmentRules` へ委譲**。事務次官級は `FormalPremier`（正式な首相）と一致するかだけを見る。局長級以下は `CabinetAppointmentRules.Authority(..., CabinetAction.所管決裁, ...)` の可否をそのまま使う＝所管大臣は可、委任のある副大臣は可（期限・委任した大臣の在任・職務執行内閣の失効もすべて既存の判定に乗る）、政務官・他省の大臣・党三役・閣外は不可。**委任の期限判定を新しく書いていない**。官僚本人（`actorId == personId`）は明示的に拒否。所管大臣が空席なら局長級以下は承認できない（承認権を台帳へ肩代わりさせない）。
- **`Check` と `Execute` は同一経路**（`Core(..., dryRun)`）。確認表示と実行時の再判定が食い違わない。
- **判定の順序を固定**：①職位の組み立て（1省1職位・当該省在籍・飛び級）→②承認権限→③本人の資格（官位/考課/最低在職年）→④空席定員。拒否は理由を返して何も変えない。
- **飛び級の扱い**：昇任・降任は隣の段だけ。配属は一般官僚から（途中の段への中途採用はしない）。異動は段を変えない（段を変えたいなら昇任・降任）。
- **資格の数値はパラメータから導出**（マジックナンバーなし）：在職年＝`minTenureYears × 段`（既定 0/3/6/9年）、考第＝`minMeritScore + 段 - 1`（既定 −/5.0/6.0/7.0）、官位＝一般官僚は無位可・課長級 正七位上・局長級 従五位下（五位の壁の上）・事務次官級 正五位下。定員は段ごと（既定 課長級4/局長級2/事務次官級1）＋省全体は既存の `Ministry.staffSlots`。**一般官僚の枠は省の配属定員そのもの**なので職位別の空席判定は課長級以上だけに問い、一般官僚は省の配属定員の判定に一本化する（§7）。
- **権限は増やさない**。`GradeAuthority` はどの段でも 艦隊作戦指揮・国庫支出・閣僚任免・所管政策決定・所管決裁 を**拒否**し、起案（政策提案）と事務の調整だけを許す。
- **保存**は politics と同じ旗つき方式（`hasCivilService`）。旧セーブは `civilService` を読まず null のまま＝空の台帳として扱う。省庁ツリーは保存されないため、復元後に `SyncStaffing` で台帳から `staffIds` を組み直せる（台帳に無い配属は外さない＝他の仕組みが入れた配属を壊さない）。
- **自動昇任は入れていない**（指示どおり）。年次 Tick も `GalaxyView` 配線も無し。

## 3. やっていないこと

- 年次の自動昇任・AI 人事・空席の自動補充。
- UI（観測層・操作画面）と `GalaxyView` の配線。台帳は生成されないので、現状ゲーム内では動かない（Core とテストのみ）。
- 在任者の失職整理（死亡・拘束・他勢力になった官僚を台帳から落とす `Reconcile` 相当）。今回は**任用時の資格判定だけ**で、時間経過の整理は次段階。

## 4. 追加した試験（EditMode / `CivilServicePostRulesTests`・17件）

正常系＝配属（台帳の全項目と `Ministry.staffIds` の同期）／昇任（段が1つ上がり旧記録が履歴へ）／降任（官位・考課を問わない）／異動（段を保ち所属省が移る・単一所属）／解任（記録終了・配属解除・定員が空く・入り直せる）。
拒否＝官位不足・考課不足と未評定・在職年不足／他省からの昇任解任・どこにも在籍しない／他勢力・死亡・拘束・在野・軍人・政治家・名簿外・存在しない省／1省1職位の重複と他省への二重配属・異動先が同じ／飛び級（配属・昇任・降任・据置）／段の定員と省の配属定員。
権限＝事務次官級は首相・局長級以下は所管大臣／首相は局長級以下を承認できない・大臣は事務次官級を承認できない（いずれも上申先つき）／政務官・党三役・他省の大臣・本人は不可／所管大臣が空席なら不可／副大臣は委任がある間だけ可・期限を過ぎると不可・委任があっても事務次官級は不可／`GradeAuthority` が全段で軍指揮権・国庫支出・閣僚任免・所管決裁を拒否。
その他＝確認と実行の可否・理由・上申先の一致と確認が状態を変えないこと／内閣と `GovernmentRegistry` を変えないこと／履歴上限と打切り件数／保存往復（台帳・履歴・`SyncStaffing`）と旧セーブが null のまま／`NormalizeLoaded`（null 安全・壊れた記録・重複在任・null 文字列）／台帳なしの拒否。

## 5. 未実行の試験（★未実行は合格ではない）

- **`dotnet test`（TestHarness 全件）＝未実行**。本作業票でテスト実行が禁止のため、新規17件を含め**一度も走らせていない**。新規 Core ファイルは csproj のグロブ（`Assets/Scripts/Core/**/*.cs`・`Assets/Tests/EditMode/*.cs`）で自動的に拾われるので csproj の変更は不要。
- **Unity でのコンパイル＝未実行**。`Ginei.Core` のみの追加で Game/Data 参照は増やしていない。
- 既存試験は緩めていない（既存ファイルの変更は `FactionState`／`CampaignSaveData`／`CampaignSerializer` へのフィールドと分岐の追加だけ）。

## 6. 次段階

1. **年次 Tick**：在任者の失職整理（死亡・拘束・他勢力）と、`BureaucracyCareerRules`（叙位・考課）の後に続く自動昇任・空席の自動補充。AI は必ず `Execute` を通す。
2. **配線**：`GalaxyView` で勢力ごとに `FactionState.civilService` を用意し、読込後に `SyncStaffing`、人事の結果を `NotificationCenter`（人事カテゴリ）へ。
3. **UI**：まず観測層（官僚 Alt+K の省庁ツリーに段と在任年を出す）、操作は決裁デスク／稟議経由で承認する形に昇格。

## 7. 追補（task_id `bureaucracy-career-capacity-fix-20260916` / attempt_id a2）：定員判定の最小修正

- **症状**：対象試験53件中1件が赤。`CivilServicePostRulesTests.Rejects_WhenGradeSlotsOrMinistryCapacityFull` の末尾（`staffSlots=1` の式部省が満員のときの一般官僚の配属）で、期待「配属定員がいっぱい」に対し実際は「式部省 職員 に空席がない（定員 1名）」を返していた。
- **原因**：④の職位別の空席判定が一般官僚にも先に効いていた。`SlotsFor` は一般官僚に対し `Ministry.staffSlots` を返す＝直後の省の配属定員の判定と**同じ枠を二度問う**形になり、先に来る職位別の理由が勝っていた（判定の中身は同じで、返す理由だけが違う）。
- **修正**：`CivilServicePostRules.Core` の④で、職位別の空席判定を **課長級以上だけ**に限り、一般官僚は省の配属定員（`ServingCount(st, ministryId) >= Ministry.staffSlots`）の判定に一本化した。変更は1ファイル・この分岐のみ。
- **緩めていないこと**：課長級／局長級／事務次官級の職位別定員は従来どおり（同試験の前半＝課長級の定員1が満員で「空席がない（定員 1名）」を返す分岐は不変）。省の配属定員の上限値も、①〜③の判定順序も、試験も変えていない（試験ファイルは未編集）。
- **副次**：一般官僚への**降任**は職位別の判定を通らなくなるが、本人はすでにその省の在任者として配属定員に数えられており、降任で省の在任者数は増えない＝定員の担保は変わらない。一般官僚への配属・異動は従来どおり配属定員で止まる。
- **試験**：本作業票でも実行禁止のため**未実行**（★未実行は合格ではない）。ChatGPT 側で `TestHarness` 全件を実行して確認する。

## 9. ChatGPTレビュー・検証結果

- Claude CLIの完了状態を確認：3作業とも exit 0、receipt一致、権限拒否なし、`editing_stopped=true`。
- 初回の対象試験53件では定員理由の重複により1件失敗。§7の最小修正後は53/53合格。
- レビューで、未初期化台帳に対する`Check`／拒否`Execute`の副作用を検出。§8の修正と3件の境界試験追加後、関連ロジック56/56合格。
- 隔離Unity 6.6.0f1 EditModeで`CivilServicePostRulesTests` 26/26合格。コンパイルエラー、失敗、スキップ、判定不能はいずれも0。
- 実画面の操作UI、年次自動昇任、失職整理、空席補充は今回の範囲外であり、次段階に残す。

## 8. 追補（task_id `bureaucracy-career-purity-fix-20260916` / attempt_id a1）：状態非変更契約の最小修正

- **症状（契約違反）**：`CivilServicePostRules.Core` の冒頭で `st.records` / `st.history` が null のとき空リストを代入していた。そのため **`Check`（状態を変えないと謳う確認）が台帳を書き換え**、また **入力不備で拒否される `Execute` も台帳を書き換え得た**（欠けた配列を作ってしまう）。「拒否のときは何も変えない」「確認は状態を変えない」という本クラスの契約と食い違っていた。
- **修正**：穴埋めを**実行の直前（⑤の冒頭）へ移した**。冒頭に残すのは `st == null` の拒否だけ。①〜④の判定と `dryRun` の戻りはすべてこの穴埋めより手前で返るため、**確認と拒否は台帳へ一切書き込まない**。成功時だけ `records`/`history` を用意してから従来どおり `End`/`Begin`/`AppendCapped` が書く。
  - 読み取り側はもとから null 安全（`FindServing`／`ServingCount` は `st.records == null` で 0・null を返す）ため、冒頭の穴埋めを外しても判定は一切変わらない。
  - `AppendCapped` の `history` null 穴埋め・`NormalizeLoaded` の整地はそのまま（別経路の安全網として残す）。
- **緩めていないこと**：判定の順序（①職位の組み立て→②承認権限→③資格→④定員）・理由の文言・上申先・`Check` と `Execute` の同一経路（`Core(..., dryRun)`）・定員・資格の数値はすべて不変。既存試験は1件も書き換えていない（クラスの要約コメントに1文を足しただけ）。
- **追加した試験（3件・§4 の17件＋3＝20件）**：
  - `Check_WithMissingLedgerLists_CreatesNothing`＝`records`/`history` を null にして、許可される確認と拒否される確認の両方を通し、**両方とも null のまま**・`historyDropped` 0・`Ministry.staffIds` 空であること（参照 API `FindServing`/`ServingCount` も落ちない）。
  - `RejectedExecute_WithMissingLedgerLists_CreatesNothing`＝権限外・飛び級・他勢力・存在しない省・在籍なし・本人承認の6分岐を null 台帳で実行し、**いずれも配列が作られない**こと。
  - `Execute_WithMissingLedgerLists_InitializesOnlyWhenItSucceeds`＝null 台帳で配属が成功すると `records` が用意されて1件書かれ `history` は空で用意される／`history` だけ null に戻して解任すると退任記録が1件積まれる（従来どおり動く）。
- **試験**：本作業票でも実行禁止のため**未実行**（★未実行は合格ではない）。ChatGPT 側で `TestHarness` 全件を実行して確認する。
