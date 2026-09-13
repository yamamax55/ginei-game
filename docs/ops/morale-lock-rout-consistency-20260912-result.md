# 不退転と敗走判定の更新順 — 調査・修正結果

Task: `morale-lock-rout-consistency-20260912`
Issue: #2175（特殊指揮）／統合検証 #2253
作業票＝[morale-lock-rout-consistency-20260912.md](morale-lock-rout-consistency-20260912.md)。
**編集停止済み**。commit / push なし。既存セーブ・v5・無関係な差分は保護。
**仕様2には触れていません。**

---

## 1. 調査：反復の原因を確定しました

報告どおり「被弾直後から次の Update までの観測窓」が原因でした。**全経路を追って確定**した内容です。

### 不退転の有効期間・発動／終了

| 場所 | 動き |
|---|---|
| `ActiveCommandState.Issue` → `Activate` | `strength.activeMoraleLock = spec.moraleLock` を立てる。`activeUntil = Time.time + duration`（不退転は **8秒**） |
| `ActiveCommandState.Update` | `Time.time >= activeUntil`／死亡で `ClearEffect()` |
| `ClearEffect` / `OnDisable` | `activeMoraleLock = false` |

発動・終了そのものは素直で、ここに問題はありません。

### 士気を変える経路（ここが問題）

| 経路 | いつ呼ばれるか | 下限 |
|---|---|---|
| `FleetStrength.TakeDamage` → `FleetMorale.OnTakeDamage` → `ChangeMorale` | **被弾のたび＝フレーム中の任意の時点**（各艦の `FleetWeapon`／`EscortShip` が個別の発砲間隔で撃つ） | **0**（`Mathf.Clamp(morale + amount, 0, maxMorale)`） |
| `ApplyMoraleDelta`（`MoraleShock` / `BattleEventManager`） | イベント時 | **0** |
| `UpdateMorale` 冒頭の「不退転なら1へ戻す」 | **1フレームに1回だけ** | — |

そして判定は `IsRouted => morale <= 0` で、**`activeMoraleLock` を一切見ていませんでした**。

### 反復の成立（これが記録に出たもの）

```
不退転中：UpdateMorale が士気を 1 へ戻す
  ↓ 同じフレーム内で被弾（ChangeMorale の下限は 0）
士気 = 0  →  IsRouted = true   ← ここを読んだ側が「敗走」と解釈
  ↓ 次の FleetMorale.Update
士気 = 1  →  IsRouted = false  ← 「敗走 解除」
  ↓ また被弾 …（以下反復）
```

`FleetMorale` と `FleetAI` の実行順は保証されないため、**同じフレーム内でも読む側によって答えが変わる**状態でした。

### 巻き添えになっていた仕組み（表示だけの問題ではない）

`IsRouted` を見ている側が、不退転中なのに次の判断をしていました。

| 参照元 | 起きること |
|---|---|
| `FleetAI.cs:203` → `FleetFormationOrderRules.ShouldReleaseForEmergency` | **陣形の保持が解除される** |
| `FleetAI.cs:212` → `InterruptSupportOrder("敗走")` | **支援の命令が中断される** |
| `FleetAI.cs:230` | **`currentState = 撤退`**（退却行動へ） |
| `FleetMovement.cs:310` | 敗走時の機動低下が掛かる |
| `BattlefieldCommandManager.cs:495` | `commanderRouted` として総退却の判断に入る |
| `Squadron.IsRoutedNow` | 陣形指定が「撤退中」で拒否される |

つまり **#2175「効果中は敗走しない」が実際に破れていました**。

---

## 2〜3. 修正（最小・更新順に依存しない形へ）

### Core（新規）`Assets/Scripts/Core/Combat/MoraleLockRules.cs`

```csharp
public const float LockedFloor = 1f;

public static bool IsRouted(float morale, bool moraleLock) => morale <= 0f && !moraleLock;
public static float Floor(bool moraleLock) => moraleLock ? LockedFloor : 0f;
public static float Clamp(float morale, float delta, float maxMorale, bool moraleLock);
public static float OnLockExpired(float morale) => Mathf.Max(morale, 0f);
```

**判定と下限の両方**に同じ形で織り込むのが要点です。
判定だけ直すと士気の値がばたつき、下限だけ直すと直接 `morale` を書く経路が残ります。

### Game（`FleetMorale`・+23/−8行）

| 変更 | 内容 |
|---|---|
| `IsRouted` | `MoraleLockRules.IsRouted(morale, MoraleLocked)`＝**いつ読んでも同じ答え** |
| `MoraleLocked`（新規・読み取り専用） | `strength.activeMoraleLock` |
| `ChangeMorale`（被弾経路） | 下限を `MoraleLockRules.Clamp` 経由に。**効果中は0へ落ちない** |
| `ApplyMoraleDelta`（イベント経路） | 同上 |
| `UpdateMorale` の従来のクランプ | 保険として残置（直接 `morale` を書く経路があっても1フレームで整う） |

**製品コードの変更はこの1ファイルだけ**です（`Assets/Scripts/Game/FleetMorale.cs`）。

### 一貫性の担保

`IsRouted` は `FleetMorale` の1か所しか定義が無く、上表の参照元は**すべてそこを読みます**。
したがって陣形保持・支援中断・退却AI・HUD・観測が**同じ有効敗走状態**を見ます。
読む側には一切手を入れていません。

### 保護した優先順位

- **通常の敗走**：効果が無ければ下限0・`morale <= 0` で敗走＝**従来どおり**（弱めていません）
- **軍団総退却**：`CorpsRetreatRules.ShouldOrderRetreat(ratio, commanderRouted, …)` は
  **兵力比でも発令**されます。不退転で `commanderRouted` が false になっても
  ratio の枝は生きているので、**総退却の判断は従来どおり働きます**（テストで固定）
