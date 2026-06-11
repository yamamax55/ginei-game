# 司馬遼太郎『坂の上の雲』参考設計（EPIC #SKUN）

> 参照元：司馬遼太郎『坂の上の雲』。明治日本の近代化と日露戦争（1904-05年）を描く大河小説。
> 秋山兄弟・正岡子規を軸に「後発国が列強と伍するために何が必要か」を問う——近代化・組織・意志・決断の構造。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略）にとって**役に立つ視点だけ**を抽出し、EPIC `#SKUN` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**メカニクス／世界観の構造パターンのみ**を参考にする。

---

## 0. なぜ「坂の上の雲」が本システムに役立つか

当プロジェクトは近代化・国力・軍事組織の**マクロ純ロジック層を大量に保有**している：

| 既存（カバー範囲） | 担当モジュール |
|---|---|
| 王朝サイクル・制度改革（腐敗低下・正統性回復） | `Regime.Reform`/`DynastyRules`（#867） |
| 技術研究・技術波動 | `ResearchRules`/`TechWaveRules`（KEN #1321） |
| 技術拡散（他国から盗む/買う/模倣） | `TechDiffusionRules`（MCN） |
| 人材パイプライン（士官学校/科挙/テクノクラート） | `CareerPipelineRules`/`SeniorityRules`（LIFE-5/6/7） |
| 戦術レベルの士気 | `FleetMorale`/`GetMoraleFactor()` |
| 社会的希望と末人問題 | `HopeRules`/`Community`（#852） |
| 補給線・制海権チョークポイント | `SupplyRules`/`LogisticsRules`（#92/#844） |
| 惑星攻城 | `PlanetSiegeRules`/`SiegeArena`（#131） |
| 外交状態・条約・同盟 | `DiplomacyRules`/`DiplomacyState`（#189）、`TreatyRules`（DIP-2） |
| 個人の成長曲線・退役・継承 | `GrowthRules`、`RetirementRules`（#530-543） |
| 財政・国債・生産力 | `FiscalRules`/`ShipyardRules` |

**しかし、坂の上の雲が固有に持つ以下の軸が欠けている**：

| 坂の上の雲が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **政府主導の全ドメイン同時加速（明治近代化プログラム）** | `Regime.Reform` は腐敗/正統性の二値変化のみ。**研究・造船・人材・財政効率が同時に加速する「近代化ダッシュ」**の多面的ブースタが無い |
| **艦隊決戦主義 vs 現存艦隊 vs 通商破壊のドクトリン選択** | `FleetAI` は戦術行動（接近/交戦/撤退）のみ。**勢力レベルで主力艦隊をどう使うかというドクトリン選択**が無い |
| **後発国の国家意志（不利な戦力比を覆す底力）** | `FleetMorale` は艦隊単位の戦術士気。`Community.hope` は社会安定。**戦力比劣位でも戦い続ける「国民の犠牲意志」という戦略リソース**が無い |
| **制海権が陸上作戦を可能にする協調構造** | `SupplyRules.IsSupplied` と `PlanetSiegeRules` は独立。**制海権確保→惑星攻城が加速するという協調ブースタ**が無い |
| **外国顧問・軍事援助による急速能力移転** | `TechDiffusionRules`（MCN）は技術の自然拡散。**同盟条件下で顧問団を招聘し特定能力の育成を加速する能動的援助**が無い |
| **決戦の機会窓口——国運を賭ける「一発逆転」の構造** | `StrategyRules` は逐次解決。**蓄積条件が揃ったとき「今こそ全力決戦」の戦略イベントが発火するトリガー機構**が無い |

**結論**：坂の上の雲は当プロジェクトに**「後発国の近代化戦略」という視点**を与える。
①近代化プログラム ②艦隊ドクトリン ③国家意志 ④制海権×陸戦協調 ⑤外国顧問 ⑥決戦窓口——6つの欠落軸を additive に足し、弱小勢力が強大敵に勝つゲームの骨格を作る。

---

## 1. 役に立つ視点（要約）

坂の上の雲の世界観を、**本システムに効く形**で1行ずつ：

