# 『老子』参考設計（EPIC #LAOZ）

> 参照元：『老子』（老聃・道徳経）。「道（タオ）」に従う自然的秩序・無為の治・柔弱が剛強に勝る——「水のように争わず、万物を利する」統治哲学。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略＋既存の統治/社会シミュ純ロジック層）にとって**役に立つ視点**だけを抽出し、EPIC `#LAOZ` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**統治哲学のメカニクス構造パターンのみ**を参考にする。

---

## 0. なぜ『老子』が本システムに役立つか

当プロジェクトは統治・社会の**既存純ロジックを大量に保有**している：

| 既存（統治・社会シミュ） | カバー範囲 |
|---|---|
| `GovernanceRules.OutputFactor` | 安定度→産出（安定が高いほど生産力UP） |
| `ConsentRules`/`Polity`（ガンジー） | 統治力＝直接戦力＋協力×人口。協力が引き下げられれば統治不能 |
| `DynastyRules`/`Regime`（孔子） | 腐敗が徳の分だけ遅く進み正統性を蝕む→天命喪失 |
| `FocusRules`（空海） | 身・口・意の同期→出力倍率（集中の哲学） |
| `NonviolenceRules`/`Movement`（ガンジー） | 非暴力抵抗・弾圧の可視化→支持転換 |
| `LogisticsRules.CohesionFactor` | 領土分断→国力割引（過拡張ペナルティ） |
| `AutonomyRules`/`CommandDoctrine` | 集団依存/自律分散ドクトリン |

**しかし、これらはいずれも「やればやるほど効く」線形モデルか、「衰退は直線的」な減少曲線**であり、老子が固有に描く以下が**欠けている**：

| 老子が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **無為の治** — 少介入で自然秩序が出現する | `GovernanceRules`は安定→産出の線形関係。**介入レベルが高すぎると産出が逆に下がる**（逆U字型・非線形）が無い |
| **反者道之動** — 極まれば必ず反転 | `DynastyRules`は直線的腐敗崩壊。「やりすぎると逆効果」の**汎用 tipping-point 曲線**（逆U字）が無い |
| **知足/小国寡民** — 適正規模＝最大安定の正の価値 | `LogisticsRules`は過拡張ペナルティのみ。**意図的小規模維持の自足安定ボーナス**（正側）が無い |
| **柔弱勝剛強** — しなやかさが長期で剛を勝る | `NonviolenceRules`は受動的ボイコット。`CommandDoctrine`は集団依存/自律分散。**短期劣後・長期回復力**という柔軟ドクトリンが無い |

**結論**：老子は当プロジェクトの統治・社会シミュに**①非線形の「過ぎたるは及ばざるが如し」曲線（反者道之動）②無為ガバナンスの逆U字効率③知足による小国の生存力④柔弱ドクトリンの回復力**という4つの欠落軸を与える。既存`GovernanceRules`/`LogisticsRules`/`AutonomyRules`に**multiplicative に接続するだけ（additive 設計）**で、統治スタイルの軸を「有為（強制・徴税・軍拡）vs 無為（自然・撤退・小規模）」として機能させる。

---

## 1. 役に立つ視点（要約）

老子の世界観を、**本システムに効く形**で1行ずつ：

