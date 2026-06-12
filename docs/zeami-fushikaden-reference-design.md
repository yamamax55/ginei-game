# 世阿弥『風姿花伝』参考設計（EPIC #ZEAM）

> 参照元：世阿弥元清『風姿花伝』（応永年間・1400年頃成立）。能楽の奥義書——花（芸の美・感動の核）をいかに育て、いかに伝え、いかに時に応じて変えるかを論じた最初の体系的芸術論。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略＋すでに大規模な人物成長・軍事ロジック層）にとって**役に立つ視点**だけを抽出し、EPIC `#ZEAM` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**芸論の構造パターン・成長論・戦術論のみ**を参考にする。

---

## 0. なぜ「風姿花伝」が本システムに役立つか

当プロジェクトは人物成長・生涯管理の**純ロジック層を大量に保有**している：

| 既存（人物成長・生涯） | カバー範囲 |
|---|---|
| `GrowthRules`/`Growth`（#537-543） | 経験→実効能力。4アーキタイプの成長曲線 |
| `RetirementRules`＋`ServiceStatus`（#530-536） | 停年・アップオアアウト・戦時召集 |
| `LifecycleRules`/`Calendar`（LIFE-1/2） | 年齢・死亡・没年 |
| `SeniorityRules`（LIFE-5/6） | 席次主義 vs 実力主義・政体依存の硬直度 |
| `CareerPipelineRules`/`CareerTrack`（LIFE-5/6/7） | 出自経路（士官/科挙/技官）・学閥CliqueBond |
| `AdmiralSkillRules`/`AdmiralSkill`（#137-140） | パッシブスキル・条件付き修正子 |
| `PersonRules`/`Person`（#866） | 軍才/文才・適材適所・役職適性 |

**しかし、これらは「能力値の成長・停年・出自」という量的・制度的フレームだけ**であり、風姿花伝が固有に描く以下が**欠けている**：

| 風姿花伝が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **秘すれば花**——全力を見せないことが驚きを生む | `CombatModifiers` に温存・奇襲の正式な乗数がない。AdmiralSkill は常時/条件付きだが「未見の力」を積む機構がない |
| **花の境目**——最盛期は成長曲線の一点ではなく**有界な窓**として存在する | `GrowthRules` は単調増加〜プラトー。「盛期ウィンドウ」（盛期中はリーダーシップ増幅・その前後は異なる質の花）がない |
| **時分の花 vs まことの花**——若さの花（一過性の輝き）と錬磨の花（耐久性ある熟達）は**質が異なる** | `GrowthRules` は量（実効値）だけ。花の種別（一過性 vs 耐久）という質的区別がない |
| **九品の階梯**——能力の高低を9段階の**熟達度**として定性的に束ねる | 能力は数値ベクトル。「熟達度スコア」という合成的・段階的評価指標がない |
| **師の伝**——師匠の花が弟子へ加速的に伝わる（経験速度の乗算・伝承連鎖） | `CareerPipelineRules` の `CliqueBond` は同窓結束のみ。**師匠→弟子の成長加速**という技伝承チェーンがない |

**結論**：風姿花伝は当プロジェクトの人物成長層に**①温存奇襲 ②盛期ウィンドウ ③花の質的区別 ④熟達度スコア ⑤師伝加速**という5つの欠落軸を与える。「量的成長」に**質的・時間的・戦略的な厚み**を足し、英雄の輝きを単なる能力値以上のものにする。

---

## 1. 役に立つ視点（要約）

風姿花伝の芸論を、**本システムに効く形**で1行ずつ：

