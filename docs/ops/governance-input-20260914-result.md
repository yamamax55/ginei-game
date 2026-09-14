# governance-input-20260914 / a1 結果（Issue109 残り：統治政策の入力と上申UI）

実施：Claude（Read/Grep/Glob/Edit/Write のみ）。**コンパイル・テストは一切実行していない**（実行は ChatGPT 側）。
コミット・push・`git add` なし。シェル・Unity 操作なし。シーン・プレハブ・アセット・ワークフロー・実セーブには触れていない。
新規 `.cs` 2本の `.meta` は、Unity（Edit モードで起動中）が作成直後に自動生成したものを、こちらの guid で上書きした（参照元はまだ無いので影響なしの見込み。Unity が再インポートする）。

---

## 1. 原因の切り分け

### 1.1 事実（コードで確認したこと）
| # | 事実 | 場所 |
|---|---|---|
| F1 | `GameInput.WasPressed` は「キーの `wasPressedThisFrame`」と「修飾の `isPressed`（フレーム末の押下中）」を比べていた。Alt を押す→T を押す→両方離す、が1フレームに収まると、キーは押された扱いなのに Alt は離れているので **Alt+T と一致しない** | `Core/Foundation/GameInput.cs`（修正前 359-361 行） |
| F2 | `CycleGovernancePolicyAtMouse` は星系との距離が **固定 1.2 を超えると何も通知せずに return**。星系情報（I キー）は `Mathf.Max(1.2f, ClickRadiusFor(id))` で要塞星系の大きな当たり判定まで拾う | `GalaxyView.Governance.cs`（修正前 109-110 行）／`GalaxyView.Input.cs` 17 行 |
| F3 | 同関数は星系が無い・内政データが無い場合も**黙って return**。`SubmitGovernancePolicy` が -1 を返す理由（国家状態なし・上限・重複・官僚機構で停止）も区別せず、官僚機構で止まった場合は「握り潰し/黙殺」と「受理されなかった」の2通知が出ていた | 同上 |
| F4 | `GalaxyView.Update` は決裁ボード・イベント・終了画面・システムメニュー表示中は `HandleKeys` まで到達しない（Alt+T はここで止まる）。文字入力欄のフォーカスは Alt+T では見ていなかった | `GalaxyView.cs` 480-487 行 |
| F5 | **P（人物名鑑）は `GalaxyView` を通らない**。`PersonObserverOverlay.Update` が直接 `GameInput.WasPressed(人物名鑑切替)` を読む。P には修飾が無く、修正前の判定で P が落ちるのは「Alt か Ctrl が押下中と見なされている」ときだけ | `PersonObserverOverlay.cs` 73 行 |
| F6 | 星系情報パネル（`SystemDetailPanel`）を開く手段は I キーのみで、マウスだけで統治政策を上申する経路は無かった | `SystemDetailPanel.Show` の呼び出し元は `OpenSystemInfoAtMouse` だけ |
| F7 | 権限：主人公（`ProtagonistCareerDirector.Protagonist`）は `PersonRole.軍人`。統治政策の効果キーは所掌＝内政。`DecisionAuthorityRules.Evaluate` は「軍人×政治案件」を、政体が軍部優位/未分化でない限り**役職より先に**裁可不可とし、国家の内政を持つ役職者（宰相など）がいれば上申、居なければ権限外にする | `DecisionAuthorityRules.cs` 94-106 行／`CivilianControlRules.cs` 38 行 |
| F8 | 宰相（`civilOffices`）は開幕では空席で、年境界の `RunCivilAppointmentTick` が叙位済みの文民から任命する。任命は保存されず、再開時は `SeedGovernment` で作り直され宰相は次の年境界まで空席 | `GalaxyView.Government.cs` 160-236 行／`GalaxyView.Personnel.cs` 893 行 |

### 1.2 推測（未確認）
- **Alt+T が反応しなかった件**：F1（短い和音の取りこぼし）は自動入力（SendInput 等で修飾と文字をまとめて送る）で起きやすいので、原因の候補ではある。ただし次の理由から**これだけが原因とは言えない**。
  - Input System の版によっては、**同じフレーム内の押す→離す**が `wasPressedThisFrame` に出ないことがある（T 自体を取りこぼす）。この版の挙動は確認していない。そうだった場合、今回の修正でも Alt+T は拾えない。
  - Windows では Alt 単独の押す→離すでメニューバーのモードに入り、キーボードのフォーカスが Game ビューから外れることがある（Editor 実行時）。
  - F2 により、要塞星系や星の縁にカーソルがあると黙って無視されていた。
- **P も反応しなかった件**：F5 のとおり P は修飾なしで、`GalaxyView` のモーダル判定も通らない。**修飾の判定だけでは説明できない**。ありうるのは (a) キー入力が Game ビューに届いていない（フォーカス）、(b) 直前の Alt が押下中のまま残った（Alt の離上が届かなかった）、(c) 人物名鑑が開かない別の理由。どれかは確かめていない。次の 4.3 の記録で切り分けられる。

## 2. 直したこと

