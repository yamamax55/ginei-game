# 閣僚権限を稟議・決裁の共通入口へ接続（#2768 #141 #67）— 結果

- task_id: cabinet-decision-bridge-20260914 / attempt_id: a1
- 基点: integration/local-cloud-20260914 3a0c9b7c
- 担当: プログラム担当 Claude（Read/Grep/Glob/Edit/Write のみ。git 操作・試験実行なし）
- 時刻: 時計を読むツールが無いため記録しない

## 原因
`DecisionAuthorityDirector.EvaluateEffectKey` は `GovernmentRegistry.GetOffices(actor)` の役職だけを `DecisionAuthorityRules.Evaluate` に渡していた。
内閣の政治任用（`PoliticsState.cabinet`＝大臣・副大臣・政務官と委任）は設計上 Registry に登録しない（登録すると軍事所掌が全軍の指揮権に数えられるため）ので、
所管大臣でも決裁デスクでは「役職に就いていない」扱いになり、上申先も Registry の役職者（宰相・宇宙艦隊司令長官）だけだった。
また上申の承認（`Conclude`）は権限判定を外して確定するだけで、審査中に決裁者が失職しても上申時点の権限のまま承認していた。
稟議の起票（`RingiDirector.StampAttribution`）も同じ判定を別に組み立てていた。

## 確定した判定（マッピング）
合成は `CabinetDecisionAuthorityRules.Evaluate`（Core・純ロジック・状態を変えない）。

1. **既存の役職判定を先に**：`DecisionAuthorityRules.Evaluate` で裁可できればそのまま返す（元首＝全所掌、宰相＝内政、既存の役職者の権限を狭めない。文民統制の制約も既存のまま）。
2. **所管省の解決**（`ResolveMinistry`）：大臣を置く省＝最上位の直下（`CabinetAppointmentRules.CabinetMinistries`）。
   - 明示の省IDがあればその省（所掌が効果キーと一致するときだけ）。現行の決裁カードは省IDを持たないので Game からは常に未指定（-1）。
   - 未指定なら `DecisionAuthorityRules.DomainOf(effectKey)` と `Ministry.domain` が一致する省が**ちょうど1つ**のときだけ。複数・皆無は -1＋理由（推測で複数省へ広げない）。
   - デモの省庁ツリー（太政官 ⊃ 式部省[内政]/民部省[内政]/大蔵省[財政]/兵部省[軍事]）での結果：
     | 効果キー | 所掌 | 所管省 |
     |---|---|---|
     | `tax.*` | 財政 | 大蔵省（一意） |
     | `fleet.establish/disband`・`mil.mobilize`・`mil.defend` | 軍事 | 兵部省（一意）＝軍事政策の決裁 |
     | `mil.offensive` | 軍事 | **作戦指揮＝閣僚の権限の対象外**（`IsOperationalCommand`） |
     | `welfare.*`・`governance.policy.*` ほか内政 | 内政 | 式部省/民部省が並ぶため**曖昧＝閣僚の権限を足さない**（既存の上申先＝宰相など、閣僚には理由を明示） |
     | `diplo.*` | 外交 | 所管省なし＝既存判定のまま |
3. **閣僚職による決裁**（`CabinetAuthority`）：`CabinetAppointmentRules.Authority(..., CabinetAction.所管決裁)` を使用。
   - 所管大臣＝裁可（職務執行内閣では期限まで所管の決裁のみ）。
   - 副大臣＝有効な `所管決裁` 委任（委任した大臣が今も在任・期限内・職務執行内閣でない）があるときだけ裁可。`所管政策` だけの委任は不可。
   - 政務官・党三役（閣僚職なし）・他省の大臣・首相（所管外）＝不可。
