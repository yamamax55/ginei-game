# 決裁デスク — 非ブロッキングのイベント/決裁トリアージ（EPIC #DESK）

> イベントや決裁で**時間を止めない**。届いたものは**重要度つきで右下に積み上がり**、最小化でき、規定時間で**AIが機械的に自動選択**する。**本当にやばいものだけ時間が止まる**（アクティブポーズ）。
> ＝コーエー/パラドックスの助言「管理は例外だけに・プレイヤーを溺れさせない」（**例外管理**）の操作系。目安箱（#1296）の諮問/裁可も、この同じ右下スタックへ積み上がる。
> 既存 `NotificationCenter`/`NotificationFeed`（左下・受動トースト）の**能動版**＝右下の決裁スタック。`EventEngine`/`GameClock` を作り直さず接続する。
> 状態：設計メモ（実装未着手）。数値・創作裁定は【要・作者判断】。

---

## 0. なぜこの設計か — 「時間が止まる」をやめる

現状、戦略イベントは `StrategyEventPanel`（S6）で **`Time.timeScale=0` のモーダル**として提示される＝**1件ごとに時間が止まる**。これはイベントが増えるほど「ポップアップを閉じ続ける作業」になり、批判レビューの「書類仕事シミュ化」そのものに転ぶ。

コーエー/パラドックス両者の助言は一致していた：**管理は例外だけにしろ（management by exception）**。だからイベント/決裁の提示を、次の原則で作り直す：

1. **時間は止めない**（既定）。イベントは時間の流れの中で届く。
2. **重要度で振る舞いを変える**。本当にやばいものだけ時間を止める。
3. **積み上がる・最小化できる**。右下にスタックし、後で見られる。
4. **放置はAIが捌く**。規定時間で最小化→さらに経過でAIが機械的に既定選択。

これは目安箱（#1296）と表裏：目安箱の**諮問/裁可も同じ右下スタックへ流れ込む**。＝**イベント（システム発・降ってくる）と決裁（目安箱発・上げる/問われる）の提示を一本化**する。

---

## 1. 重要度と振る舞い（設計の核）

`DecisionSeverity` で「時間を止めるか／締切／AI自動解決するか」を一括で決める。

| 重要度 | 時間 | 提示 | 締切超で | 例 |
|---|---|---|---|---|
| **情報** | 止めない | 左下トースト（既存 `NotificationFeed`）のまま | 自動消滅 | 造船完成・占領通知 |
| **通常** | 止めない | 右下スタックに積む | 最小化→AIが既定選択 | 軽微な内政決裁・地方の小案件 |
| **重要** | 止めない（バナーで強調） | 右下スタック＋上部バナー | 最小化→（長めの締切後）AI自動選択 | 増税/減税・人事・条約提案 |
| **重大** | **止める（アクティブポーズ）** | 中央モーダル（既存 `StrategyEventPanel` を流用） | **自動解決しない＝必ず人へ** | 易姓革命・クーデター・宣戦・滅亡級 |

→ **「本当にやばいやつ」だけが手を止めさせる**。それ以外は流れの中で捌くか、AIに委ねる。これが例外管理。

> 既存 `NotificationSeverity{情報,注意,警告}` と整合させる（情報→情報トースト、注意→通常/重要、警告→重大寄り）。重複 enum を新設せず対応表を持つ。

---

## 2. プレイヤーの体験フロー

```
イベント/裁可が発生
   │  重要度を付与
   ├─ 情報 ─────────────▶ 左下トースト（既存）・操作不要
   ├─ 通常/重要 ─────────▶ 右下スタックに積む（時間は流れ続ける）
   │        │ 規定時間 選択なし → 最小化（バッジに件数）
   │        │ さらに経過       → AIが機械的に既定選択（status-quo/最小コスト）→ 通知
   │        └ プレイヤーがクリック → 展開して決裁（承認/却下/選択肢）
   └─ 重大 ──────────────▶ 中央モーダル＋時間停止（アクティブポーズ）→ 人が決めるまで進まない
```

