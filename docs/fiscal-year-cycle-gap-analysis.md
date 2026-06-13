# 政府の財政「予算と執行で1年が回るか」— 機能テストとギャップ分析（#163/#162/#161）

> テスト観点：**歳入(税) → 予算編成(配分) → 執行(歳出) → 帰結(出資度・債務・利払い) → 翌年** の輪が閉じるか。
> 検証：Core 部品の結合テスト `FiscalYearCycleTests`（EditMode）。結論＝**Core 部品は揃っていて結合すれば1年が回るが、`GalaxyView` のデモがその輪を組み立てていない**。本書は足りない配線を列挙する。

---

## 0. 判定：現状は「半周」しかしない

```
歳入(税) ───────────────► ✅ 回る（CampaignRules.TickEconomyDay が treasury に税収を毎日加算）
   │
予算編成(配分) ─────────► ❌ 無い（FactionState.budget は空のまま＝誰も配分しない）
   │
執行(歳出) ─────────────► ⚠️ ロジックはある（TickBudgetDay が treasury -= Total）が、予算が空＝歳出0
   │
帰結(出資度) ───────────► ❌ 未適用（MilitaryReadiness/Shipbuilding/AdminStability/WelfareHope/Research/Diplomacy が計算可能だが誰も使わない）
   │
帰結(債務/利払い) ──────► ❌ 未配線（FiscalRules.Tick が呼ばれない・FiscalState が勢力に存在しない）
   │
翌年 ───────────────────► ❌ 債務が繰り越されない（形式財政が無い）
```

**＝税収が国庫に貯まるだけの一方通行**。予算・執行・債務の帰結が生まれず、財政の1年が閉じない。

---

## 1. テストで確認できたこと（Core 部品は健全）

`FiscalYearCycleTests` が固定：
- **歳入**：`FiscalRules.TaxRevenue(課税ベース, 税率)`（税率0で0・1で全額）。
- **予算編成**：`BudgetRules.AllocateByWeights`（重み配分）＋`CapToRevenue`（緊縮＝歳入まで比例縮小・シェア保存）＋`IsBalanced`/`IsDeficit`。
- **執行（出資度）**：`MilitaryReadinessFactor`(満額1/過剰2/不足0.5)・`AdministrationStabilityBonus`(満額0/過剰+/不足−)・`WelfareHopeBonus`(同) が配分 vs 必要額で動く。
- **帰結（債務）**：`FiscalRules.Tick`＝黒字は減債・赤字は増債、`InterestPayment`(債務×金利)、`IsDebtSpiral`(高債務×PB<利払い)、複数年で債務が**繰り越し・複利膨張**。
- **統合**：歳入→予算→執行(現金収支)→`FiscalRules.Tick`(債務) を3年回すと、赤字なら現金が目減りし形式債務が積み上がる＝**輪が閉じる**。

**＝部品も式も正しい。問題は「組み立て（配線）」が無いこと。**

---

## 2. 足りない機能（あぶり出した配線ギャップ）

| # | ギャップ | 現状 | closing に必要な配線 |
|---|---|---|---|
| G1 | **予算が配分されない** | `FactionState.budget` は `new NationalBudget()` のまま＝全分野0 | 年次（or 政策）で `BudgetRules.AllocateByWeights(budget, 歳入見込み, 勢力の重み)`＋`CapToRevenue`。AI/プレイヤーの優先度を重みに |
| G2 | **執行が空回り** | `TickBudgetDay` は `treasury -= Total(budget)` だが Total=0 | G1 で予算を満たせば自動で効く（執行ロジック自体は配線済み） |
| G3 | **出資度が未適用** | 6つの Factor は計算可能だが誰も読まない | `ShipbuildingFactor`→`ShipyardRules` の productionFactor／`AdministrationStabilityBonus`→`GovernanceRules` の安定度目標／`WelfareHopeBonus`→`Community/HopeRules` のドリフト／`MilitaryReadinessFactor`→戦闘#106／`ResearchOutputFactor`→研究／`DiplomacyOpinionBonus`→#189 へ各1行で乗算/加算 |
| G4 | **形式財政が無い** | `FiscalState` がどこにも生成されない・`FactionState` に無い | `FactionState` に `FiscalState`（または debt フィールド）を持たせ、年次で `ApplyToFiscalState(budget,fs)`→`FiscalRules.Tick(fs, EconomyBase, dt)` を回す＝赤字→国債→利払い→翌年へ |
| G5 | **健全度が帰結しない** | `FiscalHealthFactor`/`ExchangeRateFactor` 未呼び出し | G4 の後、`FiscalHealthFactor`→安定度#109/支持#113・`ExchangeRateFactor`→交易#94 に係数#106 で接続 |
| G6 | **年次に財政段が無い** | `RunAnnualLifecycleTick` に予算/債務の処理が無い | G1+G4 を年次 Tick に1ブロック追加（`RunFiscalYearTick`）＝歳入見込み→予算編成→`Tick`→帰結反映 |