- **効果終了後**：`activeMoraleLock = false` に戻り、下限も0へ戻る。
  切れた瞬間に即敗走はせず（下限で踏みとどまった状態から再開）、
  **その後さらに被弾すれば通常どおり敗走**します

---

## 3. 回帰試験

### EditMode（新規 `MoraleLockRulesTests`・10件）→ TestHarness **9,686件合格・0失敗**（＋10）

効果中は士気0でも敗走しない／効果なしは従来どおり敗走／下限は効果中だけ1／
**何回どんな順で被弾しても下限を割らない**（反復の芯）／**順序を入れ替えても結果が同じ**／
上限の尊重／壊れた上限でも反転しない／**効果終了だけでは敗走せず、その後の被弾で敗走する**／
**不退転中でも兵力比による軍団総退却は発令される**。

### PlayMode（新規 `MoraleLockRoutPlayModeTests`・6件）— **未実行**

更新順の問題は実物でしか再現しないので、実コンポーネントで押さえています。
不退転は本物の `ActiveCommandState.Issue`、士気は `FleetStrength.TakeDamage`（通常の被弾経路）だけで動かし、結果は書きません。

| テスト | 内容 |
|---|---|
| `Locked_NotRoutedImmediatelyAfterDamage` | ★**Update を挟まず被弾直後に読んで**敗走していないこと（実機の観測窓と同条件）を30回 |
| `Locked_RoutFlagDoesNotOscillateAcrossFrames` | 毎フレーム被弾しながら**被弾直後とUpdate後の両方**で敗走を数え、**0回**であること |
| `Locked_FormationHoldSurvivesDamage` | 効果中の被弾で**陣形の保持が解けない** |
| `Locked_SupportOrderIsNotInterruptedByDamage` | 効果中の被弾で**支援の命令が中断されない**・撤退へ移らない |
| `AfterLockExpires_NormalRoutResumes` | 効果が切れただけでは敗走せず、その後の被弾で通常どおり敗走 |
| `WithoutLock_NormalRoutStillHappens` | **不退転なしの通常敗走が従来どおり起きる**（弱めていないことの対照） |

> `recoveryRate = 0` を使っている2件は、**士気の自然回復を止めるための試験入力**です
> （自然戦闘での持続敗走とは別物）。テスト内にもその旨を書いています。

### Editor 観測への追加（依頼3）

`FormationChangeRecorder` に **`不退転`（`activeMoraleLock`）と士気値**を足しました。

- **不退転の発動／終了は記録**（敗走との前後関係を読むのに要る）
- 敗走の行に `（士気 100.0→0.0 不退転=True）` を添える
- **士気の値そのものは変化の引き金にしない**＝自然増減でログを増やしません
- 状態要約にも `士気=… 不退転=…` を追加

---

## 検証結果

| 項目 | 結果 |
|---|---|
| 6アセンブリ コンパイル | 合格（`error CS` 0件） |
| TestHarness（EditMode 相当） | **9,686件合格・0失敗**（前回 9,676 ＋10） |
| **PlayMode 6件（新規）** | **未実行**（Unity 必要） |
| Unity 実機 | **未実施** |
| 既存セーブ | 未変更（`campaign_save.json` Sep 9 13:22 のまま） |
| commit / push | 実施していない（`git log` は `5eb970f2` のまま） |

### 変更ファイル

| ファイル | 種別 | 内容 |
|---|---|---|
| `Assets/Scripts/Core/Combat/MoraleLockRules.cs` | **新規** | 不退転中の敗走判定と士気下限（純ロジック） |
| `Assets/Scripts/Game/FleetMorale.cs` | 変更（+23/−8） | `IsRouted`・`ChangeMorale`・`ApplyMoraleDelta` を窓口経由に |
| `Assets/Tests/EditMode/MoraleLockRulesTests.cs` | **新規** | 10件 |
| `Assets/Tests/PlayMode/MoraleLockRoutPlayModeTests.cs` | **新規** | 6件（未実行） |
| `Assets/Editor/FormationObservationQaMenu.cs` | 変更 | 不退転と士気を記録（自然増減では増やさない） |

---

## 再検証手順（ChatGPT へ）

### A. PlayMode

`Window > General > Test Runner` → PlayMode →
- `MoraleLockRoutPlayModeTests`（新規6件）を**一括と個別の両方**で
- あわせて `FleetFormationHoldPlayModeTests`（17件）が**引き続き通ること**
  （`IsRouted` の意味を変えたので、敗走を使う既存試験への影響を見たいです）

### B. 実機（作業票の再現手順で）

1. `Ginei/QA: 指揮権限 検証会戦を開始` → **`Ginei/QA: 陣形保持 変化の記録を開始`** → Space
2. 作業票の再現どおり進める（円陣保持 → 攻撃 → Sink → 自律戦闘 → 被弾で自然敗走 → 不退転発動）
3. `Ginei/QA: 陣形保持 変化の記録を出力`
   - 期待：`★不退転 発動` のあとに **`★敗走 開始` と `敗走 解除` の反復が出ないこと**
   - 期待：不退転中に `★保持 解除` が出ないこと
   - `★不退転 終了` のあとは、被弾が続けば `★敗走 開始` が出てよい（通常判定に戻る）

---

## 未検証点・申し送り

- **PlayMode 6件は未実行**、**実機も未実施**です。合格扱いにしないでください。
- 今回の修正で `IsRouted` の意味が「有効な敗走」に変わりました。
  影響範囲は上表の参照元すべてですが、**読む側のコードは変えていません**。
  既存の敗走まわりの実機挙動（自然敗走・総退却・捨てがまり）に想定外が出ないか、
  A の既存17件と B で見ていただけると確実です。
