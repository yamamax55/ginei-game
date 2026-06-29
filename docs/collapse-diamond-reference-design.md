# ダイアモンド『文明崩壊』参考設計（EPIC #1652）

> 参照元：ジャレド・ダイアモンド『文明崩壊——滅亡と存続の命運を分けるもの』。人口増大・環境収奪・エリートの自己隔離が重なると文明は自ら崩壊を「選ぶ」というマクロ分析。
> イースター島・グリーンランドのノルウェー人・マヤ文明など複数の崩壊事例と、生存事例（ティコピア・江戸の森林管理）を比較する歴史分析。
> 本ドキュメントは当プロジェクト（Ginei＝銀英伝風の星間国家戦略）にとって**役に立つ視点**だけを抽出し、EPIC `#1652` として issue 化する提案。
> 著作権注意：固有名・文章・固有設定（書中の具体地名/人名/逸話）は流用せず、**崩壊メカニクス／環境-社会フィードバックの構造パターンのみ**を参考にする。

---

## 0. なぜ「文明崩壊」が本システムに役立つか

### 既存（カバー範囲）

当プロジェクトは政治・人口・財政・社会の崩壊軸を広くカバーしている：

| 既存モジュール | カバー範囲 |
|---|---|
| `DynastyRules`/`Regime`（#867） | 王朝の天命喪失・易姓革命＝政治腐敗による崩壊 |
| `FactionStateRules.IsCollapsing` | 天命喪失/統治不能/組織崩壊/末人の複合崩壊検知 |
| `HopeRules`/`Community`（#852） | 希望の喪失→末人→統治不能 |
| `ConsentRules`/`Polity`（#836） | 合意撤退→統治不能 |
| `DemographicsRules`/`Population`（#153） | 通常の人口動態（コホート・依存率） |
| `ResourceProductionRules`/`SupplyRules`（#92〜95） | 資源産出・補給線・通商破壊 |
| `GovernanceRules`（#109） | 安定度・統合度・産出係数（安定→産出の現状係数） |
| `FiscalRules`（#161） | 財政破綻・債務スパイラル |
| `CollapseRules`/`CivilizationPhase`（PHNX-1 #2264） | 文明フェーズ（Rising/Mature/Declining/Collapsed/Rebuilding）＝崩壊の**骨格フレーム** |
| `LogisticsRules`（#844） | 版図一体化・孤立した星系の国力割引 |

### 文明崩壊が固有に持つ視点 × 当プロジェクトでの欠落

| 作品の構造パターン | 当プロジェクトでの欠落 |
|---|---|
| **環境資本ストックの有限性**（森林/土壌/漁場は使えば減り再生が追いつかない） | `ResourceProductionRules`は安定度比例のフロー産出のみ。**有限なストックそのものが劣化し産出の上限が永続的に下がる**仕組みが無い |
| **過剰人口と収容力**（人口がストックの扶養限界を超えると食料不足→安定崩壊） | `DemographicsRules`は有機的増減のみ。**収容力上限・超過ペナルティ**が無い |
| **環境ティッピング**（ストックが閾値を割ると劣化が非線形に加速し不可逆になる） | `FactionStateRules.IsCollapsing`は人間内因（腐敗/合意崩壊）のみ。**環境由来の崩壊トリガー**が存在しない |
| **自制という勝ち筋**（資源管理・輪作・保護区→ストック再生→持続）| `FactionState.inclusiveness`は収奪↔包摂の政治軸のみ。**環境政策レバー**（収奪vs保全）が無い |
| **エリートの自己隔離**（支配層が環境費用を下層に転嫁し、自らは隔離される→正確な被害情報が届かず対応が遅れる） | `RedistributionRules`は税・再分配のみ。**環境費用の階級別非対称負担**が無い |
| **崩壊5因子の統合診断**（環境破壊/気候変動/敵対者/交易喪失/対応失敗の複合が崩壊を引き起こす） | 既存は各因子を個別管理。**5因子を統合してリスクを読む診断ルール**が無い |

