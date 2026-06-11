# ケインズ『一般理論』参考設計（EPIC #KEYN）

> 参照元：ケインズ『雇用・利子および貨幣の一般理論』（1936）。
> **有効需要の原理・乗数効果・アニマルスピリッツ・流動性の罠・節約のパラドックス**——
> 「需要が産出を決める」という反サプライサイドの逆転を核とした、20世紀経済学の最大の革命。
> 本ドキュメントは、当プロジェクトの**既存経済純ロジック層**（`FiscalRules`/`MarketRules`/`BankRules` 等）にとって
> **役に立つ視点だけ**を抽出し、EPIC `#KEYN` として issue 化する提案。
> 著作権注意：固有名・文章・固有設定は流用せず、**経済メカニクスの構造パターンのみ**を参考にする。

---

## 0. なぜ「一般理論」が本システムに役立つか

当プロジェクトは経済の**供給サイド純ロジックを大量に保有**している（[CLAUDE.md] 参照）：

| 既存（供給サイド・マクロ） | カバー範囲 |
|---|---|
| `FiscalRules`/`FiscalState`（#161/162） | 主要収支/国債/金利/為替/税/社会保障。`IsDebtSpiral` で債務膨張 |
| `MarketRules`/`Good`/`Market`（#179-182） | 需給均衡価格（ワルラス的自動均衡）・生活水準→支持 |
| `BankRules`/`Bank`（#186） | 信用創造・取付け・債務超過 |
| `GovernanceRules.OutputFactor`（#109） | **安定度比例で産出が決まる**（供給能力的） |
| `ShipyardRules.ProductionFactor`（#884） | **安定度比例で建艦速度が決まる**（供給能力的） |
| `HopeRules`/`Community`（#852） | 希望/末人。`FiscalRules.WelfareHopeBonus` で接続済み |
| `CapitalRules`/`CapitalState`（#917） | r>g集中/格差→反乱（ピケティ） |
| `FiscalClass`/`RedistributionRules`（#163） | 税の階級別負担・累進/逆進 |

**しかし、これらはすべて「供給が整えば産出が出る」という暗黙のサプライサイド仮定に立っている。**
ケインズが固有に描く以下の視点が**欠けている**：

| 一般理論が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **有効需要の原理**＝総需要が産出を決める | `GovernanceRules.OutputFactor` は安定度依存。**需要不足が供給能力を遊休させる**「需要ギャップ」が無い |
| **乗数効果**＝財政支出1→消費連鎖→所得n倍 | `FiscalRules.Tick` は国債残高の増減のみ。**支出の波及増幅（k=1/(1−c)）**が無い |
| **アニマルスピリッツ**＝投資は期待でなく「確信」で決まる | 建艦/投資は安定度比例。**悲観が悲観を呼ぶ信認崩壊**（自己実現的な投資凍結）が無い |
| **流動性の罠**＝金利ゼロ下限で金融政策が無効化 | `BankRules.InterestRate` にゼロ下限概念なし。**金融緩和が効かなくなる境界条件**が無い |
| **節約のパラドックス**＝個人合理が集団不況を生む | 各勢力の財政は独立。**全員が同時に緊縮すると全員が貧しくなる**需要連鎖崩壊が無い |

**結論**：一般理論は当プロジェクトの経済純ロジックに**「需要サイドの顔」**を与える。
①有効需要ギャップ ②乗数効果 ③アニマルスピリッツ ④流動性の罠 ⑤節約のパラドックス という
**5つの欠落軸**が、既存の `FiscalRules`/`GovernanceRules`/`ShipyardRules` に**直交して掛かる係数**になる。
マクロ財政の「収支管理」から「景気の波」へ——不況と好況の非線形ダイナミクスを与える。

---

## 1. 役に立つ視点（要約）

一般理論の世界観を、**本システムに効く形**で1行ずつ：

