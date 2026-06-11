# 『孟子』参考設計（EPIC #MENC）

> 参照元：孟軻（紀元前4〜3世紀）著『孟子』。儒家の王道政治論・性善説・民本主義の集大成。
> 君主が**仁政（徳による統治）**を行うことで民が自然と帰服し、覇者（力による支配）は短期に強くとも長期に崩壊する——王道と覇道の時間動態を描く政治哲学。
> 本ドキュメントは、当プロジェクト（Ginei＝社会・政治シミュ層を持つ星間国家戦略）にとって
> **役に立つ視点だけ**を抽出し、EPIC `#MENC` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**政治メカニクス／世界観の構造パターンのみ**を参考にする。

---

## 0. なぜ「孟子」が本システムに役立つか

当プロジェクトは統治・民心・王朝サイクルに関する**純ロジックを大量に保有**している：

| 既存（カバー範囲） | 対応モジュール |
|---|---|
| 天命喪失・易姓革命・王朝腐敗 | `DynastyRules`/`Regime`（正統性/腐敗/徳）|
| 被支配者の協力と非協力 | `ConsentRules`/`Polity` |
| 収奪↔包摂の統治スタイル | `FactionState.inclusiveness` |
| 短期最強・長期崩壊（法家型） | `MeritRankRules.ExtractiveDecay`（QIN-5） |
| 人口動態（出生/老化/死亡） | `DemographicsRules`/`Population` |
| 文化・民族の同化/ナショナリズム | `CultureRules`/`Culture` |
| 一瞬の集中・身口意の同期 | `FocusRules`（空海#872） |
| 国家腐敗→諸侯の寝返り | `FactionLoyaltyRules` |
| 法の一貫性（法家）・参験・二柄 | `LegalConsistencyRules`/`VerificationRules`/`TwoHandlesRules`（HFZ） |

**しかし、これらは「制度・均衡・法治」という抽象モデル**であり、孟子が固有に描く以下が**欠けている**：

| 孟子が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **四端（性善説の数値化）** | `Regime.徳` は統治王朝の徳スコア。**住民・個人の道徳的感受性**——仁/義/礼/智の4つの芽が安定した統治と教育で育ち、圧政で萎む——というモデルがない |
| **足による投票＝人口移動** | `DemographicsRules` は出生/老化/死亡のみ。**仁政の星系に人が集まり、苛政から人が逃げる**星系間の人口移動がない |
| **仁政の長期持続性（王道の時間動態）** | `FactionState.inclusiveness`（収奪↔包摂）はある。しかし**仁政の蓄積が天命を長期に支え**、覇道が長期に腐敗する**時間軸のメカニズム**が薄い（`ExtractiveDecay` は法家モデルでの短期最強のみ） |
| **浩然之気（道徳的気力の蓄積）** | `FocusRules`（三密）は一瞬の集中。`Organization.personalCharisma` は静的な魅力値。**一貫した善政の積み重ねで蓄積し、不誠実な行為で急減する長期的な道徳的気力**がない |
| **天命と仁政の直接接続** | `DynastyRules.Regime.徳` は初期値として設定されるのみ。**仁政実績が徳を実際に高め**、それが腐敗加速度を抑えて天命を持続させる**閉じた連鎖**がない |

**結論**：孟子は当プロジェクトの統治ロジックに**「德治（徳による統治）の動態モデル」**という視角から、
①**四端（住民の道徳的感受性）**、②**足による投票（人口移動）**、
③**仁政の時間持続性（王道の長期安定vs覇道の短期最強）**、④**浩然之気（道徳的気力の蓄積消耗）**という
4つの真の欠落軸を与える。
`DynastyRules`・`GovernanceRules`・`DemographicsRules`・`ConsentRules` への **additive な接続**。
**HFZ（韓非子=法家）と対の「儒家」の柱**として、法vs徳の思想対立軸（`DisclosureLedger`）を完成させる。

---

## 1. 役に立つ視点（要約）

孟子の世界観を、**本システムに効く形**で1行ずつ：

