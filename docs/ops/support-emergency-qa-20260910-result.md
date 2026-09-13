# 敗走・総退却の実機再現補助 — 実装結果

task_id: `support-emergency-qa-20260910`
作業票＝[support-emergency-qa-20260910.md](support-emergency-qa-20260910.md)／
前段＝[support-order-ai-handoff-20260910-fix1.md](support-order-ai-handoff-20260910-fix1.md)（未合格のまま引き継ぎ）。
**編集停止済み**。commit / push なし。既存セーブ・v5・無関係な変更は保護。

---

## 方針：結果を QA が書かない

作業票の「命令解除/撤退状態を QA が直接設定して合格にしない」に沿って、
**触るのは入力条件だけ**にしました。

| 誘発したいこと | QA が動かすもの | 判断する人 |
|---|---|---|
| 敗走 | `FleetMorale.ApplyMoraleDelta(-morale)`＝**士気だけ** | `FleetMorale.IsRouted`（士気≤0）→ `FleetAI.Update` |
| 軍団の総退却 | 軍団各艦の `FleetStrength.strength`＝**残存兵力だけ**（士気は正常のまま） | `CorpsRetreatRules.ShouldOrderRetreat` → `BattlefieldCommandManager.ApplyCorpsRetreat` |

`manualOverride` の解除も `currentState = 撤退` も **QA からは一切書きません**。
本物の `SupportRequestDirector`（受理→承諾）・`FleetAI.Update`・`BattlefieldCommandManager` を通した結果を観測するだけです。

---

## 追加した QA 補助（`Assets/Editor/SupportEmergencyQaMenu.cs`・新規）

Play 中のみ動作。シーン・プレハブ・アセット・セーブに**何も書きません**。

| メニュー | 役割 |
|---|---|
| `Ginei/QA: 緊急中断 支援移動中に敗走させる（Play中）` | 前提確認 → 士気を0へ → 記録開始 |
| `Ginei/QA: 緊急中断 支援攻撃中に敗走させる（Play中）` | 同上（前提が「手動標的あり」） |
| `Ginei/QA: 緊急中断 他軍団を総退却の兵力まで減らす（Play中）` | しきい値を逆算して残存比を下げる（士気は正常） |
| `Ginei/QA: 緊急中断 自軍団を総退却の兵力まで減らす（直接命令の対照）（Play中）` | **対照**。直接命令の艦が巻き込まれないことを見る |
| `Ginei/QA: 緊急中断 記録を出力（Play中）` | 時系列の全文を Console へ（要約はダイアログ） |
| `Ginei/QA: 緊急中断 記録を消す（Play中）` | 記録を破棄 |

### 前提が満たされていなければ何もしない

誘発の前に「**出どころ＝支援要請** かつ **移動中 or 手動標的あり**」を確認し、
満たしていなければ **【未成立】** と、何が足りないか＋準備手順を出して**中断します**
（前提の無いまま誘発して「中断されなかった」と誤判定しない）。

対照側は「出どころ＝**直接命令** かつ実行中」を前提にします。

### 記録

`EditorApplication.update` から**読み取りだけ**でサンプリングします。

- **誘発前** → **誘発直後** → **最初の6フレームは毎フレーム** → 以降 0.25 秒ごと
- **10 秒で自動停止**（観測を逃さず、放置しても増え続けない）
- 記録項目：`Time.time`／フレーム／`timeScale`／**暦（時刻）**／艦名／軍団／生存／**士気（敗走表示）**／
  **残存比**／**override（出どころ）**／**手動標的**／**AI状態**／**座標**／**移動中**／**移動先**
- **軍団の全生存隷下**を毎回記録（総退却で他の艦も一緒に退くか見るため）
- 主役に変化が出た瞬間へ注記を入れる
  （「→ ここで支援の命令が中断されました」「→ ここで撤退状態に入りました」）
- `timeScale == 0`（一時停止中）に誘発したら**警告**を出す（そのままでは何も起きないため）

---

## 追加した PlayMode 統合試験（`Assets/Tests/PlayMode/SupportEmergencyPlayModeTests.cs`・新規）

縮小版の判断テストだけを合格にしないため、**実際の Game コンポーネント**
（`FleetStrength` / `FleetMorale` / `FleetMovement` / `WeaponArc` / `FleetWeapon` / `FleetAI` / `Squadron`）を
組んで**フレームを進め**、本物の `FleetAI.Update` に判断させます。ここでも結果は書かず士気だけ下げます。

| テスト | 何を確かめるか |
|---|---|
| `SupportOrder_IsInterruptedByRout` | 平時は行き先が保たれ、**敗走したら中断されて撤退状態へ移る** |
| `DirectOrder_SurvivesRout` | **対照**。引数なし `BeginManualOverride()` は `直接命令` になり、敗走でも中断されない |
| `SupportAttack_ClearsManualTargetOnInterrupt` | 中断後に**手動標的が残らない**（退がりながら追尾しない） |
| `SupportOrder_IsKeptWhileNotInEmergency` | 平時に勝手に投げ出さない |

### ★このテストは私の環境では実行できていません

Unity が要ります。**ChatGPT 側で実行してください**：

- Unity エディタ → `Window > General > Test Runner` → **PlayMode** タブ →
  `Ginei.Tests.PlayMode` の `SupportEmergencyPlayModeTests` を Run
