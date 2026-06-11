# T.E.ロレンス『知恵の七柱』参考設計（EPIC #SPW）

> 参照元：T.E.ロレンス著『知恵の七柱』。第一次世界大戦中の対オスマン遊撃戦の回想録。
> 小数の不正規軍が鉄道・補給線を標的に大軍を足止めし、占領地の反乱を組織した非対称戦の原典。
> 本ドキュメントは、当プロジェクト（Ginei）に**役立つ構造パターンのみ**を抽出し、EPIC `#SPW` として issue 化する提案。
> **著作権注意**：固有名・文章・キャラクター・固有設定は流用せず、**遊撃戦／反乱組織化／連合の構造パターンのみ**を参考にする。

---

## 0. なぜ「知恵の七柱」が本システムに役立つか

当プロジェクトは補給・反乱・外交の純ロジックを保有しているが、それらはいずれも**受動的・均衡的な視点**から設計されている：

| 既存（カバー済み） | カバー範囲 |
|---|---|
| `SupplyRules`（#94） | 補給線の有無・補給切れで前線が枯渇（**受動的遮断**） |
| `CommerceRaidingRules`（#95） | 護衛vs襲撃の通商破壊（**護送船団の破壊**） |
| `GovernanceRules.RebelPressure`（#109） | 時間と不満で有機的に反乱圧力が上がる（**有機的・受動的**） |
| `LoyaltyRules`/`Allegiance`（#817） | 忠誠・調略・寝返りカスケード（**現在の立場**の解決） |
| `EspionageRules` | 情報収集・妨害・発覚リスク（**情報系**） |
| `ConsentRules`/`Polity`（#836） | 被支配者の非協力・ボイコット（**内部からの離脱**） |
| `AutonomyRules`/`CommandDoctrine`（#544） | 自律分散指揮（**集団依存vs自律分散の選択**） |
| `WarGoalRules`（#192 DIP-3） | 厭戦・講和受諾（**戦争目標の正統性**） |

**しかし、ローレンスが固有に描く以下が欠けている**：

| 『知恵の七柱』が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **回廊サボタージュ**（占領せず鉄道を爆破して去る） | 占領なしで回廊を一時的に機能不全にする仕組みが無い。`IsFtlBlocked`は所有者の問題；攻城は占領を目指す。**「破壊して去る」一時遮断**が無い |
| **占領地反乱の能動組織化**（外部から扇動・資金・人員を送る） | `GovernanceRules.RebelPressure`は有機的（時間と不満で上がる）。**外部勢力が意図的に圧力を注入する**窓口が無い |
| **遊撃戦ドクトリン**（正面回避・補給線集中）の純ロジック | `CommandDoctrine`は集団依存vs自律分散。**「戦わない戦い方＝インフラ標的モード」**（交戦回避+破壊優先）の戦略運用モードが無い |
| **連合内の隠れた目標乖離**（同じ敵に向かいながら戦後目標が相容れない） | `LoyaltyRules`は「現在、誰のために戦うか」を解決する。**同盟内に潜む戦後の利益相反**（英国の約束 vs アラブ独立）をモデルできない |
| **長期遊撃戦の指揮官消耗**（蓄積する疲弊で効果が落ちる） | `GrowthRules`は上昇軌道；`RetirementRules`は構造的終焉；`LifecycleRules`は加齢。**一時的・回復可能な運用消耗**（「動けるが効果が落ちる状態」）が無い |

**結論**：ローレンスは当プロジェクトに「**能動側の非対称戦**」という欠落軸を5本与える。いずれも既存システムへの**additive 接続**であり、作り直しは不要。特に「回廊サボタージュ」と「反乱組織化」はゲームの戦略的選択肢を大きく広げる。

---

## 1. 役に立つ視点

