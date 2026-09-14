# integration-local-cloud-20260914 (a1) 結果

ブランチ：`integration/local-cloud-20260914`（HEAD a1a8ed16 ＋ origin/master c8a717d5〔PR2766 統治政策・PR2767 統治政策の稟議化〕のマージ途中）
実施：Claude（Read/Grep/Glob/Edit/Write のみ）。**コンパイル・テストは一切実行していない**（シェルなし）。コミット・push・マージもしていない。

## 1. RingiDirector.cs の衝突解消（3箇所）

両側の方針：
- **ローカル**：稟議との対応はカード（`PendingDecision.petitionId`/`friction`）に持たせる＝シーン往復で Director の辞書が消えても裁可が無視されない／決裁idは `DecisionDeck.NextDecisionId(DecisionIdBand)`／状況起案・判断材料・対象固定・提案者/決裁権者の記録（#67）／`DecisionResolutionRules.ClaimForApply` による1回だけの適用と `RecordResult`／未実装の効果を成功扱いしない。
- **リモート**：`SubmitGovernancePolicy`（地方箱への統治政策上申）と、裁可時に `Province.governancePolicy` を切り替える執行。

解消の内容（片側を捨てていない）：
- 衝突1・2：ローカルの状況起案（`TryRaiseFromSituation`〜`RaiseAgendaItem`〜`StampAttribution`〜`TitleFor`）をそのまま残し、その後ろにリモートの `SubmitGovernancePolicy` を追加。ただしローカルの安全策に合わせて次を変えた：
  - 決裁id：リモートの `nextDecisionId++`（ローカル側で削除済みのフィールド＝そのままではコンパイルエラー）→ `DecisionDeck.NextDecisionId(DecisionIdBand)`。
  - 稟議との対応：`pending` 辞書に書くのをやめ、カードに `petitionId`/`friction` を持たせる。星系と政策は効果キー `governance.policy.{systemId}.{(int)policy}` に固定（キーの形はリモートと同じ）。
  - 同時件数の上限：`pending.Count` → `ActivePendingCount()`（ローカルの数え方）。
  - 同じ星系の重複上申：`pending` の走査 → 決裁デスクの未解決カードを効果キーで判定する `HasPendingGovernanceFor`（シーン往復でも効く）。
  - `StampAttribution` を呼び、提案者・決裁権者・権限の根拠をカードに記録（#67）。
- 衝突3（`OnResolved`）：ローカルの流れ（台帳から稟議を引く→`ClaimForApply`→`Decide`→実行→`RecordResult`→通知）はそのまま。実行の部分だけ分けた：
  - 効果キーが統治政策なら `ExecuteGovernancePolicy`：`WorkflowRules.Execute`×`PetitionFlowRules.ExecutionFidelity`（リモートと同じ式）で、実行されたときだけ `Province.governancePolicy` を切り替える。対象の星系が無い、またはすでに自分の勢力のものでない場合は**失敗として記録し、別の星系には振り替えない**（稟議は実行済みにして在庫に残さない）。
  - それ以外はローカルと同じ（`RingiPipeline.ExecuteAndApply`＋`ExecuteBoardAction`）。
- `Pending` 構造体：自動マージでリモートの `isGovernancePolicy`/`targetSystemId`/`targetPolicy` が付いていたが、使わなくなったのでローカルの形に戻した（代入のないフィールドの警告を増やさないため）。

## 2. 自動マージの中で見つけた、意味の食い違い（修正済み）

**ローカルの「未実装の効果は実行不可」チェックが、リモートの統治政策上申を必ず止めていた。**
`DecisionDeck.Resolve` は、裁可のとき `DecisionEffectRegistryRules.IsImplemented(effectKey)` が false だと「実行不可」と記録し、`Resolved` を発火しない。`governance.policy.*` はどの登録にも無いため、そのままではリモートの機能が**裁可しても何も起きない**状態だった（`RingiDirector.OnResolved` まで届かない）。

