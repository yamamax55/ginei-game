# トルストイ『戦争と平和』参考設計（EPIC #WAP）

> 参照元：レフ・トルストイ『戦争と平和』（1869年）。1812年ナポレオン対ロシア遠征を舞台に、
> **焦土・撤退・パルチザン・大会戦の混沌・民族的抵抗**を描く長編。
> トルストイは同時に「個人は歴史を作れない——歴史は集団的力の産物だ」という哲学を論じる。
> 本ドキュメントは当プロジェクト（Ginei）にとって**役に立つ構造パターンのみ**を抽出する。
> **著作権注意：固有名・文章・キャラクター・固有設定は流用せず、戦略メカニクス／世界観構造パターンのみを参考にする。**

---

## 0. なぜ「戦争と平和」が本システムに役立つか

当プロジェクトは軍事・政治・補給のロジックを広く保有している：

| 既存（カバー済み） | カバー範囲 |
|---|---|
| `FrictionRules`（CLZ-1 #1133） | 摩擦：命令階層深度×補給比×士気→作戦劣化 |
| `CulminatingPointRules`（SUN-4 #1129） | 攻勢終末点：補給距離比例の戦力効率低下 |
| `ForageRules`（SUN-3 #1128） | 現地調達：占領・通過星系からの自律補給 |
| `DepotRules`（CRV-1 #1362） | 前進補給基地：到達限界の延伸 |
| `SupplyModeRules`（CRV-3 #1364） | 攻防補給非対称：攻撃側は防衛側の2〜3倍消費 |
| `InsurgencyRules`/`InsurgencyState`（SPW-2 #1385） | 占領地反乱の**能動組織化**（外部勢力が資源投入） |
| `GuerrillaDoctrineRules`/`OperationalMode`（SPW-3 #1386） | 遊撃戦ドクトリン：非正規軍の交戦回避＋インフラ破壊 |
| `CorridorSabotageRules`（SPW-1 #1384） | 回廊サボタージュ：一時的な補給路機能不全 |
| `IndirectApproachRules`（LDH-1 #1339） | 最小予期線：間接経路評価 |
| `GovernanceRules.RebelPressure`（#109） | 安定度・抵抗圧力（有機的・受動的） |
| `WarGoalRules`/`WarWeariness`（DIP-3 #192） | 厭戦・講和条件 |
| `ResourceProductionRules`/`ResourceStockpile`（#92） | 資源産出・勢力備蓄 |

**しかし、これらが揃ってもトルストイが描く以下の構造が欠けている**：

| 「戦争と平和」が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **焦土作戦**（モスクワ放棄・自軍が立ち退く前に都市・食料を燃やす） | SPW の回廊サボタージュは**敵**インフラを壊す。CRV は補給の受動的優位。自分の領土を**意図的に破壊して放棄する**という能動的決断がない |
| **侵攻深度が増すほど抵抗が強まる逆説**（モスクワへの行軍ほど補給と士気が崩れる） | SPW-2 の InsurgencyRules は外部勢力による**意図的な**反乱組織化。自領土の**自動的な深度比例抵抗逓増**（深く入るほど補給コスト増・占領地抵抗増・勢力結束増）がない |
| **大会戦の規模限界**（ボロジノ：25万人が小戦域で激突、指揮統制が崩壊し誰もコントロールできない） | CLZ-1 FrictionRules は命令階層の**深さ**が摩擦を生む。大会戦では**力の絶対規模（兵員数）**自体が非線形摩擦を引き起こす。この次元がない |
| **戦略的受動撤退ドクトリン**（クトゥーゾフ：決戦を避け、領土を捨て、冬将軍と補給線崩壊で敵を滅ぼす） | SPW-3 の GuerrillaDoctrineRules は非正規小部隊のインフラ破壊。通常の正規軍が**戦略的に決戦を拒否し、空間と時間を売ってCulminatingPointを誘発する**ドクトリンがない |