**結論**：ダイアモンド『文明崩壊』は当プロジェクトに**「環境という外因の崩壊軸」**を与える。既存の`FactionStateRules.IsCollapsing`（人間内因＝腐敗/末人/統治不能）に**環境因子を第二の崩壊エンジンとして合流**させ、崩壊を「単因」から「環境×社会の複合」へ深化させる。`CollapseRules`/`CivilizationPhase`（PHNX-1）の崩壊フレームはそのまま使い、**そこへ至る環境経路を追加**する（additive）。タイクン化回避＝高位の決断（伐採率/移民/自制レバー1本）→エンジン駆動→創発的崩壊 or 持続。

---

## 1. 役に立つ視点（本システムに効く形で）

1. **環境は文明の隠れた基盤** ＝ 豊かな生産力は財政でも政治でもなく土地の健全度に支えられている。→ Province の産出に「環境ストック健全度」という上流制約を掛ける（CLPS-1）。
2. **過剰はやがて反転する** ＝ 人口が収容力を超えた文明は食料不足→内乱→崩壊という必然に向かう（マルサスの実例集）。→ `DemographicsRules`×`GovernanceRules` に収容力超過ペナルティを渡す（CLPS-2）。
3. **ティッピングポイントを過ぎると戻れない** ＝ ストックが一定量を割ると再生より劣化が速くなり不可逆になる。→ `FactionStateRules.IsCollapsing` に環境崩壊条件を第5トリガーとして合流（CLPS-3）。
4. **自制した者だけが続く** ＝ 資源を計画的に管理した社会（ティコピア型）は存続し、収奪一辺倒（イースター島型）は滅びた。→ `FactionState` に環境政策1レバーを足し保全→再生の長期勝ち筋を実装（CLPS-4）。
5. **エリートは最後に気づく** ＝ 支配層が高台に住み環境費用を下層に転嫁する限り、危機は表面化しない。合理的エリートが破滅を選ぶ構造的原因。→ 環境費用の非対称負担・隔離係数（CLPS-5）。
6. **崩壊は5因子の複合** ＝ 単一の原因ではなく複数因子の同時悪化が文明を倒す（既存モジュールを集計する診断ルール）。→ `CollapseDiagnosisRules`（CLPS-6）。
7. **孤立×環境劣化は二重苦** ＝ グリーンランドのノルウェー人は孤立した版図で環境が劣化すると交易という救済が効かず道連れになった。→ `LogisticsRules.CohesionFactor`×環境劣化の乗算（CLPS-7）。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`GovernanceRules`/`DemographicsRules`/`FactionStateRules`/`ResourceProductionRules`/`DynastyRules`/`CollapseRules`（PHNX-1）を作り直さない**。CLPS はそれらに**欠落軸を足し接続するだけ**（additive）。タイクン化回避＝高位の政策決断→エンジン駆動→創発的帰結。

---

### ★★★ 最優先（ダイアモンドの署名・真の欠落）

#### CLPS-1 環境資本ストックと再生（`EnvironmentRules`/`EnvironmentStock`）

**概要**：各 `Province` に **再生可能資源ストック** `EnvironmentStock`（健全度 0..1）と再生率を持たせる。抽出（収奪）が再生率を超えるとストックが不可逆に減少し、劣化が進むほど再生率も落ちる（劣化スパイラル）。

- `EnvironmentStock`：`health`（0..1）・`regenerationRate`（健全度比例）・`extractionRate`（FactionState 政策で決定）
- `EnvironmentRules`（static）：
  - `Tick(stock, extractionRate, dt)` → 健全度更新（収奪>再生で不可逆減少）
  - `OutputMultiplier(health)` → 0..1（健全度が落ちるほど産出上限が下がる）
  - `IsDegraded(health)` → 健全度が `degradationThreshold`（既定0.3）を下回る
  - `IsCritical(health)` → 健全度が `criticalThreshold`（既定0.1）を下回る（ティッピング前兆）
- **接続**：`ResourceProductionRules.Produce` の上流に `OutputMultiplier` を掛け算する（安定度係数×環境係数の積）
- **実効値パターン**：基準産出は変えず、環境係数のみ返す（既存公式は非破壊）
- **EditMode テスト必須**（TestHarness）