| # | 内容 | 対応する事実 |
|---|---|---|
| A | **修飾キーを「このフレームに押されていたか」（押下中・押した・離した）で一致させる**。`ModifierSample` と純判定 `GameInput.BindingMatches`／`ActionsPressed` を追加し、`WasPressed` はこれを使う。合成の `altKey`/`ctrlKey` に加えて左右キーも見る。`IsHeld`（押しっぱなし系）は変えていない | F1 |
| B | **上申の入口を1つにした**：`RingiDirector.PreviewGovernanceProposal(systemId)`（読み取りのみ）と `RingiDirector.ProposeNextGovernancePolicy(systemId, out preview, out message)`。受付判定→既存の `SubmitGovernancePolicy`（稟議→決裁デスク）。**政策は直接変えない**。Alt+T もボタンもここを呼ぶ | F3, F6 |
| C | 受付判定の順序と文言を Core の `GovernanceProposalRules` に集めた：星系なし→管轄外→内政データなし→稟議機構なし→勢力状態なし→重複上申→決裁待ち上限。弾いたら理由を通知（`内政`・注意）。判定は通ったのに決裁デスクまで届かなかった場合も通知する | F3 |
| D | Alt+T の星系の拾い方を I キーと同じにした（`AcceptsPointerDistance`＝当たり判定と 1.2 の大きいほう）。近くに星系が無ければ「対象の星系がありません」を通知 | F2 |
| E | Alt+T を **文字入力欄にフォーカスがある間は無視**、**カーソルが UI 窓の上にある間は上申せず通知**（窓の下の見えない星を対象にしない）。盤面を止めるモーダル表示中は従来どおり `HandleKeys` に届かない。`HandleStrategyEscape` の入力欄判定も同じ `IsTextInputFocused` に寄せた（挙動は同じ） | F4 |
| F | **マウスの上申UI**：`SystemDetailPanel` に「― 統治政策の上申 ―」欄を追加。対象・現在の政策→次の政策・上申できない理由・**決裁の見込み**（あなたが裁可できる／誰へ上申される／いまは裁可できない＋根拠）を表示し、「統治政策の変更を上申」ボタンは B の入口を呼ぶ。できない時はボタンを無効（灰色）にして理由を出す。押した結果（成功・理由）を欄の下に出す。表示は 0.5 秒（実時間）ごとに読み直す（所有・決裁待ちの変化を反映）。押した後はボタンの選択を外す（Space/Enter で押し直されない）。モーダル表示中は押しても上申しない | F6, F7 |
| G | マウスだけで開ける入口：星系をダブルクリックで開く星系図（`SystemMapWindow`）のタブ列に「星系情報・統治政策の上申…」ボタンを追加（`GalaxyView.OpenSystemInfo(id)` を新設し I キーと共用） | F6 |
| H | 決裁の見込み：`DecisionAuthorityDirector.TryPreviewAuthority(effectKey, out result)`。裁可時と同じ判定（`EvaluateEffectKey`）を呼ぶだけで、上申も確定もしない。権限判定が差し込まれていなければ false（誰でも裁可できる状態） | F7 |
| I | QA：既存の「QA: 入力診断 生入力の記録」に**キー押下の記録**を追加（押したキー・Alt/Ctrl の押下中/押した/離した・入力コンテキスト・`Application.isFocused`・`GameInput.ActionsPressed` で解決したアクション）。**記録のみで何も発火しない** | 1.2 の切り分け |

変えていないもの：キー割当（Alt+T／P／Alt+P）、権限判定の中身、稟議・決裁・執行（`SubmitGovernancePolicy`／`ExecuteGovernancePolicy`）、保存形式。OS 全体のフックは使っていない。

## 3. 変更ファイル

本体
- `Assets/Scripts/Core/Foundation/GameInput.cs`：`ModifierSample`、`BindingMatches`、`ActionsPressed`、`WasPressed` の修飾判定、`using UnityEngine.InputSystem.Controls`
- `Assets/Scripts/Core/Government/GovernanceProposalRules.cs`（新規）＋`.meta`
- `Assets/Scripts/Game/RingiDirector.cs`：`PreviewGovernanceProposal`／`ProposeNextGovernancePolicy`
- `Assets/Scripts/Game/GalaxyView.Governance.cs`：`CycleGovernancePolicyAtMouse` を入口呼び出しに置換
- `Assets/Scripts/Game/GalaxyView.Input.cs`：`OpenSystemInfo(int)`（public）・`IsTextInputFocused()`・I キーの拾い方を共通判定へ
- `Assets/Scripts/Game/SystemDetailPanel.cs`：上申欄・ボタン・定期更新、枠の高さ 600→700、本文を自動縮小（12〜21pt）
- `Assets/Scripts/Game/SystemMapWindow.cs`：入口ボタン
- `Assets/Scripts/Game/DecisionAuthorityDirector.cs`：`TryPreviewAuthority`／`EvaluateEffectKey`（`Evaluate` はこれを呼ぶだけ）
- `Assets/Editor/InputDiagnosticsQaMenu.cs`：キー押下の記録

テスト・検証基盤
- `Assets/Tests/EditMode/GameInputTests.cs`（+7）
  - `ModifierSample_ActiveThisFrame_CoversHeldPressedReleased`
  - `AltT_ShortTap_AltReleasedInSameFrame_StillMatches`（短い押す→離す、前から押していた Alt を同フレームで離す、押しっぱなし、T が押されていないフレーム）
  - `AltT_WrongModifier_DoesNotMatch`（Alt なし・Ctrl+Alt・短い Ctrl 混在）
  - `AltT_WrongContext_DoesNotMatch`（会戦・タイトル・共通）
  - `KeyT_EachPress_FiresExactlyTheExpectedAction`（戦略の Alt+T＝上申だけ、戦略の T＝なし、会戦の T＝攻城戦術だけ、会戦の Alt+T＝なし）
  - `KeyP_AndAltP_StayOnPersonAndProduction_OneActionPerPress`（戦略・会戦で P＝人物名鑑だけ、Alt+P／短い Alt+P＝生産観測だけ、Ctrl+P＝なし）
  - `ActionsPressed_NullBindings_ReturnsEmpty`
