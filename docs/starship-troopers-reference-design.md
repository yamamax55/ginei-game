# ハインライン『宇宙の戦士』参考設計（EPIC #STSH）

> 参照元：ロバート・A・ハインライン『宇宙の戦士（Starship Troopers）』（1959年）。
> **機動歩兵（モービル・インファントリー）と連邦市民制**——「兵役を果たした者のみが参政権を持つ」という政治哲学、
> 軍律としての懲罰抑止論、そして価値観が全く異なる知性体との絶滅戦争を描く SF 政治小説。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略＋既に豊富な政治・軍事純ロジック層）にとって
> **役に立つ視点だけを抽出**し、EPIC `#STSH` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**政体・軍律・戦争教義のメカニクス構造のみ**を参考にする。

---

## 0. なぜ「宇宙の戦士」が本システムに役立つか

当プロジェクトは政治・軍事の**マクロ純ロジックを広く保有**している（[CLAUDE.md] 参照）：

| 既存（政治・軍事・統治） | カバー範囲 |
|---|---|
| `CivilianControlRules`（GOV-4 #145） | 文民統制の型・軍政関係・クーデター閾値 |
| `MeritRankRules`（#900-905 始皇帝モデル） | 戦功→爵位・インセンティブ士気・法家の罠 |
| `RetirementRules`（#530-536） | ServiceStatus（現役/予備役/退役）・停年・戦時召集 |
| `LoyaltyRules`/`BattleAllegianceRules`（#817） | 旗幟・忠誠・寝返りカスケード |
| `ConsentRules`/`Polity`（#836） | 合意・非協力・実効統治力 |
| `PartyRules`/`GovernmentRegistry`（GOV-6/3） | 政党・選挙・役職任命台帳 |
| `CareerPipelineRules`/`SeniorityRules`（LIFE-5/6） | 出身経路・士官学校・席次vs実力 |
| `WarGoalRules`/`DiplomacyRules`（Wave2・DIP） | 戦争目標・厭戦・講和受諾・条約 |
| `NormalizationRules`（PANO・フーコー） | 訓練強度→規格化→信頼性↑・創意↓ |

**しかし、これらは「制度内の動態」であり、宇宙の戦士が固有に描く以下が欠けている**：

| 宇宙の戦士が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **軍功市民制 = 兵役を果たした者だけが参政権を持つ** | `PartyRules` は全市民均等参加が前提。**服役履歴により政治参加権が分岐する制度**（フランチャイズ条件付き代表制）が無い |
| **懲罰の抑止論 = 明示的な罰則が脱走・臆病を抑える** | `NormalizationRules`（PANO）は「訓練が服従を内面化」する機制。**罰則の明示的厳格度→脱走コスト→部隊結束のトレードオフ**（計算する個人を前提とした抑止）が無い |
| **服役義務の社会契約 = 保護と義務の明示的交換** | `ConsentRules.Withdraw` は非協力を抽象的にモデル化。**「いざとなれば戦う」意思表明と「権利取得」の明示的交換ルール**（自発的服役→市民権）が無い |
| **交渉不能な完全他者 = 理解できない価値観を持つ敵** | `DiplomacyRules` は全勢力が opinion/条約を持つ前提。**交渉が原理的に成立しない勢力**（価値観の根本非互換）への戦争教義が無い |
| **分散強襲ドクトリン = 分散投入で生存率↑・収束で成果↑** | 陣形システム（紡錘陣/鶴翼陣等）は配置パターン。**「分散→各個生存→収束」という動的ドクトリン**（面的殲滅への対抗）が無い |

**結論**：宇宙の戦士は本プロジェクトの政治・軍事層に、
①**軍功市民制**（参政権の条件分岐）
②**懲罰抑止論**（フーコーの規格化とは異なる計算的抑止）
③**服役義務の社会契約**（明示的保護⇔義務の交換）
④**完全他者タグと殲滅教義**（交渉不能勢力の扱い）
⑤**分散強襲ドクトリン型**（動的戦術姿勢）
という5つの欠落軸を与える。
既存の PANO（フーコー）が「監視・規格化」という受動的規律なら、
STSH は「自発的奉仕と明示的罰則」という**能動的・契約的規律**の対極を補う。

