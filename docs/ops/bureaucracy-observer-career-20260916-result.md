# 官僚機構オブザーバへの省内職位表示（観測段階・#141）

- task_id: `bureaucracy-observer-career-20260916`
- attempt_id: `a1`
- 追補（レビュー修正）: `bureaucracy-observer-delegation-fix-20260916` / `a1`＝**委任を受けた副大臣が承認者として出ない不具合の修正**（本文書の「4-b」節と PlayMode 試験3・4）
- 基点: HEAD `cbe737ef`
- 位置づけ: **観測（read-only）のみ**。任命操作・稟議化は次段階。

## 変更ファイル

| ファイル | 内容 |
|---|---|
| `Assets/Scripts/Game/BureaucracyObserverOverlay.cs` | 省内職位（#141）の表示を追加。**追補＝承認表示に「委任を受けた副大臣」を加えた（4-b）**。既存の省庁ツリー・配属一覧・朝廷の権威・UI構築は不変 |
| `Assets/Tests/PlayMode/BureaucracyObserverCareerPlayModeTests.cs`（＋`.meta`） | 新規PlayMode試験5件（当初3件＋委任の修正2件） |
| `docs/ops/bureaucracy-observer-career-20260916-result.md` | 本文書（新規） |
| `docs/ops/claude-status.json` | 本 task_id/attempt_id・`review_ready`・`editing_stopped=true` へ更新 |

Core・`FactionState`・台帳・`Ministry`・`Cabinet`・人物・シーン・プレハブ・実セーブ・年次処理は**未変更**。

## 表示仕様（確定仕様との対応）

### 1. 配属者の行に職位（仕様1）
配属官僚の各行の末尾に在任記録を添える。

```
　・試験官僚31 (文才 30)　大蔵課長・在任 4年・官位 正七位上・考課 6.0（4回）
```

- 職位名は **`CivilServicePostRules.GradeTitle(record.ministryName, record.grade)`** をそのまま使う（独自名称を持たない）。
- 在任年 = `max(0, 現在年 − appointedYear)`（仕様6）。
- 官位 = `Person.courtRank`、考課 = `Person.merit.AverageScore`（小数1桁）と評定回数。`merit` が無い/未評定は **`未評定`** と明示。
- 記録の解決は `CivilServicePostRules.FindServing`（null 台帳でも安全）。

### 2. 台帳に無い配属者（仕様2）
```
　・試験政治家11 (文才 90)　台帳未登録（政治家（政治任用の職に就く）＝職業官僚の職位には就けない）
　・試験官僚32 (文才 29)　台帳未登録（台帳へ未移行（年次人事で移行される））
```
理由は **`CivilServicePostRules.PersonProblem`**（死亡・拘束・他勢力・在野・軍人・政治家・名簿に無い）をそのまま出し、問題が無ければ「台帳へ未移行」。人物が名簿で解決できない行も従来どおり `id N` を出したうえで理由を付ける＝隠さない。

### 3. 段別の在任/定員（仕様3）
省庁1つにつき1行。定員は **`CivilServicePostRules.SlotsFor(m, grade, CivilServicePostParams.Default)`**、在任数は `ServingCount`。
```
　職位 一般 3/16・課長 1/4・局長 0/2・次官 0/1
```
台帳が `null` のときはこの行を出さない（0 を実数のように見せない）。

### 4. 承認権者の状態（仕様4）
省庁1つにつき1行。段は「事務次官級」と「局長級以下」の2口。
```
　承認 次官級▸ 任命可能 上申先 試験政治家11　局長級以下▸ 承認者空席（承認権者（省#3 の所管大臣）が空席＝局長級 の人事を承認できない）
```
判定は **`CivilServicePostRules.ApprovalAuthority` のみ**を使い二重実装しない。手順（いずれも状態を変えない read-only 呼び出し）：

1. 「誰でもない操作者（actorId=-1）」で引く → `Petition` が返れば `petitionToId` が**上申先＝承認権者**（事務次官級＝首相／局長級以下＝所管大臣）。`Deny`（`canPetition=false`）なら **承認者空席**＋理由。
2. その承認権者本人で引き直し、`ok` なら **任命可能**、否なら **承認不可**＋理由（職務不能・内閣未整理など）。
3. **局長級以下だけ**、さらに所管の**副大臣**（`CabinetAppointmentRules.FindPost(cab, ministryId, 副大臣)` ＝`CabinetState.posts` から引く）の在任者IDで `ApprovalAuthority` を引き、`ok` なら同じ行に **委任承認**＋副大臣名＋委任範囲・期限（`CabinetPost.delegation`／`delegationEndYear`）を並べる。

### 4-b. 委任を受けた副大臣（本 attempt の修正・`bureaucracy-observer-delegation-fix-20260916`）

**直した不具合**：旧実装は `actorId=-1` の probe が返す `petitionToId`（＝常に所管大臣）**だけ**を本人で引き直していたため、有効な `CabinetDelegation.所管決裁` を持ち**現に承認できる副大臣が表示に出ず**、`CivilServicePostRules.ApprovalAuthority` の実際の判定と食い違っていた。

