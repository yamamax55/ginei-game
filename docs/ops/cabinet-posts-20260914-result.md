# 内閣政治任用・党三役の任免基盤 — 結果（cabinet-posts-20260914 / a1）

対象 #2768 #141 #159 #165 #145。基点 integration/local-cloud-20260914 HEAD 7324eca3。実装 Claude／試験 ChatGPT。
**試験は一切実行していない**（作業票で実行禁止）。git 操作なし。シーン・プレハブ・設定・実ユーザーセーブ・ChatGPT レビュー JSON・他作業票は編集していない。
仕様の出所：作業票本文＋`party-cabinet-followup.md`（組閣・政治任用／党三役の節）。連立交渉・官僚昇任・新選挙方式は範囲外。

## 変更ファイル
| ファイル | 種別 | 内容 |
|---|---|---|
| `Assets/Scripts/Core/Government/CabinetState.cs` | 新規 | `CabinetState`（首相との結び付け・組閣年・根拠の選挙ID・職務執行・職・履歴・捨てた件数）＋`CabinetPost`＋`AppointmentHistoryEntry`（内閣・党三役で共用）＋enum `CabinetPostKind`{大臣/副大臣/政務官}・`CabinetDelegation`{所管政策/所管決裁} |
| `Assets/Scripts/Core/Government/CabinetAppointmentRules.cs` | 新規 | 内閣の任免・権限・委任・整理・AI 組閣・読込（＋`CabinetParams`/`CabinetAction`/`AppointmentResult`/`CandidateEvaluation`） |
| `Assets/Scripts/Core/Government/PartyExecutiveRules.cs` | 新規 | 党三役の任免・権限・整理・AI 補充・読込（＋`PartyExecutiveAction`） |
| `Assets/Scripts/Core/Government/PoliticsState.cs` | 変更 | `cabinet` を追加 |
| `Assets/Scripts/Core/Government/PartyOrganization.cs` | 変更 | `PartyAppointment` に `appointedYear`/`appointedById`/`reason`/`caretakerUntilYear`（既定＝旧セーブ互換） |
| `Assets/Scripts/Core/Government/Party.cs` | 変更 | `postHistory`/`postHistoryDropped`/`postVacancyNotes` |
| `Assets/Scripts/Core/Government/ElectionCycleRules.cs` | 変更 | `NormalizeLoaded` から `PartyExecutiveRules`/`CabinetAppointmentRules` の `NormalizeLoaded` を呼ぶだけ |
| `Assets/Scripts/Game/GalaxyView.Cabinet.cs` | 新規（partial） | `RunCabinetAndPartyExecutives`（整理→補充→通知）と通知の集約 |
| `Assets/Scripts/Game/GalaxyView.Politics.cs` | 変更 | 呼出し4か所：`RunPoliticsTick` 末尾（整理＋補充）／`MaintainElectedPremier` 末尾（首相交代・不在の反映＋補充）／`SuspendElections`（内閣を置かない＝退任のみ）／`RestoreElectedOffices`（読込は整理のみ・通知なし） |
| `Assets/Scripts/Game/GovernmentObserverOverlay.cs` | 変更 | 内閣の節（首相・組閣年・職務執行、省ごとの大臣/副大臣/政務官・所属党・就任年・任命理由・空席理由・委任範囲、同じ省の職業官僚の人数、任免履歴）＋`DumpTextForTest` |
| `Assets/Scripts/Game/PoliticsObserverOverlay.cs` | 変更 | 各党の党首欄の下に党三役（在任者・役割・就任年・暫定・任命理由・空席理由・直近の任免・権限の注記） |
| `Assets/Scripts/Game/CoreStateInspector.cs` | 変更 | glossary 20キー（既存キーと重複なしを grep 確認） |
| `Assets/Tests/EditMode/CabinetAppointmentRulesTests.cs` | 新規 | Core 試験 13件 |
| `Assets/Tests/PlayMode/CabinetPostsPlayModeTests.cs` | 新規 | Game 接続試験 1件 |
| `docs/catalog/core-modules-catalog.md` | 変更 | 1行追記 |

`.meta` は新規 .cs 6本とも既に存在していた（hook 生成と推定）＝触っていない。