1. **「治大國若烹小鮮」** — 大国を治めるのは小魚を煮るようなもの（いじりすぎない）。→ 行政介入レベルに*逆U字型*を持たせる：最適点を超えると産出が下がる（`WuWeiRules`）。
2. **「反者道之動」** — 物事は極まれば必ず反転する。強さの極が弱さを生む。→ 任意のパラメータに適用できる**汎用 tipping-point 曲線**（`ReversalRules`）。税収・軍拡・圧政など横断的に利く。
3. **「知足者富」** — 足るを知る者が真の富者。小国寡民が理想。→ 領土が適正規模以下なら**自足安定ボーナス**（`ContentmentRules`）。征服でなく存続を選ぶ戦略的合理性。
4. **「柔弱勝剛強」** — 柔らかく弱きものが、硬く強きものに最終的に勝つ。→ 柔軟ドクトリン採用部隊は**短期火力劣後・長期士気回復速度UP**（`WaterDoctrineRules`）。
5. **「上善若水」** — 最善は水のごとく。争わず低きに就き万物を利す。→ `WaterDoctrineRules`の哲学的基盤。戦略的退却・迂回を*弱さ*ではなく*選択された強さ*として設計。
6. **「為學日益、為道日損」** — 学は積み上げ、道は削り落とす。→ 無為ガバナンスは「施策を足す」のではなく「障害を取り除く」。`WuWeiRules`のフレーミング。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`GovernanceRules`/`LogisticsRules`/`DynastyRules`/`AutonomyRules` を作り直さない**。LAOZ はそれらに**欠落した非線形・柔軟軸を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・老子の signature）

#### LAOZ 無為ガバナンス — `WuWeiRules`（少介入→自然安定・介入過剰ペナルティ）

- **逆U字型効率曲線**：介入レベルが低すぎると安定度維持できず、高すぎると産出ペナルティ。最適点（「烹小鮮」の火加減）で最大効率。
- `WuWeiRules`(static, Core)：`OutputFactor(interventionLevel, naturalStability)` — 介入 ≤ 最適点：自然安定を引き上げボーナス；超過：ペナルティ乗算。`OptimalIntervention(province)` — 自然安定（統合度×思想一致）から最適介入水準を返す。
- 接続：`GovernanceRules.OutputFactor` に `ModifierStack.Mul` で積む。`FactionData.governanceStyle`（minimal/moderate/maximal）で介入レベルを決める。

#### LAOZ 反者道之動 — `ReversalRules`（汎用 tipping-point 逆U字曲線）

- **どんなパラメータも極めれば反転**：税率・軍拡・行政介入・圧政いずれも最適点を超えると*逆効果*に転じる数学構造。
- `ReversalRules`(static, Core)：`Factor(value, peak, fallRate)` — value ≤ peak → 1.0+上昇分（または固定1.0）；超過 → `1 - (value - peak) * fallRate`（Clamp 0 下限）。`IsOverpeak(value, peak)` / `ExcessRatio(value, peak)`。
- 接続：`FiscalRules.TaxBurdenPenalty`（ラッファー曲線の整理）、`FactionStateRules.Tick`（軍事化過剰→正統性低下）、`WuWeiRules.OutputFactor`（介入過剰ペナルティの実装）。`ModifierStack`経由で各ルールが消費。

### ★★ 高（統治スタイル軸の補完）

#### LAOZ 知足安定 — `ContentmentRules`（適正規模ボーナス・小国寡民の正の価値）

- **適正規模以下の勢力に自足安定ボーナス**：過拡張ペナルティ（`LogisticsRules.CohesionFactor`）の正側を補完する。小国が生き残る構造的根拠。
- `ContentmentRules`(static, Core)：`ContentmentFactor(ownedSystems, idealSize)` — owned ≤ idealSize → ≥ 1.0 ボーナス；超過 → < 1.0（`LogisticsRules`と相補）。`IdealSize(stability, cohesion)` — 安定度×結束から推算。`IsContentZone(owned, ideal)`.
- 接続：`CampaignRules.EffectiveStability` × `ContentmentFactor`。`LogisticsRules.CohesionFactor`（過拡張側）と対で完全なU字安定曲線を形成。

#### LAOZ 柔弱ドクトリン — `WaterDoctrineRules`（短期劣後・長期回復力）

- **「水」のドクトリン**：低きに就き争わない→短期は火力劣後だが敗退後の士気回復が速く長期消耗戦で優位。「剛」ドクトリンは攻勢時強いが閾値超過で脆くなる（`ReversalRules`連携）。
- `WaterDoctrineRules`(static, Core)：`ResilienceFactor(doctrine)` — `CommandDoctrine.Water` で士気回復速度倍率を返す。`BrittlenessFactor(doctrine, momentumLevel)` — 剛ドクトリン×高勢い → `ReversalRules.Factor` で脆化リスク。`DoctrineFirepowerModifier`（水：-、剛：+）。
- 接続：`FleetMorale`（回復速度）×`AutonomyRules.CommandDoctrine`（既存 enum に `Water`/`Rigid` を足す）。