4. **上申先**：今も有効な所管大臣（`ValidMinisterOf`＝同じ判定を大臣本人に通して可のとき）。大臣が居ない・所管省が決まらない・内閣が無効なら既存の上申先（`GalaxyView.FindOfficeHolder`）、それも無ければ権限外。閣僚職にある人には理由を併記。
5. **決裁時点の再判定**（整理 `Reconcile` を待たない）：
   - `CabinetProblem`：政体が内閣を置かない／首相不在（職務執行でない）／首相交代・改選後の首班指名で前内閣が整理待ち／職務執行の期限切れ・新首相の組閣待ち。
   - `HolderLapseProblem`：死亡・拘束・他勢力・政治家でない・軍人・知事就任・党首就任・野党所属（`Reconcile` の失職条件を読み取りで先取り）。副大臣は委任した大臣の失職も見る。
   - 解任・委任の撤回は在任/委任の記録そのものが消えるので次の判定から効く。保存復元後も復元した `PoliticsState` とその時点の暦年で同じ判定。
6. **作戦指揮と国庫**：`mil.offensive` は閣僚の権限を足さず大臣へも上申しない（閣僚職にある人には「作戦指揮権を含まない」と併記）。国庫支出は決裁後の既存の執行（`RingiDirector` の稟議執行・予算）が担い、判定は可否だけ。

## Game の共通入口
- `DecisionAuthorityDirector.EvaluateFor(Person, effectKey)`（新・static）＝役職＋文民統制＋閣僚の合成。裁可（`AuthorityCheck`）・見込み表示（`TryPreviewAuthority`）・稟議の起票（`RingiDirector.StampAttribution`）が同じ判定を使う。
- 上申の承認：`Conclude` → `EvaluateDecider`（不在・プレイヤーと別勢力は権限外）→ `ConcludeApproval(d, name, auth)`（新・static）。裁可できれば既存どおり `AuthorityCheck` を一時的に外して `DecisionDeck.Resolve` で確定（効果は1回）、できなければ効果を出さずに差し戻して閉じる（`Settle`＋`ClaimForApply`＋理由の `RecordResult`）。
- `GalaxyView.CabinetDecisionContextOf(Faction)`（新）＝その時点の `PoliticsState`・省庁ツリー・名簿（軍人＋文民）・暦年（`ElectionYear`＝統一クロック）を束ねる。読み取りのみ（省庁のシードもしない）。内閣が無ければ null＝従来判定。
- 閣僚の Office を `GovernmentRegistry` に登録していない。

## 変更一覧
| ファイル | 種別 | 内容 |
|---|---|---|
| `Assets/Scripts/Core/Government/CabinetDecisionAuthorityRules.cs`（＋.meta） | 新規 | 上記の純判定 |
| `Assets/Scripts/Core/Government/CabinetDecisionContext.cs`（＋.meta） | 新規 | 判定材料の束 |
| `Assets/Scripts/Game/DecisionAuthorityDirector.cs` | 変更 | `EvaluateFor`/`ConcludeApproval`/`EvaluateDecider`/`CloseWithoutEffect`、`Conclude` の承認前再判定 |
| `Assets/Scripts/Game/GalaxyView.Cabinet.cs` | 変更 | `CabinetDecisionContextOf` |
| `Assets/Scripts/Game/RingiDirector.cs` | 変更 | `StampAttribution` を `EvaluateFor` に統一（未使用になったローカル変数を削除） |
| `Assets/Tests/EditMode/CabinetDecisionAuthorityRulesTests.cs`（＋.meta） | 新規 | Core 試験12件 |
| `Assets/Tests/PlayMode/CabinetDecisionBridgePlayModeTests.cs`（＋.meta） | 新規 | 共通入口の接続試験3件 |
| `docs/catalog/core-modules-catalog.md` | 追記 | 1行 |
| `docs/ops/cabinet-decision-bridge-20260914-result.md`・`docs/ops/claude-status.json` | 作業記録 | |

既存試験の削除・緩和なし。シーン/プレハブ/セーブ/ChatGPT レビュー JSON は未変更。

