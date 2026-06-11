# キンドルバーガー『熱狂、恐慌、崩壊』参考設計（EPIC #KNDB）

> 参照元：Charles P. Kindleberger『Manias, Panics, and Crashes: A History of Financial Crises』。
> 歴史的な金融危機の解剖学——変位（ショック）→信用膨張→熱狂（mania）→苦境（distress）→恐慌（panic）→崩壊→収縮——を Minsky の金融不安定仮説と組み合わせ体系化した古典。
> 本ドキュメントは、当プロジェクト（Ginei＝既に巨大な金融・財政・銀行純ロジック層を保有）にとって**役に立つ視点**だけを抽出し、EPIC `#KNDB` として issue 化する提案。
> 著作権注意：固有名・文章・固有設定は流用せず、**危機メカニクス／世界観の構造パターンのみ**を参考にする。

---

## 0. なぜ「熱狂、恐慌、崩壊」が本システムに役立つか

当プロジェクトは金融の**マクロ純ロジックを大量に保有**している（[CLAUDE.md] 参照）：

| 既存（マクロ・抽象） | カバー範囲 |
|---|---|
| `BankRules`/`Bank`（#186） | 信用創造・取付け(`BankRunRisk`)・債務超過(`IsInsolvent`) |
| `FiscalRules`/`FiscalState`（#161/162） | 国債/金利/為替・`IsDebtSpiral`（利払い>プライマリ黒字） |
| `StockMarketRules`/`Company`（#185） | 株価・配当・暴落リスク(`CrashRisk`) |
| `MarketRules`/`Market`（#179-182） | 需給均衡価格・生活水準→支持 |
| `SAW-1/2`（起票予定） | 通貨品位改鋳・改鋳投機 |
| `EventEngine`（#116） | 条件発火→通知/選択肢→効果 |

**しかし、これらは「個別市場・個別機関」の点の危機モデル**であり、キンドルバーガーが固有に描く以下が**欠けている**：

| 熱狂、恐慌、崩壊が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **Minksyサイクル＝変位→信用膨張→熱狂→苦境→恐慌→崩壊** | `BankRunRisk`/`CrashRisk` は点的な発火。**フェーズを経る危機弧（arc）の状態機械**が無い |
| **最後の貸し手（lender of last resort）** | 危機が始まったとき誰が・どの条件で救済するか。Bagehot原則（高金利・優良担保で無制限貸出）の**解決機構**が無い |
| **金融伝染（contagion）** | `BankRunRisk` は単一銀行。**取付け・暴落が隣接システムへ波及**する連鎖モデルが無い |
| **熱狂期の詐欺・粉飾（swindles）** | 好況期に必ず出現し崩壊を加速するブーム詐欺の体系が無い |
| **フィッシャーの負債デフレーション** | `IsDebtSpiral` は財政（金利>黒字）。**価格下落→実質債務膨張→強制売却→さらなる価格下落**の民間負債連鎖が無い |

**結論**：キンドルバーガーは当プロジェクトの点的な金融危機モデルに**「危機は段階を経る弧である」という時間構造**と、①危機サイクル状態機械 ②最後の貸し手（解決機構） ③金融伝染 ④負債デフレーション という4つの欠落軸を与える。**フェザーン（#160 商社国家）・銀行（#186）・財政（#161/162）**が最も恩恵を受ける。

---

## 1. 役に立つ視点（要約）

キンドルバーガーの世界観を、**本システムに効く形**で1行ずつ：

1. **危機は段階を経る**。変位→信用膨張→熱狂→苦境→恐慌→崩壊は歴史が繰り返すパターン。→ `BankRules`/`StockMarketRules`/`FiscalRules` を**共通の弧でつなぐ状態機械**。
2. **「最後の貸し手」がいなければ局所的取付けが全域崩壊になる**。救済はモラルハザードを生むが、放置は連鎖を止めない。→ **解決機構の設計選択**がゲームの政策次元に。
3. **危機は伝染する**。一国（一星系）の取付けが隣接国（隣星系）へ飛び火する。回廊（`Corridor`）を通じた伝染は `LogisticsRules` とは別の連鎖。
4. **好況期にこそ詐欺師が生まれる**。熱狂が発覚を遅らせ、粉飾崩壊が信頼を二重に破壊する。→ `BoomFraudRules`＋`EventEngine` で創発。
5. **フィッシャーの負債デフレーション**。価格下落→実質負債増大→追加売却→さらなる下落——止める手段は「価格水準を保つ」か「債務を減らす」か。→ `FiscalRules` や `MarketRules` への係数修正子。
6. **介入の遅れが危機を深める**（but介入が早すぎるとモラルハザード）。→ `EventEngine` のタイミング問題を**ゲームの政策判断**に。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`BankRules`/`FiscalRules`/`StockMarketRules`/`MarketRules` を作り直さない**。KNDB はそれらに**欠落軸を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・キンドルバーガーの signature）