1. **「秘すれば花」**——全力を見せずに温存することが「花（驚き・感動）」を生む。→ 会戦で温存した艦隊の**奇襲乗数**（`ReserveCapabilityRules`）。提督は常に全力を晒してはならない。
2. **花の境目**——最盛期は成長曲線の延長ではなく、始まり・中頂・終わりがある**有界な窓**。→ `BloomWindowRules`：盛期ウィンドウ中はリーダーシップに加算あり、前後は別の質（可能性 vs 深み）に移行。
3. **時分の花 vs まことの花**——若い頃の花は一時的な輝き（誰が見ても目を引く）、錬磨の花は持続力と安定感を持つ。→ 提督の「花の種別」が士気・従属者の忠誠の引き出し方を変える。
4. **九品の階梯**——上中下×上中下の9段階で芸を俯瞰評価する。→ 単一の熟達度スコアへ束ねて継承・任命・戦記の評価軸に。
5. **師の伝の連鎖**——師匠の花を正しく受け継ぐ弟子は成長が加速する。→ メンター関係が`GrowthRules`の経験効率を乗算。師の死後も「伝えた花」は残る（カリスマの日常化#812と共鳴）。
6. **時・場に応じた花**——同じ花でも時と場が違えば「花」にならない。→ `AdmiralData.IsPreferredFormation`（既存）の拡張として**状況適応**の発想。既存接続。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`GrowthRules`/`LifecycleRules`/`AdmiralSkillRules`/`CareerPipelineRules` を作り直さない**。ZEAM はそれらに**欠落軸を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・風姿花伝の signature）

#### ZEAM 秘すれば花 = 温存奇襲ボーナス（`ReserveCapabilityRules`）
- **温存状態の定式化**：艦隊が「全力攻撃を一度も使っていない（温存中）」状態を `ReserveState` で管理。全力投入時に`surpriseMultiplier`（既定1.25）が初回攻撃に乗る。
- **温存の消費**：全力投入・手動攻撃目標指定・一定連続交戦で温存解除。再温存には`cooldownTurns`（時間経過 or 退却）が要る。
- 接続：`CombatModifiers`（`ModifierStack` で積む）×`FleetStrength.IsFighting`×`FleetWeapon`。純ロジック new + EditMode テスト必須。

#### ZEAM 花の境目 = 盛期ウィンドウ（`BloomWindowRules`）
- **盛期の計算**：`Person.birthYear` + キャリア進行（`GrowthRules` の蓄積経験値）→ 盛期ウィンドウ判定 `IsInBloom(person, currentYear)`。
- **盛期ボーナス**：盛期中は `EffectiveLeadership` に `bloomLeadershipBonus`（既定+10%）を加算。盛期前は「可能性値（potential）」として成長加速に使用。盛期後は「深みボーナス（depthBonus）」として副提督/参謀への助言効果に転化。
- 接続：`GrowthRules`（archetypeごとに盛期ウィンドウ幅が異なる）×`AdmiralData.EffectiveLeadership`×`CommandStaffRules`（助言効果）。純ロジック new + EditMode テスト必須。

### ★★ 高（質的区別・段階的評価）

#### ZEAM 時分の花 vs まことの花 = 花の種別（`FlowerTypeRules`）
- **花の種別判定**：`FlowerType { 時分の花, まことの花, 花なし }` enum ＋ `ResolveFlowerType(person, currentYear)` 純関数。
  - 時分の花：若さの花（盛期前半＋高カリスマ）→ 士気への直接インパクト大だが持続しない。
  - まことの花：錬磨の花（盛期後半〜引退前）→ 従属者の忠誠・副提督補佐に安定的ボーナス。
- 接続：`BloomWindowRules`（ZEAM-2）×`FleetMorale`×`CommandStaffRules`。純ロジック new + EditMode テスト必須。

#### ZEAM 九品熟達度 = 9段階熟達度スコア（`MasteryRules`）
- **熟達度合成**：`MasteryScore(person)` → 能力値ベクトル + 盛期状態 + 花の種別 + キャリア年数 → `MasteryTier { 下々品…上々品 }`（9段階 enum）。
- **用途**：継承選定（`VacancyRules`）・任命台帳（`GovernmentRegistry`）への優先度付与・戦記の定性評価表示。
- 接続：`GrowthRules` × `BloomWindowRules`（ZEAM-2） × `PersonRules` × `VacancyRules`（LIFE-2）。純ロジック new + EditMode テスト必須。

### ★ 中（師伝連鎖・世界観lore）

