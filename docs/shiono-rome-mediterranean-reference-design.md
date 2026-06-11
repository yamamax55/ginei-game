# 塩野七生『ローマ亡き後の地中海世界』参考設計（EPIC #SHIO）

> 参照元：塩野七生『ローマ亡き後の地中海世界』（2008-2009年）。西ローマ帝国崩壊後、覇権の空白に生じた地中海の無秩序——サラセン海賊、十字軍、ヴェネツィア型海上共和国、オスマン帝国のコルサール、騎士団の要塞島——を描く歴史叙事。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略＋既に大量の社会・政治・軍事・経済純ロジック層）にとって**役に立つ視点だけを抽出し**、EPIC `#SHIO` として issue 化する提案。
> **著作権注意：固有名・文章・キャラクター・固有設定は流用せず、海賊経済／覇権空白ダイナミクス／通商支配の構造パターンのみを参考にする。**

---

## 0. なぜ「ローマ亡き後の地中海世界」が本システムに役立つか

当プロジェクトは通商破壊・海上支配に関する基盤ロジックを保有している：

| 既存（カバー範囲） | 何をカバーするか |
|---|---|
| `CommerceRaidingRules`（L-3 #95） | 補給線の切断：一方勢力が他方勢力の輸送を迎撃する **勢力間行為** |
| `CaptivityRules`（#154） | 捕虜の生死・解放・処断・登用（`CaptiveStatus`/`CaptureChance`/`Release`/`Execute`/`Recruit`） |
| `SupplyRules`（L-2 #94） | 補給源→前線への到達（補給路切断＝敵ZOC blocked） |
| `DiplomacyRules`/`DiplomacyState`（#189） | 外交状態：平時/同盟/不可侵/属国/交戦・条約署名・戦争宣言 |
| `LogisticsRules.CohesionFactor`（#844） | 所有星系の連結一体化度（分断すると国力低下） |
| `GalaxyMap`/`Corridor` | 銀河グラフ：星系（ノード）と回廊（エッジ） |
| `ResourceProductionRules`/`FiscalRules` | 星系の産出・財政・歳入 |
| `EspionageRules` | 諜報・情報収集 |
| `WarGoalRules`/`WarWeariness` | 厭戦・戦争目標正統性・講和 |

**しかし、これらは「正規勢力間の戦争・通商・外交」**を扱うものであり、本書が固有に描く以下が**欠けている**：

| 本書が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **非国家海上略奪者（コルサール）** | `CommerceRaidingRules` は勢力（Faction）単位の戦争行為。**正規軍でなく私的武装団が通商路を常時脅かす**構造が無い。「海賊」は戦争以外の平時にも存在する |
| **身代金経済（捕虜の経済的価値）** | `CaptivityRules` は生死・解放・処断・登用を扱うが、**捕虜の階級に応じた身代金価値・身代金交渉・仲介業者**という経済サブシステムが無い |
| **海域支配権と通行税** | `SupplyRules` は補給路の「つながり」を二値で扱う。**回廊を実効支配する小勢力が通行する艦隊から税を取る**「通行料モデル」が無い |
| **有事連合（脅威閾値→臨時同盟→脅威消失→解散）** | `DiplomacyRules` は意図的な条約型同盟。**通常は敵対する勢力が共通の実存的脅威に対し一時的に連合し、脅威消滅後に解散する**自動トリガー型連合が無い |
| **無秩序海域ダイナミクス（覇権空白→海賊増殖→通商衰退→秩序回復）** | GIB（衰退ルール）の「崩壊後の世界」を受け、**空白域で海賊経済が自律的に増殖し通商を圧迫し、新たな秩序主体が出現するまで回廊が荒廃する**長期サイクルが無い |

