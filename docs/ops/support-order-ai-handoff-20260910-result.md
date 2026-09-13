# 支援要請の承諾後の命令維持 — 実装結果

task_id: `support-order-ai-handoff-20260910`
作業票＝[support-order-ai-handoff-20260910.md](support-order-ai-handoff-20260910.md)。
**編集停止済み**。commit / push なし。既存セーブ・v5・無関係な変更は保護。

---

## 結論：疑いは当たっていました（コード上で確定）

`SupportRequestDirector.Execute` は移動で `ClearOrder()` → `SetDestination()` を呼ぶだけで、
**`FleetAI.BeginManualOverride()` を呼んでいませんでした**。

`FleetAI.Update` はこうなっています（`FleetAI.cs:170` 付近）:

```csharp
if (manualOverride)
{
    bool busy = (movement != null && movement.IsMoving)
        || (weapon != null && weapon.HasManualTarget)
        || (standardOrder != null && standardOrder.stance != FleetStandardOrder.Stance.なし);
    if (busy) return;          // 手動操作を優先（AIは口を出さない）
    manualOverride = false;    // 指示完了＝AIへ復帰
}
...
UpdateStateBehavior();          // ここで movement.SetDestination(...) を呼ぶ
```

`manualOverride` が false のままだと、この早期 return を通らず
`UpdateStateBehavior()` が**次のフレームに自分の判断で行き先を上書き**します。
＝「承諾通知は出るのに指定先へ行かない」という観測と一致します。

## 3種の命令それぞれの確認結果

| 命令 | プレイヤーの直接命令 | 承諾後の要請（修正前） | 判定 |
|---|---|---|---|
| **移動** | `FleetCommander.ExecuteMoveCommand` が `BeginManualOverride` を呼ぶ（`FleetCommander.cs:724`）／後退も同様（`:754`） | **呼んでいない** | ★不具合。行き先が上書きされる |
| **攻撃** | `ConfirmAttack` が `SetManualTargetFleet` の直後に `BeginManualOverride`（`FleetCommander.cs:578`） | **呼んでいない** | ★不具合。`FleetWeapon` は指定艦隊を狙い続けるが、**足だけ AI が見つけた別の敵へ寄っていく**＝狙いと動きがちぐはぐ |
| **陣形変更** | `ChangeFormation` は `BeginManualOverride` を**呼んでいない** | 呼んでいない | 差が無い＝**要請だけの問題ではない**。今回は変更せず（下記） |

陣形について補足：`FleetAI.UpdateFormationDoctrine` は `autoFormation` と `corpsControlled` でしか抑止されず、
手動で布いた陣形も探索周期ごとに自動切替の対象になります。ただしこれは
**プレイヤーの直接命令でもまったく同じ**なので、要請側だけ特別扱いすると挙動が食い違います。
最小範囲の原則に従い、ここは触っていません（残件として記録）。

## 修正

### 1. AI 操舵を譲らせる（`SupportRequestDirector`）

実行できたときだけ、命令の種類に応じて `BeginManualOverride()` を呼びます。

```csharp
if (SupportOrderExecutionRules.CanExecute(r.execution, outcome) && alive)
{
    r.execution = Execute(r);

    // ★実際に実行できたときだけ AI 操舵を譲らせる（＝要請した命令が上書きされない）。
    if (SupportOrderExecutionRules.IsCarriedOut(r.execution)
        && SupportOrderExecutionRules.RequiresManualSteering(r.kind))
        BeginManualOverride(r.target);
}
```

`BeginManualOverride` の中身は `FleetCommander` のものと同一（`Selectable` → `FleetAI` → `BeginManualOverride()`）
＝**直接命令と要請で扱いが変わりません**。

### 2. 「どの命令で操舵を止めるか」を Core の窓口に置いた

条件を Director に書き分けず、純ロジック側で1か所に固定しました（test-first）。

```csharp
public static bool RequiresManualSteering(SupportOrderKind kind)
    => kind == SupportOrderKind.移動 || kind == SupportOrderKind.攻撃;
```

- **移動** … 行き先そのもの
- **攻撃** … 砲は指定目標を狙うので、足も止めないとちぐはぐになる
- **陣形変更** … 足に関わらず、直接命令でも止めていないので除外

### 3. 実行できていない要請では操舵を止めない

`IsCarriedOut(execution)` が真のときだけ止めます。
対象消失・手段なし・実行不可では**AI を黙らせません**（動かないのに AI の判断まで止める、を作らない）。

### 4. 読み取り専用の診断を追加

`Ginei/QA: 支援要請 対象艦隊の命令状態を出力（Play中）`（`CommandAuthorityQaMenu`）

QA の3軍団の各艦隊について、**現在地／移動中か／指定先／残り距離／命令維持(ManualOverride)／AI状態／手動標的／陣形／標準命令**を出します。
`FleetMovement` に読み取り専用の `Destination` プロパティを足しました（値は変えません）。

「移動中なのに命令維持が False」の艦隊には **★印**を付けます＝AI に上書きされうる状態がひと目で分かります。

---

## 緊急動作と AI 復帰（維持を確認した点）