1. **性善説＝民は仁政に応答する道徳的感受性を持つ** — 住民の4つの道徳的芽（仁/義/礼/智）は安定した統治・教育・平和で育つ。高い四端は`GovernanceRules.stability` と `ConsentRules.cooperation` を底上げし、仁政の効果を増幅する。
2. **民為貴、社稷次之、君為軽（民が最も貴い）** — 民の支持こそが正統性の根拠。`ConsentRules` の「協力なしに統治は成り立たない」原理をさらに強化し、**人口移動**（足による投票）で可視化する。
3. **仁者無敵（仁政は敵なし）** — 仁政は軍事で奪えない持続的な支持基盤を作る。`DynastyRules.腐敗` の進行を遅らせる **徳の蓄積機構** として `GovernanceStyleRules` を追加。
4. **足を以て投票する（足による投票）** — 人は苛政の星系から仁政の星系へ移動する。`DemographicsRules` に**星系間の人口移動軸**を追加し、仁政の恩恵を地政学的に可視化。
5. **浩然之気は一朝一夕に育たない** — 長年の誠実な統治で蓄積し、一度の裏切りで崩れる。`FocusRules`（三密）が「瞬間の最大出力」なら、浩然之気は「積み重ねの基礎体力」。`LoyaltyRules.BaselineLoyalty` を底上げする長期係数。
6. **湯放桀・武王伐紂は「弑」ではなく「誅」** — 天命を失った暴君の打倒は正当。`DynastyRules.Revolution` に**革命の正統性判定**（四端と仁政実績が閾値を割ったか）を与え、革命の道義的根拠をゲームに組み込む。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`DynastyRules`/`GovernanceRules`/`DemographicsRules`/`ConsentRules`/`FactionState` を作り直さない**。MENC はそれらに**欠落軸を足し、接続する**だけ（additive）。
> HFZ（法家）との重複を厳禁：`LegalConsistencyRules`/`VerificationRules`/`TwoHandlesRules`/`PositionalAuthorityRules` は既に起票済み。

### ★★★ 最優先（真の欠落・孟子の signature）

#### MENC 四端モデル（`MoralSprouts` + `MoralSproutsRules`）
- **純データ `MoralSprouts`**：仁(compassion)/義(righteousness)/礼(propriety)/智(wisdom) 各 `0..1`。住民の道徳的感受性の4次元ベクトル（`Province` ないし `FactionState` に持たせる）。
- **`MoralSproutsRules`**（static・純ロジック・test-first）：
  - `Cultivate(sprouts, stabilityFactor, taxBurdenFactor, atWar, dt)` → 安定度高・低税・平時で各芽が成長、苛政・戦時で萎縮。
  - `AverageVirtue(sprouts)` → 四端の平均（0..1）= `DynastyRules.Regime.徳` への修正子入力として使う。
  - `CooperationBonus(sprouts)` → 仁の芽 × 義の芽 → `ConsentRules.Polity.cooperation` の加算修正子。
  - `StabilityBonus(sprouts)` → 礼の芽 → `GovernanceRules.stability` の収束速度係数。
  - `PolicySensitivity(sprouts)` → 智の芽 → 仁政（軽税/福祉）の効果感度倍率（仁政の恩恵がより大きく出る）。
  - `RevolutionJustness(sprouts)` → 四端平均が閾値を割れば革命は正当（`DynastyRules.Revolution` の正統性スコアとして使う）。
- **`Province`（または `FactionState`）との接続**：星系ごとに `MoralSprouts` を保持。`FactionStateRules.Tick` から `MoralSproutsRules.Cultivate` を呼ぶ。
- 接続：`GovernanceRules.Tick`（`StabilityBonus` を係数として参照）×`ConsentRules`（`CooperationBonus`）×`DynastyRules.Regime.徳`（`AverageVirtue`）×`FiscalRules`（`PolicySensitivity`）。EditMode テスト必須。

#### MENC 足による投票＝星系間人口移動（`PopulationMigrationRules`）
- **`MigrationFlow`**（純データ struct）：`fromSystemId`/`toSystemId`/`amount`（移動 Pop 量）。
- **`PopulationMigrationRules`**（static・純ロジック・test-first）：
  - `ComputeMigrationDrive(province, neighborProvinces, govParams)` → 安定度差・税負担差・四端差に比例した誘引力（引力モデル）。**高安定・低税・高四端の星系が人口を吸引**。
  - `MigratePop(pop, flow)` → 送り元の `生産年齢` cohort を削減、受入先に加算（高齢/年少は移動しにくい係数）。
  - `CanReceive(province, amount, capacity)` → 受入星系の収容限界（人口密度上限）。
  - `AnnualFlows(map, provinces, govParams)` → 銀河全星系に対して一括計算しフロー一覧を返す（`GalaxyView.onYear` から呼ぶ想定）。
- **仁政の地政学的可視化**：仁政の星系は時間とともに人口が増え、産出が増え、軍事力が高まる。苛政の星系は空洞化する。タイクン化回避＝計算はエンジン駆動、プレイヤーが各星系を「移民招致」マイクロ操作するUIは作らない。
- 接続：`DemographicsRules`（移動は `生産年齢` cohort を操作）×`GovernanceRules.OutputFactor`（人口増→産出増）×`MoralSproutsRules`（高四端が誘引力を増幅）。EditMode テスト必須。

### ★★ 高（王道の時間持続性と浩然之気）

