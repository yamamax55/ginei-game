# morale-headless-qa-fix-20260913 / a1 結果

旧士気 PlayMode 試験3件（headless batchmode nographics で失敗）の原因特定と、試験側の最小修正。
**製品コード・計算式・台帳容量（400）・生存 assert・回復量 assert は変更なし。★コンパイル・試験は未実行。**

## 1. 原因（実コード＋証拠 `battle-qa-fix1/isolated-morale.xml`）

共通の根：**headless nographics では FPS 上限がない**（数千 fps）。試験が「1フレームに1回」を前提にしていた。

| 試験 | 失敗 | 原因 |
|---|---|---|
| `MoraleLockRoutPlayModeTests.NaturalRecovery_RoutDoesNotClearWhileStillUnderFire` | L437「観測中に撃沈された」（0.90 秒）＋ログ「旗艦を失い退却した」 | 観測ループが**毎フレーム** `TakeDamage(SustainHit=200)`。観測はゲーム時間 `delay+1`=5 秒なので、フレームが速いほど発数が増える。200×旗艦軽減0.7×陣形係数 ≈ 140〜170/発 → 約600〜700発で兵力100,000が尽きる。 |
| `MoraleSourcePlayModeTests.RoutedUnderFire_NoNaturalRecoveryAtAll` | L255「観測中に撃沈された」（0.15 秒）＋ログ「旗艦を失い退却した」 | 同上（同じ毎フレーム被弾）。 |
| `MoraleSourcePlayModeTests.Idle_OnlyNaturalRecovery_AndRateMatches` | L198 期待 1.5006 に対し台帳の合計 0.2792 | 士気の上昇量 1.5006 は `0.5×3秒` と一致しており、**回復量そのものは正しい**（L201 の率 assert には届く値）。試験は `MinAbsDelta=0` なので `FleetMorale.UpdateMorale` の自然回復が**毎フレーム1件**台帳に積まれる。3ゲーム秒（8倍速）で約2,150フレーム → `MoraleAuditLog.Capacity=400` を超え、`Record` が古い記録を `RemoveAt(0)`（`Dropped++`）。残った直近400件ぶん（≈400×0.0007）だけが合計された。試験は `Dropped` を見ていなかった。 |

補足（隠れた穴）：`RoutedUnderFire_NoNaturalRecoveryAtAll` の「自然回復0件」判定も、台帳があふれると初めの記録を捨てうるので、`Dropped` を確かめない限り本当に0件かは言えなかった（`MinAbsDelta=0` では士気0のまま変わらない被弾も delta 0 で1件ずつ積まれる）。

## 2. 変更ファイル（試験のみ）

- `Assets/Tests/PlayMode/MoraleLockRoutPlayModeTests.cs`
  - `HitInterval = 0.5f`（ゲーム秒）を追加。継続被弾を**ゲーム時間の間隔**に変更（`nextHit += HitInterval`。1フレームが長いときは遅れぶんを追いつき発射＝総ダメージはゲーム時間に比例し、FPS に依存しない）。
  - 追加 assert：`HitInterval < delay`／発数 ≥ floor((delay+1)/HitInterval)／**各フレームの更新から見た最終被弾までの最大間隔 < delay**（被弾が回復の待ち時間をまたいで続いていることの実測）／兵力が減ったこと（実ダメージ）。
  - 既存の生存・不退転なし・敗走解除0回・経過 ≥ delay・frames>1 の assert はそのまま。
- `Assets/Tests/PlayMode/MoraleSourcePlayModeTests.cs`
  - `HitInterval = 0.5f` を追加し、`RoutedUnderFire_NoNaturalRecoveryAtAll` の継続被弾を上と同じ方式に変更。同じ追加 assert（発数・最大間隔 < delay）に加え、**`MoraleAuditLog.Dropped == 0`**（0件判定に欠落がないこと）。兵力減少・士気0・自然回復0件・敗走継続・生存の assert はそのまま。
  - 試験ローカルの `AuditTally` を追加：台帳を**毎フレーム集計して `Clear` で排出**し、原因別件数と自然回復の増分を累計する。排出のたびに `Dropped == 0` を assert（欠落があれば失敗）。
  - `Idle_OnlyNaturalRecovery_AndRateMatches` はこの累計で判定（他原因0件・自然回復件数>1・上昇量＝自然回復の合計 ±0.05・率 `Rate×経過秒` ・生存）。許容幅・率の式は不変。集計が回ったこと（`drains > 1`）も assert。

変えていないもの：`MoraleAuditLog`（容量400含む）、`FleetMorale`/`FleetStrength`、`routedRecoveryDelay`、許容誤差、新規 QA（`ReproducibleBattleQa*`）のコード。フレームレートの固定（`captureFramerate`/`targetFrameRate`）は使っていない。

## 3. 試験手順（ChatGPT 側で実行）

1. Unity コンパイル（error CS 0件）。
2. PlayMode 旧士気3件を別プロセスで個別実行（headless batchmode nographics・修正前と同条件）：
   - `Ginei.Tests.MoraleLockRoutPlayModeTests.NaturalRecovery_RoutDoesNotClearWhileStillUnderFire`
   - `Ginei.Tests.MoraleSourcePlayModeTests.Idle_OnlyNaturalRecovery_AndRateMatches`
   - `Ginei.Tests.MoraleSourcePlayModeTests.RoutedUnderFire_NoNaturalRecoveryAtAll`
3. PlayMode 新規 `ReproducibleBattleQaPlayModeTests` 10件＋旧32件をまとめて実行。
4. 失敗時は failure message に出る発数・最大間隔・士気値・経過秒を返してもらう。

## 4. 残件

- ★コンパイル・上記すべての試験は**未実行**（本環境は Read/Grep/Glob/Edit/Write のみ）。合格扱いにしない。
- 同ファイルの他の試験（`AfterFireStops_*`、`RoutPersistsUntilDelayThenRecovers` 等）も「敗走までの被弾」は毎フレームだが、発数で上限がある（十数発）ため FPS 依存の撃沈はしない。今回は失敗していないので触っていない。
- `RoutedUnderFire_NoNaturalRecoveryAtAll` の既存コメント「被弾では台帳が動かない」は、`MinAbsDelta=0` では delta 0 の被弾記録が積まれるため厳密には不正確（判定は自然回復の件数のみで影響なし）。コメント修正は範囲外として残した。
- 仕様2・製品バランス変更は範囲外。
