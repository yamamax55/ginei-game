# 総裁選・党内派閥の整備（内閣任命と分離） — 結果（party-leadership-20260914 / a1）

対象 #165 #159 #2768。基点 integration/local-cloud-20260914 HEAD c15fc0e8。実装 Claude／試験 ChatGPT。
**試験は一切実行していない**（作業票で実行禁止）。git 操作なし。シーン・プレハブ・設定・実セーブ・ChatGPT レビュー JSON・他作業票は編集していない。
仕様の出所：`party-cabinet-followup.md` の「総裁選・派閥・当選回数（ユーザー追加2026-09-14）」節。

## 変更ファイル
| ファイル | 種別 | 内容 |
|---|---|---|
| `Assets/Scripts/Core/Government/PartyLeadershipState.cs` | 新規 | `PartyLeadershipState`（任期・連続任期・次回年・選出待ち理由・記録）＋記録DTO `LeadershipElectionRecord`/`LeadershipCandidateResult`/`LeadershipRegionResult`/`LeadershipFactionStance`＋enum `LeadershipOutcome`/`LeadershipCandidateStatus` |
| `Assets/Scripts/Core/Government/PartyLeadershipRules.cs` | 新規 | 総裁選の本体（年次 `TickYear`・届出版 `RunElection`・推薦・第1回・決選・暫定/選出不能・派閥整理 `NormalizeFactions`・読込 `NormalizeLoaded`）＋`PartyLeadershipParams`（党規則の調整値）＋`LeadershipCandidacy` |
| `Assets/Scripts/Core/Government/PartySeniorityRules.cs` | 新規 | 当選回数→年功（逓減・頭打ち）・目安区分・党内序列＋`PartySeniorityParams`/`SeniorityInfo`/`SeniorityTier` |
| `Assets/Scripts/Core/Government/LeadershipElectionRules.cs` | 変更 | 既存 API は維持。`TallyLegislatorVotesByFaction` を一人1票（派閥ID小で1回）に修正、`StrictMajority`/`MajorityNeeded`/`ConvertToAllotment`/`SeedOf`/`SeededRoll` を追加 |
| `Assets/Scripts/Core/Government/Party.cs` | 変更 | `Party.leadership` 追加。`PartyFaction` に `policyStance`/`cohesion`/`endorsedCandidateId`/`mainstream`、`Weight` を重複IDを数えない数に |
| `Assets/Scripts/Core/Government/ElectionCycleRules.cs` | 変更 | `OrganizeParties` ③の旧党首補充を管理下の党（`leadership.managed`）で止める。`NormalizeLoaded` から `PartyLeadershipRules.NormalizeLoaded` |
| `Assets/Scripts/Game/GalaxyView.Politics.cs` | 変更 | `RunPoliticsTick` で国政選挙の前に `RunPartyLeadership`（管理下に置く→所属整理→総裁選→通知） |
| `Assets/Scripts/Game/PoliticsObserverOverlay.cs` | 変更 | 党ごとに党首任期／不在＝選出待ち／直近の総裁選（理由・投票者・推薦人数・第1回/決選の内訳・算定票・集計議席の算式・制限・地方票・派閥の動き・選挙ID・seed）／派閥一覧（領袖・所属・結束・政策・主流/反主流・無派閥）／党内序列 |
| `Assets/Scripts/Game/CoreStateInspector.cs` | 変更 | glossary 追加（既存キーと重複なしを grep 確認。`leadership`/`cohesion`/`status` は既存キーのため足していない） |
| `Assets/Tests/EditMode/PartyLeadershipRulesTests.cs` | 新規 | Core 試験 16件 |
| `Assets/Tests/PlayMode/PartyLeadershipPlayModeTests.cs` | 新規 | Game 接続試験 1件 |
| `docs/catalog/core-modules-catalog.md` | 変更 | 1行追記 |

`.meta` は新規 .cs 5本とも既に存在していた（hook/Unity 生成と推定）＝触っていない。

