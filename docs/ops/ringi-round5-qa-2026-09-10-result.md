# 第5次 実装結果（task_id = `ringi-20260910-round5-qa`）

作業票＝[ringi-round5-qa-2026-09-10.md](ringi-round5-qa-2026-09-10.md)。
実施 2026-09-10 15:31〜15:52 JST（実時計）。**編集停止済み**。commit / push なし。

---

## 依頼A：安定した検証入口

### 何が問題だったか（作業票の観測の裏取り）

既存の `CorpsFormationQaMenu` は「軍団隊形が2軍団で独立して効くか」を見るための道具で、
**起動 → 艦隊を一時生成 → 仕込む → 発令** の4段構えだった。各段のあいだ盤面は動き続けるので、

- 仕込む前に部隊が沈む → `AnchorOf` が null → 「先にテストを仕込むを実行してください」
- 仕込めても部隊喪失後で合否を判定できない

また `LaunchTestBattle` は `BattleHandoff.FromCampaign` を立てないため
`FleetCommander.Mode` は **自由操作**（`BattleCommandModeRules.ModeOf(false)`）になる。
自由操作は `BattleChain.Everything` ＝全部隊を直接操作できる**明示的なモード**なので、
そこで合格しても**戦役の指揮系統の合格にはならない**（作業票の指摘どおり）。

### 追加したもの：`Assets/Editor/CommandAuthorityQaMenu.cs`（新規・Editor 専用）

既存の `CorpsFormationQaMenu` は**そのまま残す**（軍団隊形の検証はそちらの役目）。
指揮権限の検証はこの新しい入口に分けた。

| 保証 | 実装 |
|---|---|
| 1コマンドで最後まで組む | 会戦開始 → 艦隊が湧くのを `EditorApplication.update` で待つ → 軍団の割り当てまで自動 |
| 準備中に部隊が失われない | 起動と同時に `Time.timeScale = 0`。準備が終わるまで毎フレーム 0 を維持（生成側が戻しても押さえる） |
| 準備完了で一時停止 | `PauseManager.Pause()` を呼ぶ＝通常UIと状態が食い違わず **Space 一回で再開**できる |
| 戦役モードで組む | `FromCampaign = true` / `PlayerCommandsWholeFleet = false` / `PlayerCorpsName = "QA-自軍団"`。**自由操作の権限免除は使わない** |
| 三者を識別できる | `QA-自軍団` / `QA-他軍団` / `QA-敵軍団`。提督名（「QA自軍1（直接命令できる）」等）と Hierarchy のオブジェクト名（`QA_OWN_` / `QA_OTHER_` / `QA_ENEMY_`）の両方 |
| Editor 特権で合格にしない | **このメニューに命令を通す入口は無い**。あるのは「盤面の仕込み」と「読み取り専用の状態出力」だけ。合否は通常UI（クリック選択・右クリック）で出す |
| 保存に触れない | シナリオ・シーン・プレハブ・セーブへ一切書かない。仮の提督データは `ScriptableObject.CreateInstance` ＋ `HideFlags.DontSave` |
| 静的な状態を残さない | 撤去で提督データを元へ戻し、仮データを破棄し、`BattleHandoff` の指揮系統と `timeScale` と Editor フックを全部戻す。**Play を抜けるときは自動で撤去**（`playModeStateChanged`） |

盤面の内訳＝自軍団2隊／他軍団2隊／敵2隊。
他軍団を2隊にしたのは**混在選択の内訳（直接命令 1 隊／支援要請 1 隊）が見える**ようにするため。

### メニュー一覧（正確な名前）

| メニュー | 役割 |
|---|---|
| `Ginei/QA: 指揮権限 検証会戦を開始（戦役・Play中）` | 起動〜準備〜一時停止まで一気に |
| `Ginei/QA: 指揮権限 いまの権限を出力（Play中）` | 各隊の `直接命令 / 要請 / 不可` を読み取って出す（**命令はしない**） |
| `Ginei/QA: 指揮権限 検証手順をもう一度表示（Play中）` | 下の手順をダイアログで再表示 |
| `Ginei/QA: 支援要請 他軍団を協力的にする（承諾の再現・Play中）` | 他軍団の統率を 100 に（承諾側へ固定） |
| `Ginei/QA: 支援要請 他軍団を非協力的にする（拒否の再現・Play中）` | 他軍団の統率を 0 に（拒否側へ固定） |
| `Ginei/QA: 支援要請 返事を保留させる（失効の再現・Play中）` | 返事を 999 秒後にして検討中で止める |
| `Ginei/QA: 支援要請 返事の保留を解除（Play中）` | 既定（4秒で返事）へ戻す |
| `Ginei/QA: 支援要請 要請先を撃沈する（失効の再現・Play中）` | 他軍団の先頭を通常の被弾経路で沈める |
| `Ginei/QA: 支援要請 攻撃目標を撃沈する（対象消失の再現・Play中）` | 敵の先頭を通常の被弾経路で沈める |
| `Ginei/QA: 指揮権限 検証を撤去（Play中）` | 全部元へ戻す |

