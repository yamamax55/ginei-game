# 内閣任免の操作メニュー（#141 #2768）結果 — task_id=cabinet-appointment-ui-20260914 attempt_id=a2

基点 b6f8715f。a1 は API 到達失敗（ENOTFOUND）で異常終了＝未完了。a2 で a1 の部分編集を確認して引き継ぎ、足りない部分だけを仕上げた（a1 の編集を二重に当てていない）。

## a1 から引き継いだ部分（確認のみ・再編集なし）
- `Core/Government/CabinetAppointmentRules.cs`：確認用の `CheckAppoint`/`CheckDismiss`/`CheckDelegate`/`CheckRevokeDelegation`。実行と同じ判定経路を `dryRun` で通すので、確認で出る理由と実行時の理由が食い違わない。
- `Game/GalaxyView.Cabinet.cs`：操作の入口 `CabinetOperationForPlayer()`（操作者は `PlayerCharacter()` だけ）、`CheckPlayerCabinet*`、`PlayerCabinetAppoint/Dismiss/Delegate/RevokeDelegation`（実行時にもう一度判定・任命/解任/撤回は理由が必須・通知あり）、`CabinetParamsInUse`、`CabinetPersonName`。年次の自動補充は空席だけを埋める（コメントで明記）。
- `Game/StrategyMapWindow.cs`：観測ウィンドウ「政治」タブに「内閣人事」（`CabinetAppointmentPanel.Toggle`）。
- `Game/RingiObserverOverlay.cs`：打ち切り表示の文言を「古い稟議履歴〔決着済・黙殺〕 N 件を容量整理」に変更（文言だけ）。

## a2 で追加・変更
- **新規** `Assets/Scripts/Game/CabinetAppointmentPanel.cs`（+ `.meta`）：内閣人事の画面。
  - 自動生成は Strategy シーンだけ。窓は非モーダル。タイトルバーは `WindowChrome`、Esc は `UIWindowStack` へ登録するだけ、一覧のスクロールバーは `UiScrollbars`（`CorpsOrganizationPanel` と同じ作り）。
  - 上部に表示する項目：操作者とその立場（首相／自分の職／閣外）、首相（不在ならその理由）、組閣年、在任数、暦年、職務執行内閣とその理由。
  - 職の一覧（スクロール）：省ごとに見出しを付け、大臣・副大臣・政務官の人物、所属党、国政当選回数（年功の区分）、就任年を表示。副大臣には委任の範囲と期限、大臣・政務官には役割、空席には空席理由を表示。
  - 候補一覧（スクロール）：名前で絞り込み、「適格者のみ／全員」を切り替えられる。行ごとに評価の理由（能力・当選・与党/党・派閥・評価点）と、適格かどうか（兼任できない理由などは `CandidateProblem` の文言）を表示。並びは適格者が先で、評価の高い順。上限は `maxCandidateRows`（既定80）で、超えた分は「ほか N 名」と件数を出す。候補が0人のとき・首相不在のときは、空席のままになる理由を出す。
  - 選択行：選んだ職、在任者、任命対象。
  - 操作の流れ：まず［任命／解任／副大臣へ委任／委任撤回を確認］で確認を出す。確認は `CheckPlayerCabinet*` の可否と理由で、権限外のときは権限を持つ人も表示する。［確定して実行］は判定が可で、かつ理由が必要な操作では理由が入力済みのときだけ押せる。確定すると `PlayerCabinet*` がその場でもう一度判定する。結果と失敗理由はメッセージ欄に出る。
  - 委任：範囲（所管決裁／所管政策／両方）と期限（今年＋0..`maxDelegationYears`）はボタンで切り替える。
  - 在任・委任・履歴は `PoliticsState.cabinet` にだけ書く（同じ入口経由）。独自の台帳や `GovernmentRegistry` への登録は作らない。テスト用の人物固定は本番の入口では使っていない。
  - 試験用の入口（`SelectForTest`/`ExecuteForTest` と値を返すプロパティ）：UI の選択を再現するだけで、操作者は差し替えない。
- `Game/GovernmentObserverOverlay.cs`：本文の上に「内閣人事を開く」ボタンを追加（`CabinetAppointmentPanel.Show`）。高さの調整値は `cabinetButtonHeight`。内閣の説明コメントを「操作は CabinetAppointmentPanel」に更新。観測画面は読み取り専用のまま。表示は `PoliticsState.cabinet` の実データを毎フレーム読むので、操作結果はそのまま反映される。
- **新規** `Assets/Tests/PlayMode/CabinetAppointmentPanelPlayModeTests.cs`（+ `.meta`）：接続試験4件。実 GalaxyView を使い、政府シードと年次の政治 Tick で組閣してから行う。
  1. メニューの生成：開閉できる、職一覧と候補一覧にスクロールバーがある、絞り込みと理由の入力欄がある、操作ボタンが4つある、操作を選ぶ前は確定できない。
  2. 首相：理由なしの解任は失敗、理由ありの解任は成功。そのあとメニューの確認→確定で任命が成功し、在任者・任命者・理由・履歴が台帳に載る。年次の政治 Tick を再実行しても手動で任命した在任者は差し替わらない。
  3. 政務官が操作者：任命は首相への上申扱いで失敗し、確認と実行の理由が一致する。解任も失敗。台帳と履歴は変わらず、メニューの確定ボタンも押せず、画面に「権限外」と出る。
  4. 大臣本人：最長を超える期限は「長すぎる」で失敗。今年＋1年は成功し、範囲・期限・委任者が記録される。政務官は委任できない。撤回は理由なしなら失敗、理由ありなら成功。
- `docs/catalog/components-catalog.md` に1行、ルート `CLAUDE.md` の索引にクラス名を追加。

## 実行していない試験・確認（ツール制約：Read/Grep/Glob/Edit/Write のみ）
- Unity のコンパイル（Game 層・PlayMode 試験アセンブリ）は**未確認**。PlayMode の asmdef は `UnityEngine.UI` を参照していないため、試験側からは UI の型を触らず値だけを受け取る形にした。
- 上記の PlayMode 4件、既存の Core/EditMode/PlayMode 試験は**すべて未実行**。
- 試験2の「年次の政治 Tick を同じ年に再実行する」確認は、Tick の冪等性を前提にしている。選挙日程などが原因で落ちた場合は、試験側の前提を見直す必要がある。
- 実際の画面操作（配置・見切れ・入力欄のフォーカス・1秒ごとの再構築中のクリックの手触り）は、ChatGPT 側で別途確認が必要。

## 残件・注意
- 党三役の操作、複雑な組閣交渉、主人公の政界転身は対象外（次票）。
- `CabinetAppointmentPanel.cs.meta` は、書き込み時点で既存ファイル（a1 が作った可能性あり）を上書きした。guid は `7c3e9a1f4b2d4e6a9f0c8b5d2e1a3c47`。シーン・プレハブからの参照がないことは grep で確認済み。
- Battle シーンで政府観測のボタンを押すと、メニューは生成されるが「戦略マップでのみ操作できます」と出るだけ。
- git の commit/push、実セーブ、シーン・プレハブの変更、ChatGPT レビュー JSON の編集はしていない。