- `Assets/Tests/EditMode/GovernanceProposalRulesTests.cs`（新規・6件）＋`.meta`
  - `NextPolicy_CyclesInDeclarationOrder`／`AcceptsPointerDistance_UsesLargerOfClickRadiusAndMinimum`／`Evaluate_AllConditionsMet_Accepts`／`Evaluate_ReportsFirstFailingCondition_InOrder`／`RejectionText_IsEmptyOnlyWhenAccepted_AndNamesTheSystem`／`Preview_CanSubmit_OnlyWhenNoRejection`
- `Assets/Tests/PlayMode/RingiFlowPlayModeTests.cs`（+1）
  - `Governance_SharedProposeEntry_ValidatesThenSubmitsNextPolicy`：Alt+T とボタンが共有する入口で、稟議機構なし／管轄外／地図に無い星系はカードを作らず理由を返す→通れば「次の政策（民生→動員）」のカード→未決の間は重複で弾く→裁可で1回だけ変わる→次の見込みは動員→弾圧。**権限は既存テストと同じく `AuthorityCheck=null` 固定**（権限の判定はこのテストの対象外）。実セーブには触れない
- `TestHarness/Stubs/UnityStubs.cs`：`UnityEngine.InputSystem.Controls.ButtonControl`（`wasReleasedThisFrame` 追加）／`KeyControl : ButtonControl` を実 Input System と同じ名前空間・継承に。`Keyboard` に左右の Alt/Ctrl
- `docs/catalog/core-modules-catalog.md`：1項目追記

## 4. 実画面での確認手順（ChatGPT／操作者）

### 4.0 前提・注意
- 実行前に `persistentDataPath` の `campaign_save.json` と `setup_save.json` を退避する（F5・メニューのセーブは同じファイルに書く）。
- **QA 入力（自動入力・メニュー代行）と実キーボードの確認は分けて記録する**。4.3 の記録で「キーが届いたか」を残し、実キーボードでの確認とは別の結果として扱う。
- 決裁の権限（F7/F8）：主人公は軍人なので、統治政策（内政）は通常**自分では裁可できない**。宰相が在任していれば「裁可」を押すと宰相へ**上申**され、`reviewSeconds`（既定 30 game-秒）後に宰相の賛意で承認／却下が決まる（承認なら宰相の権限で確定し、政策が1回だけ変わる）。宰相が空席なら「権限外」で止まる。**上申パネルの「決裁の見込み」行で、押す前にどれになるか分かる**。
- 今回、権限を満たすための仕込み（宰相を任命する・権限判定を外す）は**追加していない**。通常の権限の門を緩めないため。宰相の在任は政府オブザーバ（Alt+G、または上メニュー「政府」）で確認できる。任命は年境界なので、空席なら速度 3 で年を越すまで進める。再開後は宰相が次の年境界まで空席に戻る（F8）ので、再開→年越し→裁可 の順になる。
- 決裁待ちの上限は 3 件（`RingiDirector.maxConcurrent`）。税の建白カードも数に入る。

### 4.1 マウスだけの上申
1. タイトル → 新規戦役（または退避済みセーブから「戦役を再開」）→ 戦略マップ。
2. 自勢力の星系をダブルクリック → 星系図の窓が開く。
3. タブ列の「星系情報・統治政策の上申…」を押す → 星系情報パネルが開く。
4. 「― 統治政策の上申 ―」欄に「対象: 星系名　現在「民生」→ 次「動員」」「上申: できます」「決裁の見込み: …」が出ていること。
5. 「統治政策の変更を上申」を押す → 欄の下に「…を上申しました（右下の決裁デスクへ）」、右下に「［上申］… が決裁待ち」、決裁デスクにカードが出ること。官僚機構で止まった場合は「決裁デスクまで届きませんでした」＋「［握り潰し］/［黙殺］」が出る（もう一度押せる）。
6. 続けて同じボタン → ボタンが灰色で「… への統治政策の上申が決裁待ちです」と出ること（カードは増えない）。
7. 他勢力の星系で 2〜3 → ボタンが灰色で「… は管轄外のため統治政策を上申できません」と出ること。

### 4.2 Alt+T（実キーボード）
1. 自勢力の星系（要塞星系の縁でも可）にカーソルを合わせ、Alt を押したまま T → 4.1 の 5 と同じ通知・カードが出ること。
2. Alt を短く叩くように Alt+T → 同じく上申されること。
3. 他勢力の星系で Alt+T → 「管轄外」の通知。星系から離れた空白で Alt+T → 「対象の星系がありません」。星系情報パネルの上で Alt+T → 「カーソルが窓の上にあります」。
4. P → 人物名鑑だけが開く／閉じる。Alt+P → 生産観測だけが開く（人物名鑑は開かない）。
5. 決裁ボード（K）を開いた状態で Alt+T → 何も起きないこと（盤面入力は止まる）。

### 4.3 キーが届いているかの記録（反応しない時）
1. Play 中に Unity メニュー「Ginei/QA: 入力診断 生入力の記録を開始／停止（Play中）」。
2. Game ビューをクリックしてフォーカスし、Alt+T と P を数回押す。
3. 「Ginei/QA: 入力診断 生入力の記録を出力（Play中）」→ Console の `[入力診断・生入力]`。
   - `[キー押下] T Alt=… 文脈=戦略 … → 統治政策上申` があれば、キーは届いて上申アクションに解決されている（以降は通知を見る）。
   - `[キー押下]` が無ければ、Play のフレームではキーが観測されていない（フォーカス・入力の届き方）。`isFocused=False` もここで分かる。
   - P で `Alt=押下中` と出ていれば、Alt が押されたまま残っている（1.2 の (b)）。
   - この記録は Update のフレームで見ているので、フレーム内で押す→離すが完結して Input System に出ない入力は写らない。

