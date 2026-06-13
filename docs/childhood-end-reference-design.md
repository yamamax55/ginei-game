# クラーク『幼年期の終り』参考設計（EPIC #CEND）

> 参照元：アーサー・C・クラーク著 *Childhood's End*（1953）。地球に飛来した「オーバーロード（管理者）」が人類を平和な黄金時代へ導くが、その目的は人類の最後の世代が集合意識「オーバーマインド」へ収束・変容するための「産婆」を務めることだった——そして管理者たち自身は決して変容できない。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略）にとって**役に立つ構造パターンのみ**を抽出し、EPIC `#CEND` として issue 化する提案。
> **著作権注意**：固有名（オーバーロード/オーバーマインド/カレルン等）・文章・キャラクター・固有設定は流用しない。**メカニクス／世界観の構造パターンのみ**を参考にする。

---

## 0. なぜ「幼年期の終り」が本システムに役立つか

当プロジェクトはすでに社会崩壊・政体交代・文明サイクルを幅広くカバーしている：

### 既存（カバー済み）

| 既存モジュール | カバー範囲 |
|---|---|
| `DynastyRules`/`Regime`（#867） | 王朝サイクル・天命喪失・易姓革命（政体交代） |
| `PHNX` `CollapseRules`（#PHNX） | 文明の崩壊・再建サイクル（技術暴走・人口激減によるフェーズ崩壊） |
| `HopeRules`/`Community`（#852-856） | 希望・絶望・末人の発火 |
| `ConsentRules`/`Polity`（#836） | 合意の撤回・統治不能（権力は借り物） |
| `AutonomyRules`/`CommandDoctrine`（#544-550） | 軍事指揮の自律性vs集団依存 |
| `FactionStateRules`/`FactionState` | 正統性・腐敗・結束・希望の合成 |
| `DisclosureLedger`（FND-4 #495） | 秘史の連鎖開示・エンディング解放 |
| `ResearchRules`/`ResearchProject`（#123-127） | 研究進捗・一方向的進展 |
| `FactionRelations`（#189 DIP-1） | 勢力間の外交・非敵対設定 |
| `LogisticsRules`（#844） | 版図一体化度・補給線 |
| `Organization`/`SuccessionRules`（#812） | カリスマ死後の組織存続・継承 |

### 幼年期の終りが固有に持つ視点 × 当プロジェクトでの欠落

| 幼年期の終りの構造パターン | 当プロジェクトでの欠落 |
|---|---|
| **文明の変容（卒業）= 成功としての退場** | PHNX `CollapseRules` は「失敗による退場」（技術暴走・人口崩壊）。CEND は**発展の成功により競争ゲームボードを「卒業」する**——これは崩壊ではなく完成。正統性×安定度×研究×希望が閾値を超えたとき、勢力は通常の勝利判定から外れ超越状態へ移行する。現在どの純ロジックもこれをカバーしない |
| **後見人依存による発展阻害（恩恵の鎖）** | `AutonomyRules` は軍事指揮の自律度。`ConsentRules` は被統治者側の合意。**外部の庇護者から援助を受け続けることが自律的な発展能力を削ぐ**というジレンマが無い。短期安定を買う代わりに長期の自律成長力を失う「依存の罠」 |
| **触媒の非対称性（産婆は生まれられない）** | `Organization`/`SuccessionRules` は英雄死後の組織存続。**他者の変容を可能にする「仲介役」が、まさにその仲介特化ゆえに自身は変容できない**構造的非対称が無い。ブローカー・仲介勢力（フェザーン類型）の本質的制約 |
| **集合意識への収束（個から全体への垂直な断絶）** | `ConsentRules` は合意の「水平的撤回」。`AutonomyRules` は個人の自律。**個としての文明が上位の集合意識へ垂直に吸収される**概念が無い。これは「同盟→統合」の延長ではなく、種としての次の段階 |

