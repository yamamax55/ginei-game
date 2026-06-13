# 社内ワークフロー風 戦略UI（稟議・決裁・回覧）設計メモ

> 戦略レイヤーの意思決定操作を「企業の社内ワークフローシステム（稟議＝起案→合議/回覧→決裁→執行→通知）」のメタファで設計する。
> 銀英伝の**官僚機構**（帝国の省庁／同盟の議会・軍官僚）をそのまま操作系にする狙い。
> **方針：UI Toolkit ネイティブで作る（埋め込みブラウザは最後の手段）。純ロジックは既存システムへ接続し、並行システムを新設しない。**
> 状態：設計メモ（実装未着手）。数値・創作裁定は【要・作者判断】。

---

## 1. なぜこの設計か（導入可否の結論）

「ゲーム内でブラウザを開いて戦略を決める」という発想の本質は **"画面UIではなく業務システムを操作している感"** にある。これは：

- **見た目**：UI Toolkit（UXML=HTML / USS=CSS / flexbox）で十分作れる。`GineiUITK`＋テーマUSSの土台が既にある＝**ブラウザ埋め込み不要**。
- **中身**：本作は `CampaignSerializer` で世界状態を JSON 化できる（FND-2 #495）。"状態を出す→操作を受ける"契約が既にあり、将来の Web 化（localhost コンパニオン）や LLM 連携（#911）にも同じ土台で乗れる。

| 方式 | 推奨度 | 理由 |
|---|---|---|
| **A. UI Toolkit ネイティブ** | ★第一推奨 | 軽い・追加依存ゼロ・JPフォント対応済・既存移行方針に一致 |
| B. ReactUnity（React→UITK） | 条件付き | React DX が要るなら。JSランタイム＋保守面のコスト |
| C. 埋め込みブラウザ（Vuplex/UnityWebBrowser=CEF） | 最後の手段 | 本物のブラウザ体験が必須のときだけ。ビルド肥大・別プロセス・入力連携難 |
| D. localhost コンパニオン（実ブラウザで `localhost`） | 検証用 | "第2モニタで決裁"感を最も忠実に。Web独立開発可。本格Web化の前段に最適 |

→ **8〜9割は A で目的を達成できる。** 本メモは A を前提に、稟議モデルを既存純ロジックへ接続する。

---

## 2. 稟議モデル（業務フロー → ゲーム）

```
起案(Draft) → 合議/回覧(Concurrence) → 決裁(Approval) → 執行(Execution) → 通知(Notification)
   │              │                        │                │                 │
 誰が出せる?     どの省庁/役職が          必要な階級/役職が   盤面状態を         NotificationCenter
 (提案権限)      関与・抵抗するか         署名するか          動かす(効果)       へ Push
```

| 業務用語 | ゲームでの意味 | 接続する既存システム |
|---|---|---|
| **起案（稟議書の作成）** | プレイヤー or AI大臣が「やりたい施策」を1件提出 | 提案権限 `OfficeRules.CanPropose`（所掌×スコープ・#142/#144） |
| **合議・回覧** | 関係省庁の同意/抵抗・縦割り | `MinistryRules.SectionalismFriction`（省益＝抵抗 #158）・文民統制 `CivilianControlRules`（#145） |
| **決裁** | 必要な役職・階級を持つ者の承認 | `OfficeRules.CanHold`/`requiredTier`・`GovernmentRegistry.GetHolder`・`RankSystem`（#14） |
| **執行（効果適用）** | 税率/支持/外交/建艦などが動く | `CampaignState`/`FactionState`（S5/S6 で稼働中）・`DiplomacyRules`・`ShipyardRules` |
| **通知・回覧結果** | 「○○案、決裁されました」 | `NotificationCenter`/`NotificationFeed`（既存・左下トースト #964） |

### イベントエンジン（#116）との関係＝補完
- `EventEngine` は **システム発・単発モーダル**（"民衆の不満"＝向こうから降ってくる）。S5/S6 で稼働中。
- 稟議ワークフローは **プレイヤー発・多段承認**（こちらから施策を上げる）。
- **両者は `EventContext` と「効果＝デリゲート」を共有**できる。稟議の"執行"は `EventChoice.effect` と同じ形で `CampaignState` を動かす＝**効果適用ロジックを重複させない**。