**結論**：「戦争と平和」は当プロジェクトに**4つの欠落軸**を与える。
①焦土作戦（自領破壊の意思決定） ②侵攻深度×抵抗逓増（自動的な縦深防御ボーナス）
③大規模会戦の規模限界（兵員数→非線形摩擦） ④正規軍の受動撤退ドクトリン（空間を時間に換える）。
いずれも既存モジュールへの **additive 接続**で実装できる。

---

## 1. 役に立つ視点（要約）

「戦争と平和」の世界観を**本システムに効く形**で1行ずつ：

1. **「撤退は戦略である」**——領土を捨てることで補給線を引き伸ばし、敵を自壊させる。`CulminatingPointRules`(SUN-4) の**攻撃側**を制御するのが防御側の能動的選択肢。
2. **「燃やしてから去れ」**——モスクワ放棄のように、敵に渡す前に資源を破壊する。敵の `ForageRules`/`DepotRules` を無力化する最後の手段。
3. **「深く入れ入るほど、周囲が敵になる」**——侵攻深度が上がるほど占領省の抵抗は自動で増し、補給コストも増す。外部の工作がなくても深侵攻は詰まる。→ 縦深防御の数値化。
4. **「25万人が戦場に立つとき、将軍は観客になる」**——大規模会戦ほど計画は崩れる。`FrictionRules` に兵員数因子を追加する補完（CLZ-1 の拡張）。
5. **「個人は歴史の波に乗るだけだ」**——ナポレオンやクトゥーゾフが「自分が決めた」と思っている瞬間、実は状況が彼らを動かしている。→ `DisclosureLedger` への lore 入力（コード新設なし）。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`FrictionRules`（CLZ）/`CulminatingPointRules`（SUN-4）/`InsurgencyRules`（SPW-2）/`GuerrillaDoctrineRules`（SPW-3）/`ResourceProductionRules` を作り直さない**。WAP はそれらに**欠落軸を足し接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・「戦争と平和」の signature）

#### WAP 焦土作戦（`ScorchedEarthRules`/`ScorchedEarthState`）

- **自領土の意図的破壊**：撤退前に Province の資源を燃やす能動的政策。
  - `ScorchedEarthState`（`level` 0..1 / `recoveryRate`）：焦土化の程度と自然回復速度。
  - `ScorchedEarthRules`（static）：
    - `Apply(province, intensity, dt)` → level 上昇（実行強度で速度変化）。
    - `Tick(state, dt)` → level 自然減衰（占領後の再建=速い / 自領再建=遅い）。
    - `ProductionFactor(state)` → 0.0〜1.0（level=1 ならほぼゼロ産出）。
    - `ForageFactor(state)` → 現地調達係数を低下（ForageRules.ForageCapacity への修正子）。
    - `StabilityCost(level)` → 自国省の安定度ペナルティ（「自分の土地を燃やす」政治コスト）。
  - **基準値非破壊**：`Province.stability`/`ProductionFactor` を直接書き換えず、修正子として `GovernanceRules.Tick`/`ResourceProductionRules.Produce` に注入（実効値パターン）。
- **戦略的意義**：CRV の `DepotRules` と `ForageRules`(SUN-3) を無力化する対抗手段。「補給デポを設置しようとしたら焦土で使えない」——侵攻側の補給半径を縮める最後の切り札。
- 接続：`GovernanceRules`（安定度コスト）×`ResourceProductionRules`（産出係数）×`ForageRules`（現地調達係数）×`ResourceStockpile`（消費=破壊コスト）。
- **純ロジック test-first・EditMode テスト必須**。

#### WAP 侵攻深度×抵抗逓増（`HomelandResistanceRules`/`InvasionDepthState`）

