# カミュ『ペスト』参考設計（EPIC #PEST）

> 参照元：アルベール・カミュ『ペスト』（La Peste）。北アフリカの港湾都市を突如封鎖する疫病と、
> その中で日常を守り続ける人々を描いた長編小説（1947年）。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略＋既に大規模な社会・政治ロジック層）に
> とって**役に立つ視点**だけを抽出し、EPIC `#PEST` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、
> **疫病伝播・封鎖統治・危機対応・連帯のメカニクス構造のみ**を参考にする。

---

## 0. なぜ「ペスト」が本システムに役立つか

当プロジェクトは社会・政治シミュ層を大量に保有している（[CLAUDE.md] 参照）：

| 既存（社会・政治） | カバー範囲 |
|---|---|
| `HopeRules`/`Community`（#852-856） | 希望↔末人・信仰ルート・秩序ルート |
| `ConsentRules`/`Polity`（#836-837） | 大衆の協力・非協力ボイコット |
| `GovernanceRules`/`Province`（#109） | 安定度・統合度・占領統治 |
| `EventEngine`（#116） | 条件発火→選択肢→効果のイベント基盤 |
| `SupplyRules`（#94） | 補給線遮断・補給切れ |
| `MarketRules`/`Market`（#179-182） | 需給均衡・生活水準→支持 |
| `StrategyRules.IsFtlBlocked`（C-2） | 敵対端点回廊の FTL 封鎖（軍事） |
| `DynastyRules.腐敗`（#867） | 制度疲労・天命喪失 |

**しかし、これらはすべて「政治・軍事・経済」の通常均衡**であり、ペストが固有に描く以下が**欠けている**：

| ペストが固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **疫病の拡散（グラフ上の感染伝播）** | SIR型の接触感染モデルが無い。星系間回廊を伝わる疫病の動態が未実装 |
| **非軍事的回廊封鎖（コルドン・サニテール）** | `StrategyRules.IsFtlBlocked` は軍事/外交。疫病隔離目的の封鎖（味方が任意に閉じる）が無い |
| **危機対応フェーズ弧（否認→承認→動員→疲弊→収束）** | `EventEngine` は単発発火。**構造化された危機フェーズの状態機械**（段階的認知と対応）が無い |
| **共有危機下の連帯逆説（危機ほど共同体が結束する）** | `HopeRules` は希望↔絶望の線形。**見えない共通の敵が生む団結**（"みんなで抵抗する"モメンタム）が無い |
| **封鎖経済下の闇市場（公式供給遮断→非公式流通路の創発）** | `MarketRules` は公式均衡のみ。封鎖→欠乏→闇市場の自生が無い |

**結論**：ペストは当プロジェクトの社会シミュに**「見えない敵との戦い」という第二の戦争軸**を与える。
軍事的勝利が無意味になる局面——感染症・宇宙的現象・封鎖都市——で、
**①疫病伝播 ②隔離封鎖 ③危機フェーズ ④連帯逆説 ⑤闇市場**という5本の欠落軸を補う。
戦略層（銀河グラフ）・内政層（Province/GovernanceRules）・社会層（HopeRules/ConsentRules）すべてに接続できる。

---

## 1. 役に立つ視点（要約）

ペストの世界観を、**本システムに効く形**で1行ずつ：

1. **疫病は銀河グラフ上のネットワーク拡散問題**。接触のある星系間回廊を通じて感染が伝播する——SIR モデルの星間版。
2. **封鎖は「軍事占領」でなく「保護的遮断」**。コルドン・サニテールは敵を封じるのでなく、住民を守るために回廊を閉じる。目的・コスト・後遺症が軍事封鎖と異なる。→ `StrategyRules.IsFtlBlocked` に第二の封鎖型を足す。
3. **危機への制度的応答は相転移する**。否認（"大したことはない"）→承認（"非常事態"）→動員（"全力対応"）→疲弊（"慣れ・不満"）→収束。各フェーズで統治コスト・民心・物資消費が異なる。→ `EventEngine` × `GovernanceRules` で段階的な政策チェーン。
4. **共通の敵は分断を一時的に消す**。疫病を「共有の試練」とすることで、敵対していた勢力も連帯することがある。連帯は疫病が去ると崩れる——一時的コンセンサス。→ `LoyaltyRules`/`ConsentRules` への危機連帯修正子。
5. **封鎖で公式流通が止まると、非公式流通が生まれる**。禁制品・プレミアム取引・情報の闇売買——封鎖が長引くほど闇市場が肥大化し、統治コストが上がる。→ `MarketRules` への封鎖下闇市場経路。
6. **不条理な継続＝意味なき労働でも手を動かす**。感染対策要員は勝算のない状況でも活動を続ける——これが最後には疫病を制する。→ `DisclosureLedger` への世界観 lore（absurdist resilience）。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**GovernanceRules/HopeRules/EventEngine/MarketRules/StrategyRules を作り直さない**。
> PEST はそれらに**欠落軸を足し、接続する**だけ（additive）。タイクン化回避＝高位の決断→エンジン駆動→創発帰結。