1. **近代化は「制度+技術+人材」の同時最適化**。どれか一つ欠けても近代国家は成立しない——明治日本が成功したのは三者を同時に改革したから。→ `Regime.Reform` に**多面ブースタ**を接続。
2. **艦隊は「切り札」であり「保険」でもある**。決戦に投じれば一撃で決着するが、負ければ全て失う。現存艦隊のまま温存するか、通商破壊で削るか——ドクトリン選択が戦略の要。→ 新 `FleetDoctrineRules`。将来: マハン参考EPIC（バックログ）と連携予定。
3. **弱者は「意志」で戦力差を縮める**。圧倒的な戦力差でも国民の犠牲意志が高ければ士気・継戦能力・戦闘効率が維持される——「坂の上の雲」を見上げる者の強さ。→ 新 `NationalDeterminationRules`。
4. **制海権は陸戦の前提条件**。補給路を海が繋ぐ限り、島国/海洋勢力は前線を維持できる。→ `SupplyRules`×`PlanetSiegeRules` の相乗係数。
5. **顧問団は「制度ごと輸入する」技術移転**。個別技術の拡散（MCN）ではなく、同盟国から顧問を招聘して育成速度を高める能動的政策。→ 新 `ForeignAdvisorRules`（`DiplomacyState` が前提条件）。
6. **決戦は「構造的に生じる」——蓄積が臨界に達する**。国力差・補給危機・士気崩壊の蓄積が「今が最後のチャンス」という窓を開く。→ `EventEngine` に`DecisiveBattleWindowRules` のトリガーを接続。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`Regime`/`ResearchRules`/`CareerPipelineRules`/`SupplyRules`/`PlanetSiegeRules`/`DiplomacyRules` を作り直さない**。SKUN はそれらに欠落軸を足し接続するだけ（additive）。

### ★★★ 最優先（坂の上の雲の signature・真の欠落）

#### SKUN 近代化プログラム（`ModernizationProgramRules`/`ModernizationProgram`）
- **`ModernizationProgram`（純データ）**：`faction`/`intensity`（0..1）/`duration`（秒）/`remainingDuration`/`isActive`。
- **`ModernizationProgramRules`（static・純ロジック）**：
  - `Launch(faction, intensity)` → `isActive=true`、コスト計算（`FiscalRules` へ負担）。
  - `Tick(program, dt)` → `remainingDuration` 減算、終了で `isActive=false`。
  - `ResearchBonus(program)` → 研究速度×(1 + intensity×`researchScale`)。
  - `ShipyardBonus(program)` → 建艦速度×(1 + intensity×`buildScale`)。
  - `CareerBonus(program)` → 人材育成速度×(1 + intensity×`careerScale`)。
  - `StabilityRisk(program)` → `FactionState.Polity.legitimacy` 削減リスク（急速近代化の反動）。
- `Regime.Reform` 実行時に `Launch` 可（腐敗低下＋正統性回復＝EXISTING）に加え、多面加速を**オプションとして**乗せる。基準値は非破壊（実効値パターン）。
- 接続：`DynastyRules.Reform`×`ResearchRules.ResearchOutput`×`ShipyardRules.ProductionFactor`×`CareerPipelineRules`×`FiscalRules`（コスト）。
- 純ロジック・test-first（EditModeテスト必須）。

#### SKUN 艦隊ドクトリン選択（`FleetDoctrineRules`/`FleetDoctrine` enum）
- **`FleetDoctrine` enum**：`{決戦, 漸減, 通商破壊, 現存艦隊}` ＝ 勢力の主力艦隊運用方針。
  - 決戦：積極的に敵主力艦隊を捕捉し一撃決着を狙う。集中ボーナス・敗北リスク高。
  - 漸減：小競り合いで消耗させ優勢を維持する。長期戦耐久性↑・決定打遅。
  - 通商破壊：敵の補給回廊を優先攻撃し経済を締め上げる。`SupplyRules` へ直結。
  - 現存艦隊：港で温存し敵の迂闊な接近を牽制する（Fleet in Being）。損耗↓・積極支援不可。
- **`FleetDoctrineRules`（static・純ロジック）**：
  - `AIEngagementBias(doctrine)` → AIが敵主力を探しに行くか回廊を狙うかの重みづけ（`FleetAI` へ）。
  - `CombatBonus(attacker, defender)` → ドクトリン相性の有利/不利係数。
  - `RiskFactor(doctrine)` → 敗北時の損耗倍率（決戦が最高・現存艦隊が最低）。
