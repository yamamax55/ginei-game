# 『失敗の本質』参考設計（EPIC #SHP）

> 参照元：『失敗の本質』（戸部良一・寺本義也・鎌田伸一・杉之尾孝生・村井友秀・野中郁次郎、1984年）。
> 旧日本軍の6つの作戦（ノモンハン・ミッドウェー・ガダルカナル・インパール・レイテ沖・沖縄）を事例研究として、**組織の構造的病理**を分析した古典。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略＋既に厚い組織/人物/社会シミュ層）にとって**役に立つ視点**だけを抽出し、EPIC `#SHP` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**組織メカニクス／意思決定の構造パターンのみ**を参考にする。

---

## 0. なぜ『失敗の本質』が本システムに役立つか

当プロジェクトは組織・人物・社会シミュの**純ロジック層を大量に保有**している：

| 既存（組織/人物/社会） | カバー範囲 |
|---|---|
| `SeniorityRules`（LIFE-5/6） | 席次 vs 実力の昇進競争・政体依存の固さ |
| `PersonRules`/`Person`（正名 #866） | 軍才/文才・役職適性・適材適所 |
| `MeritRankRules`（QIN-2 #900-905） | 戦功→爵位・インセンティブ士気・法家の罠 |
| `Organization`/`SuccessionRules`（#812） | 組織の結束/制度化/カリスマ後継 |
| `DynastyRules`/`Regime`（#867） | 腐敗サイクル・天命喪失・易姓革命 |
| `CounselIntegrityRules`（MKV-3） | 佞臣の供給側（諫言の品質劣化） |
| `RemonstranceRules`（JGS-1） | 納諫ループ（君主の受容性×臣のフラストレーション） |
| `MinistryRules.SectionalismFriction`（GOV-5 #158） | 省益摩擦＝横断政策への抵抗係数 |
| `AutonomyRules`/`CommandDoctrine`（#544-550） | 自律分散 vs 集団依存の指揮系統 |
| `EspionageRules` | 情報収集・察知・工作 |
| `FactionState`/`FactionStateRules` | 国家状態の合成（正統性/合意/結束/希望） |
| `EventEngine`（#116） | 条件発火→選択肢→効果のデータ駆動イベント |

**しかし、これらは「個人の諫言・昇進・省益」のレベルで動く**。『失敗の本質』が固有に描く以下が**欠けている**：

| 『失敗の本質』が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **「空気」が支配する組織——異論を言う前に沈黙させる集合的圧力** | JGS-1/MKV-3 は「言った後に受け入れられるか」。**言う前に封じる集合的雰囲気**（空気）が無い |
| **成功体験への固執＝敗戦から学ばない組織学習の欠如** | `SuccessionRules` は英雄後継。**敗北の分析→能力改善の制度的フィードバック**が無い |
| **戦略的固執（損切りできない）＝埋没コストの罠** | `DynastyRules` は腐敗/天命。**失敗している作戦を止められない**サンクコスト型の固執が無い |
| **階層を登るほど悪報が変形される（情報の階層歪曲）** | `EspionageRules` は外部情報収集。**内部の悪報が上位へ伝わる過程での歪曲・圧縮**が無い |
| **作戦レベルの欠落＝戦術↔大戦略の中間ドクトリンが無い** | `AdmiralData.operation`/`intelligence` は「未使用（将来用）」。**作戦次元の計画能力が戦略成果へ接続されていない** |

**結論**：『失敗の本質』は既存システムに**「組織病理の動態」という5つの欠落軸**を与える。
①「空気」が異論を封じる→ ②悪報が上に届かない→ ③失敗しても学ばない→ ④埋没コストで撤退できない→ ⑤作戦レベルの欠如で戦略と戦術がつながらない——この**連鎖**がなぜ組織が滅ぶかの骨格であり、既存の `Organization`/`FactionState`/`DynastyRules` に**動的な病理カスケード**を与える。

---

## 1. 役に立つ視点（要約）

1. **「空気」は制度を超えて意思決定を支配する**。会議室の雰囲気が合理的分析を封じ、発言されない結論が「空気」として決定される。→ 諫言の前段階：**集合的沈黙の生成**を `AtmosphereRules` として純ロジック化。JGS-1（納諫受容性）・MKV-3（佞臣供給側）と合わせて三層の諫言病理が揃う。
2. **組織は敗北から学ばない——学ぶ制度が無ければ**。ミッドウェーの教訓はなぜ活かされなかったか。→ `Organization.institutionalization` に**学習フィードバック**の軸を足す（`OrganizationalLearningRules`）。既存の `SuccessionRules`（人の継承）と直交する**知識の継承**。
3. **「もうひと押し」が国を滅ぼす（埋没コスト）**。撤退は可能だったが撤退できなかった。→ 戦略的失敗状態で**逃げられなくなる**サンクコスト型固執（`EscalationCommitmentRules`）。`DynastyRules`（腐敗）・`ConsentRules`（協力の撤退）とは別系統の**意思決定のロック**。
4. **悪報は上に届く前に「良報」に変形される**。前線の敗北報告は大本営発表になる。→ 階層的情報歪曲（`InformationDistortionRules`）。`EspionageRules`（外部情報収集精度）と組み合わせると**「諜報は当たっても組織が受け取れない」**という最も深い病理が描ける。
5. **戦術的天才は作戦的凡庸では足りない**。個別の戦闘に強くても、複数作戦を繋ぐ「作戦レベル」の計画力が無ければ戦略目標に届かない。→ `AdmiralData.operation` を `OperationalDoctrineRules` に接続し、提督の作戦能力が戦役全体の効率に波及する実効値パターン。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**既存の `Organization`/`FactionState`/`DynastyRules`/`RemonstranceRules`（JGS-1）/`CounselIntegrityRules`（MKV-3）を作り直さない**。SHP は欠落軸を**additive に足し、接続する**だけ。

