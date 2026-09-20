# 官僚人事メニュー（#141 次工程・後半）実装結果

task_id: `civilservice-appointment-panel-20260916` / attempt_id: `a1`
ベース: 52f48ce2（Connect civil service actions to ringi）

## 1. 何を作ったか

前コミットで開通した人事の共通経路（`GalaxyView.PreviewPlayerCivilServicePost` /
`SubmitPlayerCivilServicePost`）に、プレイヤーが**配属・異動・昇任・降任・解任**を操作する画面を接続した。

| 層 | ファイル | 内容 |
|---|---|---|
| Game | `Assets/Scripts/Game/CivilServiceAppointmentPanel.cs`（新規） | 戦略専用のコード生成UI。省庁→対象→理由→二段階確認→直接実行/上申 |
| Game | `Assets/Scripts/Game/StrategyMapWindow.cs` | 上メニュー「観測」→「内政」に「官僚人事」を1行追加 |
| 試験 | `Assets/Tests/PlayMode/CivilServiceAppointmentPanelPlayModeTests.cs`（新規・8件） | 開閉/Esc・スクロールバー・5種の Preview 引数・直接実行・上申・不可理由・確認解除・読み取り専用 |
| 文書 | `docs/catalog/core-modules-catalog.md` | `CivilServiceRingiRules` と官僚人事画面の案内を追記 |

`BureaucracyObserverOverlay`（Alt+K）は**変更していない**＝観測は read-only のまま（入口は上メニューで足りるため、
観測窓に操作ボタンを足していない）。

## 2. 画面の構成（上から）

1. タイトルバー「官僚人事」（`WindowChrome.AddTitleBarLayout`＝つかんで移動・×で閉じる）
2. ヘッダー：操作者と役職（首相／閣内の職／省内職位／官職なし）・勢力・暦年・在任者数・省庁数
   ＋「可否・上申先は共通入口が決める。この画面は判定しない」の断り書き
3. 操作種別：配属／異動／昇任／降任／解任（選択中を強調）
4. **省庁一覧（スクロール）**：省名（入れ子は `└`）・所掌・在籍/配属定員・段別の在任数/定員（課長・局長・次官）
5. 選択行：操作・省・対象・**就ける段**（画面が組む要求の中身）
6. 絞り込み（名前）＋「受付可のみ表示／不可も表示」トグル
7. **対象一覧（スクロール）**
   - ■ 選択省の在任者（職位・在職年・官位・考課）。昇任/降任/解任では**ここが対象**、配属/異動では参考表示（クリック不可）
   - ■ 候補（配属＝どの省にも在籍していない人物／異動＝他省の在任者）
   - 各行の右端に **Preview 由来**の判定（直接実行できる／上申になる／不可＋理由）
8. 理由入力（空欄可）＋「未入力時は既定理由『所定の人事（○○）』」の案内
9. **［内容を確認］／［直接実行する|上申する|実行できません］／［確認を取り消す］**
10. 確認欄（［見込み］→【確認】に変わる。人事内容・理由・直接実行/上申先を固定表示）
11. 実行結果の1行（色分け）
12. **直近の人事履歴（スクロール・読み取り専用）**：退任年・職位・人物・状態・理由（打ち切り件数と `historyDropped` も明示）

3つのスクロール領域はすべて `UiScrollbars.Attach`＝常時見えるバー。横に長い情報はセル分割（`MakeCell` の
アンカー比率）で、特殊な画面比率を前提にしない。幅・高さは `StrategyScreenLayoutRules` で MAP 内へ収める
（画面比率別の別版は作らない）。

## 3. 判定を持たない作り（要点）

- **表示と確定可否は `PreviewPlayerCivilServicePost` の戻り値だけ**で決める。候補行の適格/不適格も同じ Preview を
  1人ずつ引いた結果で、理由は Preview の文字列をそのまま出す（画面で言い換えない）。
- **確定は `SubmitPlayerCivilServicePost` だけ**を呼ぶ。`ok` なら直接実行、`canPetition` なら上申、それ以外はボタン無効。
  確定時に共通入口がもう一度判定する（確認の結果を信用して書き換えない）。
- 画面が自前で決めるのは「**どの要求を組むか**」だけ＝`BuildPlan`：
  配属→一般官僚／異動・解任→現職の段／昇任→1段上／降任→1段下。
  **最上段の昇任・最下段の降任・対象未選択・省未選択**は要求そのものが組めないので Preview を呼ばず、
  理由つきで確定不能にする（可否の判定ではなく「引数を作れない」こと）。
- 台帳（`FactionState.civilService`）・`Ministry.staffIds`・決裁カード・稟議台帳を画面から直接書き換えない。
  段ごとの定員表示にだけ `CivilServicePostParams.Default` を読む（表示専用。可否には使わない）。
- `FactionState.civilService` が null／省庁未編成／主人公不在などは `ShowUnavailable` で**壊さずに理由つきで操作不能**にする。