- **[[no-outcome-preview]] 厳守**：決裁カードは選択肢を見せるが、結果の予測（支持±など）は見せない。余波は事後に通知へ。
- **AI自動選択は"機械的"**：賢い最適化でなく、既定（現状維持/最小コスト/顧問推奨）を淡々と選ぶ＝放置の代償が見える（後で「あの時こうしておけば」が起こる）。

---

## 3. データモデル案（Core・test-first・並行新設しない）

### 3-1. `PendingDecision`（Core・`[Serializable]`）
```
id / title / body
severity   : DecisionSeverity{情報,通常,重要,重大}
source     : DecisionSource{イベント, 諮問, 建白結果, システム}  ← EventEngine / 目安箱(#1296) / その他
choices    : List<string>（選択肢ラベル）   ＋ defaultChoiceIndex（AI/締切用の既定）
effectKey  : string（採択時に呼ぶ効果＝WF/EventEngine と共有・直列化可）
deadline   : 残り game-秒（0以下で締切到来）
status     : DecisionStatus{新着, 提示中, 最小化, 自動解決, 決裁済}
elapsed    : 提示からの経過
```
> 効果は `effectKey` 駆動（`PetitionEffects`/`EventChoice` と同じ実体を共有＝重複適用ロジックを作らない）。

### 3-2. `DecisionQueue`（Core・スタック）
- 有界（`NotificationCenter` のリングバッファ思想＝無制限に溜めない・溢れは古い通常案件から自動解決）。
- `Enqueue`/`Active`（時間を止めるべき重大が先頭か）/`Minimize`/`Restore`/`Resolve`/`MinimizedCount`/`OrderBySeverity`。

### 3-3. `DecisionTriageRules`（static・唯一の窓口）
- `PausesClock(severity)` … 重大のみ true（時間停止の判定）。
- `DeadlineFor(severity, prm)` … 重要度別の規定時間（通常<重要、重大は無期限＝人を待つ）。
- `AutoResolvable(d)` … 重大以外かつ締切超なら true（AIに委ねてよい）。
- `Tick(queue, dt, prm)` … 各決裁の `deadline` を減らし、超過で **提示中→最小化**、さらに超過で **最小化→自動解決**（既定選択を採択し effectKey を返す）。重大は対象外。
- `ClockShouldStop(queue)` … 活性な重大が1件でもあれば true（`GameClock`/`PauseManager` 連携の判定）。

### 3-4. AI機械的自動選択（`DecisionAutoResolveRules` or 上に内包）
- 既定選択の決め方：`defaultChoiceIndex`（現状維持/最小コスト/顧問推奨）を淡々と採択（決定論）。賢く最適化しない＝放置の代償が創発する。

---

## 4. UI（Game・UITK・`NotificationFeed` の能動版）

| 要素 | 中身 | 既存の手本 |
|---|---|---|
| **右下スタック** | 決裁カードを重要度色で縦積み・件数バッジ・新着は軽く主張 | `NotificationFeed`（左下・受動）の姉妹 |
| **最小化/展開** | クリックで折り畳み/展開。最小化はアイコン＋残り時間ゲージ | — |
| **決裁カード** | タイトル＋選択肢ボタン（結果予測は出さない）＋締切ゲージ | 目安箱 受信箱（MEYASU-7） |
| **重大モーダル** | 中央＝時間停止して提示（既存を流用） | `StrategyEventPanel` |

- 右下スタックは `GineiUITK`（UITK 段階移行）。`NotificationFeed`（左下）と棲み分け＝**左下＝知らせるだけ／右下＝あなたの判断を待つ**。
- 時間停止は重大カードが活性な間だけ＝`PausesClock`/`ClockShouldStop` を `GameClock.Pause`/`PauseManager` が読む。

---

## 5. 子Issue（着手順・test-first → 配線）

> **EPIC = #1628**。GitHub issue 起票済み（#1629〜#1634）。