### ★★★ 最優先（真の欠落・失敗の本質の signature）

#### SHP-1 「空気」組織雰囲気ルール（集合的沈黙の生成）
- **`OrganizationalAtmosphere`**（非MB純データ）：勢力の「空気」スコア（圧力0.0→1.0）。高いほど組織内で異論が言えない。
- **`AtmosphereRules`**（static・純ロジック）：
  - `AtmospherePressure(FactionState, successStreak, recentDefeatAcknowledged)` → 連戦連勝・敗北否認・権威集中で上昇
  - `InformationSuppressed(atmosphere)` → 「空気」圧力が高いと異論が**事前に自己検閲**される確率（0..1）
  - `PolicyQualityFactor(atmosphere, counselIntegrity, remonstrance)` → 空気×佞臣（MKV-3）×納諫（JGS-1）の三層を掛け合わせた政策決定品質係数（実効値パターン・基準値非破壊）
- 接続：`FactionState.Organization`（結束が高いほど空気も硬直）・`CounselIntegrityRules`（MKV-3 供給）・`RemonstranceRules`（JGS-1 受容）・`EventEngine`（空気崩壊イベント）。
- **EditMode テスト必須**：`AtmospherePressure` 計算式 / `PolicyQualityFactor` の三層合成 / `InformationSuppressed` の閾値。

#### SHP-2 組織学習能力（敗北のフィードバックループ）
- **`LearningCapacity`**（非MB純データ）：勢力の学習スコア（制度的分析力 0..1）。高いほど敗北から能力を改善できる。
- **`OrganizationalLearningRules`**（static・純ロジック）：
  - `LearnFromDefeat(defeat, learningCapacity)` → `LearningDelta`（能力改善量）を返す（低 capacity は negative feedback を逃す）
  - `InstitutionalMemory(organization)` → `Organization.institutionalization` から学習の組織化度を導出
  - `CapacityDrift(atmosphere, defeatAcknowledged)` → 「空気」が高いと capacity が低下（SHP-1 と連鎖）
- 接続：`Organization.institutionalization`（LIFE-2 #152 継承と直交する知識継承）・`AtmosphereRules`（SHP-1 と連鎖）・`FactionState.Stability` へ長期寄与。
- **EditMode テスト必須**：学習量計算 / 空気との連鎖劣化 / 長期シミュ（10ターン）。

### ★★ 高（意思決定の固着・情報の歪曲）

#### SHP-3 エスカレーション・コミットメント（損切りできない組織）
- **`EscalationBias`**（非MB純データ）：勢力のサンクコスト型固執スコア（0..1）。蓄積した投資・公言・権威が撤退を封じる。
- **`EscalationCommitmentRules`**（static・純ロジック）：
  - `CommitmentGrowth(investmentRatio, publicCommitment, authorityStaked)` → 投資量/公言/面子で蓄積
  - `WithdrawalCost(bias, escalation)` → 撤退の政治的コスト（高いほど撤退不能）
  - `IsLocked(bias, threshold)` → 固執ロック判定（`CoupRules.CoupRisk` と同形の閾値判定）
  - `EscalationDecay(defeat, acknowledgment)` → 敗北を公式に認めると固執が解ける（SHP-2 学習と接続）
- 接続：`StrategyRules`（戦略継続/撤退の判断に係数として乗る）・`DynastyRules`（腐敗とは別系統の固着）・`ConsentRules`（協力撤退との二層）・`AtmosphereRules`（空気が高いと撤退発言も封じる SHP-1 連鎖）。
- **EditMode テスト必須**：固執蓄積/解除 / IsLocked 閾値 / コスト計算。

#### SHP-4 階層的情報歪曲（悪報の圧縮・大本営発表モデル）
- **`InformationDistortionRules`**（static・純ロジック）：
  - `DistortionFactor(hierarchyDepth, atmospherePressure, counselIntegrity)` → 組織の深さ×空気圧力×佞臣係数で**情報が届く前に変形される率**
  - `EffectiveIntelligence(rawIntel, distortion)` → 実際に意思決定層が受け取る情報品質（高層ほど低下）
  - `WakeUpEvent(cumulativeGap, threshold)` → 現実と認識のギャップが閾値を超えると「大本営発表崩壊」イベントトリガー