確定した表示：

```
　承認 次官級▸ 任命可能 上申先 試験政治家11　局長級以下▸ 任命可能 上申先 試験政治家12　委任承認 副大臣 試験政治家13（所管決裁・SE798まで）
```

- **事務次官級は従来どおり首相のみ**（`DelegatedApproverText` が段で早期に空文字を返す＝委任の対象外）。
- 大臣が `ok` なら「任命可能 上申先 大臣名」、**有効な委任を持つ副大臣も `ok` なら同じ行に「委任承認 副大臣名（範囲・期限）」を併記**＝どちらも隠さない。
- 大臣が承認できず副大臣だけが `ok` の場合は「任命可能 委任承認 …　大臣は承認不可 上申先 大臣名（理由）」＝可能な経路と不可の理由の両方を出す。
- **副大臣の発見は probe に依らない**（`actorId=-1` の Petition 先は常に大臣であり副大臣を指さない）。在席は `CabinetState.posts`＝`CabinetAppointmentRules.FindPost`、**最終可否は必ず本人IDの `ApprovalAuthority`** に委ねる＝委任の有効性（範囲・期限・委任した大臣の在任・職務執行内閣）を観測層で再実装しない。
- したがって**大臣が空席**なら `ApprovalAuthority` が段の入口で `Deny` を返すため従来どおり「承認者空席」、**委任が失効**していれば副大臣の引き直しが `ok` にならないため「委任承認」は出ない＝**可能と誤表示しない**。
- 追加の呼び出しは参照系（`FindPost`／`ApprovalAuthority`）のみ＝read-only は維持（Core・台帳・内閣・人物・`Ministry` は未変更）。

### 5. 人事履歴（仕様5）
勢力ごとに省庁ツリーの後へ、`CivilServiceState.history` を**新しい順**に最大 `historyRows`（Inspector 調整値・既定5）件。
```
  人事履歴 （新しい順・最大 5件）
  ・793年 昇任 大蔵省 職員 試験官僚31 （試験：昇任）
  ・795年 退職 大蔵省 職員 試験官僚32 （試験：在野）
  …ほか 1件（上限で捨てた 2件を除く）
```
超過は件数で示し、`historyDropped`（台帳が上限で捨てた件数）も併記＝黙って落とさない。履歴が空なら「人事履歴 なし」。

### 6. 現在年（仕様6）
`GalaxyView.ElectionYearForQa`（＝本番の `ElectionYear()`＝統一クロックの宇宙暦）を使う。**年次人事が `appointedYear` を書くときと同じ暦年**なので、在任年が再読込後もずれない。

### 7. 既存UI（仕様7）
`BuildUI`/`BuildContentPanel`/`BuildScrollBody`（`UiScrollbars.Attach` の縦スクロールバー）・`Alt+K`（`GameAction.官僚機構観測切替`）・`UIWindowStack` 登録（Esc で閉じる）・`WindowChrome.MakeNonModal`／タイトルバーは**一切触っていない**。横長化・画面比率別版は作っていない。

### 8. 観測専用（仕様8）
追加した経路は `FindServing`／`ServingCount`／`SlotsFor`／`GradeTitle`／`PersonProblem`／`ApprovalAuthority` の**参照系のみ**。`Execute`／`RetireIfIneligible`／`MigrateExistingStaff`／`SyncStaffing` など状態を動かす窓口は呼ばない。`ApprovalAuthority` は `Check`/`Execute` と違い台帳へ触れない（欠けた配列の穴埋めもしない）。

### 9. null・旧セーブ（仕様9）
`FactionState.civilService == null` の勢力は見出しの直下に
```
  人事台帳 未初期化 （#141 の年次人事が未実行／旧セーブ）＝省庁の配属だけを表示
```
を出し、**省庁ツリー・配属一覧・承認権者の表示はそのまま継続**する（各行は「台帳未登録（人事台帳 未初期化）」）。`politics`／`cabinet`／`history` が null でも Core 側が null 安全に `Deny` を返すため落ちない。

### その他
- 勢力見出しの直下に台帳の要約「在任 N名　履歴 M件　暦年 SE年」を出す。
- 画面末尾の凡例に「職位＝段別 在任/定員」「承認＝内閣人事局（事務次官級は首相・局長級以下は所管大臣／委任された副大臣）」の2行を追加。
- 追加した調整値は `historyRows`（`[Header("省内職位（#141）")]`＋`[Tooltip]`）1つのみ。**このクラスはシーン/プレハブに直列化されていない**（`RuntimeInitializeOnLoadMethod` でコード生成される）ため直列化トラップ（#2548）の影響を受けない。
- 試験入口 `public string BuildDumpForQa()` を追加（本番と同じ `BuildDump()` を呼ぶだけ）。

## 追加した PlayMode 試験（未実行）

