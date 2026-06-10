# 孫子（兵法）参考設計（EPIC #SUN）

> 参照元：孫子兵法（春秋末期）。13篇からなる世界最古の兵法書の一つ。「知己知彼、百戦不殆」。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略＋既に広大な純ロジック層）にとって**役に立つ視点だけ**を抽出し、EPIC `#SUN` として issue 化する提案。
> **著作権注意：固有名・文章・キャラクター・固有設定は流用せず、軍事メカニクス／兵法の構造パターンのみを参考にする。**

---

## 0. なぜ「孫子」が本システムに役立つか

当プロジェクトは軍事・戦略ロジックを**大量に保有**している：

| 既存（カバー範囲） | 何をカバーするか |
|---|---|
| `EspionageRules/SpyNetwork` | 諜報ミッション・情報収集・妨害工作（`MissionSuccessChance/InfoGain/DetectionRisk/SabotageEffect`） |
| `SupplyRules` | 補給線・前線枯渇（`SuppliedSystems/TickFront`） |
| `WarGoalRules` | 政治的厭戦感・戦争目標・講和受諾確率 |
| `DiplomacyRules` | 外交状態・同盟/不可侵・宣戦/講和 |
| `BattleAllegianceRules`/`LoyaltyRules` | 旗幟・忠誠・寝返りカスケード |
| `StrategyRules` | 戦略会戦・占領・接触判定 |
| SGZ-4（計画中） | 離間の計＝標的勢力ペアの opinion 工作・同盟崩壊 |

**しかし、これらに孫子固有の以下が欠けている**：

| 孫子が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **詭道（欺瞞・陽動）** — 戦争はだますこと | `EspionageRules` は情報収集・破壊工作。**偽情報を流して敵AIの意思決定そのものを歪める欺瞞ミッション**が無い |
| **用間5種体系** — 間者に役割差別化 | `SpyNetwork` は役割を区別しない単一モデル。郷間/内間/反間/死間/生間の**役割別効果・コスト・リスク**が無い |
| **「糧を敵に因る」** — 現地調達で補給線依存を断つ | `SupplyRules.TickFront` は補給線提供側のモデル。**占領地・通過星系から自律調達して消費を削減**するルールが無い |
| **攻勢終末点** — 補給距離に比例する純軍事的停止点 | `WarGoalRules.WarWeariness` は政治的厭戦。**前線伸張による戦力効率低下**の純軍事モデルが無い |
| **手段の階層（謀>交>兵>攻城）** — 武力は最後の手段 | 各手段のコスト比較を明示する AI スコアリングが無い |

**結論**：孫子は当プロジェクトの諜報・兵站・AI戦略に、①欺瞞の動的次元、②用間の構造、③現地調達の経路、④攻勢終末点という4つの欠落軸を与える。すでに `EspionageRules`/`SupplyRules`/`WarGoalRules` が基盤として存在するため、**additive な接続で深みを加えるだけでよい**。

---

## 1. 役に立つ視点

孫子の世界観を、**本システムに効く形**で1行ずつ：

1. **「上兵伐謀」** — 最善は敵の謀略を打ち砕くこと。武力は最後の手段（攻城は最下）。→ AI の意思決定に「手段の優先順位スコア」（謀攻優先ドクトリン）を導入。
2. **詭道** — 強いときは弱いように見せる。近づくときは遠くに見せる。→ 欺瞞ミッションで敵AIの戦略行動そのもの（進路・兵力集中）を誤らせる。
3. **用間（間者5種）** — 知者の仁であり、諸事の要。反間（二重スパイ）が最も価値高い。→ 役割別スパイ体系で諜報に戦略的な深みと使い分けを与える。
4. **「糧を敵に因る」** — 遠地に兵を置くほど国が痩せる。現地で調達して速く進め。→ `ForageRules` で補給線の束縛を断つ攻勢オプション（占領速度 vs 安定度のトレードオフ）。
5. **攻勢終末点** — 補給が届かなくなるところで作戦は止まる。→ `CulminatingPointRules` で過伸張の限界を純ロジックとして明示。`WarGoalRules` の政治モデルとは別系統。
6. **「知己知彼」** — 情報の完全制覇が不敗の条件。→ 用間体系＋欺瞞の組み合わせが**知己知彼の機能化**。諜報への戦略的投資に意味を与える。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`EspionageRules`/`SupplyRules`/`DiplomacyRules`/`WarGoalRules` を作り直さない**。SUN はそれらに欠落軸を接続するだけ（additive）。