1. **需要が産出を決める**——供給能力があっても需要が無ければ遊休。艦船建造所は閑散とし、星系は豊かなのに人々は貧しい逆説。→ `GovernanceRules.OutputFactor` に需要係数を掛ける（供給のみの支配者より需要を生む者が銀河を動かす）。
2. **財政出動は乗数倍の波及を生む**——1兆クレジットの支出が乗数k倍の所得増を産む。赤字財政を恐れる古典派に対し、不況期には積極出動が合理的。→ `FiscalRules` に乗数を付与し、「財政均衡主義 vs ケインズ主義」を政策選択の分岐に。
3. **アニマルスピリッツは合理的期待を上回る**——投資家の「血気」が崩れると、利益計算が正でも誰も動かない。株価暴落（`StockMarketRules.CrashRisk`）と同型の、**実物投資の信認崩壊**。→ 戦敗・政変がアニマルスピリッツを吹き飛ばし造船を止める。
4. **流動性の罠では金融政策が空を打つ**——金利がゼロに近づくと、追加の通貨供給は皆が「いずれ上がる」と現金保有を増やすだけ。財政支出だけが有効。→ `BankRules` のゼロ下限が財政優位域を生む（金融vs財政の政策選択）。
5. **節約のパラドックス**——全勢力が同時に「倹約」すると、需要収縮→生産減→所得減→さらに倹約……という不況の自己強化スパイラル。→ 勢力間の需要連鎖：ある勢力の財政出動が隣の勢力の所得を支える（銀河規模の内需循環）。
6. **有効需要は平和の経済的基盤**——軍拡競争は需要を増やすが、平時の財政出動（インフラ・福祉）も同じ乗数効果がある。「戦争でしか景気が良くならない」という罠。→ 銀英伝の主題（武力による統一 vs 民主共和）に経済的根拠を与える lore。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`FiscalRules`/`MarketRules`/`BankRules`/`GovernanceRules` を作り直さない**。
> KEYN はそれらに**需要サイドの係数と動学を足し、接続する**だけ（additive）。
> タイクン化回避＝GDP数値の微操作でなく、需要/供給の**乖離係数**として背景的に効かせる。

### ★★★ 最優先（真の欠落・一般理論の signature）

#### KEYN 有効需要ギャップ（`OutputGap`/`EffectiveDemandRules`）
- **`OutputGap`**：勢力ごとの「潜在産出」（= `GovernanceRules.OutputFactor` で計算）と
  「実効需要」（= 財政支出 + 消費 + 投資 + 輸出の合計）の差 `gap`（−1..+1）。
  `EffectiveOutput = PotentialOutput × DemandFactor(gap)` — 需要不足なら潜在産出の何割しか出ない。
- 接続：`GovernanceRules.OutputFactor` に `DemandFactor` を**乗算**（基準値非破壊）。
  `ResourceProductionRules.Produce`/`ShipyardRules.ProductionFactor` が `DemandFactor` を読む。
  **需要ギャップが建艦速度・資源産出・安定度に一貫して波及**する。
- EditModeテスト：純関数 `DemandFactor(gap) ∈ [0.5, 1.0]`、`gap=0` で `1.0`、`gap=-1` で `0.5` を検証。

#### KEYN 財政乗数（`MultiplierRules`）
- **`MultiplierRules`**（純ロジック）：`FiscalMultiplier(mpc) = 1/(1−mpc)`（限界消費性向 0..1）。
  財政赤字（支出超過）→ `EffectiveDemand.AggregateShift(deficit × multiplier)` で需要を底上げ。
  財政黒字→逆向きのデフレ圧。**「赤字財政は景気刺激」「黒字財政は引き締め」の定量回路**。
- 接続：`FiscalRules.OverallBalance` → `EffectiveDemandRules`（KEYN-1）の入力として流入。
  `CampaignRules.Tick` が `FiscalRules.Tick` の後に `EffectiveDemandRules.Apply` を呼ぶ。
