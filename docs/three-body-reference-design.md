# 劉慈欣『三体』参考設計（EPIC #TBP）

> 参照元：劉慈欣『三体』三部作（三体 / 黒暗森林 / 死神永生）。
> 宇宙社会学の公理「生存は文明の第一需要であり、物質は有限である」から導かれる**暗黒森林則**——存在を知られた文明は消滅する。沈黙こそ生存、接触は相互破壊。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略＋既に大規模な政治・経済・社会シミュ層）にとって**役に立つ視点**だけを抽出し、EPIC `#TBP` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**メカニクス／世界観の構造パターンのみ**を参考にする。

---

## 0. なぜ「三体」が本システムに役立つか

当プロジェクトはゲーム理論・外交・研究・諜報の**純ロジック層を大量に保有**している：

| 既存（カバー範囲） | カバー範囲 |
|---|---|
| `GameTheoryRules`（#388） | 囚人のジレンマ/Nash均衡/TitForTat/ゼロサム判定 |
| `DiplomacyRules`/`DiplomacyState`（DIP-1 #189） | 外交状態遷移・opinion修正子・`IsHostile` 駆動 |
| `WarGoalRules`＋`CasusBelli`（DIP-3 #192） | 厭戦/戦争目標/講和受容 |
| `EspionageRules`/`SpyNetwork` | 情報収集/妨害/発見リスク |
| `ResearchRules`/`ResearchProject`（#123-127） | 研究進捗/産出/政体偏り |
| `LogisticsRules`（#844） | 版図一体化度・連結成分 |
| `EventEngine`（#116） | 条件発火→通知/選択肢→効果 |
| `DisclosureLedger`（FND-4 #495） | 秘史開示・条件連鎖開示 |

**しかし、これらは「既知の敵との合理的ゲーム」を前提としており**、三体が固有に描く以下が**欠けている**：

| 三体が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **疑猜鎖（Chain of Suspicion）** | `GameTheoryRules` は既知の利得表。**善意でも崩れる再帰的不信**（「相手も私が疑っていると知っている」という無限後退）が無い |
| **技術の不連続跳躍** | `ResearchRules` は線形進捗。**技術ブレークスルーによる突然の軍事パラダイム転換**（既存戦力が一夜で陳腐化）が無い |
| **危機紀／安定紀の環境サイクル** | `EventEngine` は単発イベント。**複数年続く「危機紀」（生産全削減・戦争確率上昇）と「安定紀」の交代サイクル**が無い |
| **文明の露出リスク** | `EspionageRules` は「スパイが捕まるリスク」。**勢力全体の「存在の可視性」——外交発信・艦隊行動が外部からの標的化を招く**——が無い |
| **恒星系廃棄戦略** | `StrategyRules` は占領か撃滅。**奪われる前に経済価値ごと破壊する焦土戦略**（勝ち方でなく「奪わせない」）が無い |
| **救生艇トリアージ（文明的緊急脱出）** | `ColonizationRules` は平時の入植。**壊滅状況での脱出優先順位決定**——誰を・何を・どの順で——の倫理ルールが無い |

**結論**：三体は当プロジェクトのゲーム理論層に**「接触そのものが危険な宇宙」という非対称不確実性の次元**と、①疑猜鎖 ②技術跳躍 ③危機紀サイクル ④存在露出 ⑤焦土戦略 ⑥救生艇倫理 という6つの欠落軸を与える。いずれも**既存モジュールに additive に接続**でき、コード重複なく実装可能。

---

## 1. 役に立つ視点（要約）

三体三部作の世界観を、**本システムに効く形**で1行ずつ：