対応（Core・最小限）：
- `GovernanceRules` に効果キーの窓口を追加：`PolicyPetitionKeyPrefix`／`PolicyPetitionKey(systemId, policy)`／`TryParsePolicyPetitionKey(key, out systemId, out policy)`（形が違う・星系idが負・政策が未定義なら false）。
- `DecisionEffectRegistryRules.IsImplemented` は、正しい形の統治政策キーを実装済みとみなす（壊れたキーは今までどおり実行不可）。
- EditMode テストを3件追加（`GovernancePolicyTests`）。

## 3. 自動マージしたファイルのレビュー

| ファイル | 判定 | メモ |
|---|---|---|
| `Core/Strategy/Province.cs` | 問題なし | `governancePolicy` の既定は民生＝旧セーブは従来どおり |
| `Core/Society/CampaignSaveData.cs` | 問題なし | `ProvinceSave.governancePolicy`(int・既定0=民生)。ファイル末尾の空行が1行消えただけ |
| `Core/Society/CampaignSerializer.cs` | 問題なし | 書き込みと読み込みが対応。未定義の値は民生に戻す |
| `Tests/EditMode/CampaignSaveFullPersistTests.cs` | 問題なし | 往復テストと、旧セーブが民生になるテスト |
| `Game/GalaxyView.Governance.cs` | 概ね問題なし | `SupplyReadinessOf`/`MilSupplyLowReadiness`（`GalaxyView.MilitarySupply.cs`）・`WorldMouse`/`NearestSystemDist`（`GalaxyView.Input.cs`）・`map`/`cam`/`provinces` はローカルにもある。`StrategySession.Provinces` は `provinces` と同じ参照。**`supplyOk: true` が実際の補給状況に変わった＝補給が不足している勢力の安定度の目標が下がる（−20）**。仕様どおりの変更だが、盤面の動きは変わる |
| `Game/GalaxyView.Input.cs` | **要確認（未修正）** | 下の懸念1 |
| `Game/SystemDetailPanel.cs` | 問題なし | 統治政策を1行表示するだけ |

## 4. 残っている懸念（未修正）

1. **P キーの二重割り当て**：`GalaxyView.Input.cs` が `kb.pKey` を直接読んで統治政策を上申する。一方、`GameInput` では P＝`人物名鑑切替`（共通）、Alt+P＝`生産観測切替`（共通）。自分の星系にマウスを合わせて P を押すと、人物名鑑が開くと同時に上申も出る。Alt+P でも上申が出る。さらに「`Keyboard.current` を直接読まず `GameAction` に足す」という規約（#107）にも反している。キーの選び直しは仕様の判断なので、今回は変えていない。**おすすめ**：`GameAction` に新しいアクションを足して空いているキーに割り当てるか、少なくとも Alt を押しているときは除外する。`SystemDetailPanel` の案内文「P で変更を上申」も合わせて直す。
2. **権限チェックとの関係**：統治政策キーの所管は `DecisionAuthorityRules.DomainOf` で内政になる。操作している人物が内政を決裁できる役職に就いていなければ、裁可は上申（`DecisionAuthorityDirector`）に回り、審査のあとに実行される。ローカルの #67 の仕様どおりだが、リモートの想定（すぐ切り替わる）とは体感が変わる。
3. **セーブ・ロード後**：`RingiDirector.Ledger`（static）は保存されないので、ロード後に残っているカードを裁可すると `OnResolved` が早い段階で return する。これは既存の税の稟議でも同じで、今回の統合で生じた問題ではない。
4. `CycleGovernancePolicyAtMouse` の `distance > 1.2f` は直書きの数値（リモートのコード。規約違反だが今回は触っていない）。
5. Game 層のコンパイルは未確認（`nextDecisionId` の残り・衝突マーカーの残りが無いことは grep で確認済み）。

