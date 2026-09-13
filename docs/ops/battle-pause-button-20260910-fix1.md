# 停止／再開ボタン 実機差戻し対応（fix1）

task_id: `battle-pause-button-20260910-fix1`
元仕様＝[battle-pause-button-20260910.md](battle-pause-button-20260910.md)／初回結果＝[battle-pause-button-20260910-result.md](battle-pause-button-20260910-result.md)。
**編集停止済み**。commit / push なし。既存セーブ・v5・無関係な変更は保護。

---

## 報告された症状

Strategy Play → `Ginei/QA: 指揮権限 検証会戦を開始` → 準備完了ダイアログを閉じる → 上端の「再開」ボタンを中心で2回クリック。

- 位置・大きさは問題なし
- **背景はホバー／押下らしく変色する**
- しかし `PAUSE` 表示も時刻 `SE796.1.14 18:27` も変わらない
- `Editor.log` 末尾に `Game Paused` のみ、**`Game Resumed` が無い**
- Space / Escape の自動入力も不応答。一方、**艦隊の選択・移動要請メニューは動作した**

## 調べたこと（事実）

| 調査対象 | 結果 |
|---|---|
| `Battle.unity` の EventSystem | **1つだけ**・`m_IsActive: 1`・`m_Enabled: 1`。`m_ActionsAsset` / `m_PointAction` / `m_LeftClickAction` すべて割当済み＝**この経路のモジュール設定は正常** |
| `Strategy.unity` の EventSystem | **存在しない**（実行時生成に依存） |
| Input System | **1.20.0** |
| 実行時の `AddComponent<InputSystemUIInputModule>()` | `Assets/Scripts/Game` の**約40箇所**。1.20 では **入力アクションが空のまま**＝そのモジュールが使われるとポインタもクリックも届かない |
| `PauseManager.Resume()` の到達 | ログに出ていない＝`TogglePause()` が呼ばれていない |
| ボタン側のガード | ボタンが**見えている**時点で `IsTimeInputDeferred` も `IsSystemUiShown` も false（表示条件とクリック時のガードは同一式）。したがって**ガードで見送られたのではなく、クリックイベント自体が届いていない** |

### 症状の切り分けで効く事実

**艦隊の選択・移動は動いた**のに、**UI ボタンだけ効かなかった**。
`FleetCommander` は `Mouse.current.leftButton` を**直接**読み、ボタンは **EventSystem 経由**です。
つまり切れているのは「マウス入力そのもの」ではなく、**EventSystem のクリック配送**です。

その上で「押下の変色は出るのに `onClick` が来ない」に合う原因は主に2つ：

1. **ドラッグ判定**：押してから離すまでに `EventSystem.pixelDragThreshold`（既定 10px）以上動くと、
   モジュールはクリックでなく**ドラッグ**と解釈し `PointerClick` を飛ばしません。
   押下時の変色（`PointerDown`）は出るので、まさにこの見え方になります。
   自動入力が「移動してすぐ押す」形だと、座標が落ち着く前に押下が入って起こり得ます。
2. **アクション未割当のモジュール**：実行時生成の EventSystem が使われた場合（上記40箇所）。
   `Battle.unity` には正常なものがあるため今回は考えにくいものの、経路として実在する欠陥です。

**どちらであったかは実機のログでしか確定できません**。そのため下記のとおり、
原因を判別できる読み取り専用の診断を足し、同時に両方を塞ぎました。

---

## 修正

### 1. 実行時生成モジュールのアクション未割当を塞ぐ（`PauseManager`）

```csharp
public static void EnsureEventSystem()
{
    EventSystem existing = Object.FindAnyObjectByType<EventSystem>();
    if (existing != null)   // 増やさない（重複するとどちらが効くか読めなくなる）
    { EnsureModuleActions(existing.GetComponent<InputSystemUIInputModule>()); return; }

    GameObject esObj = new GameObject("EventSystem");
    esObj.AddComponent<EventSystem>();
    EnsureModuleActions(esObj.AddComponent<InputSystemUIInputModule>());
}

public static void EnsureModuleActions(InputSystemUIInputModule module)
{
    if (module == null) return;
    if (module.actionsAsset != null && module.point != null && module.leftClick != null) return;
    module.AssignDefaultActions();          // ★これが無いと実行時生成のモジュールは無反応
}
```

既存のモジュールが空だった場合も**その場で修復**します（重複 EventSystem を作らない）。

### 2. EventSystem に依存しない直接判定のフォールバック（`BattlePauseButton`）

**本筋は今までどおり `EventSystem → Button.onClick`** です。届かなかったときだけ効く保険を1本足しました。

- `Mouse.current` で「ボタンの内側で押して、内側で離した」を見る（`RectangleContainsScreenPoint`）
- 離した直後には実行せず、**2フレーム待つ**。その間に通常経路が来たら**何もしない**（二重に切り替えない）
- どちらの経路でも最終的に `Toggle()` 1か所を通り、**呼ぶのは `PauseManager.TogglePause()` だけ**
  ＝`Time.timeScale` を触る別経路は増えていません（元仕様を維持）
- ガード（艦隊詳細／編制／システムメニュー／設定）は両経路とも同じものを通ります

ドラッグ判定で弾かれるケースも、アクション未割当のケースも、これで押せるようになります。

### 3. フォールバック時もクリックが盤面へ透過しない（`FleetCommander`）