**結論**：本書は当プロジェクトに**「正規勢力間の枠の外にある経済アクター」という視点**を与える。コルサールは戦争でも平和でもなく「恒常的な略奪商売」として機能し、①コルサール経済 ②身代金算定 ③通行税 ④有事連合という4つの欠落軸を埋める。**通商破壊#95の非国家版**を補完し、**フェザーン型通商国家（#160）に最も強いテクスチャ**を与える。GIB（衰退後）の「その後」として接続。

---

## 1. 役に立つ視点（要約）

本書の世界観を、**本システムに効く形**で1行ずつ：

1. **覇権空白は海賊の黄金時代**——帝国が海を管理しなくなった瞬間、私的武装団が恒常化する。→ GIB `DeclineRules` の崩壊後アウトカムとして**コルサール繁殖**が自動発生するサイクル。
2. **コルサールは戦士でなく商売人**——略奪の目的は殺傷でなく捕虜の身代金換金。「捕らえて売る」が収益モデルで、軍事合理性より経済合理性で動く。→ `CaptivityRules`（捕虜）に**経済価値**を追加する。
3. **小さな要塞島が海域を支配する**——騎士団の島・ヴェネツィアの港湾拠点は、艦隊よりも**位置**で海を押さえた。制海権は艦隊決戦だけでなく回廊上の拠点支配から生まれる。→ 回廊を「支配」する概念と通行税を導入。
4. **敵同士が連合するのは脅威が「全員の問題」になった時だけ**——カトリック対プロテスタントも共通の敵がいる間だけ手を結ぶ。→ `DiplomacyRules` に**脅威駆動型一時連合**のトリガーを足す。
5. **通商と略奪は同じ回廊を共有する**——コルサールの巣は貿易港の近くにある。回廊の繁栄は略奪の魅力を高め、逆もまた然り。→ `CommerceRaidingRules`×`FiscalRules`（回廊収益）×コルサールが有機的に連動。
6. **秩序の回復は覇権ではなくネットワークで起きる**——ヴェネツィアは海軍力より港湾ネットワークで地中海を管理した。→ 多極型海域支配の世界観 lore として `DisclosureLedger` に接続。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`CommerceRaidingRules`/`CaptivityRules`/`DiplomacyRules`/`SupplyRules`/`LogisticsRules` を作り直さない**。SHIO はそれらに**欠落軸を足し、接続する**だけ（additive）。タイクン化回避＝マイクロ操作を増やさず、エンジン駆動で創発。

### ★★★ 最優先（真の欠落・本書の signature）

#### SHIO コルサール経済基盤（非国家海上略奪アクター）

`CorsairBand`（非MonoBehaviour純データ）：
- `id`/`factionId`（後援勢力・null＝完全独立）/`homeSystemId`（根拠地）/`strength`（船団規模）/`ransomGold`（累積身代金収益）/`isActive`

`CorsairRules`（static・純ロジック）：
- `ThreatLevel(band, corridor)` ——根拠地から半径内の回廊に対し `strength` 比例の略奪脅威度（0..1）を返す
- `Raid(band, corridor, supplyScale, roll)` ——確率的に補給を削減し捕虜を取得（`CaptivityRules` のキャプチャを呼ぶ）。`CommerceRaidingRules.ConvoyDestroyed` の非国家版
- `SponsorBonus(factionId)` ——後援勢力の财政から手数料を受け取る（私掠状=letter of marque）。後援の無い独立コルサールは財政連動なし
- `GrowthFactor(band, map, hostilePresence)` ——隣接する敵対正規勢力が少ない/HostilePair が無い回廊では `strength` が毎ターン微増（覇権空白で繁栄）

接続：`CommerceRaidingRules`（ベースの略奪判定）×`CaptivityRules`（捕虜供給源）×`FiscalRules`（後援勢力への収益）×GIB `DeclineRules`（空白発生トリガー）

#### SHIO 身代金算定ロジック