**統率 100 / 0 を選んだ理由**：応じる気 ＝ 統率×0.6 ＋ 士気×0.4 −（交戦中 0.25）、承諾は 0.5 以上。
士気の寄与は 0.2〜0.4（`FleetMorale.GetMoraleFactor` は 0.5〜1.0）なので、
統率 100 は最悪（低士気かつ交戦中）でも 0.55 で**必ず承諾**、統率 0 は最良でも 0.4 で**必ず拒否**。
士気や交戦状態に左右されず再現できる。

### 実機手順（通常UIで合否を出す）

Unity Play 開始 → `Ginei/QA: 指揮権限 検証会戦を開始（戦役・Play中）`
→ 準備完了ダイアログが出て一時停止。以降は**通常UIだけ**で判定する。

1. **自軍団**：`QA自軍1` をクリック → 右クリックで移動先を指定 → **動く**こと。
2. **他軍団**：`QA他軍1` をクリック → 右クリックで移動 →
   **直接は動かず**「…へ移動を要請しました（応じるかは相手の判断）」と出ること。
3. **敵軍団**：`QA敵1` をクリック → **選択できない**こと。
4. **混在**：`QA自軍1` と `QA他軍1` を範囲選択 → 移動 →
   「直接命令 1 隊／支援要請 1 隊」の内訳が出て、**自軍団だけ動く**こと。
5. **軍団隊形**：右クリックメニューから自軍団へ発令 → **通る**こと。
   他軍団へ同じ操作 → **通らず理由が出る**こと。
6. **解除**：他軍団を選び「軍団指定の解除」→ **通らず理由が出る**こと（第4次の修正点）。
   自軍団を選び同じ操作 → **通る**こと。
7. Space で再開してから、支援要請の4分岐（下）を確認する。
8. 終了時は `Ginei/QA: 指揮権限 検証を撤去`（忘れても Play 終了で自動撤去）。

---

## 依頼B：支援要請の結果と実行の整合

### 確認結果（ソース上の懸念は**実在した**）

`SupportRequestDirector.Update` は改修前、

```csharp
bool alive = r.targetFleet != null && r.targetFleet.IsAlive;   // 受け手の生存だけ
SupportRequestOutcome outcome = SupportRequestRules.Judge(alive, ...);
...
NotificationCenter.Push(... OutcomeText(outcome ...));          // ← 先に「応じました」
if (outcome == 承諾 && alive) Execute(r);                        // ← 後から実行、戻り値なし
```

だったので、次の3つが**すべて「応じました」で終わっていた**（実機再現はしていないが、経路として成立する）。

1. **攻撃目標の消失** … `Judge` は受け手の生存しか見ないので承諾になる。
   `Execute` は `if (weapon != null && r.attackTarget != null)` で黙って何もしない。
2. **必要コンポーネントの欠落** … `FleetMovement` / `FleetWeapon` / `Squadron` が無ければ黙って何もしない。
3. **陣形変更の失敗** … `squad.TryChangeFormation(...)` の戻り値を**捨てていた**。
   車懸かりは軍神専用（`FormationAccessRules`）なので、非軍神へ要請すると通らないのに成功に見える。

### 直したこと

**Core（新規）`Assets/Scripts/Core/Combat/SupportOrderExecutionRules.cs`**

- `enum SupportOrderExecution { 未実行, 実行, 対象消失, 手段なし, 実行不可 }`
- `IsCarriedOut` / `IsFailure` / `CanExecute`（**二重実行の防止**＝未実行かつ承諾のときしか通さない）
- `ResultText(outcome, execution, who, kind)`＝**承諾でも実行できていなければ成功の文面にしない**。
  失敗3種はそれぞれ別の文面（どこで止まったか実機で分かる）
- `IsNoteworthy`＝実行できたときだけ「情報」、それ以外は必ず「注意」
- `CannotRequestText`＝要請を出す前に対象が消えていたとき用（まだ誰も応じていないので「応じましたが」と書かない）