## 5. 変更したファイル（今回 Claude が編集したもの）

- `Assets/Scripts/Game/RingiDirector.cs`（衝突解消＋統治政策の上申をローカルの安全策に合わせた）
- `Assets/Scripts/Core/Strategy/GovernanceRules.cs`（効果キーの窓口を追加）
- `Assets/Scripts/Core/Government/DecisionEffectRegistryRules.cs`（統治政策キーを実装済みとみなす）
- `Assets/Tests/EditMode/GovernancePolicyTests.cs`（3件追加）
- `docs/ops/integration-local-cloud-20260914-result.md`（新規・このファイル）
- `docs/ops/claude-status.json`

ワークフロー・シーン・アセット・セーブデータ・その他の自動マージしたファイルは編集していない。`RingiDirector.cs` は衝突が解消されたので、`git add` で解決済みにしてほしい（ChatGPT 側）。

## 6. おすすめの確認テスト

1. Unity のコンパイル（Game の `RingiDirector.cs`・`GalaxyView.Governance.cs`・`GalaxyView.Input.cs`）
2. EditMode：`GovernancePolicyTests`（8件＝既存5＋追加3）、`BattleAuthorityAndRequestTests`（`UnimplementedEffect_IsDetected`/`ImplementedEffects_AreRecognized`）、`CampaignSaveFullPersistTests`（`Provinces_RoundTrip`/`Provinces_OldSaveDefaultsToCivilPolicy`/`Provinces_RoundTrip_ThroughJson`）、`GovernanceTests`/`GovernanceAggregateTests`
3. PlayMode：`RingiFlowPlayModeTests`（ローカルの `ForceRaise`→裁可→実行の流れが壊れていないか）
4. TestHarness：`dotnet test`（Core の変更＝`GovernanceRules`/`DecisionEffectRegistryRules`）
5. 手動（Strategy）：自分の星系で P→決裁デスクに「（地方箱）」のカードが出る→裁可→`SystemDetailPanel` の統治政策が変わる／同じ星系に重複して上申できない／見送りでは変わらない／Strategy→Battle→Strategy と往復したあとに裁可しても反映される
   - **a2 で訂正**：キーは P ではなく **Alt+T**（下の7節）。

---

## 7. a2 追記（キー重複の解消・PlayMode 回帰テスト・保存の確認）

実施：Claude（Read/Grep/Glob/Edit/Write のみ）。**コンパイル・テストは一切実行していない**（実行は ChatGPT 側）。コミット・push・`git add` もしていない。

### 7.1 a1 の記述の訂正
- 4節の懸念1（P キーの二重割り当て）は「未修正」と書いたが、**a2 で修正した**（7.2）。
- 4節の懸念3「`RingiDirector.Ledger`（static）は保存されない」は、**結果としては正しいが理由が不正確**だった。実際は次のとおり（7.4）：保存の仕組み自体はあるが、保存しているのは別の入れ物。
- 6節5の手動確認のキーは P → **Alt+T**。

### 7.2 統治政策の上申キーを GameInput へ移した（P の重複を解消）
- 今までの割り当て（`GameInput` の表を確認）：P＝人物名鑑（共通）、Alt+P＝生産観測（共通）。上申は `GalaxyView.Input.cs` が `kb.pKey` を直接読んでいたので、P でも Alt+P でも上申が出ていた。
- 新しいアクション `GameAction.統治政策上申` を追加し、**戦略専用の Alt+T** に割り当てた。
  - Alt+T は未使用（共通・戦略・会戦のどれにもない）。素の T は会戦の「攻城戦術切替」だけで、戦略では何もしない。Game 層に T を直接読むコードも無い（grep で確認）。
  - `GameInput.WasPressed` は Alt/Ctrl を厳密に比べるので、**素の T・Ctrl+Alt+T では反応しない**。P と Alt+P には上申が一切載っていないので、**人物名鑑・生産観測を開いても上申は出ない**。
  - enum の値は**末尾に追加**（既存アクションの数値を動かさない）。説明文を追加したので、ヘルプ（`HelpOverlay` は `ActionsInContext` から自動生成）の戦略画面に「その他」として出る。
