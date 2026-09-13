# 支援要請の承諾後の命令維持 — 差戻し対応（fix1）

task_id: `support-order-ai-handoff-20260910-fix1`
前回結果＝[support-order-ai-handoff-20260910-result.md](support-order-ai-handoff-20260910-result.md)。
**編集停止済み**。commit / push なし。既存セーブ・v5・無関係な変更は保護。

---

## 指摘は妥当でした（これは私が入れた回帰です）

前回、承諾した支援要請に `BeginManualOverride()` を立てました。ところが

- `BattlefieldCommandManager.ApplyCorpsRetreat` は `if (ai.ManualOverride) continue;` で**上書き中の艦を総退却から除外**する
- `FleetAI.Update` は上書き中かつ実行中なら早期 return するので、**敗走の判断（`currentState = 撤退`）まで届かない**

つまり **承諾する前はできていた敗走・総退却が、承諾したせいでできなくなっていました**。
「支援を引き受けたから死ぬ」は要請の趣旨と合いません。前回の報告で
「直接命令と同じだから既存仕様」と整理しましたが、**支援要請の経路にとっては新規の劣化**であり、
整理としても誤りでした。指摘のとおり修正します。

## 直し方：上書きに「出どころ」を持たせ、支援要請だけ中断できるようにした

直接命令の扱いは**一切変えていません**（変えると既存の操作感が変わるため）。

### Core（新規）`Assets/Scripts/Core/Combat/ManualOverrideRules.cs`

```csharp
public enum ManualOverrideKind
{
    なし,        // AI 操舵中
    直接命令,    // 従来どおり。緊急でも中断しない・総退却の対象外
    支援要請,    // ★緊急（敗走・総退却）では中断して退がれる
}

public static class ManualOverrideRules
{
    // 命令が終わったか（＝AI へ返してよいか）
    public static bool IsOrderComplete(bool isMoving, bool hasManualTarget, bool hasStandingOrder);

    // 緊急のために上書きを手放すべきか（支援要請だけ true になりうる）
    public static bool ShouldReleaseForEmergency(ManualOverrideKind kind, bool routed, bool corpsRetreatOrdered);

    // 総退却の下令がその艦を対象にしてよいか（直接命令だけ false ＝従来どおり尊重）
    public static bool CanOrderRetreat(ManualOverrideKind kind);
}
```

`CanOrderRetreat` は **なし と 支援要請 で同じ結果**になります＝これが回帰の打ち消しそのものです。

### Game 側の配線

| ファイル | 変更 |
|---|---|
| `FleetAI` | `manualOverride`(bool) → `overrideKind`(enum)。`ManualOverride` プロパティは**そのまま残す**ので既存の読み手（`FleetWeapon` など）は無改造。`BeginManualOverride()` は既定で `直接命令`＝**既存の呼び出しは挙動が変わらない**。`InterruptSupportOrder(reason)` を追加 |
| `FleetAI.Update` | 上書き中に**まず敗走を見る**。支援要請なら中断して、そのまま下の撤退判断へ進む。直接命令は従来どおり早期 return |
| `BattlefieldCommandManager.ApplyCorpsRetreat` | `if (ai.ManualOverride) continue;` → `if (!ManualOverrideRules.CanOrderRetreat(ai.OverrideKind)) continue;` ＋ 支援要請なら `InterruptSupportOrder("総退却")`。**直接命令は今までどおり除外** |
| `SupportRequestDirector` | `ai.BeginManualOverride(ManualOverrideKind.支援要請)` を渡す |

中断時は攻撃の手動目標と標準命令も解きます（退がりながら指定目標を追い続ける、を残さない）。
中断したことは通知に出ます（「… は敗走のため、引き受けた支援を中断しました」）。

---

## 攻撃についての確認（ご指摘の2点）

### 射程外の目標へ接近できなくなっていないか → **むしろ逆でした**

`FleetWeapon.HandlePursuit`（`FleetWeapon.cs:257`）:

```csharp
if (fleetAI != null && fleetAI.enabled && !fleetAI.ManualOverride) return;
// AI操舵中は追尾しない（手動上書き中は追尾する）
```

**追尾は `ManualOverride` が true のときしか動きません。**
つまり前回の修正前は、承諾した攻撃要請は手動目標を持つだけで
**射程外の敵へ一歩も近づけませんでした**（`PursueToward` に到達しない）。
`BeginManualOverride` を立てたことで、射程外なら `movement.SetDestination(targetPos)` で追尾し、
射程内（`weaponArc.range * pursuitStopRatio`）で止まって射界を維持する、という
直接命令と同じ挙動になります。AI の移動を止めることが**接近の条件**でした。

### 完了・対象消失で解除できるか → **できます**

- `HandlePursuit` は `if (!IsFleetAlive(manualTargetFleet)) { manualTargetFleet = null; … }` で自分から指定を落とす
- すると `HasManualTarget` が false になり、移動も止まれば `IsOrderComplete` が true
- `FleetAI.Update` が `EndManualOverride()` して AI へ復帰

この経路をテストでも固定しました（`AttackOrder_CompletesWhenTargetIsGone`）。

---

## テスト（真偽表だけにしない）

