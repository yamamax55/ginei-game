# 内閣・党三役の Game 接続試験失敗の切り分けと修正 — 結果（cabinet-qa-fix-20260914 / a1）

対象 #2768 #141 #165。前作業 cabinet-posts-20260914/a1 の未コミット変更の上に最小修正を重ねた。
**試験は一切実行していない**（作業票で実行禁止）。git 操作なし。シーン・プレハブ・設定・実ユーザーセーブ・ChatGPT レビュー JSON・他作業票は編集していない。既存の官僚名簿（`Ministry.staffIds`）・`GovernmentRegistry` への書き込みは追加していない。

## 失敗の切り分け
証拠：`outputs/qa/cabinet-a1/playmode.xml`（18件中17合格）。失敗は `CabinetPostsPlayModeTests.AnnualTick_…`、237行 `Assert.AreEqual(0, OfficesOf(a.holderId).Count, "党三役に政府役職が付いた")` が Expected 0 / But was 1。`unity.log` には人物ID・通知本文は出ていない。

**結論：党三役だから政府役職を得たのではなく、現職の知事が党三役の候補になり任命された。**

根拠（コードからの消去法。実行で人物IDは確認していない）：
1. `PartyExecutiveRules` と `GalaxyView.Cabinet.cs` は `GovernmentRegistry` に一切書き込まない（grep で確認）。党三役の任命で役職が増える経路はない。
2. この試験の世界では `GovernmentRegistry` に載りうる役職は2種類だけ。`SeedGovernmentForQa`→`SeedCommandOffices` が台帳を `Clear` し、軍人がいないので司令長官は空席。残るのは `同盟宰相`（910+勢力）＝首相と、`同盟総督`（920+勢力・星系スコープ）＝選挙で選ばれた知事（`SyncElectedGovernors`）。237行の時点では試験は `RunPoliticsTickForQa` しか呼んでいないため、官位による銓衡の任命もない。
3. 首相は `CandidateProblem` の「首相は党三役を兼ねない」で除外済み。したがって Office の種類は **知事職（`同盟総督`、scope＝星系1か2）** に絞られる。
4. 任命順序（`GalaxyView.RunPoliticsTick`）：総裁選 → 国政（組閣・`ApplyElectedPremier`）→ 議員 → **知事選 `RunDue` → `SyncElectedGovernors`（知事職を登録）** → `RunCabinetAndPartyExecutives`（`PartyExecutiveRules.Reconcile` → 閣僚 `AutoFill` → **党三役 `AutoFill`**）。
5. 閣僚の候補判定には知事の除外（`GovernedSystemOf`）があるが、党三役の `CandidateProblem` にはなかった。閣僚が先に与党の人材を取り、知事は入閣を拒否されて残るため、AI 補充で知事が三役の最上位候補になった（試験は与党の三役を見ていたので、与党党員で党首でない知事）。
6. 実際の人物ID（11〜24 のどれか）は、知事選と党所属の結果に依存するため実行しないと特定できない。修正後の試験の失敗文言には人物IDを出すようにした（下記）。

## 修正（最小）
| ファイル | 内容 |
|---|---|
| `Assets/Scripts/Core/Government/PartyExecutiveRules.cs` | `CandidateProblem`：現職知事を拒否「星系#N の知事と党三役は兼任しない」（共通入口 `TryAppoint` と AI の `AutoFill` が同じ判定を通る）。`Reconcile`：在任者が **首相に就いた**／**知事に就いた** とき三役を失職「星系#N の知事に就いた（三役を兼ねない）のため失職」（既存の「党首に就いた」と並ぶ）。空席理由の文言に首相・知事を追加。doc コメント更新。 |
| `Assets/Scripts/Core/Government/CabinetAppointmentRules.cs` | `Reconcile` の在任者ごとの失職に「**党首（党名）に就いたため失職（首相でない党首は入閣しない）**」を追加（`LeaderPartyOf` の private ヘルパ）。任命時の拒否（首相でない党首）と在任整理を一致させた。**首相本人は閣僚職に載らないため、正式な首相が党首でなくなっても失職・総辞職・職務執行にはならない。** |

入口の一貫性：
- **共通入口／AI**：`TryAppoint` と `AutoFill` はどちらも `CandidateProblem` を通る＝知事を選ばない。
- **年次整理**：`RunPoliticsTick` と `MaintainElectedPremier` の `RunCabinetAndPartyExecutives` → `Reconcile` が知事・首相・党首への就任で三役を失職させ、通知（`NotifyPartyExecutives` は失職の理由を括弧で付ける）。
- **読込**：`RestoreElectedOffices` は `SyncElectedGovernors` の後に `RunCabinetAndPartyExecutives(notify:false, autoFill:false)` を呼ぶため、保存に残った兼任も読込の整理で三役だけ失職する（通知・再任命なし・空席理由は台帳に残る）。
- 知事職そのもの（`LocalElectionState`／`GovernmentRegistry` の総督）は三役の整理では外さない。修正前の兼任を含む保存を読むと、読込の整理で「失職」の履歴が1件付く（通知は出ない）。