---

## 3. 政体による差別化（ここが面白さの核）

同じ施策でも**政体で稟議の段数・速度・歪み方が変わる**＝銀英伝の専制 vs 民主の対比をUIで体感させる。

| 政体（`inclusiveness`/`CivilianControlType`/`Regime`） | 稟議の挙動 |
|---|---|
| **帝国＝専制（収奪寄り・君主統帥）** | 決裁が速い・合議が少数・**腐敗(`Regime.corruption`)で執行が歪む/中抜き**。元首決裁で一発 |
| **同盟＝民主（包摂寄り・文民統制）** | 合議段数が多い・**議会/政党の承認が要る**（`PartyRules`/`LeadershipElectionRules` #159/#165）・遅いが正統性が高い |
| **軍部優位/未分化** | 軍人が政治案件を起案可（`CivilianControlRules.MilitaryMayHoldPoliticalOffice`）＝クーデター素地（`CoupRisk`） |

→ 「速いが脆い専制」対「遅いが正統な民主」を、**ワークフローの段数という手触り**で表現できる。

---

## 4. データモデル案（Core・test-first・並行システム禁止）

新規は最小限。**権限・所掌・効果は既存に委譲**し、稟議は"状態機械＋必要承認者の算出"だけを持つ。

### 4-1. `Proposal`（Core・`[Serializable]` 純データ）
```
id / title / body
domain      : OfficeDomain（軍事/内政/外交/財政/元首）  ← Office と同じ語彙
scope       : OfficeScope（国家/方面/星系）             ← Office と同じ語彙
faction     : 起案勢力
drafterId   : 起案者（ICharacter.id）
status      : ProposalStatus{起案,合議中,決裁待ち,承認,却下,執行済,失効}
concurrences: List<(ministryId, agreed)>               ← 回覧の足跡
effectKey   : string（執行で呼ぶ効果の識別子）          ← デリゲート表で解決（直列化可）
cost        : 財政コスト（FiscalState/treasury へ）
```
> 効果は直接デリゲートを持たず **`effectKey`＋効果レジストリ**で解決（セーブ可能にするため。`EventChoice` のデリゲートはランタイム専用なので稟議では key 駆動にする）。

### 4-2. `WorkflowRules`（static・純ロジック・唯一の窓口）
- `CanDraft(drafter, domain, scope, faction)` … `OfficeRules.CanPropose` ＋ `CivilianControlRules` に委譲
- `RequiredApprovers(proposal, government)` … domain×scope→決裁に要る役職/階級（`OfficeRules`/`requiredTier`）
- `ConcurringMinistries(proposal, ministries)` … 回覧先＝関係省庁（`MinistryRules`）
- `Friction(proposal, ministries)` … `MinistryRules.SectionalismFriction`＝抵抗で遅延/コスト増
- `Advance(proposal, ...)` … 状態遷移（起案→合議中→決裁待ち→承認/却下）
- `Resolve(proposal)` … 承認なら `status=執行済`、却下なら終了（**効果適用は呼び出し側が effectKey で**）
> 政体差（段数・速度）は `inclusiveness`/`CivilianControlType`/`Regime` を引数で受けて分岐＝**`FactionState` を read-only**（実効値パターン）。

### 4-3. 効果レジストリ（Game or Data）
`effectKey → Action<CampaignState, Proposal>` の表。S5/S6 の効果（税率↓・建艦キュー投入・条約締結 等）をここへ登録。`EventEngine` の効果と同じ実体を共有。

---

## 5. UI（UI Toolkit・`GineiUITK` パイロット）

`OrderOfBattlePanel`（UITK パイロット）と同じ型で実装。`StrategyEventPanel`（S6 モーダル）を発展させる形でも可。

