# マアルーフ『アラブが見た十字軍』参考設計（EPIC #ARAB）

> 参照元：アミン・マアルーフ著『アラブが見た十字軍』。7世紀にわたるフランク人（十字軍）の東方進出を、**アラブ側の一次史料・年代記**から再構成した歴史叙述。
> 視点の反転——「文明の衝突」を「敗者・被占領者」の目で描き、政治的分裂・宗教的動員・占領下の持続と奪還・文化的混淆という普遍的パターンを浮かび上がらせる。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略＋既に大量の社会・政治純ロジック層）にとって**役に立つ構造パターン**だけを抽出し、EPIC `#ARAB` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用しない。**歴史メカニクス／世界観の構造パターンのみ**を参考にする。

---

## 0. なぜ本書が本システムに役立つか

当プロジェクトは社会・政治の**純ロジック層を広く保有**している（[CLAUDE.md] 参照）：

| 既存（カバー範囲） | カバー内容 |
|---|---|
| `LoyaltyRules` / `Allegiance` (#817) | 個人レベルの旗幟・寝返りカスケード（関ヶ原型） |
| `ReligionRules.HolyWarPressure` (#172-175) | 聖戦圧力・改宗圧力・異端 |
| `CultureRules.AssimilationPressure` (#194) | 占領→住民が占領者文化へ同化する圧力 |
| `ConsentRules` / `Polity` (#836-838) | 統治への合意・協力撤回（非暴力） |
| `GovernanceRules` / `Province` (#109) | 占領統合度・安定度・産出 |
| `FeudalRules` / `Fief` (#168-169) | 封建徴兵・陪臣反乱リスク |
| `EspionageRules` / `SpyNetwork` | 諜報・妨害・発覚リスク |
| `DiplomacyRules` / `DiplomacyState` (#189) | 外交状態・友好度・条約 |
| `Organization` / `SuccessionRules` (#812-814) | 英雄死後の組織存続・カリスマの日常化 |
| `WarGoalRules` / `CasusBelli` (#192) | 厭戦・講和受諾条件 |

**しかし、本書が固有に示す以下の構造パターンが欠けている**：

| 本書が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **分裂した主権者群が外的実存脅威にも集団防衛を形成できない** | `LoyaltyRules` は個人旗幟。**複数主権体が脅威下で協調失敗するゲーム構造**（囚人のジレンマ×連合形成）が無い |
| **宗教的権威による政治統合（聖戦型動員）** | `ReligionRules.HolyWarPressure` は改宗圧力。**政治的分裂を一時的に超越する宗教的動員の政軍効果**（連合形成倍率）が無い |
| **占領下の潜在的抵抗蓄積と奪還動力** | `Province.integration` は占領統合を表すが、**被占領期間の長さが抵抗潜在量を蓄え、統一的指導者出現時に結晶化する**メカニクスが無い |
| **接触地帯での逆方向文化混淆（占領者が現地化する）** | `CultureRules.AssimilationPressure` は被占領者→占領者文化への同化。**長期占領で占領者が現地文化に融合する逆圧力**（フールーン現象）が無い |
| **政治暗殺の戦略的組織化** | `EspionageRules` は情報収集・妨害。**標的暗殺を組織的手段として戦略に組み込む**（ハサン・サッバーフ型の暗殺教団）ルールが無い |

**結論**：本書は当プロジェクトの政治・軍事ロジックに**①集団防衛の調整失敗 ②宗教的政治統合 ③占領下潜在抵抗 ④接触地帯の逆混淆 ⑤政治暗殺の戦略化**という5つの欠落軸を与える。特に①②は**「なぜ圧倒的多数の側が負けるのか」「どう反転するのか」**という戦略ゲームの核心に直結する。

---

## 1. 役に立つ視点（要約）

本書の世界観を、**本システムに効く形**で1行ずつ：

1. **分裂した主権者群は外敵に個別撃破される**——各勢力が局所的利益を優先し、合計兵力は敵を上回るのに連合を形成できない。→ 既存の `LoyaltyRules`（個人旗幟）に**主権体レベルの調整失敗**という上位問題を加える。
2. **宗教的権威は分裂を一時的に超越できる**——法学者・カリフの聖戦宣布が政治的計算を上書きし、連合を強制形成する（サラディン登場）。→ `ReligionRules.HolyWarPressure` に**集団行動の制約解除倍率**を接続する。
3. **占領は根絶しない——潜在抵抗は時間で蓄積する**——200年の占領後も住民思想は残り、統一的指導者が現れた瞬間に抵抗が結晶化した。→ `Province.nativeIdeology` × 占領期間 → 奪還動力の純ロジック。
4. **占領者は長期滞在で現地化する**——現地生まれの「フールーン」（フランク2世）は現地語を話し文化を吸収した。→ `CultureRules` の**逆方向同化圧力**。
5. **暗殺は組織戦略である**——ハサン・サッバーフ率いるイスマーイール派は標的暗殺を政治手段として体系化し、数十年にわたって地域政治を変形した。→ `EspionageRules` を拡張する**政治暗殺の戦略的使用**ルール。
6. **「文明の衝突」は実は「無知と接触と驚き」の連鎖**——アラブ側は十字軍を蛮族と蔑みつつ医療・建築を学んだ。→ `DisclosureLedger`（FND-4）へのloreデータ入力。視点反転の秘史開示。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`LoyaltyRules`/`ReligionRules`/`CultureRules`/`GovernanceRules`/`EspionageRules` を作り直さない**。ARAB はそれらに**欠落軸を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・本書の signature）

#### ARAB 集団防衛調整失敗（`CoalitionDefenseRules`）
- **問題の構造**：複数の主権的勢力（非同盟）が共通の外的脅威を受けるとき、各勢力は「自分が先に降伏/協力すれば標的を免れる」という囚人のジレンマに直面し、**連合を形成できないまま個別撃破**される。
- **純ロジック**（新規 `CoalitionDefenseRules` static）：
  - `ThreatLevel(threat, defenders)` — 脅威強度と防衛側合計兵力の比
  - `CoalitionFormationChance(fragmentation, sharedThreat)` — 分裂度×脅威共有度 → 連合成立確率
  - `DefectorIncentive(isolatedStrength, coalitionSize)` — 単独降伏の誘因（連合が大きいほど一抜けが合理的）
  - `EffectiveDefenseStrength(coalitionStrength, formationChance)` — 実効防衛力（連合確率×合計）
- 接続：`FactionRelations.IsHostile`×`DiplomacyRules`（共通脅威を検出）×`StrategyRules.FindEncounters`（多勢力会戦の前段）×`LoyaltyRules.ResolveWinner`（連合崩壊後の個別旗幟へ）。
- test-first：EditMode/TestHarness で `ThreatLevel` / `CoalitionFormationChance` の境界条件を固定。

#### ARAB 聖戦型動員（`SacralMobilizationRules`）
- **問題の構造**：宗教的権威（カリフ・法学者）が「聖戦」を宣布することで、`CoalitionDefenseRules.DefectorIncentive` を一時的に上書きし、分裂した主権者群を強制的に連合へ押し込む。
- **純ロジック**（新規 `SacralMobilizationRules` static）：
  - `MobilizationMultiplier(holyWarPressure, legitimacy, fragmentation)` — 聖戦圧力×宗教的正統性×分裂度 → 実効連合形成倍率（1.0〜上限 `MobilizationCap`）
  - `MobilizationDecay(turns)` — 宣布後の時間経過で倍率が基準に戻る（聖戦疲弊）
  - `TriggerCondition(holyWarPressure)` — 閾値を超えると強制連合（`LoyaltyRules.ResolveCascade` の宗教版）
- 接続：`ReligionRules.HolyWarPressure`（入力）×`CoalitionDefenseRules.CoalitionFormationChance`（倍率適用）×`Organization.cohesion`（制度化が持続時間を延ばす）。
- test-first：EditMode で `MobilizationMultiplier` 境界・`MobilizationDecay` 収束を固定。

### ★★ 高（占領の動態・文化の逆圧力）

#### ARAB 占領下潜在抵抗蓄積（`OccupationResistanceRules`）
- `Province.integration` は「占領者が統合を進める」ゲージだが、**被占領側の抵抗潜在量**が無い。
- **純ロジック追加**（`OccupationResistanceRules` static）：
  - `AccumulateResistance(nativeIdeology, ownerIdeology, occupationTurns)` — 思想差×期間 → 抵抗潜在量（`resistancePotential` 0..1）
  - `CrystallizationBonus(resistancePotential, unificationEvent)` — 統一的指導者/聖戦宣布 → 抵抗が速攻で戦力変換（奪還動力）
  - `ResistanceSuppressionCost(resistancePotential)` — 抵抗潜在が高いほど統治コスト増（`GovernanceRules.Tick` の安定度減衰に係数）
- 接続：`Province.nativeIdeology`×`GovernanceRules.IdeologyModifier`×`SacralMobilizationRules.TriggerCondition`（結晶化トリガー）。
- test-first：EditMode で蓄積・結晶化境界を固定。

#### ARAB 接触地帯の逆方向文化混淆（`HybridizationRules`）
- `CultureRules.AssimilationPressure` は被占領者→占領者方向のみ。**長期占領で占領者が現地文化に吸収される逆圧力**が無い。
- **純ロジック追加**（`HybridizationRules` static・`CultureRules` の姉妹）：
  - `OccupierDriftRate(occupationTurns, intermarriageRate, isolation)` — 占領年数×婚姻率×孤立度 → 占領者文化の現地化速度
  - `HybridCulture(occupierCulture, localCulture, driftRate)` — 混淆文化値（0=純占領者〜1=完全現地化）
  - `LoyaltyShift(hybridCulture)` — 現地化が進むと宗主国への忠誠が低下（FeudalRules.VassalRebellionRisk 係数）
- 接続：`CultureRules`×`FeudalRules.VassalRebellionRisk`×`GovernanceRules`（混淆文化は統治コスト低下・分離独立リスク上昇の両面）。
- test-first：EditMode で `OccupierDriftRate` × `LoyaltyShift` の境界を固定。

### ★ 中（暗殺戦略・lore）

#### ARAB 政治暗殺の戦略的使用（`AssassinationRules`）
- `EspionageRules` は情報収集・妨害・発覚リスクを扱うが、**標的暗殺を組織的に体系化する**ルールが無い。
- **純ロジック追加**（`AssassinationRules` static）：
  - `SuccessChance(networkStrength, targetGuard, roll)` — ネットワーク強度×防護 → 成功確率（決定論roll）
  - `PoliticalImpact(target, successChance)` — 標的の役職・カリスマ度 → 成功時の政治衝撃（`VacancyRules` 連鎖・`Organization.charisma` 低下）
  - `RetaliationRisk(successChance, exposure)` — 発覚で報復＝`DiplomacyRules.DeclareWar` トリガー
  - `NetworkBuildCost(targetTier)` — 高位標的ほど構築コスト大（戦略的決断を要する）
- 接続：`EspionageRules`（ネットワーク強度を共有）×`VacancyRules.FillVacancy`（空席補充）×`SuccessionRules.ResolveSuccession`（カリスマ断絶）×`CaptivityRules`（失敗=捕虜）。
- test-first：EditMode で `SuccessChance` / `PoliticalImpact` 境界を固定。

#### ARAB（lore）世界観の開示データ：占領された側の記憶と文明
- 「文明の衝突」は「無知と接触と驚き」——蛮族と侮った側が先進医療・建築を学び、被占領者は宗教と文化で生き延びた。
- 「200年の占領は根絶できない」——征服はゴールではなく、抵抗の始まり。
- 「視点反転の真実」——同一の戦争が、勝者と敗者で全く異なる歴史として残る（秘史FND-4の両面記述）。
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への**lore データ入力**。秘史開示の「視点の反転」カテゴリへ。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 新たな外交状態・講和条件 | **`DiplomacyRules`/`WarGoalRules` がカバー** |
| 十字軍自体の軍事組織・騎士団 | **`FleetRoster`/`OrderOfBattle`/`MilitaryFormation` がカバー**。新勢力はアセット追加のみ |
| 宗教改革・聖地巡礼の経済 | **`ReligionRules`/`MarketRules` で接続可能**。新EPIC化しない |
| 包囲攻城戦の追加戦術詳細 | **`PlanetSiegeRules`/`SiegeArena` がカバー**（惑星攻城=城塞攻略の構造同型） |
| 人種差別・奴隷制度の新ルール | 本プロジェクトの倫理方針外。`CaptivityRules` で最低限カバー済み |

---

## 3. EPIC #ARAB の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。
> 既存政治・宗教・文化ロジックは**接続のみ・重複新設しない**（additive）。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/構造パターンのみ**参考。

> **EPIC = #2184**。GitHub issue 起票済み（#2185〜#2197）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **ARAB-1** | #2185 | 集団防衛調整失敗（`CoalitionDefenseRules`：分裂×脅威→連合失敗の純ロジック） | `FactionRelations`×`DiplomacyRules`×`StrategyRules`。test-first |
| **ARAB-2** | #2187 | 聖戦型動員（`SacralMobilizationRules`：宗教権威→分裂超越→連合形成倍率） | `ReligionRules.HolyWarPressure`×ARAB-1×`Organization` |
| **ARAB-3** | #2188 | 占領下潜在抵抗蓄積（`OccupationResistanceRules`：思想差×期間→抵抗潜在→奪還動力） | `Province.nativeIdeology`×`GovernanceRules`×ARAB-2結晶化 |
| **ARAB-4** | #2191 | 接触地帯の逆方向文化混淆（`HybridizationRules`：占領者が現地化する逆同化圧力） | `CultureRules`×`FeudalRules.VassalRebellionRisk` |
| **ARAB-5** | #2194 | 政治暗殺の戦略的使用（`AssassinationRules`：標的暗殺→空席→組織崩壊） | `EspionageRules`×`VacancyRules`×`SuccessionRules` |
| **ARAB-6** | #2197 | （lore）開示データ：占領された側の記憶・視点反転・文明の衝突の両面性 | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`ARAB-1 → ARAB-2`（集団防衛失敗＋聖戦型動員＝本書の最大signature・戦略ゲームの核心）→ `ARAB-3`（占領潜在抵抗＝Province に奥行きを足す）→ `ARAB-4`（逆混淆＝CultureRules 拡張）→ `ARAB-5`（暗殺＝EspionageRules 拡張）→ `ARAB-6`（lore 入力・コードなし）。

> いずれも既存政治・軍事・文化ロジックを**後退させず接続**する additive 設計。多勢力戦略（3勢力以上の会戦/割拠）に最も効く。