## 設計判断（重要）
- **閣僚職は `GovernmentRegistry` へ登録しない。** 兵部省（軍事所掌）の大臣を `Office` として登録すると、`GalaxyView.StampBattleCommandAuthority` の `OfficeRules.CanPropose(軍事, 国家)` が真になり全軍の直接指揮権になる（既存 PlayMode 試験の `AssertNoFleetCommand` もこの判定）。そこで在任・委任・履歴は `PoliticsState.cabinet`（保存される台帳）を単一の出所にし、権限は `CabinetAppointmentRules.Authority` で問う。`GovernmentRegistry` は「首相・知事との兼任」判定の読み取りにだけ間接的に使う（知事は選挙台帳側で判定）。資格の型は `OfficeRules.CanHold`（政治任用・文民）を再利用。
- **職業官僚と分離。** 省の配属（`Ministry.staffIds`）・事務次官級・宰相銓衡には一切書き込まない。デモの `RunMinistryStaffingTick` は政治家を含む全文民を省へ配属するため、同じ人物が「省職員」かつ「閣僚」になりうる（配属は変えずそのまま＝表示で両方見える）。これを排他にすると現状のデモでは閣僚候補がほぼ消えるため、今回は排他にしていない（残件）。
- **党三役は既存 `Party.posts` を使用**（別台帳なし）。空席の理由は `posts` に空エントリを残すと `PartyMembershipRules.Normalize` が毎年「変更」として数えるため、`Party.postVacancyNotes` に分けた。

## 仕様と実装の対応
- **任免主体**：`FormalPremier`＝政府が単独過半/少数政権で、首相が生存・自由・同勢力・在野でない文民政治家のときだけ有効。首相本人以外の任免は `canPetition=true`・`petitionToId=首相` の拒否（上申扱い）。党三役は `FormalLeader`（有効な党首本人）。党首そのものは任命しない（総裁選）。
- **資格・拒否理由**：名簿に存在しない／死亡／拘束・不在／他勢力／在野／政治家でない／軍人／存在しない省（最上位の太政官にも大臣は置かない）／在任中（先に解任）／兼任（首相本人・他の閣僚職・知事・党三役・首相でない党首・与党でない党の党員＝連立は未実装）。党三役は党員でない／党首本人／首相／他の三役／閣僚。候補不足は空席＋理由（人物を作らない・履歴は増やさない）。
- **権限差**：首相＝閣僚任免・提案・調整（所管の決裁は大臣へ上申）。大臣＝所管省の政策決定・決裁・提案・調整（他省は不可）。副大臣＝提案・調整＋大臣本人の明示委任（範囲は 所管政策/所管決裁 のみ・期限はその年から最長2年＝無期限不可・大臣の交代/解任・期限切れ・職務執行で失効）。政務官＝提案・調整のみ。`艦隊作戦指揮`・`国庫支出` はどの職でも拒否。党三役：幹事長＝党運営・選挙候補調整、政調会長＝政策調整、総務会長＝党内合意、党首＝党役職の任免と党内の行為。政府決裁・閣僚任免・国庫・艦隊指揮は党の役職では拒否。
- **共通整理（`Reconcile`・冪等）**：政体が対象外→全員退任（総辞職）／首相が替わった→前内閣は総辞職して新首相に結び付け／下院改選後の首班指名（政府の `sourceElectionId` 変化）→同じ首相でも総辞職して組み直す（同じ職への再任は「続投」）／首相不在（死亡・拘束・組閣未成立）→職務執行内閣：大臣は所管の決裁と調整のみ・任免/政策決定/委任なし・期限＝翌年まで→期限切れで総辞職／在任者の死亡・拘束・他勢力・政治家でない・知事就任・野党への所属→失職。党三役：離党（`PartyOrganizationRules.Leave` が消した在任を直前の履歴から検出）・死亡・党首就任→失職、党首交代→退任（新党首の再任は続投）、党首不在→期限つき暫定→期限切れで失職。
- **AI 任免**：`AutoFill` は首相／党首を任命者として `TryAppoint` を通す（プレイヤー勢力も同じ入口）。順序＝大臣→副大臣→政務官（省ID順）、その後に党三役（兼任しないため閣僚が先）。候補評価＝能力（行政素養70%＋民望30%）×0.6＋当選回数の逓減年功×0.25（大臣1・副大臣0.5・政務官0）＋経験の目安（大臣ベテラン/副大臣中堅/政務官新人）0.1＋与党0.1＋派閥の在任が少ないほど0.05/(1+n)。同点は ID 小。選定理由（能力・国政当選回数と区分・所属党・派閥・評価値）を `appointmentReason` に残す。
- **Game 接続**：年次の政治 Tick の末尾（国政・知事・議員の反映後）で整理＋補充、宰相の維持（首相交代時）でも同じ処理、非民主へ移行で退任のみ、開幕/読込の復元では整理のみ（通知・任命なし）。通知は内閣の任命を1通にまとめ、総辞職・職務執行・失職は個別。党三役は党ごとに1通。通知文には既存試験の検索語（「選挙」「首相に」「総裁選」「当選議員」等）を含めていない。
- **保存**：`PoliticsState.cabinet`・`Party.postHistory`/`postVacancyNotes`・`PartyAppointment` の追加フィールドは既存の戦役セーブに同乗。旧セーブ＝内閣は空（次の年次で組閣）、党三役の任命者不明は現党首の任命として続行（履歴・再任命なし）。読込では整理のみ＝任命イベントを重複させない。履歴は上限60件で古い順に捨て、捨てた件数を保持。