### ★★★ 最優先（真の欠落・ペストの signature）

#### PEST-1 疫病拡散モデル（`EpidemicRules`/`Epidemic`）
- **SIR 型拡散**：`Epidemic`（状態データ＝感染星系セット・感染度・収束フラグ）＋
  `EpidemicRules`（static 純ロジック）＝`Spread(map, epidemic, dt)`（接触回廊から確率的拡散）・
  `IsInfected(systemId)`・`Infect/Recover`・`SpreadRate`（接触率×感染度）・
  `RecoveryRate`（星系医療力 = `Province.stability` 比例）・`ExtinctionCheck`（感染0で終息）。
- **グラフ拡散**：銀河グラフ（`GalaxyMap`）の回廊を伝播経路とする。隣接する感染星系が多いほど感染確率↑。
- **星系固有耐性**：`GovernanceRules.OutputFactor`（安定度）が高い星系は自然回復率↑。
- 接続：`GalaxyView.Update` が `EpidemicRules.Spread` を `CalendarDispatcher` の日次フックで呼ぶ（TIME-6 連動）。
- EditMode テスト必須：拡散が回廊グラフに従うこと・感染度0で終息・安定度が回復率に効くことを確認。

#### PEST-2 疫病隔離回廊（コルドン・サニテール）
- **非軍事的回廊封鎖**：`EpidemicRules.IsQuarantined(corridor)` ＋ `Quarantine/Lift`。
  封鎖した回廊は FTL・亜光速ともに通行不可だが、**軍事占領ではない**ため：
  - `FactionRelations.IsHostile` は変化しない（敵対状態に変えない）
  - 補給は遮断される（`SupplyRules.SuppliedSystems` の到達判定が隔離回廊で止まる）
  - 長期封鎖コスト：`Province.stability` 低下（閉じ込められた側のストレス）
- **プレイヤーの決断**：封鎖継続（感染防止↑/安定低下）vs 封鎖解除（感染再拡大リスク/安定回復）＝`EventEngine` の選択肢として提示。
- 接続：`StrategyRules.IsFtlBlocked` とは別経路（軍事 vs 隔離を区別）。

### ★★ 高（危機統治の動的フェーズ）

#### PEST-3 危機対応フェーズ弧（否認→承認→動員→疲弊→収束）
- **フェーズ状態機械**：`CrisisPhase`（enum：否認/承認/動員/疲弊/収束）＋
  `CrisisArc`（感染規模閾値・経過時間・`FactionState` の結束/腐敗に応じてフェーズ遷移）。
- **フェーズ別効果**：
  - 否認：対策コスト0だが感染加速
  - 承認：`GovernanceRules.Tick` に安定低下修正子
  - 動員：`FleetPool` の一部を衛生部隊へ転用（戦力↓/感染回復速度↑）
  - 疲弊：`ConsentRules.Withdraw` 圧力↑（長引く制限への反発）
  - 収束：感染終息後の安定回復・連帯結束の残留ボーナス
- 接続：`EventEngine` が各フェーズ境界で通知イベントを発火。`NotificationCenter.Push(戦闘, 警告)` で盤面通知。

#### PEST-4 連帯の逆説（共有危機下の共同体結束強化）
- **共有危機連帯修正子**：`EpidemicRules.SolidarityBonus(factionA, epidemic)` ＝
  感染規模が一定を超えると `Community.Hope` の自然減衰を緩和（危機を「共有の試練」と認識）＋
  `ConsentRules.ControlStrength` に一時的連帯修正子（非協力ボイコットの閾値を上げる）。
- **他勢力連帯**（任意）：感染が複数勢力の星系に拡大した場合、
  `DiplomacyRules.TargetOpinion` に共同対処ボーナスを加算（共通の脅威が外交を緩める）。
- **収束後の崩壊**：疫病終息と同時に連帯修正子は除去＝平時の対立が戻る（一時的コンセンサスの終わり）。
- 接続：`HopeRules`/`ConsentRules`/`DiplomacyRules` への additive 修正子（基準値非破壊・実効値パターン）。

### ★ 中（封鎖経済の創発・世界観 lore）