---

## 3. 推奨：年の輪を閉じる最小配線（順序）

1. **G1+G2**：`RunFiscalYearTick`（年次）で各勢力の `budget` を歳入見込み×重みで編成（`AllocateByWeights`+`CapToRevenue`）。既存の日次 `TickBudgetDay` が執行を実行＝現金が動く。
2. **G4**：`FactionState` に `FiscalState`（debt 等）を持たせ、年次で `Tick`＝赤字が債務へ繰り越し・利払いが翌年に乗る。
3. **G3+G5**：出資度・健全度を既存窓口（`ShipyardRules`/`GovernanceRules`/`HopeRules`/`CombatModifiers`/`FiscalRules`）へ1行ずつ接続＝予算配分の選択がゲームに効く。

いずれも **Core 純ロジックは追加不要**（既存の `BudgetRules`/`FiscalRules` を呼ぶだけ）。本書の検証テストが回帰の土台になる。

---

## 5. 配線実施（done）— 年の輪を閉じた

ギャップのうち **G1・G2・G4・G5＋G3(建艦)** を配線済み（Core 純ロジックは追加せず既存窓口を呼ぶ）：

- **G1 予算編成**：`GalaxyView.RunFiscalYearTick`（年次）が勢力ごとに `BudgetRules.AllocateByWeights(budget, 歳入レート×支出性向, 重み)`。**帝国＝軍拡（軍事/建艦厚め・性向1.1で赤字気味）／同盟＝均衡（内政/社会保障厚め）**。
- **G2 執行**：予算が満ちたので既存の日次 `CampaignRules.TickBudgetDay` が `treasury -= Total(budget)` で**現金を実際に動かす**。
- **G4 形式財政**：`FactionState.fiscal`（`FiscalState`）を新設し、`CampaignRules.TickFiscalYear`（年次）が `ApplyToFiscalState`＋`FiscalRules.Tick`＝**赤字→国債→利払い→翌年（債務繰り越し）**。
- **G5 帰結（通知）**：`FiscalRules.IsDebtSpiral` を年次で判定し債務スパイラルを警告。
- **G3 建艦**：`TickShipyard` の生産係数に `ShipbuildingFundingFactor`（建艦予算/必要額）を乗算＝**建艦予算を絞ると建艦が遅れ、厚くすると速まる**（#163→#884）。

**残（G3 の他分野）**：`AdministrationStabilityBonus`→`GovernanceRules`／`WelfareHopeBonus`→`HopeRules`／`MilitaryReadinessFactor`→戦闘#106／`DiplomacyOpinionBonus`→#189 は窓口は用意済み・接続は今後（同パターンで1行）。`FiscalHealthFactor`→安定度/交易（G5 残り）も同様。

**結果**：歳入(日次)→予算編成(年次)→執行(日次で現金が動く)→形式財政(年次で債務繰り越し)→翌年 が回る。建艦予算は建艦速度に効く。配線テスト＝`FiscalYearCycleTests.TickFiscalYear_*`。

---

## 4. テスト（EditMode・`FiscalYearCycleTests`）

歳入（税率→税収）／予算編成（配分・緊縮 cap・均衡）／執行（出資度の満額・過剰・不足）／帰結（黒字減債・赤字増債・複利・債務スパイラル）／統合（歳入→予算→執行→債務を3年）／**ギャップ pin（空予算＝執行0＝年が回らない）** を固定。`TestHarness`（dotnet）でも回帰。