`RansomRules`（static・純ロジック）：
- `RansomValue(person, factionState)` ——`Person.rankTier`/`admiralData.leadership` × 捕縛された勢力の財政健全度（`FiscalHealthFactor`）で身代金額（gold単位）を算出。上位者ほど高額・財政が苦しい勢力は払えない
- `NegotiatedRansom(asking, payerFiscal, roll)` ——交渉確率：asking 額 ÷ 支払能力（`FiscalState`）が高いほど交渉失敗（処断/抑留に移行）
- `BrokerCommission(amount, brokerFaction)` ——中立商人勢力（フェザーン型）が仲介料を抜く。身代金フローが中立通商国の財政に流れ込む
- `ReleaseOnRansom(prisoner, goldPaid)` ——`CaptivityRules.Release` の身代金版（解放+財政トランスファー）

接続：`CaptivityRules`（捕虜ソース）×`FiscalRules`（支払能力・収益計上）×`CorsairRules`（身代金がコルサール繁殖資金に）

### ★★ 高（通商支配と有事連合）

#### SHIO 海域支配権と通行税（`SeaLaneTollRules`）

`SeaLaneTollRules`（static・純ロジック）：
- `ControllerOf(corridor, fleets)` ——回廊両端の星系を所有 or 旗艦が常駐する勢力を「支配者」として返す（支配者不在はnull＝無主地）
- `TollRevenue(corridor, tradeVolume, tollRate)` ——支配者が課せる通行税収益：`tradeVolume`（通過補給量 `SupplyRules`）× `tollRate`（0..0.3・支配強度比例）→ `FiscalState` 歳入へ
- `ThreatToToll(corsairBand, corridor)` ——コルサールの脅威度が高い回廊では `tradeVolume` が減少（略奪→通商収縮）
- `EstablishControl(faction, corridorId, fleets)` ——回廊への恒常的な艦隊駐留で支配を宣言（`StrategyRules` 的な占拠と並立）

接続：`Corridor`/`GalaxyMap`（回廊）×`SupplyRules`（通過補給量）×`FiscalRules`（歳入）×`CorsairRules`（通行税減収トリガー）×`LogisticsRules`（通商ネットワーク一体化）

#### SHIO 有事連合（脅威閾値→臨時同盟→脅威消失→解散）

`ThreatCoalitionRules`（static・純ロジック）：
- `SharedThreat(factionA, factionB, map, reg)` ——同一の共通敵対勢力に対し `factionA`・`factionB` が共に `FactionRelations.IsHostile` ＝ true なら共通脅威スコア（0..1）を返す
- `CoalitionThreshold` ——共通脅威スコアが `CoalitionThreshold`（既定0.6）を超えると連合提案が生成
- `FormTemporaryCoalition(factionA, factionB, target)` ——`DiplomacyState` の外に「臨時連合」を記録（通常の同盟ではない。対象勢力限定・自動期限付き）
- `ShouldDissolve(coalition, map, reg)` ——共通敵が `HasHostilePair` から消えた（退却・壊滅・講和）なら解散トリガー。解散後は元の外交状態に戻る（自動的に平時 or 旧来の敵対に復帰）
- `CoalitionBonus(coalition)` ——連合中は双方の `strength` を合算して共通敵に対して計算（`StrategyRules.ResolveEncounters` への係数）

接続：`DiplomacyRules`/`DiplomacyState`（外交状態の上位レイヤー）×`StrategyRules`（戦力合算）×`WarGoalRules`（共通戦争目標）×`FactionRelations.IsHostile`

### ★ 中（世界観サイクル・lore）

#### SHIO 無秩序海域ダイナミクス（覇権空白サイクル）

純ロジック追加として `CorsairRules.GrowthFactor` と `SeaLaneTollRules.ThreatToToll` を接続すれば、
- 「GIB の衰退崩壊 → 回廊が無主地化 → コルサール繁殖（`GrowthFactor` ↑）→ 通行税収入消滅（`ThreatToToll` ↑）→ 通商衰退 → 新たな支配者が回廊を占拠 → 通行税回復」
というサイクルが**エンジン駆動で自然に発生**する。コード新規は不要（SHIO-1〜4 の組み合わせで創発）。

