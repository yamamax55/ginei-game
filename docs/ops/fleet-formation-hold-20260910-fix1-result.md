# 陣形保持 初版レビュー差戻し — 対応結果（fix1）

Task: `fleet-formation-hold-20260910-fix1`
Issue: https://github.com/yamamax55/ginei-game/issues/2253（確定仕様1）
差戻し＝[fleet-formation-hold-20260910-fix1.md](fleet-formation-hold-20260910-fix1.md)／
初版＝[fleet-formation-hold-20260910-result.md](fleet-formation-hold-20260910-result.md)。
**編集停止済み**。commit / push なし。既存セーブ・v5・無関係な差分は保護。
**仕様2には進んでいません。**

---

## 1. 撤退中の陣形受付判定漏れ（必須）— 修正しました

### 何が漏れていたか

初版の `Squadron.RequestFormation` は `FleetStrength.IsRetreating || 士気敗走` しか見ていませんでした。
ところが軍団総退却の実際の流れは：

| 状態 | `IsRetreating` | 士気敗走 | `FleetAI.currentState` |
|---|---|---|---|
| 総退却の下令直後〜退却移動中 | **false**（`BeginRetreat` は<b>戦場端</b>で呼ばれる） | **false**（士気は正常） | **撤退** |
| 戦場端に到達して離脱 | true | - | 撤退 |

つまり**士気正常で退却移動している最中**は初版のどちらにも当たらず、
新しい陣形指定を**受理してスキルポイントを消費し**、次の軍団周期で解除される
＝**ちらつき＋費用の無駄**が起きます。指摘のとおりです。

### 直し方

**Core に判定を足しました**（`FleetFormationOrderRules`）：

```csharp
public static bool IsRetreatingState(bool withdrawing, bool routed,
                                     bool aiRetreating, bool corpsRetreatOrdered)
    => withdrawing || routed || aiRetreating || corpsRetreatOrdered;
```

`Squadron` はこの4つを集めて渡すだけです。
`aiRetreating` が「士気正常の退却移動中」を、`corpsRetreatOrdered` が
**「直接命令を維持していて撤退へ落とされない例外の艦」**を拾います。

**総退却の発令状態を専用に持たせました**（`BattlefieldCommandManager`）：

```csharp
private static readonly HashSet<string> corpsRetreatOrdered = new HashSet<string>();
public static bool IsCorpsRetreatOrdered(string corpsKey);
```

- 既存の `flowStates` に相乗りしていません。あれは `ApplyBattleFlow` が
  **敵不在で `flowStates.Remove` する**ので、退却中に問い合わせると消えている恐れがあります。
- **「総退却せよ」と判断が下った時点**で立てます（`any` の中ではありません）。
  隷下が全員直接命令中で1隻も撤退へ落とせなくても、**軍団としては退却を命じている**からです。
- 兵力が戻って `ShouldOrderRetreat` が false になれば降ろします。
- `Awake` のリセットに追加（前の会戦を持ち越さない）。

**直接の移動／攻撃命令は一切変更していません。** 拒否するのは新規の陣形保持だけです。

---

## 2. テストの実態を補強 — 書き直しました

指摘のとおり初版は「窓口を手で10回叩く」「到着を assert しない」でした。実経路に置き換えています。

| 指摘 | 対応 |
|---|---|
| 実AIの複数周期を保証していない | `DirectOrder_SurvivesRealAiDoctrineCycles`＝**敵を隣に置き**`autoFormation = true`・`searchInterval = 0.02f` にして**実際の `UpdateFormationDoctrine` を1秒間回す**。フレーム数も assert（試験が空回りしていない証拠） |
| 試験が空振りでない保証 | `WithoutHold_RealAiDoesChangeFormation`＝**同じ状況で保持が無ければ実AIが陣形を決める**ことを確認（対照） |
| 到着を assert していない | `Hold_SurvivesActualArrival`＝`IsMoving` が false になるまで待って**到着を assert**、続けて `OverrideKind == なし`（**直接命令の終了**）も assert してから保持を見る |
| 軍団総退却の実経路（士気正常・離脱前） | `CorpsRetreatMovement_RejectsFormationWithoutSpendingPoints`＝実 `BattlefieldCommandManager` に**実際に総退却を発令させ**、`IsRetreating == false` と `IsRouted == false` を**前提として assert** した上で、陣形指定が `撤退中` で拒否され**ポイントが減らない**ことを確認 |
| 直接命令の例外艦 | `CorpsRetreatOrdered_RejectsNewHoldForDirectOrderedFleet`＝直接命令の艦が総退却に巻き込まれない（既存の対照）ことを assert しつつ、**新規の保持だけ拒否**されることを確認 |
| 保持の解除 | `CorpsRetreat_ReleasesExistingHold` |
| 軍団隊形との分離 | `CorpsFormationBroadcast_DoesNotOverrideFleetHold`＝実 `BattlefieldCommandManager` の陣形発令が艦隊の保持を上書きしないことを確認 |
| 支援要請の受理経路 | `SupportRequest_AcceptedFormationBecomesHold`＝**本物の `SupportRequestDirector`** の受理→承諾を通し、保持になり AI に上書きされないことを確認 |