| # | issue | 主眼 | 接続 |
|---|---|---|---|
| **DESK-1** | #1629 | 非ブロッキング決裁キュー（`PendingDecision`/`DecisionQueue`＝スタック・有界・状態機械） | `NotificationCenter` リング思想 |
| **DESK-2** | #1630 | 重要度→時間挙動（`DecisionSeverity`＋`DecisionTriageRules`＝重大のみ時間停止・締切・自動解決可否） | `NotificationSeverity` 対応表 |
| **DESK-3** | #1631 | 規定時間→最小化→AI機械的自動選択（`Tick`＝締切超で最小化、さらに経過で既定選択・決定論） | `GameClock`/暦Tick |
| **DESK-4** | #1632 | GameClock 連携＝重大だけ時間停止（`ClockShouldStop`→`GameClock.Pause`/`PauseManager` 協調・既存 always-pause を解消） | TIME/`PauseManager` |
| **DESK-5** | #1633 | 右下スタックUI＋最小化（UITK＝重要度色・件数バッジ・締切ゲージ・展開/折り畳み） | `NotificationFeed`/`GineiUITK` |
| **DESK-6** | #1634 | イベント/目安箱決裁の合流（`EventEngine` と MEYASU 諮問/裁可 を同じキューへ・`StrategyEventPanel` を重大時のみに） | `EventEngine`#116・MEYASU #1296 |

### 推奨着手順
`DESK-1 → DESK-2 → DESK-3`（Coreトリアージ＝time/severity/auto-resolve を test-first で固定）→ `DESK-4`（時間停止の配線）→ `DESK-5`（右下UI）→ `DESK-6`（イベント/裁可の合流）。

> **MEYASU-7（箱の受信箱UI）はこの DESK の上に乗る**＝目安箱の諮問/裁可カードは DESK の右下スタックに流れる（並行UIを作らない）。

---

## 6. 完了条件（EPIC）
- イベント/決裁で**時間が止まらない**（既定）。**重大だけ**アクティブポーズで手を止めさせる。
- 決裁は**右下に重要度つきで積み上がり**、最小化/展開でき、有界に保たれる。
- **規定時間で最小化→さらに経過でAIが機械的に既定選択**し、放置の代償が後から効く。
- イベント（`EventEngine`）と目安箱の諮問/裁可（#1296）が**同じ右下スタックへ合流**する（提示UIの一本化）。
- いずれも `NotificationCenter`/`EventEngine`/`GameClock`/`PauseManager` を後退させず接続する additive 設計。

---

## 7. 【要・作者判断】
- **重要度別の規定時間**（通常/重要の締切秒・暦圧縮との兼ね合い）。
- **重大の線引き**：何を「時間を止める」級にするか（易姓革命/クーデター/宣戦/滅亡級…）。乱発は例外管理を壊す。
- **AI既定選択の方針**：現状維持か・最小コストか・顧問推奨か（放置の"らしい"失敗のために、賢すぎない方が良い）。
- **キューの上限と溢れ処理**：溜まりすぎたら古い通常案件から自動解決でよいか。
- **左下(通知)と右下(決裁)の境界**：情報/注意/警告のどれを右下へ昇格させるか。

---

## 8. 接続する既存システム（並行新設しない）
- 通知・重要度・リング：`NotificationCenter`/`NotificationFeed`/`NotificationSeverity`（NOTIF #964）
- イベント発火・選択肢・効果：`EventEngine`/`EventRules`/`GameEventDef`/`EventContext`（#116）・`StrategyEventPanel`
- 目安箱の諮問/裁可・効果：MEYASU #1296（`WorkflowRules`/`Petition`/`PetitionEffects`）・MEYASU-7 はこの上
- 統一時間・自動スロー・ポーズ：`GameClock`/`TimeFlowRules`（TIME）/`PauseManager`
- 効果適用の共有：`effectKey`（`PetitionEffects`/`EventChoice` と同実体）
- UI 基盤：`GineiUITK`＋テーマ USS（UITK 段階移行）
- スケーラビリティ：終盤ラグ規律 #1117（有界キュー・暦境界Tick・自動解決で溜めない）