#### KNDB 危機サイクル状態機械（Minskyサイクル）
- **`MinskyPhase` enum**：`{安定, 膨張, 熱狂, 苦境, 恐慌, 収縮}` の6フェーズ。
- **`CrisisCycleRules`**（純ロジック・test-first）：変位ショック→信用膨張率が閾値を超えると熱狂へ→資産価格が反転すると苦境→取付け発火で恐慌→最後の貸し手介入or放置で収縮。各フェーズ遷移に**修正子**（`BankRules`/`FiscalRules`/`MarketRules` への係数）と**発火イベント**（`EventEngine`）。
- 接続：`BankRules.BankRunRisk` × `StockMarketRules.CrashRisk` × `FiscalRules.FiscalHealthFactor` → フェーズ判定。`GalaxyView.Update` が `CrisisCycleRules.Tick` を `CalendarDispatcher` 日次で回す。

#### KNDB 最後の貸し手（Bagehot原則・危機解決機構）
- **`LenderOfLastResortRules`**（純ロジック・test-first）：Bagehot則＝`LendFreely`（流動性危機に無制限貸出）× `AtPenaltyRate`（高金利＝モラルハザード抑制）× `AgainstGoodCollateral`（担保あり=優良資産のみ）。
- **介入判断**：`ShouldIntervene`（恐慌フェーズ + 波及リスク > 閾値）／`MoralHazardCost`（救済回数が増えるほど次の危機確率↑）／`SystemicRisk`（伝染規模見積もり）。
- 接続：`FactionState.Organization`（中央銀行相当の制度化度）× `CrisisCycleRules` の恐慌フェーズ → `EventEngine` 選択肢（介入/放置/条件付き）＋`FiscalRules.Tick`（介入コスト）。

### ★★ 高（危機の波及・連鎖）

#### KNDB 金融伝染（取付け・暴落の星間波及）
- **`FinancialContagionRules`**（純ロジック・test-first）：`ContagionRisk(systemA, systemB, map)`（回廊接続 × 経済統合度 × 危機フェーズ → 伝染確率）／`PropagateShock`（源発地の危機フェーズを隣接システムへ遷移させる確率的伝播）／`FirewallStrength`（外貨準備・FactionState → 防火壁）。
- **Correlated crash**：同一回廊の両端が熱狂フェーズなら相関崩壊リスク上昇（`CorrelatedCrashRisk`）。
- 接続：`GalaxyMap.Neighbors` × `CrisisCycleRules.MinskyPhase` × `LogisticsRules.CohesionFactor`（版図一体化が高いほど伝染しやすい）。**`StrategyRules.IsFtlBlocked` とは別系統**（経済伝染 vs. 軍事封鎖）。

#### KNDB フィッシャーの負債デフレーション
- **`DebtDeflationRules`**（純ロジック・test-first）：`DebtDeflationPressure`（`MarketRules` の価格水準低下 × 民間債務比率 → 実質負債膨張率）／`ForcedSelling`（膨張が閾値超えると強制売却→さらなる価格下落の自己強化ループ）／`IsDeflationSpiral`（収束しない無限後退条件）／`BreakDeflation`（リフレ政策：`FiscalRules.Tick` の財政出動or `LenderOfLastResortRules.LendFreely` で断ち切る）。
- `FiscalRules.IsDebtSpiral`（財政赤字→金利→利払い膨張）とは別回路＝**民間負債 × 価格水準**の連鎖。接続：`MarketRules.Tick` × `BankRules.CreditCreation` 縮小 × `CrisisCycleRules` 収縮フェーズ。

### ★ 中（詐欺・ブーム信頼崩壊・lore）