EventSystem が不調だと `IsPointerOverGameObject()` が false になり、
「ボタンを押したつもりのクリックが盤面にも届く」恐れがあります。そこで UI 判定を1か所に集約し、
停止ボタンの矩形も見るようにしました（ボタン側が直接判定に切り替わるのと同じ条件＝食い違いを作らない）。

```csharp
private static bool IsPointerOverUI()
{
    var es = EventSystem.current;
    if (es != null && es.IsPointerOverGameObject()) return true;
    return BattlePauseButton.IsPointerOverButton();
}
```

あわせて、同じ式をインラインで持っていた箇所（`FleetCommander` の左右クリック判定）もこの窓口へ寄せました。

### 4. 読み取り専用の入力診断（新規 Editor メニュー）

`Ginei/QA: 入力診断 EventSystem とボタンの状態を出力（Play中）`

**何も変更しません**（EventSystem・モジュール・ボタンの状態を書き換えず、停止／再開の代行もしません）。出す内容：

- EventSystem の**個数**／どれが `current` か／アクティブ・有効／シーン名
- モジュールの種別と `actionsAsset` / `point` / `leftClick` の**割当有無**
- `Mouse.current` / `Keyboard.current` の有無、マウス座標、左ボタン押下中か
- `InputSystem.settings.updateMode`（FixedUpdate 処理だと `timeScale=0` で入力が止まる）、`backgroundBehavior`、`Application.isFocused`
- `Time.timeScale`、`PauseManager.IsPaused` と**2つのガードの現在値**
- ボタンの表示中／押せる／ポインタが上にあるか
- **直近のクリックがどの経路で届いたか**と、経路別の到達回数
- `IsPointerOverGameObject()`、**`pixelDragThreshold`**、ポインタ直下の UI（上位5件）

**読み方**

| 診断の出方 | 意味 |
|---|---|
| EventSystem 経由が 0、直接判定が増えている | 原因は **EventSystem 側**（ドラッグ判定 or アクション未割当）。ボタンは保険で動く |
| どちらも 0 のまま | クリックが**アプリへ届いていない**＝自動入力の制約が濃厚 |
| EventSystem 経由が増えている | 通常経路が復旧している |
| `pixelDragThreshold` を超える移動が疑わしい | 自動入力を「移動 → 1フレーム待つ → 押下 → 離す」に分ける |

---

## 検証結果

| 項目 | 結果 |
|---|---|
| 6アセンブリ コンパイル | 合格（`error CS` 0件） |
| TestHarness 全件 | 合格（**9,640件・0失敗**。純ロジックの変更なしのため件数据え置き） |
| 既存セーブ | 未変更（`campaign_save.json` Sep 9 13:22 のまま） |
| commit / push | 実施していない |
| 元仕様の維持 | `Time.timeScale` の別経路なし／状態は `PauseManager` が唯一の出所／直前速度は `Resume` 任せ／モーダルのポーズ維持／クリック透過防止 — すべて維持 |

### 変更ファイル

| ファイル | 内容 |
|---|---|
| `Assets/Scripts/Game/PauseManager.cs` | `EnsureEventSystem` を重複回避＋アクション割当まで面倒を見るよう修正、`EnsureModuleActions` を追加 |
| `Assets/Scripts/Game/BattlePauseButton.cs` | 直接判定フォールバック、経路の記録（診断用の読み取りプロパティ）、`Toggle()` へ一本化 |
| `Assets/Scripts/Game/FleetCommander.cs` | UI 判定を `IsPointerOverUI()` へ集約し、停止ボタンの矩形も見る |
| `Assets/Editor/InputDiagnosticsQaMenu.cs` | **新規**・読み取り専用の入力診断 |

---

## 実機手順（ChatGPT へ）

1. Strategy Play →`Ginei/QA: 指揮権限 検証会戦を開始（戦役・Play中）`→ 準備完了ダイアログを閉じる。
2. **まず**`Ginei/QA: 入力診断 EventSystem とボタンの状態を出力（Play中）`を実行し、
   出力（Console にも出ます）をそのまま控える。＝**押す前の基準値**。
3. 上端の「再開」ボタンを中心で1回クリック。
   - 期待：`PAUSE` が `SPEED: x.x` に変わり、時刻が進み始め、`Editor.log` に `Game Resumed. TimeScale: …`。
4. もう一度クリック → 「停止」に戻ること。
5. **もう一度**入力診断を実行し、「直近のクリック経路」と「到達回数」を控える。
   - `EventSystem（通常経路）` … 通常経路で解決
   - `直接判定` … EventSystem 側が原因（保険で動作）。`pixelDragThreshold` の行も一緒に報告してください
   - どちらも 0 … クリックがアプリへ届いていない（自動入力の制約）
6. 併せて確認：ボタンの上でクリックしても**背後の艦隊が選択解除されたり移動命令が出たりしない**こと。
7. 元仕様の回帰：`2` キーで2倍速 → 停止 → 再開で**2倍速のまま**戻ること。Space との表示同期。
   艦隊詳細／編制／Esc システムメニューを開くとボタンが消え、閉じると戻ること。

## 実機未検証点

上記 1〜7 は**すべて未検証**です。今回の修正は「両方の原因を塞ぎ、どちらだったか分かるようにする」もので、
**症状が消えたことの確認は取れていません**。合格扱いにしないでください。
とくに、診断で「どちらも 0」だった場合は**実装ではなく自動入力の制約**が原因なので、
手動クリックでの再確認をお願いします。

**編集停止**。
