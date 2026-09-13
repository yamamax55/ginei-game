# 仕様1 最終QA補助と残る統合試験 — 実装結果

Task: `fleet-formation-hold-final-qa-20260910`
Issue: https://github.com/yamamax55/ginei-game/issues/2253（確定仕様1）
依頼＝[fleet-formation-hold-final-qa-20260910.md](fleet-formation-hold-final-qa-20260910.md)／
前段＝[fleet-formation-hold-fix2-chatgpt-qa-20260910.md](fleet-formation-hold-fix2-chatgpt-qa-20260910.md)（一括14/14・個別1/1合格）。
**編集停止済み**。commit / push なし。既存セーブ・v5・無関係な差分は保護。
**製品コード（`Assets/Scripts`）は一切変更していません。仕様2にも進んでいません。**

---

## 依頼1：不足する統合試験を補いました（PlayMode 14件 → **17件**）

すべて実コンポーネントの公開経路だけを使い、結果状態（保持・override・撤退）は**書いていません**。

### `Hold_SurvivesPauseResumeAndFastForward`（新規）

一時停止 → **停止中に陣形を指定** → 再開 → 3倍速、と時間操作を跨いで保持が続くこと。
AI は `autoFormation = true` / `searchInterval = 0.02` で上書きを狙わせています。

- 停止中でも指定を受理できること（アクティブポーズと同じ状況）
- 停止中・再開後・倍速中それぞれで保持と陣形が変わらないこと
- `Time.timeScale` は TearDown で必ず元へ戻します（依頼2）

### `Hold_SurvivesAttackOrderEndingAndTargetLoss`（新規）

直接の攻撃命令中に**標的を破棄**し、

1. `HasManualTarget` が落ちる（前提の崩れを検出）
2. 命令が完了して `OverrideKind == なし` へ戻る
3. **それでも陣形の保持は続く**

を順に assert します。攻撃の終了は「命令の完了」であって「陣形をやめる理由」ではない、という仕様の確認です。

### `NextBattle_DoesNotInheritHoldOrCorpsRetreat`（新規）

会戦をまたぐ漏れの確認です。**実際のオブジェクト寿命**で検証しています。

- 前の会戦で総退却を発令させる → `IsCorpsRetreatOrdered` を前提 assert
- **全オブジェクトを破棄**（＝シーンの寿命に相当）
- 次の会戦として**同じ軍団名**で組み直す（キーが一致するので漏れていれば必ず刺さる）
- 保持が無いこと／指定元が `なし` に戻っていること／
  **総退却の発令状態が漏れていないこと**／新しい会戦で普通に指定できること

> **シーンロードを使わなかった根拠**：陣形の保持は `Squadron` の非直列化フィールドなので、
> オブジェクト破棄で必ず消えます。跨いで漏れうるのは **static** の
> `BattlefieldCommandManager.corpsRetreatOrdered` だけで、これは `Awake` でリセットされます。
> `SceneManager.LoadScene("Battle")` に依存させると Build Settings とシナリオ解決に縛られ、
> 試験の失敗原因が本題からずれます。**最小の代替検証手順**として、実機側の手順5で
> 「会戦→結果→タイトル→もう一度会戦」を1回踏んでいただく形にしました（下記）。

既存の所属変更試験・到着試験とは重複していません。

---

## 依頼2：試験間の共有状態を復元しました

指摘のとおり、旧 TearDown は `spawned` の破棄だけで、
`SupportRequest` 試験が `StrategySession.Clock` の `paused` / `speed` を変えたまま返していました。
（fix1 で支援要請の試験が落ちた原因も「前段が残したクロック状態」でした。）

```csharp
[SetUp]  savedTimeScale / savedClockPaused / savedClockSpeed を控える
[TearDown] spawned 破棄 → Time.timeScale と Clock.paused / Clock.speed を復元
```

今回追加した `Time.timeScale` の操作もこれで戻ります。順序依存を作りません。

---

## 依頼3：検証会戦限定の状態ダンプを追加しました

**`Ginei/QA: 陣形保持 検証会戦の状態を出力（Play中）`**（読み取り専用・QA 3軍団だけが対象）

| 区分 | 出す内容 |
|---|---|
| 時間 | `Time.timeScale`／`PauseManager.IsPaused`／暦・時刻／統一クロックの経過・速度・停止中 |
| 艦隊ごと | **一意名**（Hierarchy の `QA_OWN_…` 等）／軍団／提督 |
| 陣形 | 現在陣形／保持／**指定元**／最後に決めた出どころ／HUD と同じ表示文字列／**スキルポイント**（現在/上限）／保持時の軍団キー |
| 命令 | 移動中／行き先／手動標的／**override の出どころ** |
| 状態 | 敗走（士気つき）／AI状態／**軍団総退却の発令中** |
| 通知 | `NotificationCenter` の直近から**陣形・支援・保持**を含む行を最大12件 |

UI 通知は流れて消えるので、**入力操作の結果をログで照合する**のが狙いです（依頼の目的そのまま）。
全文は Console の `[陣形保持QA]` に出し、ダイアログには件数だけ出します（長文で切れないように）。

---

## 依頼4：時間操作は `PauseManager` の正規APIだけを通しました

**`Ginei/QA: 時間 一時停止／再開（Play中）`** … `PauseManager.TogglePause()`
**`Ginei/QA: 時間 1倍速 / 2倍速 / 3倍速（Play中）`** … `PauseManager.SetTimeScale(n)`