- 接続：`FleetAI`（エンゲージメント優先順位の重みに乗せる）×`StrategyRules`（回廊攻撃/迎撃の優先度）×`FactionData`（勢力ごとにドクトリンを設定）。
- 将来: マハン参考EPIC（バックログ未処理）と連携・統合予定。
- 純ロジック・test-first（EditModeテスト必須）。

#### SKUN 国家意志・後発国の底力（`NationalDeterminationRules`/`NationalDetermination`）
- **`NationalDetermination`（純データ）**：`will`（0..1）/`sacrificeAcceptance`（0..1）/`isUnderdog`（戦力比 < `underdogThreshold`）。
- **`NationalDeterminationRules`（static・純ロジック）**：
  - `DeriveWill(factionState)` → `Community.hope`×組織結束×統合度の加重平均（0..1）。
  - `MoraleRecoveryBonus(det)` → 意志高なら士気回復速度倍率（最大×1.5）。
  - `UnderdogCombatFactor(det)` → `isUnderdog` かつ意志高なら戦闘効率補正（`CombatModifiers` へ合流・最大1.2）。
  - `WithdrawThreshold(det)` → 低意志で撤退閾値が早まり、高意志で延長。
- `FactionState.Community.hope` を「戦略的意志」として再合成——社会希望×犠牲許容の積。`FleetMorale`（戦術）とは別軸（戦略）。
- 接続：`FactionState`→`CombatModifiers.ModifierStack`（実効値パターン）→`FleetMorale`（士気回復係数）。
- 純ロジック・test-first（EditModeテスト必須）。

### ★★ 高（核心を補完する）

#### SKUN 制海権×陸上作戦協調（`SeaControlLeverageRules`）
- **`SeaControlLeverageRules`（static・純ロジック）**：
  - `HasSeaControl(map, corridorId, faction)` → 回廊の両端星系が同勢力保有または敵不在 = true。
  - `SiegeBonus(map, corridorId, siegingFaction, params)` → 制海権あり=`params.bonus`（既定1.25）、無し=1.0。
  - `SupplyMultiplier(map, system, faction)` → 補給路上の全回廊の制海権が補給効率に追加ボーナス。
- `StrategyRules.TickSieges` 呼び出し時に、隣接回廊の制海権ボーナスをアドオン。
- 接続：`StrategyRules.TickSieges`×`SupplyRules.IsSupplied`×`PlanetSiegeRules.Tick`。
- 純ロジック・test-first（EditModeテスト必須）。

#### SKUN 外国顧問・軍事援助（`ForeignAdvisorRules`/`ForeignAdvisor`）
- **`ForeignAdvisor`（純データ）**：`sourceFaction`/`targetFaction`/`field`（`ResearchField`）/`remainingDuration`/`careerBoostScale`（0..1）/`techTransferRate`（0..1）。
- **`ForeignAdvisorRules`（static・純ロジック）**：
  - `CanRequest(diplomacy, source, target)` → 同盟または友好条約が条件（`DiplomacyState`）。
  - `ResearchBoost(advisor, baseOutput)` → `baseOutput × (1 + techTransferRate)`。
  - `CareerBoost(advisor)` → `careerBoostScale` × ソース勢力の当該分野能力値 = 育成速度加算。
  - `CostPerTick(advisor)` → 維持費（`FiscalRules` から引く）。
  - `TechDiffusionRules`（MCN）との違い：本ルールは**外交同盟が前提の能動的招聘**（コスト＋条件あり・定額・期間限定）。MCN 拡散は条件なしの受動的機会的拡散。
- 接続：`DiplomacyState`（条件）×`ResearchRules`（研究加速）×`CareerPipelineRules`（育成加速）×`FiscalRules`（維持費）。
- 純ロジック・test-first（EditModeテスト必須）。

#### SKUN 決戦の機会窓口（`DecisiveBattleWindowRules`）
- **`DecisiveBattleWindowRules`（static・純ロジック）**：
  - `IsWindowOpen(det, doctrine, fleetRatio, enemySupplyRisk, params)` → bool（全条件の積が閾値以上）。
  - `DecisiveBattleCombatBonus(windowOpen, doctrine)` → 窓が開いた状態でドクトリン=決戦の交戦は `combatBonusMultiplier`（既定1.3）。
  - `PostBattleConsequence(won, fleetLossRatio)` → 勝利=`NationalDetermination.will`↑、敗北=腐敗↑＋意志↓（リスクリターン非対称）。
