# C4: 軍の質が会戦に効くか — 監査と合成窓口

> テスト計画 [test-completion-plan.md] の C4。会戦のダメージ/勝敗に、提督能力・陣形・士気・**下士官団・新兵練度・補給/予算 readiness** が届いているかを検証。

## 0. 会戦ダメージの実体

`ShipCombat.ComputeDamage`（Game/ShipCombat.cs:187）：
```
ダメージ = baseDamage × 提督攻撃(AbilityFactor(EffectiveAttack)) × 士気(moraleFactor) × 側背面(FlankFactor)
```
＋防御は被弾側 `FleetStrength.TakeDamage`（`DefenseDamageFactor`）。

## 1. 何が効いているか（WIRED）

| 質の入力 | 経路 | 状態 |
|---|---|---|
| 提督攻撃（参謀補完込み `EffectiveAttack`） | `ComputeDamage`→`CombatModifiers.AbilityFactor` | ✅ |
| 士気 | `ComputeDamage`（moraleFactor） | ✅ |
| **継戦/兵站（`FleetSustainment`）** | `FleetMorale.GetMoraleFactor` が `sustainment.EffectiveFactor` を乗算→ComputeDamage | ✅（士気経由・ORBAT-4） |
| 側背面 | `CombatModifiers.FlankFactor` | ✅ |
| 提督防御（`EffectiveDefense`） | `FleetStrength.TakeDamage`→`DefenseDamageFactor` | ✅ |
| 得意陣形（#104） | `FleetMovement`/`TakeDamage`（移動/被ダメ） | ✅（別経路） |

## 2. 何が効いていないか（ORPHAN・C4 のギャップ）

会戦経路（`ShipCombat`/`FleetWeapon`/`FleetStrength`/`EscortShip`）から参照0：

| 質の入力 | 計算窓口 | 会戦への到達 | 根因 |
|---|---|---|---|
| **下士官団（背骨）** | `NcoEducationRules.ProficiencyMultiplier`（命中/回避） | ❌ | **会戦ユニットに `NcoCorps` が紐づいていない**（#210 三層 attribute 未実装＝`FleetStrength`/`Squadron` にフィールド無し） |
| **新兵練度** | `RecruitTrainingRules`/`SkillEffectRules.MilitaryQuality` | ❌ | 同上＝ユニットに練度が無い |
| **弾薬/補給の即応** | `MilitaryReadinessRules.FirepowerFactor`（弾薬→戦闘力 #2049） | ❌ | `ComputeDamage` が読まない＋戦略の補給状態が会戦へ渡らない |
| **予算の軍事 readiness** | `BudgetRules.MilitaryReadinessFactor`（#163） | ❌ | 同上（C5 残件） |

**＝「下士官団を鍛えた／新兵を練成した／補給が満ちた／軍事予算を厚くした」が会戦の強さを変えない。** 軍政・教育・財政の積み上げが戦術結果に届かない＝4X として痩せる核。

## 3. 本タスクの寄与（合成窓口）

- **`ForceQualityRules.CombatMultiplier(NcoCorps, recruitProficiency, readinessFactor)`**（Core・test-first）を新設＝**下士官団の背骨 × 新兵練度 × 即応 を単一の戦闘力倍率（0.4〜2.0）に合成**する唯一の窓口。会戦経路はこの1倍率を `ComputeDamage` に掛けるだけで「質」が効く設計点。各部品は既存窓口へ委譲（二重実装しない）。
- テスト `ForceQualityRulesTests`：精鋭>素人／即応で増減／上下限クランプ／即応は `MilitaryReadinessRules`/`BudgetRules` の出力をそのまま渡せる。

## 4. 配線（実装済）— 質が会戦ダメージに届く

**会戦側の統合点（既定1.0＝従来動作不変）**：
- `ShipCombat.ComputeDamage(..., out isFlank, float qualityFactor = 1f)` を追加し `baseDamage × 提督攻撃 × 士気 × 側背面 × qualityFactor`。
- `FleetStrength.qualityFactor`（public 調整値・既定1.0）。`FleetWeapon`（旗艦）・`EscortShip`（配下艦は旗艦の質に従う）が `ComputeDamage` へ渡す。

**戦略→会戦の供給（grounded source）**：
- `BattleHandoff.qualityA/qualityB`（既定1.0・`Queue` でリセット）。`BattleSetup.SetupFromHandoff` が旗艦の `qualityFactor` に流す。
- `GalaxyView.TryDescend`：降下する艦隊の**補給**（`StrategicFleet.supply`→`MilitaryReadinessRules.FirepowerFactor`）を `ForceQualityRules.CombatMultiplier(null, 0.5, …)` で質倍率にする＝**干上がった艦隊で会戦に降りると弱い**（既存の補給状態が戦術結果に効く）。

## 5. 残（質をさらに豊かにする・段階）

- **下士官団・新兵練度の attribute**（#210）＝今は `null`/中立（補給のみが質を動かす）。`StrategicFleet`/`FleetStrength` に `NcoCorps`＋練度を持たせれば `ForceQualityRules` の他2因子も効く（合成窓口は対応済み）。
- **予算 readiness**（`BudgetRules.MilitaryReadinessFactor`）を `qualityA/B` に合成（C5）。
- 直置きシナリオ（`ScenarioData`）では `qualityFactor` 既定1.0＝従来動作（後方互換）。

## 6. 結論

C4：**会戦の中核（提督/士気/側背面/防御/継戦）に加え、軍の質（当面は補給＝即応）が `qualityFactor` 経由で会戦ダメージに届くようになった**。合成は `ForceQualityRules`（Core・test）が単一窓口。下士官団/新兵練度は attribute（#210）で追って効かせる。
