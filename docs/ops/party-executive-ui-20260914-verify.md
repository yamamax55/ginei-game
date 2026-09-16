# 党人事メニュー 検証手順（ChatGPT 実行用）

Task: `party-executive-ui-20260914` / attempt a2 / revision 2
実装と根拠は `party-executive-ui-20260914-result.md`。**Claude 側は編集停止済み**（`review_ready`）。

> Claude のセッションから Unity は動かせません（Unity MCP 未接続）。
> 以下は ChatGPT が Unity で実施する手順です。

---

## A. PlayMode（自動）

### A-1. 新規：`PartyExecutivePanelPlayModeTests`（3件）

| 試験 | 合格の意味 |
|---|---|
| `Panel_BuildsScrollableListsAndClickEntries` | 窓が組める（スクロールバー・絞り込み/理由の入力欄・操作ボタンが揃う） |
| `Leader_DismissThenAppointThroughPanel_Succeeds` | 党首本人で 理由なし失敗 → 解任 → 確認 → 確定で任命。台帳に載り、年次の補充で差し替わらない |
| `NonLeaders_CannotAppointOrDismiss_ReasonShownAndNothingChanges` | 党首でない操作者（与党の非党首・他党）は確認も実行も失敗し台帳不変 |

**まず3件を単独でも1回ずつ**流してください（一括だけだと他試験の static 残りに紛れます）。

### A-2. 回帰：内閣まわり（共通入口 `GalaxyView.Cabinet.cs` を触ったため）

| 試験ファイル | 件数 |
|---|---|
| `CabinetAppointmentPanelPlayModeTests` | 4 |
| `CabinetLiveContextPlayModeTests` | 6 |
| `CabinetDecisionBridgePlayModeTests` | 3 |
| `CabinetPostsPlayModeTests` | 2 |

**計 15件。** 今回の変更は `GalaxyView.Cabinet.cs` への**追加**（党人事の入口4本）と
`RunCabinetAndPartyExecutives` の**doc コメント1行**だけなので、
ここが落ちたら「隣接変更の巻き添え」を疑ってください。

### 失敗したときの読み分け

過去2タスクで、**試験条件の欠陥を製品の不具合と誤読**しかけたことが2回あります。
落ちたら次の順で切り分けてください。

| 失敗の形 | まず疑うもの |
|---|---|
| 「権限なし」と出て台帳が変わらない試験が落ちる | **操作者の取り違え**（`PlayerCharacter()` が誰になっているか） |
| 年次の補充で在任者が差し替わる | `autoFill` が空席だけを埋めているか（製品側の可能性あり＝**報告してください**） |
| 確認は「可」なのに実行が「否」 | **製品側**。`CheckAppoint` と `TryAppoint` の分岐がずれている＝報告してください |
| 窓の生成そのものが落ちる | シーン・Bootstrap の前提（Strategy シーンか） |

---

## B. 実機（Strategy・目視）

Play で Strategy を開いて、次の4点です。

### B-1. 入口が2つとも効くこと

- 上部メニュー **「党人事」**
- 政治観測（**O**）の **「党人事を開く」** ボタン

どちらからでも同じ窓が開き、**Esc で手前から1枚だけ閉じる**こと
（他の窓を開いた状態でも順番が崩れないか一度確認してください）。

### B-2. 主人公が**党首のとき**

1. 三役のどれかを選び **解任** → 理由を入れずに確定できないこと（ボタンが押せない）
2. 理由を入れて解任 → 空席になり、履歴に載ること
3. 候補を選んで **任命** → 在任者が入れ替わること
4. 通知（人事カテゴリ）に **党首名と理由**が出ること

### B-3. 主人公が**党首でないとき**

- ヘッダに「あなたは○○＝任免権なし」等が出ること
- 確認に **不可の理由**と、**任免権者の名前**が出ること
- 確定ボタンが押せないこと、台帳が変わらないこと

対象は**与党の非党首**（閣僚・党三役を含む）と**他党の人物**の両方。

### B-4. 年をまたいでも手動任命が残ること

手動で三役を任命した状態で**年境界を越え**、
`RunCabinetAndPartyExecutives` の自動補充が走っても
**在任者が差し替わらない**こと（空席だけ埋まる）。

→ 差し替わったら**製品側の不具合**です。報告してください。

---

## C. 報告に含めてほしいもの

1. PlayMode の結果 XML（`docs/ops/` へ）と、単独実行の可否
2. 実機は B-1〜B-4 それぞれの可否。落ちたものは**画面の文言そのまま**
3. 落ちた場合、A の「読み分け」表のどれに当たりそうか（断定不要）

**未実行＝合格ではありません。** 不明な項目は「未実施」と書いてください。

---

## D. 補足

- `commit/push なし`（HEAD は `de4d0820`）。作業ツリーに未コミットのまま置いてあります。
- 状態ファイルの `updated_at` は**実時計（JST）で記録**しました。
  前回の「時計が無いので記録しない」運用に戻すべきなら指示してください。