1. **「接触は相互破壊」＝外交に命がけのコストが生じる**。交渉するほど自分の存在を知らせ、標的になる。→ `DiplomacyRules` の積極外交に「露出リスク」という逆インセンティブを与える。
2. **疑猜鎖＝善意の双方でも戦争になる**。「あなたが私を脅威と思うか不明」→「だから先制する」の再帰論理。→ `DiplomacyRules.DriftOpinion` に**連鎖不信**の動学を足す。
3. **技術跳躍は既存秩序をリセットする**。一夜で全軍事力が陳腐化する急変が可能性として存在する。→ `ResearchRules` に**ブレークスルー確率**と**陳腐化ショック**を加える。
4. **危機紀／安定紀は歴史の構造**。安定する長期→突然の崩壊→長期危機→回復の繰り返し。→ `CalendarDispatcher` と連動する**マクロ環境状態**を導入し世界全体の変数を変える。
5. **焦土＝勝利の第三の形**。占領でも撃滅でもなく「奪った相手が何も得られないよう」破壊する。→ `StrategyRules` に**系廃棄コマンド**を追加（奪還後も復旧コスト残存）。
6. **救生艇倫理は社会契約の極限**。圧倒的危機で「全員を救えない」とき誰を優先するかが政権の正統性と生存を決める。→ `HopeRules`/`ConsentRules` に**緊急脱出優先度ルール**を接続。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**既存の `GameTheoryRules`/`DiplomacyRules`/`ResearchRules`/`EspionageRules` を作り直さない**。TBP はそれらに**欠落軸を足し、接続する**だけ（additive）。

### ★★★ 最優先（三体の signature・真の欠落）

#### TBP 疑猜鎖（Chain of Suspicion / 再帰的不信カスケード）
- **不確実性のネスト**：A が B の意図を知らない → B も A の疑念を知らない → 双方が善意でも先制攻撃が合理解になる。
- 新 `SuspicionChainRules`（純ロジック・test-first）：初期不信度 `suspicion(a→b)` × 情報可視性係数 → カスケード反復 → `threshold` 超えで `IsHostile=true` へ昇格する可能性。`GameTheoryRules.TitForTat` と組み合わせると「先に撃ったほうが勝つ」均衡が生まれる。
- 接続：`DiplomacyRules.DriftOpinion` を駆動する新修正子・`EspionageRules.InfoGain`（情報収集で不信を緩和）。

#### TBP 技術跳躍と陳腐化衝撃（Technology Leap / Power Inversion）
- **ブレークスルー**：`ResearchRules.Tick` に `BreakthroughChance`（研究速度・資源量に比例）を追加。発火すると `ResearchTier` が1段跳躍＝通常進捗の5〜10倍相当。
- **陳腐化ショック**：跳躍した勢力の相手側の`CombatModifiers` に陳腐化係数（1.0→0.6）を一定期間かける。
- 新 `TechLeapRules`（純ロジック・test-first）。接続：`ResearchRules` × `CombatModifiers`（#106） × `EventEngine`（跳躍イベント）。

#### TBP 危機紀／安定紀サイクル（Era State / Environmental Chaos Cycle）
- **マクロ環境状態**：`enum EraPhase {安定紀, 危機紀, 終末紀}`。`EraStateRules` が CalendarDispatcher の年次フックで遷移確率を評価し切り替える。
- 危機紀：全勢力の生産係数 −20%・戦争確率上昇・`DriftOpinion` 加速。安定紀：通常。終末紀：生産 −50%・移民圧力・救生艇シナリオ起動。
- 新 `EraStateRules`＋`EraState`（純ロジック・test-first）。接続：`CalendarDispatcher`（TIME-6）× `GovernanceRules.OutputFactor` × `EventEngine`。

### ★★ 高（重要な欠落・既存に一軸追加）

#### TBP 文明の露出リスク（Civilizational Exposure / Visibility Profile）
- **存在の可視性**：外交発信・艦隊集結・大規模建艦を行うたびに勢力の `exposureLevel`（0..1）が上がる。
- 高 `exposureLevel` は：他勢力の `EspionageRules.InfoGain` ボーナス・`SuspicionChainRules` の初期不信値上昇・逆説的に同盟引き込みリスク増。
- 新 `ExposureRules`（純ロジック・test-first）。接続：`EspionageRules` × `DiplomacyRules` × `FactionRelations`。

#### TBP 恒星系廃棄戦略（System Denial / Scorched Star）
- **焦土コマンド**：占領される前に自勢力星系の施設・生産力を意図的に破壊。`SystemDenialRules.Deny(province)` は `stability`/`integration`/造船所を最低値に設定し `recoveryYears`（回復に要する暦年数）を付与。
- 新 `SystemDenialRules`（純ロジック・test-first）。接続：`GovernanceRules`（`OutputFactor` 低下）× `ShipyardRules`（造船停止）× `StrategyRules`（占領後も回復コスト残存）。

### ★ 中（重要だが他層依存）