#### MENC 仁政と覇道の時間動態（`GovernanceStyleRules`）
- **`GovernanceStyle`**（enum）：`仁政 / 覇道`（または連続値 `virtueScore: 0..1`）。
  - `仁政`：軽税・福祉・非戦。短期は成長遅め、長期は `DynastyRules.腐敗` 加速度↓＋`MoralSprouts.Cultivate` 係数↑＋人口吸引↑ → 自己強化する。
  - `覇道`：高税・軍事征服・強権。短期は `ResourceProductionRules` 高出力、長期は `MeritRankRules.ExtractiveDecay` と連動して腐敗↑・四端萎縮。
- **`GovernanceStyleRules`**（static・純ロジック）：
  - `DetectStyle(province, fiscal, policy)` → 税率・軽課・福祉水準から実態の統治スタイルを判定（自己申告でなく行動から推計）。
  - `VirtuePersistenceFactor(style, accumulatedYears)` → 仁政継続年数に比例する徳の持続係数（徳は「積み重ね」で重くなる）。
  - `CorruptionDeceleration(style, sprouts)` → 仁政×高四端で `DynastyRules.Regime.腐敗` の加速度を抑制。
  - `ExtractiveAmplification(style)` → 覇道 × `MeritRankRules.ExtractiveDecay` の相乗作用。
- **HFZ との相補**：HFZ は「法的一貫性が安定を生む（制度側）」、MENC は「仁政の徳が腐敗を遅らせる（徳側）」。両方実装することで**法家↔儒家の対立軸**がゲームに内在する。
- 接続：`DynastyRules.Regime`（`CorruptionDeceleration`）×`MeritRankRules`（`ExtractiveAmplification`）×`MoralSproutsRules`（`Cultivate` 係数）。EditMode テスト必須。

#### MENC 浩然之気（`MoralForce` + `MoralForceRules`）
- **純データ `MoralForce`**：`accumulated: 0..1`（蓄積量）＋`consistency: 0..1`（直近の言行一致度）。
- **`MoralForceRules`**（static・純ロジック）：
  - `Tick(force, isActingWithIntegrity, wasVerified, dt)` → 一貫した行動で蓄積・約束破りや不誠実で急減（非線形：蓄積は遅く減少は速い）。`wasVerified` は HFZ-2 `VerificationRules` の成果照合パスを参照。
  - `BaselineLoyaltyBonus(force)` → 蓄積された浩然之気が `LoyaltyRules.BaselineLoyalty` に加算（人が自然と集まる状態）。
  - `MoralCharismaFactor(force)` → `Organization.personalCharisma` へ乗数として適用（浩然之気の高いリーダーは徳によってカリスマを補完）。
  - `IsCollapsed(force)` → 浩然之気が閾値以下で崩壊状態（「一朝の裏切りで失う」臨界点）。
- **`FocusRules`（三密）との差異**：三密は「この瞬間の身口意の同期＝最大出力」（戦闘バフ）。浩然之気は「長年の積み重ね＝持続的な道徳的信頼」（統治係数）。直交・重複しない。
- **HFZ-2 `VerificationRules`（参験）との相互作用**：言行一致（参験パス）→ `isActingWithIntegrity=true` → 浩然之気↑。佞臣が参験を誤魔化す → 崩壊加速。
- 接続：`LoyaltyRules.BaselineLoyalty`（`BaselineLoyaltyBonus`）×`Organization.personalCharisma`（`MoralCharismaFactor`）×HFZ-2 `VerificationRules`（言行一致判定）。EditMode テスト必須。

### ★ 中（接続規則・lore）

#### MENC 天命と仁政・四端の接続規則（`DynastyRules` への入力配線）
- 新型クラス不要。**接続規則のみ**：`DynastyRules.Regime.徳` の更新式を `FactionStateRules.Tick` から拡張し、`MoralSproutsRules.AverageVirtue(sprouts)` × `GovernanceStyleRules.VirtuePersistenceFactor(style, years)` を係数として参照。
- `革命の正統性`：`DynastyRules.Revolution` 発火時、`MoralSproutsRules.RevolutionJustness(sprouts)` が閾値未満なら **正当な革命**（`EventEngine` に道義的革命イベントを発火）、超えていれば**簒奪**（正統性ペナルティ）。
- 接続：`DynastyRules`（徳の修正・革命正統性）×`FactionStateRules.Tick`（統合 Tick から呼ぶ）。純ロジック追加のみ。EditMode テスト必須（修正子の値域検証）。