- `GalaxyView.Input.cs`：`kb.pKey` の直読みを `GameInput.WasPressed(GameAction.統治政策上申)` に置き換えた（直読みを1つ減らした＝#107 の規約どおり）。
- `SystemDetailPanel.cs`：案内文の「P」を `GameInput.KeyLabel(GameAction.統治政策上申)` で作るようにした（表示は「Alt+T」。割り当てを変えれば自動で追従）。
- `GalaxyView.Governance.cs`：コメントの「（Pキー）」だけ直した（処理は変えていない）。

### 7.3 テストの追加（未実行）
- EditMode `GameInputTests.GovernancePetition_IsAltT_InStrategyOnly_AndDoesNotShareP`：上申＝Alt+T・戦略専用・表示名「Alt+T」／人物名鑑は P（Alt なし）・生産観測は Alt+P のまま／P を使う割り当ては人物名鑑と生産観測だけ／会戦の操作一覧に上申が出ない／既定の割り当てに衝突なし。
- PlayMode `RingiFlowPlayModeTests`（既存のファイルに3件追加。製品のメソッドを飛ばさず、`SubmitGovernancePolicy` → `DecisionDeck.Resolve` → `RingiDirector.OnResolved` を実際に通す）：
  - `Governance_Approve_ChangesPolicyExactlyOnce`：上申しただけでは変わらない → 裁可で `Province.governancePolicy` が切り替わる、`Resolved` が1回だけ届く（未実装扱いで止まっていない）、カードは適用済み・結果＝実行、稟議は執行済。政策を戻してからもう一度裁可 → 弾かれ、`Resolved` も効果も2回目は起きない。
  - `Governance_Decline_LeavesPolicyAndAllowsResubmit`：見送りでは変わらない、カードは処理済み（結果＝対象外）、稟議は却下。決着後は同じ星系にまた上申できる。
  - `Governance_DuplicateForSameSystem_IsRejectedWhilePending`：未決の間は、同じ星系への上申が（政策が違っても）拒否され、カードが増えない。
  - テストの前提（明示的に固定）：自勢力（同盟）の星系を1つ `StrategySession.Provinces` に置く／地方箱（その星系）の信認を最大にする／`DecisionDeck.AuthorityCheck = null`（「誰でも裁可できる」後方互換の経路。権限による上申は #67 側の話なので対象外）／GalaxyView は置かない（摩擦は既定値 0.4、所有者チェックは省かれる）。変更した static は TearDown で元に戻す。
  - 官僚機構を通るかは確率なので、既存テストと同じくリトライで浮上させる（最大400回）。

### 7.4 「Ledger は保存されない」を実際のコードで確認した結果
- `CampaignSaveManager.SaveSession/LoadSession` は、稟議を **`StrategySession.Petitions` / `StrategySession.FleetPetitions`** に、決裁カードを **`StrategySession.Decisions`** に保存・復元している（#稟議完成②）。
- 決裁カードは `DecisionDeck.Queue => StrategySession.Decisions` なので、**カードは保存される**（`petitionId` 付き）。
- ところが `RingiDirector.Ledger` と `FleetRingiDirector.Ledger` はどちらも `static readonly ... = new PetitionLedger()` の**別の入れ物**で、`StrategySession.Petitions` に書き込むコードはどこにも無い（grep で確認）。つまり**保存される稟議の一覧は常に空**。
- ロード後に残っているカードを裁可すると：確定はされ `Resolved` も発火するが、`OnResolved` が `Ledger.Get(petitionId) == null` で return する＝**効果も結果の記録も無く、黙って終わる**（統治政策キーも同じ）。
- 分類：**今回の統合で生じたものではない既存の問題**（#稟議完成② の移行が途中）。根拠：`CampaignSaveManager.cs`・`FleetRingiDirector.cs` はこのマージで変わっていない／`GalaxyView.Persistence.ResetCampaignStatics` も Director 側の Ledger を消している。※ git が使えないので、ローカル側の衝突前の `RingiDirector.cs` がどうだったかは直接見ていない（上の根拠からの推定）。
- 直していない（保存の設計には手を入れない指示のため）。**最小の直し方の候補**：`RingiDirector.Ledger` と `FleetRingiDirector.Ledger` を `public static PetitionLedger Ledger => StrategySession.Petitions;`（艦隊は `FleetPetitions`）にする。使っている側（`.Clear()`/`.Get`/`.Add`/`.NextId`）はそのまま動く見込み。ただし、読み込み時に採番の続きが正しく戻るかは別途確認が要る。