**Core（追記）`SupportRequestRules.Judge` のオーバーロード**

```csharp
Judge(bool targetAlive, bool orderTargetValid, float elapsedSeconds, float willingness, in SupportRequestParams p)
```

`orderTargetValid == false` なら**返事を待たずに失効**。従来の4引数版は
`orderTargetValid: true` へ委譲＝**後方互換**（既存20件のテストはそのまま通る）。

**Game `SupportRequestDirector`**

- `Request.execution`（初期値 `未実行`）を持たせ、`CanExecute` で**1件につき1回**しか実行しない
- `OrderTargetValid(r)`＝攻撃の要請だけ目標の `Squadron` → `FleetStrength.IsAlive` を見る
  （移動・陣形変更は対象が座標／陣形なので常に有効）
- **実行してから通知する**順序に入れ替え、文面と重要度を `SupportOrderExecutionRules` から採る
- `Execute` は `void` → `SupportOrderExecution` を返すように変更し、
  移動＝`FleetMovement` 欠落は `手段なし`／攻撃＝目標消失は `対象消失`・`FleetWeapon` 欠落は `手段なし`／
  陣形＝`Squadron` 欠落は `手段なし`・`TryChangeFormation` が false なら `実行不可`
- `Enqueue` でも出す前に対象を見て、すでに消えていれば**要請を受理しない**
  （「要請しました」と出してから黙って流れる、を作らない）

### 追加した回帰テスト（12件・`Assets/Tests/EditMode/SupportOrderExecutionTests.cs`）

対象消失は承諾でなく失効になる／検討中の段階でも即失効する／4引数版の後方互換（全経過秒で一致）／
実行できたかの区別／`未実行` は失敗ではない／**二重実行の防止**（実行済み・失敗済みは再実行しない）／
承諾以外では実行しない／**承諾でも実行できていなければ成功の文面にしない**／失敗3種が別文面／
承諾以外は従来の文面と一致／成功だけ「情報」で他は「注意」／要請前の対象消失は「応じましたが」と言わない／null 安全。

### 実機手順（支援要請の4分岐）

準備完了後に **Space で再開**してから：

| 分岐 | 手順 | 期待 |
|---|---|---|
| **承諾** | `QA: 支援要請 他軍団を協力的にする` → `QA他軍1` を選び右クリックで移動 | 数秒後「応じました」→ **実際に動く** |
| **拒否** | `QA: 支援要請 他軍団を非協力的にする` → 同じ要請 | 「断りました」→ **動かない** |
| **対象消失** | `QA: 支援要請 返事を保留させる` → `QA他軍1` を選び `QA敵1` へ攻撃を要請 → `QA: 支援要請 攻撃目標を撃沈する` | 「…への攻撃の要請は流れました」 |
| **失効** | `QA: 支援要請 返事を保留させる` → `QA他軍1` へ移動を要請 → `QA: 支援要請 要請先を撃沈する` | 「…への移動の要請は流れました」 |
| **重複** | 承諾状態で同じ要請を連打 | 「すでに出しています（返事待ち）」で増えない |
| **陣形の実行不可** | `QA他軍1` へ**車懸かり**を要請（非軍神） | 「応じましたが、その命令を実行できませんでした」 |

---

## Play 終了時の「Some objects were not cleaned up when closing the scene」

作業票の指示どおり、**今回のQA終了処理に関係するかだけ**を確認した。**関係する**。

- 原因は `CorpsFormationQaMenu.SpawnFleetsForTest`（旧 101 行目）の
  `clone.hideFlags = HideFlags.DontSave;`。
  `HideFlags.DontSave` は「保存しない」だけでなく
  **「新しいシーンをロードしても破棄されない」**性質を持つ（`DontSaveInEditor | DontSaveInBuild | DontUnloadUnusedAsset`）。
  Play 中のシーンはそもそもディスクへ保存されないので**この指定に利点は無く**、
  複製した艦隊が Play 終了時のシーン破棄から外れて残る＝あの警告になる。
- ユーザーは今回「艦隊を一時生成」まで実行して**撤去せずに Play を終了**しているため、条件が揃っている。
- 同時に、仮の軍団長（`ScriptableObject`）も撤去コマンドでしか破棄していなかった。

**対処（`CorpsFormationQaMenu.cs`）**