## 仕様と実装の対応
- **任期・実施時期**：任期3年（`termYears`）。年次で①党首の欠缺（`VacancyCause`＝名簿不在/死去/離反・在野/拘束・行方不明/政治家でない→`leaderId=-1` にして実施）②任期満了③暫定任期の満了④党首空席、で実施。就任年不明の現党首（旧データ・シナリオ）は**その年から任期を起算するだけ**（総裁選しない・`termNote` に注記）。選挙ID＝`勢力:党ID:総裁選:年`（国政選挙IDと別）。同じ党・同じ年は `lastElectionYear` で一度だけ（再処理・読込後の同年は何もしない）。適格者のいない党は選出不能を一度だけ記録し毎年の空記録を作らない。
- **首相・政府と分離**：勝者は `PartyOrganizationRules.AppointPost(党首)`＝`Party.leaderId` だけ。`government`・`GovernmentRegistry`・議席・当選回数・軍に触れない。首相は従来どおり下院選挙後の `FormGovernment` か、首相欠缺時の `MaintainGovernment` でだけ決まる。Game では総裁選を国政選挙より前に回すため、選挙年は新党首で組閣できる。
- **旧来の党首補充との関係**：`ElectionCycleRules.OrganizeParties` ③（支持の加重最大で即補充）は、`PartyLeadershipRules.Adopt`/`TickYear` が管理下にした党では動かない。管理前の党（Core 単体の既存呼出し・旧セーブ）は従来どおり＝既存試験の期待を変えない。
- **第1回**：議員票＝党籍があり当選した党の議席に在任する適格なネームド議員が一人1票。**集計議席**（人物のいない議席）は `useAggregateSeatVotes`（既定 true）で匿名票として加え、配分はネームド議員の票の比率で最大剰余、ネームド票が無ければ候補評価の比率（推計）＝**算式を `aggregateFormula` に記録**。党員票＝一般党員集計（`PartyMembershipTally`）の生票：星系集計を優先し、全国集計が星系合計を上回る分は「星系不明分」として党員票にのみ含める。生票は候補の党員訴求（`PoliticianRules.MemberVoteAppeal`、地盤の星系は `HomeTurnoutBonus`）比で最大剰余配分。算定票＝議員票の総数（議員票0の党は `noLegislatorMemberAllotment`=100）へ `ConvertToAllotment`（端数＝剰余大→生票大→候補ID小）。**一般党員が不明なら党員票なし**（ネームド人数から換算しない・制限に明記）。
- **過半数と決選**：`StrictMajority`（票×2＞有効票）。未達は第1回順位（得票→議員票→算定票→生票→seed くじ→ID）の上位2人で決選＝議員の再投票（＋集計議席の再配分）＋**党員集計のある星系ごと1票**（第1回の生票で2人を比較、同数は無効）。勢力が持つ星系に限る（Game は国政選挙の地域＝所有星系を渡す）。決選の同票は seed のくじ（理由に明記）。
- **小党・例外**：国政議員（実在・集計とも）がいない党は `smallPartyNamedMemberVotes`（既定 true）でネームド党員が一人1票（例外として制限に明記）。例外を切り、党員も不明なら有効票0＝**選出不能（空席のまま）**。候補1人＝無投票当選。候補ゼロ＝現党首がいれば暫定続投、いなければ候補評価最上位の党員を暫定党首（いずれも1年、`pendingReason` に理由）、適格な党員がいなければ選出不能。
- **立候補・推薦**：自動は領袖（候補評価順）→現党首→その他（候補評価順）で上限3人。推薦人数＝clamp(floor(投票者×7%), 1, 20) を投票者−1以下に抑える＝小党でも成立可能。推薦人は残る候補のうち最も支持する1人だけを推し、要件未達の最弱候補を撤回させて繰り返す（撤回者は推薦人に回る）。届出版 `RunElection(..., declared)` は候補本人・投票資格なし・重複の推薦を無効化し、二重に届けた推薦人は支持の高い1人だけに数える（入力順に依らない）。連続任期上限（既定3期）の現党首は立候補不可＝無限再選の防止。
- **派閥**：既存 `PartyFaction`（`bossId`/`memberIds`）に政策傾向・結束・直近の推薦・主流を追加。`NormalizeFactions`＝null/重複派閥ID除去、党員でない領袖・複数派閥の領袖を外す、領袖を自派閥へ移す、党外・負ID・二重所属を除く（派閥ID小に残す）＝一人一派閥。死去・離党・移籍は既存 `PartyMembershipRules.Normalize`→`PartyOrganizationRules.Leave` が名簿から外す。投票は**各人の選好**＝候補評価＋信条一致＋人間関係（忠誠の対象/同期/同郷）＋所属派閥の推薦×結束×重み＋seed 揺らぎ（候補は自分に投票）。領袖の推薦は政策傾向・関係・候補評価で決まり、推した候補が決選に残らなければ支持変更。票は一人1回だけ数え、派閥ごとに「投票した所属者/推薦どおり（差＝造反）」と主流/反主流を記録。
- **当選回数**：`PartySeniorityRules` は `LegislatorRecord` の国政累積（下院/上院・シナリオ明示の前歴を含む）だけを読む＝知事当選・総裁選勝利・閣僚任命は数えない。年功＝min(回数,12)/(min(回数,12)+3) を上限値で正規化（逓減・頭打ち）。候補評価＝0.6×能力（党員訴求と行政素養）＋0.3×党文化（0〜2、既定1）×年功＝同条件ならベテランが上、能力の高い若手は勝てる、党文化0で年功無視。区分（新人/中堅/ベテラン/履歴未登録）と序列は表示用で役職・階級・権限を与えない。記録のない人は「履歴未登録」。
- **保存**：`Party.leadership`・`PartyFaction` の新フィールドは既存の戦役セーブに同乗。`NormalizeLoaded` は null 埋め・壊れた記録除去・上限・派閥整理のみ（旧セーブは `managed=false` のまま＝読込で総裁選も当選回数も起こさない）。
- **表示**：政治オブザーバ（O）の各党の下に出す。既存の ScrollRect/スクロールバーの中の本文に追記しただけ（スクロール構造は不変）。

