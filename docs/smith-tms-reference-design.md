# アダム・スミス『道徳感情論』参考設計（EPIC #TMS）

> 参照元：アダム・スミス『道徳感情論』（The Theory of Moral Sentiments, 1759）。
> 経済学の父スミスが『国富論』（1776）より先に著した道徳哲学の書。
> 「共感（sympathy）」を道徳の根拠とし、「公平な観察者（impartial spectator）」という内なる裁き手によって人間行動が形成されると論じる。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略＋既に巨大な社会シミュ層）にとって
> **役に立つ視点**だけを抽出し、EPIC `#TMS` として issue 化する提案。
> ★著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**道徳哲学のメカニクス構造のみ**を参考にする。

---

## 0. なぜ「道徳感情論」が本システムに役立つか

当プロジェクトは社会・政治シミュの**純ロジック層を大量に保有**している（[CLAUDE.md] 参照）：

| 既存（評判・忠誠・同意） | カバー範囲 |
|---|---|
| `DiplomacyRules.opinion`（外交）| 勢力ペアの opinion 修正子・状態遷移 |
| `LoyaltyRules`（忠誠）| 忠誠/調略値→旗幟・寝返りカスケード |
| `ConsentRules`/`Polity`（合意）| 抑圧×希望→協力・統治不能閾値 |
| `MeritRankRules`（軍功授爵）| 戦功→爵位→インセンティブ士気 |
| `SecurityRules`（秘密警察）| 監視・粛清→支持低下ペナルティ |
| `PersonRules.Effectiveness`（適材適所）| 役職適性×役割一致→能力発揮率 |
| `FactionState.inclusiveness`（統治スタイル）| 収奪0↔包摂1 の1軸 |
| ALM-系（評判の勢力レベル）| 王道/覇道の評判軸・エンディング分岐 |

**しかし、これらは「数値・制度・外部強制」から動く**のであり、スミスが固有に描く以下が**欠けている**：

| 道徳感情論が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **共感（sympathy）＝他者の行動を見て感情が動く**という評判生成の機構 | `opinion`は宣言的な外交状態。**行動を目撃した者が道徳的に評価する**回路がない |
| **公平な観察者（impartial spectator）**＝自己行動を第三者視点で内省する精度 | JGS-3「慢心加速」は成功→腐敗の加速のみ。**内省によって自己欺瞞が抑制される**逆回路がない |
| **慎慮・仁愛・正義の3徳**という統治スタイルの三軸 | `FactionState.inclusiveness`は収奪/包摂の1軸。**賢明な利己心(慎慮)・積極的配慮(仁愛)・規範強制(正義)** の三角形がない |
| **商業的誠実さ→長期信頼蓄積**という市場の道徳基盤 | `MarketRules`は均衡計算。**繰り返し取引での誠実さが信頼を積む**経路がない（SAW-6のネームド破産とは別の信頼積み立ての軸） |

**結論**：道徳感情論は当プロジェクトの社会シミュに**「外部強制でなく内発的道徳」という補完軸**を与える。
具体的には①**共感評判**（行動を見た者が評価する回路）②**公平な観察者**（自己欺瞞の逆回路）③**3徳統治スタイル**（1軸→3軸への拡張）という3つの欠落軸が核心。
なお、国富論（#1263）はすでに分業・市場・貿易を扱っており、道徳感情論はそれと**直交した道徳的側面**（市場を可能にする信頼・承認・内省）を足す。

---

## 1. 役に立つ視点（要約）

道徳感情論の構造を、**本システムに効く形**で1行ずつ：

1. **「共感は社会の接着剤」**：人は他者の行動を想像して感情が動き、承認/非承認を返す。→ **行動→評判生成の内発的回路**（`EmpathyRules`）が `LoyaltyRules`×`ConsentRules` に欠けている回路を補完。
2. **公平な観察者＝内なる第三者の裁き手**：自己行動を「見知らぬ公平な者」の目で評価する精度が高いほど自己欺瞞が少ない。→ JGS-3「慢心加速」（`DynastyRules`拡張）に**抑制の逆回路**（`ImpartialObserverRules`）を足す。
3. **3徳（慎慮・仁愛・正義）は統治の三軸**：慎慮＝利益を賢明に計算する/仁愛＝他者への積極的配慮/正義＝規範を強制する。 → `FactionState.inclusiveness`（1軸）を3軸に拡張し `GovernanceRules`/`DynastyRules` に修正子を供給。
4. **市場は共感に支えられた取引**：誠実な商人は繰り返し取引で信頼を積み、長期的に有利になる。→ `MarketRules`×`DiplomacyRules.opinion` に**交易誠実さ→信頼係数**を足す（国富論#1263・SAW-6と三層）。
5. **自己欺瞞のメカニズム**：公平な観察者が「身内フィルター」に汚染されると自己正当化が暴走する＝スミスの「腐敗」。→ `SecurityRules`（外部監視）の**対になる内部監視**、`DynastyRules.腐敗`の道徳的説明。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`LoyaltyRules`/`ConsentRules`/`DiplomacyRules`/`PersonRules`/`FactionState` を作り直さない**。
> TMS は**欠落回路を追加して接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・道徳感情論の signature）

#### TMS 共感評判エンジン（`EmpathyRules`）
- **行動→道徳評価→評判修正子**：軍事/政治行動（民への恩恵・残虐命令・誠実な交渉・欺瞞）を「目撃した勢力/人物」が共感度スコアとして評価 → 支持・忠誠・外交 opinion の修正子に変換。
- 評価は**行為者の意図×結果×目撃者の立場**で計算（同じ行動でも立場で共感度が変わる）。
- 接続：`PersonRules`（行為者）×`ConsentRules.協力`×`LoyaltyRules.忠誠`×`DiplomacyRules.opinion`。
  **「数値ではなく行動が評判を生む」**回路の起点。

