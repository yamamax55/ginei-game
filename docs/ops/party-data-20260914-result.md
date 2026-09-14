# 政党データと所属管理の整備 — 結果（party-data-20260914 / a1）

対象 #159 #165 #2768。基点 integration/local-cloud-20260914 HEAD 9834aa1f。実装 Claude／試験 ChatGPT。
**試験は一切実行していない**（作業票で実行禁止）。git 操作なし。

## 変更ファイル
| ファイル | 種別 | 内容 |
|---|---|---|
| `Assets/Scripts/Core/Government/PartyMembershipRules.cs`（＋既存 .meta） | 新規 | 入党/離党/移籍の共通入口・整理・自動配属・与野党判定・内訳・一般党員集計・読込正規化 |
| `Assets/Scripts/Core/Government/PartyMembershipTally.cs`（＋.meta） | 新規 | 一般党員の集計（全国/星系・人・出所・時点・既定は不明） |
| `Assets/Scripts/Core/Government/Party.cs` | 変更 | `memberIds` の意味を明記（ネームド所属政治家）・`nationalMembership`/`regionalMemberships` 追加・`PartyFaction.memberIds` 注記 |
| `Assets/Scripts/Core/Government/PartyOrganizationRules.cs` | 変更 | `Leave` が重複IDも除き、派閥の名簿・領袖からも外す（null 安全） |
| `Assets/Scripts/Core/Government/PoliticsState.cs` | 変更 | `independentPersonIds`（自ら離党した人＝自動補充で再入党させない） |
| `Assets/Scripts/Core/Government/LegislatorRosterRules.cs` | 変更 | `VacateSeat`（一人の議席を外す公開窓口。当選回数は不変） |
| `Assets/Scripts/Core/Government/ElectionCycleRules.cs` | 変更 | `OrganizeParties` を `PartyMembershipRules` へ委譲・`PoliticsState` 版追加（RunNationalYear/MaintainGovernment はこちら）・`NormalizeLoaded` から党データ正規化 |
| `Assets/Scripts/Game/PoliticsObserverOverlay.cs` | 変更 | 与党★を支持率→組閣と議席由来に。党ごとに支持率/確定議席/党首/ネームド政治家/国政議員/一般党員（不明・出所）を分離表示 |
| `Assets/Scripts/Game/ProtagonistCareerDirector.cs` | 変更 | 主人公の政界入り：既に所属していれば動かさない（二重所属防止）・入党先は組閣した党を優先（未組閣は従来の支持率最大） |
| `Assets/Scripts/Game/CoreStateInspector.cs` | 変更 | glossary 7キー追加（既存キーと重複なしを grep 確認） |
| `Assets/Tests/EditMode/PartyMembershipRulesTests.cs`（＋既存 .meta） | 新規 | Core 試験 13件 |
| `Assets/Tests/PlayMode/PartyStatusObserverPlayModeTests.cs`（＋既存 .meta） | 新規 | 表示試験 1件 |
| `docs/catalog/core-modules-catalog.md` | 変更 | 1行追記 |

## 変更理由（要件との対応）
- **memberIds と議員・一般党員の分離**：`Party.memberIds`＝ネームド所属政治家と明記。議員は従来どおり `LegislatorRecord`。一般党員は別型 `PartyMembershipTally`（`known=false`＝不明が既定、`SetGeneralMembership` で出所・時点つきの明示値だけ入る、ID数から換算する経路なし）。
- **共通入口**：`PartyMembershipRules.Join/Leave/Transfer`。資格＝`ElectionCycleRules.IsEligiblePolitician`（生存・自由・在野でない・同勢力・文民政治家）、党は同勢力のみ、一人一党（他党所属者の Join は拒否し移籍を案内）。拒否は理由文字列を返し例外を出さない。
- **整理**：`Normalize`＝資格喪失（死亡・名簿不在・離反・在野・非政治家／拘束は残す）の離党、負ID・重複ID除去、重複所属は「その党の議席→党首を務める党→党ID小」で1党に絞る、党首・党役職（重複役職・党外就任者・posts内の党首）・派閥名簿/領袖を党員だけに。
- **自動補充**：`AssignUnaffiliated` は無所属者だけ。配属先 `ChooseParty` は既存フィールドの同名一致のみ（綱領=`Person.creed`〔無関心は対象外〕+2、支持基盤=`Person.socialOrigin`+1）→ネームド党員の少ない党→党ID小。一致なしは「対応なし」の理由つき。デモ党は綱領/支持基盤が空なので従来の配属と同じ結果。**自ら離党した人は `independentPersonIds` で再入党させない**（自動補充が離党を翌年に打ち消さないため・入党/移籍の窓口で解除）。
- **役職・派閥・議員資格の整合**：離党/移籍で旧党の党首・役職・派閥から外し、旧党に帰属する議席を `VacateSeat` で集計議席へ戻す（既存 `Reconcile` の「議席は党に帰属」方針に一致）。確定議席・当選回数は触らない。党首欠缺は `leaderId=-1` のまま、年次の `OrganizeParties` の既存党首選で残る党員から補充（候補なしなら空席のまま）。
- **与野党**：`GovernmentPartyId`/`RoleOf`＝組閣状態が単独過半/少数政権でその党が存在するときだけ与党、他は確定議席があれば野党・無ければ議席なし、組閣なし/未成立/対象外は全党「未確定」。支持率は読まない。
- **首相・内閣・軍**：所属管理のどの経路も `government`・政府役職・指揮権に触れない（試験で首相不変を固定）。
- **保存復元**：新フィールドは `Party`/`PoliticsState` に同乗（`FactionStateSave.politics`）。`NormalizeLoaded` で null 集計を「不明」で生成、負の人数は不明へ、星系集計の null/負ID/重複を除去、無所属表明の重複・所属中の人を除去、党員は人物名簿なしで構造整理のみ（入党・選挙・通知は起こさない）。

