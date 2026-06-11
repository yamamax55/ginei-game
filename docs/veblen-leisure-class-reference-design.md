# ヴェブレン『有閑階級の理論』参考設計（EPIC #VEBL）

> 参照元：ソースタイン・ヴェブレン著（1899年）。産業社会における**有閑階級（武士/貴族的支配階級）の経済的行動**を分析した制度経済学の古典。  
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略＋既に巨大な経済/政体純ロジック層）にとって**役に立つ視点**だけを抽出し、EPIC `#VEBL` として issue 化する提案。  
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**経済メカニクス／世界観の構造パターンのみ**を参考にする。

---

## 0. なぜ「有閑階級の理論」が本システムに役立つか

当プロジェクトは階級・経済・文化の**マクロ純ロジックを大量に保有**している：

| 既存（マクロ・抽象） | カバー範囲 |
|---|---|
| `FiscalClass{富裕層,中間層,貧困層}` + `TaxStructure`/`RedistributionRules`（#163） | 階級別税負担・再分配・累進/逆進 |
| `MarketRules`/`Good`/`Market`（#179-182） | 需給均衡価格・生活水準→支持 |
| `FeudalRules`/`Fief`（#168・Wave2） | 徴募・反乱・門地開放 |
| `CultureRules`/`Culture`（#194） | 同化圧力・分離独立・ナショナリズム |
| `FactionState`/`FactionStateRules` | 正統性・安定度・腐敗・合意 |
| `CareerTrack`/`SeniorityRules`（LIFE-5/6） | 士官/官僚の出自・席次vs実力 |
| `Ministry`/`MinistryRules`（GOV-5） | 省庁ツリー・省益・縦割り摩擦 |

**しかし、これらは「国家・市場・政体」という抽象主体のマクロ均衡**であり、ヴェブレンが固有に分析する以下が**欠けている**：

| 有閑階級が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **誇示的消費（Veblen財）** | `MarketRules` は需要が価格に反比例する通常財のみ。**地位財（価格↑→需要↑）**という逆需要曲線が無い |
| **金銭的模倣カスケード** | `FiscalClass` は税負担の分析のみ。**下位階級が上位を模倣して消費規範が下方に波及**する動学が無い |
| **誇示的浪費と正統性** | `FactionState.legitimacy` は制度・徳・カリスマで上下するが、**資源の意図的浪費・見せびらかしによる権力誇示**が正統性に連結していない |
| **制度の儀礼性（ceremonialism）** | `Ministry`/`GovernmentRegistry`/`OfficeRules` は機能的役職を管理するが、**機能を失っても威信のため存続する儀礼的制度**という概念が無い |
| **捕食文化 vs 産業文化の軸** | `CultureRules` は同化/分離のみ。**武士的な捕食文化（名誉・略奪）と工人的な産業文化（実用・技術）の対軸**が無い |

**結論**：ヴェブレンは当プロジェクトの階級・経済・政体システムに**「消費規範と制度の慣性という人間行動の二重構造」**という5つの欠落軸を与える。特に**`FiscalClass`（#110 Pop階級）＋`FeudalRules`（#168 封建）＋`MarketRules`（#179 市場）**の三角地帯に接続するときに最も効く。

---

## 1. 役に立つ視点（要約）

ヴェブレンの世界観を、**本システムに効く形**で1行ずつ：

1. **地位財（Veblen財）＝価格が上がるほど需要が増える財**。奢侈品は実用でなく「高いことを見せる」ために買われる。→ `MarketRules` の均衡財と直交する**第二の需要曲線**。
2. **金銭的模倣の滴り落ち（trickle-down）**。中間層は上流を模倣し、貧困層は中間層を模倣する→消費規範の波及＝**市場需要の内生的変動**。社会全体が地位財に引きずられて浪費する構造。
3. **浪費こそが権力の証明**。有閑階級は働かず消費し浪費することで支配の正統性を示す。→ `FactionState.legitimacy` に「誇示的浪費」という補正軸。トレードオフ＝過剰浪費は財政圧迫→長期崩壊。
4. **制度は実用を超えて儀礼のために生き残る**。役職・儀式・礼服が「機能より威信のために」維持される＝制度の慣性。→ `MinistryRules.SectionalismFriction`（省益）の**理論的根拠**を補強。
5. **支配文化の軸＝捕食（名誉・戦争）vs 産業（実用・技術）**。勢力のイデオロギー軸に追加でき、外交・徴募・技術投資効率に影響。→ `FactionData.ideology` の細分化軸として。
6. **勤労本能 vs 捕食本能の葛藤**。ヴェブレンが見た人間の二重衝動は、英雄死後の組織存続（#812 カリスマの日常化）で「制度化しないと捕食文化に退行する」問いと重なる。→ `DisclosureLedger` のlore接続先。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`FiscalClass`/#168封建/#179市場 を作り直さない**。VEBL はそれらに**欠落軸を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・ヴェブレンの signature）

