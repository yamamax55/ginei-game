# 現状監査 — 縦スライス1ループの実機検証（2026-06-18）

> **追記（2026-06-18 後）**：本監査の取得後に PR #2476（EPIC #2477＝軍人立志伝レイヤー TKO-1〜13）が master へマージされた。本書の file:line は取得時点のスナップショットであり、`Assets/Scripts/Core/Personnel` 周辺の行番号は #2476 後にずれている可能性がある。一方で本書の主眼（§2 のギャップ＝**初見導線／セーブ永続／会戦の質／因果の物語**）は立身出世層とは独立しており、依然として有効な「次の一手」候補である。
>
> 「いま必要なこと」を決めるための現状監査。新システムを足す前に、**すでにある1ループが実機でどこまで気持ちよく通るか／どこが壊れ・物足りないか**をコードで裏取りした記録。
> 検証＝配線コードの直接確認（推測でなく file:line で裏取り）。回帰テストは CI の TestHarness ループ（毎時）が担保（本コンテナに dotnet 無し）。
> 上位の方針は [`game-critique.md`](./game-critique.md) / [`game-improvements.md`](./game-improvements.md) / [`vertical-slice-roadmap.md`](./vertical-slice-roadmap.md)。

---

## 0. 総括

**骨格の1ループは、ロードマップ（06-10版）が「残作業」と書いた S5/S6 を含めて、ほぼ閉じている。**
戦略移動 → 接敵で会戦 → 占領で所有移転 → 占領惑星の Province が税/資源を産む → 国庫へ → 高税が民心を蝕む → 稟議が決裁デスクに上がり選択が世界へ反映、までコードで連結を確認した。

→ つまり**いま必要なのは「新しい回路」ではなく、通っている回路を「遊べる・続けられる・分かる」体験に仕上げること**。`game-improvements.md` の主張（広げるより一本を磨く／観測を操作へ／因果を物語る）と実機の状態が一致した。

| スライス要素 | 判定 | 要点 |
|---|---|---|
| 税→国庫→支持(S5) | △ | 経路は通る。ただし**通常プレイの税レバーUIが無い**（デバッグ専用キー `[`/`]`） |
| イベント提示(S6) | ✅ | `StrategyEventPanel`/`DecisionDeck`/`RingiDirector` で稟議→決裁→世界反映が一周 |
| 占領→内政→税 | ✅ | 占領で owner フリップ→Province 産出が新所有勢力へ→`TickEconomyDay` で国庫加算 |
| 会戦の「質」 | △ | `ForceQualityRules`→`ComputeDamage` 配線済。ただし**効くのは補給readinessのみ**（下士官団/新兵練度は会戦ユニットに未紐付け） |

---

## 1. 確認できた事実（WIRED・file:line）

- **税→国庫→民心**：`CampaignRules.TickEconomyDay`（Core/Society/CampaignRules.cs:33-49）が毎日 `taxRate`→`treasury` 加算＋`TaxBurdenPenalty`→`community.hope` 減衰。駆動は `GalaxyView.cs:494`（CalendarDispatcher 日次）。
- **イベント/稟議の一周**：`RingiDirector.cs:51-149` が建白→官僚伝播→`DecisionDeck.Enqueue`→裁可→`RingiPipeline.ExecuteAndApply` で effectKey（`tax.*` 等）を `FactionState` へ適用（官僚 friction で骨抜き込み）。会戦中は `BattleEventManager.cs:53-77`。
- **占領の連鎖**：`PlanetSiegeRules.cs:129` で `planet.owner=attacker`、`GalaxyView.Persistence.cs:20-21` で StarSystem 同期、`GalaxyView.Economy.cs:1185-1295` で owned Province の産出、`CampaignRules.cs:22-27` の `EconomyBase`（人口×安定度）で課税ベース。
- **会戦の質（補給）**：`ForceQualityRules.cs`→`ShipCombat.cs:202-238`（`fQual` 乗算）。供給は `GalaxyView.Input.cs:472-473`（補給→readiness→質倍率）→`BattleSetup.cs:418-419` で旗艦へ。

---

## 2. 優先度つきギャップ（ROI 順＝「完成・遊びやすさ」への効き）

