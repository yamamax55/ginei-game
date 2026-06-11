# シュンペーター『資本主義・社会主義・民主主義』参考設計（EPIC #SCHU）

> 参照元：J・A・シュンペーター著『資本主義・社会主義・民主主義』（Capitalism, Socialism and Democracy, 1942）。
> 資本主義の動力は均衡でなく**創造的破壊**にある——古い産業を壊すことで新しい秩序が生まれ、やがて資本主義は自らの成功によって変質する、という巨大な歴史診断。
> 本ドキュメントは、当プロジェクト（Ginei＝星間国家戦略＋既に大型の経済純ロジック層）にとって**役に立つ視点**だけを抽出し、EPIC `#SCHU` として issue 化する提案。
> 著作権注意：固有名・文章・固有設定は流用せず、**経済/政治哲学のメカニクス構造パターンのみ**を参考にする。

---

## 0. なぜ「シュンペーター」が本システムに役立つか

### 既存の経済純ロジック層（当プロジェクトのカバー範囲）

| 既存モジュール | カバー範囲 |
|---|---|
| `ResearchRules`/`ResearchProject`（#123-127） | 研究産出・技術進歩・政体偏りバイアス |
| `MarketRules`/`Good`/`Market`（#179-182） | 需給均衡価格・生活水準→支持 |
| `FiscalRules`/`FiscalState`（#161/162） | 国債/金利/為替・税/社会保障・債務スパイラル |
| `Company`/`StockMarketRules`（#184-185） | 企業・株価・配当・暴落リスク |
| `FirmRules` FRM#1022 | 企業バリューチェーン・商社経済 |
| `Organization`/`SuccessionRules`（#812/#814） | 英雄死後の組織存続・制度化・個人カリスマ |
| `DynastyRules`/`Regime`（#867） | 腐敗・天命喪失・改革・易姓革命 |
| `ConsentRules`/`Polity`（#836） | 合意=統治の借り物・撤収による統治不能 |
| `HopeRules`/`Community`（#852） | 希望枯渇→末人発火 |
| `LeadershipElectionRules`/`PartyRules`（GOV-6/7） | 総裁選・政党・派閥投票 |
| `PersonRules`/`Person`（#866） | 軍人/文民の役割×役職適性 |

**しかし、これらはいずれも「均衡の動態」か「組織の存続」を扱うマクロ論**であり、シュンペーターが固有に描く以下が**欠けている**：

### シュンペーターが固有に持つ視点 × 当プロジェクトでの欠落

| シュンペーターの固有視点 | 当プロジェクトでの欠落 |
|---|---|
| **創造的破壊**：新産業が旧産業を破壊することで資本主義は前進する | `ResearchRules` は生産性を加算するだけ。**新技術が旧市場を萎縮・消滅させる破壊面が無い** |
| **企業家 vs 管理者**：企業家は均衡を破る革新者、管理者は均衡内で最適化する執行者。両者は別の人間類型 | `PersonRules` は軍人/文民だが、**革新者（均衡破壊）と管理者（均衡維持）の弁別が無い** |
| **官僚化による起業家精神の死**：成功した企業は革新から管理へ移行し、やがてイノベーション能力を失う | `Organization.institutionalization`（制度化）は存在するが、**成功→官僚化→イノベーション死の自己消滅ループが無い** |
| **革新クラスターと景気波動**：技術革新は均一でなく塊（クラスター）で来て長波（コンドラチェフ波）を生む | `ResearchRules` は個別プロジェクトだが、**クラスター発生→景気長波の構造が無い** |
| **知識人階級の正統性侵食**：資本主義の成功が知識人余剰を生み、その知識人が体制を批判して正統性を蝕む | `FactionState` に思想ドリフトはあるが、**繁栄→知識人余剰→体制批判→正統性侵食の経路が無い** |
| **競争的民主主義**：民主主義は「人民の意思」でなく競争するエリートが統治者を選ぶ手続き。経済的置換が激しいと扇動政治家が台頭する | `LeadershipElectionRules` はあるが、**経済置換→民主主義の質劣化のフィードバックが無い** |

**結論**：シュンペーターは当プロジェクトの経済システムに**「創造的破壊」という非線形ダイナミクス**と、**①技術革新の波動 ②企業家/管理者の人物類型 ③成功が自らを腐食する自壊ループ**の3欠落軸を与える。特に `ResearchRules`（#123）×`MarketRules`（#179）×`FirmRules`（FRM#1022）×`DynastyRules`（#867）の**接続触媒**として機能する。

---

## 1. 役に立つ視点（要約）

シュンペーターの世界観を、**本システムに効く形**で1行ずつ：