## 期待する試験（未実行）
EditMode `CabinetAppointmentRulesTests`（13件）：
1. `Reconcile_BindsPremier_CreatesThreePostsPerMinistry_WithoutAppointing` — 3省×3職・太政官に大臣なし・「未任命」・職名・再整理0件。
2. `TryAppoint_OnlyFormalPremier_OthersArePetitions` — 権限外は首相への上申、首相の任命で在任/任命者/党/年/履歴「就任」、`GovernmentRegistry` の件数・兵部省の職業官僚 {90,91} 不変、組閣未成立では誰も任命不可。
3. `TryAppoint_RejectsIneligibleAndUnknownMinistry_WithReasons_NoChange` — 名簿外/死亡/拘束/他勢力/軍人/非政治家/野党党首/野党党員/首相本人/存在しない省/太政官。
4. `Concurrency_OnePostPerPerson_GovernorPartyExecutiveAndOccupiedRejected`
5. `Authority_MinisterViceSecretaryPremier_Differ_NoFleetCommandOrTreasury`
6. `Delegation_ExplicitScopeAndTerm_OnlyMinister_VoidedByMinisterChangeAndExpiry`
7. `AutoFill_SelectsByAbilityAndDiminishingSeniority_ShortageLeavesVacancy_PremierChangeResigns` — 大臣：ベテラン5（0.629）→式部、若手の実力者6（0.478）→大蔵、同点0.31 は ID 小の2→兵部、副大臣 3/4、残りは「適格な候補なし」、政権交代で総辞職1＋退任5・旧大臣/旧首相の権限なし、新与党の8だけ入閣、改選後の組み直しで「続投」。
8. `PremierAbsent_CaretakerWithLimitedScope_ThenExpiresAndResigns`
9. `HolderDeathCaptivityDefection_VacateWithReason_Idempotent_AndRegimeChangeResigns`
10. `PartyExecutives_LeaderOnly_RoleSeparation_NoGovernmentAuthority`
11. `PartyExecutives_DepartureLeaderChangeAndAbsence_ClearOldAuthority` — 離党の失職検出・党首交代2件・前党首は任命不可・再任で続投・AI 補充3件（与党 4/6・野党 8）・首相は三役不可・党首不在の暫定3件→期限後失職。
12. `SaveRoundTrip_KeepsCabinetDelegationPartyPostsHistory_LoadDoesNotReappoint`（`CampaignSerializer` の文字列往復）
13. `LegacySave_PartyPostsWithoutAppointer_AreKeptWithoutReappointment`