- 不退転中は `commanderRouted` が false になるため、**軍団総退却は兵力比でのみ発令**されます。
  これは #2175 の趣旨どおりですが、仕様として気になる場合はご指摘ください（今回は変更していません）。
- 仕様2・配下艦艇改良は**範囲外**として触れていません。

**編集停止**。

---

# 差戻し対応（revision 4）— PlayMode 2失敗の原因と作り直し

2026-09-12 の差戻し（`morale-lock-rout-consistency-20260912-review.md`／`morale-lock-r3-playmode-20260912.xml`）を受けた対応です。

## 結論：**試験の組み立て不備**でした（製品回帰ではありません）

### 原因

`FleetStrength.Awake` は依存部品を**その場でキャッシュ**します。

```csharp
private void Awake()
{
    moraleComponent = GetComponent<FleetMorale>();   // ← ここ
    movement = GetComponent<FleetMovement>();
    squadron = GetComponent<Squadron>();
}
```

一方、私の `BuildFleet` は `AddComponent<FleetStrength>()` を**先**に呼んでいました。
`AddComponent` は**その場で `Awake` を走らせる**ため、そのとき `FleetMorale` はまだ付いておらず、
**`moraleComponent` が null のまま固定**されます。その結果：

```csharp
strength -= finalDamage;
if (moraleComponent != null)      // ← null なので素通り
    moraleComponent.OnTakeDamage(finalDamage);
```

**被弾しても士気が1ミリも減っていませんでした。**

### これが2失敗と4合格の両方を説明します

| テスト | 実際に起きていたこと |
|---|---|
| `WithoutLock_NormalRoutStillHappens` | 士気100のまま → 敗走しない → **失敗**（報告どおり False） |
| `AfterLockExpires_NormalRoutResumes` | 同上。効果終了後に1発当てても士気が動かない → **失敗** |
| 合格した4件 | `IsRouted == false` を期待する試験なので、**士気が動かなければ自明に真**＝**空振り合格** |

レビューの「合格4件も十分に士気を削って不具合条件を踏めているか再評価が必要」というご指摘は**当たっていました**。

### 製品側ではない根拠

- `Awake` で依存を掴むのは Unity の通常作法で、本作の規約（CLAUDE.md「他コンポーネント参照は Awake/Start でキャッシュ」）どおり
- 実機の艦隊は**プレハブ生成**なので全部品が同時に揃い、この順序問題は起きない
- 他の PlayMode 試験は `TakeDamage` を使っていない（`grep` で確認）ため、影響は本ファイルだけ

## 作り直した内容（結果の直書き・期待緩和は無し）

### 1. 組み立ての修正

```csharp
var go = new GameObject(name);
go.SetActive(false);          // ★全部品が揃うまで Awake を遅らせる
... AddComponent を全部 ...
go.SetActive(true);           // ここで一斉に Awake が走る
```

### 2. 空振りを構造的に潰す

| 仕掛け | 内容 |
|---|---|
| **`Fixture_DamageActuallyDrainsMorale`（新規）** | 他の試験より先に「**被弾が実際に士気を削る**」ことを証明する。これが落ちれば以降は全部空振りだと分かる |
| **対照を先に証明** | `WithoutLock_NormalRoutStillHappens` で「**このやり方なら確実に敗走まで到達する**」ことを示し、不退転側は**同じやり方で同じだけ**叩く |
| **士気の到達を assert** | 効果中の各試験で `morale == LockedFloor(1)` を assert ＝**危険な条件を踏んだ証明** |
| **生存を assert** | 判定の瞬間に `IsAlive` を assert ＝**死亡や未到達での空振り合格を作らない**。ループ内でも毎回確認 |
| **必要回数を逆算** | `HitsToDrainMorale`＝`ceil(maxMorale / (maxMorale × maxSingleHitMoraleFraction)) + 3`。効果中はその**2倍**叩く |

### 3. 被弾量の設計（死なずに士気だけ削り切る）

`OnTakeDamage` は **1発あたりの士気減少に上限**があります
（`drain = min(damage × damageDrainFactor, maxMorale × maxSingleHitMoraleFraction)`）。
実測値で計算すると：

| 項目 | 値 |
|---|---|
| 素ダメージ `RawHit` | 1000 |
| 旗艦の軽減 `flagshipDamageReduction` | 0.4 → ×0.6 |
| 紡錘陣の被ダメ倍率 | ×1.10 |
| 不退転の被ダメ倍率 | ×0.85（効果中のみ） |
| 最終ダメージ | 約 561〜660 |
| 士気減少 | `min(約28〜33, 100×0.08=8)` → **8（上限で頭打ち）** |
| 士気0まで | **13発** |
| 32発ぶんの兵力消費 | 約2万／10万 → **生存** |

**上限が支配する**ので、多少の係数差では結論が変わりません（頑健）。
初期兵力は 100,000 に上げ、死亡による空振りを避けています。

## 検証結果（差戻し対応後）

| 項目 | 結果 |
|---|---|
| 6アセンブリ コンパイル | 合格（`error CS` 0件） |
| TestHarness（EditMode 相当） | **9,686件合格・0失敗**（変更なし＝純ロジックは無修正） |
| **PlayMode 7件**（6件＋組み立て証明1件） | **未実行**（Unity 必要） |
| 製品コードの変更 | **今回の差戻し対応では無し**（revision 3 の `MoraleLockRules` ＋ `FleetMorale` のまま） |

### 差戻し対応での変更ファイル