#### CLPS-2 環境収容力と過剰人口（`CarryingCapacityRules`）

**概要**：環境ストックが養える人口上限（収容力）。`Population.Total` が収容力を超えると**過剰人口ペナルティ**（食料不足→安定度低下→死亡率上昇）。収容力割れまで人口が縮小して均衡（オーバーシュート&コラプス）。

- `CarryingCapacityRules`（static）：
  - `Capacity(stock, systemType)` → 環境ストック×システム類型（農業型は高い）から収容力を算出
  - `OvershootRatio(population, capacity)` → 超過比率（1.0以下＝持続・超えると非線形ペナルティ）
  - `StabilityPenalty(overshoot)` → `GovernanceRules.EquilibriumStability` への加算（負値）
  - `MortalityPressure(overshoot)` → `DemographicsRules` の死亡率係数（収容力の1.5倍で+50%）
  - `CarryingCapacityParams`：`BaseCapacityPerHealth`/`AgricultureBonus`/`OveshootExponent`（調整可）
- **接続**：`EnvironmentRules`（CLPS-1）× `DemographicsRules`（LIFE-3）× `GovernanceRules.EquilibriumStability`
- **EditMode テスト必須**

---

### ★★ 高優先（崩壊エンジンへの合流・自制の実装）

#### CLPS-3 環境ティッピングと崩壊合流（`FactionStateRules` 拡張）

**概要**：ストックが臨界閾値（`CriticalThreshold`）を割ると安定度・希望・人口が**連鎖的・非線形に**落ちる。`FactionStateRules.IsCollapsing`（現状＝天命喪失/統治不能/組織崩壊/末人の4条件）に**「環境崩壊」を第5の崩壊条件として合流**。

- `FactionStateRules.IsCollapsing` 拡張：
  - 第5条件＝`EnvironmentRules.IsCritical(stock) AND OvershootRatio > 1.5`（臨界ストック×重過剰人口）
  - 条件成立時 → `CollapseRules`（PHNX-1）の `Collapsed` フェーズへ遷移（既存パス流用）
- 非線形加速：`IsDegraded` 状態では `DynastyRules` の腐敗増速係数を乗算（内因×環境の相乗効果）
- **接続**：`EnvironmentRules`（CLPS-1）× `CarryingCapacityRules`（CLPS-2）× `FactionStateRules.IsCollapsing` × `CollapseRules`（PHNX-1）
- **EditMode テスト必須**

#### CLPS-4 自制という勝ち筋（`FactionState` 環境政策フィールド）

**概要**：高位の一軸政策レバー**収奪0↔保全1**。保全を選ぶと短期産出は落ちるが**ストックが再生し長期に持続**。収奪は即効だが崩壊へ。`FactionState.inclusiveness`（収奪↔包摂）と同型のパターン。

- `FactionState` に `environmentPolicy`（0.0＝完全収奪 ↔ 1.0＝完全保全）フィールドを追加
- `EnvironmentRules.Tick` の抽出率 = `baseExtractionRate × (1 - environmentPolicy)`（保全が高いほど収奪減）
- 保全↑ → 再生率回復 → `OutputMultiplier` 回復 → 長期産出安定（我慢が報われる）
- **接続**：`FactionState`（統合層）× `EnvironmentRules.Tick`（CLPS-1）× `GovernanceRules`（徳と有為の相互作用）
- **EditMode テスト必須**

---

### ★ 中優先（エリート隔離・複合診断・lore）

#### CLPS-5 エリートの自己隔離と破滅的意思決定（`EliteInsulationRules`）

**概要**：支配層が環境費用から隔離されているほど収奪を続ける合理性が高まる（短期利益→エリートへ・長期被害→全体へ）。隔離が高い社会は自制レバーを引かず崩壊へ向かう＝なぜ合理的エリートが破滅を選ぶかの構造モデル。