#### VEBL-1 Veblen財と誇示的消費（地位財・逆需要曲線）
- **地位財モデル**：`StatusGood`（純ロジック struct）＝ `isStatusGood` フラグ＋`visibilityFactor`（誇示度0..1）。通常財は `price↑→demand↓`；地位財は `price↑→demand↑`（逆需要）まで引き上がる（`StatusDemandFactor`）。
- **`VeblenGoodsRules`**（static・純ロジック・test-first）：`AdjustDemand(good, price, statusGoodsParams)` → 通常財との組み合わせで需要曲線を合成。`FiscalClass` の富裕層ほど地位財消費シェアが高い（`StatusConsumptionShare`）。上限は `GoodType` 別 `VeblenCap`（タイクン化回避＝係数で背景的に効かせる）。
- 接続：`MarketRules.ClearingPrice` の需要計算に `VeblenGoodsRules.AdjustDemand` を折り込み。`FiscalState` の富裕層支出構造に反映。EditMode テスト必須（逆需要の符号検証）。

#### VEBL-2 金銭的模倣カスケード（pecuniary emulation）
- **模倣ルール**：`EmulationRules`（static・純ロジック・test-first）＝`EmulationCascade(FiscalClass[], MarketRef)` が上位階級の消費規範を下位へ波及させる。模倣率 `emulationRate`（0..1）＝近い階級ほど高い（貧困層は中間層を真似るのに精一杯・富裕層を直接真似ることは稀）。
- 機能：地位財需要が中間層にも「波及」し、**需要底上げ→価格高騰→貧困層の生活水準圧迫→安定度低下**という連鎖を生む。タイクン回避＝`EmulationPressure`（0..1）係数を既存 `GovernanceRules` の `Tick` へ渡すだけ（独自ゲームループ無し）。
- 接続：`EmulationRules.EmulationCascade` → `MarketRules.Tick` 内の需要計算に乗算 ／ `GovernanceRules.Tick` の安定度係数への入力。`DemographicsRules`（#153）の人口ボーナス/オーナスと連動可。EditMode テスト必須（カスケードの単調性・境界条件）。

### ★★ 高（政体・制度への誇示効果）

#### VEBL-3 誇示的浪費と正統性（conspicuous waste → legitimacy）
- **浪費正統性モデル**：`OstentationRules`（static・純ロジック）＝`WasteSignal(expenditure, visibility, FiscalHealth)` → `legitimacyDelta`（正の補正）と `fiscalPenalty`（負の補正）の両方を返す。**見せびらかしの規模**（宮廷・軍事パレード・豪華外交）が正統性↑、**見苦しい倹約・失業**が正統性↓。**過剰浪費は `FiscalState` の赤字→長期安定崩壊**というトレードオフが核。
- 接続：`FactionState.Polity.legitimacy` の入力。`FiscalRules.Expenditure` に「誇示支出」`ostentationSpend` 項目。`EventEngine`（#116）の誇示的浪費/節約イベント素材。EditMode テスト（過剰浪費でペナルティが正統性ボーナスを上回る境界値）。

#### VEBL-4 制度の儀礼性（ceremonial vs. instrumental）
- **儀礼的慣性モデル**：`CeremonialismRules`（static・純ロジック）＝役職/省庁に `ceremonialWeight`（威信価値0..1）を付与し、機能効率 `functionalEfficiency` が低下しても `institutionalPrestige` が高ければ廃止抵抗 `AbolitionResistance` が生じる。**省益 `SectionalismFriction`（#158）の「なぜ無駄な省庁は消えないか」**の理論的根拠。
- 接続：`Office`/`Ministry` に `ceremonialWeight` フィールド追加。`MinistryRules.Dissolve` に `CeremonialismRules.AbolitionResistance` 係数を掛け込む。`DynastyRules`（#867）の制度疲労・腐敗との相乗（儀礼化した制度ほど腐敗しやすい）。EditMode テスト（抵抗係数の境界・機能0でも威信で存続するケース）。