1. 複製した艦隊の `hideFlags` を `HideFlags.None` にした（＝通常どおりシーン破棄で片付く）。
2. `[InitializeOnLoadMethod]` ＋ `playModeStateChanged` で、
   **撤去し忘れたまま Play を抜けても**仮の提督データと複製艦隊を必ず破棄するようにした。
3. 新設の `CommandAuthorityQaMenu` も同じ後始末を最初から持たせた。

※ `WindowInputDiagnostics`（Game 層・窓入力診断）も `HideFlags.DontSave` の GameObject を作るが、
これは専用メニューで **`Enable()` を明示的に呼んだときだけ**生成される別系統で、今回のQAとは無関係。
今回の範囲を広げないため**触っていない**（同種の欠陥として記録だけ残す）。

---

## 検証結果

| 項目 | 結果 |
|---|---|
| 6アセンブリ コンパイル | 合格（Core / Data / Game / Editor / Tests.EditMode / Tests.PlayMode すべて `error CS` 0件） |
| TestHarness 全件 | **9,640件合格・0失敗**（第4次 9,628 ＋ 12件） |
| 既存セーブ | 未変更（`campaign_save.json` Sep 9 13:22 のまま） |
| v5 Windows ビルド | 未変更 |
| commit / push | 実施していない（`git log` は `5eb970f2` のまま） |
| 通常UIの指揮権限 | **緩めていない**（Editor から命令を通す入口を作っていない・`CommandOrderSource` の既定はプレイヤーのまま） |

### 変更ファイル

| ファイル | 種別 | 内容 |
|---|---|---|
| `Assets/Scripts/Core/Combat/SupportOrderExecutionRules.cs` | 新規 | 実行結果の区別・文面・二重実行の防止 |
| `Assets/Scripts/Core/Combat/SupportRequestRules.cs` | 変更 | `Judge` に `orderTargetValid` 版を追加（4引数版は後方互換で委譲） |
| `Assets/Scripts/Game/SupportRequestDirector.cs` | 変更 | 実行してから通知・実行結果を返す・対象の有効性・受理前の門番 |
| `Assets/Tests/EditMode/SupportOrderExecutionTests.cs` | 新規 | 回帰テスト12件 |
| `Assets/Editor/CommandAuthorityQaMenu.cs` | 新規 | 依頼Aの検証入口（Editor 専用・Play 中のみ） |
| `Assets/Editor/CorpsFormationQaMenu.cs` | 変更 | `HideFlags` の是正＋Play 終了時の自動後始末 |

いずれも `.meta` を作成済み。

---

## 実機未検証点（ChatGPT へ）

自動テストは合格したが**実機合格ではない**。以下はすべて Unity Play での確認が必要。

1. 上記A手順 1〜8（自軍団／他軍団／敵軍団／混在／軍団隊形／解除）。
   とくに **準備完了まで1隻も失われていないこと**と、
   `QA: 指揮権限 いまの権限を出力` が「モード＝戦役」「指揮軍団＝QA-自軍団」を示すこと。
2. 上記B手順（承諾・拒否・対象消失・失効・重複・陣形の実行不可）。
3. `QA: 指揮権限 検証を撤去` 後、および**撤去せずに Play を終了**した後に、
   「Some objects were not cleaned up when closing the scene」が**出ないこと**。
4. 第3次の手順15〜20、第4次の手順21〜26のうち未消化ぶん。

## 残件（第5次時点）

1. **`Judge` の「時間切れによる失効」は現在の配線では到達しない**。
   `expireSeconds` は必ず `replySeconds` 以上（`SupportRequestParams` がクランプする）で、
   返事は `replySeconds` の時点で必ず承諾／拒否として片付くため、
   `elapsedSeconds >= expireSeconds` の枝に入らない。
   実機で見える失効は「要請先が戦闘不能」「目標消失」の2経路だけ。
   QA では「返事を保留させる」で検討中に留めてから対象を消して再現する。
   *時間切れそのものを成立させるなら「返事が来ないことがある」判定が要る＝仕様の追加になるので、
   今回の範囲では実装していない。*
2. AI 勢力どうしの上申・支援要請は未実装。
3. 条約（外交状態・交易路）は未実装＝理由付きで実行不可のまま。
4. 支援要請は会戦内のみ。戦略側の他軍団への要請は未実装。
5. 要請の状態を一覧する UI は無い（通知のみ）。
6. `WindowInputDiagnostics` の `HideFlags.DontSave`（同種の欠陥・今回は範囲外）。

**編集停止**（2026-09-10 15:52 JST）。