| ファイル | 内容 |
|---|---|
| `Assets/Tests/PlayMode/MoraleLockRoutPlayModeTests.cs` | 組み立て順の修正／組み立て証明の新規追加／到達・生存の assert／被弾量と回数の設計（**6件 → 7件**） |

## 再検証のお願い

1. `MoraleLockRoutPlayModeTests`（**7件**）を一括と個別で
   - **まず `Fixture_DamageActuallyDrainsMorale` が通ること**（ここが落ちたら他は読まないでください）
   - 次に `WithoutLock_NormalRoutStillHappens`（対照）が通ること
   - その上で残り5件
2. 既存 `FleetFormationHoldPlayModeTests`（17件）の再実行
3. 実機での再現確認（`変化の記録` で `★不退転 発動` 後に敗走/解除の反復が出ないこと）

**未実行＝合格ではありません。** とくに今回は、私の試験自体が信用できない状態だったので、
`Fixture_` と対照の2件が通ることを先に確認していただけると確実です。

---

# 差戻し対応（revision 6）— 既存対照 `WithoutHold_RealAiDoesChangeFormation` の失敗

revision5 の再検証（`morale-lock-rout-consistency-20260912-review5.md`）で、
新規7件は一括・単独とも合格。一方で既存 `FleetFormationHoldPlayModeTests` が 16合格1失敗になりました。

## 結論：**既存試験（対照）の作りの問題**でした。製品回帰ではありません

失敗した `WithoutHold_RealAiDoesChangeFormation` には**2つの欠陥が重なって**いました。

### 欠陥1：`searchInterval` を設定するのが遅すぎた（決定的）

```csharp
yield return null;              // ← ここで最初の FleetAI.Update が走る
ai.searchInterval = 0.02f;      // ← もう遅い
```

`FleetAI.Update` は探索のたびに
`nextSearchTime = Time.time + searchInterval` を予約します。
最初の Update の時点では `searchInterval` は**既定の 2.0 秒**なので、
次の探索は 2 秒後に予約されます。**試験の観測時間は 1 秒**なので、

> **AI の陣形判断は、試験全体を通して「最初の1回」しか走っていませんでした。**

### 欠陥2：その1回で AI が「変えたい」状態になっていなかった

兵力が互角（10000 対 10000）だと：

```csharp
float ratio = own / enemy;                       // = 1.0
if (ratio <= OutnumberedRatio /*0.6*/) return 方陣;
if (ratio >= OutnumberingRatio /*1.5*/) return 鶴翼陣;
...
return Formation.紡錘陣;                          // ← ここに落ちる
```

推奨は **紡錘陣**＝試験が設定した初期陣形と同じ。
`FleetAI` は `if (squadron.currentFormation != rec)` でしか呼ばないので、
**`TryChangeFormation` が一度も呼ばれません**。

唯一変わりうるのは、その直後の**カウンター陣形**の枝ですが：

```csharp
if (!routed && targetEnemy != null && ... && BattleAiRules.ShouldAct(AiSkill(), UnityEngine.Random.value))
```

`AiSkill()` は提督データ無しで 0.5 ＝ **`Random.value` による50%の抽選**。
つまりこの試験は「**1回きりの coin flip**」に賭けていました。
以前 17/17 で通ったのは抽選に当たっていたからで、今回外れて落ちた、というのが実態です
（単独再実行でも同じ結果になるのは、Play セッションの乱数状態が揃うため）。

### 併せて分かったこと：保持ありの試験も「複数周期」ではなかった

対になる `DirectOrder_SurvivesRealAiDoctrineCycles` も同じ書き方だったため、
**AI の判断は1回しか走っていません**でした。
レビューの「AI周期を跨いだ保持試験の前提も再確認が必要」というご指摘のとおりです。
合格はしていましたが、主張していたほど強い試験ではありませんでした。

## 修正（AI 経路は迂回せず、期待値も緩めない）

共通の下ごしらえ `SetupAiWantsFormationChange` を作り、**保持あり／なしで同じ条件**にしました。

```csharp
// ★最初の Update が走る前に設定する（呼び出し側はまだ yield していない）
ai.autoFormation = true;
ai.searchInterval = AiSearchInterval;   // 0.02 秒

// ★劣勢にして「方陣を布きたい」状態にする。初期は紡錘陣＝必ず差がある
enemyStrength.strength = mineStrength.strength * 10;   // 自/敵 = 0.1
sq.currentFormation = Formation.紡錘陣;

// 前提そのものを assert（同じなら AI は何もしない＝試験が空振りになる）
Assert.LessOrEqual(ratio, FormationDoctrineRules.OutnumberedRatio, …);
Assert.AreNotEqual(Formation.紡錘陣, FormationDoctrineRules.RecommendFormation(…), …);
```

- **乱数に依存しません**：抽選が外れても推奨は 方陣（≠紡錘陣）なので必ず変えたくなる。
  当たれば 鶴翼陣（これも ≠紡錘陣）なので、どちらでも「AI が決めた」ことは変わりません
- **判断も適用も本物の `FleetAI` / `Squadron`** が行います（窓口の直叩きはしていません）
- 観測時間 1 秒 ÷ 周期 0.02 秒 ＝ **約50回**の探索が実際に回ります

### 主張を強めた assert

| 試験 | 変更後 |
|---|---|
| `WithoutHold_RealAiDoesChangeFormation`（対照） | `LastFormationSource == 艦隊AI`（「なし以外」より厳しく）＋**陣形が初期から実際に変わった**こと。失敗時は現在陣形と AI 状態を添える |
| `DirectOrder_SurvivesRealAiDoctrineCycles`（保持あり） | 同じ条件で 円陣 のまま／保持が残る／指定元が書き換わらない／**`LastFormationSource` が 艦隊AI になっていない**（＝AI の指定が1度も受理されていない）ことを追加 |