1. **「戦場は戦略地図上の空間、兵士は移動コスト」**――ローレンスは「正面から殺すな、補給線を切れ」と考えた。`SupplyRules` に**能動的な切断者**の概念を加える。
2. **占領地の民心は外部から着火できる**――外部勢力が資源と諜報を投じることで、有機的な反乱圧力を増幅できる。`GovernanceRules.RebelPressure` の**能動版**。
3. **小数精鋭が大軍を封じるのは補給線の攻撃による**――遊撃戦の本質は戦闘回避＋インフラ破壊の組み合わせ。`CommerceRaidingRules` の**陸路（回廊）版**。
4. **同盟は「今」を結ぶが、「戦後」が相容れない**――共通の敵が消えた瞬間に連合は崩壊する。`WarGoalRules` に**連合内の潜在的目標乖離スコア**を足す。
5. **英雄は消耗する**――長期遊撃戦の指揮官は勝利と引き換えに何かを失う。`AdmiralData` 能力パイプラインに**運用消耗の減衰係数**を接続する。
6. **「約束された独立」は国家の正統性を問い直す**――戦後の裏切りは`DynastyRules`/`ConsentRules`のloreとして開示エンジンに乗せる。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`SupplyRules`/`GovernanceRules`/`LoyaltyRules`/`EspionageRules`/`WarGoalRules` を作り直さない**。SPW はそれらに**欠落軸を接続するだけ**（additive）。

### ★★★ 最優先（真の欠落・ローレンスの signature）

#### SPW 回廊サボタージュ（`CorridorSabotageRules`/`CorridorSabotageState`）

- **破壊して去る**：星系を占領せずに回廊を一定 game-time 機能不全にする。  
  `SabotageState`（`level`0..1 / `decayRate`）。`Apply(state, force, dt)`→level 上昇 / `Tick(state, dt)`→level 減衰 / `IsDisrupted(state, threshold)`→bool。  
- **戦略効果**：disrupted 時は `SupplyRules.SuppliedSystems` で補給遮断扱い。FTL 速度も低下（遅延コスト）。占領や攻城とは独立した第三の回廊干渉手段。  
- 接続：`StrategyRules.IsFtlBlocked`（check に SabotageState も加算）×`SupplyRules`×`GuerrillaDoctrineRules`（SPW-3 が実行窓口）。EditMode テスト必須。

#### SPW 占領地反乱組織化（`InsurgencyRules`/`InsurgencyState`）

- **能動的反乱扇動**：外部勢力が敵占領地に資源と諜報を投じて`GovernanceRules.RebelPressure`を増幅する。  
  `InsurgencyState`（`level`0..1 / `sponsorFactionName`）。`Invest(state, investment, espionageQuality)`→level 上昇（投資に対数的 ROI）/ `Tick(state, province, dt)`→level 減衰・安定度修正子を返す / `StabilityModifier(state)`→安定度ペナルティ係数。  
- **基準値非破壊**：`Province.stability` を直接変えず、`GovernanceRules.Tick` の修正子として注入する（実効値パターン）。  
- 接続：`GovernanceRules`×`EspionageRules`（諜報品質を質因子）×`ResourceStockpile`（投資コスト）。EditMode テスト必須。

### ★★ 高（戦略的選択肢の拡張）

#### SPW 遊撃戦ドクトリン（`GuerrillaDoctrineRules`/`enum OperationalMode`）

- **戦わない戦い方**：戦略艦隊の運用モードに `遊撃` を追加。交戦回避＋回廊妨害を優先するAI挙動を定義する純ロジック。  
  `enum OperationalMode { 通常, 遊撃 }`。`EngageProbability(mode, strengthRatio)`→遊撃は常に低確率 / `SabotagePotential(mode, strength)`→遊撃モードの時間あたり妨害量 / `AttritionRate(mode)`→遊撃は敵中での損耗が速い（リスク付き）。  
- 接続：`CorridorSabotageRules`（SPW-1）の実行量を算出 / `FleetAI`/`StrategyRules`（戦略レイヤー配線は後段）。EditMode テスト必須。

#### SPW 連合の隠れた目標乖離（`AllianceDivergenceRules`/`AllianceDivergence`）

- **同盟内の潜在的利益相反**：`LoyaltyRules`が「今誰のために戦うか」を解くのに対し、**「戦後に何を求めるか」の乖離**をモデルする。  
  `AllianceDivergence`（`hiddenConflict`0..1 per faction-pair）。`DriftConflict(state, goalA, goalB, dt)`→目標の差が大きいほど乖離が成長 / `BetrayalRisk(state, warOutcome)`→戦争終結時の裏切りイベント確率 / `ResetOnSharedVictory(state)`→完全勝利で乖離をリセット。  
- 接続：`WarGoalRules`（目標乖離度の入力）×`DiplomacyState`（`BreakTreaty` の引き金へ）×`EventEngine`（裏切りイベント）。EditMode テスト必須。

### ★ 中（補完・世界観lore）

#### SPW 長期遊撃戦の指揮官消耗（`CommanderBurdenRules`/`CommanderBurdenState`）