- **縦深防御の数値化**：外部介入なしに、敵が自領土に深く侵入するほど占領コストと補給負担が自動で増す。
  - `InvasionDepthState`（`faction` / `maxDepth`=勢力の最奥補給システムからの最大Dijkstraホップ数 / `depthPerSystem` Map）。
  - `HomelandResistanceRules`（static）：
    - `ComputeDepth(province, map, faction)` → 侵攻側補給起点から province への距離（ホップ数）。
    - `ResistanceFactor(depth, params)` → 0..1（`params.slopePerHop` で深度比例増加・上限 `maxResistanceFactor`=0.5）。
    - `SupplyCostMultiplier(depth, params)` → 1.0〜`maxCostMultiplier`（深い侵攻ほど補給コスト増）。
    - `InsurgencyBonus(depth, params)` → InsurgencyState.level の自動積み増し量（SPW-2 の外部組織化と**別系統**で積算・二重カウント注意=加算のみ）。
  - **起点**：`GalaxyMap.Neighbors` + `GalaxyPathfinder.FindPath` で距離を算出（純ロジック・Unity 非依存）。
- 接続：`SupplyRules`（補給コスト乗算）×`InsurgencyRules`（SPW-2 level 加算）×`GalaxyPathfinder`（距離計算）×`GovernanceRules`（抵抗修正子）。
- **純ロジック test-first・EditMode テスト必須**。

### ★★ 高（既存への接続補強）

#### WAP 大規模会戦の規模限界（`MassEngagementRules`）

- **兵員規模→非線形摩擦**：CLZ-1 `FrictionRules` は命令階層の「深さ」を扱う。WAP は「絶対規模（総交戦兵員数）」が閾値を超えると摩擦が急増するという別次元。
  - `MassEngagementRules`（static）：
    - `TotalEngagedStrength(fleets)` → 交戦中全旗艦の strength 合計。
    - `MassFrictionFactor(totalStrength, params)` → 1.0〜`maxMassFactor`（`params.threshold` 超えから指数増加）。
    - `ApplyToFriction(existing, totalStrength)` → `FrictionRules.FrictionFactor` に乗算（additive 合成）。
  - **CLZ-1 の拡張**（作り直しではない）：`FrictionRules` の呼び出し側（`BattleManager` 等）が `ApplyToFriction` を追加で乗算するだけ。
- 接続：`FrictionRules`（CLZ-1 の乗算拡張）×`BattleManager`（交戦兵員数集計）。
- 純ロジック・EditMode テスト必須。

#### WAP 戦略的受動撤退ドクトリン（`TradeSpaceForTimeRules`）

- **正規軍が決戦を拒否し、領土と時間を換算する**：SPW-3 の GuerrillaDoctrineRules は非正規小部隊向け。WAP はフル正規軍が**戦略的に**撤退を選択し、攻勢終末点（SUN-4）の到来を待つドクトリン。
  - `TradeSpaceForTimeRules`（static）：
    - `enum WithdrawalStance { 通常交戦, 段階的撤退 }`。
    - `ShouldWithdraw(fleet, map, campaign)` → 保有戦力/敵戦力比・過伸張度・季節要因から「撤退継続」か「決戦」かを判定（返値 bool）。
    - `WithdrawalTarget(fleet, map)` → 補給が最も充実した自領土の安全星系を撤退先として返す（`GalaxyPathfinder` 活用）。
    - `AttritionGainRate(overextension, stance)` → 撤退ドクトリン採用中の消耗逓増率（敵の overextension × 自領侵攻深度が大きいほど高い）。
    - `IsExhaustedInvader(fleet, params)` → 攻勢終末点（SUN-4 `CulminatingPointRules` の係数）が閾値を超えた侵攻側を検知。
  - 戦略AIへの接続：`GalaxyView`/`StrategyRules` の AI 行動選択で `ShouldWithdraw` を参照し、段階的撤退 AI を実現（最小実装：撤退ターゲット指定のみ・戦略UIは後段）。
- 接続：`CulminatingPointRules`（SUN-4 overextension 参照）×`GalaxyPathfinder`（撤退先探索）×`HomelandResistanceRules`（WAP-2 の逓増コスト）×`StrategyRules`。
- 純ロジック・EditMode テスト必須。

### ★ 中（世界観 lore・コード新設なし）