### 4.4 本題：上申→保存→タイトル再開→権限のある裁可
1. 4.1 か 4.2 で上申し、カードが決裁デスクにあることを確認（裁可しない）。
2. メニュー → セーブ（または F5）→「セーブしました」。
3. メニュー → タイトルへ戻る → 「戦役を再開」→ 右下にカードが復元されていること。
4. 対象の星系で星系情報パネルを開き、「決裁の見込み」を確認。
   - 「裁可すると 〇〇 へ上申されます」→ カードの「裁可する」を押す → 「［上申］… を 〇〇 へ上げました」→ 約 30 game-秒後に承認なら政策が次の政策に変わり、パネルの「現在」が更新される。却下なら政策は変わらずカードは閉じる（どちらになったかを記録）。
   - 「いまは裁可できません（… 適任者もいません）」→ 宰相が空席。速度を上げて年を越し、Alt+G で宰相の在任を確認してからもう一度 4 を見る。
   - 「あなたが裁可できます」→ そのまま裁可して政策が1回だけ変わること。
5. もう一度「裁可する」→ 弾かれ、政策が二重に変わらないこと。

## 5. テスト（すべて未実行）
1. Unity コンパイル（Core `GameInput`/`GovernanceProposalRules`、Game `RingiDirector`/`GalaxyView.*`/`SystemDetailPanel`/`SystemMapWindow`/`DecisionAuthorityDirector`、Editor `InputDiagnosticsQaMenu`、EditMode/PlayMode テスト）
2. TestHarness：`dotnet test`（スタブの `Controls` 名前空間変更を含む）
3. EditMode：`GameInputTests`・`GovernanceProposalRulesTests`
4. PlayMode：`RingiFlowPlayModeTests`（既存8件＋新規1件）

## 6. リスク・未対応
- **修飾の判定を広げた副作用**：Alt（または Ctrl）を離したのと同じフレームに別の文字キーを押すと、修飾つきとして扱う（例：Alt を離した直後の同フレームの P は Alt+P＝生産観測になり、人物名鑑は開かない）。1回の押下で2つのアクションは出ない。
- **同じフレーム内でキー自体が押されて離された場合**、この Input System の版で `wasPressedThisFrame` に出るかは確認していない。出ない版なら、短い和音は今回の修正でも拾えない（イベント単位で読む変更はしていない）。
- AltGr（右 Alt が Ctrl+Alt として届く配列）では Alt+T は一致しない（従来どおり）。
- 文字入力欄の判定は uGUI/TMP の InputField のみ。UI Toolkit の TextField にフォーカスがある場合は見ていない。
- 権限を満たした裁可の再現は、宰相の在任（年境界の任命・保存されない）と宰相の賛意に依存し、決定論ではない。確実に再現する QA の仕込みは作っていない（2 と 4.0 の理由）。
- `SystemDetailPanel` の枠を 700px にした。低い解像度では画面からはみ出す可能性がある（ドラッグで移動は可）。
- `PreviewGovernanceProposal` は「重複」を「上限」より先に判定する（`SubmitGovernancePolicy` 内の順とは逆）。出る理由の文言が変わるだけで、受け付けるかどうかは同じ。
- ヘルプ（H）・盤面の操作ヒント行には、新しいマウスの入口を書き足していない。

---

# 追加修正 governance-input-fix2-20260914 / a1

実施：Claude（Read/Grep/Glob/Edit/Write のみ）。**コンパイル・テスト・実画面操作は一切実行していない**（ChatGPT 側で実行）。
コミット・push・`git add` なし。シーン・プレハブ・ユーザーセーブへの書き込みなし。前タスクの未コミット変更はそのまま（上に追記・編集した箇所のみ変更）。
新規 `.cs` 5本の `.meta` はこちらで guid を指定して書いた（うち4本は Unity〔Edit モードで起動中〕が作成直後に自動生成したものを上書き・参照元なし）。

## 7. 実画面の証拠（ChatGPT 確認・受領）
- 星系図の「星系情報・統治政策の上申…」で開いた星系情報パネルが**星系図の背面**に出た。
- ポポカテペトル（自領）の民生→動員の上申ボタンはマウスで反応。3回とも**地方官僚機構で握り潰し**でカードは届かなかった。
- 入力診断で sky の Alt_L+t と p は**キー押下の記録ゼロ**、マウスの押下・離上は 4 件。SystemMapWindow が開いていた。
  → Update のフレームで観測されなかった、というだけで OS に届かなかった証明ではない。**今回のコード修正で実キーボードの短い操作が直ったとは言えない**（届くかどうかは未確認のまま）。

## 8. 今回の変更

### 8.1 星系情報パネルを前面に（必須1）
| 事実 | 対応 |
|---|---|
| `SystemDetailPanel` と `SystemMapWindow` の Canvas が**どちらも sortingOrder 950**。同順位の Canvas の前後は保証されず、星系図の後ろに描かれた | `SystemDetailPanel.SortingOrder = 960`（const）を Canvas と `UIWindowStack` の順位に使う＝星系図より必ず手前・観測窓（1090）より後ろ。Esc も星系情報→星系図の順に閉じる |
| 上申ボタンのモーダル判定に**艦隊編成画面（`FleetOrganizationPanel.IsOpen`）が無く**、`GalaxyView.Update` の早期 return と条件が違った | `GalaxyView.IsBoardModalOpen`（static）を新設し、`GalaxyView.Update`・上申ボタン・星系図の入口ボタン・`GalaxyView.OpenSystemInfo` が同じ判定を使う（`Update` の条件は同じ6つをそのまま移しただけ＝挙動は同じ） |