これで「**保持なしなら AI が必ず変える**（対照が成立）→ **同じ条件で保持ありなら変わらない**」という
対の構造になります。

## 検証結果（revision 6）

| 項目 | 結果 |
|---|---|
| 6アセンブリ コンパイル | 合格（`error CS` 0件） |
| TestHarness | **9,686件合格・0失敗**（変更なし＝純ロジックは無修正） |
| **PlayMode（既存17件）** | **未実行**（Unity 必要） |
| 製品コードの変更 | **今回は無し** |

### 変更ファイル

| ファイル | 内容 |
|---|---|
| `Assets/Tests/PlayMode/FleetFormationHoldPlayModeTests.cs` | `SetupAiWantsFormationChange` を新設し、対照と保持ありを同条件に。設定タイミングと兵力比を修正、assert を強化 |

## 再検証のお願い

1. **`WithoutHold_RealAiDoesChangeFormation` を単独で**（対照が成立することの確認）
2. `DirectOrder_SurvivesRealAiDoctrineCycles` を単独で
3. `FleetFormationHoldPlayModeTests` 一括17件
4. `MoraleLockRoutPlayModeTests` 7件（revision5 で合格済み・回帰確認）
5. 実機での不退転再現確認

**未実行＝合格ではありません。**

## 仕様の確認（いただいた整理に合わせています）

不退転中の総退却について、レビューでいただいた整理

> 実際には敗走していないため `commanderRouted = false` と扱い、艦艇比による総退却判断は継続する。
> 明示的な軍団総退却命令を不退転で拒否しないことは維持する。

は、**現行の実装と一致**しています（EditMode の
`CorpsRetreat_StillTriggersByStrengthRatioWhileLocked` で固定済み）。この点の変更は行っていません。

---

# revision 7 — 不退転がない時間帯の「敗走→翌フレーム解除」

revision6 の実機検証（`morale-lock-rout-consistency-20260912-review6.md`／
`morale-lock-r6-battle-20260912.txt`）で、陣形保持 17/17 合格・**不退転中の反復は3艦隊で解消**を確認。
残った別症状への対応です。

## 原因を確定しました（仕様変更は不要）

### 記録が示していたこと

```
F33400 t=156.92  QA自軍2  ★敗走 開始（士気 2.0→0.0 不退転=False）
F33401 t=156.92  QA自軍2   敗走 解除（士気 0.0→0.0 不退転=False）   ← 次フレーム
F33444 t=157.12  QA自軍2  ★敗走 開始（士気 0.1→0.0 不退転=False）
F33445 t=157.12  QA自軍2   敗走 解除（士気 0.0→0.0 不退転=False）
```

解除の行も士気は `0.0` 表示。ご指摘のとおり**丸めによる微小な正値**です。
`UpdateMorale` の立ち直りは

```csharp
ChangeMorale(recoveryRate * Time.deltaTime);   // 0.7 × 約0.005 ≒ 0.0035
```

で、`IsRouted` は `morale <= 0`。つまり**回復1フレームぶん（約0.0035）で敗走が解けます**。

### なぜ待ち時間が効かなかったか

立ち直りの門は「交戦が `routedRecoveryDelay`(4秒) 途切れたら」ですが、
その「交戦中か」は `FleetWeapon.IsInCombat` だけを見ていました。

```csharp
IsInCombat = (Time.time < lastFireTime + fireInterval) || enemyInArc;
```

＝**「自分が撃った」または「自分の射界に敵がいる」**。
したがって

- 射界の外（側面・背後）から叩かれている
- 敗走して背を向け、撃つのをやめている

という**一方的に撃たれている部隊は「非交戦」**と判定されます。
`lastCombatTime` は `IsInCombat` のときしか更新されないので、
士気0まで削られた時点で既に4秒以上“非交戦”が積み上がっており、
**敗走した次のフレームには門が開いていた**——これが反復の正体です。
（レビューの仮説どおりでした。）

## 修正（最小・1行＋判定の切り出し）

コメントに書かれた設計意図は「**交戦が途切れてしばらく経ったら**立ち直る」であり、
**撃たれていることは交戦**です。したがってこれは意図の回復であって**仕様変更ではありません**。

**Core（新規）`Assets/Scripts/Core/Combat/RoutRecoveryRules.cs`**

```csharp
public static bool IsCombatContact(bool engaging, bool tookDamage) => engaging || tookDamage;

public static bool CanRecover(bool combatContact, float secondsSinceCombat, float recoveryDelay)
{
    if (combatContact) return false;
    return secondsSinceCombat >= Mathf.Max(0f, recoveryDelay);
}
```

**Game（`FleetMorale`）**

| 変更 | 内容 |
|---|---|
| `OnTakeDamage` | **`lastCombatTime = Time.time;` を1行追加**（被弾も交戦として数え直す） |
| `UpdateMorale` の立ち直り判定 | インラインの条件を `RoutRecoveryRules.CanRecover(...)` へ委譲 |

`lastCombatTime` は**立ち直りの門でしか使われていない**ことを確認済み（他への副作用なし）。

### 効果

- **撃たれている間は門が開かない** → 士気0のまま敗走が続く → **反復しない**
- 攻撃が止んで4秒経てば**従来どおり立ち直る**（回復を殺していない）
- 不退転中の保護・効果終了後の通常敗走・明示総退却は**いずれも不変**

## 仕様変更の候補（今回は実装していません・ご判断ください）