#### TMS 公平な観察者フィルター（`ImpartialObserverRules`）
- **自己評価精度**：元首/提督の「公平な観察者精度（impartialScore）」が高いほど自己欺瞞バイアスが小さく政策判断が精度を増す。低いほど自己正当化が暴走し `DynastyRules.腐敗` を加速（JGS-3 `SelfRestraintBrake` の道徳版・逆回路）。
- `impartialScore` は `PersonRules`（知恵/正義徳スコア）・諫言受容（JGS-1 `RemonstranceRules.受容性`）から導出 → 基準値非破壊の**実効値パターン**。
- 接続：`DynastyRules.Tick`（腐敗増速ブレーキ）×`JGS-3`（慢心加速の逆）×`FounderTransitionRules`（守成期の自己正当化）。

### ★★ 高（既存単軸を三軸へ拡張）

#### TMS 3徳統治スタイル軸（新 `MoralStyleRules`）
- 現在の `FactionState.inclusiveness`（収奪0↔包摂1）に、慎慮（`prudence`・0..1）・仁愛（`benevolence`・0..1）・正義（`justice`・0..1）の**3徳スコア**を追加（既存1軸は後方互換で保持）。
- 純関数：`MoralStyleFactor(prudence, benevolence, justice)` → `GovernanceRules.EquilibriumStability` の修正子。慎慮高＝効率的統治・仁愛高＝民心安定・正義高＝腐敗抑制・3つ揃うと `PublicTrust` 最大化。
- 接続：`GovernanceRules`（安定度）×`DynastyRules.徳`×`FactionStateRules.Stability`×`LogisticsRules.CohesionFactor`。

### ★ 中（市場誠実性・lore）

#### TMS 商業誠実性の信頼基盤（新 `CommercialIntegrityRules`）
- 繰り返し交易での**誠実さ（defection/cooperation 累積）**→ 貿易パートナーへの opinion 修正子。
- 純関数：`TrustAccumulation(交易回数, 誠実率)` → `DiplomacyRules.TargetOpinion` への修正子。国富論#1263（分業・自由貿易）と SAW-6（ネームド破産）の**道徳的基盤**として両者を接続。
- 接続：`MarketRules`×`DiplomacyRules.opinion`×`GameTheoryRules`（繰り返しTitForTat#388）。

#### TMS（lore）世界観の開示データ
- 「共感が市場を可能にする」「公平な観察者＝文明の良心」「3徳の欠如が帝国を腐らせる」。
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への**lore データ入力**。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 見えざる手（市場の自律均衡） | **国富論 #1263 がカバー**。道徳感情論は市場の道徳的基盤のみ足す |
| 市場均衡・取引価格 | **`MarketRules`/`StockMarketRules` がカバー** |
| 外交 opinion の状態遷移 | **`DiplomacyRules` がカバー**。TMS は修正子を供給するだけ |
| 軍功・功績報酬 | **`MeritRankRules`（QIN-2/3）がカバー** |
| 監視・秘密警察 | **`SecurityRules`#166 がカバー**（TMS は内発的道徳=対極） |
| 忠誠の数値計算 | **`LoyaltyRules` がカバー**。TMS は共感という新インプットを足すだけ |
| 外交条約・戦争目標 | **DIP-2/3 がカバー** |
| 政党・選挙 | **GOV-6/7 がカバー** |
| 封建的義務・領主への忠誠 | **`FeudalRules`#168 がカバー** |

---

## 3. EPIC #TMS の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。
> 既存ロジックは**接続のみ・重複新設しない**。
> ★著作権注意：固有名・文章・キャラは不使用、**メカニクス/道徳哲学構造のみ**参考。

> **EPIC = #1576**。GitHub issue 起票済み（#1578〜#1594）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **TMS-1** | #1578 | 共感評判エンジン（`EmpathyRules`・行動→道徳評価→支持/忠誠/opinion修正子） | 新 `EmpathyRules`。`PersonRules`×`ConsentRules`×`LoyaltyRules`×`DiplomacyRules.opinion` |
| **TMS-2** | #1582 | 公平な観察者フィルター（`ImpartialObserverRules`・自己欺瞞バイアス→腐敗加速ブレーキ） | JGS-3 `SelfRestraintBrake` の道徳版。`DynastyRules.Tick`×`RemonstranceRules` 接続 |
| **TMS-3** | #1586 | 3徳統治スタイル軸（`MoralStyleRules`・慎慮/仁愛/正義→安定度修正子） | `FactionState.inclusiveness`1軸を3徳スコアで拡張。`GovernanceRules`×`DynastyRules.徳` |
| **TMS-4** | #1590 | 商業誠実性の信頼基盤（`CommercialIntegrityRules`・繰り返し交易→信頼蓄積→opinion修正） | `MarketRules`×`DiplomacyRules.TargetOpinion`×`GameTheoryRules`#388 |
| **TMS-5** | #1594 | （lore）世界観の開示データ（共感が市場を可能にする/公平な観察者=文明の良心/3徳の欠如） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`TMS-1`（共感評判＝最も固有で欠落の大きい signature） → `TMS-2`（公平な観察者＝JGS-3 の逆回路として今すぐ接続可） → `TMS-3`（3徳統治スタイル＝`FactionState` 拡張） → `TMS-4`（市場信頼基盤＝`MarketRules`×国富論#1263×SAW-6 の三層完成） → `TMS-5`（lore・いつでも可）

> いずれも既存ロジックを**後退させず接続**する additive 設計。`DynastyRules`/`GovernanceRules`/`LoyaltyRules` を作り直さない。