`RequiresManualSteering` の真偽を見るだけでは今回の回帰は捕まりません。
そこで **`FleetAI.Update` の判断を写した縮小版**をテスト内に置き、
「AI が操舵から手を引くか」を状況ごとに確かめる形にしました。

```csharp
private static bool AiStandsDown(ManualOverrideKind kind, bool moving, bool hasManualTarget,
                                 bool hasStandingOrder, bool routed)
{
    if (!ManualOverrideRules.IsOverriding(kind)) return false;
    if (ManualOverrideRules.ShouldReleaseForEmergency(kind, routed, false)) return false;
    return !ManualOverrideRules.IsOrderComplete(moving, hasManualTarget, hasStandingOrder);
}
```

追加した9件のうち、回帰を直接捕まえるのは次の3つです。

| テスト | 何を守るか |
|---|---|
| `SupportOrder_DoesNotChangeCorpsRetreatEligibility` | 総退却の対象になれるかが**承諾前と承諾後で同じ**。前回の実装ならここで落ちる |
| `SupportOrder_RoutedFleetCanStillRetreat` | 移動中でも攻撃追尾中でも、敗走したら AI が操舵を取り戻す。前回の実装ならここで落ちる |
| `DirectOrder_KeepsLegacyBehavior` | **直接命令は従来どおり**（総退却の対象外・敗走でも中断しない）。うっかり全部中断可能にしたらここで落ちる |

ほかに、平時は支援命令が守られること／中断は緊急のときだけ／完了判定の全分岐／攻撃の対象消失で復帰／文面。

---

## 検証結果

| 項目 | 結果 |
|---|---|
| 6アセンブリ コンパイル | 合格（`error CS` 0件） |
| TestHarness 全件 | **9,652件合格・0失敗**（前回 9,643 ＋ 9件） |
| 既存セーブ | 未変更（`campaign_save.json` Sep 9 13:22 のまま） |
| commit / push | 実施していない（`git log` は `5eb970f2` のまま） |
| 直接命令の扱い | **不変**（`BeginManualOverride()` の既定が `直接命令`／総退却は従来どおり除外／敗走でも中断しない） |

### 変更ファイル

| ファイル | 内容 |
|---|---|
| `Assets/Scripts/Core/Combat/ManualOverrideRules.cs` | **新規**。出どころ enum と持続/中断の決まり |
| `Assets/Tests/EditMode/ManualOverrideRulesTests.cs` | **新規**。回帰テスト9件 |
| `Assets/Scripts/Game/FleetAI.cs` | 出どころを保持／敗走で支援命令を中断／`InterruptSupportOrder` |
| `Assets/Scripts/Game/BattlefieldCommandManager.cs` | 総退却が支援要請の艦を対象にする（直接命令は除外のまま） |
| `Assets/Scripts/Game/SupportRequestDirector.cs` | 上書きを `支援要請` として立てる |
| `Assets/Editor/CommandAuthorityQaMenu.cs` | 診断に「出どころ」を追加 |

---

## 実機手順（ChatGPT へ）

前回の手順1〜10に加えて、**緊急動作の確認**を足してください。

11. **敗走で中断できること**：`QA他軍1` へ遠くの座標を移動要請 → 承諾（移動中＝命令維持 True）→
    その艦が交戦して敗走するまで待つ（または敵と接触させる）。
    - 期待：通知に「… は敗走のため、引き受けた支援を中断しました」が出る。
    - 期待：診断で **命令維持=False／出どころ=なし** になり、AI状態が **撤退** へ移ること。
12. **総退却で中断できること**：他軍団が総退却を下令する状況（軍団の兵力が減る）を作る。
    - 期待：支援を引き受けていた艦も**一緒に退却する**（取り残されない）。
    - 期待：通知に「…総退却のため、引き受けた支援を中断しました」。
13. **直接命令は変わっていないこと**：**自軍団**の艦へ直接の移動命令を出し、同じ状況を作る。
    - 期待：診断で **出どころ=直接命令**。総退却に巻き込まれず、敗走でも命令が続くこと（従来どおり）。
14. **攻撃の接近**：`QA他軍1` に**射程外の**`QA敵1` を攻撃要請 → 承諾後、
    - 期待：**目標へ近づいていく**（診断の残り距離が減る）。射程に入ると止まって撃つこと。
15. **攻撃の解除**：その状態で `Ginei/QA: 支援要請 攻撃目標を撃沈する` を実行。
    - 期待：診断で **手動標的=False／命令維持=False／出どころ=なし** に戻ること（固まらない）。

## 実機未検証点

11〜15 を含め、**すべて未検証**です。とくに今回は
「敗走・総退却で実際に中断されること」が要なので、そこを見ていただかないと合格になりません。
合格扱いにしないでください。

## 残件

1. **陣形は AI の自動切替（`UpdateFormationDoctrine`）に戻されうる**。
   直接命令でも同じで、要請だけの問題ではないため未変更（ご指示どおり残件として記録）。
2. 支援要請は会戦内のみ。戦略側の他軍団への要請は未実装。
3. 要請の状態を一覧する UI は無い（通知のみ）。

**編集停止**。
