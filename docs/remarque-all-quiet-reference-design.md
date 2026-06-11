# レマルク『西部戦線異状なし』参考設計（EPIC #RMK）

> 参照元：エーリヒ・マリア・レマルク『西部戦線異状なし』（Im Westen nichts Neues, 1929）。
> 第一次世界大戦の西部戦線を若い兵士の目で描いた反戦小説——**産業化された消耗戦・戦友紐帯・失われた世代**。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略＋既存の社会・政治・軍事純ロジック層）にとって
> **役に立つ視点だけを抽出し**、EPIC `#RMK` として issue 化する提案。
> **著作権注意：固有名・文章・キャラクター・固有設定は流用せず、社会・軍事のメカニクス／構造パターンのみを参考にする。**

---

## 0. なぜ「西部戦線異状なし」が本システムに役立つか

当プロジェクトは個人の士気・戦時の社会崩壊・人口動態を**すでに広く保有**している：

| 既存（カバー範囲） | 担当モジュール |
|---|---|
| 戦術士気・敗走・回復 | `FleetMorale`（戦闘内リセット型） |
| 旗幟・寝返りカスケード | `LoyaltyRules`/`BattleAllegianceRules`（#817） |
| 希望の喪失・末人 | `HopeRules`/`Community`（#852） |
| 合意の撤退・非協力 | `ConsentRules`/`Polity`（#836） |
| 厭戦・戦争目標の正統性 | `WarGoalRules.WarWeariness`（DIP-3 Wave2） |
| 三位一体の緊張（政府×軍×民） | `TrinitarianTensionRules`（CLZ-3） |
| 作戦摩擦 | `FrictionRules`（CLZ-1） |
| 個人の老衰・死亡 | `LifecycleRules`/`Calendar`（LIFE-1/2） |
| 人口コホート動態 | `DemographicsRules`/`Population`（LIFE-3） |
| 指揮官輩出パイプライン | `CareerPipelineRules`（LIFE-5/6/7） |
| 捕虜・処断 | `CaptivityRules`（LIFE-4） |

**しかし、これらは「国家・社会」という抽象主体のマクロ集計**であり、レマルクが固有に描く以下が**欠けている**：

| レマルクが固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **会戦をまたぐ累積疲弊** | `FleetMorale` は1会戦ごとにリセット。会戦を重ねるほど回復しにくくなる**持続的疲弊**（消耗戦の核心）が無い |
| **戦友紐帯（Kameradschaft）** | `LoyaltyRules` は理念・正統性による忠誠。**特定個人への感情的絆**（仲間が生きている間だけ戦力が出る、仲間が死ぬと崩れる）が無い |
| **膠着戦況の結果型** | `StrategyRules.ResolveCorridorBattle` は必ず勝者を返す。**両軍が大損害を被りながら決着がつかない**「拮抗」という第3の戦闘結果が無い |
| **前線-後方情報非対称** | `TrinitarianTensionRules.popularSupport` は統一合意値。**プロパガンダで後方支持が高いまま前線士気が崩れる**という乖離モデルが無い |
| **世代断絶（失われた世代）** | `DemographicsRules.Tick` は自然老衰。戦争が18〜25歳コホートを集中消耗させ**20年後のリーダーシップ欠乏**を生む経路が無い |
| **帰還兵の厭戦伝播** | `HopeRules`/`ConsentRules` は市民の内的動態。**戦場の真実を知る帰還兵が後方に伝播して市民希望を侵食する**という感染経路が無い |

**結論**：西部戦線異状なしは既存の戦争モジュールに**「消耗戦の内側から見た視点」**と、
**①累積疲弊 ②戦友紐帯 ③膠着（拮抗）④前線-後方乖離 ⑤世代断絶 ⑥帰還兵伝播**という6つの欠落軸を与える。
既存の CLZ/GUN/DIP の**後退なく接続する additive 設計**。

---

## 1. 役に立つ視点（要約）

レマルクの世界観を、**本システムに効く形**で1行ずつ：

