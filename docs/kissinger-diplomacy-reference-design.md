# キッシンジャー『外交』参考設計（EPIC #KISS）

> 参照元：ヘンリー・キッシンジャー『外交』（1994）。ウィーン会議からベトナム戦争に至る国際政治史を、**勢力均衡・国際秩序の正統性・革命国家・大国協調**の概念で読み解く外交理論の古典。
> 本ドキュメントは、当プロジェクト（Ginei＝多勢力星間国家戦略＋既に豊富な外交純ロジック層）にとって**役に立つ視点**だけを抽出し、EPIC `#KISS` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**外交メカニクス／世界観の構造パターンのみ**を参考にする。

---

## 0. なぜ「キッシンジャー『外交』」が本システムに役立つか

当プロジェクトは外交の**基礎層**を大量に保有している：

| 既存（外交・国家ロジック） | カバー範囲 |
|---|---|
| `DiplomacyState`/`DiplomacyRules`（#189 DIP-1） | 外交状態enum×opinion×状態遷移→`FactionRelations.IsHostile` |
| `TreatyRules`/`Treaty`（DIP-2 #191） | 条約データ・OpinionEffect・Leverage・IsBreach |
| `WarGoalRules`（DIP-3 #192） | 厭戦・GoalLegitimacy・PeaceAcceptance |
| `FactionState`/`FactionStateRules` | 王朝正統性・腐敗・同意・希望の合成 |
| `Regime`/`DynastyRules`（#867） | 天命喪失・改革/革命サイクル |
| `Organization`/`SuccessionRules`（#812） | カリスマの日常化・制度存続 |
| `EspionageRules` | スパイ網・工作・妨害 |
| `ThreatCoalitionRules`（SHIO-4） | 共通脅威→臨時連合→脅威消失→自動解散 |
| `LogisticsRules`（#844） | 版図の地理的一体化度 |
| `CoupRules`（#215-219） | クーデターリスク・成功率・事後正統性 |

**しかし、これらは「個別の外交行為・国家内部崩壊」の層であり、『外交』が固有に描く以下が欠けている**：

| キッシンジャー固有の視点 | 当プロジェクトでの欠落 |
|---|---|
| **国際秩序の正統性**（受容された枠組みの有無） | 外交状態は2国間関係のみ。**体制全体の正統性（受容or拒絶）** が無い |
| **革命国家の特殊行動様式**（規則を拒絶する主体） | `FactionState.legitimacy` は国内正統性のみ。**「ルールそのものを拒絶する勢力」の外交ロジック**が無い |
| **勢力均衡の構造的傾向**（平時の恒常的均衡志向） | SHIO-4 は危機時の臨時連合。**常時作動する覇権抑止メカニズム**（同盟選択を歪める背景力）が無い |
| **大国協調・球圏認知**（協議による秩序維持） | 2国間条約のみ。**大国間の多角的協調と勢力圏の相互承認**が無い |
| **外交的孤立工作**（ビスマルク式連衡術） | 個別の同盟締結のみ。**同盟網を設計して競合相手を包囲孤立させる**戦略が無い |
| **正統性ある講和の永続性**（ウィーン vs ヴェルサイユ） | `WarGoalRules.PeaceAcceptance` は受諾確率のみ。**講和の質が戦後安定に与える長期影響**が無い |
| **均衡者（ピボット国家）の戦略的曖昧さ** | 同盟or非同盟の二値のみ。**どちらにでも傾ける中間的均衡国の戦略的価値**が無い |

**結論**：『外交』は当プロジェクトの2国間外交層に**①国際秩序の正統性 ②覇権均衡の構造力 ③大国協調による秩序管理 ④外交孤立工作 ⑤講和の質と戦後安定 ⑥均衡者**という6つの欠落軸を与える。これらは**DIP-1〜3（基礎外交）の上に乗る上位層**であり、DIP を壊さず additive に接続する。

---

## 1. 役に立つ視点（要約）

『外交』の世界観を、**本システムに効く形**で1行ずつ：

1. **国際秩序は「正統性」があると安定し、「革命国家」が現れると破綻する**。ウィーン体制は敗者フランスも受け入れ100年の平和を実現、ヴェルサイユは逆。→ **正統性を持つ講和と持たない講和の帰結の分岐**（`WarGoalRules`の上位層）。
2. **勢力均衡は外交官が設計せずとも自然に発生する歴史の法則**。ある国が覇権に近づくと他国は自動的に連衡する。→ **覇権指数が外交同盟選択を歪める背景力**（SHIO-4 の常時作動版）。
3. **大国は協調によって秩序を管理できる**。ウィーン体制の核心は「大国が協議して球圏を認め、革命を押さえ込む」Concert（欧州協調）。→ **多角的協調体制と勢力圏の相互承認**。
4. **外交の天才は孤立工作で戦う前に勝つ**。ビスマルクは同盟網を設計してフランスを孤立させ、戦わずに優位を保った。→ **孤立指数と同盟網設計の武器化**（`EspionageRules`×外交）。
5. **均衡者（オフショア・バランサー）の戦略的曖昧さが鍵を握る**。どちらにも付かないと見せることで、双方から引き付けられる最大の外交価値を持つ。→ **ピボット国家の特殊ステータスと外交交渉力ボーナス**。
6. **革命国家はリアルポリティクではなくイデオロギーで行動し、通常外交が効かない**。→ 国内の `Regime`/`FactionState` の状態が外交行動様式を根本から変える接続。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**DIP-1〜3・SHIO-4・`FactionState`/`Regime` を壊さない**。KISS はそれらに**欠落軸を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・キッシンジャーの signature）

