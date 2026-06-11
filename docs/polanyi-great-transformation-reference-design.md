# ポランニー『大転換』参考設計（EPIC #POLA）

> 参照元：カール・ポランニー『大転換 ― 市場社会の形成と崩壊』（1944）。
> 19世紀文明（自由市場・金本位制・勢力均衡・自由主義国家）がなぜ崩壊したかを解き明かす政治経済の古典。
> 本ドキュメントは、当プロジェクト（Ginei＝星間国家戦略＋既に多数の社会・経済純ロジック層）にとって
> **役に立つ視点だけを抽出**し、EPIC `#POLA` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**政治経済メカニクス／社会構造パターンのみ**を参考にする。

---

## 0. なぜ『大転換』が本システムに役立つか

当プロジェクトは社会・経済の**マクロ純ロジックを大量に保有**している（[CLAUDE.md] 参照）：

| 既存（マクロ・抽象） | カバー範囲 |
|---|---|
| `MarketRules`/`Good`/`Market`（#179-182） | 市場ごとの需給均衡価格・生活水準→支持 |
| `FiscalRules`/`FiscalState`（#161/162） | 国債/金利/為替・税/社会保障 |
| `RedistributionRules`/`TaxStructure`（#163） | 累進課税・階級別負担・階級対立 |
| `ConsentRules`/`Polity`（#836） | 合意の構造・非協力（ボイコット） |
| `HopeRules`/`Community`（#852） | 希望と末人（ロンドン派）・絶望の敷居 |
| `DiplomacyRules`/`DiplomacyState`（#189） | 二国間条約・宣戦/講和・opinion修正子 |
| `LogisticsRules`（#844） | 版図の一体化度（連結成分→国力係数） |
| `Population`/`DemographicsRules`（#153） | 人口ボーナス/オーナス・出生/死亡/老齢化 |
| `GovernanceRules`/`Province`（#109） | 安定度・統合度・占領→不満の動学 |
| `FactionStateRules`/`FactionState` | 社会・政治シミュ層の合成（最上層） |

**しかし、これらは「国家・市場・人口」という抽象単位の均衡であり、ポランニーが固有に描く以下が欠けている**：

| 『大転換』が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **市場の埋め込み度（embeddedness）** | 市場が社会的制約からどれだけ「解放」されているかの指標が無い。自由化すると効率↑・不安定↑のトレードオフが表現できない |
| **二重運動（Doppelbewegung）** | 市場拡大が社会的限界を超えると保護的反動が自動発生する回路が無い。`PartyRules`/`ConsentRules` は政治反応だが「市場圧力→保護制度の自動建設」という**エンジン**が欠けている |
| **擬制商品ストレス（fictitious commodity）** | 労働（`Population`）・土地（`StarSystem`）を完全商品として扱う時の**固有の制度的不安定**が無い。`GovernanceRules.stability`の一般的低下とは別軸 |
| **多極経済秩序の相互支持（四本柱）** | `DiplomacyRules` は二国間。**勢力均衡×自由貿易×通貨安定×自由主義国家が互いを支え合い、一本が崩れると連鎖する**多極システム安定が無い |
| **社会保護制度の内生的成長** | `FiscalRules.WelfareCost` は外生的コスト。二重運動の産物として**保護制度が政治的に建設され定着していく動学**が無い |

**結論**：『大転換』は当プロジェクトの経済・政治に**「市場 vs 社会の緊張という根源的エンジン」**と、
①市場の埋め込み度 ②二重運動回路 ③擬制商品ストレス ④多極秩序の相互支持 ⑤保護制度の内生的成長 という5つの欠落軸を与える。
**フェザーン（#160 商社国家）・財政（#161-163）・統治（#109）・外交（#189）** のすべてに効く。

---

## 1. 役に立つ視点（要約）

『大転換』の世界観を、**本システムに効く形**で1行ずつ：