#### SHIO（lore）世界観の開示データ

- 「帝国が消えると海は無法地帯になる（覇権空白の法則）」
- 「略奪者も市場原理で動く（コルサールの合理性）」
- 「小さな要塞島が銀河の咽喉を押さえる（位置の政治）」
- 「敵同士の連合は共通の敵が生きている間だけ」

接続：**コード新設せず** `DisclosureLedger`（FND-4）への**lore データ入力**のみ。

### ❌ 不採用（重複・既存で十分・タイクン化リスク）

| 不採用項目 | 理由 |
|---|---|
| 通商破壊の基本ロジック | **`CommerceRaidingRules`（#95）が既にカバー**。SHIO は非国家アクター拡張のみ追加 |
| 捕虜の基本ハンドリング（生死・解放・処断・登用） | **`CaptivityRules`（#154）が既にカバー**。SHIO は経済価値軸のみ追加 |
| 要塞/惑星攻城の戦術ロジック | **`PlanetSiegeRules`（#131）が既にカバー**。騎士団の砦も同ルールで扱える |
| 宗教的熱狂による動員（聖戦） | **`ReligionRules.HolyWarPressure`（#172-175）が既にカバー**。十字軍動員は接続のみ |
| ヴェネツィア/ジェノヴァの個別都市国家AI | タイクン化回避＝個別勢力の詳細な商業マイクロは不要。`SeaLaneTollRules` で経済モデルとして抽象化する |
| 海図・航路の物理シミュレーション | スコープ外。`GalaxyMap` の回廊グラフで十分に抽象化できる |
| オスマン帝国の固有実装 | 多勢力システム（`FactionData`）で汎用的に扱える。固有コード不要 |
| ローマの衰退そのもの | **GIB `DeclineRules`（`#GIB`）がカバー**。SHIO は「衰退後の世界」にフォーカス |

---

## 3. EPIC #SHIO の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。既存ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #1880**。GitHub issue 起票済み（#1885〜#1896）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **SHIO-1** | #1885 | コルサール経済基盤（`CorsairBand`/`CorsairRules`・非国家海上略奪アクター・後援/私掠状/覇権空白で増殖） | 新 `CorsairRules`。`CommerceRaidingRules`（略奪判定）×`CaptivityRules`（捕虜供給）×GIB `DeclineRules`（空白トリガー） |
| **SHIO-2** | #1888 | 身代金算定ロジック（`RansomRules`・捕虜の経済的価値・階級×財政健全度・仲介商人手数料） | `CaptivityRules`（捕虜ソース）×`FiscalRules`（収益/支払能力）×SHIO-1（コルサール資金） |
| **SHIO-3** | #1891 | 海域支配権と通行税（`SeaLaneTollRules`・回廊支配者→通行税→FiscalState歳入・コルサール脅威で減収） | `Corridor`/`GalaxyMap`×`SupplyRules`×`FiscalRules`×SHIO-1（脅威→通商縮小） |
| **SHIO-4** | #1894 | 有事連合（`ThreatCoalitionRules`・共通脅威閾値→臨時連合→脅威消失→自動解散） | `DiplomacyRules`/`DiplomacyState`（外交状態上位）×`StrategyRules`（戦力合算）×`WarGoalRules` |
| **SHIO-5** | #1896 | （lore）世界観の開示データ（覇権空白の法則・コルサールの合理性・位置の政治・有事連合の儚さ） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順

`SHIO-1`（コルサール経済基盤＝最も固有で欠落の大きい signature）→ `SHIO-2`（身代金＝SHIO-1 の収益モデル）→ `SHIO-3`（通行税＝コルサール×通商の連動）→ `SHIO-4`（有事連合＝連合外交の新軸）→ `SHIO-5`（lore 入力）。

> いずれも既存ロジックを**後退させず接続**する additive 設計。**通商破壊#95・通商国家フェザーン#160・GIB `DeclineRules`** に最も効く。