#### WAP（lore）歴史哲学「英雄と歴史の力」開示データ

- トルストイの第二エピローグ：「偉人は歴史を作らない、歴史が偉人を作る」。
- ゲーム世界では：「主人公はナポレオンではなく時代の渦の中を生きた一人」「戦略の巨人と呼ばれた者も、実は集団的な力の結節点だった」という視点を秘史として開示。
- 接続：**コード新設なし**。`DisclosureLedger`（FND-4）への **lore データ入力**として、既存の開示エンジンに乗せる。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| パルチザン遊撃戦の能動組織化 | **SPW-2 InsurgencyRules がカバー**。WAP は depth-proportional な自動増幅（WAP-2）として接続 |
| 遊撃戦ドクトリン（非正規小部隊） | **SPW-3 GuerrillaDoctrineRules がカバー**（WAP-4 は正規軍の戦略的撤退で別物） |
| 回廊妨害・補給線切断 | **SPW-1 CorridorSabotageRules がカバー**。WAP-1 は自領土の破壊で方向が逆 |
| 摩擦・作戦計画の劣化 | **CLZ-1 FrictionRules がカバー**（WAP-3 は兵員規模次元の追加拡張のみ） |
| 攻勢終末点（過伸張） | **SUN-4 CulminatingPointRules がカバー**（WAP-4 はその「誘発戦略」として接続） |
| 防御側の補給優位 | **CRV-3 SupplyModeRules がカバー**（WAP-2 はそれに depth 次元を加える） |
| 冬将軍（季節・環境要素） | 宇宙空間設定に直接適用困難。環境ハザードとしての抽象化は既存の BlackHole/SiegeArena で十分 |
| 大国の興亡・過剰拡張 | **KEN（ケネディ）#1321 がカバー** |
| 間接アプローチ | **LDH #1338 がカバー** |

---

## 3. EPIC #WAP の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 戦略盤面/UIへ配線。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #1407**。GitHub issue 起票済み（#1410〜#1423）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **WAP-1** | #1410 | 焦土作戦（`ScorchedEarthRules`/`ScorchedEarthState`：自領土破壊→敵の現地調達/デポ無効化） | `GovernanceRules`×`ResourceProductionRules`×`ForageRules`(SUN-3)×`DepotRules`(CRV-1) |
| **WAP-2** | #1413 | 侵攻深度×抵抗逓増（`HomelandResistanceRules`/`InvasionDepthState`：深く入るほど補給コスト増・反乱自動増幅） | `SupplyRules`×`InsurgencyRules`(SPW-2)×`GalaxyPathfinder`×`GovernanceRules` |
| **WAP-3** | #1417 | 大規模会戦の規模限界（`MassEngagementRules`：総兵員数→CLZ-1 FrictionRules の乗算拡張） | `FrictionRules`(CLZ-1)×`BattleManager`×`CombatModifiers` |
| **WAP-4** | #1421 | 戦略的受動撤退ドクトリン（`TradeSpaceForTimeRules`：正規軍が決戦を拒否し攻勢終末点を誘発） | `CulminatingPointRules`(SUN-4)×`GalaxyPathfinder`×`HomelandResistanceRules`(WAP-2)×`StrategyRules` |
| **WAP-5** | #1423 | （lore）歴史哲学「英雄と歴史の力」開示データ（`DisclosureLedger`へ、コード新設なし） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順

`WAP-1`（焦土＝最も固有・既存補給戦術の対抗手段）→ `WAP-2`（侵攻深度=焦土と連動して縦深防御を完成）→ `WAP-4`（戦略ドクトリン＝WAP-1/2 の純ロジックを前提に戦略AIへ配線）→ `WAP-3`（大規模会戦=CLZ-1 拡張・単体でも追加可能）→ `WAP-5`（lore）。

> いずれも既存ロジックを**後退させず接続**する additive 設計。焦土(WAP-1)+深度抵抗(WAP-2)+撤退ドクトリン(WAP-4) で「クトゥーゾフ戦略」が再現可能になる。