上の修正で報告された反復は止まりますが、**攻撃が完全に止んだあと**は
「回復の最初の1ティック（士気 ≒ 0.0035）で敗走が解ける」という挙動が残ります。
一方向の遷移なので反復はしませんが、*ほぼ士気ゼロで立ち直った*ことにはなります。

これを変えるなら**立ち直りの閾値（ヒステリシス）を入れる**ことになり、
`IsRouted` の意味が変わる＝**仕様変更**なので、候補と影響だけ挙げます。

| 候補 | 内容 | 影響 |
|---|---|---|
| **A. 現状維持**（今回） | 士気が正になった時点で解除 | 変更なし。反復は上の修正で解消済み。最小 |
| **B. 解除に閾値** | 敗走の解除は `士気 >= maxMorale × R`（例 R=0.1）を満たしてから | `IsRouted` の定義が「0以下」から状態機械へ変わる。敗走の継続時間が延び、AI の撤退・陣形保持の解除・総退却の判断に波及。要 PlayMode 再確認 |
| **C. 解除に最短時間** | 敗走に入ってから最短 N 秒は解除しない | 実装は小さいが、短時間で立ち直る演出が消える。B と併用可 |

**B/C は #2175 の範囲を越えて通常戦闘の手触りに影響します。**
ご指示があれば別 task_id で候補を詰めます（今回は A のままにしてあります）。

## 回帰試験

### EditMode（新規 `RoutRecoveryRulesTests`・5件）→ TestHarness **9,691件合格・0失敗**（＋5）

被弾も交戦とみなす／交戦中はどれだけ時間が経っても立ち直らない／待ち時間の境界（未満・ちょうど・超過）／
**実機で起きた並びの再現**（敗走した次のフレームに立ち直らない → 攻撃が止んで待ち時間を満たしたら立ち直る）／
待ち時間0・負でも壊れない。

### PlayMode（`MoraleLockRoutPlayModeTests` に2件追加＝**9件**）— 未実行

**ご指摘のとおり、既存7件は `recoveryRate = 0` で回復を止めているためこの残件を検出できません。**
そこで**自然回復を既定のまま**にした対照を足しました。

| テスト | 内容 |
|---|---|
| `NaturalRecovery_RoutDoesNotClearWhileStillUnderFire` | 自然回復ありで敗走させ、**被弾を続けている60フレーム**のあいだ（被弾直後とUpdate後の両方で）**敗走が一度も解けない**こと＝実機の反復の再現と解消 |
| `NaturalRecovery_RecoversAfterFireStops` | 攻撃を止めて待ち時間を過ぎれば**従来どおり立ち直る**こと（回復を殺していないことの対照）＋解除時に士気が正であること |

## 検証結果（revision 7）

| 項目 | 結果 |
|---|---|
| 6アセンブリ コンパイル | 合格（`error CS` 0件） |
| TestHarness | **9,691件合格・0失敗**（＋5） |
| **PlayMode 9件** | **未実行**（Unity 必要） |
| 実機 | **未実施** |
| 既存セーブ | 未変更 |
| commit / push | 実施していない |

### 変更ファイル

| ファイル | 種別 | 内容 |
|---|---|---|
| `Assets/Scripts/Core/Combat/RoutRecoveryRules.cs` | **新規** | 立ち直りの門（被弾も交戦／待ち時間） |
| `Assets/Scripts/Game/FleetMorale.cs` | 変更（+7/−2） | `OnTakeDamage` で `lastCombatTime` 更新／立ち直り判定を Core へ委譲 |
| `Assets/Tests/EditMode/RoutRecoveryRulesTests.cs` | **新規** | 5件 |
| `Assets/Tests/PlayMode/MoraleLockRoutPlayModeTests.cs` | 変更 | 自然回復ありの対照2件を追加（7件→9件） |

## 再検証のお願い

1. `MoraleLockRoutPlayModeTests` **9件**（とくに新規2件を単独でも）
2. `FleetFormationHoldPlayModeTests` 17件の回帰
3. 実機：同じ QA 会戦で、**不退転がない時間帯**に
   `★敗走 開始` の直後に `敗走 解除` が続かないこと（記録で確認）
   - あわせて、攻撃が途切れた場面では**従来どおり立ち直る**ことも見てください
4. **保持ありの回帰は別途**（レビューのご指摘どおり、今回の実機は陣形保持命令を出していないため）

**未実行＝合格ではありません。**

---

# revision 8 — 試験の補強のみ（製品コードの挙動は変更なし）

revision7 レビュー（`morale-lock-rout-consistency-20260912-review7.md`）の指摘に沿って、
**PlayMode の試験条件だけ**を補強しました。復帰閾値・最短敗走時間の新設（候補 B / C）は
**採用せず現状維持**というご判断に従っています。

## 指摘1：60フレームでは4秒の超過を保証していない（そのとおりでした）

`NaturalRecovery_RoutDoesNotClearWhileStillUnderFire` は 60 フレームを回すだけで、
`routedRecoveryDelay`(4秒) を超えている保証がありませんでした。
実機ログの ~200fps なら 60 フレームは約 0.3 秒です。

さらに致命的なのは、**`Time.time` が 4 秒未満のうちに走ると
修正前（`lastCombatTime = 0` のまま）でも門が閉じていて合格してしまう**点です。
＝**修正の証拠にならない試験**でした。

### 直した内容

`EstablishOpenRecoveryGateThenRout` を新設し、両試験の前提を揃えました。

```csharp
// ① 非交戦のまま、ゲーム時間で待ち時間を超過させる
Time.timeScale = FastForward;              // 8倍速＝実時間は短く、ゲーム時間は確実に進める
float idleStart = Time.time;
while (Time.time - idleStart < delay + 1f) yield return null;
Assert.GreaterOrEqual(Time.time - idleStart, delay,
    "前提：非交戦の経過がゲーム時間で待ち時間を超えていること（超えないと修正前でも通る）");

// ② 通常の被弾で敗走させる（生存も assert）
```