PlayMode `CabinetPostsPlayModeTests.AnnualTick_FormsCabinetAndPartyExecutives_SurvivesSave_DeathAndPremierChangeClearAuthority`（1件）：初年に組閣（4省×3職・大臣1名以上・一人一職・首相/知事/野党でない・任命者=首相・選定理由・空席理由・`GovernmentRegistry` の役職なし・全軍指揮権なし・通知1通）、党三役（党員・党首でない・閣僚でない・政府決裁なし）、職業官僚の配属と政治家でない官僚#30 は不変／同年の政治 Tick＋宰相銓衡で履歴・通知が増えない／JSON 往復→再構築で在任・履歴が戻り読込で通知なし・同年再処理なし／閣僚の死亡で失職と通知／首相の死亡で職務執行（政策決定不可・委任なし・死亡した首相の任免権なし）→翌年の総裁選→新首相で総辞職と新組閣（全在任の任命者=新首相・退任者に権限なし・官僚配属不変）／政府・政治オブザーバの表示。

既存回帰で通るべきもの：`ElectionCycleRulesTests`・`PartyOrganizationRulesTests`・`PartyMembershipRulesTests`・`PartyLeadershipRulesTests`・`ElectionSaveRoundTripTests`・`OfficeRulesTests`・`GovernmentRegistryTests`、PlayMode `ElectionGameIntegrationPlayModeTests`・`PartyLeadershipPlayModeTests`・`LegislatorHistoryPlayModeTests`・`PoliticsObserverElectionPlayModeTests`・`PartyStatusObserverPlayModeTests`。
注意して見てほしい点：`ElectionGameIntegrationPlayModeTests` の死亡/捕虜ケースで宰相の維持の後に内閣が職務執行へ入り「内閣職務執行」の通知が1通増える（既存の検索語とは重ならない想定）。`PartyLeadershipPlayModeTests` の手順4は `GovernmentRegistry` の役職数だけを比べているため、閣僚任命（台帳のみ）では変わらない想定。

## 未確認
- Unity コンパイル（Core/Game/テスト）・EditMode/PlayMode・TestHarness `dotnet test` はいずれも未実行。候補評価の期待値（0.629/0.478/0.31）は手計算。PlayMode は人物の党への配属・議席の結果に依存するため、定員の充足数は下限だけを確かめている。
- 実画面（Alt+G 政府・O 政治）の見た目・行の長さ・スクロール位置の保持は未確認（本文は毎フレーム再生成・ScrollRect とスクロールバーは既存のまま）。

## 残件と制限
- **手動任免／上申の操作 UI は未配線**（プレイヤーが首相でも画面から任免・委任できない。Core の `TryAppoint`/`Dismiss`/`Delegate` と上申先は用意済み）。`DecisionAuthorityDirector`（決裁デスク）は閣僚の所管決裁・副大臣の委任をまだ読まない＝稟議の裁可判定は従来の `GovernmentRegistry` 経路のまま。
- 連立（与党以外の党員の入閣）・連立協議は未実装＝与党でない党の党員は入閣不可として拒否。
- 首相が就任後に党首でなくなった場合・就任後に党首に選ばれた閣僚は、整理で解任しない（任命時だけ拒否）。
- 派閥均衡は候補評価の小さな加点だけ。順送り/抜擢の不満・協力低下、派閥の人事要求は未実装。
- 同じ人物が「省の職業官僚（配属）」と「閣僚」を兼ねうる（デモの省配属が政治家も配属するため）。官僚任用の作業（#156）で分離する。事務次官職そのものは現状の省データに無い（表示は職業官僚の人数のみ）。
- 首相の欠缺が年次の宰相銓衡より後（同年の後段）に起きた場合、次の年次まで台帳上は在任のまま（`Authority` は在任者の生存・自由を毎回確かめるため権限は効かない）。
- 党内の未処理案件（政調会長の政策上申・総務会長の党内承認の案件キュー）は未実装＝役割の権限判定のみ。
- 大臣・副大臣・政務官の定員は省ごと各1。副大臣の委任範囲は所管政策/所管決裁の2種のみ。
- 1ファイル1クラスの逸脱：既存 `Party.cs`/`ElectionCycleRules.cs` に倣い、補助の DTO・Params・enum・結果型を同じファイルに置いた。
