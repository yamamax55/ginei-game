# アリストテレス『政治学』参考設計（EPIC #ARIS）

> 参照元：アリストテレス『政治学』（Politika, 前4世紀）。政体の分類・中間層・公益と私益・市民的友愛・僭主維持術を中心とした古代ギリシャ政治哲学の古典。
> 本ドキュメントは、当プロジェクト（Ginei）にとって**役に立つ視点**だけを抽出し、EPIC `#ARIS` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用しない。**政治哲学の構造パターン・メカニクスのみ**を参考にする。

---

## 0. なぜ「アリストテレス『政治学』」が本システムに役立つか

### 既存（カバー範囲）

| 既存モジュール | カバー範囲 |
|---|---|
| POLY-1: `AnacyclosisRules`/`RegimeForm`（#1442） | 六政体の類型と腐落サイクル（循環の**構造**） |
| POLY-2: `MixedConstitutionRules`（#1445） | 混合政体の三成分混合比→腐落抵抗（**構造的**安定化） |
| `DynastyRules`/`Regime` | 正統性・腐敗・天命喪失・易姓革命サイクル |
| `ConsentRules`/`Polity` | 被支配者の協力・非協力・統治不能 |
| `ConstitutionRules`/`Constitution`（Wave2 #170） | 制約権力・権利→正統性・立憲君主制 |
| `SeparationOfPowersRules`（Wave2 #171） | 三権分立・専制リスク・グリッドロック |
| `RedistributionRules`/`FiscalClass` | 階級別税負担・累進度・階級対立 |
| `SecurityRules`/`SecurityApparatus` | 弾圧・密告・クーデター検知 |
| `MeritRankRules`（QIN：始皇帝） | 軍功→爵位・制度の畏怖・`ExtractiveDecay` |
| `AutonomyRules`/`TrustVsCohesion` | 軍事単位の信頼vs結束 |

### アリストテレス固有の視点 × 当プロジェクトでの欠落

| アリストテレス固有の視点 | 当プロジェクトでの欠落 |
|---|---|
| **中間層（mesoi）の政体安定化** | `FiscalClass` に中間層データはあるが、**中間層が厚いほど政体が安定し・薄いと極端な政体へ滑落する**という動学が無い。POLY-2は「構造的な混合比」、ARIS-1は「**社会経済的基盤（誰が多数派か）**」という補完的な別軸 |
| **公益/私益政体品質（正政体/僭政体）** | `DynastyRules` は腐敗**速度**（徳が遅らせる）。**統治がそもそも公益志向か私益志向か**というQUALITY DIMENSIONが無い。POLY-1の循環「いつ」に対し、「なぜ崩れるか（目的の歪み）」を与える |
| **収奪経済志向（chrematistics）vs 管理型（oikonomia）** | `FiscalRules`は財政技術。`ExtractiveDecay`（QIN）は制度的インセンティブ罰。**支配者が国家を家政（有限・持続）として管理するか、無限増殖機械として扱うか**という**動機的区別**が無い |
| **市民的信頼（civic philia）と審議崩壊** | `AutonomyRules.TrustVsCohesion` は軍事部隊スケール。**政体内の市民相互の政治的信頼が失われると、形式的に正しい憲法制度でも審議不能になる**という市民社会レイヤーが無い |
| **僭主維持術の具体的ツールキット** | `SecurityRules` は弾圧一般。**貧困化課税・卓越者排除（tall poppy）・大型公共事業による疲弊・密告奨励・相互不信醸成**というアリストテレスが分析した僭主専用のメンテナンス手法の型が無い |

**結論**：ポリュビオス（POLY）が政体の**構造的循環**を与えたとすれば、アリストテレスは**社会経済的基盤（中間層）・目的的品質（公益/私益）・動機的区別（oikonomia/chrematistics）・市民的信頼・僭主の具体的維持術**という5つの欠落軸を補完する。POLY-1/2が「形式」なら、ARISは「中身（誰が参加し、何のために統治するか）」を与える。特に中間層安定化は「銀河の経済格差が政体の寿命を直接決める」という戦略レイヤーへの接続点になる。

---

## 1. 役に立つ視点

1. **「中間層が厚い社会は安定し、両極が剥き出しの社会は内乱へ向かう」** → `FiscalClass`中間層シェア×`GovernanceRules`×POLY-1の滑落ベクトル抑制（既存#109/#110の統合点）。
2. **「正しい政体は公益のため、誤った政体は統治者の私益のため」** → 公益スコアが腐敗速度・正統性を修正。POLY-1の循環速度を動的に変える。
3. **「家を治めるように国を治めよ（oikonomia）、それ以上の無限増殖（chrematistics）は腐る」** → 収奪志向フラグ→`DynastyRules`腐敗加速の別回路（QIN-5 ExtractiveDecayとは動機・対象が異なる）。
4. **「友愛（philia）なき政体には市民はいない、奴隷がいるだけだ」** → 市民的信頼が審議制度の実効性を支える。`SeparationOfPowersRules.IsGridlocked`に社会的基盤を与える。
5. **「僭主は民を貧しくし、互いを疑わせ、高みにある者を引き倒す」** → `TyrantToolkitRules`が`SecurityRules`/`CoupRules`に具体的手法を追加。
6. **「人間はポリス的動物（ζῷον πολιτικόν）」** → 政治参加なき市民は市民ではない。lore開示の基本公理（カリスマ/制度化#812とも共鳴）。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**POLY-1/2・`DynastyRules`・`SecurityRules`・`SeparationOfPowersRules` を作り直さない**。ARISはそれらに**欠落軸を追加し接続する**だけ（additive）。