### P1 初見導線がゼロ（学習曲線の崖）
- **事実**：`tutorial`/`onboard` 系スクリプト無し。`HelpOverlay` の初回自動表示も `TitleManager` の「遊び方」導線も無し。
- **なぜ最優先**：1ループは通っているのに**新規プレイヤーがそこへ辿り着けない**。`game-critique.md` の「崖のような学習曲線」そのもの。最初の30分を掴めなければ深さに到達される前に去られる。
- **最小手**：会戦の初回に操作ヒント1枚／戦略マップ初回に「移動→接敵→占領→税→稟議」を1文ずつ段階開示（`PlayerPrefs` で初回判定・既存 `HelpOverlay`/`GineiUITK` を流用）。

### P1 継続プレイで「将」と編制が消える（セーブ未永続）
- **事実**（`campaign-save-roundtrip-gaps.md`）：人物ロスター（提督/文官）・`FleetPool`/`FleetRoster`/`OrderOfBattle`・`Province` 内政は **`CampaignSaveData` 非保持**。`GalaxyView` は campaign セーブ自体を呼んでいない。DTO/シリアライザ（`PersonSave` 等）は実装済みだが**保存トリガが未配線**。
- **なぜ重大**：銀英伝風でロード時にネームド提督・艦隊編制が消えるのは致命的。「続きから」が成立しない＝完成の前提を欠く。
- **最小手**：`GalaxyView` の保存点で `CampaignSaveManager.Save(campaign, commanders+civilians)`、ロードで `LoadPeople()`→`commanders` 復元を配線（純ロジック側は既にある）。

### P2 経済レバーが通常プレイで触れない
- **事実**：税率変更は `debugMode` 限定（`GalaxyView.Input.cs:74-75`）。通常プレイの税操作は稟議（決裁デスク）経由のみ＝「提案して祈る」型。
- **判断点**：これは設計思想（君は主人公でない＝直接レバーを握らせない）と表裏。**意図どおりなら税の直接UIは不要**。ただし `vertical-slice-roadmap.md` S5 の受け入れ基準「1レバー＋1帰結が画面で見える」を満たすには、せめて**現税率と国庫/民心への効きを観測UIで可視化**したい（操作でなく可視化が穴）。

### P2 会戦の「質」が補給しか効かない
- **事実**：`ForceQualityRules` は合成済だが、`StrategicFleet` に下士官団(`NcoCorps`)/新兵練度フィールドが無く、`null`/中立値で合成（`combat-quality-audit.md` の通り現状コードでも確認）。
- **影響**：「軍政・教育・財政を積んでも会戦の強さが変わらない」＝4X の積み上げが戦術に届かない。深さの目玉が空回り。
- **最小手**：`StrategicFleet`（または編制側）に練度/下士官スカラを1つ持たせ、降下時に `ForceQualityRules.CombatMultiplier` の引数へ流す（合成窓口は既存・追加は属性1つ）。

### P3 因果の「事後の物語」が無い
- **事実**：稟議は決裁デスクに出るが、「自分の一手がどこで歪み・誰に拾われ・どう結実したか」の伝播トレースは未表示（`NotificationCenter` は現在状態の通知のみ）。
- **なぜ価値**：`game-improvements.md` の核＝「主人公でない」を無力感→カタルシスへ反転させる装置。1ループが通った今こそ刺せる。ただし P1/P2 より大きめ。

### P3 表面の一点豪華（目安箱/決裁デスク画面）
- ゲームの顔である決裁デスク/稟議画面を UITK で磨く（`game-improvements.md` の一点豪華主義）。1ループ確定後の仕上げ枠。

---

## 3. 推奨：次の一手

**P1 を2本（初見導線＋セーブ永続）から着手する。** 理由：
1. どちらも**新規ロジック不要＝既存資産の配線/可視化**（投資対効果が最大）。
2. 1ループは通っているのに「辿り着けない／続けられない」という、**完成を直接妨げる2つの穴**を塞ぐ。
3. `game-improvements.md` の方針（広げず・配線して見せる）に完全合致。

P2/P3 はその後、§2 の順で増分。新EPIC・新Coreドメインの追加はいったん止める（`game-critique.md` の最大リスク＝広げすぎて完成しない、への規律）。

---

> 要約：**回路はもう通っている。穴は「入口（導線）」と「継続（セーブ）」と「手応えの可視化」。** 新システムでなく、これらの配線・可視化・演出を埋めるのが、いま必要なこと。
