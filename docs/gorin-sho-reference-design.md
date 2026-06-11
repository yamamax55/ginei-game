# 宮本武蔵『五輪書』参考設計（EPIC #GRN）

> 参照元：宮本武蔵『五輪書』（17世紀成立）。二天一流の兵法を地・水・火・風・空の五巻に著した実戦哲学。
> 武蔵の核心は**「拍子と間合い・後の先」**——敵の崩れを読み、最適な距離で、反攻のタイミングに一刀を入れる。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間艦隊戦＋既に豊富な戦闘純ロジック層）にとって**役に立つ視点**だけを抽出し、EPIC `#GRN` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**戦闘メカニクス／哲学の構造パターンのみ**を参考にする。

---

## 0. なぜ「五輪書」が本システムに役立つか

当プロジェクトは戦闘の**マクロ純ロジックを大量に保有**している：

| 既存（戦闘・提督） | カバー範囲 |
|---|---|
| `CombatModifiers` + `ModifierStack`（#106） | 攻撃/防御/側背面/機動の係数合成 |
| `FleetAI` (接近/交戦/撤退) | 三状態の AIステート機械 |
| `AdmiralSkillRules` / `AdmiralSkill`（#137） | 常時/劣勢時/交戦時のパッシブスキル |
| `AutonomyRules` + `CommandDoctrine`（#544-550） | 集団依存/自律分散ドクトリン＋創発シナジー |
| `FocusRules`（三密 #872） | 身×口×意 の集中度＝一瞬の最大出力 |
| `提督の決断 EPIC #502` | アクティブ采配＝戦機（窓）に切る特殊指令 |

**しかし、これらは「状態遷移・定常係数・内部集中」であり、五輪書が固有に描く以下が欠けている**：

| 五輪書が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **拍子（リズム）**：交戦には乗り/崩れのリズムがあり、崩れた敵は脆弱になる | `FleetAI` は内部状態機械。**敵艦隊の「崩れ」状態を外部から読んで攻勢転換する**窓の構造モデルが無い |
| **後の先**：敵の起こりを読み、先を譲って相手が動いた瞬間に反攻する | `CommandDoctrine` は集団依存/自律分散の**内部協調方式**。**攻撃タイミングの先手/後の先ドクトリン**（意図的に待つ＝積極的受動性）が無い |
| **間合い（ましろ）**：最適な交戦距離があり、それを意識的に制御する | `WeaponArc` は静的射程。**提督が間合いを意識して制御し、距離ドクトリンによる補正**が無い |
| **観の目（かんのめ）**：高所から戦場全体を見る目＝敵の崩れを遠くから感知 | `intelligence` stat は未使用。**戦場全体の拍子を遠くから読む「戦場知覚」**モデルが無い |

**結論**：五輪書は当プロジェクトの会戦系に**①拍子崩し（窓の開く条件の構造化） ②後の先ドクトリン（積極的受動性） ③間合い管理 ④観の目（戦場知覚）** という4つの欠落軸を与える。そして**#502（决断システム）の「戦機（窓）」に具体的なメカニクス的根拠**を提供する——拍子が崩れたときに窓が開く。

---

## 1. 役に立つ視点（要約）

五輪書の世界観を、**本システムに効く形**で1行ずつ：

1. **「拍子を知る者が勝つ」**——あらゆる事に拍子あり、乗る拍子と外す拍子を知れ。→ 会戦に**リズム状態（乗り/崩れ）**を持ち込み、崩れた敵への攻勢に補正を与える。#502 の「戦機窓」に根拠を与える。
2. **「後の先は弱さでなく技である」**——敵に先を取らせ、起こりの刹那に返す。→ `FleetAI` に**反攻型タイミングドクトリン**（积極的受动性）を加える。`CommandDoctrine`（集団依存/自律分散）とは直交した**攻撃タイミング軸**。
3. **「間合いが機会を決める」**——近すぎれば崩れ、遠すぎれば届かぬ。最適な距離に留まれ。→ 提督ごとの**交戦距離ドクトリン**（接近/中/遠）。得意間合いで戦えば効率UP・外れると低下。
4. **「観の目、見の目を分けよ」**——大きな目で全体を把握し、細部に惑わされるな。→ `intelligence`（現在未使用）を**戦場知覚範囲**に接続。高インテリジェンスは遠くの「崩れ」を感知できる。
5. **「空の巻」＝形にとらわれない境地**——技を超えると技が消える。自然体の勝ち方。→ `DisclosureLedger` への **lore 入力**。「無形の境地」は秘史・人間讃歌EPIC（#916）と共鳴。
6. **「兵法は集団にも通ずる」**——一人の剣の拍子は千人の軍の拍子と同型。→ 個艦の拍子＝部隊の拍子に拡張可能（スケール不変の設計指針）。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**#502決断システム・`CombatModifiers`・`FleetAI`・`AdmiralSkillRules`・`FocusRules`・`AutonomyRules` を作り直さない**。GRN はそれらに**欠落軸を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・五輪書の signature）