#### PEST-5 封鎖下の闇市場（欠乏→非公式流通路の創発）
- **闇市場ルール**：`BlackMarketRules`（static）＝
  `EmergenceRisk(province, supplyBlockedDays)` （封鎖が長いほど闇市場が生まれやすい）・
  `BlackMarketStrength(province)` （闇市場の規模・安定度低下と比例）・
  `UndergroundSupplyFactor` （補給の一部を闇市場が代替。欠乏を完全には解消しない）・
  `TaxLeakage` （闇市場規模×税収の毀損率＝`FiscalRules.TaxRevenue` への係数）。
- **プレイヤーの選択**：取り締まり（安定保護/物資不足継続）vs 黙認（物資確保/腐敗と税収毀損）。
- 接続：`SupplyRules`（補給遮断トリガー）×`MarketRules`（公式均衡への影響）×`FiscalRules`（税収毀損）。

#### PEST-6 （lore）不条理な継続と感染終息後の世界観開示
- **内容**：疫病が「個人の英雄的行動でなく、日常的な粘り強い継続作業」で制される——
  これは absurdist resilience（不条理な粘着）の体現。
  感染終息後に「見えない敵との戦いは終わったが、次の疫病はいつ来るか分からない」という開示。
- **接続**：コード新設なし。`DisclosureLedger`（FND-4）への **lore データ入力**のみ。
  `DisclosureEntry`（category=秘史/真相）で疫病終息後に解放（CCX-6 世界観 codex 方針に一貫）。

### ❌ 不採用（重複・既存で十分・タイクン化）

| 不採用 | 理由 |
|---|---|
| 個々の市民のマイクロ行動（逃亡/残留/ボランティア選択） | タイクン化。Province/Community の集合値で十分 |
| 医療リソースの在庫管理（病院ベッド数・薬剤） | タイクン化。`ResourceStockpile`（#92 L-1）で代替可能 |
| キャラクターの感染・死亡RNG | `LifecycleRules.ShouldDieOfAge` の疫病版は設計が複雑なうえ感情操作的。ロスター規模をマクロで下げるだけで十分 |
| 宗教と疫病の接続 | `ReligionRules`（#172-175）が既にカバー。SAW-7（教会の経済力）との重複 |
| 疫病の「神学的罰」メカニクス | `DisclosureLedger` の lore 入力で十分。コード新設不要 |
| 国際支援・援助ルート | `DiplomacyRules`（DIP-2）で条約として表現可能。PEST 専用コード不要 |

---

## 3. EPIC #PEST の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。
> 既存ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #2223**。GitHub issue 起票済み（#2225〜#2242）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **PEST-1** | #2225 | 疫病拡散モデル（`EpidemicRules`/`Epidemic`・SIR型・銀河グラフ上の伝播） | 新 `EpidemicRules`/`Epidemic`（Core・test-first）。`GalaxyMap`×`CalendarDispatcher`（TIME-6） |
| **PEST-2** | #2228 | 疫病隔離回廊（コルドン・サニテール・非軍事封鎖） | `StrategyRules`/`EpidemicRules` 拡張。`SupplyRules` 到達判定へ |
| **PEST-3** | #2232 | 危機対応フェーズ弧（否認→承認→動員→疲弊→収束の状態機械） | 新 `CrisisArc`/`CrisisPhase`（Core・test-first）。`EventEngine`×`GovernanceRules` |
| **PEST-4** | #2236 | 連帯の逆説（共有危機→共同体結束・一時的コンセンサス） | `HopeRules`/`ConsentRules`/`DiplomacyRules.TargetOpinion` 修正子（additive） |
| **PEST-5** | #2239 | 封鎖下の闇市場（欠乏→非公式流通路の創発） | 新 `BlackMarketRules`（Core・test-first）。`SupplyRules`×`MarketRules`×`FiscalRules.TaxRevenue` |
| **PEST-6** | #2242 | （lore）不条理な継続と疫病後の世界観開示 | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`PEST-1`（疫病拡散＝最も固有で欠落の大きい signature・純ロジック先行）
→ `PEST-2`（隔離封鎖＝拡散モデルがあって初めて意味を持つ回廊制御）
→ `PEST-3`（危機フェーズ弧＝PEST-1/2 の盤面状態を段階的に統治層へ接続）
→ `PEST-4`（連帯の逆説＝フェーズ動員期の社会的帰結として接続）
→ `PEST-5`（闇市場＝封鎖経済の創発効果・`SupplyRules` 遮断後）
→ `PEST-6`（lore＝最後に開示エンジンへデータ入力）

> いずれも既存社会シミュを**後退させず接続**する additive 設計。
> 疫病という「軍事力で解決できない脅威」が、社会・内政・外交・経済の全層に波及する創発構造。