これで「**修正前なら確実に門が開いている**」状態を作ってから観測します。
その状態で敗走が解けなければ、被弾が待ち時間を数え直している証拠になります。

継続被弾の区間も**フレーム数ではなくゲーム時間**で回し、経過を assert します。

```csharp
float fireStart = Time.time;
while (Time.time - fireStart < delay + 1f) { … TakeDamage(SustainHit) … }
Assert.GreaterOrEqual(Time.time - fireStart, delay,
    "継続被弾の観測がゲーム時間で待ち時間に届いていない＝修正前でも通る試験になっている");
```

**被弾量**は継続用に小さめの `SustainHit = 200`（最終約130）にし、
長く撃ち続けても兵力 100,000 に対して数千の消費で済むようにしました。
ループ内と最後の判定で**生存を assert** します。

## 指摘2：「最終的に復帰する」だけでは境界を見ていない（そのとおりでした）

`NaturalRecovery_RecoversAfterFireStops` を
**`NaturalRecovery_RoutPersistsUntilDelayThenRecovers`** に作り直し、
最後の被弾からの**境界の前後**を実コンポーネントで確かめます。

| 区間 | 検証 |
|---|---|
| 最後の被弾 〜 待ち時間の 0.9 倍 | **敗走が続く**こと（1フレームごとに assert・解けたら失敗時刻を表示） |
| 待ち時間経過後 | **立ち直る**こと＋**士気が正**であること＋生存 |

「待ち時間未満の観測が実際に進んだ」ことも assert し、区間が空振りにならないようにしています。

## 指摘3：`IsCombatContact` は製品から未使用（そのとおりでした）

製品（`FleetMorale`）は被弾時に直接 `lastCombatTime` を更新しており、
`RoutRecoveryRules.IsCombatContact` を**呼んでいません**
（毎フレームの被弾有無を持ち回らずに済むため）。

**この関数の単体合格を製品接続の根拠にしない**旨を、
関数の XML ドキュメントと EditMode テストの両方に明記しました。
配線の証拠は PlayMode の `NaturalRecovery_*` が担います。

## 検証結果（revision 8）

| 項目 | 結果 |
|---|---|
| 6アセンブリ コンパイル | 合格（`error CS` 0件） |
| TestHarness | **9,691件合格・0失敗**（revision7 から増減なし＝純ロジックは無修正） |
| **PlayMode 9件** | **未実行**（Unity 必要） |
| **製品コードの挙動** | **変更なし**（revision7 の `FleetMorale` ＋ `MoraleLockRules` ＋ `RoutRecoveryRules` のまま） |

### 変更ファイル（revision 8 ぶん）

| ファイル | 内容 |
|---|---|
| `Assets/Tests/PlayMode/MoraleLockRoutPlayModeTests.cs` | 前提づくりを共通化（ゲーム時間で待ち時間超過＋敗走）／継続被弾をゲーム時間で観測し経過を assert／境界前後の検証へ作り直し／生存 assert／`SustainHit`・`FastForward` を導入 |
| `Assets/Scripts/Core/Combat/RoutRecoveryRules.cs` | **doc コメントのみ**（`IsCombatContact` は製品未使用＝単体合格を配線の根拠にしない旨） |
| `Assets/Tests/EditMode/RoutRecoveryRulesTests.cs` | 同趣旨の注記を追加 |

## 再検証のお願い

1. `MoraleLockRoutPlayModeTests` **9件**（一括＋`NaturalRecovery_*` の2件を単独でも）
   - 補強後は各試験が**ゲーム時間で待ち時間を跨ぐ**ため、1件あたり数秒かかります（8倍速で短縮済み）
2. `FleetFormationHoldPlayModeTests` 17件の回帰
3. 実機：不退転がない時間帯に `★敗走 開始` の直後へ `敗走 解除` が続かないこと。
   あわせて攻撃が途切れた場面では従来どおり立ち直ること
4. **保持ありの回帰は別途**（今回の実機は陣形保持命令を出していないため）

**未実行＝合格ではありません。**

## 仕様判断の記録

復帰閾値（候補 B）・最短敗走時間（候補 C）は**採用せず現状維持**。
`IsRouted` の定義は `士気 <= 0`（不退転中を除く）のままです。
別検討となった場合は新しい task_id でお願いします。

---

# revision 9 — revision8 の失敗は試験条件の欠陥（製品コードの挙動は変更なし）

`NaturalRecovery_RoutDoesNotClearWhileStillUnderFire` の単独実行が失敗
（非敗走の検出 545 回／最終士気 1.014／観測 5.0 ゲーム秒・証跡 `morale-lock-r8-underfire-20260912.xml`）。
レビューの仮説どおり、**AI の特殊指揮『不退転』の混入**でした。
**製品の回帰ではありません**（不退転は敗走を止めるのが仕様）。

## 原因（コードで確定）

配線を順にたどると、この試験構成では**必ず**不退転が候補に挙がります。

