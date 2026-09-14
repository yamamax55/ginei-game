# 黙殺稟議の台帳容量管理 結果（petition-dormant-retention-20260914 / a1）

基点：0eaa661b。git 操作・試験実行・実セーブ・シーン/プレハブ変更なし。

## 原因
- `PetitionLedger.Prune` は `WorkflowRules.IsResolved`（執行済/却下）だけを削除対象にしていた。
- 黙殺は `IsActive` でも `IsResolved` でもない中間状態のため、容量を超えても一切整理されず、黙殺が溜まると台帳が無制限に伸びた。
- あわせて、採番位置（seq）と打切り件数（droppedCount）は保存されていなかった。`ReadPetitions` は残っている id の最大値と決裁カードの id からしか採番を戻せない。そのため、容量で落ちた「最新 id」がカードに紐づいていないと、ロード後にその id が使い回されうる。今回の変更で新しい稟議も打切り対象になり得るため、この問題が表に出る。

## 変更
| ファイル | 内容 |
|---|---|
| `Assets/Scripts/Core/Government/WorkflowRules.cs` | `IsPrunableDormant(pet)` を追加（黙殺かつ `vindicated=false`）。`IsActive`/`IsResolved` の意味は変えていない（黙殺は終端のまま扱わない）。 |
| `Assets/Scripts/Core/Economy/PetitionLedger.cs` | `Prune` を2段にした。①古い順に決着済み（執行済/却下）を落とす ②それでも超過していれば、古い順に `IsPrunableDormant` を落とす。起案/伝播中/決裁待ち/承認/再浮上と、`vindicated=true` の黙殺は削除しない。保護対象だけで超過する場合は超過を許す。`droppedCount` は1件ごとに加算し、状態・採番は変えない。保存用に `LastIssuedId`（採番済み最大 id）を追加。 |
| `Assets/Scripts/Core/Society/CampaignSaveData.cs` | `petitionsLastIssuedId`/`petitionsDroppedCount`/`fleetPetitionsLastIssuedId`/`fleetPetitionsDroppedCount` を追加（旧セーブは 0＝従来動作）。 |
| `Assets/Scripts/Core/Society/CampaignSerializer.cs` | `WritePetitionLedgerMeta`/`ReadPetitionLedgerMeta` を追加。読み込みは `ReserveId(lastIssuedId)` と、打切り件数の加算（読み込み中に容量で落ちた分＋保存値）。容量は設定値なので保存しない。null 安全。 |
| `Assets/Scripts/Data/CampaignSaveManager.cs` | 税・編制の両台帳について、上記メタの書き込み/読み込みを各2行ずつ配線（`ReadPetitions` の直後）。 |
| `Assets/Tests/EditMode/PetitionLedgerTests.cs` | 下記の試験11件を追加。既存試験は変更・緩和していない。 |

### 追加試験（EditMode・PetitionLedgerTests）
- `IsPrunableDormant_OnlyUnvindicatedDormant_IsResolvedUnchanged`：対象判定の全状態分岐。黙殺は `IsResolved`/`IsActive` がともに false のまま。
- `Prune_KeepsDormant_WithinCapacity`：容量内の黙殺は保持する。
- `Prune_ManyDormant_BoundedToCapacity_OldestFirst`：黙殺100件・容量4で4件に収まり、打切り96件、古い順に落ち、状態は変わらない。
- `Prune_DropsResolvedBeforeDormant_EvenIfDormantIsOlder`：黙殺の方が古くても決着済みを先に落とす。決着済みが無くなってから古い黙殺を落とす。
- `Prune_NeverDropsProtectedStatuses_AllowsOverCapacity`：保護対象5状態と vindicated 黙殺は落ちず、容量超過を許す。
- `Prune_VindicatedDormantProtected_StillResurfaces`：保護された黙殺が 再浮上→決裁待ち へ進む（同じ実体・同じ id）。
- `RetainedDormant_WithinCapacity_CanLaterBeVindicatedAndResurface`：容量内に残した黙殺が、後から vindicated になって再浮上できる。
- `Prune_Repeated_IsIdempotent_AndDoesNotReuseIds`：Prune を繰り返しても件数・打切り数が変わらず、落とした id を再利用しない。
- `SaveRoundTrip_KeepsNextIdAndDroppedCount_WhenNewestWasDropped`：JsonUtility 往復で打切り件数と次の採番を保つ（最新 id が落ちていても再利用しない）。
- `SaveRoundTrip_SmallerCapacityOnLoad_AddsLoadDrops_LegacyMetaNoop`：読み込み側の容量が小さい場合に読み込み時の打切りも数える。旧セーブ（メタ0）は何も変えない。null 安全。

## 未実行の試験（すべて未実行）
- Unity コンパイル（Core/Data/Tests）。
- EditMode：`PetitionLedgerTests`（既存10＋新規11）、関連回帰 `PetitionFlowRulesTests`/`WorkflowRulesTests`/`RingiCompletionTests`/`PetitionTriageRulesTests`/`CampaignSaveFullPersistTests`。
- TestHarness `dotnet test`：JsonUtility はスタブ（System.Text.Json 近似）。public int フィールドのみなので差は出ない見込み。
- PlayMode 回帰：`RingiFlowPlayModeTests`（保存/ロード経路で `CampaignSerializer.ReservePetitionIds` を使用）。

## 残件・注意
- `RingiObserverOverlay.cs:127` の表示文言は「古い決着済 N 件を掃き出し」のまま。現在は黙殺の打切りも含むが、Game 層 UI は対象外として触っていない（文言修正は別票）。
- 現状、`vindicated=true` にするコード経路も `PetitionFlowRules.Resurface` の実呼び出しも Game 層に存在しない（試験のみ）。依頼文にある `TryResurface` という名前は存在せず、実際のメソッド名は `Resurface`。再浮上の配線は別件。
- `Prune` は `Add` 時にしか呼ばれない。状態が黙殺へ変わっただけでは次の `Add` まで整理されない（従来と同じ挙動）。
- 容量（既定64）は保存しない。コード側の設定を優先する方針のため。
- `docs/catalog/core-modules-catalog.md` は新モジュールではないので追記していない。
