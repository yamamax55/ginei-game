# 陣形専用の保持・解除と命令優先順位 — 実装結果

Task: `fleet-formation-hold-20260910`
Issue: https://github.com/yamamax55/ginei-game/issues/2253（確定仕様1）
作業票＝[fleet-formation-hold-20260910.md](fleet-formation-hold-20260910.md)。
**編集停止済み**。commit / push なし。既存セーブ・v5・無関係な差分は保護。
仕様2（費用一覧UI）・仕様3（整列度／補正）・QAメニュー整理は**含めていません**。

---

## 設計：陣形の保持を「移動・攻撃と独立した状態」として持たせる

これまで陣形は `Squadron.currentFormation` という**現在値だけ**で、
「誰が指定したか」「保持しているか」が無く、AI が自由に上書きしていました。
そこへ**保持状態**を足し、指定の受理／拒否／解除をひとつの窓口に集約しました。

### Core（新規）`Assets/Scripts/Core/Combat/FleetFormationOrderRules.cs`

```csharp
public enum FormationOrderSource { なし, 艦隊AI, 軍団AI, 支援要請, 直接命令 }
public enum FormationOrderResult { 受理, 保持により拒否, 資格不足, ポイント不足, 撤退中, 対象なし }

public readonly struct FleetFormationHold
{
    public readonly bool held;                    // 手動保持中か
    public readonly Formation formation;          // 指定陣形
    public readonly FormationOrderSource source;  // 指定元
    public readonly string corpsKey;              // 指定を受けたときの指揮系統
}
```

判断は全部この純ロジックに置きました。

| API | 役割 |
|---|---|
| `CanAccept(hold, incoming)` | 保持中は**明示指定だけ**が上書きできる |
| `Decide(hold, incoming, sameFormation, hasPoints, qualified, retreating)` | 受理か、断る理由か |
| `Apply(hold, incoming, formation, corpsKey)` | 受理後の保持（**AI の指定は保持を作らない**） |
| `ShouldReleaseForEmergency(routed, corpsRetreat)` | 敗走・総退却で解く（出どころ不問） |
| `ShouldReleaseOnCorpsChange(hold, currentCorpsKey)` | 所属変更で旧系統の保持を解く |
| `ShouldRestore(hold, current)` | ずれた陣形の自動復帰 |
| `HoldText` / `ResultText` / `ReleaseText` | 表示（保持状態・指定元・拒否/解除理由） |

**優先順位**は仕様どおり
`敗走/総退却による保持解除 > 明示指定（直接命令・承諾済み要請）> 軍団AI > 艦隊AI`。
明示指定どうしは**最後に受理したもの**が勝ちます（直接と要請に上下を付けない）。

### Game：`Squadron` を唯一の窓口にした

```csharp
public FormationOrderResult RequestFormation(Formation f, FormationOrderSource source)
public bool TryChangeFormation(Formation f, FormationOrderSource source)   // 上の bool 版
public FleetFormationHold FormationHold { get; }
public bool IsFormationHeld { get; }
public bool ReleaseFormationHold(string reason)
```

`Squadron` は盤面の事情（スキルポイント・資格・撤退中・軍団キー）を集めて Core に渡し、
結果を適用するだけです。**断ったときは陣形も保持もスキルポイントも一切触りません**（消費なし・自動再試行なし）。

`TryChangeFormation` から**引数なしの版を無くしました**。5つの呼び出し元すべてに出どころを書かせる形にして、
「うっかり AI の指定が保持を作る」事故を型で防いでいます。

| 呼び出し元 | 渡す出どころ |
|---|---|
| `FleetCommander.ChangeFormation` | `直接命令` |
| `FleetCommander.ChangeFormationForCorps`（新規） | `直接命令` |
| `SupportRequestDirector.Execute`（陣形変更） | `支援要請` |
| `BattlefieldCommandManager`（軍団長・隷下の2か所） | `軍団AI` |
| `FleetAI.UpdateFormationDoctrine` | `艦隊AI` |

### 保持の面倒は `Squadron.Update` が見る

```csharp
MaintainFormationHold();   // ① 所属が変わっていたら解く ② 陣形がずれていたら無料で戻す
```

**自動復帰は陣形だけ**で、移動にも攻撃にも触りません（仕様の「自動復帰は移動攻撃を中断しない」）。
復帰に費用はかかりません（「保持継続費用なし」）。

---

## 仕様の各項目と対応