## 期待する試験（未実行）
EditMode `PartyLeadershipRulesTests`（16件）：
1. `Math_StrictMajority_Conversion_Seed` — 50%は過半数でない・51/100・0票、算定票 {500,300,200}→{5,3,2}／同数端数はID小／生票0→0、seed/くじの再現、推薦人数（50人→3・10人→1・1000人→20・一人党0・投票者−1上限）。
2. `FirstRound_StrictMajority_WithConvertedMemberVotes_AndFactionsMainstream` — 議員10票（6/4）＋星系600/400の生票 513/487→算定 5/5＝11/20 で第1回当選、任期800〜803、主流/反主流、党首選で当選回数不変。
3. `NoMajority_RunoffTopTwo_LegislatorsAndRegions_ReversesFirstRound` — 第1回 8/7/5（生票 162/69/69・算定6/2/2）→決選で第3派閥が忠誠先へ支持変更し 議員2+地方3=5 対 議員8=8 で2位が逆転、地方票3星系。
4. `NationalMembersOnly_RunoffHasNoRegionalVotes_Restricted`
5. `UnknownMembers_NoMemberVotes_NotDerivedFromNamedCount`
6. `SmallParty_NamedMembersVote_EndorsementsCollapseToUncontested` — 3人党で推薦不足の撤回→無投票、一人党は推薦0で無投票。
7. `NoEligibleMembers_Or_NoValidVotes_LeaderStaysVacant_NoFabrication` — 死者だけ＝選出不能・選出待ち・翌年は空記録なし、例外オフ＋党員不明＝候補2人でも有効票0で空席。
8. `DeclaredCandidacy_NoCandidates_IncumbentContinuesOrActingLeader` — 資格なしの届出だけ＝暫定選出1年、同じ年の再実施は null。
9. `DeclaredEndorsers_OnePersonOneCandidate_DuplicatesAndInvalidNotCounted_OrderIndependent`
10. `Factions_DuplicatesNormalized_VotesCountedOncePerPerson` — 一括集計の二重計上なし、一人一派閥化・2回目変更0、投票者6＝派閥投票者＋無派閥。
11. `Cohesion_DoesNotForceAllMembers_DefectionReproducibleBySeed`
12. `Seniority_DiminishingCapped_YoungAbleCanWin_NotAuthority` — 年功 0/0.3125/逓減/頭打ち、区分、候補評価 0.3935、若手の抜擢・同条件はベテラン・党文化0で同値、序列と履歴未登録。
13. `Term_ThreeYears_SameYearNoChange_TermLimit_LegacyLeaderOnlyStartsTerm`
14. `OnlyTermLimitedIncumbent_ContinuesProvisionally`
15. `LeaderDefects_ElectionHeld_PremierSeatsAndWinsUnchanged` — 党首（首相）離反で総裁選、首相・議席・当選回数不変、管理下の党は `OrganizeParties` で党首を補充しない。
16. `SaveRoundTrip_KeepsLeadershipRecordsAndFactions_LoadHoldsNoElection` — 記録・票・地方票・派閥の往復、旧党は未管理のまま、同じ選挙の再処理 null、読込で当選回数不変。

