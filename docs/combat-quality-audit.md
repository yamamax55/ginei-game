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

## 4. 残（質を会戦へ届ける配線・要・段階実装）

合成窓口は出来たが、会戦に効かせるには2段の配線が要る（いずれも Game 層・#210 PB を含む）：
1. **会戦ユニットへ質を attribute**＝`FleetStrength`（または `Squadron`）に `NcoCorps`＋新兵練度＋（戦略から持ち込む）補給/予算 readiness を持たせる（戦略→会戦は `BattleHandoff` で運ぶ）。
2. **`ComputeDamage` に質倍率を1つ足す**＝`ComputeDamage(..., float qualityFactor = 1f)` を追加し `baseDamage × … × qualityFactor`。各 caller（`FleetWeapon.PerformAttack`/`EscortShip`）が `ForceQualityRules.CombatMultiplier(...)` を渡す。

> ＝合成ロジック（数式）は本タスクで Core に確定・テスト済。残るは**データ attribution（#210）と1パラメータ追加**で、会戦コンポーネントに触れるため承認の上で段階的に。

## 5. 推奨

C4 の結論：**会戦の中核（提督/士気/側背面/防御/継戦）は効くが、軍政・教育・財政で積んだ「質」は届いていない**。完成（選択が会戦結果を変える）には §4 の2段配線が要る。`ForceQualityRules` がその合成点として準備済み。