- 接続：`EspionageRules`（外部情報収集精度→ここで内部歪曲を掛ける二段モデル）・`AtmosphereRules`（SHP-1）・`GovernmentRegistry`（階層深さの計算元）・`EventEngine`（崩壊イベント）。
- **EditMode テスト必須**：歪曲係数計算 / EffectiveIntelligence 連鎖 / 閾値崩壊判定。

### ★ 中（作戦ドクトリン・長期実効値）

#### SHP-5 作戦ドクトリン（戦術↔大戦略の中間レベル）
- **`OperationalDoctrineRules`**（static・純ロジック）：
  - `DoctrineQuality(operation, intelligence)` → `AdmiralData.EffectiveOperation`/`EffectiveIntelligence` から作戦計画品質（0..1）を導出（**未使用だった `operation`/`intelligence` 能力値を初めて意味のある効果へ接続**）
  - `CampaignEfficiency(doctrine, fleetCount, coordinationScore)` → 複数艦隊の作戦連携効率（個別戦闘の和を超える相乗効果 or それを妨げる縦割りペナルティ）
  - `CoordinationScore(orderOfBattle)` → `OrderOfBattle` の梯団ツリー深さと司令配置から算出（縦割り＝深く複雑なほど低下）
- 接続：`AdmiralData.EffectiveOperation`/`EffectiveIntelligence`（実効値パターン・基準値非破壊）・`OrderOfBattle`（梯団組織）・`CampaignRules`（戦役効率への寄与）・`MinistryRules.SectionalismFriction`（縦割り係数の軍事版）。
- **EditMode テスト必須**：DoctrineQuality 計算 / CampaignEfficiency の相乗/ペナルティ境界 / CoordinationScore。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 慢心・奢り（Victory Disease）の新設 | **JGS-3 が既にカバー**（成功→非線形腐敗増速・受容性ブレーキ）。SHP は JGS-3 に連鎖するだけ |
| 席次制度そのものの再実装 | **`SeniorityRules`（LIFE-5/6）が既にカバー**。硬直係数は政体で変わる |
| 個人の佞臣・諫言品質の新設 | **MKV-3（`CounselIntegrityRules`）が供給側・JGS-1（`RemonstranceRules`）が受容側をカバー**。SHP は「言う前に封じる空気」という前段階のみ |
| 精神力を物量の代替とするメカニクス | **`HopeRules`/`FocusRules` が部分カバー**。新モジュール不要（SHP-1 の `PolicyQualityFactor` に係数として波及させる） |
| 陸海軍縦割りの組織新設 | **`MinistryRules.SectionalismFriction`（GOV-5）が省益摩擦をカバー**。SHP-5 の `CoordinationScore` で軍事版を派生させれば十分 |
| 作戦の物理配置・兵站マイクロ管理 | **`SupplyRules`/`CommerceRaidingRules`/`GalaxyMap` がカバー**。タイクン化回避 |

---

## 3. EPIC #SHP の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。既存組織ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/組織病理の構造パターンのみ**参考。

> **EPIC = #1369**。GitHub issue 起票済み（#1371・#1375・#1378・#1383・#1388）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **SHP-1** | #1371 | 「空気」組織雰囲気ルール（`AtmosphereRules`/`OrganizationalAtmosphere`：集合的沈黙圧力・政策品質三層合成） | `FactionState.Organization`×MKV-3×JGS-1。諫言病理の前段層 |
| **SHP-2** | #1375 | 組織学習能力（`OrganizationalLearningRules`：敗北→能力改善のフィードバック・制度的記憶） | `Organization.institutionalization`×`AtmosphereRules`×`FactionState` |
| **SHP-3** | #1378 | エスカレーション・コミットメント（`EscalationCommitmentRules`：損切り不能・固執ロック・崩壊解除） | `StrategyRules`×`DynastyRules`×`ConsentRules`×SHP-1 |
| **SHP-4** | #1383 | 階層的情報歪曲（`InformationDistortionRules`：悪報の変形・大本営発表崩壊イベント） | `EspionageRules`×`AtmosphereRules`×`EventEngine`×`GovernmentRegistry` |
| **SHP-5** | #1388 | 作戦ドクトリン（`OperationalDoctrineRules`：`operation`/`intelligence` 能力を戦役効率へ接続・縦割り協調係数） | `AdmiralData.EffectiveOperation`/`EffectiveIntelligence`×`OrderOfBattle`×`CampaignRules` |

### 推奨着手順
`SHP-1`（空気＝全連鎖の根・三層病理の前段）→ `SHP-4`（情報歪曲＝SHP-1 を使う最初の応用）→ `SHP-2`（学習能力＝SHP-1 連鎖の出口）→ `SHP-3`（固執＝SHP-1/2 両方と連鎖）→ `SHP-5`（作戦ドクトリン＝`operation` 能力値の初配線・独立性高い）。

> SHP-1 の `AtmosphereRules` が他4件の係数を供給する設計のため、SHP-1 を先に固定してから後続を実装するのが最短パス。いずれも**既存組織ロジックを後退させず additive に接続**する設計。