---

## 1. 役に立つ視点（要約）

宇宙の戦士の世界観を、**本システムに効く形**で1行ずつ：

1. **「参政権は権利でなく報酬」＝兵役を通じて初めて市民になる**。生まれながらの権利でなく、命がけの奉仕によって得る。→ `CivicFranchiseRules`（軍功市民制）の核。既存 `MeritRankRules`（昇進）とは別に、**民政への参加権**を兵役で得る制度を追加。
2. **「処罰は抑止のためにある」＝罰則の存在が違反を未然に防ぐ**。罰が厳しいほど（および確実なほど）違反は減る——ただし苛烈すぎると怨嗟が積む。→ `MilitaryJusticeRules`（懲罰抑止論）の核。PANO の「訓育」とは別機制。
3. **「自発的服役＝社会契約の具現」＝強制でなく選択によって義務が成立する**。強制徴兵と自発的服役では社会の合意コストが根本的に異なる。→ `ServiceObligationRules`（服役型×合意コスト）。`ConsentRules` と接続。
4. **「理解できない敵との戦争は別ルールで動く」＝交渉は前提でない**。相手の価値観が根本的に非互換なら opinion や条約が成立しない——降伏も講和も認めない絶滅戦争教義。→ `FactionData.isNegotiable`（交渉不能タグ）と`WarGoalRules`拡張。
5. **「分散して生き残り、収束して目標を達成する」＝面的殲滅への解答**。密集は強力だが一撃で壊滅する——分散配備は損耗を分散させ生存率を上げるが、各個撃破リスクを生む。→ `BattleDoctrineRules`（戦術ドクトリン型）として陣形システムと直交する次元を追加。
6. **「責任なき権利は腐敗する」＝権利の正統性は義務の履行から来る**。これは `DynastyRules` の「天命」や `ConsentRules` の「合意」を貫く共通テーマ。→ **コード新設せず** `DisclosureLedger`（FND-4）へのloreデータとして開示エンジンに乗せる。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**既存の `PartyRules`/`LoyaltyRules`/`ConsentRules`/`DiplomacyRules`/Formation を作り直さない**。STSH はそれらに**欠落軸を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・宇宙の戦士の signature）

#### STSH 軍功市民制（`CivicFranchiseRules`）

- **`ServiceRecord`（純データ）**：`personId` / `factionId` / `yearsServed` / `honorableDischarge`（名誉除隊）/ `isEnfranchised`（参政権保持）。
- **`CivicFranchiseRules`（static）**：
  - `IsEnfranchised(ServiceRecord, FranchiseParams)` → 服役年数・除隊区分で参政権を判定
  - `FranchiseShare(factionId, roster)` → 市民（参政権保持者）/ 全人口 の比率
  - `LegitimacyFromFranchise(share, FranchiseParams)` → 市民比率×参加率が政治正統性に影響
  - `SupportModifier(person, isEnfranchised)` → 市民/非市民で政治的支持感度が変わる
- 接続：`PartyRules.Support`（参政権保持者のみが「議員票」に算入）/ `ConsentRules.ControlStrength`（フランチャイズ比率が実効統治力に影響）/ `GovernmentRegistry.TryAppoint`（市民のみが特定役職に就ける）。
- **新設純ロジック**：`ServiceRecord`（Core）＋`CivicFranchiseRules`（static Core）。EditModeテスト必須。

#### STSH 懲罰抑止論（`MilitaryJusticeRules`）

- **`PunishmentRegime`（純データ）**：`severity`（0..1 罰則の厳格度）/ `enforcement`（0..1 執行確度）/ `coverageScope`（適用範囲：脱走/臆病/不服従）/ `publicVisibility`（公開処刑の有無）。
- **`MilitaryJusticeRules`（static）**：
  - `DeserterRisk(morale, PunishmentRegime)` → 士気水準×罰則から脱走確率を算出（厳格高→脱走↓）
  - `CohesionBonus(PunishmentRegime)` → 抑止効果が部隊結束に加算する係数（`LoyaltyRules` への補正項）
  - `ResentmentAccrual(PunishmentRegime, dt)` → 厳格すぎる体制が怨嗟を蓄積（支持低下に波及）
  - `OptimalSeverity(morale, loyalty)` → 怨嗟コストを最小化する懲罰厳格度の均衡点
