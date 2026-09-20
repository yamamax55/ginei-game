# TestHarness と実 Unity の結果の食い違い — 結果

Task: `testharness-unity-divergence-20260916` / attempt a1 / revision 2
発端：`civilservice-appointment-panel-20260916` の検証中に、EditMode を**実 Unity で**回したところ
TestHarness では出ない失敗が1件出た。

**検証基盤の信頼性の問題**として扱い、原因を機構まで確定させて最小修正した。

---

## 1. 何が起きていたか

`ReplacementFlowRulesTests.戦力化は頭数回復と統合進捗のしきい値で判定`（82行目）

```csharp
Assert.IsTrue(ReplacementFlowRules.IsUnitCombatReady(5f, 10f, 6f));  // 6/10 = 0.6 ≥ しきい 0.6
```

| 実行環境 | 結果 |
|---|---|
| TestHarness（.NET 8） | **合格** |
| 実 Unity（Mono・6000.6.0f1） | **失敗** |

同じテストコード・同じ製品コードで結果が割れていました。

---

## 2. 原因（機構まで確定）

一時的な読み取り診断を Unity で実行し、実値をビット列で取得しました（取得後に削除済み）。

```
q(bits)=9A-99-19-3F   th(bits)=9A-99-19-3F      ← ビット列は完全に同一（0x3F19999A = 0.6f）
qEqualsTh     = True
localCompare  = True     ← float 変数に入れてから比較
inlineCompare = False    ← (time / integ) >= th をインラインで比較
doubleCompare = False
```

**同じ値なのに、書き方で答えが変わります。**

- しきい値 `0.6f` は float で正確に表せず **0.60000002384…**
- 一方 `6/10` は **0.6 ちょうど**
- Mono は除算結果を float へ丸めず**高い精度のまま**比較する
  → `0.6 >= 0.60000002…` ＝ **false**
- .NET は float へ丸めてから比較する
  → 両者が同じ float になり ＝ **true**

製品コードは

```csharp
return (time / integ) >= th;   // ← インライン形
```

だったため、**実機でだけ仕様（「しきい値**以上**で戦力化」）どおりに動いていませんでした。**

---

## 3. 直し方を一度間違えています（記録）

最初、診断の `localCompare = True` を根拠に「**float 変数へ代入すれば確定する**」と考えて

```csharp
float ratio = time / integ;
return ratio >= th;
```

に直しましたが、**Unity で再コンパイル（24.5秒）したうえで同じテストが落ちたまま**でした。
JIT が変数を高精度レジスタに保持しうるため、**変数化では直りません**。
診断で `localCompare` が真だったのは、値を文字列化した副作用で実際に float スロットへ落ちたためです。

➡ **境界値 0.6 は表現できない以上、正しい直し方は許容差**です。

---

## 4. 修正（最小・1箇所）

`ReplacementFlowParams` に名前付き定数を置き（マジックナンバー禁止の規約に従う）、

```csharp
/// 割合としきい値をちょうど境界で比べるときの許容差。
public const float BoundaryEpsilon = 1e-5f;
```

比較を

```csharp
float ratio = time / integ;
return ratio >= th - ReplacementFlowParams.BoundaryEpsilon;
```

としました。

**これは仕様の追加ではなく、仕様どおりにするための修正です**（仕様は「しきい値**以上**で戦力化」）。
許容差は割合の刻み（1/統合時間）よりはるかに小さいので、判定の意味は変わりません。
既存の他の assert（0.5 は未達／頭数0は未戦力）も従来どおりです。

---

## 5. 検証

| 項目 | 修正前 | 修正後 |
|---|---|---|
| 実 Unity EditMode | 10,008/10,009（1失敗） | **10,009/10,009（0失敗）** |
| TestHarness | 10,006/10,006 | **10,006/10,006** |

**両ランタイムが一致しました。** 証跡 `docs/ops/testharness-unity-divergence-editmode-fixed-20260916.xml`。

---

## 6. ★同じ形が Core に他9箇所あります（今回は変更していません）

インラインで演算結果をそのまま比較している箇所：

| ファイル | 行 |
|---|---|
| `Combat/BattlePerceptionRules.cs` | 141 |
| `Combat/DepotRules.cs` | 195 |
| `Combat/HomelandResistanceRules.cs` | 217 |
| `Combat/PiracyRules.cs` | 78 |
| `Economy/CompensationRules.cs` | 173 |
| `Economy/LogisticsBurdenRules.cs` | 197 |
| `Society/HistoricismTrapRules.cs` | 159 |
| `StrategicReserveRules.cs` | 187 |
| `Strategy/AtmosphereRules.cs` | 211 |

いずれも**現時点で失敗しているテストはありません**。
割れるのは「ちょうど境界」かつ「その境界が float で表せない」ときだけなので、
現在の試験値がたまたま境界を踏んでいないだけの可能性があります。

**予防的に9箇所を書き換えるのは、失敗していない挙動を動かすことになるので今回は見送りました。**
まとめて直す判断をされる場合は別途ご指示ください。

---

## 7. この件から言えること（検証基盤について）

**TestHarness の緑は、実 Unity の緑を意味しません。**

TestHarness は `Assets/Tests/EditMode` を Unity 非依存で回す**写し**であり、
`Mathf` のスタブ実装は実 Unity と同一でしたが、**ランタイムの浮動小数の扱いが違います**。
今回の1件はその差が表に出た最初の例です。

対処の方針（提案）：

- 境界の比較は**許容差を入れる**（今回の形）。新規の純ロジックでも同じ作法にする
- 節目では **EditMode を実 Unity でも回す**（今回できることが分かったので、TestHarness の緑だけで締めない）

CLAUDE.md への作法追記が要るかはご判断ください（今回は追記していません）。

---

## 8. 保護

- **commit/push なし**（HEAD は `52f48ce2` のまま）
- セーブ・シーン・プレハブは未変更
- 診断用の一時ファイルは削除済み
- 変更したのは `Assets/Scripts/Core/ReplacementFlowRules.cs` **1本のみ**（定数追加＋比較1行）