#### KNDB ブーム詐欺と信頼崩壊（Minskyの「ポンツィ金融」）
- **`BoomFraudRules`**（純ロジック・test-first）：`FraudEmergenceProbability`（熱狂フェーズ比例で詐欺出現確率上昇）／`FraudMagnitude`（信用拡大量に比例）／`FraudDiscoveryRisk`（収縮フェーズで高まる）／`ConfidenceShock`（発覚時の信頼崩壊＝`FiscalHealthFactor`/`GovernanceRules.OutputFactor` ペナルティ）。
- `CrisisCycleRules.MinskyPhase` が熱狂のとき `EventEngine` が詐欺イベントを確率発火 → 発覚でさらにフェーズ加速。

#### KNDB（lore）危機の歴史的パターンと世界観開示データ
- 「熱狂は繰り返す」「制度（最後の貸し手）が危機の伝染を断ち切る」「負債デフレーションを放置した1930年代と介入で止めた1987年」。
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への**lore データ入力**。CCX-6（世界観codex退避）方針に一貫。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 銀行取付けそのものの新設 | **`BankRules.BankRunRisk`/`IsInsolvent` が既にカバー**。KNDB は状態機械でこれを包む |
| 株式暴落そのものの新設 | **`StockMarketRules.CrashRisk` がカバー**。KNDB は伝染・フェーズ包含のみ |
| 財政赤字・国債スパイラル | **`FiscalRules.IsDebtSpiral` がカバー**。KNDB は民間負債デフレとして別軸で足す |
| 通貨危機・改鋳投機 | **SAW-1/2 がカバー**。KNDB は状態機械から参照のみ |
| 高位の中央銀行マイクロ操作 | タイクン化回避。政策選択は `EventEngine` の選択肢で高位に |
| 個別銀行の資産運用マイクロ | タイクン化回避。`BankRules` のマクロ係数で背景的に |

---

## 3. EPIC #KNDB の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。既存金融ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**危機メカニクス/世界観構造のみ**参考。

> **EPIC = #1608**。子Issue = #1610〜#1623（KNDB-1〜6）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **KNDB-1** | #1610 | 危機サイクル状態機械（`MinskyPhase` enum + `CrisisCycleRules`）＝変位→信用膨張→熱狂→苦境→恐慌→収縮 | 新 `CrisisCycleRules`。`BankRules`×`StockMarketRules`×`FiscalRules` をフェーズで包む |
| **KNDB-2** | #1613 | 最後の貸し手（`LenderOfLastResortRules`）＝Bagehot原則・高金利/優良担保/無制限貸出・モラルハザードトレードオフ | `CrisisCycleRules`×`EventEngine`×`FiscalRules.Tick` |
| **KNDB-3** | #1615 | 金融伝染（`FinancialContagionRules`）＝取付け・暴落の星間波及・防火壁・相関崩壊リスク | `GalaxyMap.Neighbors`×`CrisisCycleRules`×`LogisticsRules` |
| **KNDB-4** | #1619 | フィッシャーの負債デフレーション（`DebtDeflationRules`）＝価格下落→実質債務膨張→強制売却の自己強化ループ | `MarketRules`×`BankRules`×`CrisisCycleRules` 収縮フェーズ |
| **KNDB-5** | #1621 | ブーム詐欺と信頼崩壊（`BoomFraudRules`）＝熱狂期の詐欺出現確率・発覚で信頼崩壊・`EventEngine`連動 | `CrisisCycleRules` 熱狂フェーズ×`EventEngine`×`FiscalHealthFactor` |
| **KNDB-6** | #1623 | （lore）危機の歴史的パターンと世界観開示データ（繰り返す熱狂・制度の価値・放置 vs. 介入の分岐） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順

`KNDB-1`（危機サイクル状態機械＝基盤）→ `KNDB-2`（最後の貸し手＝解決機構）→ `KNDB-3`（金融伝染＝星間連鎖）→ `KNDB-4`（負債デフレーション）→ `KNDB-5`（ブーム詐欺）→ `KNDB-6`（lore）。

> いずれも既存金融ロジック（`BankRules`/`FiscalRules`/`StockMarketRules`/`MarketRules`）を**後退させず接続**する additive 設計。フェザーン#160（商社国家）・国家財政・戦略経済レイヤーに最も効く。