**結論**：幼年期の終りは当プロジェクトに4つの欠落軸を提供する：
1. **文明変容閾値**（CEND-1）—— 発展の成功によるゲームボード「卒業」。PHNX の「底の崩壊」に対する「頂点の昇華」軸
2. **後見依存ジレンマ**（CEND-2）—— 保護を受けるほど自律発展力が衰える依存の罠
3. **触媒の非対称性**（CEND-3）—— 他者の変容を助ける専門化が自己変容を阻む構造的逆説
4. **開示lore**（CEND-4）—— 「DisclosureLedger」への天井CAP×エンディング連鎖データ（コード新設なし）

これらはすべて **additive**（既存モジュールを後退させない）であり、戦略ゲームに「征服でも崩壊でもない第三の出口」という縦軸を与える。

---

## 1. 役に立つ視点（要約）

幼年期の終りの世界観構造を、**本システムに効く形**で1行ずつ：

1. **文明には発展の先に「卒業」がある**——崩壊でも征服でもなく、達成による離脱。→ `TranscendenceRules` で「勝利スコア至上の競争」を超える第三出口を作る。`CampaignRules.LeadingFaction` に超越勢力の除外軸を足す。
2. **保護は能力を借りるが能力を育てない**——援助を受け続ける勢力は自律した発展力を喪失する。→ `PatronageTrapRules` が外部依存度を蓄積し、有機的成長係数に上限を設ける。短期利益vs長期能力のトレードオフ。
3. **仲介を極めると変容から外される**——すべての交易を通すブローカーは最も豊かになるが、誰も辿り着けない境地へは辿り着けない。→ `CatalystRules` が触媒特化度を `TranscendenceEligibility` のペナルティとして計算。フェザーン（#160）の本質的ジレンマ。
4. **黄金時代は探求心を殺す**——物質的満足が外部から与えられると希望の質が変わる。→ `PatronageTrapRules` と `HopeRules` の接続：援助依存が `Community.hope` の**外発型希望**（依存継続への期待）を生成し、内発型希望（自律的挑戦）を押し出す。
5. **深層記憶は未来の像を過去に投影する**——なぜ先祖が悪魔を恐れたかは、子孫が悪魔の姿になるからだ。→ コード新設なし。`DisclosureLedger` への開示データ。超越完了で「我々が恐れていたものは、なりゆく姿だった」を解禁。
6. **超越は銀河的必然——だが管理者は傍らで見守るだけ**——最も高度な知性でも宇宙の変容サイクルには乗れない。→ `CatalystRules.CanTranscend` が CEND-1 とクロスし、仲介特化勢力を超越判定から除外する。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`DynastyRules`/`FactionState`/`HopeRules`/`DisclosureLedger` を作り直さない**。CEND はそれらに**欠落軸を足し接続する**（additive）。タイクン化回避＝マイクロ操作を増やさず閾値・係数・フェーズで駆動し創発帰結を出す。

### ★★★ 最優先（真の欠落・幼年期の終りの signature）

#### CEND 文明変容閾値（`TranscendenceRules`/`TranscendenceState`）

**ゲーム的意義**：勝利条件が「相手を滅ぼす」だけでなく「自らを完成させる」という第三の出口を生む。超越した勢力はもはや通常の競争に参加しない——これはゲームボードから「格上に去る」ことで、崩壊（PHNX）の対極をなす。

**純ロジック設計**：
- `TranscendenceState`: `transcendenceScore`(0..1)/`isTranscending`/`isTranscended`
- `TranscendenceRules`(static):
  - `EligibilityScore(factionState)`: `stability × legitimacy × researchProgress × (1 - dependencyRatio)` → 0..1（`PatronageTrapRules.DependencyRatio` を引数で受ける）
  - `IsEligible(score, params)`: `score ≥ threshold` で変容開始適格
  - `BeginTranscendence(state)`: 不可逆フラグを立てる（状態は変えない・純ロジック）
  - `IsExcludedFromVictory(state)`: 超越済みは通常勝利判定から除外
  - `TranscendenceParams{threshold(既定0.85), stabilityWeight, legitimacyWeight, researchWeight}`