| 画面 | 中身 | 既存の手本 |
|---|---|---|
| **ワークトレイ（受信箱）** | 自分の決裁待ち/起案中の稟議一覧（ScrollView 行） | `OrderOfBattlePanel` のリスト／`NotificationFeed` |
| **起案フォーム** | 所掌/スコープ/対象を選ぶ→**必要決裁者・回覧先・コストをプレビュー**（青=可決可能/赤=不相応＝政治プロト #14 の色分けを流用） | `politics-prototype-design.md` |
| **決裁画面** | 稟議の詳細＋合議状況＋［承認］［却下］。承認で効果適用＆ポーズ解除 | `StrategyEventPanel.Show` |
| **ダッシュボード** | KPI＝国庫/税率/支持/安定度/版図一体化（`LogisticsRules.CohesionFactor`）＝社内ポータルのトップ | S5 の `UpdatePolicyLine` を発展 |

- 表示中は `Time.timeScale=0`（既存パネル流儀）。`GalaxyView.Update` は `XxxPanel.IsOpen` の間 return（入力/進行を譲る）。
- 戦略シーンに `RuntimeInitializeOnLoadMethod` で自動生成（手配線不要）。キーは `GameInput` に `GameAction` を追加（戦略コンテキスト）。

---

## 6. 段階実装（推奨）

| 段 | 内容 | 検証 |
|---|---|---|
| **WF-1** | `Proposal`/`ProposalStatus`/`WorkflowRules`＋効果レジストリ（Core/Data・test-first） | EditMode/TestHarness |
| **WF-2** | UITK ワークトレイ＋決裁画面（手動で1件決裁→`CampaignState` が動く） | エディタ Play |
| **WF-3** | 起案フォーム＋合議（`SectionalismFriction`/`CivilianControl` ゲート・政体で段数変化） | エディタ Play |
| **WF-4** | AI 大臣が起案/決裁（無人運用でも稟議が回る） | エディタ Play |
| **WF-5（任意）** | Web 化：`CampaignSerializer` の JSON を localhost で実ブラウザ表示→決裁を戻す（D 方式の検証） | 別ブラウザ |

> WF-1〜2 で「稟議で国が動く」最小ループを作り、面白さを確認してから WF-3 以降へ。**埋め込みブラウザ（C）は WF-5 の手応え次第で初めて評価**する。

---

## 7. 【要・作者判断】
- **稟議の段数・速度の具体値**（専制=元首一発／民主=議会承認の段数）。政体ごとのテンポ。
- **却下/塩漬け（失効）の扱い**：縦割り抵抗が強いと稟議が流れる＝民主のもどかしさをどこまで出すか。
- **腐敗の表現**：専制で執行が"中抜き"される割合（`Regime.corruption` 連動）。
- ~~**プレイヤーの立ち位置**：元首として全決裁するのか、一省庁の長として権限内だけ動かすのか~~ → **【解決済み】**：どちらでもなく**序列外の「目安箱＝越階の諫言回路」**とする（建白↑／諮問裁可↓／注入横の3動詞）。詳細は [`meyasubako-proposal-redesign-design.md`](./meyasubako-proposal-redesign-design.md)（EPIC #MEYASU）。本 WF の状態機械を作り直さず、その上に「箱アクター・個人ごと信認・建白の伝播・LLM稟議生成」を足す発展形。
- **Web 化（D/C）に踏み込むか**：UITK で十分か、本物のブラウザ没入を狙うか。

---

## 8. 接続する既存システム（再掲・並行新設しない）
- 提案権限・役職：`OfficeRules`/`GovernmentRegistry`/`Office`（#142/#144）
- 文民統制・クーデター：`CivilianControlRules`（#145）
- 省庁ツリー・省益：`MinistryRules`/`Ministry`（#158）
- 政党・議会承認・総裁選：`PartyRules`/`LeadershipElectionRules`（#159/#165）
- 効果適用・単発イベント：`EventEngine`/`EventRules`/`GameEventDef`/`EventContext`（#116）
- 世界状態・財政・支持：`CampaignState`/`CampaignRules`/`FactionState`（S5/S6 稼働中）
- 外交・建艦の決裁対象：`DiplomacyRules`（#190）/`ShipyardRules`（#884）
- 通知：`NotificationCenter`/`NotificationFeed`（#964）
- セーブ/Web契約：`CampaignSerializer`/`CampaignSaveData`（FND-2 #495）
- UI 基盤：`GineiUITK`＋テーマ USS（UITK 段階移行）
