# カード『エンダーのゲーム』参考設計（EPIC #ENDR）

> 参照元：オースン・スコット・カード『エンダーのゲーム』（Ender's Game）。地球の危機に備え、軍事天才児を育成する**バトルスクール**と、「自分は演習をしていると思いながら実は本物の艦隊戦を指揮していた」という核心的ねじれを描く SF。
> 本ドキュメントは、当プロジェクト（Ginei）に対して**役に立つメカニクスの構造パターンのみ**を抽出し、EPIC `#ENDR` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は一切流用しない。**軍事教育・指揮情報・戦術論の構造パターンのみ**参考にする。

---

## 0. なぜ「エンダーのゲーム」が本システムに役立つか

当プロジェクトは指揮・戦術・成長の**軍事ロジック層を大量に保有**している：

| 既存（カバー範囲） | カバーしている内容 |
|---|---|
| `GrowthRules/Growth`（#537-543 Wave1） | 経験→実効能力。4アーキタイプ（首席/在野俊英/老練/叩き上げ）の成長曲線 |
| `AutoBattleSim`（TIME-4） | Lanchester二乗則で自動解決。所要 game-time を返す |
| `CareerPipelineRules`（LIFE-5/6/7 #155-157） | 士官学校/科挙/有力者/テクノクラートの出自経路 |
| `AutonomyRules`＋`CommandDoctrine`（Wave1 #544-550） | 集団依存/自律分散の2極。創発シナジー・傑物前提 |
| `DeceptionRules`（SUN-1 孫子） | 欺瞞作戦・偽情報・陽動 |
| `AdmiralSkillRules/AdmiralSkill`（Wave1 #137-140） | 提督パッシブスキル・条件別修正子 |
| `CombatModifiers`/`ModifierStack`（#106） | 戦闘係数の合成窓口 |
| `DisclosureLedger`（FND-4 #495） | 条件付き秘史開示の連鎖 |
| `EspionageRules`/`SpyNetwork` | 対外諜報：情報収集・妨害 |

**しかし、以下の軸が欠落している**：

| エンダーのゲームが固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **演習の蓄積が実戦能力を育てる（自動解決の地続き性）** | `AutoBattleSim` は解決するが、その会戦結果が指揮官の `GrowthRules` へ供給される経路がない |
| **上位司令部が前線指揮官に「本当のことを言わない」情報フィルタリング** | `EspionageRules` は対外諜報。**組織内の縦方向情報遮断**（上位→下位の意図的な情報操作）がない |
| **慣習的戦術前提の転換が一時的な圧倒的優位を生む（「門が下にある」型フレームシフト）** | `CommandDoctrine{集団依存/自律分散}` は指揮スタイル。**新戦術パラダイムの「発見→習熟→敵の適応」サイクル**がない |
| **集合意識型指揮の完全同期ボーナス＋中枢崩壊による一括瓦解** | `CommandDoctrine` の2極は人間型指揮モデル。**旗艦撃破=全艦隊即崩壊という非人間型の壊滅モデル**がない |

**結論**：エンダーのゲームは当プロジェクトの軍事層に**4つの欠落軸**を与える：
①演習→実戦能力移転、②指揮情報フィルタリング、③戦術フレームシフト、④集合意識型指揮の脆弱性。
そして `AutoBattleSim`/`GrowthRules`/`CommandDoctrine` という**3つの既存システムに橋を架ける connector**として最も効く。

---

## 1. 役に立つ視点（要約）

エンダーのゲームの世界観を、**本システムに効く形**で1行ずつ：

1. **演習と実戦の境界が消える**——「勝つための訓練」は本物の戦争そのものだった。→ `AutoBattleSim`（自動解決）を「訓練累積の源泉」と接続する機会。**会戦を重ねるほど提督が育つ**サイクルを補完。
2. **情報は権力の道具**——高位者は前線指揮官に「演習と思わせたまま」本物の命令を出す。→ `EspionageRules` の**縦方向版**として「上からの情報操作」が既存諜報層に欠ける穴を埋める。
3. **「門が下にある」——常識を疑った瞬間が圧勝を生む**。戦術前提を転換した側が慣習に縛られた側を圧倒する。→ `CombatModifiers` × `AutonomyRules` に「認識パラダイムシフト係数」を追加する着火点。
4. **集合意識は最強かつ最脆弱**——完全同期の集団指揮はゼロ遅延の完璧な協調を生むが、中枢消滅で全崩壊する。→ `CommandDoctrine` に第3の型を追加し、`SuccessionRules`（継承不能）との接続を明確化。
5. **天才を育てる残酷な設計**——意図的な孤立・過酷条件・非公平なルールが逆境適応力を強制的に高める。→ `GrowthRules` の「逆境成長係数」として欠けている4番目のパラメータ。`CareerPipelineRules` の士官学校経路（#155）を実効的に機能させる。
6. **勝利の後に残るもの**——「最大の敵を殺した者は、殺した相手を最もよく理解する」。後日になって真実が開示される。→ `DisclosureLedger` への lore 入力（コード新設なし）。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`GrowthRules`/`AutoBattleSim`/`CommandDoctrine`/`EspionageRules` を作り直さない**。ENDR はそれらに**欠落の橋**を架けるだけ（additive）。