#### KISS-1: 国際秩序の正統性と革命国家（`InternationalOrderRules`）
- **国際秩序正統性**：主要勢力が現在の国際的取り決め（勢力配置・条約体系）を「正統（受容された）」と見なすかどうか。正統性が高い秩序は安定し、低いと革命国家が現れやすい。
- **革命国家**：`FactionState` の特殊フラグ——規則そのものを拒絶する勢力。通常の外交（条約・opinion調整）が機能しない。containment か transformation かの二択を迫る。
- `OrderLegitimacy`（struct）：`legitimacyLevel`（主要勢力の加重受容度）、`isRevolutionary[faction]`
- `InternationalOrderRules`（static）：`IsRevolutionary(factionState)`（内部正統性崩壊×イデオロギー過激化→革命国家判定）／`OrderStability(allFactions)`（全主要勢力の受容加重平均）／`ContainmentPressure`（革命国家への包囲圧力）／`IntegrationThreshold`（懐柔条件）
- 接続：`FactionState`・`Regime`・`DiplomacyRules`・`WarGoalRules`

#### KISS-2: 勢力均衡の構造的傾向（`HegemonyBalanceRules`）
- **SHIO-4 との違い**：SHIO-4 は「共通脅威への反応的・臨時的連合」。KISS-2 は「覇権に近づいた勢力への平時の恒常的均衡志向」——危機でなく常時作動する背景力として同盟選択を歪める。
- `HegemonyIndex`：全勢力の相対的実力シェア。`HegemonThreshold` 超えた勢力は均衡連衡の標的になる。
- `HegemonyBalanceRules`（static）：`IsBidForHegemony(faction, map)`（HegemonyIndex×LogisticsRules.CohesionFactor）／`BalancingPressure(faction)`（対抗側が感じる連衡動機 0..1）／`SwingStateValue(faction)`（どちらにも付けるピボット国の外交価値）
- 接続：`LogisticsRules`・`DiplomacyRules`（同盟決定のmodifier）・SHIO-4 `ThreatCoalitionRules`（均衡圧力がthreshold超えで臨時連合に転化）

### ★★ 高（欠落が大きく既存を補完）

#### KISS-3: 大国協調体制と勢力圏認知（`ConcertRules`）
- **Concert**：閾値以上の実力を持つ「大国」が協議テーブルに参加し、秩序を能動的に管理する仕組み。大国同士が「あちらの勢力圏は不干渉」と相互承認＝革命の連鎖を抑制する。
- `SphereOfInfluence`（pure data）：`claimant`（主張勢力）、`includedSystems`（対象星系ID）、`recognizedBy[]`（承認した大国リスト）
- `ConcertRules`（static）：`GreatPowerThreshold`（実力シェア）／`GreatPowers(allFactions)`／`CanCallConclave`（大国が過半で招集）／`RecognizeSphere(sphere, byFaction)`／`ViolatesSphere(action, sphere)`（内政干渉→Concert 信用の損失）／`ConcertStability`（参加大国の相互承認度）
- 接続：`DiplomacyState`・`TreatyRules`・`GalaxyMap`・KISS-1 `InternationalOrderRules`

#### KISS-4: 外交的孤立工作（`DiplomaticIsolationRules`）
- ビスマルクの武器：同盟網を設計して競合相手を外交的に包囲孤立させる。孤立指数が高い勢力は戦争リスク増大（援軍なし）・交渉力低下。
- `DiplomaticIsolationRules`（static）：`IsolationIndex(faction, diplomacyStates)` 0..1（友好同盟数/隣接勢力数の逆比）／`IsEncircled(faction, map)`（GalaxyMap 上の隣接敵≥友好）／`AllianceCoverage(faction)`（同盟でカバーされた隣接比率）／`IsolationWarRiskBonus`（孤立勢力は抑止が効かず先制リスク↑）
- 接続：`DiplomacyState`・`TreatyRules`・`EspionageRules`（条約を破らせる政治工作）・KISS-2 BalancingPressure