| 仕様 | 対応 |
|---|---|
| 指定陣形・指定元・手動保持/自動を艦隊単位で保持 | `Squadron.FormationHold`（`FleetFormationHold`） |
| 移動/攻撃と独立 | 保持は `FleetMovement` / `FleetWeapon` を一切参照しない。移動終了・停止・倍速で解けない |
| 通常AI・軍団AIが上書きしない | `CanAccept` が保持中は明示指定だけ通す。5経路すべてが窓口を通る |
| 明示指定の自動復帰が移動攻撃を中断しない | `MaintainFormationHold` は `currentFormation` だけを戻す |
| ポイント不足/資格不足/撤退中は拒否・原状維持・消費なし・自動再試行なし | `Decide` が理由を返し、`RequestFormation` は受理時しか状態を変えない。再試行の仕組みは持たない |
| 同一陣形は無料で保持設定可能 | `Decide` は `sameFormation` ならポイントを見ない |
| 要請承諾前は変更しない | 実行は `SupportRequestDirector` の承諾後だけ（既存の往復のまま） |
| 軍団所属変更で旧指揮系統の保持を解除 | `ShouldReleaseOnCorpsChange`（軍団キーで判定） |
| 会戦終了で解除 | 保持は `Squadron` の非直列化フィールド＝シーン破棄で消える |
| 保存復元 | **対象外**。会戦の途中保存は未対応（`currentFormation` を保存するデータが無いことを確認） |
| 軍団隊形と艦隊陣形の分離 | 軍団隊形の変更／解除は `CorpsFormation` のままで、艦隊の保持に触れない。軍団AI の陣形発令は保持に弾かれる |
| 別操作「配下艦隊の陣形を一括指定」 | `FleetCommander.ChangeFormationForCorps` ＋ メニュー「軍団 ▸ 配下艦隊の陣形を一括指定 ▸」 |
| 権限と対象を守る | 一括指定は `RightFor(sel) == 直接命令` の艦隊だけ（#67 の判定を使用） |
| 手動保持中は自動包囲を抑止 | `Squadron.UpdateEncircleTarget` が保持中は `encircleTarget = null` |
| 包囲中に指定を受理したら解除して形成へ | `RequestFormation` の受理時に `encircleTarget = null` |
| 艦隊全体の側面移動は可能 | 抑止したのは配下艦の群がりだけ。移動経路には触れていない |
| 指定元・保持状態・理由の表示 | HUD の陣形行が `HoldText`（例「円陣（保持・直接命令）」「紡錘陣（自律）」）。拒否・解除は通知へ |
| **総退却は陣形保持だけ解除し、直接移動/攻撃は中断しない** | `ApplyCorpsRetreat` で `ReleaseFormationHold` を**`continue` より前**に置き、直接命令の艦でも陣形だけ解く。移動/攻撃の扱いは従来のまま |

### 解除の操作

- 艦隊：「陣形 ▸ **陣形の保持を解除**」（#67 の関門を通した対象だけ）
- 軍団：「軍団 ▸ 軍団指定を解除」は**従来どおり軍団隊形だけ**（艦隊の保持は変わりません）

---

## テスト

| 種別 | 内容 |
|---|---|
| EditMode **20件**（新規 `FleetFormationOrderRulesTests`） | 優先順位／保持を AI が上書きしない（艦隊AI・軍団AI とも）／要請の指定も保護される／明示どうしは最後が勝つ／**AI の指定は保持を作らない**／拒否3種で原状維持／同一陣形は無料／撤退中は同一陣形でも拒否／緊急解除／所属変更で解除／自動復帰／表示（「変更していません」と分かる文面）／null 安全 |
| PlayMode **9件**（新規 `FleetFormationHoldPlayModeTests`） | **AI 周期を10回越えても保持される**／**移動完了を越えても解けない**／ずれたら復帰し**移動先を変えない**／ポイント不足の拒否で**陣形・保持・ポイントが不変**／同一陣形は無料で保持が付く／**敗走で保持は解けるが直接命令の移動は続く**／所属変更で解除／要請の指定も保持になる／解除すると AI に戻る |

TestHarness（EditMode 相当）は **9,672件・0失敗**（前回 9,652 ＋20）。

### ★PlayMode 9件は未実行

Unity が要ります。**ChatGPT 側で実行してください**：
`Window > General > Test Runner` → PlayMode タブ → `FleetFormationHoldPlayModeTests`
（または `-runTests -testPlatform PlayMode`）。終了時は Play OFF に戻してください。
コンパイルは通っていますが、実行結果は未取得です。

---

## 検証結果