### ★ 中（lore・世界観）

#### LAOZ（lore）世界観の開示データ

コード新設なし。`DisclosureLedger`（FND-4）へのデータ入力のみ：
- 「天下皆知美之為美、斯悪已」（美徳を掲げるとその反転＝偽善が生まれる）
- 「曲則全・枉則直」（曲がれば保たれる・撓めば真っ直ぐになる）
- 「小国寡民」（小さき国の逆説的な生存力）
- 「反者道之動」（反転は道の運動）
- 接続：`EventEngine`（#116）経由でゲーム内の「無為派の政策選択イベント」に本文断片として。

### ❌ 不採用（重複・既存で十分・スコープ超過）

| 不採用 | 理由 |
|---|---|
| 道（Tao）を明示的なゲームパラメータ化 | 道は諸ロジックの*出力*として現れる抽象概念。数値化は哲学の矮小化かつタイクン化 |
| 陰陽の二項対立を全面システム化 | `ReversalRules`の逆U字が構造的に包含する。二項分類はさらなる重複 |
| 小国寡民を独立勝利条件として実装 | 既存`LeadingFaction`×`ContentmentFactor`で十分。新規勝利条件UIはスコープ超過 |
| 導引・気功の身体修行モデル | `FocusRules`（空海の三密#872）が「身・口・意」として既にカバー |
| 神仙思想・不老不死の長寿キャラ | `LifecycleRules`の例外処理→複雑化。エンディング系世界観EPICで扱う方が適切 |
| 老子の伝記・人物ドラマ | 固有名/キャラ不使用の制約。世界観loreの参照元として留める |

---

## 3. EPIC #LAOZ の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。既存統治ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**統治哲学のメカニクス構造パターンのみ**参考。

> **EPIC = #1543**。GitHub issue 起票済み（#1546, #1550, #1554, #1558, #1562）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **LAOZ-1** | #1546 | `WuWeiRules` — 無為ガバナンス（少介入→自然安定。介入過剰の逆U字ペナルティ） | `GovernanceRules.OutputFactor`×`ModifierStack`。`FactionData.governanceStyle` |
| **LAOZ-2** | #1550 | `ReversalRules` — 反者道之動（汎用逆U字 tipping-point 曲線） | `FiscalRules`・`FactionStateRules`・`WuWeiRules`の横断ペナルティ基盤 |
| **LAOZ-3** | #1554 | `ContentmentRules` — 知足安定（適正規模ボーナス・`LogisticsRules`の正側補完） | `CampaignRules.EffectiveStability`×`ContentmentFactor` |
| **LAOZ-4** | #1558 | `WaterDoctrineRules` — 柔弱ドクトリン（短期劣後・長期士気回復力） | `FleetMorale`×`AutonomyRules.CommandDoctrine`（Water/Rigid enum 追加） |
| **LAOZ-5** | #1562 | （lore）世界観の開示データ（無為・反転・小国寡民・曲則全） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`LAOZ-2`（ReversalRules＝最も横断的・他ルールの基盤になる）→ `LAOZ-1`（WuWeiRules＝LAOZ-2を消費）→ `LAOZ-3`（ContentmentRules＝独立・`LogisticsRules`と対）→ `LAOZ-4`（WaterDoctrineRules＝LAOZ-2+`FleetMorale`）→ `LAOZ-5`（lore・最後）。

> いずれも既存統治ロジックを**後退させず接続**する additive 設計。`GovernanceRules`/`DynastyRules`/`LogisticsRules` の線形仮定に**非線形の「過ぎたるは及ばざるが如し」曲線**を後付けで加える。
