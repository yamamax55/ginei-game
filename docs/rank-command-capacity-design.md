# 階級準拠の指揮統率 — 兵力を人物から切り離す（EPIC #RANKCMD）

> **兵力（＝艦隊数/隻数）が人物（`AdmiralData.baseStrength`）に紐づいているのは設計的に誤り**。兵力は艦隊（`FleetUnitData`/`FleetStrength`）が持つべきで、人物（提督）は**階級に応じて「指揮できる規模」**を持つべき（銀河英雄伝説準拠）。本 EPIC はこの分離と、階級ごとの指揮可能規模の導入を行う。
> 状態：設計メモ（実装未着手）。数値・創作裁定は【要・作者判断】。

---

## 0. 問題

`AdmiralData.baseStrength`（「この提督が率いる際の基準兵力」・既定10000）が**人物に固定の兵力を持たせている**。これはおかしい：

- 兵力（艦艇数）は**艦隊の属性**であって、人物の属性ではない。同じ提督でも、率いる艦隊が違えば兵力は違う。
- 人物が持つべきは「**どれだけの規模を指揮できるか（指揮限界）**」であり、それは**階級**で決まる（銀英伝では中将で一個艦隊、元帥で宇宙艦隊総司令）。

人物名鑑（`PersonObserverOverlay`）に「兵力 10000」と出るのは、この誤った紐付けの表れ。

---

## 1. あるべき姿

| 概念 | 持ち主 | 出所 |
|---|---|---|
| **兵力（艦隊数/隻数）** | **艦隊** | `FleetUnitData.baseStrength`（編制台帳・#146）／会戦は `FleetStrength.strength`（旗艦艦艇数） |
| **指揮できる規模（指揮限界）** | **人物（階級）** | 階級 `rankTier`（#14）→ `CommandCapacityRules`（新規・銀英伝準拠） |

- 提督は固定兵力を持たない。配属された艦隊の兵力を率いる。
- 提督の階級が、率いられる**艦隊の規模の上限**と**梯団の段**（分艦隊/艦隊/艦隊群/宇宙艦隊）を決める。

---

## 2. 銀河英雄伝説準拠の指揮階級

銀英伝の指揮系統を参考に、階級 tier ごとの指揮可能規模を定める（隻数は目安・const 調整）：

| tier | 階級 | 指揮できる規模（銀英伝準拠） | 梯団の段 | 目安隻数 |
|---|---|---|---|---|
| 5 | 准将 | 分艦隊の一部〜分艦隊 | 分艦隊 | 〜3,000 |
| 6 | 少将 | 分艦隊 | 分艦隊 | 〜6,000 |
| 7 | **中将** | **一個艦隊の司令官になれる下限** | 艦隊 | 〜12,000 |
| 8 | **大将** | 標準的な一個艦隊司令官 | 艦隊 | 〜15,000 |
| 9 | 上級大将（帝国） | 複数艦隊・方面 | 艦隊群/方面 | 〜30,000 |
| 10 | **元帥** | **宇宙艦隊総司令（数個艦隊）** | 宇宙艦隊 | 〜60,000＋ |

> 例：ヤン（中将で第13艦隊司令）、ラインハルト（大将→元帥で全軍）、ミッターマイヤー/ロイエンタール（元帥で方面の複数艦隊）。
> **編制の段**：分艦隊（准将/少将）⊂ 艦隊（中将/大将）⊂ 艦隊群・方面（上級大将）⊂ 宇宙艦隊（元帥）。

これは現行 `OrderOfBattle` の梯団（艦隊7中将/軍団8大将/軍集団10元帥・#147）を**銀英伝準拠へ補正**する（大将は「軍団」でなく「艦隊」司令が自然・分艦隊の段が欠けている）。

---

## 3. データモデル案（Core・test-first・並行新設しない）

### 3-1. `CommandCapacityRules`（static・銀英伝準拠・純ロジック）
- `MaxStrengthForTier(rankTier)` … その階級が指揮できる最大兵力（隻数）。const ラダー（上表）。欠番 tier は `RankSystem.ResolveTier` で直近へ丸め。
- `EchelonForTier(rankTier)` … 指揮できる梯団の段（分艦隊/艦隊/艦隊群/宇宙艦隊）。
- `CanCommand(rankTier, fleetStrength)` … その兵力の艦隊を指揮できるか（`fleetStrength ≤ MaxStrengthForTier`）。
- `RequiredTierForStrength(strength)` … その兵力を率いるのに必要な最小階級。
> `RankSystem`（#14）を読むだけ。基準値非破壊。`CombatModifiers`(#106) と同様、ここに公式を集約し各所のインライン判定を増やさない。