- EditModeテスト：`FiscalMultiplier(0.8)=5.0`、`FiscalMultiplier(0)=1.0`、`mpc>1` は例外を検証。

### ★★ 高（投資の不安定性・金融政策の限界）

#### KEYN アニマルスピリッツ（`AnimalSpiritsRules`/`InvestmentClimate`）
- **`InvestmentClimate`**（`[Serializable]` 純データ）：`confidence`（0..1、確信水準）。
  `AnimalSpiritsRules.InvestmentFactor(confidence)` → 建艦速度/資源投資に乗算。
  **負のショック**（戦敗・政変・クーデター `CoupRules`）が `confidence` を急落させ、投資が凍結する
  自己実現的スパイラル（need → output ↓ → income ↓ → need ↓ …）。
  **正のショック**（大勝・英雄登場 `GrowthRules`）が confidence を回復させ boom を引き起こす。
- 接続：`ShipyardRules.ProductionFactor` × `InvestmentFactor`（基準値非破壊）。
  `EventEngine`（#116）が戦勝/政変イベントを `InvestmentClimate.Shock` として伝播。
  KEYN-1 の `EffectiveDemand` の投資項に `InvestmentClimate.confidence` を流し込む。
- EditModeテスト：`InvestmentFactor(0)≈0.3`（完全凍結でも最低は残す）/`InvestmentFactor(1)=1.5`（ブーム）。

#### KEYN 流動性選好と金利下限（`LiquidityPreferenceRules`）
- **`LiquidityPreferenceRules`**（pure static）：`LiquidityDemand(uncertainty)` = 不安が高いほど
  現金保有を選ぶ（credit 需要を食いつぶす）。名目金利が `ZeroLowerBound`（既定0.5%）以下になると
  `IsLiquidityTrap=true`——これ以上の金融緩和（`BankRules.CreditCreation`）は有効需要を増やさない。
  `EffectiveCreditMultiplier = IsLiquidityTrap ? 0 : CreditCreation × (1−LiquidityDemand)` 。
- 接続：`BankRules.InterestRate` / `FiscalRules.IsDebtSpiral` を参照して `IsLiquidityTrap` を判定。
  流動性の罠の状態では KEYN-2 `MultiplierRules` の**財政乗数が最大**（金融は効かず財政のみが効く）。
- EditModeテスト：`IsLiquidityTrap(rate=0.0)=true`/`(rate=5.0)=false`/流動性罠時に `EffectiveCreditMultiplier=0`。

### ★ 中（集合的行為問題・世界観 lore）

#### KEYN 節約のパラドックス（`ThriftParadoxRules`）
- **`ThriftParadoxRules`**（pure static）：複数勢力が同時に `FiscalSurplus > ThriftThreshold` のとき、
  銀河全体の `AggregateEffectiveDemand` が収縮する需要連鎖崩壊を計算。
  `ParadoxRisk = ParticipatingFactions / TotalFactions`（一斉緊縮の割合が高いほどリスク↑）。
  `ParadoxMultiplier` でギャップが増幅——`CampaignRules.EffectiveStability` にペナルティ。
- 接続：`CampaignRules.Tick`（統合層）が全勢力の `FiscalState` を横断し `ThriftParadoxRules.Evaluate` を呼ぶ。
  `MarketRules`（需要連鎖）の `ClearingPrice` にも波及。
- EditModeテスト：全勢力緊縮（2/2）で `ParadoxRisk=1.0`、1/2 で `0.5`。

#### KEYN（lore）不況の時代と財政民主主義の開示データ
- 「不況は政策で治せる——均衡予算主義は誤り」
  「アニマルスピリッツが軍拡競争を呼ぶ——需要を戦争で満たしてはならない」
  「王道の帝国経済＝需要を生む平和」
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への**lore データ入力**。
  `EventEngine`（#116）の条件イベント「不況期の財政論争」に結びつける。