- PANO の `NormalizationRules`（訓育→内面化）との違い：**STSH は「処罰を見た個人が計算し行動を変える」抑止論**。内面化でなく外的コスト。
- 接続：`LoyaltyRules.ResolveStance`（脱走リスク係数を旗幟計算に組み込む）/ `FleetMorale`（士気低下時の逃走確率補正）/ `SecurityRules.RepressionSupportPenalty`（公開処刑の支持低下）。
- **新設純ロジック**：`PunishmentRegime`（Core）＋`MilitaryJusticeRules`（static Core）。EditModeテスト必須。

### ★★ 高（欠落・additive 追加で価値が高い）

#### STSH 服役義務の社会契約（`ServiceObligationRules`）

- **`RecruitmentPolicy`（enum）**：`{自発,選抜徴兵,全員徴兵}`。
- **`ServiceObligation`（純データ）**：`factionId` / `policy`（RecruitmentPolicy）/ `serviceYearsRequired` / `exemptionRate`（免除率 0..1）。
- **`ServiceObligationRules`（static）**：
  - `RecruitablePool(population, policy, exemptionRate)` → 徴兵可能人数
  - `ConsentCost(policy, ConsentRules.Polity)` → 徴兵型ごとの合意コスト（自発=低・全員徴兵=高）
  - `LegitimacyBonus(policy)` → 自発的服役が正統性に与えるプラス（市民は「選んで」守る）
  - `FleetStrengthSupply(obligation, FleetPool)` → 服役型が `FleetPool` への兵力供給率に影響
- 接続：`FleetPool`（兵力供給量）/ `ConsentRules.ControlStrength`（徴兵強制→合意低下）/ `CivicFranchiseRules`（自発服役のみ参政権付与）。
- **新設純ロジック**：`ServiceObligation`（Core）＋`ServiceObligationRules`（static Core）。EditModeテスト必須。

#### STSH 交渉不能勢力タグと殲滅戦争教義

- **`FactionData.isNegotiable`（bool・既定 true）**：false＝交渉が原理的に成立しない勢力フラグ。
- **拡張ルール**（`DiplomacyRules.CanNegotiate` / `WarGoalRules.PeaceAcceptance`）：
  - `CanNegotiate(a, b)` → どちらかが `isNegotiable=false` なら `false`（条約/意見drift 無効）
  - `PeaceAcceptance(wargoal)` → 殲滅戦争目標（`CasusBelli.Extermination`）では和平不可
  - `ExtinctionThreshold(faction, FleetRegistry)` → 敵旗艦が一定数以下になると「殲滅完了」判定
- 接続：`BattleManager.EvaluateVictory`（殲滅目標の新条件）/ `DiplomacyRules.IsHostile`（交渉不能は常時敵対）/ `WarGoalRules`（既存Wave2への拡張）。
- **最小実装**：`FactionData.isNegotiable` フィールド追加（Core・既存の ScriptableObject 拡張）＋ `DiplomacyRules`/`WarGoalRules` へのガード条件追加。EditModeテスト必須。

#### STSH 分散強襲ドクトリン（`BattleDoctrineRules`）

- **`BattleDoctrine`（enum・Core）**：`{集中,分散,機動}`。陣形（Formation）が「配置パターン」なら、ドクトリンは「戦術姿勢」。
- **`BattleDoctrineRules`（static）**：
  - `SurvivabilityFactor(doctrine)` → 分散：被弾面積↓（広域攻撃に強い）/ 集中：火力↑
  - `ConcentrationRisk(doctrine)` → 集中：一撃全滅リスク↑ / 分散：各個撃破リスク↑
  - `FormationCompatibility(doctrine, Formation)` → ドクトリン×陣形の相性（分散＋鶴翼陣が最適など）
  - `DoctrineModifiers(doctrine)` → `CombatModifiers`/`ModifierStack` への補正として返す（実効値パターン）