### 7.5 a2 で変更したファイル
- `Assets/Scripts/Core/Foundation/GameInput.cs`（`GameAction.統治政策上申`＝Alt+T・戦略、説明文）
- `Assets/Scripts/Game/GalaxyView.Input.cs`（P の直読み → GameInput）
- `Assets/Scripts/Game/GalaxyView.Governance.cs`（コメントのみ）
- `Assets/Scripts/Game/SystemDetailPanel.cs`（案内文のキー表示を GameInput から作る）
- `Assets/Tests/EditMode/GameInputTests.cs`（1件追加）
- `Assets/Tests/PlayMode/RingiFlowPlayModeTests.cs`（3件追加＋TearDown で static を戻す）
- `docs/ops/integration-local-cloud-20260914-result.md`（この節）
- `docs/ops/claude-status.json`

ワークフロー・シーン・アセット・セーブデータは編集していない。新しいファイル（.meta が要るもの）は作っていない。

### 7.6 a2 のおすすめ確認（ChatGPT で実行）
1. Unity のコンパイル（Core の `GameInput.cs`、Game の `GalaxyView.Input.cs`/`SystemDetailPanel.cs`/`GalaxyView.Governance.cs`、PlayMode テスト）
2. EditMode：`GameInputTests`（全件。特に `Defaults_HaveNoConflicts` と新規1件）＋6節2のテスト
3. PlayMode：`RingiFlowPlayModeTests`（既存2件＋新規3件）
4. TestHarness：`dotnet test`（`GameInput` は Core）
5. 手動（Strategy）：自領星系で **P＝人物名鑑だけが開き、上申は出ない**／**Alt+P＝生産観測だけ**／**Alt+T＝上申のカードが出る**／素の T では何も起きない／星系詳細パネルの案内が「Alt+T」／H のヘルプに上申が出る

## 8. ChatGPT 統合検証結果（2026-09-14 JST）
- Core TestHarness全件：9780/9780合格、失敗0・スキップ0。ローカル.NET10.0.401でプロセス限定DOTNET_ROLL_FORWARD=Majorを使用。既存CS0219警告1件。
- Unity6000.6.0f1：コンパイル成功、PlayMode65/65合格、失敗0・スキップ0。固定QA・機能スイッチ・援軍・調整・士気原因・不退転/敗走・陣形保持の既存60件とRingiFlow5件。
- 統治政策の承認・見送り・重複上申は実際の決裁経路を使うPlayMode試験で確認。権限判定は試験条件で省略しており、権限付きの実画面操作を検証したとは扱わない。
- git diff --check 合格。ゲームアセット・シーン・保存データの変更なし。
- 証跡：ローカル outputs/integration-20260914/core.trx、playmode.xml、unity.log（Codex作業ディレクトリ側）。
- 通常会戦の描画・操作感・Alt+Tの実キーボード・所有変更後の操作は未確認。
- 稟議台帳保存先の不一致は既存の未修正課題。今回の合格は保存/ロードを含むゲーム全体の完成を意味しない。