`Assets/Tests/PlayMode/BureaucracyObserverCareerPlayModeTests.cs`（5件＝当初3件＋委任の修正で2件）。盤面は `CivilServiceAnnualIntegrationPlayModeTests` と同じ流儀＝`GalaxyView` を**無効な GameObject** に載せて `BindElectionQaWorld`＋`SeedGovernmentForQa`、内閣は `CabinetAppointmentRules` で決め打ち（選挙の乱数に依らない）。台帳は固定データを直に置く＝表示だけを問う。セーブ・シーンには触れない。

1. `Observer_ShowsPostTitleTenureRankMeritSlotsApproverAndHistory`
   - 職位名（`GradeTitle` と一致）・`在任 4年`・`官位 正七位上`・`考課 6.0（4回）`
   - 段別 在任/定員が `SlotsFor` と一致（一般/課長/局長/次官）
   - `任命可能`／`上申先`（首相・所管大臣）と、大臣を置かない太政官の `承認者空席`
   - `台帳未登録` とその理由（`政治家` / `台帳へ未移行`）
   - 履歴が新しい順で5件、超過は `…ほか 1件`、`上限で捨てた 2件`、最古の1件は出ない
   - **2回呼んでも本文が同一**で、台帳（records/history/historyDropped/段/就任年）と全省の `staffIds` が不変
2. `Observer_WithoutLedger_ShowsUninitialized_AndKeepsMinistryTree`
   - `人事台帳 未初期化` を出しつつ省庁名・配属官僚・承認権者の行は継続、段別の行は出さない
   - 観測後も `civilService` は null のまま、配属も不変
3. `Observer_ShowsViceMinister_WithValidApprovalDelegation`（委任の修正で追加）
   - 大臣を置く省の1つに **大臣（人物12）＋副大臣（人物13）** を置き、大臣本人が `CabinetAppointmentRules.Delegate` で **所管決裁を SE798 まで委任**
   - 前提として Core（`ApprovalAuthority`）が大臣・副大臣の双方に局長級の承認を認めていることを先に確認＝表示が Core を先回りしていないことの土台
   - 本文に `任命可能`／上申先の大臣名／`委任承認`／副大臣名／`SE798まで` が**すべて**出る（両方を隠さない）
   - 2回呼んでも本文が同一で、`CabinetPost.delegation`／`delegationEndYear`／`holderId` と全省の `staffIds` が不変（観測専用）
4. `Observer_HidesViceMinister_AfterDelegationExpired`（委任の修正で追加）
   - 同じ盤面で `StrategySession.Clock` を**委任期限の翌年**へ進める（内閣は整理しない＝委任の記録は残ったまま失効）
   - 前提として Core が失効した委任を認めないこと（`ApprovalAuthority(副大臣).ok == false`）を確認
   - 本文に `委任承認` も `SE798まで` も**出ない**一方、`任命可能`＋所管大臣名は残る＝委任だけが消える
   - 失効した委任の記録（範囲・期限）にも触れていない
5. `Overlay_KeepsScrollbarAndVisibilityToggle`
   - `Canvas/ObserverPanel` の構造、既定は非表示、`ScrollRect.vertical`＋`Scrollbar`（`verticalScrollbar` 配線）の存在、`SetVisible`/`Toggle` の開閉と表示中 `Update` が例外なく回ること

既存3件は**緩めていない**（判定・期待値とも未編集。追加は定数・ヘルパ・新規2件のみ）。

## 未実行の試験（★合格ではない）

本作業票でテスト実行は禁止のため、以下はすべて **未実行**。ChatGPT 側で実行して確認が要る。

| 対象 | 状態 | 備考 |
|---|---|---|
| `BureaucracyObserverCareerPlayModeTests`（5件＝当初3件＋委任2件） | 未実行 | PlayMode＝Unity が要る（TestHarness の対象外） |
| Unity コンパイル（`Ginei.Game`） | 未実行 | Game 層の .cs を変更＝`dotnet test` では検出できない。`Unity_ReadConsole` で確認が要る |
| TestHarness `dotnet test` 全件 | 未実行 | Core は未変更のため回帰は想定しないが、確認は要る |

## 残件・次段階

- **操作化（第2層）**：任命・異動・昇任・降任・解任の入口を作る場合は `CivilServicePostRules.Check`/`Execute` を唯一の窓口として呼び、稟議化は `RingiPipeline`/`WorkflowRules` の既存状態機械へ載せる（本作業票の対象外）。
- 表示の負荷：`BuildDump` は表示中のみ毎フレーム走り、省庁ごとに `ApprovalAuthority` を最大6回引く（次官級2回＋局長級2回＋副大臣の引き直し1回＋`FindPost`。委任の追補で局長級が1回増えた。`FormalPremier` は名簿走査）。省庁・勢力が増えたら暦境界キャッシュ化を検討（現状の規模では従来の配属一覧の走査と同程度）。
- 承認権者が長期不在の政体（大臣を置かない構成）では全省が「承認者空席」で埋まる。これは #141 の配線どおりの現況表示であり、代替承認経路を作るかは別途の判断。
- 履歴は退任記録（`history`）のみで、在任中の昇任の足跡は「現職の就任年」だけが見える。経歴（同一人物の履歴の追跡表示）は次段階の検討事項。