### 8.2 修飾キーの押した順（必須2）
**検討結果：懸念は実在する（コード上）。** 前タスクの `ModifierSample.ActiveThisFrame` は「このフレームに押下中・押した・離した」をまとめて見るので、
前フレームから押していた Alt を**離してから** P を押す、が同じフレームに収まると Alt は「離した」＝Alt+P（生産観測）と判定し、人物名鑑は開かない。
フレーム末の状態には押した順が無いので、この判定のままでは直せない。

対応（イベントの順序を保つ）：
- **Core `KeyChordLog`**（純データ・TestHarness 対象）：押下を「キー・その瞬間の Ctrl/Alt・フレーム番号」で積む。古いフレームは積むときに捨て、1フレーム 64 件で打ち切り件数を数える。
- **Core `GameInput.ChordLog`／`ChordMatches`**：`WasPressed` は、記録にそのキーの押下があれば**押した瞬間の修飾**で判定し、無ければ従来のフレーム状態（`ModifierSample`）で判定する。キー割当・`IsHeld` は変えていない。
- **Game `KeyChordRecorder`**（static・Play 開始で自動差し込み）：`InputSystem.onEvent` のキーボード状態イベントを届いた順に読み、「イベント直前は離れていて、適用後は押されている」キーを、そのイベント適用後の左右 Alt/Ctrl と一緒に記録する。エディタ用更新のイベントは記録しない。**入力を消費・改変しない・OS フックなし・権限の迂回なし**。Unity 専用の読み取りはここだけで、Core 側は `Key` とフレーム番号しか知らない（TestHarness のスタブに `Time.frameCount` を1行追加）。
- **入力診断**（既存 QA メニュー）に `[イベント順の押下]` 行を追加（押した瞬間の Alt/Ctrl と解決したアクション）。記録のみ。

前提（未確認・Unity の試験で確かめる）：`onEvent` はイベントを状態へ書き込む**前**に呼ばれる／キーボードのイベント処理時の `Time.frameCount` は同じフレームの `Update` と一致する。どちらかが違えば、下の PlayMode 試験のうち「Alt 解除後の bare P」が落ちる（記録が使われずフレーム状態の判定になるため）。

### 8.3 統治上申→裁可の実画面 QA 条件（必須3）
`Assets/Editor/GovernanceProposalQaMenu.cs`（Editor 専用・**戦略マップの Play 中だけ**メニューが有効）
- `Ginei/QA（Play中のみ・保存しない）/統治上申/裁可まで通る条件を整える（戦略・Play中）`（390）
- `Ginei/QA（Play中のみ・保存しない）/統治上申/いまの条件と見込みを出力（戦略・Play中）`（391・読み取りのみ）
- `Ginei/QA（Play中のみ・保存しない）/統治上申/QA条件を元に戻す（戦略・Play中）`（392・適用中のみ有効）
- ※当初は `Ginei/QA: 統治上申 …` として Ginei 直下に置いたが、直下が約100件で3項目が画面外（y≈1541px 等）になり UIA クリックが失敗したため、既存 QA（援軍38／回廊要塞40 等）と同じ親のサブメニューへ移した（14 節）。

「整える」は**何を変えるかをダイアログで見せ、確認してから**メモリ上の値だけを変える（ファイル保存なし）。変えるのは判定が**読む入力**だけで、判定そのもの（権限・官僚機構の生存ロール・稟議の決裁と執行）は無効化しない。政策は直接変えない。
| 変える値 | 理由（通常の判定式） |
|---|---|
| 対象星系の地方箱の信認・国家の傾聴度 → 1 | 生存確率＝傾聴 ×（1−摩擦）×（0.5＋0.5×正統性）`PetitionFlowRules.SurvivalChance` |
| 正統性・結束・希望 → 1 | 上式の正統性（`FactionLoyaltyRules.BaselineLoyalty`＝3つの平均） |
| 内政の省庁の省益 → 0.001 | 上式の摩擦（`MinistryRules.DomainFriction`）。0 にすると `RingiDirector` が既定 0.4 に戻すので 0 より大きい最小値。執行の骨抜き（`ExecutionFidelity`）も同じ摩擦 |
| 宰相（内政・国家の正式な文民役職）に、賛意が承認の閾値 0.5 に届く文民を任命 | 上申の承認＝`PetitionEscalationRules.Review`（賛意＝(運営＋情報)/200×0.5＋0.25 → 運営＋情報 ≥ 100 が必要）。任命は `GovernmentRegistry.TryAppoint`（`OfficeRules.CanHold` を通る）。在任の宰相が資格と賛意を満たせばそのまま使う。満たさなければ解任して候補を任命し、候補の位階が官位相当（従五位下）に足りなければ**位階だけ**上げる。**能力値は変えない**＝候補がいなければ何も変えずに理由を出す |