- **回復可能な運用疲弊**：`GrowthRules`（上昇）・`LifecycleRules`（加齢）とは別の、一時的かつ回復可能な能力低下。長期遊撃戦や占領地指揮で蓄積し、後方に退くと回復する。  
  `CommanderBurdenState`（`burden`0..1）。`AccrueBurden(state, mode, dt)`→遊撃モードで速く蓄積 / `RecoverBurden(state, dt)`→後方・非戦闘で緩やかに回復 / `EffectivenessMultiplier(state)`→burden が高いほど全能力が低下（基準値非破壊）。  
- 接続：`AdmiralData.Effectivexxx`（乗算修正子として注入）×`GuerrillaDoctrineRules`（遊撃モード時に加速）×`ProtagonistRules`（主人公は消耗が特に表現される）。EditMode テスト必須。

#### SPW （lore）世界観の開示データ

- **「遊撃戦の勝利は何をもたらすか」「約束された独立の相克」「英雄の代償」**をloreデータとして`DisclosureLedger`に追加。コード新設なし。  
- 接続：`DisclosureLedger`（FND-4）。秘史系世界観EPIC（天井CAP/エンディング）と連鎖条件で接続可能。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 鉄道・物理地形の詳細モデリング | `GalaxyMap`の回廊トポロジーで代替可能 |
| 部族制度・部族法の専用システム | `FactionData`/`LoyaltyRules`で代替可能 |
| 宗教×民族連合の新設 | `ReligionRules`×`DiplomacyRules`×`CultureRules`がカバー |
| 諜報・情報収集のゲーム化 | `EspionageRules`がカバー（`InsurgencyRules`の質因子として接続のみ） |
| 心理戦・プロパガンダ新設 | `NonviolenceRules.Repress`/`EventEngine`（風説イベント）でカバー |
| 傭兵・非国家武装勢力の専用システム | `FleetUnitData`/`ShipRole`で役割を付与すれば代替可能 |

---

## 3. EPIC #SPW の子Issue（採用分のみ・着手順）

> 純ロジックはTestHarness/EditModeで先に固定（test-first）→盤面/UIへ配線。
> **著作権注意**：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #1382**。GitHub issue 起票済み（#1390〜#1401）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **SPW-1** | #1390 | 回廊サボタージュ（`CorridorSabotageRules`/`CorridorSabotageState`：占領なしで回廊を一時遮断） | `StrategyRules.IsFtlBlocked`拡張×`SupplyRules`。破壊して去る第三の回廊干渉手段 |
| **SPW-2** | #1394 | 占領地反乱組織化（`InsurgencyRules`/`InsurgencyState`：外部からの扇動で`RebelPressure`を増幅） | `GovernanceRules`（修正子注入・基準値非破壊）×`EspionageRules`×`ResourceStockpile` |
| **SPW-3** | #1396 | 遊撃戦ドクトリン（`GuerrillaDoctrineRules`/`enum OperationalMode`：交戦回避＋回廊妨害モード） | SPW-1の実行量算出×`FleetAI`/`StrategyRules`への接続インターフェイス |
| **SPW-4** | #1398 | 連合の隠れた目標乖離（`AllianceDivergenceRules`/`AllianceDivergence`：戦後の利益相反スコア） | `WarGoalRules`×`DiplomacyState.BreakTreaty`×`EventEngine`（裏切りイベント） |
| **SPW-5** | #1400 | 長期遊撃戦の指揮官消耗（`CommanderBurdenRules`：一時的回復可能な能力低下・運用疲弊） | `AdmiralData.Effectivexxx`乗算修正子×`GuerrillaDoctrineRules`×`ProtagonistRules` |
| **SPW-6** | #1401 | （lore）遊撃戦の世界観開示データ（「約束された独立」「英雄の代償」を`DisclosureLedger`へ） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`SPW-1`（回廊サボタージュ＝最も固有・既存`SupplyRules`に直結）→ `SPW-2`（反乱組織化＝`GovernanceRules`の能動版）→ `SPW-3`（遊撃ドクトリン＝両方の実行側）→ `SPW-4`（連合乖離＝外交層の深化）→ `SPW-5`（指揮官消耗）→ `SPW-6`（lore）。

> いずれも既存純ロジックを**後退させず接続**するadditive設計。`SupplyRules`/`GovernanceRules`の能動側を補完し、非対称戦の選択肢を戦略レイヤーに与える。