1. **兵士は繰り返す会戦で精神的に磨耗し続ける**。休息なき前線勤務は回復力を削り、戦力が数字と乖離する。→ 累積疲弊 `CombatFatigueRules` の根拠（`CLZ-1` 摩擦の内的版）。
2. **兵士が戦う理由は「国家」でなく「隣の戦友」**。仲間が死ぬたびに戦う理由が失われ、小集団の崩壊が部隊崩壊の引き金になる。→ `KameradschaftRules`：一次集団凝集力の動的モデル。
3. **産業化された消耗戦は決着を生まない**。双方が大損害を被りながらも戦線は動かず、戦争は無目的に持続する。→ 膠着(`StalemateRules`)という第3の戦闘結果。
4. **後方の民衆はプロパガンダで「戦況良好」と思い込む**。前線の真実と後方の認識が乖離し、それが乖離として爆発する時、体制は一夜で崩れる。→ `HomeFrontRules`：前線-後方情報非対称。
5. **18歳で出征した者の世代は丸ごと消える**。戦争終結後に何が残るか——指導者・技術者・親の世代の欠落が国家の未来を20年腐食する。→ `GenerationalWoundRules`：世代断絶の純ロジック。
6. **傷ついた帰還兵が戦場の真実を持ち帰る**。英雄譚でなく悪夢を語る者が増えるほど、後方の「戦争への合意」は侵食される。→ `ReturneesContagionRules`：帰還兵による後方希望の侵食。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> **大原則：`FleetMorale`/`LoyaltyRules`/`WarGoalRules`/`DemographicsRules` を作り直さない。**
> RMK はそれらに**欠落回路を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・レマルクの signature）

#### RMK-1 累積戦闘疲弊 `CombatFatigueRules` / `CombatFatigue`
- **`CombatFatigue`（純データ）**：`battleCount`（参戦会戦数）/ `cumulativeCasualtyRatio`（累積損耗率）/ `restDays`（休息日数）/ `fatigueLevel`（0..1・高いほど深刻）。
- **`CombatFatigueRules`（static）**：
  - `Accumulate(fatigue, casualtyThisBattle, dt)` → `fatigueLevel` 上昇（損耗が多いほど急増）
  - `Recover(fatigue, restDt)` → 休息で低下（疲弊が深いほど回復遅い・非線形）
  - `FatigueMoraleModifier(fatigue)` → 0.5〜1.0 の倍率（`FleetMorale.GetMoraleFactor()` への乗算係数）
  - `IsExhausted(fatigue)` → 閾値で戦闘不能判定
- 接続：`FleetMorale.GetMoraleFactor()` × 疲弊係数（基準値非破壊・実効値パターン）/ `BattleHandoff` で疲弊を会戦→戦略へ持ち越し / `StrategySession` で各艦隊の `CombatFatigue` を保持。
- **純ロジック・test-first・EditMode テスト必須**。

#### RMK-2 戦友紐帯 `KameradschaftRules` / `PrimaryGroup`
- **`PrimaryGroup`（純データ）**：`memberId`（艦隊/将校ID）/ `bondStrength`（紐帯の強さ0..1・出撃を重ねるほど上昇）/ `survivingRatio`（生存率）。
- **`KameradschaftRules`（static）**：
  - `GrowBond(group, sharedCombat)` → 共同戦闘で絆が深まる（上限あり）
  - `OnMemberLost(group, lostCount, total)` → 生存率低下→絆崩壊速度加速
  - `CohesionBonus(group)` → 生存率高いほど戦闘効率ボーナス（`CombatModifiers` に乗算）
  - `CollapseThreshold()` → 生存率 < 閾値で戦友紐帯崩壊（一次集団凝集ボーナスが消失）
- 接続：`CombatModifiers`（`ModifierStack.Mul` に一次集団ボーナス）/ `FleetStrength.TakeDamage` で損耗時に `OnMemberLost` を呼ぶ / `AdmiralData.staffOfficers`（制度的補佐）とは**別系統**（人情 vs 制度）。
- **純ロジック・test-first・EditMode テスト必須**。