- 変えないもの：主人公の役職・文民統制・`DecisionDeck.AuthorityCheck`・`reviewSeconds`・乱数。操作者が特定できない／権限判定が無効／稟議機構が無い、なら**何も変えずに中止**（検証にならないため）。
- 生存確率は 99.9% 前後で **100% ではない**（乱数 `Random.value`）。止まったらもう一度押す。時間が進むと正統性などは通常の Tick で動くので、「出力」で現在の確率を確認できる。
- 適用すると通知（警告）「［QA］統治上申の検証条件を適用中…セーブしないでください」を出し、対象星系の星系情報パネルを開く。Console に `[QA 統治上申]`。
- 「戻す」は信認・傾聴度・正統性/結束/希望・省益・任命・位階を QA 前の値へ戻す。QA 中に上申・裁可した結果（カード・政策）は戻さない。Play を抜けると控えは捨てる。
- 小さな公開 API 追加（読み取り・QA 用）：`GalaxyView.PremierOfficeOf(Faction)`／`GalaxyView.PremierRank`、`DecisionAuthorityDirector.Favor(Person)` を public に、`RingiDirector.PreviewMinistryFriction(Faction, OfficeDomain)`。

## 9. 具体的な QA 操作（人／ChatGPT が押す）
前提：`persistentDataPath` の `campaign_save.json`／`setup_save.json` を退避。**QA 中は F5・メニューのセーブをしない**。
1. タイトル → 新規戦役（または退避済みセーブから再開）→ 戦略マップ。
2. （前面表示）自領の星系をダブルクリック → 星系図 → タブ列「星系情報・統治政策の上申…」→ **星系情報パネルが星系図の手前に出る**こと。Esc 1回で星系情報だけ閉じ、2回目で星系図が閉じること。
3. （モーダル）決裁ボード（K）を開いた状態で星系図の入口ボタン → 「いまは星系情報パネルを開けません」の通知。パネル表示中に K を開いて上申ボタン → 「いまは上申できません」。
4. Unity メニュー `Ginei/QA（Play中のみ・保存しない）/統治上申/いまの条件と見込みを出力` → 現状（宰相・上申先・通る確率・決裁の見込み）を記録。
5. `Ginei/QA（Play中のみ・保存しない）/統治上申/裁可まで通る条件を整える` → 確認ダイアログの内容（対象星系・宰相・位階の変更・確率）を記録 →「QA条件を適用する」。
6. 開いた星系情報パネルで「決裁の見込み: 裁可すると 〇〇 へ上申されます」（または「あなたが裁可できます」）を確認。
7. 「統治政策の変更を上申」を押す → 右下にカード。「決裁デスクまで届きませんでした」なら押し直す（回数を記録）。
8. カードの「裁可する」→「［上申］… を 〇〇 へ上げました」。
9. 時計を進める（一時停止を解除）→ **30 game-秒**（`DecisionAuthorityDirector.reviewSeconds`・一時停止中は進まない）後に「［上申の裁可］〇〇 が … を承認しました」「［執行］…」→ パネルの「現在」が次の政策（民生→動員）に変わること。もう一度「裁可する」相当の操作で二重に変わらないこと。
10. `Ginei/QA（Play中のみ・保存しない）/統治上申/QA条件を元に戻す` → 通知「元に戻しました」。
11. （キー・任意）`Ginei/QA: 入力診断 生入力の記録を開始／停止` → Game ビューをクリック → Alt+T／P／Alt+P、Alt を押したまま離して直後に P → 出力。`[イベント順の押下]` が出ていれば Input System まで届いている。**どちらの行も出ないなら、この版でもキーはゲームに届いていない**（コード修正の効果とは別問題として記録）。

## 10. 変更ファイル（今回）
本体
- `Assets/Scripts/Core/Foundation/KeyChordLog.cs`（新規）＋`.meta`
- `Assets/Scripts/Core/Foundation/GameInput.cs`：`ChordLog`／`ChordMatches`、`WasPressed` を記録優先に、`ModifierSample` の注記
- `Assets/Scripts/Game/KeyChordRecorder.cs`（新規）＋`.meta`
- `Assets/Scripts/Game/SystemDetailPanel.cs`：`SortingOrder=960`、上申ボタンのモーダル判定を共通化
- `Assets/Scripts/Game/SystemMapWindow.cs`：入口ボタンのモーダル判定
- `Assets/Scripts/Game/GalaxyView.cs`：早期 return を `IsBoardModalOpen` に置換（条件は同じ）
- `Assets/Scripts/Game/GalaxyView.Input.cs`：`IsBoardModalOpen`、`OpenSystemInfo` のモーダル判定
- `Assets/Scripts/Game/GalaxyView.Government.cs`：`PremierOfficeOf`／`PremierRank`
- `Assets/Scripts/Game/DecisionAuthorityDirector.cs`：`Favor` を public に
- `Assets/Scripts/Game/RingiDirector.cs`：`PreviewMinistryFriction`
- `Assets/Editor/GovernanceProposalQaMenu.cs`（新規）＋`.meta`
- `Assets/Editor/InputDiagnosticsQaMenu.cs`：`[イベント順の押下]` の記録と読み方

テスト・検証基盤・文書
- `Assets/Tests/EditMode/KeyChordLogTests.cs`（新規・8件）＋`.meta`
- `Assets/Tests/PlayMode/GameInputChordPlayModeTests.cs`（新規・5件）＋`.meta`
- `TestHarness/Stubs/UnityStubs.cs`：`Time.frameCount`
- `docs/catalog/core-modules-catalog.md`・`docs/catalog/components-catalog.md`：各1項目
- `docs/ops/governance-input-20260914-result.md`（本追記）・`docs/ops/claude-status.json`

