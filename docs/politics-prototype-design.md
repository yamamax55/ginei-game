# 政治プロト設計：階級と提案（EPIC #14）

> PC-98 版『銀河英雄伝説IV EX』の「階級と作戦提案」を再現する。
> プレイヤー＝一提督として**作戦を上申**し、**階級に見合う提案だけが通る**（可決可能=青／不相応=赤）。
> **戦果で merit↑→昇進で提案範囲が拡大**する。
> 前提：実装済みの **`RankSystem`**（tier ベースの同列/比較/昇進）＋ **`FactionData.ranks`**（tier＋階級名）。
> 設計原則：既存の戦闘エンジンに手を入れず、**既存フロー（Title→Battle→Result）だけで成立する最小プロト**を先に作る。

## 0. 確認したい論点（推奨デフォルトで本書は記述）
1. **最小プロトの舞台**：推奨＝**会戦前の「作戦会議」画面**（タイトル/会戦選択の延長）。提案＝「どの作戦を実行するか」。…ここから始めてよい？（後で Phase C 戦略の行動へ写像）
2. **提案が通る条件**：推奨＝**自階級 tier ≥ 提案の必要 tier なら青（可決可能）/未満なら赤（不相応）**。上官の気まぐれ等は将来拡張。…この単純規則でよい？
3. **昇進の駆動**：推奨＝**戦果（勝敗＋勝利条件達成＋MVP）で merit 加算 → 閾値で `RankSystem.NextRankTier` 昇進**。降格は当面なし。…これでよい？
4. **対象勢力の階級観**：推奨＝**プレイヤー勢力の `FactionData.ranks` をそのまま使う**（帝国=上級大将あり等。共産は党称号でも tier で同列）。…OK？

> 以降は上記「推奨」で記述。違えば指摘で差し替える。

## 1. データモデル
### 1-1. プレイヤーの地位（`GameSettings` に保持）
- `playerRankTier`(int)：現在の階級 tier（`FactionData.ranks` の tier。例 少将6→…→元帥10）。
- `playerMerit`(int)：功績。戦果で増える。
- 既存 `playerFactionData`／`selectedAdmiral` と合わせ、`FactionData.GetRankName(tier)` で「少将」等の表示名を解決。
- セーブ拡張（#19）で永続化。

### 1-2. 提案定義 `ProposalData`（ScriptableObject）
- `proposalName`：作戦名（例：「正面決戦」「側面迂回」「持久防衛」「別働隊派遣」「全軍出撃」）。
- `requiredTier`(int)：上申に必要な階級 tier（提案のスケール。小規模=低 tier、方面〜全軍=高 tier）。
- `description`／効果パラメータ：最小プロトでは**どのシナリオ/兵力配分/勝利条件で会戦を始めるか**に写像（既存 `ScenarioData`／`BattleSetup` の選択）。
- 一覧は `Resources` 配下（`ScenarioData` と同じ解決）＋エディタ生成メニュー。

## 2. 提案システム（青＝可決可能／赤＝不相応）
- **判定**：`canPropose = RankSystem.Compare(playerRankTier, proposal.requiredTier) >= 0`（自階級が必要 tier 以上）。
  - 青（可決可能）：`canPropose == true`。実行できる（最小プロトでは選んで会戦開始）。
  - 赤（不相応）：`false`。選べない／選ぶと「貴官の階級では上申できない」と却下表示。
- **表示**：作戦会議UIで各提案を**必要階級つきで一覧**し、青/赤で色分け（`requiredTier` を `FactionData.GetRankName` で名前表示）。昇進で赤→青に解禁されていくのが体感。
- **集約**：階級の同列/比較は必ず `RankSystem` 経由（勢力をまたいでも tier で比較）。欠番階級は `RankSystem.ResolveTier` で丸める（共産の党称号等）。

## 3. 昇進と範囲拡大（戦果→merit→昇進）
- **merit 加算**（会戦決着時、`BattleManager`／`ResultManager` の戦績から）：
  - 勝利＝大、敗北＝小/0、勝利条件達成（時間防衛/旗艦撃破/護衛）＝加点、MVP（`GameSettings.mvpAdmiral` が自分）＝加点。