PlayMode `PartyLeadershipPlayModeTests.AnnualTick_HoldsLeadershipElections_SeparateFromPremier_SurvivesSave_AndOverlayShows`（1件）：初年に党ごと総裁選1回・通知1通ずつ・組閣した党の党首が首相／同年再処理で増えない／JSON 往復→再構築で党首・選挙ID・記録数・当選回数が戻り読込で総裁選しない／翌年に首相が離党→総裁選で新党首、首相・宰相職不変・新党首の役職数不変・軍の指揮権なし・年次宰相銓衡でも首相不変／オブザーバに総裁選・選挙ID・seed・党内序列・無派閥・「不在＝選出待ち」・分離の注記、同年は空席のまま。

既存回帰で通るべきもの：`LeadershipElectionRulesTests`（Tally は重複なしの入力なので 7/3 のまま）・`PoliticianRulesTests`・`PoliticianLifecycleIntegrationTests`・`ElectionCycleRulesTests`・`PartyMembershipRulesTests`・`PartyOrganizationRulesTests`・`LegislatorRosterRulesTests`・`ElectionSaveRoundTripTests`・`LocalElectionRulesTests`、PlayMode `ElectionGameIntegrationPlayModeTests`・`LegislatorHistoryPlayModeTests`・`PoliticsObserverElectionPlayModeTests`・`PartyStatusObserverPlayModeTests`。
Game 経路の変化で注意して見てほしい点：`ElectionGameIntegrationPlayModeTests` は初年の党首が旧来の③でなく総裁選（小党例外→推薦で1人に収束→無投票）で決まる。首相=第一党党首・同年再処理で「選挙」を含む通知ゼロ（総裁選の通知文は「選挙」を含まない）・捕虜首相の再組閣理由「職務を続けられない」は維持される想定。捕虜の首相＝党首のケースでは、管理下の党は同じ年に党首を補充しないため `MaintainGovernment` の結果は「組閣未成立（党首がいない）」になる（理由文字列は従来どおり前置される）。

## 未確認
- Unity コンパイル（Core/Game/テスト）・EditMode/PlayMode・TestHarness `dotnet test` はいずれも未実行。数値の期待（生票 513/487・162/69/69、候補評価 0.3935 等）は手計算で、float 丸めの端数で1票ずれる可能性はゼロではない。
- 実画面（戦略マップの O 窓）での見た目・行の長さ・スクロール位置の保持は未確認（本文は毎フレーム再生成・ScrollRect は既存のまま）。
- JsonUtility の実機での入れ子（CampaignSaveData→…→LeadershipCandidateResult＝7段）は深さ上限10以内の想定。
- 候補評価・派閥の重み・推薦割合・算定票100などの既定値は仮置き（ゲームバランス未調整）。

## 不足・残件（次の小作業へ）
- **組閣・党三役（範囲外）**：連立・新内閣・党三役の任命、派閥均衡の組閣人事への反映、順送り/抜擢の不満・協力低下は未実装。党首交代時に首相が別党首のまま残る状態（首相が離党した場合を含む）の扱いは次票の仕様判断。
- 暫定選出の党首（1年）も `FormGovernment` の首班候補になりうる（既存の組閣規則は党首の資格しか見ない）＝組閣票で扱いを決める必要。
- 党首欠缺が政治 Tick 後（年の後半）に起きると、次の年次の総裁選まで党首不在＝その間の再組閣は「組閣未成立」になる。
- 派閥の分裂・新党結成、派閥の自動結成（デモ党は派閥なし＝全員無派閥）、派閥の政策傾向/結束の入力経路（シナリオ・イベント）は未実装。
- 届出版 `RunElection`（プレイヤー・イベントの立候補/推薦/撤回）の UI・操作入口は未配線。候補の撤回・死亡を「告示後」に扱う段階的な状態遷移は持たず、年次の一括処理（記録に立候補/撤回/落選/決選進出/当選を残す）。
- 議員/人事画面での「国政当選○回・党内序列・任命候補理由」の並べ替え UI は未実装（政治オブザーバに序列の上位を表示するのみ）。地方議員の当選歴は現状の選挙層に無いため数えていない。
- 規約メモ：新規 `PartyLeadershipState.cs`/`PartyLeadershipRules.cs`/`PartySeniorityRules.cs` は既存 `Party.cs`/`ElectionCycleRules.cs` に倣い、補助の直列化DTO・Params・enum を同じファイルに置いた（1ファイル1クラスからの逸脱）。