## 11. 必要な試験（すべて未実行）
1. Unity コンパイル（Core/Game/Editor/EditMode/PlayMode）。Editor の `[MenuItem]` 検証関数に2つの属性を付けている。
2. TestHarness：`cd TestHarness && dotnet test -v q`（`KeyChordLogTests` を含む・スタブ変更あり）
3. EditMode：`KeyChordLogTests`（8）・既存 `GameInputTests`・`GovernanceProposalRulesTests`
4. PlayMode：`GameInputChordPlayModeTests`（5：短い Alt+T／bare P の短押し／短い Alt+P／前フレームから押していた Alt+P／**Alt 解除後の bare P**）、既存 `RingiFlowPlayModeTests`
   - テスト用の仮想キーボードを足し、`InputSystem.QueueStateEvent` で1フレームに複数イベントを順に積む。実キーボード・OS には触れない。
   - ゲームビューのフォーカスに依らず処理させるため、テスト中だけ `InputSystem.settings.editorInputBehaviorInPlayMode`／`backgroundBehavior` を変え、TearDown で戻す（プロジェクトに InputSettings アセットは見当たらない＝ファイルには書かれない見込み。アセットがある環境なら変更がエディタ上で dirty になる可能性）。
   - 実行中に実キーボードを触るとイベントが混ざりうる。
5. 実画面：9 節。

## 12. 残件・リスク
- **実キーボードの入力がゲームに届くか**は未解決・未確認（sky の自動入力で記録ゼロ）。今回の修正は「届いたイベントの解釈」だけ。
- `KeyChordRecorder` の前提（8.2 末尾）は Unity で未確認。外れた場合はフレーム状態の判定に戻るだけで、前タスク以前より悪くはならない（Alt 解除後の P の誤判定が残る）。
- `KeyChordRecorder` は Input System のイベントを見るので、Input System が捨てたイベント（無効デバイス・フォーカス外）は記録されない。逆に、`onEvent` の後段で捨てられるイベントがあれば記録だけ残る可能性がある（未確認）。
- AltGr（右 Alt＝Ctrl+Alt）は従来どおり Alt+T と一致しない。
- QA 条件は通過確率を 99.9% 前後にするだけで決定論ではない。宰相の上申先は `FindOfficeHolder`（内政・国家の役職者のうち階級の高い人）なので、別の役職者がいればその人に上がる（「出力」の「実際の上申先」で確認）。
- QA 中にセーブすると変えた値が保存される（止める仕組みは入れていない）。
- `SystemDetailPanel` は 700px のまま（低解像度ではみ出す可能性・前回から変わらず）。

## 13. ChatGPTレビュー・検証実績（2026-09-14、fix2完了後）
- Claude CLI governance-input-fix2-20260914/a1 は終了コード0。claude-status task一致、review_ready、editing_stopped=true、revision2を確認。
- 差分レビュー：上申の処理入口統一、理由表示、盤面モーダル判定共通化、星系情報パネルsortingOrder960、イベント順の修飾判定、QAの正式宰相任命・通常稟議経路を確認。git diff --check成功。
- TestHarness Core **9806/9806合格、失敗0、スキップ0**。core-fix2.trxをoutputs/governance-input-20260914に保存。既存の未使用変数警告1件。
- Unity Editorの変更後ログ：最終Tundra build success、現在のコンパイルエラーなし。
- 実画面（fix2前）：Title→戦役再開→自領星系情報→上申ボタンまでマウスで到達。官僚機構による握り潰し結果を表示。裁可成功は未確認。
- 自動Alt+T/PはUpdate記録にキー押下なし、マウス記録4件。OS未到達の証明ではなく、InputSystemイベントでの再試験が必要。
- **未完了**：Unity GameInputChordPlayModeTests5件、RingiFlowPlayModeTests、fix2後前面表示・モーダル・QA条件→上申→裁可→返事→実際の政策変更。既存PlayMode68件合格は前回の保存修正版の証拠であり、今回の入力修正版の合格には流用しない。
- 実機操作阻害：Unityが最小化。sky.activate_windowは `user input was detected in this window; call get_window_state before continuing`、get_window_stateは `window is minimized`。状態再取得・復帰再試行後も不可のため、座標操作を停止。ユーザーがUnityを表示する必要がある。
- 元のユーザーセーブ2件は保存前バックアップとSHA256一致。QA条件はまだ適用しておらずゲーム内保存なし。
- 別途保存課題：政府役職は再開時SeedGovernmentで再構築、宰相が空席から年次任命となる。今回の操作修正の完了条件と分けて追跡する。
- 現在のプログラム差分は未コミット。Unity試験・実画面確認後に統合ブランチへ保存する。

## 14. QA メニューの置き場所変更（governance-qa-menu-20260914/a2）
- ChatGPT 確認済み（受領）：Core 9806 全件合格・Unity コンパイル成功・**星系情報パネルが星系図の前面に出ることを実画面で確認**（9 節 2 の前面表示）。Unity Play は停止済み。
- 問題：`Ginei` 直下が約100件あり、統治上申 QA の3項目が画面外（y≈1541px 等）で UIA クリックが window bounds 外となり失敗。
- 変更：`GovernanceProposalQaMenu` のメニューパス定数のみを `Ginei/QA（Play中のみ・保存しない）/統治上申/` 配下へ（親の表記は `ReinforcementQaMenu`／`FortressCorridorQaMenu`／`WindowInputQaMenu` と同一）。項目名・優先度（390〜392）・有効条件・動作は変更なし。
- 試験：未実行（Unity コンパイル・メニュー表示の実画面確認とも）。