- `Time.timeScale` を**別系統で書き換えていません**。Space・停止ボタンと同じ状態を共有します
- 停止中に倍速を指定した場合は `savedTimeScale` が更新され、**再開時にその速度になる**
  （製品の挙動そのまま）ので、その旨をダイアログに出します
- `PauseManager` が無い／停止している（ウィンドウ化会戦）ときは、その理由を表示して何もしません

### 所属変更の最小手順（QA補助つき）

**`Ginei/QA: 陣形保持 保持中の艦を別軍団へ配属換え（Play中）`**

- 前提＝**陣形を保持している QA 艦隊がいること**（無ければ【未成立】で何もしない）
- 変えるのは **`corpsName` という入力条件だけ**。保持の解除は書かず、`Squadron.Update` に判断させます
- 実行後、次の進行フレームで「陣形保持を解除しました（指揮系統の変更）」が出ます

UI にこの操作は無いため（配属換えは戦略側の編制画面の役目）、最小の補助として追加しました。

---

## 検証結果

| 項目 | 結果 |
|---|---|
| 6アセンブリ コンパイル | 合格（Core / Data / Game / Editor / Tests.EditMode / **Tests.PlayMode** すべて `error CS` 0件） |
| TestHarness（EditMode 相当） | 合格（**9,676件・0失敗**。今回変更なし） |
| **PlayMode 17件** | **未実行**（Unity 必要。既存14件＋新規3件） |
| 実機 | 未実施 |
| 既存セーブ | 未変更（`campaign_save.json` Sep 9 13:22 のまま） |
| commit / push | 実施していない（`git log` は `5eb970f2` のまま） |
| **製品コードの変更** | **なし**（`Assets/Scripts` の更新時刻が動いていないことを確認） |

### 変更ファイル

| ファイル | 内容 |
|---|---|
| `Assets/Tests/PlayMode/FleetFormationHoldPlayModeTests.cs` | 統合試験3件を追加／`SetUp`・`TearDown` で共有状態を復元 |
| `Assets/Editor/CommandAuthorityQaMenu.cs` | 状態ダンプ／時間の正規API補助／配属換え補助 |

---

## 実行手順

### A. PlayMode（実行対象名）

`Window > General > Test Runner` → **PlayMode** タブ →
`Ginei.Tests.PlayMode` → `FleetFormationHoldPlayModeTests` を **Run**（17件）。

新規3件は個別でも実行してください：

- `Hold_SurvivesPauseResumeAndFastForward`
- `Hold_SurvivesAttackOrderEndingAndTargetLoss`
- `NextBattle_DoesNotInheritHoldOrCorpsRetreat`

（新規3件は `Time.timeScale` と静的状態を触るので、**一括と個別の両方**での確認をお願いします。
TearDown で戻しますが、そこが効いているかの確認も兼ねます。）

### B. 実機（クリック手順）

1. `Ginei/QA: 指揮権限 検証会戦を開始（戦役・Play中）` → 準備完了ダイアログを閉じる
2. `Ginei/QA: 陣形保持 検証会戦の状態を出力（Play中）` → **基準値**を控える（Console `[陣形保持QA]`）
3. `QA自軍1` をクリック →「陣形 ▸ 円陣」→ HUD「円陣（保持・直接命令）」
4. **時間操作を跨ぐ**：
   `Ginei/QA: 時間 一時停止／再開` → もう一度実行して再開 → `Ginei/QA: 時間 3倍速`
   → しばらく進めてから **状態を出力** →
   `保持=True` / `指定元=直接命令` / `陣形=円陣` が変わっていないこと、
   `timeScale=3.0` になっていること
5. **会戦を跨ぐ**：`Esc` →「タイトルへ戻る」→ もう一度 `Ginei/QA: 指揮権限 検証会戦を開始` →
   **状態を出力** → 全艦が `保持=False` / `最後に決めた=なし` / `軍団総退却=False` であること
   （前の会戦の状態が漏れていないこと）
6. **配属換え**：`QA自軍1` に陣形を指定して保持させてから
   `Ginei/QA: 陣形保持 保持中の艦を別軍団へ配属換え（Play中）` → 再開 →
   「陣形保持を解除しました（指揮系統の変更）」の通知と、状態出力で `保持=False`
7. 終了時は `Ginei/QA: 指揮権限 検証を撤去` → Play OFF

---

## 実機未検証点

- **PlayMode 17件は未実行**です（私の環境では Unity を動かせません）。**合格扱いにしないでください。**
- 上記 B の実機手順1〜7も未実施です。
- 手順5（会戦をまたぐ漏れ）は、PlayMode 側をオブジェクト寿命で代替しているため、
  **実機で1回踏んでいただくのが本番の確認**になります。

## 制約の遵守と申し送り

- **製品コードは変更していません。** 今回、製品側の問題は見つかっていません。
- **敗走QA**：作業票の注意どおり、敵から離れた非交戦艦は士気0が最初の進行フレームで回復します。
  PlayMode 側は `recoveryRate = 0` を**試験入力として明記**しており、
  自然戦闘での持続敗走とは別物です。実機の敗走確認は**交戦中の艦**で行ってください
  （`陣形保持 保持中の艦を敗走させる` は敵との距離を表示し、近すぎる場合は警告します）。
- 手動解除後に HUD の指定元が残る表示は**改善候補のまま**（仕様2へ越境しない）。
- 入力取りこぼし・未解放オブジェクト警告・QAメニュー全体分類は**別件**として触っていません。

**編集停止**。