### ★★★ 最優先（孫子の signature・真の欠落）

#### SUN 欺瞞作戦・陽動（DeceptionRules）
- 新 `DeceptionRules`（純ロジック・Core・test-first）：偽情報/陽動/虚兵ミッション。
- **欺瞞の種類**：存在欺瞞（存在しない艦隊を誤認させる）／意図欺瞞（攻撃目標を偽る）／兵力欺瞞（戦力を過大/過小に見せる）。
- **状態タグ** `DeceivedState`：標的勢力が特定回廊・星系について誤った認識を保持。`GalaxyView` AI がこのタグを「真」として進路/目標を決定する（欺瞞が戦略行動を歪める）。
- **リスク**：発覚確率は `EspionageRules.DetectionRisk` に乗せる（発覚で `DiplomacyState.opinion` が急落＝逆効果）。欺瞞を重ねるほど発覚リスク上昇（信頼残高の消費）。
- 接続：`EspionageRules`×`SpyNetwork`×`DiplomacyState`×`GalaxyView`AI。

#### SUN 用間5種体系（SpyRoleRules）
- 新 `SpyRoleRules`（純ロジック・Core・test-first）＋ `SpyNetwork` に役割 enum `SpyRole{郷間,内間,反間,死間,生間}` を追加。
- 役割別効果：
  - **郷間**（現地人スパイ）→ `InfoGain` 高・`Province` 状態を詳細取得
  - **内間**（敵勢力内通者）→ `FactionState`/`CampaignState` の一部を直接取得
  - **反間**（二重スパイ）→ 欺瞞ミッション（SUN-1）の成功率+30%・最高価値
  - **死間**（捨て石陽動）→ 欺瞞成功率↑・発覚時に消耗（再起用不可）・コスト低
  - **生間**（帰還型）→ 基本の情報帰還・再起用可
- `EffectMultiplier(role, missionType)`／`DetectionRiskModifier(role)`／`RecruitmentCost(role)` を純関数で定義。
- 接続：`EspionageRules`×`SpyNetwork`×`DeceptionRules`(SUN-1)。

### ★★ 高（兵站の深化）

#### SUN「糧を敵に因る」現地調達（ForageRules）
- 新 `ForageRules`（純ロジック・Core・test-first）：前線付近の占領/通過星系から補給を引き出す。
- **効果**：艦隊の `ResourceStockpile` 消費量を最大 `maxForageRatio`（既定0.4）削減。補給線を断たれても `forageFactor` 分だけ前進継続可能。
- **条件**：通過星系の `Province.stability` と `GovernanceRules.OutputFactor` に比例（豊かで安定した星系ほど調達量↑）。略奪→安定低下（`GovernanceRules.OnOccupied` 連動）のトレードオフ——現地調達は占領地の民心を蝕む。
- **限界**：`maxForageSystems`（既定2ホップ以内）を超えると効果減衰。完全自給はできない＝本線補給との組み合わせ。
- 接続：`SupplyRules`×`ResourceProductionRules`×`GovernanceRules`×`FleetPool`。

#### SUN 攻勢終末点・戦略的過伸張（CulminatingPointRules）
- 新 `CulminatingPointRules`（純ロジック・Core・test-first）：補給源からの距離に比例して戦力効率が低下する純軍事モデル。
- **指標** `OverextensionFactor(fleetId, map, supplySystemIds)`：補給源から最短経路ホップ数をシグモイド補正 → 0..1 の過伸張度。
- **効果**：`StrategyRules.ResolveCorridorBattle` に `1 - overextension × 0.5` を乗算（実効戦力低下）。`WarGoalRules.WarWeariness` とは別系統——こちらは政治ではなく純軍事の停止点。
- 接続：`SupplyRules`×`GalaxyPathfinder`×`StrategyRules`。

### ★ 中（AI 統合・lore）

#### SUN 謀攻優先ドクトリン（SunziDoctrineRules）
- 新 `SunziDoctrineRules`（純ロジック）：「謀>交>兵>攻城」の手段階層を AI の行動スコアリングに与える。
- `PreferenceScore(option, state)` — 欺瞞成功期待値が高いなら直接戦闘コストを加算／外交解決可能なら武力オプションのスコアを抑制。
- 接続：`GalaxyView`AI×`DiplomacyRules`×`DeceptionRules`(SUN-1)。タイクン化回避＝AI の自律判断に乗せる（プレイヤーへの強制なし）。