| # | 場所 | 事実 |
|---|---|---|
| 1 | `FleetAI.Update`（241-246行） | `Time.time >= nextSearchTime` で `ConsiderActiveCommand()` を呼ぶ。`searchInterval` 既定 **2.0 秒** |
| 2 | `FleetAI.ConsiderActiveCommand`（348行） | `BattleAiRules.ShouldAct(AiSkill(), Random.value)` のゲート。`admiralData` が無いと `AiSkill()` は **0.5 ＝ 1/2 の抽選** |
| 3 | `BattleAiRules.TryChooseCommand` | `if (moraleRatio < 0.4f) { cmd = 不退転; return true; }`。**敗走中は士気比 0** ＝ 毎回ここに落ちる |
| 4 | `ActiveCommandRules.Spec(不退転)` | `moraleLock = true`・**持続 8 秒**・クールダウン 30 秒 |
| 5 | `ActiveCommandState.Activate` | `strength.activeMoraleLock = true` |
| 6 | `FleetMorale.MoraleLocked` → `MoraleLockRules.IsRouted(morale, true)` | **士気に関係なく false**。同時に `Clamp` の下限が `LockedFloor = 1` になる |

`autoFormation = false` は陣形の自動切替を止めるだけで、**特殊指揮は止まりません**。

### 数字が一致します

- **最終士気 1.014** — これが決め手です。不退転の下限は `MoraleLockRules.LockedFloor = **1f**`。
  乗っていなければ下限は 0 で、士気は 0.00x 台にしかなりません。**1.0 台は不退転の署名**です
  （1 で止まったところへ `UpdateMorale` の非交戦回復 `recoveryRate × deltaTime` が少し乗って 1.014）。
- **持続 8 秒 ≧ 観測 5 秒** — 一度発動すると観測窓を丸ごと覆います。
- **545 回** — ループは1周につき2回数える（被弾直後と Update 後）ので、
  約 272 フレーム＝**観測のほぼ全域**が不退転下。1回の発動で全域が覆われた形と一致します。
- revision7 までの 60 フレーム（約 0.3 秒）は**最初の AI ティック（+2 秒）にすら届いていなかった**ため
  混入せず、観測をゲーム時間で長くした revision8 で初めて露出しました。

### なぜ他の7件は無事か

| 試験 | 露出 |
|---|---|
| `Locked_*` 4件 | 自分で不退転を発令済み。AI の再発令は `Activate` の「効果中は重ねがけ不可」で false ＝無害 |
| `AfterLockExpires_NormalRoutResumes` | 効果切れを待つが、クールダウン **30 秒** > 経過 8 秒 ＝ 再発令されない |
| `WithoutLock_NormalRoutStillHappens` | 約16フレーム（timeScale 1）＝最初の AI ティック（2秒）前に終わる |
| `Fixture_DamageActuallyDrainsMorale` | 1フレーム |

＝**露出していたのは自然回復の2件だけ**なので、修正もそこに限定します。

## 修正（試験のみ・製品コードは無変更）

### ① 自然回復2件で FleetAI を止める（干渉の切り分け）

```csharp
// BuildFleetWithNaturalRecovery
go.GetComponent<FleetAI>().enabled = false;
```

止めるのは**これだけ**です。試験対象の経路

`FleetStrength.TakeDamage` → `FleetMorale.OnTakeDamage` → `FleetMorale.Update` → `RoutRecoveryRules.CanRecover`

は **FleetAI を通りません**。実 `TakeDamage` と実 `Update` による被弾・回復はそのまま動きます
（`recoveryRate` は既定 0.7 のまま＝回復を無効化していません。
士気や敗走フラグを直接書き換えてもいません）。

FleetAI を必要とする `Locked_SupportOrderIsNotInterruptedByDamage` があるため、
`BuildFleet`（共通）ではなく**自然回復用の組み立てだけ**に入れています。

### ② 不退転が乗っていないことを観測中も assert

```csharp
private static void AssertNoMoraleLock(FleetStrength strength, string where)
    => Assert.IsFalse(strength.activeMoraleLock, where + "：不退転が乗っている（特殊指揮が混入…）");
```

前提づくり（待機中・敗走させる被弾中・敗走した時点）と、
**両試験の観測ループの毎フレーム**、および判定の瞬間に置きました（計13か所）。

これで今回のような混入は「敗走が解けた」ではなく
**「不退転が乗っている」**として落ちます＝原因の取り違えが起きません。
`WithoutLock_NormalRoutStillHappens` にも同じ理由で1か所足しています。

## 検証結果（revision 9）

| 項目 | 結果 |
|---|---|
| 6アセンブリ コンパイル | 合格（`error CS` 0件） |
| TestHarness | **9,691件合格・0失敗**（revision8 から増減なし＝純ロジック無修正） |
| **PlayMode 9件** | **未実行**（Unity 必要）＝**合格ではありません** |
| **製品コードの挙動** | **変更なし**（`FleetMorale` / `MoraleLockRules` / `RoutRecoveryRules` は revision7 のまま） |

### 変更ファイル（revision 9 ぶん）

| ファイル | 内容 |
|---|---|
| `Assets/Tests/PlayMode/MoraleLockRoutPlayModeTests.cs` | 自然回復2件で FleetAI を無効化（理由をコメントで明記）／`AssertNoMoraleLock` を新設し前提と観測ループへ計13か所／失敗の経緯を doc コメントに記録 |

製品コード（`Assets/Scripts/`）は**1行も触っていません**。

## 再検証のお願い

1. `NaturalRecovery_RoutDoesNotClearWhileStillUnderFire` と
   `NaturalRecovery_RoutPersistsUntilDelayThenRecovers` を**単独で**
2. `MoraleLockRoutPlayModeTests` 9件を一括
3. `FleetFormationHoldPlayModeTests` 17件の回帰
4. 通常会戦：不退転中／終了後、無保護の継続被弾／被弾停止、陣形保持

**万一また落ちた場合の読み方**：失敗文が「不退転が乗っている」なら**試験条件**、
「撃たれ続けているのに敗走が解けた」なら**製品側**です。今回の切り分けで両者が混ざらなくなりました。

**未実行＝合格ではありません。** 仕様1は未承認のままです。