1. **「均衡でなく嵐こそが資本主義」**。最適化より革新の波が経済を動かし、旧産業は必ず滅びる。→ `ResearchRules` に**破壊面**を付けることで初めて「技術で国家が変わる」が成立する。
2. **「革新者は組織の内側から生まれるが、やがて組織に飲み込まれる」**。企業家→管理者への転落が制度疲労の源。→ `Organization` × `ResearchRules` の自己消滅ループ。
3. **「イノベーションは孤立しない——波に乗る者と乗り遅れる者の間で格差が生まれる」**。技術クラスターが有利な勢力と没落する勢力を分ける。→ 戦略レイヤーの**非対称技術競争**の根拠。
4. **「資本主義は自分の成功で子育て係を育て、子供に殺される」**。繁栄が知識人を増やし、知識人が体制批判で正統性を蝕む。→ `FactionState` の**内発的腐食**の新軸。
5. **「民主主義とは競争する政治家がリーダーを決める手続きだ」**。経済的苦境（創造的破壊の犠牲者）が扇動家に票を与える。→ `LeadershipElectionRules` × 経済置換の**フィードバック**。
6. **「歴史は前進し、前進するたびに何かが滅ぶ」**。進歩=生成と破壊の同時性。→ 世界観EPIC（秘史/天井CAP/進化論）への開示データ。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**ResearchRules/MarketRules/DynastyRules/Organization を作り直さない**。SCHU はそれらに**欠落軸を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・シュンペーターのシグネチャ）

#### SCHU 創造的破壊ルール（CreativeDestructionRules）
- **旧市場の萎縮**：新技術が普及するほど対応する旧産業の生産能力が縮小（`DestructionFactor(oldGood, newTech)`）。
- **置換ショック**：旧産業縮小 → 雇用/収入喪失 → `GovernanceRules.OutputFactor`/安定度への負圧（社会コスト）。
- **破壊なき創造はない**：新技術の正味利益 = 創造利得 − 破壊コスト。低コストで抑えた勢力だけが長期で勝つ。
- 接続：`ResearchRules.ResearchOutput` 発火 → 新 `CreativeDestructionRules`（純ロジック・test-first） → `MarketRules` 供給構造変化 + `GovernanceRules` 安定へ波及。

#### SCHU 企業家類型と起業活動（EntrepreneurRules）
- **企業家 vs 管理者**の弁別：`Person` に `entrepreneurialDrive`（0.0〜1.0）を追加。高値＝均衡破壊者、低値＝均衡維持者。
- **企業家が革新を駆動**：勢力内の平均 `entrepreneurialDrive` が `ResearchRules.ResearchOutput` に乗算（高企業家精神＝研究が速い）。
- **起業活動の消耗**：成功するほど `entrepreneurialDrive` が下がり管理者化（後述SCHU-3と接続）。
- 接続：`PersonRules` / `CareerPipelineRules` の拡張 × `ResearchRules` × `Company`（FRM#1022）の革新スコア。

### ★★ 高（自壊ループ・波動）

#### SCHU 官僚化とイノベーション死（BureaucratizationRules）
- **制度化の罠**：`Organization.institutionalization` が高いほど `ResearchRules` 出力に `bureaucracyPenalty` を乗算。
- **成功→安定→惰性**：企業価値（`StockMarketRules`）が高いと組織が現状維持を優先 = `entrepreneurialDrive` 補充が止まる。
- **大組織の鈍化**：梯団/省庁（`OrderOfBattle`/`MinistryRules`）の規模が大きいほど官僚化圧力が増す。
- 接続：`Organization`（#812）× `ResearchRules` × `MinistryRules`（GOV-5）の修正子回路。

#### SCHU 革新クラスターと景気波動（InnovationWaveRules）
- **クラスター判定**：同一勢力で同期間に N 件以上のブレークスルーが重なった時に「革新波」が発生。
- **波のフェーズ**：発生→普及→飽和→崩壊（コンドラチェフ型）の4相 enum。
- **フェーズ別効果**：発生/普及期は `MarketRules` 生産性↑・`FiscalRules.FiscalHealthFactor`↑、飽和/崩壊期は旧市場の創造的破壊（SCHU-1）が加速。
- 接続：`ResearchRules.ResearchProject` クラスター検出 → `InnovationWaveRules`（純ロジック） → `MarketRules` × `FiscalRules` × `GovernanceRules`。

#### SCHU 知識人階級と正統性侵食（IntellectualCritiqueRules）
- **繁栄 → 知識人余剰**：`FiscalRules.FiscalHealthFactor` が高い間、勢力の「知識人資本」が蓄積。
- **体制批判**：知識人資本が閾値を超えると `FactionState.Stability` を緩やかに侵食（`LegitimacyErosion`）。
- **創造的破壊が加速する**：SCHU-1 の置換ショックが大きいほど体制批判は強くなる（犠牲者が知識人の言葉に乗る）。
- 接続：`FiscalRules` × 新 `IntellectualCritiqueRules`（純ロジック）× `DynastyRules.腐敗` × `HopeRules`。