#### RMK-3 膠着戦況 `StalemateRules` — 拮抗という第3の戦闘結果
- **`StalemateResult`（struct）**：`isStalemate`/ `aRemaining`/ `bRemaining`/ `aBattlesLost`/ `bBattlesLost`。
- **`StalemateRules`（static）**：
  - `IsMutualAttrition(aStr, bStr, StalemateParams)` → 双方の損耗率が接近かつ高いとき拮抗判定
  - `ResolveStalematedBattle(aStr, bStr, params)` → `StalemateResult` を返す（双方減耗・勝者無し）
  - `WarWearinessImpact(result)` → 拮抗は単純敗北より厭戦コストが高い（終わらない戦争のコスト）
  - `StalemateParams`：`attritionThreshold`（双方損耗率閾値）/ `breakoutBonus`（突破力差大なら拮抗不成立）
- 接続：`StrategyRules.ResolveCorridorBattle` → 拮抗判定を呼び、拮抗時は `engaged` を延長 / `WarGoalRules.WarWeariness` に拮抗コスト加算（CLZ-2/GUN の動員過少見積もりと合流）。
- **純ロジック・test-first・EditMode テスト必須**。

### ★★ 高（前線-後方の分断と世代への傷）

#### RMK-4 前線-後方情報非対称 `HomeFrontRules` / `HomeFrontState`
- **`HomeFrontState`（純データ）**：`frontMorale`（前線実態 0..1）/ `homeFrontSupport`（後方支持 0..1・プロパガンダ操作後）/ `propagandaIntensity`（0..1）/ `realityGap`（乖離量）。
- **`HomeFrontRules`（static）**：
  - `UpdateHomeFrontSupport(frontMorale, propaganda)` → プロパガンダ強度が高いほど後方支持が前線実態から乖離して高止まり
  - `RealityGap(frontMorale, homeFrontSupport)` → 乖離量算出
  - `GapBreakingPoint(gap)` → 乖離が閾値を超えると急激崩壊リスク（`ConsentRules.Withdraw` を引き起こす）
  - `PropagandaDecay(intensity, returnees)` → 帰還兵数が増えるとプロパガンダ効果が減衰（RMK-6 と連動）
- 接続：`FleetMorale`（前線実態）× `ConsentRules.ControlStrength`（後方合意）× `TrinitarianTensionRules.popularSupport`（CLZ-3）× `EventEngine`（乖離崩壊イベント）。
- **純ロジック・test-first・EditMode テスト必須**。

#### RMK-5 世代断絶 `GenerationalWoundRules` / `GenerationalWound`
- **`GenerationalWound`（純データ）**：`cohortBirthYear`（対象世代の出生年）/ `initialSize`（戦前コホート人口）/ `survivingSize`（生存者数）/ `depletionRatio`（0..1）/ `woundYear`（戦争年）。
- **`GenerationalWoundRules`（static）**：
  - `ApplyWarAttrition(wound, warYears, casualtyRate)` → 若年コホートを集中消耗（`DemographicsRules.Tick` の自然死亡とは別経路）
  - `LeadershipVacuumFactor(wound, elapsedSinceWar)` → 20年後にピークを持つリーダーシップ欠乏係数（0.5〜1.0）
  - `PipelineImpact(wound, elapsedSinceWar)` → `CareerPipelineRules` への供給減倍率
  - `IsVacuumActive(wound, currentYear)` → 傷が現在のリーダー層に影響しているか
- 接続：`DemographicsRules`（通常人口動態）× `CareerPipelineRules`（指揮官輩出パイプ）× `LifecycleRules`（個別死亡）× `AnnualLifecycleRules`（暦の年次）。
- **純ロジック・test-first・EditMode テスト必須**。

### ★ 中（帰還兵による後方侵食）

#### RMK-6 帰還兵の厭戦伝播 `ReturneesContagionRules` / `ReturneeEffect`
- **`ReturneeEffect`（純データ）**：`returneeCount`（帰還兵数）/ `traumaLevel`（0..1・平均疲弊レベル）/ `contagionStrength`（伝播力）。
- **`ReturneesContagionRules`（static）**：
  - `ContagionRate(effect, propagandaIntensity)` → プロパガンダが強いほど伝播を抑制（前線-後方格差 RMK-4 と双方向）
  - `ApplyContagion(community, effect)` → `HopeRules.Shift` で後方 `Community.hope` を低下
  - `IsSilenced(effect, propagandaIntensity)` → プロパガンダが閾値を超えると帰還兵の証言が封殺される（公式物語の勝利）
  - `ReturneeCountFromAttrition(fatigue, fleetCount)` → `CombatFatigue`（RMK-1）から帰還兵数を推定