就任後に党首へ昇任した場合の点検：
- 党三役 → 党首：既存の `Reconcile` で失職していた（今回の EditMode で固定）。
- 閣僚 → 首相でない党首：**修正前は整理で解任されず兼任が残っていた**（前作業の結果文書の残件に明記されていた穴）。今回失職にした。
- 首相 → 党首でなくなる：自動失職にしない（試験で任免権が残ることを固定）。
- 党三役 → 党首でない首相：今回失職にした（首相は三役を兼ねない、という任命時の規則に整理を揃えた）。

## 追加・変更した試験（未実行）
EditMode `CabinetAppointmentRulesTests`（13 → 15件）：
- `PartyExecutives_GovernorRejected_GovernorOrPremierInOfficeLosesPartyPost_WithReason`：知事3 の幹事長任命を理由つきで拒否し状態不変／AI 補充で知事を飛ばす（2/4/5、同点は ID 小）／在任の政調会長4 が知事に就くと失職1件・理由・空席理由・政策調整の権限なし・知事の在任は残る・再整理0件・補充は6／幹事長2 が党首でない首相に就くと失職、党首1と首相2は動かさない。
- `Cabinet_MinisterBecomingNonPremierLeader_LosesPost_PremierLosingLeadershipKeepsOffice`：兵部大臣2 が与党党首に就くと失職（総辞職・職務執行なし）・理由・所管決裁の権限なし／大蔵大臣3 は続投／党首でなくなった首相1 の閣僚任免権は残る／再入閣は「首相でない党首」で拒否・再整理0件／党首2 が任命した幹事長4 が党首に就くと三役を失職。

PlayMode `CabinetPostsPlayModeTests`（1 → 2件）：
- 既存 `AnnualTick_…`：237行の直前に `GovernedSystemOf < 0`（「現職知事（人物#N）が党三役に就いた」）を追加＝失敗時に原因と人物IDが出る。政府役職0件の検査はそのまま残した（緩めていない）。
- 新規 `GovernorIsNotPartyExecutive_ConflictLosesPartyPost_OnAnnualTickAndLoad`：同じ試験世界で初年の政治 Tick → 全党の三役が知事でなく政府役職なし／党首でない党員の知事を見つけ `CandidateProblem` が知事を理由に拒否・知事職は1つ／修正前の状態（知事が幹事長）を直接書いて同じ年の政治 Tick → 三役だけ失職・履歴の理由・通知に「知事に就いた」・幹事長の権限なし・知事の在任と知事職は残る／知事が政調会長の状態を JSON 往復 → 再構築（`SeedGovernmentForQa`）で読込の整理が失職・空席理由・「党三役」の通知0・知事職は復元。セーブファイルは読み書きしない（文字列の往復のみ）。
  - 前提（知事が党首でない党員）は失敗した実行の状況から成り立つはずだが、成り立たなければ「前提：…」で落ちる。

既存回帰で見てほしい点：
- `ElectionGameIntegrationPlayModeTests`／`PartyLeadershipPlayModeTests` で、総裁選に勝った現職閣僚が首相でない党首になる年があれば、内閣の「失職」通知が1通増える（今回の仕様どおり）。
- 首相交代の年は総辞職が先に走るため、新首相＝新党首の元閣僚に「失職」は重ならない。

## 変更ファイル
- `Assets/Scripts/Core/Government/PartyExecutiveRules.cs`
- `Assets/Scripts/Core/Government/CabinetAppointmentRules.cs`
- `Assets/Tests/EditMode/CabinetAppointmentRulesTests.cs`
- `Assets/Tests/PlayMode/CabinetPostsPlayModeTests.cs`
- `docs/catalog/core-modules-catalog.md`（党三役の兼任の記述に知事・就任後の失職を追記）
- `docs/ops/cabinet-qa-fix-20260914-result.md`（本書）
- `docs/ops/claude-status.json`

## 未確認
- Unity コンパイル、EditMode／PlayMode、TestHarness `dotnet test` はいずれも未実行。
- 失敗時の人物ID・星系ID は実行ログが無く未特定（上記の消去法による推定）。

## 別残件（今回の修正には混ぜていない）
- **閣僚の権限が決裁デスク／稟議へまだ接続されていない。** `DecisionAuthorityDirector`（決裁デスク）と稟議の裁可判定は `GovernmentRegistry` 経路のままで、`CabinetAppointmentRules.Authority`（大臣の所管決裁・副大臣の委任）を読まない。次の小作業で接続する。
- 手動任免／上申の操作 UI、連立入閣、省職員と閣僚の兼務の分離（#156）は前作業の残件のまま。