- 接続：`Formation`（直交軸として加算・既存陣形システムを変えない）/ `FleetMovement.GetMobilityFactor`（機動ドクトリンで速度補正）/ `ShipCombat.ComputeDamage`（集中ドクトリンで火力倍率）/ `CombatModifiers.ModifierStack`（#106 に準拠）。
- **新設純ロジック**：`BattleDoctrine`（Core enum）＋`BattleDoctrineRules`（static Core）。EditModeテスト必須。

### ★ 中（世界観 lore・コード新設なし）

#### STSH（lore）世界観の開示データ

- 「責任なき権利は腐敗する」「完全な他者性との戦争の倫理」「抑止の逆説（厳しすぎる罰則が腐敗を生む）」「兵士であることは選択であり、その選択が人を市民にする」。
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への lore データ入力のみ。世界観EPIC群（秘史・天井CAP・ニーチェ「神は死んだ」・エンディング）との thematic 共鳴。

---

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| パワードスーツの技術ツリー | `ResearchRules`（Wave2）に薄く接続するだけ。新EPIC化せず。タイクン化回避 |
| 士官学校の授業シミュレーション | `CareerPipelineRules.Stamp`（士官学校経路）が既にカバー。授業詳細は微操作 |
| 宇宙降下作戦の物理（大気圏突入/ポッド） | 入植艦`ShipRole`/`FleetMovement` で十分。新物理演算はスコープ外 |
| 訓練の規格化・パノプティコン | **PANO（フーコー）が既にカバー**（`NormalizationRules`/`PanoptismRules`）。STSH は重複させず |
| 複数種族の生物学的差異 | 固有設定の流用に相当する。`FactionData.isNegotiable` フラグで抽象化するだけで十分 |
| ハインライン特有の社会思想の実装 | 著作権・思想の著作物該当リスク。メカニクス構造のみ抽出 |

---

## 3. EPIC #STSH の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UI へ配線。既存政治・軍事ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**政体・軍律・戦争教義のメカニクス構造のみ**参考。

> **EPIC = #2348**。GitHub issue 起票済み（#2353〜#2366）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **STSH-1** | #2353 | 軍功市民制（`ServiceRecord`＋`CivicFranchiseRules`：兵役→参政権・フランチャイズ比率→正統性） | `PartyRules.Support`×`ConsentRules.ControlStrength`×`GovernmentRegistry` |
| **STSH-2** | #2357 | 懲罰抑止論（`PunishmentRegime`＋`MilitaryJusticeRules`：罰則厳格度→脱走↓×怨嗟↑トレードオフ） | `LoyaltyRules.ResolveStance`×`FleetMorale`×`SecurityRules` |
| **STSH-3** | #2360 | 服役義務の社会契約（`ServiceObligation`＋`ServiceObligationRules`：徴兵型×合意コスト×兵力供給） | `FleetPool`×`ConsentRules`×`CivicFranchiseRules`（STSH-1接続） |
| **STSH-4** | #2363 | 交渉不能勢力タグと殲滅戦争教義（`FactionData.isNegotiable`拡張＋`DiplomacyRules`/`WarGoalRules`ガード） | `DiplomacyRules.CanNegotiate`×`WarGoalRules.PeaceAcceptance`×`BattleManager` |
| **STSH-5** | #2365 | 分散強襲ドクトリン（`BattleDoctrine` enum＋`BattleDoctrineRules`：陣形と直交する戦術姿勢型） | `Formation`×`CombatModifiers.ModifierStack`×`FleetMovement.GetMobilityFactor` |
| **STSH-6** | #2366 | （lore）世界観の開示データ（責任と権利・完全な他者性・抑止の逆説） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順

`STSH-1`（軍功市民制＝最も固有で欠落の大きい signature）→ `STSH-2`（懲罰抑止論＝PANO と対をなす）→ `STSH-3`（服役義務＝STSH-1 の supply 側）→ `STSH-4`（交渉不能タグ＝既存コードへの最小追加）→ `STSH-5`（ドクトリン型＝陣形の次元拡張）→ `STSH-6`（lore）。

> いずれも既存政治・軍事ロジックを**後退させず接続**する additive 設計。