#### TBP 救生艇トリアージ（Lifeboat Ethics / Civilizational Triage）
- **緊急脱出優先順位**：`LifeboatRules.PriorityScore(fleet, province)` が軍事力・生産力・人口・忠誠度を重み付けし「何を残すか」を序列化。`EraPhase.終末紀` 時に発火し `ColonizationRules.CanColonize` と連動。
- 新 `LifeboatRules`（純ロジック・test-first）。接続：`HopeRules`（希望崩壊）× `ConsentRules`（統治不能リスク）× `ColonizationRules` × `FleetPoolRules`。

#### TBP（lore）宇宙社会学の公理と暗黒森林（Dark Forest worldview lore）
- **コード新設なし**：`DisclosureLedger`（FND-4）への lore データ入力。
- 開示連鎖：「銀河は沈黙している（Fermi）」→「沈黙の理由＝暗黒森林則の発見」→「自分たちが森の中にいる気づき」→エンディング分岐。
- 接続：`DisclosureLedger` × `EventEngine`（`SuspicionChainRules` の暴走イベントからトリガー）。

### ❌ 不採用（重複・既存で十分・スケール過剰）

| 不採用 | 理由 |
|---|---|
| 次元削減攻撃（2次元化・光速武器） | スケール過剰・ゲームバランス破綻。既存 `BlackHole` 特殊地形で十分 |
| 智子（Sophon）=全通信傍受 | `EspionageRules.InfoGain` + 高 `exposureLevel` で同効果を表現可能。専用実装は重複 |
| 宇宙航行の時間膨張（相対性） | 物理シミュレーション範囲外。ゲームループが破綻する |
| 三体問題の軌道カオス（天体力学） | 本プロジェクトは星系グラフ（抽象）。物理シミュは対象外 |
| 暗黒森林警告放送（他文明への通報） | 自殺的コマンドはゲームデザイン不整合。`ExposureRules` の高リスクシナリオで代替 |

---

## 3. EPIC #TBP の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UI配線。既存層は接続のみ・重複新設しない。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #2265**。GitHub issue 起票済み（#2267〜#2291）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **TBP-1** | #2267 | 疑猜鎖（善意でも崩れる再帰的不信カスケード）＝新 `SuspicionChainRules` | `DiplomacyRules.DriftOpinion` × `GameTheoryRules` × `EspionageRules` |
| **TBP-2** | #2271 | 技術跳躍と陳腐化衝撃（ブレークスルー→一夜で軍事パラダイム転換）＝新 `TechLeapRules` | `ResearchRules`（#123-127）× `CombatModifiers`（#106）× `EventEngine` |
| **TBP-3** | #2274 | 危機紀／安定紀サイクル（マクロ環境状態 `EraState` ＝年次で遷移）＝新 `EraStateRules` | `CalendarDispatcher`（TIME-6）× `GovernanceRules` × `EventEngine` |
| **TBP-4** | #2278 | 文明の露出リスク（外交発信・艦隊行動→可視性↑→標的化リスク）＝新 `ExposureRules` | `EspionageRules` × `DiplomacyRules` × `FactionRelations` |
| **TBP-5** | #2281 | 恒星系廃棄戦略（焦土＝奪われる前に系を破壊・回復コスト残存）＝新 `SystemDenialRules` | `GovernanceRules` × `ShipyardRules` × `StrategyRules` |
| **TBP-6** | #2287 | 救生艇トリアージ（終末紀の脱出優先順位決定）＝新 `LifeboatRules` | `HopeRules` × `ConsentRules` × `ColonizationRules` × `FleetPoolRules` |
| **TBP-7** | #2291 | （lore）宇宙社会学の公理と暗黒森林——`DisclosureLedger` データ入力（コード新設なし） | `DisclosureLedger`（FND-4）× `EventEngine`（SuspicionChainRules 暴走から連鎖） |

### 推奨着手順
`TBP-1`（疑猜鎖＝三体最大の signature。外交システム全体の前提を変える）→ `TBP-2`（技術跳躍＝戦略バランスの撹乱因子）→ `TBP-3`（危機紀サイクル＝TBP-1/2 の環境トリガー）→ `TBP-4`（露出リスク＝TBP-1 の初期値を供給）→ `TBP-5`（焦土＝危機紀に使われる戦略オプション）→ `TBP-6/7`（救生艇/lore＝TBP-3 終末紀の応答）。

> いずれも既存純ロジックを**後退させず接続**する additive 設計。