#### MENC（lore）世界観の開示データ（儒家vs法家・王道の銀河統一）
- 「仁政は覇道を超えて続く」「易姓革命は民の正当な権利」「浩然之気は宇宙に満ちる」。
- **HFZ-6（法家：制度の自動性）**と対の**思想対立軸の完成**：韓非子＝「徳のある名君を待つな・制度を作れ」↔ 孟子＝「制度より君主の徳が根本」。
- 世界観EPIC（啓蒙/秘史/エンディング）・思想対立軸 #617〜623 と接続。
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への**lore データ入力**。CCX-6（世界観codex退避）方針に一貫。

### ❌ 不採用（重複・既存で十分・後退防止）

| 不採用 | 理由 |
|---|---|
| 易姓革命そのもの（Revolution） | **`DynastyRules.Revolution` が既存**。MENC は正統性の入力を与えるだけ（MENC-5） |
| 仁義礼智を役職条件にする | `OfficeRules.requiredTier`（階級ゲート）で役職資格は既にカバー済み。重複新設しない |
| 民本主義の投票システム（参政権） | `PartyRules`/`LeadershipElectionRules`（GOV-6/7）が既にカバー。MENC は民心の動態のみ |
| 性善説・性悪説の直接UI実装 | タイクン化回避。思想対立はlore（MENC-6）として `DisclosureLedger` で扱う |
| 周礼・礼楽制度の詳細実装 | `GovernmentRegistry`/`OfficeRules`（GOV-1/3）が役職制度をカバー。礼は `MoralSprouts.礼` の係数で効かせる |
| 孟子の経済論（井田法・均田制） | `GovernanceRules.OutputFactor` ＋ `FiscalRules.TaxBurdenPenalty` の接続で表現十分。マイクロな土地制度UIは不要 |
| 人口移動のUIコントロール（移民招致） | タイクン化回避。`PopulationMigrationRules.AnnualFlows` をエンジン駆動にし、プレイヤーが直接操作しない |

---

## 3. EPIC #MENC の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UI へ配線。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。
> HFZ（韓非子 #1331）と相補。法vs徳の両極を実装して思想対立軸を完成させる。

> **EPIC = #1561**。GitHub issue 起票済み（#1564〜#1572）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **MENC-1** | #1564 | 四端モデル（`MoralSprouts` + `MoralSproutsRules`・仁/義/礼/智の住民道徳的感受性） | 新 `MoralSproutsRules`。`GovernanceRules`/`ConsentRules`/`DynastyRules.徳`/`FiscalRules` への係数接続 |
| **MENC-2** | #1566 | 足による投票＝星系間人口移動（`PopulationMigrationRules`・仁政→人口吸引・苛政→人口流出） | 新 `PopulationMigrationRules`+`MigrationFlow`。`DemographicsRules`×`GovernanceRules`×`MoralSproutsRules` |
| **MENC-3** | #1568 | 仁政と覇道の時間動態（`GovernanceStyleRules`・王道の長期持続性vs覇道の短期最強） | 新 `GovernanceStyleRules`。`DynastyRules.腐敗`×`MeritRankRules.ExtractiveDecay`×`MoralSproutsRules` |
| **MENC-4** | #1570 | 浩然之気（`MoralForce` + `MoralForceRules`・一貫した善政の積み重ね→道徳的気力蓄積→忠誠係数） | 新 `MoralForceRules`。`LoyaltyRules.BaselineLoyalty`×`Organization.personalCharisma`×HFZ-2接続 |
| **MENC-5** | #1571 | 天命と仁政・四端の接続（`DynastyRules.徳`への修正子入力・革命の正統性判定） | 新型不要。`FactionStateRules.Tick` 拡張＋`DynastyRules.Revolution` 正統性スコア接続 |
| **MENC-6** | #1572 | （lore）世界観の開示データ（王道/覇道・易姓革命の正当性・儒家vs法家対立軸完成） | `DisclosureLedger`（FND-4）。コード新設なし。HFZ-6・思想対立軸 #617〜623 と接続 |

### 推奨着手順
`MENC-1`（四端＝最も根幹・GovernanceRules/ConsentRules/DynastyRules の全域に係数を与える）→
`MENC-2`（人口移動＝仁政の地政学的可視化・DemographicsRulesに星系間軸を追加）→
`MENC-3`（仁政/覇道の時間動態＝ExtractiveDecayとの相補で法家↔儒家の両軸完成）→
`MENC-4`（浩然之気＝リーダー個人の道徳的積み重ね・HFZ-2 参験と相互作用）→
`MENC-5`（天命接続＝DynastyRules.徳の決定論を完成させる）→
`MENC-6`（lore）。

> いずれも既存の統治・民心・王朝サイクルロジックを**後退させず接続**する additive 設計。
> `DynastyRules`（天命論）の**儒家的内実**を、HFZ（法家的内実）と並べて完成させる。