1. **市場は社会に埋め込まれている** ── 自由市場は自然発生せず、政治的に設計される人工物。完全自由化は効率を上げるが社会を破壊し、必ず防衛反応（保護）を呼ぶ。→ 自由化レバーに**リスクとリターン**の両面を与える。
2. **二重運動 ── 市場化と保護化は同時に進む** ── ランカシャーの綿工場が進むほど同時に工場法・救貧法が生まれた。→ 市場拡大が**政治的保護コストを内生化する**エンジン。
3. **擬制商品 ── 労働/土地/貨幣は本来商品ではない** ── これらを純粋商品として扱う（解雇自由・土地投機・信用創造）ほど社会は壊れやすくなる。→ 既存の `Population`/`Province`/`FiscalRules` に**新しい不安定軸**を足す。
4. **四本柱の均衡と連鎖崩壊** ── 金本位制・勢力均衡・自由貿易・自由主義国家は相互に支え合う。1本の崩落が残りを引き寄せる。→ 多極戦略における**連鎖リスクの表現**。
5. **保護制度は後退しない** ── 一度建設された社会保護（労働法・社会保険・中央銀行）は制度的惰性で残る。→ 「構造的慣性」の純ロジック化。
6. **市場統合と地政学は不分離** ── 19世紀の自由貿易圏は帝国主義的拡張を前提とした。閉じた経済は戦争リスクを下げるが効率も下がる。→ `LogisticsRules`（版図一体化）に経済的意味を与える。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`MarketRules`/`FiscalRules`/`ConsentRules`/`LogisticsRules` を作り直さない**。POLA はそれらに**欠落軸を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・『大転換』の signature）

#### POLA 市場の埋め込み度指標（`EmbeddednessRules`/`MarketEmbeddedness`）
- `embeddedness`（0..1、1＝完全埋め込み＝伝統的/計画経済、0＝完全脱埋め込み＝純粋自由市場）。
- `ProductivityFactor(e)`：脱埋め込みほど高産出（`GovernanceRules.OutputFactor` に乗算）。
- `InstabilityRisk(e)`：脱埋め込みほど高不安定（擬制商品ストレス・二重運動強度に係数）。
- 政策レバー（「規制緩和/強化」）で `embeddedness` を時間変化させる。
- 接続：`MarketRules` × `GovernanceRules.OutputFactor` × `FactionStateRules`。**純ロジック新設・test-first**。

#### POLA 二重運動（`DoubleMovementRules`）— 市場圧力→保護需要→制度建設の自動回路
- `MarketPressure(embeddedness, population, province)` ── 脱埋め込み度×人口×安定低下で市場圧力。
- `ProtectionDemand(pressure)` ── 閾値超過で保護的政治運動の需要が発生。
- `InstitutionBuildingTick(demand, existing, dt)` ── 需要から保護制度（`SocialProtection`）が内生的に成長（ここが真の欠落）。
- 接続：`MarketEmbeddedness`（POLA-1）×`PartyRules`（政治運動・#159）×`HopeRules`（希望→末人#852）×`ConsentRules`（#836）。**純ロジック新設・test-first**。

#### POLA 擬制商品ストレス（`FictitiousCommodityRules`）— 労働/土地を完全商品化する制度リスク
- **労働商品化ストレス** `LaborCommodificationStress(embeddedness, population)`：人口に市場原理を強制するほど `HopeRules.Community.hope` を侵食（希望の逓減 → 末人化）。
- **土地商品化ストレス** `LandCommodificationStress(embeddedness, province)`：星系を純粋市場資産として扱うほど `GovernanceRules.stability` 外側に「収用不満」が発生。
- 接続：`DemographicsRules`/`Population`（LIFE-3）×`GovernanceRules`/`Province`（#109）×`EmbeddednessRules`（POLA-1）。**純ロジック新設・test-first**。

### ★★ 高（多極秩序・制度的慣性）

#### POLA 多極経済秩序の相互支持と連鎖崩壊（`InternationalOrderRules`）
- 四本柱（勢力均衡・通貨安定・自由貿易・自由主義国家）を `OrderPillar` として数値化。
- `OrderStability(campaign)` ── 四本柱の加重平均。
- `CascadeRisk(pillar, stability)` ── 1本が閾値を割ると他の柱の安定にペナルティを伝播（正のフィードバックループ）。
- 接続：`DiplomacyRules.IsHostile`（勢力均衡）×`LogisticsRules.CohesionFactor`（自由貿易）×`FiscalRules.ExchangeRateFactor`（通貨）×`FactionState.IsCollapsing`（自由主義国家）×`CampaignRules`。**純ロジック新設・test-first**。