## 4. 二段階確認

- ［内容を確認］は **Preview が受け付けるときだけ**押せる。押すと確認欄が【確認】に変わり、人事内容・理由・
  直接実行/上申先が固定表示される。
- 最終ボタンは **確認済みのときだけ**押せ、キャプションが「直接実行する」／「上申する」／「実行できません」に変わる。
- **操作種別・省・対象・理由のいずれかを変えると確認は解除**される（理由欄は `onValueChanged` で即解除）。
- 確認していない状態で最終処理を呼んでも実行されず「先に［内容を確認］を押してください」を出す。

## 5. 実行後の表示

| 結果 | 表示 |
|---|---|
| 実行 | 緑「直接実行しました：{根拠}（台帳へ反映済み）」＋対象・理由をクリア |
| 上申 | 橙「上申しました：決裁#{decisionId}　決裁者 {名前}（人物#{id}）。**台帳はまだ変わっていません**＝右下の決裁デスクで裁可されると反映されます」 |
| 却下 | 赤「受け付けられませんでした：{理由}」 |

## 6. 終盤ラグ対策（スケーラビリティ規律）

- 一覧の再構築は `refreshInterval`（既定1.2秒・実時間）ごと。毎フレーム再計算しない。
- 候補の Preview 照会は `maxTargetEvaluations`（既定120名）で打ち切り、**照会しなかった件数を画面に出す**（黙って切らない）。
- 行数は `maxTargetRows`（60）・`maxHistoryRows`（40）で打ち切り、**隠した件数を表示**する。
- 並びは「直接実行→上申→不可、同順は人物ID昇順」の決定論。

## 7. 試験（PlayMode・8件／★未実行）

`CivilServiceAppointmentPanelPlayModeTests`（`CivilServiceRingiPlayModeTests` と同じ固定条件の同盟＋内閣＋
年次人事で初期化した台帳、`GalaxyView` は無効 GameObject＝Start を走らせない）。

1. `Panel_BuildsScrollableListsAndClosesThroughEscStack` — 生成・3一覧のスクロールバー・入力欄・5操作ボタン・
   `UIWindowStack.CloseTopmost()` で閉じる・Toggle
2. `Preview_ArgumentsFollowSelectionRules_ForAllFiveActions` — 5種の選択から Preview へ渡る
   省/行為/人物/**段**（配属=一般官僚・異動/解任=現職・昇任=1段上・降任=1段下）
3. `ImpossibleSteps_AreNotConfirmable_AndShowReason` — 最下段の降任・最上段の昇任・対象なし＝確定不能＋理由
4. `DirectExecute_ThroughPanel_UpdatesLedger` — 所管大臣が配属：確認前は確定不可・確認後に「直接実行する」・
   実行で台帳と `staffIds` が動き決裁カードは増えない・理由が履歴に残る
5. `DirectExecute_WithBlankReason_UsesDefaultReason` — 理由空欄でも受け付け、既定理由が履歴に残る
6. `Petition_ThroughPanel_ShowsDecisionIdAndLeavesLedgerUntouched` — 権限外は「上申する」・決裁id が番号帯内・
   決裁者＝所管大臣・画面に決裁idと決裁者・**台帳と staffIds は不変**
7. `ChangingSelectionOrReason_ClearsConfirmation` — 理由/対象/操作種別の変更で確認解除・未確認では実行されない
8. `Rebuild_IsReadOnly` — 再描画・表示切替・絞り込みで在任記録/履歴/決裁カード/稟議/配属が一切変わらない
9. `MissingLedger_DisablesOperationsWithReason` — `civilService = null` でも壊れず、理由つきで操作不能
   （※上の 8 件＋本件で計 9 メソッド。うち 8 が操作系）

既存試験は未編集（緩めていない）。**本作業票の指示によりテストは実行していない**＝未実行は合格ではない。

## 8. 変更していないもの

`CivilServicePostRules` / `CivilServiceRingiRules` / `CivilServiceState` / `CivilServiceAnnualRules` /
`GalaxyView` の共通経路 / `RingiDirector` / `DecisionAuthorityDirector` / `DecisionDeck` / 保存形式 /
シーン / プレハブ / 実セーブ / `BureaucracyObserverOverlay` / 既存試験。git 操作なし。

## 9. 残（親か次作業で）

- Unity でのコンパイル確認（Game 層2本＝新規パネル＋`StrategyMapWindow`）と PlayMode 9件の実行。
- `docs/catalog/components-catalog.md` の表と CLAUDE.md の「既存コンポーネント（索引）」への
  `CivilServiceAppointmentPanel` 追記は**許可ファイル外のため未実施**。
- AI 勢力の官僚人事は従来どおり年次処理（`CivilServiceAnnualRules`）のまま＝本画面はプレイヤー操作のみ。
- 観測の「官僚」（Alt+K）からの入口ボタンは任意のため未追加（上メニューのみ）。