- `EventEngine`（#116）に「決戦の機会」イベントをpush。プレイヤーが受諾/拒否（拒否=窓は時間と共に消える）。
- 接続：`NationalDeterminationRules`（SKUN-3）×`FleetDoctrineRules`（SKUN-2）×`SupplyRules`（敵補給危機）×`EventEngine`（#116）。
- 純ロジック・test-first（EditModeテスト必須）。

### ★ 中（世界観lore）

#### SKUN （lore）坂の上の雲世界観の開示データ
- 「近代化の代償（改革が反乱を生む）」「小国の夢（坂の上の雲を見上げる）」「決戦の必然（構造が戦いを呼ぶ）」「制度と個人（組織と人の関係）」。
- 接続：**コード新設なし** `DisclosureLedger`（FND-4）への**lore データ入力**のみ。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 騎兵戦術・陸戦の詳細戦術 | 本ゲームは艦隊戦。地上戦術の新設は対象外 |
| 講和交渉・外交条約の手続き詳細 | `DiplomacyRules`/`WarGoalRules.PeaceAcceptance`（DIP-3）で既にカバー |
| 砲術技術の個別スペック（射程・命中率の精密計算） | `CombatModifiers` で係数カバー済み。低レベル詳細化はタイクン化 |
| 海軍報道・プロパガンダ戦 | `EspionageRules`/`DeceptionRules`（SUN系）でカバー |
| マハン理論そのもの（シーパワー論・チョークポイント要衝） | マハン参考EPIC（バックログ未処理）で後処理。重複新設しない |
| 個人キャラクターの伝記ドラマ | `GrowthRules`/`CareerPipelineRules` + lore（SKUN-7）で対処 |

---

## 3. EPIC #SKUN の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。既存近代化/戦略ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #1430**。GitHub issue 起票済み（#1431〜#1437）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **SKUN-1** | #1431 | 近代化プログラム（`ModernizationProgramRules`：研究×造船×人材の多面加速、`Regime.Reform` 連動） | `DynastyRules`×`ResearchRules`×`ShipyardRules`×`CareerPipelineRules`×`FiscalRules` |
| **SKUN-2** | #1432 | 艦隊ドクトリン選択（`FleetDoctrineRules`/enum `{決戦/漸減/通商破壊/現存艦隊}`：AI行動重みとドクトリン相性） | `FleetAI`×`StrategyRules`×`FactionData`。マハン参考EPIC連携予定 |
| **SKUN-3** | #1433 | 国家意志・後発国の底力（`NationalDeterminationRules`：劣位戦力比での戦闘効率補正・士気回復加速） | `FactionState`→`CombatModifiers.ModifierStack`→`FleetMorale` |
| **SKUN-4** | #1434 | 制海権×陸上作戦協調（`SeaControlLeverageRules`：制海権保有→隣接惑星攻城・補給にボーナス） | `StrategyRules`×`SupplyRules`×`PlanetSiegeRules` |
| **SKUN-5** | #1435 | 外国顧問・軍事援助（`ForeignAdvisorRules`/`ForeignAdvisor`：同盟条件下で研究・人材育成を加速） | `DiplomacyState`×`ResearchRules`×`CareerPipelineRules`×`FiscalRules` |
| **SKUN-6** | #1436 | 決戦の機会窓口（`DecisiveBattleWindowRules`：蓄積条件が揃ったとき `EventEngine` に「決戦の機会」発火） | `NationalDeterminationRules`×`FleetDoctrineRules`×`SupplyRules`×`EventEngine` |
| **SKUN-7** | #1437 | （lore）坂の上の雲世界観の開示データ（「近代化の代償」「小国の夢」「決戦の必然」を `DisclosureLedger` へ） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`SKUN-1 → SKUN-2`（近代化プログラムとドクトリン＝互いを補完する核心）→ `SKUN-3`（国家意志＝両者に乗る戦略リソース）→ `SKUN-4 → SKUN-5`（制海権協調と外国顧問＝盤面への配線）→ `SKUN-6`（決戦窓口＝全要素の統合イベント）→ `SKUN-7`（lore は随時）。

> いずれも既存モジュールを**後退させず接続**する additive 設計。弱小勢力が強大勢力に勝つ「逆転の条件」を与える。