- `EliteInsulationRules`（static）：
  - `InsulationFactor(factionState)` → `PowerRules.EffectivePower`×`CapitalRules.ConcentrationDrift`（r>g集中度）から隔離係数0..1
  - `PerceivedEnvironmentalDamage(actualDamage, insulation)` → エリートが認知する環境被害（隔離が高いほど低く見える）
  - `PolicyBias(insulation)` → 隔離係数が高いほど `environmentPolicy` を保全方向に動かしにくい（自制への抵抗）
  - `InsulationDecay(stock)` → ストックが臨界以下になると隔離も維持できず急落（最後に気づく）
- **接続**：`RedistributionRules`（#163 環境費用の非対称）× `CapitalRules`（#917 r>g集中）× `EventEngine`（#116 「警告を無視するエリート」イベント）
- **EditMode テスト必須**

#### CLPS-6 崩壊5因子の統合診断（`CollapseDiagnosisRules`）

**概要**：ダイアモンドの5因子（環境破壊/気候変動/敵対者/交易喪失/対応失敗）を**既存モジュールから読む純関数集計**。新stateを持たず診断スコアだけを返す。

- `CollapseDiagnosisRules`（static・純関数・新state無し）：
  - `EnvironmentScore(stock)` → CLPS-1 の健全度から（低いほど危険）
  - `ClimateScore(eventLog)` → `EventEngine` の気候/天災カテゴリ頻度
  - `HostileScore(diplomacy)` → `DiplomacyRules` の交戦中の敵対数と強度比
  - `TradeScore(logistics)` → `LogisticsRules.CohesionFactor`（孤立度）× `SupplyRules.SuppliedSystems`（補給遮断率）
  - `ResponseScore(policy)` → 自制レバー値（低い=対応失敗）× `EliteInsulationRules.InsulationFactor`（認知遅延）
  - `OverallRisk(scores)` → 5因子の加重平均（0..1・0.7以上で「崩壊近接」警告を `EventEngine` 経由で通知）
- **接続**：`EnvironmentRules`（CLPS-1）× `EventEngine`（#116）× `DiplomacyRules`（#189）× `LogisticsRules`（#844）× CLPS-4/5
- **EditMode テスト必須**

#### CLPS-7 （lore）開示データ＋孤立×環境劣化の二重苦

**概要**：崩壊の世界観を `DisclosureLedger` に入力（コード新設なし）＋孤立版図の二重苦を `CampaignRules.Tick` で係数接続。

- **lore データ入力**（`DisclosureLedger`・FND-4）：
  - 「社会は自ら崩壊を選ぶ——警告は常に先に来た」
  - 「短期合理が長期破滅を生む——エリートは最後に気づく」
  - 「自制した者だけが続く——生存者は過去を管理した」
  - 「環境は文明の隠れた基盤——土地の悲鳴は静かだ」
- **孤立×環境劣化の二重苦**：`LogisticsRules.CohesionFactor`（版図孤立度）が低い勢力は環境劣化時に `SupplyRules` 補給救済が機能しない。`CampaignRules.Tick` で `EffectiveStability = Stability × CohesionFactor × OutputMultiplier(stock)` として三者を掛け合わせる（新State不要・係数接続のみ）
- **接続**：`DisclosureLedger`（FND-4）× `LogisticsRules`（#844）× `SupplyRules`（#94）× `EnvironmentRules`（CLPS-1）× `CampaignRules.Tick`

---

### ❌ 不採用（重複・タイクン化回避・既存カバー）