#### KISS-5: 正統性ある講和と戦後安定（`PeaceLegitimacyRules`）
- ウィーン会議（寛大な講和→100年の安定）vs ヴェルサイユ（過酷な講和→不満が次の戦争の種）。`WarGoalRules.PeaceAcceptance` は受諾確率のみで、講和後の**安定持続性**が無い。
- `PeaceLegitimacyRules`（static）：`SettlementLegitimacy(terms, loserWarWeariness, loserFactionState)` 0..1（条約寛大さ×敗者疲弊度×敗者国内正統性）／`RevanchismRisk(settlement, dt)`（正統性低講和→時間経過で再戦意欲↑）／`PostWarStabilityFactor`（戦後期間の外交opinion drift modifier）
- 接続：`WarGoalRules`・`DiplomacyRules.MakePeace`・`FactionState.legitimacy`・`Regime`・KISS-1 OrderLegitimacy

### ★ 中（均衡者・世界観lore）

#### KISS-6: 均衡者（ピボット国家）の戦略的曖昧さ（`PivotStateRules`）
- どちらにも付かない「均衡者」は双方から引き付けられる最大外交価値を持つ。戦略的曖昧さが交渉力に転化。英国の欧州均衡者ロールの機械化。
- `PivotStateRules`（static）：`IsPivot(faction, diplomacyStates)`（友好0かつ孤立0の中立勢力）／`PivotDiplomaticBonus`（曖昧性が高い間の条約交渉に加算）／`PivotThresholdBalance`（均衡者ボーナスが消える同盟コミット量）
- 接続：`DiplomacyState`・KISS-2 SwingStateValue・`TreatyRules`

#### KISS-7: （lore）世界観の開示データ
- 「勢力均衡は外交官なしに自然発生する歴史の法則」「革命国家は正統な秩序の外側に立つ」「寛大な講和だけが百年の平和を買う」
- 接続：**コード新設なし**。`DisclosureLedger`（FND-4）への lore データ入力のみ。CCX-6（世界観codex）方針に一貫。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 臨時脅威連合の新実装 | **SHIO-4 `ThreatCoalitionRules` がカバー**。KISS-2 はその「平時版背景力」として接続するのみ |
| 2国間条約・同盟締結の新実装 | **DIP-2 `TreatyRules` がカバー**。KISS は上位概念のみ追加 |
| 和平受諾確率の新実装 | **DIP-3 `WarGoalRules.PeaceAcceptance` がカバー**。KISS-5 は講和後の長期影響のみ |
| 革命そのものの実装 | **`Regime.Revolution`・`DynastyRules` がカバー**。KISS-1 は「革命国家の外交行動様式」のみ |
| クーデター・内乱の詳細 | **`CoupRules` がカバー** |
| 外交条約の細目（カサスベリ/戦争目標） | **DIP-3 `WarGoalRules`/`TreatyRules` がカバー** |
| 外交官ネームドキャラのUI | 盤面配線は後続。今回は純ロジック層のみ |

---

## 3. EPIC #KISS の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。既存外交ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #1905**。GitHub issue 起票済み（#1908〜#1920）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **KISS-1** | #1908 | 国際秩序の正統性と革命国家（`InternationalOrderRules`・OrderLegitimacy・IsRevolutionary） | `FactionState`/`Regime`×`DiplomacyRules`。通常外交が効かない革命国家の識別と封じ込め |
| **KISS-2** | #1912 | 勢力均衡の構造的傾向（`HegemonyBalanceRules`・覇権指数・BalancingPressure・SwingStateValue） | `LogisticsRules`×DiplomacyRules×SHIO-4。平時の背景力として同盟選択を歪める |
| **KISS-3** | #1915 | 大国協調体制と勢力圏認知（`ConcertRules`・`SphereOfInfluence`・相互不干渉） | `DiplomacyState`×`TreatyRules`×KISS-1 OrderLegitimacy |
| **KISS-4** | #1916 | 外交的孤立工作（`DiplomaticIsolationRules`・IsolationIndex・IsEncircled・AllianceCoverage） | `DiplomacyState`×`EspionageRules`×KISS-2 BalancingPressure |
| **KISS-5** | #1917 | 正統性ある講和と戦後安定（`PeaceLegitimacyRules`・SettlementLegitimacy・RevanchismRisk） | `WarGoalRules`×`DiplomacyRules.MakePeace`×`FactionState` |
| **KISS-6** | #1919 | 均衡者・ピボット国家（`PivotStateRules`・戦略的曖昧さ→外交交渉力ボーナス） | `DiplomacyState`×KISS-2 SwingStateValue×`TreatyRules` |
| **KISS-7** | #1920 | （lore）世界観の開示データ（勢力均衡の法則/革命国家/寛大な講和の永続性） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`KISS-1 → KISS-2`（正統性と均衡＝最も固有で欠落の大きい signature）→ `KISS-3`（Concert＝正統性を能動的に管理する仕組み）→ `KISS-4`（孤立工作＝外交武器化）→ `KISS-5`（講和品質）→ 残り（KISS-6/7）。

> いずれも既存外交層（DIP-1〜3）・SHIO-4・`FactionState` を**後退させず接続**する additive 設計。多勢力バランス（帝国/同盟/フェザーン等）に最も効く。