### ★★★ 最優先（アリストテレスの固有 signature）

#### ARIS 中間層安定化係数（MesoiRules / メソイ理論）
- **`FiscalClass`の中間層シェア**（中間クラス人口比）を**政体安定倍率**に変換
  - 中間層シェア > `upperMesoThreshold`（既定0.5）→ 安定係数 1.0以上（POLY-1の腐落ベクトルを鈍化）
  - 中間層シェア < `lowerMesoThreshold`（既定0.2）→ 安定係数 < 1.0（腐落ベクトルを加速）
- `MesoiMetrics`（struct）= `middleClassShare` / `mesoStabilityFactor`（純変換）
- `MesoiRules`（static・pure logic・test-first）= `ComputeMetrics` / `StabilityFactor(metrics)` / `IsAtRisk`（シェア0.2未満で真）
- 接続：`FiscalClass`（入力）×`GovernanceRules.OutputFactor`（安定度修正）×POLY-1 `AnacyclosisRules`（循環速度修正）×`FactionStateRules.Stability`（複合安定度）

#### ARIS 公益-私益政体品質スコア（CommonGoodOrientationRules）
- 統治の「正しさ」を0..1スコアで表現：高いほど公益志向（正政体）、低いほど私益志向（僭政体）
  - スコア = f(累進度`RedistributionRules.Progressivity`、制度的制約`ConstitutionRules.ConstrainedAuthority`、市民参加率）
  - 低スコア → `DynastyRules.Regime.corruption` 加速乗算 + 正統性低下速度UP
  - 高スコア → POLY-2 `mixIndex` と相乗（正しい混合政体は腐落しにくい）
- `CommonGoodScore`（pure struct）/ `CommonGoodOrientationRules`（static・test-first）= `Compute` / `CorruptionMultiplier(score)` / `LegitimacyDrainFactor(score)`
- 接続：`RedistributionRules`×`ConstitutionRules`×`DynastyRules`×POLY-1/2×`FactionStateRules`

### ★★ 高（欠落を補完する動機層・社会層）

#### ARIS 収奪経済志向（ChrematisticsRules）
- `EconomicOrientation` enum {管理型（oikonomia）/収奪型（chrematistics）} で支配者の動機を表現
  - 管理型 → 財政運営は持続的、正統性への影響なし
  - 収奪型 → 短期財政改善・長期的に `DynastyRules.Regime.corruption` を加速（**QIN-5 ExtractiveDecayと別回路**：QIN-5はインセンティブ制度罰、ARIS-3は支配者の目的動機）
- `ChrematisticsRules`（static・test-first）= `ExtractiveMultiplier(orientation)` / `IsExtractiveMode(fiscalState, polity)` （高税+低分配の組み合わせから推定）/ `LongTermDecay(orientation, dt)`
- 接続：`FiscalRules`（税率・財政）×`DynastyRules`（腐敗加速）×`MeritRankRules`（QIN-5と別経路を明示）×`FactionStateRules.IsCollapsing`

#### ARIS 市民的信頼と審議崩壊（CivicPhiliaRules）
- `CivicPhilia`（0..1）：政体内で市民相互が政治的に信頼できる程度
  - 低下要因：高不平等（中間層の薄さ）・長期専制・戦時・僭主ツールの使用
  - 回復要因：中間層の厚さ・平和・市民参加の機会
  - 閾値割れ → `SeparationOfPowersRules.IsGridlocked` 圧力増加（形式的制度は機能するが実態として審議不能）
- `CivicPhiliaRules`（static・test-first）= `Tick(philia, mesoFactor, tyrantPressure, dt)` / `GridlockPressure(philia)` / `IsDeliberationCollapsed(philia)` / `RecoveryRate(mesoMetrics)`
- 接続：`ConsentRules`（非協力との対称）×`AutonomyRules.TrustVsCohesion`（軍事との対称）×`SeparationOfPowersRules`×ARIS-1（中間層→信頼の基盤）×ARIS-5（僭主術→信頼低下）

#### ARIS 僭主維持術（TyrantToolkitRules）
- 僭主（`RegimeForm.Tyranny` の支配者）が使える「維持コスト低減策」と短/長期トレードオフ
  - `TyrantMeasure` enum：{貧困化課税, 卓越者排除（tall_poppy）, 大型公共事業疲弊, 密告奨励, 相互不信醸成}
  - 各措置の**短期効果**：クーデターリスク低下（`CoupRules.CoupRisk` 減少）・弾圧コスト低下
  - 各措置の**長期コスト**：`CivicPhilia` 低下・生産性（`GovernanceRules.OutputFactor`）低下・正統性侵食
  - 「卓越者排除」→ 後継者プール枯渇（`VacancyRules.SelectSuccessor` の候補減少圧力）
- `TyrantToolkitRules`（static・test-first）= `ApplyMeasure(measure, params)→EffectBundle` / `CivicCost(measure)` / `CoupReduction(measure)` / `LongTermLegitimacyDrain(measure)`
- 接続：`SecurityRules`（弾圧・密告）×`CoupRules`（クーデター抑制）×`CivicPhiliaRules`（信頼破壊）×`FiscalRules`（貧困化課税）×`VacancyRules`（人材枯渇）

### ★ 中（世界観lore）

#### ARIS lore 開示データ
- 政治的動物の公理・友愛の喪失・oikonomia対chrematisticsの哲学・僭主の末路（中間層なき社会の結末）
- 接続：`DisclosureLedger`（FND-4）への**lore データ入力**。コード新設なし
- `DisclosureEntry`：前提条件＝秘史Aなど。`StrategySession.Campaign`の崩壊フラグ連動可

### ❌ 不採用（重複・既存でカバー済み）

| 不採用 | 理由 |
|---|---|
| 六政体の類型・腐落サイクル（Anacyclosis） | **POLY-1 `AnacyclosisRules`（#1442）がカバー済み**。重複新設しない |
| 混合政体の安定指数 | **POLY-2 `MixedConstitutionRules`（#1445）がカバー済み**。ARISはその入力を増やすだけ |
| 三権分立・権力分立そのもの | **`SeparationOfPowersRules`（Wave2 #171）がカバー済み** |
| 自然奴隷論・奴隷制度の実装 | ゲームメカニクスとして採用不適切（人道・ゲームデザイン上の問題） |
| 審議機能（deliberative assembly）の組織実装 | **`OfficeRules`/`GovernmentRegistry`/`MinistryRules` がカバー済み**。市民的信頼（ARIS-4）で間接的に効かせる |
| 市民権の具体的定義（軍事奉仕/財産基準） | `GovernmentRegistry.TryAppoint`の資格ゲートで代替可能 |
| 立憲主義・権利保護そのもの | **`ConstitutionRules`/`MagnaCartaRules`（#170/#624）がカバー済み** |

---

## 3. 子Issue表（ARIS-1〜6・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UI配線。既存政治・経済ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**政治哲学の構造パターン・メカニクスのみ**参考。

> **EPIC = #1491**。GitHub issue 起票済み（#1495〜#1505）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **ARIS-1** | #1495 | MesoiRules — 中間層安定化係数（`FiscalClass`中間層シェア→政体安定倍率・POLY-1腐落速度修正） | `FiscalClass`×`GovernanceRules`×POLY-1。純ロジック新設・EditModeテスト必須 |
| **ARIS-2** | #1499 | CommonGoodOrientationRules — 公益-私益政体品質スコア（累進度・制度制約→`DynastyRules`腐敗加速係数） | `RedistributionRules`×`ConstitutionRules`×`DynastyRules`×POLY-2。純ロジック新設・EditModeテスト必須 |
| **ARIS-3** | #1502 | ChrematisticsRules — 収奪経済志向（管理型/収奪型の動機区別→腐敗加速の別回路・QIN-5と異系統） | `FiscalRules`×`DynastyRules`×`MeritRankRules`（QIN-5と別経路）。純ロジック新設・EditModeテスト必須 |
| **ARIS-4** | #1503 | CivicPhiliaRules — 市民的信頼と審議崩壊（不平等・僭主圧力で低下→`SeparationOfPowers.IsGridlocked`増幅） | `ConsentRules`×`AutonomyRules`×`SeparationOfPowersRules`×ARIS-1/5。純ロジック新設・EditModeテスト必須 |
| **ARIS-5** | #1504 | TyrantToolkitRules — 僭主維持術（貧困化課税・tall poppy排除・大型事業・密告・不信醸成の短/長期効果） | `SecurityRules`×`CoupRules`×`CivicPhiliaRules`×`FiscalRules`×`VacancyRules`。純ロジック新設・EditModeテスト必須 |
| **ARIS-6** | #1505 | （lore）開示データ — 政治的動物・友愛の喪失・oikonomia対chrematistics・僭主の末路 | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`ARIS-1 → ARIS-2`（中間層と公益品質＝アリストテレスの社会経済的 signature。POLY-1/2に社会的基盤を追加）→ `ARIS-3`（収奪志向＝QIN-5と別回路で動機を明示。ARIS-2公益スコアの裏面）→ `ARIS-4`（市民的信頼＝制度が機能する社会条件。ARIS-1/2/3全体を受ける）→ `ARIS-5`（僭主維持術＝具体的ツールキット。ARIS-4のCivicPhiliaを毀損する入力）→ `ARIS-6`（lore＝コード不要・いつでも可）。