- 接続：`FactionStateRules.Tick`（安定度・正統性・希望を集約してスコアを計算）、`CampaignRules.LeadingFaction`（超越勢力を除外して最有力勢力を算出）、`DisclosureLedger.TryReveal`（超越完了がエンディング開示チェーンを解放・CEND-4）
- **EditMode テスト必須**：スコア計算・閾値判定・`IsExcludedFromVictory` の単体テスト

### ★★ 高（既存システムへの依存コスト軸の追加）

#### CEND 後見依存ジレンマ（`PatronageTrapRules`/`PatronageState`）

**ゲーム的意義**：外交的保護・艦隊援助・物資供給を受け入れることへの長期コストを明示する。短期安定と長期発展能力のトレードオフが、外交・戦略判断に深みを生む。オーバーロードが人類に平和を与えたことで人類は自力探査能力を失った——の構造。

**純ロジック設計**：
- `PatronageState`: `dependencyRatio`(0..1)/`aidAccumulator`(累積援助量)
- `PatronageTrapRules`(static):
  - `AccumulateDependency(state, aidAmount, ownProduction)`: 外部援助比率が高いほど `dependencyRatio` 上昇
  - `DependencyDecay(state, dt, params)`: 援助なし期間で依存度が自然低下（`PatronageParams.decayRate` 既定0.05/年）
  - `OrganicGrowthCap(dependencyRatio)`: 有機的成長係数の上限（`1.0 - dependencyRatio × capStrength`、下限`minOrganicCap`=0.3）
  - `IsStunted(ratio, threshold)`: 発展阻害判定
  - `PatronageParams{decayRate, capStrength, stuntedThreshold}`
- 接続：`FactionStateRules.Tick`（成長係数の上限として `OrganicGrowthCap` を乗算）、`TranscendenceRules.EligibilityScore`（依存度が変容スコアを削る）、`FleetPoolRules`/`ShipyardRules.CommissionToPool`（外部供給艦が `aidAmount` にカウント）、`LogisticsRules.CohesionFactor`（補給依存が一体化度を下げる経路）
- **EditMode テスト必須**：依存度蓄積・有機成長上限計算・自然低下の単体テスト

#### CEND 触媒の非対称性（`CatalystRules`）

**ゲーム的意義**：仲介・ブローカー役に特化した勢力が自己の変容（CEND-1）を達成できないという構造的制約。フェザーン（#160 商社国家）に「利益の最大化は達成できるが、卒業できない」という固有の重みを与える。超越できない仲介者の悲劇が、プレイヤーに役割選択の意味を問う。

**純ロジック設計**：
- `CatalystRules`(static):
  - `CatalystBonus(catalystRatio, targetDevelopmentFactor)`: 触媒役が相手に与える発展加速倍率（`1.0 + catalystRatio × catalystStrength`）
  - `SelfAdvancementPenalty(catalystRatio)`: 触媒特化度が高いほど自律進歩係数が下がる（`1.0 - catalystRatio × penaltyStrength`、下限0.2）
  - `CanTranscend(catalystRatio, threshold)`: 触媒比率が閾値超なら変容不適格（`TranscendenceRules.IsEligible` に渡す追加条件）
  - `CatalystParams{catalystStrength(既定0.3), penaltyStrength(既定0.4), exclusionThreshold(既定0.7)}`
- 接続：`TranscendenceRules.EligibilityScore`（`CanTranscend` が false なら変容スコアを0クランプ）、`FactionRelations`/`DiplomacyRules`（仲介特化度は「非敵対勢力の数 / 全勢力」で近似）、`FactionStateRules`（触媒ボーナスを同盟勢力の `Tick` に適用）
- **EditMode テスト必須**：ボーナス計算・ペナルティ計算・`CanTranscend` の組み合わせテスト