| 項目 | 結果 |
|---|---|
| **撃沈** | 影響なし。`FleetStrength.TakeDamage` → `ResolveFlagshipDown` は `FleetAI` を通りません。`FleetAI.Update` も先頭で `!strength.IsAlive` なら即 return |
| **敗走の減速** | 影響なし。`FleetMovement.cs:310` が `IsRouted` を見て機動を落とすのは `FleetAI` の外 |
| **敗走の状態遷移** | 命令の実行中は `FleetAI` の 撤退 判定まで届きません。ただし**プレイヤーの直接命令でもまったく同じ**（`manualOverride` の既存仕様）で、到着時に自動で AI へ戻ります |
| **総退却** | `BattlefieldCommandManager.ApplyCorpsRetreat` は元から `if (ai == null \|\| ai.ManualOverride) continue;`＝**手動中の艦は尊重する**設計。要請を受けた艦も直接命令と同じ扱いで、命令完了後に軍団の退却へ合流します |
| **AI 復帰** | `FleetAI` 自身が「移動終了かつ手動標的なしかつ標準命令なし」で `manualOverride` を落とします。**Director 側に復帰処理を持たせていません**（二重実装しない） |

※ 総退却と敗走について「命令中は割り込まない」のは**既存の設計判断**です。
要請側だけ挙動を変えると直接命令と食い違うため、今回は合わせました。
仕様として変えたい場合は直接命令と一緒に見直す話になります（残件）。

---

## 検証結果

| 項目 | 結果 |
|---|---|
| 6アセンブリ コンパイル | 合格（Core / Data / Game / Editor / Tests.EditMode / Tests.PlayMode すべて `error CS` 0件） |
| TestHarness 全件 | **9,643件合格・0失敗**（前回 9,640 ＋ 3件） |
| 既存セーブ | 未変更（`campaign_save.json` Sep 9 13:22 のまま） |
| commit / push | 実施していない（`git log` は `5eb970f2` のまま） |
| 指揮系統 | 直接命令／要請の区別は変えていない（`CommandableSelection` の関門も `CommandOrderSource` の既定も不変） |

### 追加した回帰テスト（3件）

- 移動と攻撃は AI 操舵を止める必要がある
- 陣形変更は止めない（直接命令と同じ土俵）
- **実行できていない結果では止めない**（対象消失・手段なし・実行不可・未実行のすべてで確認）

### 変更ファイル

| ファイル | 内容 |
|---|---|
| `Assets/Scripts/Game/SupportRequestDirector.cs` | 実行成功時に `BeginManualOverride` を呼ぶ／ヘルパ追加 |
| `Assets/Scripts/Core/Combat/SupportOrderExecutionRules.cs` | `RequiresManualSteering` を追加 |
| `Assets/Tests/EditMode/SupportOrderExecutionTests.cs` | 回帰テスト3件 |
| `Assets/Scripts/Game/FleetMovement.cs` | 読み取り専用 `Destination` プロパティ（診断用） |
| `Assets/Editor/CommandAuthorityQaMenu.cs` | 命令状態の読み取り専用ダンプを追加 |

---

## 実機手順（ChatGPT へ）

1. Strategy Play → `Ginei/QA: 指揮権限 検証会戦を開始（戦役・Play中）` → 準備完了ダイアログを閉じる。
2. `Ginei/QA: 支援要請 他軍団を協力的にする（承諾の再現）` を実行。
3. **Space で再開**（時間を進める）。
4. `QA他軍1` を選択 → 右クリックで**盤面の離れた場所**へ移動を指示 → 「要請しました」。
5. 数秒後「応じました」。**その直後に**
   `Ginei/QA: 支援要請 対象艦隊の命令状態を出力（Play中）` を実行し、`QA他軍1` の行を見る。
   - 期待：**移動中=True／指定先＝指示した座標／命令維持=True**、★印が**付かない**こと。
6. 数秒おいてもう一度同じ診断を実行。
   - 期待：**指定先が変わっていない**こと（AI に書き換えられていない）。**残り距離が減っている**こと。
7. 到着後にもう一度診断。
   - 期待：**移動中=False／命令維持=False**（＝AI へ復帰した）。
8. **攻撃**：`QA他軍1` を選び `QA敵1` へ攻撃を要請 → 承諾後に診断。
   - 期待：**手動標的=True／命令維持=True**で、目標へ寄っていくこと。
9. **緊急動作**：`Ginei/QA: 支援要請 要請先を撃沈する` で `QA他軍1` を沈める。
   - 期待：撃沈は妨げられないこと（診断の一覧から消える）。
10. **回帰**：自軍団への直接の移動命令が今までどおり指定先へ到達すること。

## 実機未検証点

上記 1〜10 は**すべて未検証**です。原因の特定と修正はコード上で確定していますが、
**指定先へ実際に到達することの確認は取れていません**（作業票の「UIで承諾しただけでは到達完了と扱わない」に従います）。
合格扱いにしないでください。

## 残件

1. **陣形は AI の自動切替（`UpdateFormationDoctrine`）に戻されうる**。
   直接命令でも同じなので今回は触っていません。仕様として手動陣形を保持したいなら、
   直接命令と要請をまとめて見直す必要があります。
2. **命令実行中は総退却・敗走の判断が保留される**（`manualOverride` の既存仕様）。
   直接命令と同じ挙動に合わせてあります。変えるなら両方まとめて。
3. 支援要請は会戦内のみ。戦略側の他軍団への要請は未実装。
4. 要請の状態を一覧する UI は無い（通知のみ）。

**編集停止**。