#### ZEAM 師伝の連鎖 = メンター成長加速（`MentorshipRules`）
- **メンター関係の設定**：`MentorBond { mentorId, discipleId, bondStrength }` 純データ。
- **成長加速**：師のMasteryTierが高いほど弟子の `GrowthRules.GainExperience` の効率が上がる（`mentorEfficiencyFactor`）。師の死後もbondStrengthの一定割合が残る（「伝えた花」）。
- 接続：`GrowthRules` × `MasteryRules`（ZEAM-4） × `LifecycleRules`（死亡で bond が減衰）× `Organization`（#812 カリスマの日常化と共鳴）。純ロジック new + EditMode テスト必須。

#### ZEAM（lore）世界観の開示データ
- 「秘すれば花——見せすぎた英雄は驚きを失う」「時分の花——若さの輝きを永遠と思うな」「まことの花——積み上げた熟達のみが時代を超える」「師伝——英雄は死んでも弟子の中に生き続ける」。
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への**loreデータ入力**。CCX-6（世界観codex退避）方針に一貫。

### ❌ 不採用（重複・既存で十分・適用不可）

| 不採用 | 理由 |
|---|---|
| 能の9つの芸風類型（幽玄・荒事 etc.）の専用データ | 芸術スタイルの固有分類。`Formation`/`AdmiralSkill`/`ShipClass` で代替可 |
| 陰陽・序破急の演じ分けを新モジュール化 | `CombatModifiers`+`FleetMovement` の戦術リズムが近似。重複新設しない |
| 客席・観客管理（誰に見せるか） | UIレイヤー。純ロジックとして独立させない |
| 装束・仮面の視覚分類 | ゲームの艦艇アートには非適用 |
| 稽古の段階（初心・中心・老心）を独立システム化 | `GrowthRules`+`BloomWindowRules`（ZEAM-2）+`MasteryRules`（ZEAM-4）で十分。別ループを増やさない |

---

## 3. EPIC #ZEAM の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。既存成長ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**芸論の構造パターンのみ**参考。

> **EPIC = #1898**。GitHub issue 起票済み（#1899〜#1913）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **ZEAM-1** | #1899 | 秘すれば花 = 温存奇襲ボーナス（`ReserveCapabilityRules`/`ReserveState`） | `CombatModifiers`×`FleetWeapon`×`FleetStrength`。温存→全力投入で surpriseMultiplier |
| **ZEAM-2** | #1901 | 花の境目 = 盛期ウィンドウ（`BloomWindowRules`・`IsInBloom`/`BloomPhase`） | `GrowthRules`×`AdmiralData.EffectiveLeadership`×`CommandStaffRules`。盛期中ボーナス・後期は助言転化 |
| **ZEAM-3** | #1904 | 時分の花 vs まことの花 = 花の種別（`FlowerTypeRules`/`FlowerType`） | `BloomWindowRules`×`FleetMorale`×`CommandStaffRules`。種別により士気/忠誠への影響が異なる |
| **ZEAM-4** | #1906 | 九品熟達度 = 9段階熟達スコア（`MasteryRules`/`MasteryTier`） | `GrowthRules`×`BloomWindowRules`×`VacancyRules`×`GovernmentRegistry`。継承・任命の優先度 |
| **ZEAM-5** | #1910 | 師伝の連鎖 = メンター成長加速（`MentorshipRules`/`MentorBond`） | `GrowthRules`×`MasteryRules`×`LifecycleRules`×`Organization`。師の死後も「伝えた花」が残る |
| **ZEAM-6** | #1913 | （lore）世界観の開示データ（秘すれば花・時分の花・まことの花・師伝） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`ZEAM-1`（温存奇襲＝最も固有で即会戦に効く signature）→ `ZEAM-2`（盛期ウィンドウ＝成長層への骨格）→ `ZEAM-3`（花の種別＝ZEAM-2依存）→ `ZEAM-4`（九品熟達度＝ZEAM-2/3を束ねる合成）→ `ZEAM-5`（師伝連鎖＝ZEAM-4依存）→ `ZEAM-6`（lore・コード不要・随時）。

> いずれも既存成長・戦闘ロジックを**後退させず接続**する additive 設計。英雄の輝きが「いつ・どう咲くか」という時間軸を与える。