#### GRN 拍子と戦機窓（`BattleRhythm` + `BattleRhythmRules`）
- **拍子状態** `BattleRhythm` enum: `{乗り, 崩れ, 後の先機}`。
  - `乗り`：艦隊が秩序正しく機動中・定位置で交戦中（通常）。
  - `崩れ`：陣形変換中・方向転換中・大量被害後・退却直後。敵の攻撃に脆弱。
  - `後の先機`：`崩れ`の敵が接近してきたとき、反攻のカウンターウィンドウ。
- **`BattleRhythmRules`**（static・純ロジック）：
  - `DetectBreak(fleet, lastFormationChange, moraleDelta)` → 崩れ判定（bool）
  - `RhythmAttackBonus(attackerState, defenderState)` → 乗り攻撃/崩れ防御に係数差
  - `OpenCounterWindow(fleet, enemy)` → 後の先機判定（敵が崩れ状態かつ接近中）
- 接続：`CombatModifiers.ModifierStack`（係数合成）＋`FleetAI`（崩れ検知で接近機会判定）＋**#502 の「戦機窓」の開く条件を構造化**（崩れ=窓が開く）。
- 純ロジック・test-first（EditMode + TestHarness）。

#### GRN 後の先ドクトリン（`TimingDoctrine` + `TimingDoctrineRules`）
- **`TimingDoctrine`** enum: `{先の先, 後の先, 自在}`（`CommandDoctrine` 集団/自律とは別軸）。
  - `先の先`：先手で仕掛ける攻撃型。接近時の移動速度/攻撃に小ボーナス。
  - `後の先`：敵の起こりを待つ反攻型。待機→反撃時に移動/攻撃に大ボーナス（待った分の蓄え）。
  - `自在`：状況に応じて切り替え。ボーナスは小さいが状況適応性高。
- **`TimingDoctrineRules`**（static・純ロジック）：
  - `PostureBonus(doctrine, isCounter)` → 后の先で反撃中なら大ボーナス
  - `IsCounterWindow(fleet, enemy)` → 後の先機（GRN-1）で反攻中かどうか
  - `WaitThreshold(admiral)` → 後の先提督が「待つ」交戦距離の閾値（機動力比例）
- 接続：`AdmiralData`（`timingDoctrine` フィールド追加）＋`FleetAI`（状態遷移でドクトリン参照）＋`CombatModifiers`（後の先反撃の係数スタック）＋**`AutonomyRules` の CommandDoctrine とは直交した新軸**（縦軸=協調/自律、横軸=先手/後手）。
- 純ロジック・test-first。

### ★★ 高（既存に欠落している距離・知覚の軸）

#### GRN 間合いドクトリン（`EngagementDistanceRules`）
- 提督ごとの**最適交戦距離**：`EngagementRange` enum `{近距離, 中距離, 遠距離}`。
  - 高機動（`EffectiveMobility`）→ 近距離得意、高攻撃（`EffectiveAttack`）→ 遠距離得意が既定。
  - `preferredRange` を `AdmiralData` に追加（`hasPreferredRange=false` で後方互換）。
- **`EngagementDistanceRules`**（static）：
  - `PreferredRange(admiral)` → 能力から自動算出（`hasPreferredRange`=false 時のフォールバック）
  - `DistanceFactor(current, preferred)` → 得意間合いで1.0超、外れると低下