#### SUN（lore）世界観の開示データ
- 「戦わずして勝つ（上兵伐謀）」「知己知彼（情報優位が不敗の条件）」「形・勢・虚実（主動性と虚の集中）」「謀攻——攻城は最下なり」。
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への**lore データ入力**。CCX-6 世界観 codex 方針に一貫。

### ❌ 不採用（重複・既存カバー・タイクン化）

| 不採用 | 理由 |
|---|---|
| 戦場地形9種の個別実装 | ZOC/BlackHole 等で部分カバー済み。地形マイクロはタイクン化の温床 |
| 将軍の「五危」（性格欠陥）システム | `AutonomyRules`#544 の負側・`AdmiralData` の能力構造で対応可。新設不要 |
| 火攻（特殊兵器）の個別演出 | `FleetWeapon` のミサイルモードで代替可能。演出差別化のみ |
| 外交優先の強制（プレイヤーへ）| プレイヤー自由度と衝突。AI スコアリングのみ（SUN-5）に限定 |
| 間者の社会的身分/変装詳細 | キャラクター固有実装＝タイクン化。`SpyRole` enum に抽象化 |
| 「孫子の計」勝敗算出モデル | `StrategyRules.ResolveCorridorBattle` が既にカバー |

---

## 3. EPIC #SUN の子Issue（採用分のみ・着手順）

> 純ロジック新設は EditMode/TestHarness 先行（test-first）→ 盤面/UI へ配線。既存軍事ロジックは**接続のみ・重複新設しない**。
> **著作権注意：固有名・文章・キャラクターは不使用。軍事メカニクス/兵法の構造パターンのみ参考。**

> **EPIC = #1125**。GitHub issue 起票済み（#1126〜#1131）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **SUN-1** | #1126 | 欺瞞作戦・陽動（DeceptionRules — 偽情報/陽動で敵AI行動を歪める） | `EspionageRules`×`SpyNetwork`×`GalaxyView`AI×`DiplomacyState` |
| **SUN-2** | #1127 | 用間5種体系（SpyRoleRules — 郷間/内間/反間/死間/生間の役割別効果・リスク） | `EspionageRules`×`SpyNetwork`×SUN-1(`DeceptionRules`) |
| **SUN-3** | #1128 | 「糧を敵に因る」現地調達（ForageRules — 占領地・通過星系からの自律補給） | `SupplyRules`×`ResourceProductionRules`×`GovernanceRules`×`FleetPool` |
| **SUN-4** | #1129 | 攻勢終末点・戦略的過伸張（CulminatingPointRules — 補給距離比例の戦力効率低下） | `SupplyRules`×`GalaxyPathfinder`×`StrategyRules.ResolveCorridorBattle` |
| **SUN-5** | #1130 | 謀攻優先ドクトリン（SunziDoctrineRules — 謀>交>兵>攻城のAIスコアリング） | `GalaxyView`AI×`DiplomacyRules`×SUN-1(`DeceptionRules`) |
| **SUN-6** | #1131 | （lore）「戦わずして勝つ」「知己知彼」「形・勢・虚実」世界観codex | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順

`SUN-1`（欺瞞作戦＝孫子の signature・最も欠落が大きい）→ `SUN-2`（用間体系で SUN-1 を強化・反間が欺瞞の最大化）→ `SUN-3`（現地調達＝兵站独立性・補給戦の第二経路）→ `SUN-4`（攻勢終末点＝過伸張の限界・SUN-3 と表裏）→ `SUN-5`（ドクトリン＝SUN-1/2/3/4 を AI に統合）→ `SUN-6`（lore）。

> SUN-1・SUN-2 が完成すると `EspionageRules` が「情報収集→欺瞞→AI誤動作」の連鎖を持つ。SUN-3・SUN-4 が完成すると兵站が「補給線提供」一本から「現地調達＋終末点」の二軸になる。**SGZ-4（離間の計・opinion工作）とは独立**：SUN は意思決定の歪め方（欺瞞）、SGZ は関係値の操作（opinion）。