| 不採用 | 理由 |
|---|---|
| 個別の伐採/耕作/漁獲マイクロ操作 | タイクン化。環境は高位政策レバー1本+エンジン駆動（CLPS-4）で代替 |
| 多段の食料生産チェーン/BOM | `ResourceProductionRules`/`SCM`（#982）でカバー。CLPS は係数接続のみ |
| 気候変動システムの新規実装 | `EventEngine`（#116）の気候/天災イベントを流用。新規システム不要 |
| 敵対侵攻の新規システム | `DiplomacyRules`（#189）・戦略レイヤーでカバー。CLPS-6 で参照するのみ |
| 人口動態の作り直し | `DemographicsRules`（#153）に収容力比を渡して接続（additive） |
| 王朝崩壊そのものの再実装 | `DynastyRules`（#867）・`FactionStateRules`＝環境を第二崩壊エンジンとして合流するのみ |
| 文明フェーズ（Rising/Collapsed等）の再実装 | `CollapseRules`/`CivilizationPhase`（PHNX-1 #2264）でカバー。CLPS はそこへ至る環境経路を追加するだけ |
| 孤立した遠征地ならではの固有ルール | `LogisticsRules.CohesionFactor`×`EnvironmentRules` の係数接続のみ（CLPS-7） |
| 文明間比較の可視化UI | 純ロジック層の診断スコア（CLPS-6）を観測層から読む設計。新UI新設は別issue |

---

## 3. EPIC #1652 の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→盤面/UI へ配線。既存ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**世界観の構造パターンのみ**参考。

> **EPIC = #1652**。GitHub issue 起票済み（#1653〜#1659）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **CLPS-1** | #1653 | 環境資本ストックと再生（`EnvironmentRules`/`EnvironmentStock`＝有限ストック・再生率・収奪>再生の不可逆劣化） | `ResourceProductionRules.Produce` の上流制約。EditModeテスト必須 |
| **CLPS-2** | #1654 | 環境収容力と過剰人口（`CarryingCapacityRules`＝収容力超過→マルサス的縮小・食料不足→安定度低下） | `DemographicsRules`（LIFE-3）× `GovernanceRules.EquilibriumStability`。EditModeテスト必須 |
| **CLPS-3** | #1655 | 環境ティッピングと崩壊合流（閾値割れで非線形崩壊＝`FactionStateRules.IsCollapsing` に第5条件追加） | `EnvironmentRules`×`CarryingCapacityRules`×`FactionStateRules`×`CollapseRules`（PHNX-1）。EditModeテスト必須 |
| **CLPS-4** | #1656 | 自制という勝ち筋（`FactionState` 環境政策フィールド＝収奪0↔保全1の高位レバー・持続管理） | `FactionState`×`EnvironmentRules.Tick`×`GovernanceRules`。EditModeテスト必須 |
| **CLPS-5** | #1657 | エリートの自己隔離と破滅的意思決定（`EliteInsulationRules`＝環境費用の非対称負担→認知遅延→自制不行使） | `RedistributionRules`（#163）×`CapitalRules`（#917）×`EventEngine`（#116）。EditModeテスト必須 |
| **CLPS-6** | #1658 | 崩壊5因子の統合診断（`CollapseDiagnosisRules`＝環境/気候/敵対/交易/対応失敗の純関数集計・新State無し） | `EnvironmentRules`×`EventEngine`×`DiplomacyRules`×`LogisticsRules`×CLPS-4/5。EditModeテスト必須 |
| **CLPS-7** | #1659 | （lore）開示データ＋孤立×環境劣化の二重苦（`DisclosureLedger` lore入力＋`LogisticsRules`×環境係数接続） | `DisclosureLedger`（FND-4）×`LogisticsRules`（#844）×`CampaignRules.Tick`。コード新設なし |

### 推奨着手順

`CLPS-1`（環境ストック＝全体の基盤・上流制約の起点）→ `CLPS-2`（収容力＝人口圧力との接合）→ `CLPS-3`（崩壊合流＝PHNX-1への接続）→ `CLPS-4`（自制レバー＝高位決断の実装）→ `CLPS-5`（エリート隔離＝意思決定失敗の構造的原因）→ `CLPS-6`（5因子診断＝既存モジュールの集計）→ `CLPS-7`（lore入力＋係数接続＝コード不要・最後）。

> CLPS-1/2/3/4/5/6 はすべて既存モジュールへの**additive 接続**。`FactionStateRules.IsCollapsing` の崩壊経路が「人間内因のみ」から「環境×社会の複合」へ深まる。`CollapseRules`/`CivilizationPhase`（PHNX-1）はそのまま活用し、**環境という第二の崩壊エンジン**として接続する。