### 追加した試験
- EditMode `CabinetDecisionAuthorityRulesTests`：所管省の解決（一意/複数/皆無/明示ID）、大臣の裁可、他省大臣・政務官・党三役・無役→大蔵大臣へ上申、副大臣（委任なし/所管政策のみ/所管決裁/期限の年/期限切れ/他省/撤回）、死亡・解任（委任も失効・死亡大臣へ上申しない）、首相交代・改選・知事就任・野党移籍・他勢力（整理前でも不可）、職務執行（期限まで大臣可・委任不可・期限切れ・新首相）、元首/宰相の権限を狭めない・内閣 null は既存判定と同一、文民統制（軍人→文民大臣へ上申）、作戦指揮の除外、曖昧所掌の理由と既存上申先の維持、判定で状態を変えない（履歴・在任・委任・Registry）、保存往復後の同一判定と決裁年での期限・死亡。
- PlayMode `CabinetDecisionBridgePlayModeTests`：政務官の裁可→所管大臣へ上申（効果なし・カードの根拠は見込みと同じ判定）→大臣の承認で効果1回・フック復元・二重確定なし／審査中の大臣解任→効果なしで差し戻し・再解決しない／大臣本人は共通入口で直接裁可・効果1回。GalaxyView は置かず、Game 入口と同じ Core の合成を `AuthorityCheck` に差し込む。

## 未実行の試験（ChatGPT で実行をお願いします）
1. Unity コンパイル（Core・Game・EditMode・PlayMode）。
2. EditMode 新規：`CabinetDecisionAuthorityRulesTests`（TestHarness `dotnet test` でも対象）。
3. PlayMode 新規：`CabinetDecisionBridgePlayModeTests`。
4. 推奨の関連回帰：
   - EditMode：`CabinetAppointmentRulesTests`、`RingiCompletionTests`（権限判定）、`RingiAuthorityRoundTripTests`、`PersonRingiRulesTests`。
   - PlayMode：`RingiFlowPlayModeTests`（`StampAttribution`・`Resolve` の経路）、`CabinetPostsPlayModeTests`、決裁デスク/上申を通す既存 PlayMode 一式。
   - Editor QA：`GovernanceProposalQaMenu`（統治政策の上申＝内政キー。デモでは内政が曖昧なので宰相への上申のまま変わらない想定）。
   - TestHarness：`cd TestHarness && dotnet test -v q`（全件）。

## 挙動の変化（注意点）
- 財政・軍事政策の案件で、裁可できない人の上申先が「Registry の役職者」から「有効な所管大臣」へ変わる（大臣が居ない勢力・非民主政では従来どおり）。例：艦隊の設立/解散の稟議はプレイヤーが権限外なら兵部大臣へ上申される。
- 上申の承認は決裁者の再判定に通るため、旧セーブのカードで決裁者が既に役職を失っている場合は承認されず差し戻しになる。

## 残件（今回に含めない）
- 手動の閣僚任免・委任・撤回の操作 UI（現在は AI 組閣と Core の入口のみ）。
- 決裁カード/稟議へ所管省ID（明示の省指定）を載せる経路と、内政の複数省（式部省/民部省）を振り分ける省選択 UI。Core は `explicitMinistryId` を受けられるが Game からは未使用。
- 外交所掌の省がデモの省庁ツリーに無い（外交案件には閣僚の権限が付かない）。
- `CabinetAppointmentRules.Authority` の首相に対する制限と `DecisionAuthorityRules` の元首権限の食い違いの整理（今回は元首の既存権限を狭めない方針で据え置き）。
- 作戦指揮キーは `mil.offensive` のみ登録。今後、艦隊・軍団を直接動かす効果キーを足すときは `CabinetDecisionAuthorityRules.OperationalCommandKeys` へ追加が必要。
- Game 層のコンパイルは未確認（Unity を使えないため）。