### ★ 中（文化軸・世界観lore）

#### VEBL-5 捕食文化 vs 産業文化軸（predatory vs. industrial）
- **文化軸モデル**：`FactionData` に `culturalAxis`（`enum CulturalAxis{捕食的, 中間, 産業的}` ・既定=中間＝後方互換）を追加。係数：**捕食的**＝兵力効率/士気補正↑・産出/技術速度↓；**産業的**＝逆。
- 接続：`CultureRules.AssimilationPressure`（捕食文化同士は融合しやすい）・`PersonRules.Effectiveness`（捕食文化では軍人適性↑文民↓）・`GovernanceRules.OutputFactor`（産業文化では安定度→産出の係数↑）・`CombatModifiers`（捕食文化の士気ボーナス係数）。**後方互換**＝`culturalAxis` 未設定なら `中間` 扱い。

#### VEBL-6 （lore）世界観の開示データ
- 「有閑貴族の誇示＝制度の腐敗の起点」「産業化が有閑階級を解体する」「不労所得の正統性とその崩壊」「勤労本能vs捕食本能（#812 カリスマ日常化への伏線）」。
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への**lore データ入力**。CCX-6（世界観codex退避）方針に一貫。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 社会階級システムそのものの新設 | **#110 Pop階級が計画済み**。VEBL は消費行動の追加のみ（接続のみ） |
| 封建領主の徴税・反乱・門地 | **#168 `FeudalRules` がカバー**。VEBL は重複しない |
| 市場均衡の再設計 | **#179 `MarketRules` がカバー**。VEBL は地位財という補正を足すのみ |
| 所得不平等の一般指標（ジニ係数等） | **`RedistributionRules`/`FiscalClass` が累進/逆進でカバー** |
| 貴族の軍事特権の物理的実装 | **`CivilianControlRules`/`CareerTrack` 士官学校系統がカバー** |
| 株式バブルそのもの | **`StockMarketRules`#185 がカバー**。VEBL は商品(地位財)の投機のみ |

---

## 3. EPIC #VEBL の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。既存経済/政体ロジックは**接続のみ・重複新設しない**。  
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #1589**。GitHub issue 起票済み（#1593〜#1606）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **VEBL-1** | #1593 | Veblen財と誇示的消費（地位財 `StatusGood`・逆需要曲線 `VeblenGoodsRules`） | `MarketRules` の需要計算に status goods の逆需要。`FiscalClass` 富裕層消費 |
| **VEBL-2** | #1597 | 金銭的模倣カスケード（`EmulationRules`・消費規範の下方波及→需要底上げ→安定低下） | `MarketRules`×`DemographicsRules`×`GovernanceRules`。純ロジック test-first |
| **VEBL-3** | #1601 | 誇示的浪費と正統性（`OstentationRules`・浪費→正統性↑/過剰で財政圧迫→崩壊） | `FactionState.legitimacy`×`FiscalRules`。`EventEngine` の誇示イベント素材 |
| **VEBL-4** | #1603 | 制度の儀礼性（`CeremonialismRules`・機能↓でも威信で存続する役職/省庁） | `Ministry`/`OfficeRules` の廃止抵抗係数。`DynastyRules` 制度疲労と相乗 |
| **VEBL-5** | #1605 | 捕食文化 vs 産業文化軸（`FactionData.culturalAxis`・軍事効率↑↔産出効率↑の対軸） | `CultureRules`/`PersonRules`/`GovernanceRules`/`CombatModifiers` |
| **VEBL-6** | #1606 | （lore）世界観の開示データ（有閑貴族の誇示と腐敗・産業化と階級解体・勤労vs捕食本能） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順

`VEBL-1 → VEBL-2`（Veblen財と模倣カスケード＝最も固有で欠落の大きい signature＝市場動学の逆転）→ `VEBL-3`（浪費と正統性＝政体側への接続）→ `VEBL-4`（儀礼的制度慣性＝省庁/役職の文化面補強）→ `VEBL-5`（文化軸＝勢力レベルの色付け）→ `VEBL-6`（lore）。

> いずれも既存市場/財政/政体システムを**後退させず接続**する additive 設計。`FiscalClass`（#110 Pop階級）＋`FeudalRules`（#168 封建）＋`MarketRules`（#179 市場）の三角地帯に最も効く。