### ★ 中（lore・開示データ）

#### CEND（lore）深層記憶・予型論・天井CAP開示（`DisclosureLedger` への入力データ）

**ゲーム的意義**：「なぜ宇宙の深奥を畏れるのか」という謎が、変容の進行に応じて少しずつ解禁される。超越達成がエンディング開示チェーンのトリガーになる。コード新設なし——純粋に `DisclosureLedger` へのデータ入力。

**開示設計**：
- 断片1（早期：`TranscendenceScore ≥ 0.3`）：「太古より伝わる恐怖の像——それは敵ではなく、先を行くものの影だった」
- 断片2（中期：`TranscendenceScore ≥ 0.6`）：「変容しようとする者の傍らで、変容できない者がいつも見守っている。それが宇宙のルールだ」
- 真相（変容完了：`isTranscended = true`）：「我々が恐れていたのは外から来るものではなく、自分たちがなりゆく姿だった」＋エンディング解放
- 接続：`DisclosureLedger`（FND-4）× `TranscendenceRules.IsTranscended`（CEND-1）× `EventEngine`（#116）

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 集合意識としての「オーバーマインド」を新規エンティティとして実装 | 超越勢力の「ゲームボード退場」で十分に表現できる。新エンティティは過剰設計でタイクン化 |
| 技術文明の停滞メカニクス | `BNW BNW-2`「安定vs活力の政策選択」と重複。CEND の視点は停滞の内的原因でなく外的依存による阻害（`PatronageTrapRules` が担う） |
| 「最後の人間」の個人スケールの物語イベント | `EventEngine`（#116）の個別イベントとして足せるが、CEND EPIC の純ロジック範囲外。後続 issue で個別対応 |
| 人類の段階的進化を生物レベルで表現 | `DemographicsRules`（#153）が人口動態をカバー。生物的変容の新規実装は過剰。係数の変動で表現 |
| 宇宙的時間スケール（億年単位）の歴史サイクル | `PHNX TechRegressionRules` と `CyclicPatternRules` でカバー済み。超長期サイクルの新規軸は不採用 |

---

## 3. EPIC #CEND の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。既存モジュールは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #2351**。GitHub issue 起票済み（#2355〜#2364）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **CEND-1** | #2355 | 文明変容閾値（`TranscendenceRules`/`TranscendenceState`）— 発展の成功によるゲームボード卒業 | `FactionStateRules`×`CampaignRules.LeadingFaction`×`DisclosureLedger`。PHNX崩壊軸の対極 |
| **CEND-2** | #2358 | 後見依存ジレンマ（`PatronageTrapRules`/`PatronageState`）— 外部援助が有機的成長上限を設ける | `FactionStateRules`×`TranscendenceRules.EligibilityScore`×`FleetPoolRules` |
| **CEND-3** | #2361 | 触媒の非対称性（`CatalystRules`）— 仲介特化勢力は変容できない（フェザーン・ジレンマ） | `TranscendenceRules`×`FactionRelations`×`FactionStateRules` |
| **CEND-4** | #2364 | （lore）深層記憶・予型論・天井CAP開示データ（`DisclosureLedger` 入力） | コード新設なし。CEND-1完了トリガー×`DisclosureLedger`×`EventEngine` |

### 推奨着手順
`CEND-1 → CEND-2`（変容閾値と依存ジレンマは直結する核。まず純ロジックを固める）→ `CEND-3`（触媒非対称はCEND-1に依存する拡張）→ `CEND-4`（開示データはCEND-1完了後に書く）。

> いずれも既存モジュールを**後退させず接続する** additive 設計。フェザーン（#160）・覇権交代・PHNX文明サイクルと連動し、「征服」「崩壊」に続く第三の終幕「卒業」を戦略ゲームにもたらす。