### ★★★ 最優先（真の欠落・エンダーのゲームの signature）

#### ENDR 演習→実戦能力移転（TrainingCycleRules）
- **`TrainingCycleRules`（新・純ロジック）**：会戦（自動解決＋実会戦）の結果を `GrowthRules.GainExperience` に供給する橋。
  - `ExperienceYield(simResult, repetitions, isRealBattle)` → 実会戦は演習より多く経験を与える（逆説：死線が最大の師）
  - `TrainingFatigue(repetitions)` → 繰り返しの逓減率（同じ演習を100回しても成長は上限に達する）
  - `AdversarialBonus(trainingDifficulty)` → 意図的に不利な状況で訓練すると成長係数が上昇
- 接続：`AutoBattleSim`（TIME-4・解決器）× `GrowthRules`（Wave1#537・成長器）× `AdmiralData.EffectiveXxx`（実効値）
- **`AutoBattleSim` は「戦歴」を返すが `GrowthRules` には届いていない**——この橋がないと提督は何百回自動解決しても育たない。

#### ENDR 指揮情報フィルタリング（CommandIntelFilterRules）
- **`CommandIntelFilterRules`（新・純ロジック）**：組織内の縦方向情報遮断——上位司令部が前線指揮官に開示する情報の「フィルタレベル」。
  - `InfoFilterLevel { 完全開示, 部分開示, 隠蔽 }` （enum）
  - `FilteredIntelQuality(fullInfo, filterLevel, admiralIntelligence)` → 前線指揮官が認識できる情報品質（0..1）
  - `IntelGapEffect(perceivedSituation, actualSituation)` → 認識ギャップが戦闘判断精度に与える係数
  - `RevealThreshold(gapMagnitude)` → ギャップが閾値を超えると `EventEngine` 経由でイベント発火（「演習だと思っていたら実戦だった」型）
- 接続：`EspionageRules`（対外諜報の縦方向版）× `NotificationCenter`（NOTIF-1）× `EventEngine`（#116）× `GovernmentRegistry`（GOV-1）
- **`EspionageRules` は「敵から」情報を得る。`CommandIntelFilterRules` は「味方の上位から」情報を隠す**——方向が逆の別系統。

### ★★ 高優先（既存層を有意に拡張）

#### ENDR 戦術フレームシフト（TacticalParadigmRules）
- **`TacticalParadigmRules`（新・純ロジック）**：「門が下にある」型の戦術パラダイム転換——慣習的前提を疑った側が一時的優位を得る。
  - `TacticalParadigm { 正規教義, フレームシフト済, 対応済 }` （enum）
  - `ParadigmAdvantage(myParadigm, enemyParadigm)` → 自軍シフト済×敵軍未対応 → ボーナス倍率（ピーク）、敵が対応すると収束
  - `AdaptationProgress(enemyIntelligence, paradigmAge)` → 時間経過と敵 intelligence で対応が進む（イノベーター優位の消滅）
  - `DiscoveryChance(admiralMobility, adversityLevel)` → 逆境 × 高機動提督ほど転換を発見しやすい
- 接続：`CombatModifiers`/`ModifierStack`（#106・係数合成）× `AutonomyRules.CommandDoctrine`（Wave1）× `AdmiralSkillRules`（Wave1#137）
- **`DeceptionRules`（SUN-1）は敵の行動を騙す。`TacticalParadigmRules` は自軍の認識フレームを変える**——どちらも「見え方の操作」だが方向が違う別系統。