PlayMode は **14件**（初版9件 → 実経路中心に再構成）。
EditMode は撤退状態の4件と HUD 表示の1件を追加して **24件**。

### ★PlayMode 14件は未実行

Unity が要ります。前回同様 **ChatGPT 側で実行**してください
（`Window > General > Test Runner` → PlayMode → `FleetFormationHoldPlayModeTests`）。
コンパイルは通っていますが、**実行結果は未取得＝合格ではありません**。

---

## 3. 実機手順の前提 — 直しました＋狭いQA補助を1つ追加

指摘のとおり、初版の手順9は「支援移動中に敗走させる」QA（**支援要請の艦しか対象にしない**）を
直接命令の対照に使っており、手順10も他軍団の総退却なので直接命令艦の対照になりません。

**追加したQA補助（1つだけ）**

`Ginei/QA: 陣形保持 保持中の艦を敗走させる（Play中）`

- 前提＝**陣形を保持している艦がいること**（出どころは問わない＝直接命令でも支援要請でも使える）
- 触るのは**士気だけ**。保持の解除も撤退状態も書きません
- 未成立なら何もせず、成立条件（HUD が「（保持・…）」になること）を表示します

**直接命令の総退却の対照**は既存の
`Ginei/QA: 緊急中断 自軍団を総退却の兵力まで減らす（直接命令の対照）（Play中）` を使います
（こちらは元から `OwnCorps` × `直接命令` を前提にしているので適切です）。

修正後の手順は下の「実機手順」を参照してください。

---

## 4. 一括指定の対象 — 選択軍団に限定しました

初版は `ActorChain().corpsName` を使い、**空なら直接命令できる全艦隊**が対象でした。
総司令官だと全軍が対象になり、軍団メニューを開いた文脈と無関係の艦隊まで変わります。

修正後（`FleetCommander.ChangeFormationForCorps`）：

- 対象は**いま選択している艦隊が属する軍団**だけ。**複数選択なら選択された各軍団**
- その軍団の生存艦隊のうち **`RightFor(sel) == 直接命令`** のものだけに適用
- 選択に軍団所属の艦隊が無ければ「軍団に属する艦隊を選択してから実行してください」
- 対象0なら理由を表示（系統外が何隊あったかも出す）
- **対象ごとの失敗理由も通知**（黙って一部だけ変わらない、を作らない）
- 完了通知に軍団名・成功数/対象数・系統外の除外数を出す

---

## 補足への対応

| 補足 | 対応 |
|---|---|
| `Priority` が「直接4/支援3」なのに `CanAccept` は同順位の後勝ち＝矛盾 | **`Priority` を削除**しました（未使用・誤用防止）。優先順位は `CanAccept`（明示指定どうしは後勝ち）に一本化 |
| HUD の非保持が常に「自律」で軍団AI指示と区別できない | `Squadron.LastFormationSource` を追加し、`HoldText(hold, current, lastSource)` が**最後に決めた出どころ**を出すように変更。表示は「方陣（**軍団指示**）」「方陣（**自律**）」「紡錘陣（**初期**）」で区別できます |
| 状態ファイルが約30分更新されなかった | 今回は**受理時 / 指摘1・4完了時 / テスト・QA完了時 / 完了時**の4回更新しました |

---

## 検証結果