- **昇進**：`playerMerit` が階級ごとの閾値を超えたら `playerRankTier = RankSystem.NextRankTier(playerFactionData, playerRankTier)`。最高位（`HighestTier`）で打ち止め。
- **効果**：上位 tier の提案（より大規模な作戦）が青に解禁＝**提案範囲の拡大**。少将→中将→大将→（上級大将）→元帥、と作戦規模が上がる IV EX の体験。
- 演出：昇進時に通知（結果画面 or 次の作戦会議で「昇進した」表示）。

## 4. 承認ロジック（最小→拡張）
- **最小**：青なら可決（実行）、赤なら却下。上官は出てこない。
- **拡張（将来）**：上官（より高位）への上申を介し、rank 差・merit・勢力スタンスで可決確率を出す。共産の強硬/穏健（#17）や政治イベントと連動。分離独立・クーデター等の政治フックもここに乗る。

## 5. 最小プロト（既存フローだけで成立）→ Phase C 接続
- **作戦会議画面**（`TitleManager` のシナリオ選択を拡張 or 新シーン/モーダル）：
  1. 自分の階級・merit を表示（`FactionData.GetRankName(playerRankTier)`）。
  2. 提案（=作戦）一覧を青/赤で提示。青を選ぶと、その作戦の `ScenarioData` 設定で `BattleSetup`→会戦へ。
  3. 会戦結果→ merit 加算→（閾値超で）昇進→次の会戦では上位作戦が解禁。
- これで **Phase C 戦略が無くても「昇進して作戦範囲が広がる」политикプロトが回る**＝テスト可能。
- **Phase C 接続**：将来、提案は戦略レイヤーの行動（回廊への攻勢/防衛/増援要請/編制変更）に写像。`requiredTier` が「単艦移動<方面指揮<全軍指揮」のスコープに対応（`docs/phase-c-core-design.md` の戦略指示と接続）。

## 6. 実装フック（最小・既存非破壊）
| 追加/接続 | 内容 |
|---|---|
| `GameSettings` | `playerRankTier`/`playerMerit` 追加（既定＝初期階級・0）。`ResetStats` 等と整合。 |
| `ProposalData`(SO)＋生成メニュー | 作戦提案の定義（`requiredTier`＋会戦設定への写像）。 |
| `PoliticsManager`（静的 or 1個） | merit 加算・昇進判定（`RankSystem.NextRankTier`）・提案の青赤判定の唯一の窓口。 |
| 作戦会議UI | 提案一覧（青/赤・必要階級表示）。`TitleManager` 拡張 or 専用モーダル（実行時生成、既存UI流儀）。 |
| `BattleManager`/`ResultManager` | 決着時に戦果→`PoliticsManager.AddMerit(...)`。昇進通知を結果画面に表示。 |
| `RankSystem`/`FactionData.ranks` | 階級の表示・比較・昇進の土台（実装済み）。新規の階級ロジックを足さない。 |
| `SaveData`(#19) | 階級・merit の永続化。 |

> ※ 階級の同列/比較/昇進は **`RankSystem` が唯一の窓口**（CLAUDE.md の規約）。政治ロジックは `PoliticsManager` に集約し、各所に散らさない。

## 7. 段階手順・リスク・未決
1. **データ**：`GameSettings` に rank/merit、`ProposalData`＋サンプル提案（必要 tier 別に数本）。
2. **判定**：`PoliticsManager` の青赤判定（`RankSystem.Compare`）。
3. **UI**：作戦会議画面で青/赤一覧→青を選んで会戦開始。
4. **昇進**：戦果→merit→`NextRankTier`→上位提案解禁。結果画面に昇進通知。
5. **（将来）**：上官承認・政治イベント・Phase C 戦略行動への写像・共産内部対立(#17)連動。

- **リスク**：提案＝会戦設定の写像が `ScenarioData` と二重管理にならないよう、提案は「どの `ScenarioData` を、どの兵力/勝利条件で」を**参照**する薄い層にする。
- **リスク**：階級ロジックを `PoliticsManager`/`RankSystem` 以外に書かない（散逸防止）。
- **【要・作者判断】**：merit の加点配分・昇進閾値・初期階級（少将6 想定）・提案ラインナップ（作戦名と必要階級）・元帥到達後の終局条件。

---

### 変更履歴
- v0.1：階級と提案の政治プロト設計。青赤の提案判定＋戦果→merit→昇進を、`RankSystem`/`FactionData.ranks` の上に最小フックで提示。既存フローだけで回る最小プロト→Phase C 接続。**【要・作者判断】箇所は確定待ち。**
