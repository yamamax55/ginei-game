---
type: audit
tags: [audit]
---

# 現状再監査 — #2476（軍人立身出世 TKO-1〜13）マージ後（2026-06-18）

> [`current-state-audit-2026-06.md`](current-state-audit-2026-06.md) の続報。EPIC #2477（軍人立身出世レイヤー TKO-1〜13）が **PR #2476 で master にマージされた直後**の状態を、コードで裏取りし直した記録（前回監査の file:line は #2476 前のスナップショットで一部失効）。
> 検証＝配線コードの直接確認（推測でなく file:line）。本コンテナは dotnet/Unity とも無いため EditMode/Play は未実行（CI/手元で要確認）。

---

## 0. 総括

**#2476 で立身出世層は「Core＋執務机UI＋月次ループ」まで通った。** だが**看板機能が実際のプレイ（会戦）に反応していない**のが最大の穴：主命の成否は会戦結果でなく**乱数コイン**で、会戦の戦果は主人公の武勲に**一切流れない**。さらに、戦って得た提督の経験値は**捨てられている**（永続しない）。

→ いま必要なのは新システムでなく、**「積み上げ（会戦・教育・軍政）が立身出世と戦闘力へ実際に届く」配線**。観測だけが進み体験に届かない、という本プロジェクト既知のリスクが、最新の目玉機能でも再発している。

| 領域 | 判定 | 一言 |
|---|---|---|
| 立身出世 Core（TKO-1〜13） | ✅ | 15+クラス実装済み（`Assets/Scripts/Core/Personnel`） |
| 月次評定ループ（暦駆動） | ✅ | `ProtagonistCareerDirector` が月境界で `MonthlyCouncilRules.Hold`・定員ゲート連携 |
| 執務机UI（TKO-8・Alt+J） | ✅ | `ProtagonistDeskOverlay` で階級/武勲/主命/恩義/一代記を可視化 |
| **会戦→武勲／主命成否** | 🔴 | **未配線。主命成否は乱数・戦果は武勲に流れない** |
| **提督の成長(XP)永続** | 🔴 | **会戦XPは一時オブジェクトに入れて破棄** |
| 軍の質が戦略→戦術へ | 🟡 | 器（`ForceQualityRules`）はあるが戦略層が値を入れない |
| 立身出世状態の観測（J） | 🟡 | 執務机にはあるが `CoreStateInspector` 未登録（規約違反） |
| 初見導線 | 🔴 | tutorial/onboarding 無し（HelpOverlay は H キーのみ） |
| セーブ往復 | 🟡 | 人物/艦隊/星系/暦は復元。練度/XP は未保存 |

**前回監査から解消済み（進捗）**：セーブ永続（`GalaxyView.Persistence.cs:97` で人物/艦隊/Province/Clock を `CampaignSaveManager.SaveSession`）・執務机UI・月次ループは**もう通っている**。

---

## 1. P1（看板機能が空回り＝最優先・小さく直る）

### P1-a 会戦→武勲が未配線・主命成否がランダム
- **事実**：
  - `ProtagonistCareerDirector.cs:105` 主命の達成は `Random.value < mandateSuccessChance(0.35)` の**コイン**。コメント自身が `:104` で「将来は会戦結果で駆動」と未配線を明言。
  - `BattleManager` は `ProtagonistCareerDirector`/`MeritRecordRules` を**一切呼ばない**（grep 一致0件）。会戦は武功章メダル（`BattleManager.cs:630`）を出すだけで、主人公の武勲点（`MeritRecordRules.Record`）へ繋がっていない。
  - 平時の武勲は `juniorServiceMerit=14f` の月次自動付与（`:102`）＝**戦っても戦わなくても同じペースで昇る**。
- **なぜ最優先**：「自分の手柄で抜擢される」という #2476 の核が、実際の会戦と切れている。立身出世が**プレイの結果でなく時間経過**で進む＝目玉が体験に届かない。
- **最小手**：①`BattleManager` の決着処理で、主人公の提督（`isProtagonist` 相当）が参加した会戦の戦果（撃沈/旗艦撃破/勝利）を `MeritRecordRules.Record(ExploitKind.撃沈/旗艦撃破/防衛達成)` へ。②主命の成否を「会戦/目標の達成」に紐付け（乱数フォールバックは残してよいが、戦果があれば戦果優先）。窓口（`MeritRecordRules`/`SovereignMandateRules`）は既存＝**配線のみ**。

### P1-b 提督の成長(XP)が永続しない
- **事実**：`BattleManager.cs:624-626`「Growth は AdmiralData へ未永続（Wave1 配線待ち）。一時インスタンスで関数の疎通のみ確認」＝会戦で得た XP は `tempGrowth` に入れて**即破棄**。
- **なぜ重要**：会戦を重ねても提督が強くならない＝RPG/育成の手応えが無い。立身出世（階級）と能力成長（数値）の両輪のうち片輪が空転。
- **最小手**：`AdmiralData`（または対応する成長ストア）に `Growth` を持たせ、`GainExperience` を永続先へ適用＋セーブ往復に含める（P2-b と同根）。

---

## 2. P2（積み上げが戦闘力・継続に届かない）