### ❌ 不採用（重複・既存で十分・タイクン化）

| 不採用 | 理由 |
|---|---|
| 国民所得統計（GDP/GNP）の明示的な計算 | タイクン化の典型。係数として背景的に効かせるだけ（`OutputFactor`×`DemandFactor`）でよい |
| 為替レート・国際収支の独立モデル | `FiscalRules.ExchangeRateFactor` がカバー。KEYN は接続のみ |
| 中央銀行・公定歩合の独立UI | `BankRules.InterestRate` がカバー。流動性の罠は状態フラグで足りる |
| フィリップス曲線（インフレ-失業トレードオフ） | `GovernanceRules.OutputFactor` + `DemographicsRules` で代替可能。独立実装しない |
| IS-LM図の教科書的忠実再現 | ゲームに不要。`EffectiveDemandRules`+`LiquidityPreferenceRules` の係数で本質は届く |
| 長期期待の明示モデル（将来割引） | `StockMarketRules.FairPrice`/`BankRules` で十分。重複しない |

---

## 3. EPIC #KEYN の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。
> 既存経済ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・人名・固有設定は不使用、**経済メカニクスの構造のみ**参考。

> **EPIC = #1538**。GitHub issue 起票済み（#1540〜#1557）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **KEYN-1** | #1540 | 有効需要ギャップ（`OutputGap`/`EffectiveDemandRules`・需要不足が潜在産出を遊休させる） | `GovernanceRules.OutputFactor`×`DemandFactor`。`ResourceProductionRules`/`ShipyardRules` が読む |
| **KEYN-2** | #1542 | 財政乗数（`MultiplierRules`・財政支出→所得連鎖の増幅 k=1/(1−c)） | `FiscalRules.OverallBalance`→KEYN-1 `EffectiveDemandRules` の入力 |
| **KEYN-3** | #1545 | アニマルスピリッツ（`InvestmentClimate`/`AnimalSpiritsRules`・信認崩壊→投資凍結→需要不足の自己強化スパイラル） | `ShipyardRules.ProductionFactor`×`InvestmentFactor`。`EventEngine` からショック伝播 |
| **KEYN-4** | #1548 | 流動性選好と金利下限（`LiquidityPreferenceRules`・ZLB で金融政策が無効化→財政のみが有効） | `BankRules.InterestRate`/`FiscalRules.IsDebtSpiral`→`IsLiquidityTrap`。KEYN-2 の乗数を最大化 |
| **KEYN-5** | #1552 | 節約のパラドックス（`ThriftParadoxRules`・全勢力一斉緊縮→需要連鎖崩壊→全員が貧しくなる集合的行為問題） | `CampaignRules.Tick`×全勢力`FiscalState`横断評価。`CampaignRules.EffectiveStability` へペナルティ |
| **KEYN-6** | #1557 | （lore）不況の時代・財政民主主義・アニマルスピリッツが戦争を招く開示データ | `DisclosureLedger`（FND-4）。`EventEngine`（#116）の「財政論争」イベントに接続。コード新設なし |

### 推奨着手順
`KEYN-1`（有効需要ギャップ＝最も本質的な欠落・他全員の基盤）→
`KEYN-2`（乗数＝財政政策に動学を与える。KEYN-1 に直結）→
`KEYN-3`（アニマルスピリッツ＝投資の不安定性・建艦速度に波及）→
`KEYN-4`（流動性の罠＝金融政策の限界。KEYN-2 と相互参照）→
`KEYN-5`（節約のパラドックス＝統合層 `CampaignRules` に配線。KEYN-1〜2 完了後）→
`KEYN-6`（lore＝コード不要なのでいつでも可）。

> いずれも既存財政/市場/生産ロジックを**後退させず接続**する additive 設計。
> `FiscalRules` の収支管理 + `GovernanceRules` の安定度管理に**需要の波**という第三の軸を加える。