- 接続：`FleetAI`（AIが間合い目標を計算）＋`FleetMovement`（目標距離への接近/離脱）＋`CombatModifiers`（間合い補正の ModifierStack 合成）＋**`得意陣形(#104)` のパターンと同型**（得意間合い=コードパターン共有）。
- 純ロジック・test-first。基準値は非破壊（実効値パターン）。

#### GRN 観の目・見の目（`BattlePerceptionRules`）
- **戦場知覚範囲**：`intelligence`（`EffectiveIntelligence`）が高い提督は遠くの「崩れ」を感知できる。
  - 低インテリジェンス＝見の目：射程内の直近敵の崩れしか感知できない。
  - 高インテリジェンス＝観の目：戦場全体の崩れを感知し、AI が最も有利な標的を選べる。
- **`BattlePerceptionRules`**（static）：
  - `PerceptionRadius(admiral)` → `EffectiveIntelligence` から知覚半径を算出
  - `CanSenseRhythm(admiral, targetFleet, distance)` → 知覚半径内なら `BattleRhythm` を感知可能
  - `BestTarget(admiral, enemies, rhythmMap)` → 観の目で選ぶ最優先標的（崩れ敵を優先）
- 接続：`AdmiralData.EffectiveIntelligence`（現在未使用 → 活用）＋GRN-1 `BattleRhythmRules`＋`FleetAI`（索敵ロジックを `BestTarget` へ委譲可能）。

### ★ 中（世界観lore）

#### GRN（lore）空の巻・無形の境地（`DisclosureLedger`）
- 五輪書の哲学：「空とは形にとらわれない境地——技を超えると技が消え、自然に勝つ」。
- 「兵法の極意は人間の意志の産物」→ 人間讃歌 #916「AIにできない一瞬の決断」と共鳴。
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への**lore データ入力**。

### ❌ 不採用（重複・適用不可）

| 不採用 | 理由 |
|---|---|
| 二刀流/二天一流の具体的な技法 | 個人剣術の型。艦隊戦には適用不可 |
| 身体鍛錬・反復訓練ループ | `GrowthRules`（#537-543）がカバー予定（経験→実効能力） |
| 個人対個人の「形稽古」実装 | タイクン化回避＝マイクロ操作増加につながる |
| `CommandDoctrine` の 後の先版への置換 | 既存 `AutonomyRules` は後退させない。別軸で足す |
| 「水のように」変化する形（形そのものの動的変化） | 陣形変換（#104）が既にカバー |
| スタミナ/体力系（持久戦での消耗） | `FleetMorale` が士気で既にカバー |

---

## 3. EPIC #GRN の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ `FleetAI`/`CombatModifiers` に配線。既存戦闘ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/哲学の構造パターンのみ**参考。

> **EPIC = #1372**。GitHub issue 起票済み（#1376, #1379, #1384, #1387, #1391）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **GRN-1** | #1376 | 拍子と戦機窓（`BattleRhythm` 型 + `BattleRhythmRules`・乗り/崩れ/後の先機） | `CombatModifiers`・#502 戦機窓の構造化 |
| **GRN-2** | #1379 | 後の先ドクトリン（`TimingDoctrine` + `TimingDoctrineRules`・反攻型AI補正） | `AdmiralData` フィールド追加・`FleetAI` 状態遷移 |
| **GRN-3** | #1384 | 間合いドクトリン（`EngagementDistanceRules`・最適交戦距離×係数） | `FleetAI`・`FleetMovement`・得意陣形#104 と同型 |
| **GRN-4** | #1387 | 観の目・見の目（`BattlePerceptionRules`・intelligence→戦場知覚） | `AdmiralData.EffectiveIntelligence` を活用・GRN-1 接続 |
| **GRN-5** | #1391 | （lore）空の巻・無形の境地（`DisclosureLedger` への哲学データ入力） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`GRN-1`（拍子崩し＝最も固有で欠落の大きい signature・#502 戦機窓の根拠）→ `GRN-2`（後の先ドクトリン＝1と連動する反攻スタイル）→ `GRN-3`（間合い＝距離制御の軸を独立実装）→ `GRN-4`（観の目＝intelligence を初めて使う・1の感知に乗る）→ `GRN-5`（lore は最後）。

> いずれも既存戦闘システムを**後退させず接続**する additive 設計。**#502「決断システム」の「戦機窓」に**拍子崩し（GRN-1）という**メカニクス的根拠を与える**のが最大の接続効果。