| 項目 | 結果 |
|---|---|
| 6アセンブリ コンパイル | 合格（`error CS` 0件） |
| TestHarness（EditMode 相当） | **9,676件合格・0失敗**（初版 9,672 ＋4件。HUD 表示1件を差し替え、撤退状態4件を追加） |
| PlayMode 14件 | **未実行**（Unity 必要） |
| 実機 | **未実施** |
| 既存セーブ | 未変更（`campaign_save.json` Sep 9 13:22 のまま） |
| commit / push | 実施していない（`git log` は `5eb970f2` のまま） |

### 変更ファイル（fix1 ぶん）

| ファイル | 内容 |
|---|---|
| `Assets/Scripts/Core/Combat/FleetFormationOrderRules.cs` | `IsRetreatingState` 追加／`Priority` 削除／`HoldText` に最後の指定元 |
| `Assets/Scripts/Game/Squadron.cs` | `IsRetreatingNow`（4条件）／`LastFormationSource` |
| `Assets/Scripts/Game/BattlefieldCommandManager.cs` | 総退却の発令状態を専用管理＋`IsCorpsRetreatOrdered` 公開 |
| `Assets/Scripts/Game/FleetCommander.cs` | 一括指定を選択軍団に限定＋失敗理由の通知 |
| `Assets/Scripts/Game/FleetHUDManager.cs` | `HoldText` の引数追加 |
| `Assets/Editor/SupportEmergencyQaMenu.cs` | 「陣形保持 保持中の艦を敗走させる」を追加 |
| `Assets/Tests/EditMode/FleetFormationOrderRulesTests.cs` | 撤退状態4件追加・HUD 表示を差し替え・`Priority` 削除に追随 |
| `Assets/Tests/PlayMode/FleetFormationHoldPlayModeTests.cs` | 実経路中心に再構成（14件） |

---

## 実機手順（修正版）

1. `Ginei/QA: 指揮権限 検証会戦を開始（戦役・Play中）` → Space で再開
2. `QA自軍1` を選択 →「陣形 ▸ 円陣」→ HUD が **「現在陣形: 円陣（保持・直接命令）」**
3. しばらく戦わせる → AI が陣形を変えないこと（保持表示が消えない）
4. 移動命令 → **到着後も**保持が残っていること
5. 非軍神へ「陣形 ▸ 車懸かり」→ 拒否の通知が出て陣形が変わらないこと
6. **`QA自軍1` を選択したまま**「軍団 ▸ 配下艦隊の陣形を一括指定 ▸ 方陣」→
   **QA-自軍団の艦隊だけ**が方陣になり、他軍団は変わらないこと。軍団隊形も変わらないこと
7. 「軍団 ▸ 軍団指定を解除」→ **艦隊の陣形保持は解けない**こと
8. 「陣形 ▸ 陣形の保持を解除」→ 表示が「（自律）」等に戻り、以後 AI が陣形を決めること
9. **【修正】** `QA自軍1` に陣形を指定して保持させ、直接の移動命令も出してから
   `Ginei/QA: 陣形保持 保持中の艦を敗走させる（Play中）` →
   **陣形の保持だけ解除**され、**直接命令の移動は続く**こと
10. **【修正】** `QA自軍1` に直接の移動命令＋陣形保持を掛けた状態で
    `Ginei/QA: 緊急中断 自軍団を総退却の兵力まで減らす（直接命令の対照）（Play中）` →
    **陣形の保持は解ける**が**移動命令は維持**されること。
    さらにこの状態で「陣形 ▸ 円陣」を指定 → **「撤退中のため変更できません」**で拒否され、
    スキルポイントが減らないこと（**指摘1の実機確認**）
11. 他軍団へ陣形変更を要請 → 承諾後 HUD が「（保持・支援要請）」になり軍団AIに上書きされないこと

---

## 実機未検証点

- **手順1〜11 はすべて未検証**、**PlayMode 14件も未実行**です。合格扱いにしないでください。
- とくに**手順10の後半**（総退却中の陣形指定が拒否され費用が減らない）が指摘1の実機確認です。
- 会戦の途中保存は未対応のため、保持の保存復元は引き続き未実装です。
- 仕様2（費用一覧UI）は**着手していません**（仕様1の実装・レビュー・実機検証の完了後）。
- 既知の別件（入力の取りこぼし、`DamagePopup` 未解放警告）は今回も未着手です。

**編集停止**。
