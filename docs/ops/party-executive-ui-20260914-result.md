# 党人事メニュー（党三役の手動任免） — 結果

Task: `party-executive-ui-20260914` / attempt a2 / revision 1
関連 #2768 / #159 / #165。`CabinetAppointmentPanel`（内閣人事・`cabinet-appointment-ui-20260914`）の党版。

**やったこと**：党首が党三役（幹事長・政調会長・総務会長）を**手動で任免する窓**を作り、
既存の `PartyExecutiveRules`（Core）へ繋いだ。判定の数値・権限の仕様は**変えていない**。

---

## 1. できたもの

### `PartyExecutivePanel`（Game・新規 882行）

Strategy シーンに自動生成される非モーダル窓。入口は2つ：

- `StrategyMapWindow` の上部メニュー **「党人事」**
- 政治観測（**O**）の **「党人事を開く」** ボタン

表示するもの：

| 区画 | 中身 |
|---|---|
| ヘッダ | 操作者の各党での立場（**党首＝任免権者** ／ 党三役・党員・他党＝**権限なし**）と党首 |
| 党・職 | 幹事長／政調会長／総務会長の在任者・派閥・当選年功・就任年・任命者・暫定期限・役割・空席理由 |
| 履歴 | その党の直近の任免履歴 |
| 候補 | 名前で絞り込み／適格者のみ⇔全員／能力・当選年功・所属・派閥の評価と**不適格の理由** |
| 確認 | 「確認 → 確定して実行」の2段。可否と理由、権限外なら**任免権者の名前**を出す |

**党首の任命入口は作っていない**（党首は総裁選で決まるため）。

### 共通入口（`GalaxyView.Cabinet.cs` +79行）

```
PartyOperationForPlayer()        … 操作者・勢力・政治状態・名簿・暦年を組む（組めなければ理由）
CheckPlayerPartyAppoint/Dismiss  … 確認（状態を変えない）
PlayerPartyAppoint/Dismiss       … 実行（理由必須・成功時に人事カテゴリへ通知）
```

操作者は **`PlayerCharacter()`（主人公）だけ**＝任意の人物を渡せません。
権限判定は `PartyExecutiveRules` に委ね、ここでは何も足していません。

### Core（`PartyExecutiveRules` +21行）

`CheckAppoint` / `CheckDismiss` を追加。**新しい判定は書かず**、
既存の `TryAppoint` / `Dismiss` を `AppointCore` / `DismissCore` に括り出して
`dryRun` フラグで**同じ順序・同じ拒否理由**を返すだけにしています。

```csharp
TryAppoint(...)   => AppointCore(..., dryRun: false);
CheckAppoint(...) => AppointCore(..., dryRun: true);
```

→ **確認の表示と実行時の再判定が食い違いません。**

### 観測層（`PoliticsObserverOverlay` +31行）

政治観測に「党人事を開く」ボタンを1つ。
**観測そのものは read-only のまま**で、操作は専用メニュー側の共通入口に通します。

---

## 2. 守った約束

| 約束 | どうしたか |
|---|---|
| 台帳を二重に作らない | 在任・履歴は **`Party.posts` / `Party.postHistory` だけ**。`GovernmentRegistry` には登録しない |
| 党三役に過剰な権限を与えない | 政府の決裁・国庫・軍の指揮権は**付与しない**（`PartyExecutiveRules.Authority` のまま） |
| 年次の自動補充と衝突しない | `RunCabinetAndPartyExecutives` の `autoFill` は**空席だけ**を埋める＝手動で任命した在任者を差し替えない |
| Esc の優先順位を壊さない | `UIWindowStack.Register` / `Unregister` するだけ（`escapeKey` を直読みしない） |
| 窓の意匠を二重実装しない | タイトルバーは `WindowChrome`、スクロールバーは `UiScrollbars` |
| 判定を二重実装しない | 可否は全て `PartyExecutiveRules` 経由。パネルは表示と選択だけ |

---

## 3. 確認した設計上の要点