- またはコマンドラインで `-runTests -testPlatform PlayMode`

**コンパイルは通っています**（`Ginei.Tests.PlayMode` を含む6アセンブリで `error CS` 0件）が、
実行結果は未取得です。**合格扱いにしないでください。**
実行には Unity が必要なので、終了時は Play OFF に戻してください。

---

## 実機手順

### 準備（共通）

1. Unity Play 開始 → `Ginei/QA: 指揮権限 検証会戦を開始（戦役・Play中）` → 準備完了ダイアログを閉じる
2. `Ginei/QA: 支援要請 他軍団を協力的にする（承諾の再現）`
3. **Space で再開**（`timeScale = 0` のままだと何も起きません）

### A. 支援移動中の敗走

4. `QA他軍1` を選択 → 右クリックで**遠くへ**移動を指示 → 「応じました」を待つ
5. `Ginei/QA: 支援要請 対象艦隊の命令状態を出力` で **出どころ=支援要請／移動中=True** を確認
6. `Ginei/QA: 緊急中断 支援移動中に敗走させる（Play中）`
7. 10 秒待ってから `Ginei/QA: 緊急中断 記録を出力（Play中）` → Console の `[緊急中断QA]` を読む

**期待**：士気が0になった直後の数フレーム以内に
`override=なし`（→ 中断の注記）／`AI=撤退`（→ 撤退の注記）へ変わり、
その後 **移動先が退却方向へ変わって座標が動く**こと。通知にも
「… は敗走のため、引き受けた支援を中断しました」が出ること。

### B. 支援攻撃中の敗走

8. `QA他軍1` を選択 → `QA敵1` へ攻撃を要請 → 「応じました」
9. `Ginei/QA: 緊急中断 支援攻撃中に敗走させる（Play中）` → 記録を出力

**期待**：`override=なし` に加えて **手動標的=False** になること（追尾が残らない）。

### C. 軍団の総退却（士気は正常のまま）

10. 準備をやり直し、`QA他軍1` へ移動を要請して承諾させる
11. `Ginei/QA: 緊急中断 他軍団を総退却の兵力まで減らす（Play中）`
12. 記録を出力

**期待**：`BattlefieldCommandManager` は約1秒ごとに判断するので、
1〜2 秒以内に軍団長が総退却を下令（通知「整然退却を下令」）し、
支援を引き受けていた艦も **override=なし／AI=撤退** になること。
**他の生存隷下も一緒に `AI=撤退`** になっていること（記録は軍団全艦を毎回出します）。

### D. 直接命令の対照（既存動作の維持）

13. 準備をやり直し、**`QA自軍1`** を選択して通常どおり右クリックで移動命令（＝直接命令）
14. `Ginei/QA: 支援要請 対象艦隊の命令状態を出力` で **出どころ=直接命令** を確認
15. `Ginei/QA: 緊急中断 自軍団を総退却の兵力まで減らす（直接命令の対照）（Play中）` → 記録を出力

**期待**：自軍団の**他の艦は `AI=撤退` になる**が、
**直接命令の艦だけは `override=直接命令` のまま巻き込まれない**こと（従来どおり）。

---

## 検証結果

| 項目 | 結果 |
|---|---|
| 6アセンブリ コンパイル | 合格（Core / Data / Game / Editor / Tests.EditMode / **Tests.PlayMode** すべて `error CS` 0件） |
| TestHarness（EditMode 相当） | 合格（**9,652件・0失敗**。今回はロジック変更が無いため件数据え置き） |
| **PlayMode 統合試験** | **未実行**（Unity が必要。ChatGPT 側で実行が必要） |
| 実機検証 | **未実施**（上記手順 A〜D） |
| 既存セーブ | 未変更（`campaign_save.json` Sep 9 13:22 のまま） |
| commit / push | 実施していない（`git log` は `5eb970f2` のまま） |
| 製品コードの変更 | **なし**（今回は QA 補助とテストの追加のみ。fix1 の実装をそのまま検証にかける） |

### 変更ファイル

| ファイル | 種別 | 内容 |
|---|---|---|
| `Assets/Editor/SupportEmergencyQaMenu.cs` | **新規** | 誘発（入力条件のみ）＋時系列記録＋自動停止＋前提の未成立表示 |
| `Assets/Tests/PlayMode/SupportEmergencyPlayModeTests.cs` | **新規** | 実コンポーネントでの統合試験4件（**未実行**） |

いずれも `.meta` 作成済み。**製品コード（`Assets/Scripts`）には一切手を入れていません。**

---

## 実機未検証点・残件

1. **手順 A〜D はすべて未検証**。とくに C（総退却で支援艦も一緒に退く）と
   D（直接命令は巻き込まれない）が fix1 の要です。
2. **PlayMode 統合試験は未実行**。コンパイルは通っていますが、実行結果は取れていません。
   実コンポーネントの初期化（`Squadron` など）で想定外があれば、その修正が必要になる可能性があります。
3. 今回は**不具合を見つけていません**（製品コードの修正なし）。
   検証で不具合が出たら、原因・差分・再試験を次の task_id で記録します。
4. 陣形の AI 自動上書きは別残件のまま（直接命令でも同じ挙動のため）。

**編集停止**。