## 15. ChatGPT追加検証（2026-09-14、QAメニュー整理後）
- governance-qa-menu-20260914/a2 のCLI終了コード0、task/attempt一致・review_ready・editing_stopped=trueを確認。3項目は既存QA親の統治上申サブメニューへ移動。
- Unity 6000.6.0f1 batchmode PlayMode **74/74合格、失敗0・スキップ0**。入力イベント5、稟議9、陣形保持17、士気ロック/敗走9、士気源6、固定会戦10、機能切替12、調整値6。
- 短いAlt+T/P/Alt+P、前フレームからAlt押下、Alt解除後のPをInputSystem状態イベントで確認。実OS自動入力が届く証拠とは区別する。
- 証拠：outputs/governance-input-20260914/playmode.xml・unity.log（ローカル作業領域）。Unity試験が一時変更したProjectSettingsのDefineは終了時に復元され、同ファイル差分なし。
- 星系情報が星系図の前面に表示されることはユーザー復帰後の画面で確認済み。
- 新規上申→裁可→返事→政策変更の実画面確認は、この追記時点ではまだ未完了。

## 16. 実画面の上申→裁可→執行の成功受領と、残った表示不一致の修正（governance-panel-refresh-20260914/a1）
- ChatGPT 確認済み（受領）：Core 9806 全件合格、Unity PlayMode 74 全件合格（入力5・稟議9 など）。通常画面で QA 条件を適用→新規上申1回→右下カード「裁可する」→所管の最高評議会議長（デモ）へ上申→時間を再開→承認通知・執行実効100%・上段の政策が「民生」→「動員」になることを確認。保存せず Play 停止。15 節の「未完了」はこれで完了。
- 残った不具合1（表示の食い違い）：`SystemDetailPanel` 下段の本文（`BuildInfo`）は開いた時（`Display`）だけ作られていた。開いたまま政策が変わると、上段（0.5秒ごとに読み直す上申欄）は「動員」、下段の「統治政策」は「民生」のままだった。
  - 修正：上申欄と同じ更新間隔（`governanceRefreshInterval`・実時間）で `RefreshShownSystem` を呼び、表示中星系の最新データから見出し・安定度バー・本文を書き直す。データは `GalaxyView.TryGetSystemInfo`（新設・読み取りのみ）から取る。`OpenSystemInfo` もこれを使うよう整理（開く条件・動きは同じ）。GalaxyView が無い時は上申欄と同じ `StrategySession.Map/Provinces` を、それも無ければ最後に受け取った参照を読み直す。本文は内容が変わった時だけ書き込む。
- 残った不具合2（重なり）：決裁の見込みが長いと、上申欄の最後の行が上申ボタンに重なっていた（FullHD・高さ700pxの枠）。原因は本文の必要高さが大きく、縦並びレイアウトが上申欄を必要高さより低く縮めていたこと。
  - 修正：本文は「残りの高さ」だけを使う（`preferredHeight=0`・自動縮小はそのまま）。上申欄と結果文は、折り返し後の必要高さを最小高さとして確保（`ReserveTextHeight`）。画面比率ごとの専用処理はなし。
- 変えていないもの：数値・稟議の判定・保存・命令・QA 条件・役職。シーン/プレハブ/設定/セーブの編集なし。
- 変更ファイル：`Assets/Scripts/Game/SystemDetailPanel.cs`／`Assets/Scripts/Game/GalaxyView.Input.cs`（`TryGetSystemInfo` の追加と `OpenSystemInfo` の整理のみ）／新規 `Assets/Tests/PlayMode/SystemDetailPanelRefreshPlayModeTests.cs`（＋.meta）／本書／`docs/ops/claude-status.json`。
- 試験（**未実行**）：`SystemDetailPanelRefreshPlayModeTests.OpenPanel_PolicyChanged_BodyMatchesGovernanceBlockAfterRefresh`＝パネルを開く→政策を直接「動員」に変える→実時間1秒待つ→上段「現在「動員」」と本文「統治政策: 動員」が一致し、本文に「民生」が残らない。Unity コンパイル、既存 PlayMode 74 件の回帰、実画面（開いたまま裁可→執行で下段が変わる／長い見込みでボタンと重ならない）も未確認。

## 17. 最終レビュー・実画面検証（ChatGPT、2026-09-14）
- Claude governance-panel-refresh-20260914/a1 は終了コード0、状態review_ready、editing_stopped=true、revision4。追加差分は表示の更新・高さの確保・読み取り共通化・回帰試験のみ。git diff --check成功。
- 最後の変更後、Unity PlayModeの表示更新1件・稟議9件・入力イベント5件、計15/15合格（失敗0・スキップ0）。証拠はローカル outputs/governance-input-20260914/playmode-panel.xml と unity-panel-retry.log。先の74件に含まれる入力・稟議14件を再確認し、表示更新1件を追加した結果であり、89件の独立試験とは数えない。
- 実画面：Title→戦役再開→一時停止→統治上申QA条件→ポポカテペトルの星系情報。長い決裁見込みが最後の行まで表示され、上申ボタンに重ならない。
- 上申を1回クリック→新規カード1件・同じ星系の再上申は無効→新規カードを裁可→時間再開。最高評議会議長（デモ）による裁可通知、執行実効100%を確認。
- パネルを閉じずに、上段「現在『動員』」、本文「統治政策: 動員」が一致することを確認。安定度も65%→56%へ更新。実際のGalaxyView経由の最新データ取得を画面で確認した。
- 一時停止してパネルを閉じ、星系上で自動Alt+Tを一度再試行したが、新規上申・画面反応は確認できなかった。InputSystem仮想キーボード5件合格とOS自動入力の成功は区別する。マウスの上申入口は通し検証済み。
- 保存せずPlay停止。campaign_save.jsonとsetup_save.jsonのSHA256は作業前バックアップと一致。QA条件・政策変更はユーザーセーブに保存していない。
- 別の残件：政府役職の保存・再開時再構築、裁可待ちの間に役職が変わった場合の権限再確認、盤面モーダルの実操作確認。Issue #109全体は完了扱いにしない。