## テスト期待（未実行）
EditMode `PartyMembershipRulesTests`（13件）：
1. `Join_RejectsOtherFactionDeadMissingAndAlreadyAffiliated` — 他勢力党/死亡/他勢力人物/名簿不在/党不在を拒否、二党目は「移籍」を案内、null 安全。
2. `Transfer_ClearsOldPostsFactionAndSeat_KeepsSeatTotalsWinsAndGovernment` — 旧党の幹事長・派閥領袖/名簿・上院議席が外れ、確定議席・当選回数・首相不変、集計議席+1、Reconcile で重ねて外さない。
3. `Transfer_Rejections_AndUnaffiliatedMustJoin` — 他勢力党・同じ党・無所属・拘束中を拒否（拘束中は党籍と議席を保つ）。
4. `LeaderLeaves_NoException_PremierUnchanged_NotAutoRejoined_LeaderRefilledFromMembers` — 党首離党で空席・首相不変、OrganizeParties で例外なし・再入党しない・党首は残る党員から・空党は空のまま、再入党で当選回数/議席は戻らない。
5. `Normalize_DuplicatesInvalidAndOrphanedOffices` — 重複ID・負ID・死亡・重複所属（議席の党/党首の党を優先）・党外役職・重複役職・派閥、2回目は変更0。
6. `Normalize_DuplicateWithoutSeatOrLead_KeepsSmallerPartyId`
7. `AssignUnaffiliated_KeepsExisting_UsesPlatformAndClassBase_WithReasons` — 既存所属不動・綱領/支持基盤/対応なしの理由・信条優先・無所属表明者と他勢力党を除外。
8. `RunNationalYear_DoesNotRebalanceExistingMembers`
9. `Role_FromGovernmentAndSeats_NotSupport` — 組閣なし未確定・与党/野党/議席なし・未成立/対象外/存在しない党。
10. `GeneralMembership_UnknownByDefault_NotDerivedFromNamedIds`
11. `Summarize_SeparatesSupportSeatsNamedAndLegislators`
12. `SaveRoundTrip_KeepsTalliesUnknownAndIndependents_LoadAddsNoMembersAndKeepsHistory` — 既存の選挙議席・開票記録・議員履歴・首相を往復で維持。
13. `OldData_NullTalliesAndDuplicates_NormalizeWithoutJoining`

PlayMode `PartyStatusObserverPlayModeTests.Overlay_PartyRoleFromGovernment_AndSeparatedCounts`（1件）：選挙後に支持率が逆転しても組閣した党が[与党]・他党[野党]、内訳の各見出し、`250,000人（出所 党公表・SE800）`、`不明（未設定）`、表示で議席/所属/支持率を変えない。

既存回帰で通るべきもの：`ElectionCycleRulesTests`・`LegislatorRosterRulesTests`・`PartyOrganizationRulesTests`・`ElectionSaveRoundTripTests`・`LocalElectionRulesTests`、PlayMode `PoliticsObserverElectionPlayModeTests`・`ElectionGameIntegrationPlayModeTests`・`LegislatorHistoryPlayModeTests`（デモ党は綱領/支持基盤が空＝配属結果は従来と同一の想定）。

## 未確認
- Unity コンパイル（Core/Game/テスト）・EditMode/PlayMode・TestHarness `dotnet test` はいずれも未実行。
- `.meta`：`PartyMembershipRules.cs.meta`・`PartyMembershipRulesTests.cs.meta`・`PartyStatusObserverPlayModeTests.cs.meta` は既に存在（hook 生成と推定）＝触っていない。`PartyMembershipTally.cs.meta` は Write 時に既存扱いで、こちらで guid `9c3e5a71d2b64f08a6e1c4d7b8f0a352` に上書きした＝Unity が別 guid で取り込み済みなら再生成の確認が要る（参照元はないので実害は想定しない）。
- `250,000` の桁区切りは実行環境のカルチャ依存（`ToString("#,0")`）。
- 実画面（戦略マップの O 窓）での見た目・行の長さは未確認。
- 主人公の政界入り（`ProtagonistCareerDirector`）は軍人ロールのため共通入口の資格判定を通らず、従来どおり低レベル `PartyOrganizationRules.Join` を使う（二重所属だけ防止）。年次の `OrganizeParties` では名簿不在として外れる既存挙動は変えていない。

## 残件・仕様判断
- 移籍した首相：首相の地位・組閣の党は動かさない（要件どおり）。総裁選／内閣の範囲で「与党を離れた首相」をどう扱うかは次票。
- 知事の `governorPartyId` は当選時の党の記録として残る（移籍しても書き換えない）。表示上の所属と食い違う可能性あり。
- 一般党員集計を入れる経路（シナリオ・イベント）は未配線＝現状は全党「不明」表示。
- `independentPersonIds` は死亡者を掃除しない（ネームド人数で有界）。
- Game 側の入党/離党/移籍の操作 UI・通知は未実装（Core 入口のみ）。
- 範囲外：総裁選本体・新内閣・党の結成/分裂。