**確認（dry-run）と実行の判定は本当に一致するか** — `AppointPost` の内部条件
（`party != null` ／ `IsMember` ／ `personId >= 0`）はいずれも dry-run の戻り位置より**手前**で弾かれます
（`IsMember` は `CandidateProblem` の1行目、負の id も `IsMember` が false）。
＝**確認で「可」と出たものが実行で「党籍がない」になることはありません。**

**理由（reason）の必須判定は `Check*` に無く `Player*` にだけある** — これは意図どおりです。
理由は UI の入力であって権限の規則ではないため、パネル側が
`confirmButton.interactable = r.ok && reasonOk` で閉じ、実行時にも再確認します。

---

## 4. 試験

### EditMode（`CabinetAppointmentRulesTests` +37行・1件追加）

`PartyExecutives_CheckMatchesExecution_AndDoesNotMutate`

- 権限外・適格・空席それぞれで **`Check*` と `Try*`/`Dismiss` の可否・拒否理由・`petitionToId` が一致**
- **確認では状態が動かない**（在任者・`postHistory` の件数が変わらない）

### PlayMode（`PartyExecutivePanelPlayModeTests`・新規3件）

実 `GalaxyView`（政府シード＋年次の政治 Tick で組閣・党首・党三役まで）に対して実施。

| 試験 | 見るもの |
|---|---|
| メニュー生成 | スクロールバー・入力欄（絞り込み／理由）・操作ボタンが揃う |
| 党首本人 | 理由なしは失敗 → 理由つき解任 → 確認 → 確定で任命。台帳に載り、**年次の補充で差し替わらない** |
| 党首でない操作者 | 与党の非党首（閣僚・党三役を含む）／他党の人物は**確認も実行も失敗**し台帳は不変・確定ボタンも押せない |

`Start` と実セーブは走らせず、TearDown で static・Registry・Clock・`Active` を戻します。

### 結果

| 項目 | 結果 |
|---|---|
| 6アセンブリ コンパイル | **合格**（`error CS` 0件） |
| TestHarness | **9,921件合格・0失敗**（新規 EditMode 1件を単独でも確認） |
| **PlayMode 3件** | **未実行**（Unity 必要）＝**合格ではありません** |

---

## 5. 変更ファイル

| ファイル | 種別 |
|---|---|
| `Assets/Scripts/Game/PartyExecutivePanel.cs` | 新規（882行）＋`.meta` |
| `Assets/Tests/PlayMode/PartyExecutivePanelPlayModeTests.cs` | 新規（301行・3件）＋`.meta` |
| `Assets/Scripts/Core/Government/PartyExecutiveRules.cs` | `CheckAppoint`/`CheckDismiss`（dry-run 括り出し・+21） |
| `Assets/Scripts/Game/GalaxyView.Cabinet.cs` | 党人事の共通入口4本（+79） |
| `Assets/Scripts/Game/PoliticsObserverOverlay.cs` | 「党人事を開く」ボタン（+31） |
| `Assets/Scripts/Game/StrategyMapWindow.cs` | 上部メニュー「党人事」（+2） |
| `Assets/Tests/EditMode/CabinetAppointmentRulesTests.cs` | 確認＝実行の一致試験（+37） |
| `CLAUDE.md` / `docs/catalog/components-catalog.md` | 索引・カタログへ1行 |

---

## 6. 次にお願いしたいこと

1. **PlayMode**：`PartyExecutivePanelPlayModeTests` 3件
2. **PlayMode 回帰**：`CabinetAppointmentPanelPlayModeTests`（共通入口の隣接変更のため）
3. **実機**：Strategy で
   - 上部メニュー「党人事」と 政治観測（O）の「党人事を開く」の両方から開くこと
   - 主人公が**党首のとき**：解任 → 任命が通り、理由未入力では確定できないこと
   - 主人公が**党首でないとき**：権限なしと表示され確定できず、**任免権者の名前**が出ること
   - 年をまたいでも**手動で任命した三役が自動補充で差し替わらない**こと

**未実行＝合格ではありません。**

---

## 7. 補足（状態ファイルの時刻について）

前回の状態ファイルには「時計を読むツールが無いため時刻は記録しない」との注記がありましたが、
**本セッションは実時計を読めます**。今回から `updated_at` に実時刻（JST）を入れています。
運用をどちらに揃えるかは指示に従います。