### 3-2. `AdmiralData.baseStrength` の非推奨化
- 人物から固定兵力を外す。`baseStrength` は**非推奨**（後方互換のため残すが、新規は読まない）。兵力の出所を `FleetUnitData.baseStrength`／`FleetStrength.strength` に一本化。
- `BattleSetup`/`Squadron`/`FleetStrength.ApplyAdmiralData` が `AdmiralData.baseStrength` を読んでいれば、艦隊側（`FleetUnitData`/シナリオ）から取るように移す。

### 3-3. 分艦隊 echelon（任意・銀英伝準拠の段）
- `OrderOfBattle` の `EchelonType` に**分艦隊**を追加し、艦隊 ⊃ 分艦隊 の段を持てるようにする（少将/准将の指揮先）。

---

## 4. 既存への接続（並行新設しない）
- 階級：`RankSystem`（#14・tier/丸め）。
- 艦隊兵力：`FleetUnitData.baseStrength`（#146）・`FleetStrength.strength`（会戦）。
- 配属ゲート：`FleetRoster.CanAssign`（現状 `rankTier≥requiredTier`）／`OrderOfBattle.CanCommand`（梯団の `RequiredTier`）を `CommandCapacityRules` 経由に＝**過大な兵力の艦隊は下位階級が指揮不可**。
- 指揮班：`CommandStaffRules`（#885・副提督/参謀）と整合（副提督が指揮を補完しうるかは別途）。

---

## 5. 子Issue（着手順・test-first → 配線）

> **EPIC = #1710**。GitHub issue 起票済み（#1711〜#1714）。

| # | issue | 主眼 | 接続 |
|---|---|---|---|
| **RANKCMD-1** | #1711 | 兵力を人物から分離（`AdmiralData.baseStrength` 非推奨・兵力は `FleetUnitData`/`FleetStrength` が単一の出所） | #146・会戦 |
| **RANKCMD-2** ✅ | #1712 | `CommandCapacityRules`（銀英伝準拠・rankTier→最大指揮兵力・`CanCommand`/`RequiredTierForStrength`/`MaxStrengthForTier`） | `RankSystem`#14 |
| **RANKCMD-3** ✅ | #1713 | 配属ゲートへ反映（`FleetRoster.CanAssign(admiral,unit,…)`／`OrderOfBattle.CanCommand(admiral,formationId)`＋`StrengthUnder` を指揮可能規模で判定＝過大兵力は下位階級が率いれない・`AssignAdmiral`/`AssignCommander` が両ゲート・兵力0は規模0扱いで後方互換・EditMode 済） | #146/#147 |
| **RANKCMD-4** ✅ | #1714 | 銀英伝準拠の編制対応＋分艦隊 echelon（`EchelonType` に分艦隊を追加＝艦隊⊃分艦隊。`RequiredTier(分艦隊)=少将6`／`CommandCapacityRules.EchelonForTier`＝階級→自然な段で大将=艦隊を表現。EditMode 済。※軍団=大将8↔上級大将9 の付け替えは【要・作者判断】で保留＝既存 tier 据え置き・後方互換） | #147 |

### 推奨着手順
`RANKCMD-2`（指揮限界の純ロジック＝核・test-first）→ `RANKCMD-1`（兵力の分離）→ `RANKCMD-3`（ゲート反映）→ `RANKCMD-4`（編制段の補正）。

---

## 6. 完了条件（EPIC）
- 人物（`AdmiralData`）が固定兵力を持たない。兵力＝艦隊数は艦隊（`FleetUnitData`/`FleetStrength`）が単一の出所。
- 階級 tier ごとに**指揮できる規模**が銀英伝準拠で定まり（`CommandCapacityRules`）、人物名鑑は「兵力」でなく「**指揮可能規模/階級**」を表示する。
- 配属・梯団が指揮可能規模でゲートされ、**過大な兵力の艦隊は下位階級が指揮できない**。
- いずれも `RankSystem`/`FleetUnitData`/`OrderOfBattle`/`CommandStaffRules` を後退させず接続する additive 設計。

---

## 7. 【要・作者判断】
- **隻数ラダーの具体値**（各階級の最大指揮兵力）。銀英伝の「一個艦隊＝約1.2万〜1.5万隻」をどの tier に置くか。
- **階級↔梯団の対応**：大将を「艦隊」司令とするか（現 `OrderOfBattle` は軍団8大将）。分艦隊 echelon を足すか。
- **副提督による指揮補完**（#885）：副提督がいると一段上の規模を率いられる等のルールを入れるか。
- **指揮限界超過時の挙動**：率いきれない艦隊を率いると統率/士気にペナルティ（実効値パターン）か、そもそも配属不可か。
- 人物名鑑の表示：「兵力」を「指揮可能規模（〜N隻・分艦隊/艦隊/…）」へ差し替え。