- 接続：`CombatFatigueRules`（RMK-1）× `HomeFrontRules.PropagandaDecay`（RMK-4）× `HopeRules`（後方 `Community.hope` の侵食）× `WarGoalRules.WarWeariness`（厭戦値への加算）。
- **純ロジック・test-first・EditMode テスト必須**。

### ❌ 不採用（既存でカバー・タイクン化回避）

| 不採用 | 理由 |
|---|---|
| 個人PTSD詳細モデル（症状・治療・復帰） | マイクロ操作増加。`CombatFatigue`（RMK-1）の疲弊係数で代替 |
| 医療・野戦病院システム | 既存 `SupplyRules`（#94）の補給線に接続するだけ。新EPIC化しない |
| 銃殺（軍法・脱走処罰） | `CaptivityRules`（LIFE-4）の `Execute` + `CivilianControlRules` が類似。重複新設しない |
| 戦壕戦の地形システム | 既存 `ZoneOfControl`/`BlackHole` の戦術地形に接続。会戦ルール改変は大きすぎ |
| 開戦前の外交・動員 | GUN（`MobilizationRules`/`AllianceCascadeRules`）が完全にカバー |
| 戦争の原因論（動員ゲーム） | GUN・CLZ がカバー。重複不採用 |
| 厭戦・戦争目標の正統性そのもの | DIP-3 `WarGoalRules.WarWeariness` がカバー。RMK はここへ加算するだけ |

---

## 3. EPIC #RMK の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。
> 既存モジュールは**接続のみ・重複新設しない**。
> **著作権注意：固有名・文章・キャラは不使用、メカニクス/社会構造のみ参考。**

> **EPIC = #1402**。GitHub issue 起票済み（#1403〜#1418）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **RMK-1** | #1403 | 累積戦闘疲弊（`CombatFatigue`・会戦をまたぐ持続的士気劣化） | `FleetMorale.GetMoraleFactor()`×疲弊係数 / `BattleHandoff` 持ち越し |
| **RMK-2** | #1405 | 戦友紐帯（`KameradschaftRules`・一次集団凝集ボーナスと崩壊） | `CombatModifiers`×絆ボーナス / `FleetStrength.TakeDamage` で更新 |
| **RMK-3** | #1408 | 膠着戦況（`StalemateRules`・拮抗=第3の戦闘結果） | `StrategyRules.ResolveCorridorBattle` 拡張 / `WarGoalRules.WarWeariness` 加算 |
| **RMK-4** | #1412 | 前線-後方情報非対称（`HomeFrontRules`・プロパガンダ格差） | `FleetMorale`×`ConsentRules`×`TrinitarianTensionRules`（CLZ-3）×`EventEngine` |
| **RMK-5** | #1416 | 世代断絶（`GenerationalWoundRules`・失われた世代の指導者欠乏） | `DemographicsRules`×`CareerPipelineRules`×`AnnualLifecycleRules` |
| **RMK-6** | #1418 | 帰還兵の厭戦伝播（`ReturneesContagionRules`・後方希望の侵食） | `CombatFatigueRules`（RMK-1）×`HomeFrontRules`（RMK-4）×`HopeRules` |

### 推奨着手順
`RMK-1`（疲弊＝最も固有で欠落の大きいsignature）→ `RMK-2`（戦友紐帯＝レマルクの核心）→ `RMK-3`（膠着＝戦闘解決の構造欠落）→ `RMK-4`（前線-後方非対称＝CLZ-3への接続）→ `RMK-5`（世代断絶＝長期マクロ帰結）→ `RMK-6`（帰還兵伝播＝RMK-1/4への橋渡し）。

> いずれも既存の `FleetMorale`/`WarGoalRules`/`DemographicsRules` を**後退させず接続**する additive 設計。消耗戦シナリオ・長期多会戦キャンペーン・戦略-戦術連携に最も効く。