#### ENDR 集合意識型指揮の脆弱性（CollectiveMindRules）
- **`CollectiveMindRules`（新・純ロジック）**：人間型指揮の対極——完全同期の集合意識型指揮。ゼロ遅延協調ボーナス × 中枢消滅で全崩壊。
  - `CommandDoctrine` に第3の型 `集合意識型` を追加
  - `CollectiveSyncBonus(doctrine, intactRatio)` → 全艦残存時に最大ボーナス（人間指揮を超える協調）
  - `CoreCollapseRisk(flagshipHealth)` → 旗艦（中枢）が瀕死になると全艦機能停止リスクが上昇
  - `IsSuccessionPossible(doctrine)` → `集合意識型` は継承不能（`SuccessionRules.PromoteVice` が機能しない）
- 接続：`CommandDoctrine`（Wave1 #544 拡張）× `SuccessionRules`（#812・継承）× `BattleAllegianceManager`（#817・戦況変化）
- **既存 `CommandDoctrine` は人間型の2極（集団依存/自律分散）。`集合意識型` は「非人間型の第3極」**。継承が機能しない点で `SuccessionRules` の境界条件を明示する。

### ★ 中優先（世界観 lore・コード新設なし）

#### ENDR （lore）「勝利の代償」後日開示（Speaker for the Dead）
- コード新設なし。`DisclosureLedger`（FND-4）への lore データ入力のみ。
  - 「演習と思っていた最終決戦は実戦だった」（秘史カテゴリ）
  - 「敵を完全に理解した時、殺した相手への悲しみが生まれる」（真相カテゴリ）
  - 「勝利の英雄は最大の加害者だった」→ `Community.Hope` の遅延低下イベント接続
- 接続：`DisclosureLedger`（FND-4）× `NotificationCenter`（NOTIF-1）× `Community.Hope`（#852）

### ❌ 不採用（重複・既存で十分・タイクン化リスク）

| 不採用 | 理由 |
|---|---|
| バトルスクールの物理（無重力戦闘訓練・チーム割り当て） | タイクン化回避（訓練マイクロを増やさない）。`CareerPipelineRules` 士官学校経路（#155）で十分 |
| 提督ランキング・点数表示 | 既存 `meritScore`（`FleetUnitData`）＋`AdmiralSkillRules` でカバー |
| 宇宙通信遅延（Ansible 遮断） | `FrictionRules`（CLZ-1 クラウゼヴィッツ）が作戦摩擦として接続済み |
| 艦隊の3次元戦術表示 | 当システムは2D確定（CLAUDE.md 絶対規約）。不採用 |
| 異種族外交（Speaker for the Dead 外交ルート） | `DiplomacyRules`（#189）＋`DisclosureLedger` で対応可能。ENDR-5 の lore として足りる |

---

## 3. EPIC #ENDR の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。既存軍事ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #2341**。GitHub issue 起票済み（#2347, #2352, #2356, #2359, #2362）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **ENDR-1** | #2347 | 演習→実戦能力移転（TrainingCycleRules・`AutoBattleSim`×`GrowthRules` の橋） | `AutoBattleSim`(TIME-4)×`GrowthRules`(#537)。演習繰り返しに逓減・逆境係数・実会戦プレミアム |
| **ENDR-2** | #2352 | 指揮情報フィルタリング（CommandIntelFilterRules・組織内縦方向の情報遮断） | `EspionageRules`縦方向版×`EventEngine`×`NotificationCenter`。認識ギャップ→判断誤差→閾値でイベント発火 |
| **ENDR-3** | #2356 | 戦術フレームシフト（TacticalParadigmRules・「門が下にある」型パラダイム転換） | `CombatModifiers`/`ModifierStack`×`CommandDoctrine`×`AdmiralSkillRules`。習熟→敵の適応→優位消滅サイクル |
| **ENDR-4** | #2359 | 集合意識型指揮の脆弱性（CollectiveMindRules・完全同期ボーナス×中枢崩壊で全瓦解） | `CommandDoctrine`第3極×`SuccessionRules`継承不能×`BattleAllegianceManager` |
| **ENDR-5** | #2362 | （lore）「勝利の代償」後日開示（Speaker for the Dead） | `DisclosureLedger`(FND-4)。コード新設なし。`Community.Hope`遅延低下イベント接続 |

### 推奨着手順
`ENDR-1`（演習→実戦移転＝`AutoBattleSim` と `GrowthRules` の間に最も大きな穴）→ `ENDR-2`（情報フィルタリング＝縦方向諜報の signature）→ `ENDR-3`（戦術フレームシフト＝`CombatModifiers` への接続）→ `ENDR-4`（集合意識型＝非人間型指揮モデル）→ `ENDR-5`（lore 最後）。

> いずれも既存軍事ロジックを**後退させず接続**する additive 設計。`AutoBattleSim`/`GrowthRules`/`CommandDoctrine` の空白地帯に橋を架ける。