#### POLA 社会保護制度の内生的成長と制度的慣性（`SocialProtectionRules`）
- `SocialProtection`（保護水準 0..1）：二重運動（POLA-2）の `InstitutionBuildingTick` で蓄積。
- `WelfareLift(protection)` ── 保護水準→希望ボーナス（`HopeRules`）・消費水準向上。
- `ProtectionInertia` ── 一度建設された保護は政治的コストなしに下げられない（ラチェット効果）。
- `EfficiencyDrag(protection)` ── 高保護は `ProductivityFactor` を微減させる（自由化との緊張を維持）。
- 接続：`DoubleMovementRules`（POLA-2）×`HopeRules`/`Community`（#852）×`FiscalRules.WelfareCost`（#162）。**純ロジック新設・test-first**。

### ★ 中（世界観lore）

#### POLA（lore）市場社会の転換と社会の自己防衛に関する世界観開示データ
- 「経済が社会を支配する時、社会は必ず取り返そうとする」「自由市場は自然物ではなく政治的構築物」「擬制商品の完全市場化は内部崩壊を招く」。
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への **lore データ入力**。世界観EPIC（啓蒙/#836/#852/ニーチェ末人）との連鎖。

### ❌ 不採用（重複・既存で十分・タイクン化回避）

| 不採用 | 理由 |
|---|---|
| 金本位制の詳細実装 | `FiscalRules.ExchangeRateFactor`（#161）＋ `CoinageRules`（SAW-1）で十分カバー。POLA は係数を提供するだけ |
| 「自由主義経済」の再現マクロ（貿易量計算/貿易収支）| `LogisticsRules`/`MarketRules` が既にカバー。タイクン化回避 |
| 労働組合・ストライキのマイクロ実装 | `ConsentRules.Withdraw`（#836）が同構造をカバー。別クラス不要 |
| ファシズム/共産主義イデオロギーエンジン新設 | `FactionData.ideology`/`DynastyRules`/`Regime`（#867）で十分。追加新設しない |
| 歴史的具体（コーン法廃止・プルシア農業改革…） | ゲームの舞台は宇宙。歴史固有事象のコード化は不要 |
| 階級闘争の詳細シミュ | `RedistributionRules`/`TaxStructure`（#163）が`ClassTension`をカバー済み |

---

## 3. EPIC #POLA の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。既存ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**政治経済メカニクス/社会構造パターンのみ**参考。

> **EPIC = #1585**。GitHub issue 起票済み（#1588〜#1604）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **POLA-1** | #1588 | 市場の埋め込み度指標（`EmbeddednessRules`/`MarketEmbeddedness`）— 自由化=効率↑不安定↑のトレードオフ | 新 `EmbeddednessRules`。`GovernanceRules.OutputFactor`×`MarketRules`×`FactionStateRules` に係数提供 |
| **POLA-2** | #1592 | 二重運動（`DoubleMovementRules`）— 市場圧力→保護需要→制度建設の自動回路 | POLA-1×`PartyRules`（#159）×`HopeRules`（#852）×`ConsentRules`（#836） |
| **POLA-3** | #1596 | 擬制商品ストレス（`FictitiousCommodityRules`）— 労働/土地の完全商品化が生む固有の制度リスク | `DemographicsRules`/`Population`×`GovernanceRules`/`Province`×POLA-1 |
| **POLA-4** | #1599 | 多極経済秩序の相互支持と連鎖崩壊（`InternationalOrderRules`）— 四本柱の崩壊カスケード | `DiplomacyRules`（#189）×`LogisticsRules`（#844）×`FiscalRules`（#161）×`CampaignRules` |
| **POLA-5** | #1602 | 社会保護制度の内生的成長と制度的慣性（`SocialProtectionRules`）— 二重運動の産物・ラチェット効果 | `DoubleMovementRules`（POLA-2）×`HopeRules`（#852）×`FiscalRules.WelfareCost`（#162） |
| **POLA-6** | #1604 | （lore）市場社会の転換と社会の自己防衛に関する世界観開示データ | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`POLA-1`（埋め込み度指標を基盤に確立）→ `POLA-2`（二重運動＝最も固有のエンジン）→ `POLA-3`（擬制商品ストレス＝POLA-1/2に乗る）→ `POLA-4`（多極秩序＝外交/財政に横断）→ `POLA-5`（保護制度成長＝POLA-2に乗る）→ `POLA-6`（lore）。

> いずれも既存社会・経済ロジックを**後退させず接続**する additive 設計。
> フェザーン#160（商社国家）・財政#161-163・統治#109・外交#189 のすべてに効く。
