# 仕様1 fix1 差戻し（PlayMode 3失敗）— 対応結果（fix2）

Task: `fleet-formation-hold-20260910-fix2`
Issue: https://github.com/yamamax55/ginei-game/issues/2253（確定仕様1）
差戻し＝[fleet-formation-hold-20260910-fix2.md](fleet-formation-hold-20260910-fix2.md)／
証跡＝`fleet-formation-hold-fix1-playmode-20260910.xml`（11合格3失敗・16.647秒）。
**編集停止済み**。commit / push なし。既存セーブ・v5・無関係な差分は保護。**仕様2には進んでいません。**

---

## 結論：3件とも**試験の前提不備**でした（ゲーム本体の不備ではありません）

3つとも「試験が前提を成立させないまま結果を assert していた」もので、
製品コードは**1行も変えていません**。根拠は下記のとおり各失敗ごとにコードで特定しました。
実機で「陣形保持を解除しました（敗走）」通知が出ている観測とも整合します。

期待値は緩めていません。**むしろ前提の assert を増やして厳しく**しました。

---

## 失敗1 `CorpsRetreatOrdered_RejectsNewHoldForDirectOrderedFleet`（撤退中を期待、実際は受理）

### 原因：個体AIの撤退と軍団総退却の発令を取り違えていた（指摘1のとおり）

試験は `cmdAi.currentState == 撤退` になるのを待っていました。ところが

```csharp
// FleetAI.cs:26
public float retreatRatio = 0.3f;
// FleetAI.cs:235
else if (currentState != AIState.撤退 && (float)strength.strength / strength.maxStrength < retreatRatio)
    currentState = AIState.撤退;
```

兵力を **1割**へ落とした時点で、`FleetAI` が**自分の判断で**次のフレームに `撤退` を立てます。
これは軍団総退却とは別物です。`BattlefieldCommandManager` が1度も回らないうちに
待機ループを抜けてしまい、`IsCorpsRetreatOrdered` はまだ false。

対象の隷下は直接命令中なので `FleetAI.Update` が早期 return して `currentState` も 撤退 にならず、
`IsRetreatingNow()` の4条件がすべて false ＝**受理**。観測どおりです。

### 直し方（試験側）

**軍団総退却の発令そのもの**を待ち、前提として assert します。

```csharp
string corpsKey = CorpsFormation.KeyFor(sub);
while (!BattlefieldCommandManager.IsCorpsRetreatOrdered(corpsKey) && Time.time < timeout) yield return null;
Assert.IsTrue(BattlefieldCommandManager.IsCorpsRetreatOrdered(corpsKey), "軍団総退却が発令されなかった（前提が成立していない）");
```

`CorpsRetreatMovement_RejectsFormationWithoutSpendingPoints`（合格していた方）も同じ待ち方へ揃え、
「発令された」ことと「隷下が撤退へ移った」ことを別々に assert するようにしました
（前者が本当の前提、後者は結果）。

直接命令・移動の継続を確認してから新規保持の拒否／ポイント不変を見る順序は元から満たしています。

---

## 失敗2 `Rout_ReleasesHold_ButNotMovement`（保持解除を期待、実際は保持 true）

### 原因：敗走が**次のフレームに解けていた**（指摘2のとおり）

```csharp
// FleetMorale.cs
public float recoveryRate = 0.7f;
public float routedRecoveryDelay = 4f;
private float lastCombatTime;            // 初期値 0

if (IsRouted) {
    if (!inCombat && Time.time - lastCombatTime >= routedRecoveryDelay)
        ChangeMorale(recoveryRate * Time.deltaTime);   // ← 回復して IsRouted が false へ
    return;
}
```

この試験の艦は**一度も交戦していない**ので `lastCombatTime` は 0 のまま。
PlayMode の `Time.time` は（14件連続実行で）とっくに 4 秒を超えているため、
士気を 0 にした**次のフレームに回復**して `IsRouted` が false へ戻ります。
`FleetAI` が敗走を見る前に消えるので、保持が解除されません。

実機では敗走は戦闘の結果なので `lastCombatTime` が直近＝4秒の猶予が効きます。
**ゲーム側の不備ではありません。**

### 直し方（試験側）

回復率という**入力条件**を止めて敗走を維持し、成立と維持を assert します。

```csharp
morale.recoveryRate = 0f;                 // 入力条件（結果は書かない）
morale.ApplyMoraleDelta(-morale.morale);
Assert.IsTrue(morale.IsRouted, "前提：敗走状態になっていること");
for (int i = 0; i < 5; i++) yield return null;
Assert.IsTrue(morale.IsRouted, "前提：敗走が維持されていること（回復で戻っている）");
```

あわせて「直接の移動が実際に続いている」ことも assert に追加しました（`mv.IsMoving`）。

---

## 失敗3 `SupportRequest_AcceptedFormationBecomesHold`（10秒でも保持 false）

### 原因：**誰も統一クロックを進めていなかった**（指摘3のとおり）

`SupportRequestDirector` は game-time で判断します。

```csharp
private static float Now()
{
    GameClock clock = StrategySession.Clock;      // ← static。null ではない
    return clock != null ? (float)clock.ElapsedSeconds : Time.time;
}
```