### ★ 中（民主主義の質・lore）

#### SCHU 競争的民主主義と経済置換（CompetitiveDemocracyRules）
- **民主主義の手続き論**：民主主義 = 競争するエリートが統治者を選ぶ手続き。「人民の意思」は二次的。
- **経済置換 → 扇動政治家台頭**：SCHU-1 の置換ショックが大きいほど `LeadershipElectionRules` での大衆票が極端化（`PopulistPressure`）。
- **民主的品質の劣化**：扇動政治家の当選確率が上がると `GovernmentRegistry` の人事が歪む（能力より人気）。
- 接続：`CreativeDestructionRules`（置換ショック）× `LeadershipElectionRules` × `PartyRules` × `GovernmentRegistry`。

#### SCHU（lore）世界観の開示データ
- 「創造的破壊＝進歩と喪失は同一の出来事」「資本主義は自らの成功で変質する」「民主主義は手続きであって真理でない」。
- **コード新設なし**：`DisclosureLedger`（FND-4）への lore データ入力。世界観EPIC（秘史/天井CAP/啓蒙/エンディング）へ接続。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| マルクス型の階級闘争・搾取論 | `RedistributionRules`/`ClassTension`（#163）が既存でカバー |
| 独占資本主義（後期シュンペーター）の価格支配 | `MarketRules` の均衡モデルに薄く接続するだけ（新EPIC不要） |
| 社会主義への具体的移行シナリオ | `DynastyRules.Revolution`（#867）が革命一般をカバー |
| 景気循環の数学的タイミング（在庫循環/設備投資循環） | `FiscalRules` で十分。周期の精密化はタイクン化回避違反 |
| 資本主義以前の制度遺産（封建制の残滓） | `FeudalRules`（#168/#169）がカバー |
| 起業家の心理プロファイル詳細 | `PersonRules.Effectiveness` × `GrowthRules` に薄く接続するだけ |

---

## 3. EPIC #SCHU の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。既存経済ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章は不使用、**メカニクス/哲学構造のみ**参考。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **SCHU-1** | #1581 | 創造的破壊ルール（`CreativeDestructionRules`・新技術が旧市場を萎縮させる破壊面＋置換ショック） | 新 `CreativeDestructionRules`。`ResearchRules`×`MarketRules`×`GovernanceRules` |
| **SCHU-2** | #1584 | 企業家類型と起業活動（`EntrepreneurRules`・均衡破壊者 vs 管理者の人物弁別） | `PersonRules` / `CareerPipelineRules` 拡張 × `ResearchRules` × FRM#1022 |
| **SCHU-3** | #1587 | 官僚化とイノベーション死（`BureaucratizationRules`・成功→制度化→革新力喪失の自壊ループ） | `Organization`（#812）× `ResearchRules` × `MinistryRules` |
| **SCHU-4** | #1591 | 革新クラスターと景気波動（`InnovationWaveRules`・コンドラチェフ型4フェーズ） | `ResearchRules.ResearchProject` クラスター検出 × `MarketRules` × `FiscalRules` |
| **SCHU-5** | #1595 | 知識人階級と正統性侵食（`IntellectualCritiqueRules`・繁栄→知識人余剰→体制批判の経路） | `FiscalRules` × `DynastyRules.腐敗` × `HopeRules` × `FactionState` |
| **SCHU-6** | #1598 | 競争的民主主義と経済置換（`CompetitiveDemocracyRules`・置換ショック→扇動政治家→民主的品質劣化） | SCHU-1 置換ショック × `LeadershipElectionRules` × `PartyRules` |
| **SCHU-7** | #1600 | （lore）世界観の開示データ（創造的破壊の必然性／資本主義の自壊／手続き民主主義の本質） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順

`SCHU-1 → SCHU-2`（創造的破壊＋企業家類型＝最も固有で欠落の大きいシグネチャ）→ `SCHU-3`（官僚化＝自壊ループの核）→ `SCHU-4`（革新波動＝SCHU-1/2/3を束ねる長期構造）→ `SCHU-5`（知識人批判＝SCHU-1の社会的帰結）→ `SCHU-6`（民主主義への接続）→ `SCHU-7`（lore）。

> いずれも既存経済/社会ロジックを**後退させず接続**する additive 設計。`ResearchRules`（#123）× `FirmRules`（FRM#1022）× `DynastyRules`（#867）の接触点に最も効く。