### P2-a 軍の質が戦略から戦術へ流れない 〔訂正：自動解決は実装済み・残は実会戦の一貫性のみ〕
- **訂正（再調査）**：本項は前回監査（#2476 前）の失効。master には既に**艦隊練度システムが配線済み**＝`GalaxyView.Veterancy.cs`（`fleetXp` を id キーで保持・`RunVeterancyTick` 年次獲得/減衰・`FleetVeterancyFactor`）。**自動解決の戦闘係数は練度を折込済み**＝`GalaxyView.Economy.cs:1088` `CombatFactorOf = EffectiveCombatFactor(supply, tech) × FleetVeterancyFactor(f)`。よって「軍の積み上げ→戦闘力」は**自動解決では成立**している（前回監査の "効くのは補給のみ" は失効）。※私はこれを見落とし P2-a を二重実装しかけて破棄した（教訓＝着手前に #2476 の Game パーシャルまで確認する）。
- **残る穴（軽微・要設計判断）**：**実会戦（BattleHandoff）**の質だけ別式で、`GalaxyView.Input.cs:472-473/497` が `ForceQualityRules.CombatMultiplier(null, 0.5f, FirepowerFactor(supply))×tech` を使い**練度を折り込まない**＝自動解決と実会戦で歴戦艦隊の扱いが不一致。
- **最小手（提案）**：実会戦の質に既存 `FleetVeterancyFactor(a/b)` を乗算して自動解決と揃える（新規ロジック不要・既存アクセサ）。ただし「手動会戦は艦隊練度でなく操艦の腕で決める」意図なら現状維持が正＝**設計判断が要るため未着手**（オーナー確認待ち）。

### P2-b セーブ往復に練度/成長が乗らない
- **事実**：`CampaignSaveData`/`CampaignSerializer` は星系/勢力/国庫/税率/人物/艦隊/Province/Clock/朝廷を保存（`Core/Society/CampaignSaveData.cs` 各 Save 構造体）。だが `StrategicFleetSave` に **veterancy/NCO/提督Growth が無い**（P2-a・P1-b と同根＝そもそもフィールドが無い）。
- **影響**：「続きから」で艦隊の練度や提督の成長が失われる（ネームド提督・編制自体は復元される）。
- **最小手**：P1-b/P2-a でフィールドを足したら、対応する Save 構造体へ1行ずつ追加。

### P2-c 立身出世状態が CoreStateInspector(J) 未登録
- **事実**：`CoreStateInspector`（既定ルート＝Campaign/Provinces/Clock＋軍系static）に**立身出世状態の登録が無い**（`ProtagonistCareerDirector`/`RankPyramidDirector` から `Register` 呼び出し0件）。観測層規約「新 Core 状態は登録1行で覗ける」に反する。
- **緩和材料**：執務机（Alt+J）で主人公状態は見える＝プレイヤー向けには可視。J(汎用ダンプ)に出ないだけ。
- **最小手**：`CoreStateInspector.Register("立身出世", () => ProtagonistCareerDirector.Instance)` 1行（規約どおり）。

---

## 3. P3（導線・継続・演出の仕上げ）

### P3-a 初見導線ゼロ（前回 P1 のまま未着手）
- **事実**：tutorial/onboarding 無し。`HelpOverlay` は H キーのみで初回自動表示なし（`HelpOverlay.cs:106-120`）。`TitleManager` に「遊び方」導線なし。
- **最小手**：初回のみ（`PlayerPrefs` 判定）会戦/戦略マップで操作ヒント1枚＋1ループを段階開示（既存 `HelpOverlay`/`GineiUITK` 流用）。

### P3-b 新規戦役は初年度まで政府が空
- **事実**：`GalaxyView.Government.cs`（~127-136）「最初の年境界まで 要職の任命なし／省庁 未配線」＝開幕〜初年度末まで二官八省が立たない。
- **最小手**：戦役セットアップ時に初期任命/省庁シードを1回呼ぶ（年次Tickと同じ窓口を Setup でも）。

### P3-c 税レバーは設計どおり（穴ではない）／可視化のみ残
- **事実**：税率は `debugMode` 限定（`GalaxyView.Input.cs:68-77`）。コメント通り「通常プレイは AI 委任＝タイクン化回避」の**意図的設計**。残るのは現税率→国庫/民心の**効きの可視化**（E オブザーバはキー裏にある）。優先度低。

### P3-d 因果の物語
- **事実**：主人公の一代記（`ProtagonistChronicle`・Alt+D）は在る。だが稟議/建白が**どう歪み誰に拾われ結実したか**の汎用伝播トレースは未表示。開示エンジン（`DisclosureLedger`）は依然未配線。P1/P2 より大きめ＝後段。

---

## 4. 推奨：次の一手

**P1-a（会戦→武勲・主命成否）から。** 理由：
1. #2476 の**目玉を「動いて見える」状態にする**最短手＝既存窓口（`MeritRecordRules`/`SovereignMandateRules`）への配線のみ、新ロジック不要。
2. 「立身出世が時間でなくプレイで進む」へ反転＝体験の手応えに直結。
3. P1-b（成長永続）・P2-a/b（質→戦闘・セーブ）は `Growth`/veterancy フィールド追加で連鎖的に解ける（同根）ので、P1-a の次にまとめて。

> 要約：**Core も観測も執務机も通った。穴は「会戦の結果が立身出世と戦闘力へ届かない」配線。** 乱数で進む出世を、戦って勝ち取る出世へ——既存窓口への配線で目玉を生かすのが、いま必要なこと。