`StrategySession.Clock` は `new GameClock()` が入った static で、**null にはなりません**。
通常の会戦では `BattleManager` が `Advance` しますが、この試験には居ないので
`ElapsedSeconds` が止まったまま＝`now - startTime` が常に 0 ＝**永久に「検討中」**。
実時間10秒待っても承諾に至りません。

### 直し方（試験側）

`BattleManager` と同じやり方でクロックを進めます（**判断は Director に行わせる**＝
保持を直接設定する試験には戻していません）。

```csharp
GameClock clock = StrategySession.Clock;
clock.paused = false;  clock.speed = 1f;      // 前の試験が止めたまま残ることがある
while (!sq.IsFormationHeld && Time.time < timeout) { clock.Advance(Time.deltaTime); yield return null; }
```

さらに、**どこで止まったか記録**できるよう、失敗時のメッセージに
経過秒と要請にまつわる通知列（受付「要請しました」→判断「応じました／断りました／流れました」）を
添えるようにしました（`RecentSupportMessages`）。次に落ちたときは段階が読めます。

---

## QA補助の改善（実機で撃沈され判定できなかった件）

`Ginei/QA: 陣形保持 保持中の艦を敗走させる（Play中）` を改良：

- 保持中の艦が複数いるときは**敵から最も遠い艦**を選ぶ
- モーダルに**最寄りの敵との距離**を表示
- 距離が近い（25 未満）ときは
  **「⚠ 敵が近いため追撃で撃沈され、移動の継続を判定できないことがあります」**と警告し、
  離れた艦に保持させてから実行するよう案内

---

## 検証結果

| 項目 | 結果 |
|---|---|
| 6アセンブリ コンパイル | 合格（`error CS` 0件） |
| TestHarness（EditMode 相当） | **9,676件合格・0失敗**（今回変更なし） |
| **PlayMode 14件** | **未実行**（Unity 必要／前回は 11合格3失敗） |
| 実機 | 未実施 |
| 既存セーブ | 未変更（`campaign_save.json` Sep 9 13:22 のまま） |
| commit / push | 実施していない（`git log` は `5eb970f2` のまま） |
| **製品コードの変更** | **なし**（3失敗はすべて試験の前提不備） |

### 変更ファイル

| ファイル | 内容 |
|---|---|
| `Assets/Tests/PlayMode/FleetFormationHoldPlayModeTests.cs` | 失敗3件の前提を成立・assert するよう修正／要請の段階を記録するヘルパ |
| `Assets/Editor/SupportEmergencyQaMenu.cs` | 敗走QAが敵から最も遠い保持艦を選び、距離と警告を出す |

**`Assets/Scripts` には一切手を入れていません。**

---

## 再検証手順（ChatGPT へ）

### 1. PlayMode（個別・一括の両方）

`Window > General > Test Runner` → PlayMode → `FleetFormationHoldPlayModeTests`

- **14件を一括**で実行して全件合格すること
- 前回失敗した3件は**個別実行**でも合格すること
  （失敗2・3は前のテストが残した時刻・クロック状態に影響されるため、
  個別と一括の両方で確認していただけると確実です）

### 2. 実機（最小限）

1. `Ginei/QA: 指揮権限 検証会戦を開始（戦役・Play中）` → Space で再開
2. `QA自軍1` に「陣形 ▸ 円陣」→ HUD「円陣（保持・直接命令）」
3. **一括指定が選択軍団だけ**：`QA自軍1` を選択したまま
   「軍団 ▸ 配下艦隊の陣形を一括指定 ▸ 方陣」→ **QA-自軍団だけ**が方陣。他軍団は不変
4. **軍団解除と艦隊保持の分離**：「軍団 ▸ 軍団指定を解除」→ **艦隊の保持は解けない**
5. **敗走で保持だけ解除**：`QA自軍1` を**敵から離れた位置**へ直接移動させ、陣形を保持させてから
   `Ginei/QA: 陣形保持 保持中の艦を敗走させる（Play中）`
   → モーダルの「最寄りの敵との距離」が十分（警告が出ない）ことを確認して実行
   → 「陣形保持を解除しました（敗走）」が出て、**直接の移動が続く**こと（撃沈されずに観察できる）
6. **総退却中の拒否**：`QA自軍1` に直接移動命令＋陣形保持 →
   `Ginei/QA: 緊急中断 自軍団を総退却の兵力まで減らす（直接命令の対照）（Play中）`
   → 保持は解除・移動は継続。その状態で「陣形 ▸ 円陣」→
   **「撤退中のため陣形を変更できません」**で拒否され、スキルポイントが減らないこと
7. **支援要請の承諾**：他軍団へ陣形変更を要請 → 「応じました」→
   HUD が「（保持・支援要請）」になり、軍団AI に上書きされないこと

---

## 実機未検証点

- **PlayMode 14件は未実行**です（私の環境では Unity を動かせません）。**合格扱いにしないでください。**
- 上記の実機手順1〜7も未実施です。
- 会戦の途中保存は未対応のため、保持の保存復元は引き続き未実装です。
- 仕様2（費用一覧UI）・仕様3・QAメニュー分類は**着手していません**。
- 既知の別件（再開ボタンの入力取りこぼし、`Some objects were not cleaned up` 警告、
  Test Runner 開始前の Font Material の edit-mode 警告）は今回も触っていません。
  作業票の判断どおり、今回の3失敗の原因ではありません。

**編集停止**。