| 項目 | 結果 |
|---|---|
| 6アセンブリ コンパイル | 合格（Core / Data / Game / Editor / Tests.EditMode / Tests.PlayMode すべて `error CS` 0件） |
| TestHarness 全件 | **9,672件合格・0失敗** |
| PlayMode 9件 | **未実行**（Unity 必要） |
| 直列化トラップ確認 | `FleetUnit.prefab` の `currentFormation: 0`＝紡錘陣は**スクリプト既定と同値**。既定を変えていないので prefab の修正は不要 |
| 既存セーブ | 未変更（`campaign_save.json` Sep 9 13:22 のまま） |
| commit / push | 実施していない（`git log` は `5eb970f2` のまま） |

### 変更ファイル

| ファイル | 種別 | 内容 |
|---|---|---|
| `Assets/Scripts/Core/Combat/FleetFormationOrderRules.cs` | **新規** | 保持状態・優先順位・受理/拒否・解除・表示 |
| `Assets/Tests/EditMode/FleetFormationOrderRulesTests.cs` | **新規** | 20件 |
| `Assets/Tests/PlayMode/FleetFormationHoldPlayModeTests.cs` | **新規** | 9件（未実行） |
| `Assets/Scripts/Game/Squadron.cs` | 変更 | `RequestFormation` 窓口・保持の保有と維持・包囲抑止 |
| `Assets/Scripts/Game/FleetCommander.cs` | 変更 | 直接命令を窓口へ・拒否理由の通知・`ChangeFormationForCorps` 新設 |
| `Assets/Scripts/Game/SupportRequestDirector.cs` | 変更 | 承諾した陣形要請を `支援要請` として受理・拒否理由の通知 |
| `Assets/Scripts/Game/FleetAI.cs` | 変更 | 出どころ `艦隊AI`・敗走で保持を解除 |
| `Assets/Scripts/Game/BattlefieldCommandManager.cs` | 変更 | 出どころ `軍団AI`・総退却で保持だけ解除 |
| `Assets/Scripts/Game/CommandMenu.cs` | 変更 | 「陣形の保持を解除」「配下艦隊の陣形を一括指定 ▸」 |
| `Assets/Scripts/Game/FleetHUDManager.cs` | 変更 | 陣形行に保持状態・指定元 |

---

## 実機手順（ChatGPT へ）

1. `Ginei/QA: 指揮権限 検証会戦を開始（戦役・Play中）` → Space で再開
2. `QA自軍1` を選択 →「陣形 ▸ 円陣」→ HUD が **「現在陣形: 円陣（保持・直接命令）」** になること
3. しばらく戦わせる → **AI が陣形を変えないこと**（HUD が円陣のまま・保持表示が消えない）
4. その艦隊へ移動命令 → 到着後も **保持が残っている**こと（移動と独立）
5. 「陣形 ▸ 車懸かり」を非軍神へ指示 → **拒否の通知**が出て**陣形が変わらない**こと
6. 「軍団 ▸ 配下艦隊の陣形を一括指定 ▸ 方陣」→ 自軍団の各艦隊が方陣になり、
   **軍団隊形（並べ方）は変わらない**こと
7. 「軍団 ▸ 軍団指定を解除」→ **艦隊の陣形保持は解けない**こと
8. 「陣形 ▸ 陣形の保持を解除」→ 保持表示が「（自律）」に戻り、以後 AI が陣形を変えること
9. `Ginei/QA: 緊急中断 支援移動中に敗走させる` →
   **陣形の保持が解ける**通知が出て、**直接命令の移動は続く**こと
10. `Ginei/QA: 緊急中断 他軍団を総退却の兵力まで減らす` →
    総退却で**陣形の保持だけ**解け、直接命令の艦の移動命令は維持されること
11. 他軍団へ陣形変更を要請 → 承諾後、その艦隊の HUD が **「（保持・支援要請）」** になり
    軍団AI に上書きされないこと

---

## 実機未検証点・残件

1. **上記 1〜11 はすべて未検証**。PlayMode 9件も未実行です。合格扱いにしないでください。
2. **会戦の途中保存は未対応**のため、保持の保存復元は実装していません
   （保存する仕組みができた時点で `FleetFormationHold` を足す想定）。
3. 仕様2（費用一覧UI）・仕様3（整列度・戦闘補正）は別スコープのまま。
4. QAメニュー整理（`qa-menu-categories-20260910.md`）は後続。
5. 既知の別件（入力の取りこぼし、`DamagePopup` 未解放警告）は今回も触っていません。

**編集停止**。
