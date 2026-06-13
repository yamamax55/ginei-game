# C1: Core 配線監査（orphan list）— 計算されるが効かないモジュールの棚卸し

> テスト計画 [test-completion-plan.md] の C1。`Assets/Scripts/Core` の `public static class *Rules`（約329）を、`Assets/Scripts/Game` からの参照有無で分類。
> 目的＝**完成に向けて「プレイヤーに届くべきなのに届いていない」モジュールを優先度付きで特定**する。

## 0. 結果サマリ

- Core 静的ルールクラス **約329**。うち **Game 層から直接参照あり＝約30%（〜99）／直接参照なし＝約70%（〜230）**。
- ＝**社会・政治・経済の膨大なシミュが計算されているが、その大半は盤面/会戦/UI に出ない**。

### ★重要な但し書き（誤読しないために）
1. **直接 grep は「推移的配線」を取りこぼす**＝orphan に見えても、配線済みオーケストレータ経由で実は効いているものがある：
   - `MilitarySwordHonorRules`→`WarCollegeCareerRules`→`GalaxyView`（今セッションで配線）＝恩賜の軍刀・昇進優遇は効いている。
   - `FiscalRules`/`BudgetRules`→`CampaignRules.TickFiscalYear`/`TickBudgetDay`→`GalaxyView`＝財政は効いている。
   - `CombatModifiers`/`ShipCombat` 等は会戦コンポーネント経由で効く。
   - → 「直接参照0」は**配線候補のシグナル**であって即バグではない。
2. **未配線の多くは設計上わざと**（CLAUDE.md 観測層節：「第2層『操作化』は手で昇格＝自動化しない核」／経済はタイクン化/終盤ラグ回避のため**集約・背景化**）。**全230を配線するのは禁忌**（Stellaris 型終盤ラグの道）。
3. よって本監査の使い方＝**「届くべきもの」だけを Tier A から選んで配線**し、Tier C は breadcrumb として残す。

---

## 1. Tier A — 完成のために配線すべき（プレイヤーが効くと期待する・主要ループに絡む）

> 「軍事を続けられる／政体が動く／諜報が機能する」は銀英伝風 4X の中核体験。ここが空回りだと「選択が結果を変えない」。

### A-1 軍事兵站（長期戦の成立に必須）
| モジュール | 届いていない効果 | 配線先 |
|---|---|---|
| `MilitarySupplyRules`/`MilitaryDemandRules`/`MilitarySupplyFulfillmentRules`/`MilitaryReadinessRules`/`MilitaryLogisticsRules`(#2049) | 補給切れで前線が干上がる・戦闘力低下が起きない | `StrategicFleet.supply` 日次（一部 `RunMilitarySupplyTick` 配線済か要確認）→戦闘力#106 |
| `CommerceRaidingRules`(#95) | 通商破壊で敵を干上がらせられない | 戦略の補給線判定＋会戦の護衛/襲撃 |
| `EspionageRules` | 諜報・破壊工作が一切起きない | 戦略の情報/妨害アクション＋イベント#116 |

### A-2 軍政・政体の駆動（このPRで作った層の仕上げ）
| モジュール | 届いていない効果 | 配線先 |
|---|---|---|
| `CivilianControlRules`/`CoupRules` | クーデターリスクが評価されず政変が起きない | 年次 Tick で `CoupRisk`→`CoupRules.Resolve`→政体/正統性（MILGOV-US §4 で提案済） |
| `SuccessionRules`/`SuccessionLawRules`/`LeadershipElectionRules`/`PartyRules` | 君主継承・総裁選・与党首班が盤面で起きない | 年次/イベントで継承・選挙→元首/政府の長を更新 |
| `CommandChainRules`(MILGOV-US §3-A) | 指揮二系統の集中度→クーデター駆動因が未接続 | `CoupRisk` の driver へ |

### A-3 予算出資度の残（前タスクの続き）
| `MilitaryReadinessFactor`→戦闘#106／`DiplomacyOpinionBonus`→#189 | 予算の軍事/外交配分が会戦・関係に効かない | 会戦解決に勢力即応・`DiplomacyState` に opinion 加点の注入点 |

### A-4 外交の実体化
| `TreatyRules`/`TreatyManagementRules`/`TreatyLedger`(#191) | 条約（同盟/不可侵）が opinion/敵対に効かない | `DiplomacyTickRules` 経由で `FactionRelations` へ（DIP-1 配線の延長） |

---

## 2. Tier B — 深み（機会があれば配線・体感は中）

- **宗教・文化**：`ReligionRules`（改宗圧力・聖戦）／`CultureRules`（同化・分離独立・亡命）／`NonviolenceRules`。＝住民の思想対立・分離独立が盤面に出ない（#109/#194 と接続余地）。
- **司法・治安**：`JusticeRules`（正義観→正統性）。`LawEnforcementRules`/`CrimeRules` は LAW-6 で一部配線済（要確認）。
- **労働市場の集計**：`LaborRules`/`LaborMatchingRules`/`LaborWageRules`/`LaborProductivityRules`（失業率・賃金が支持/生産に効くか＝C2 で検証）。
- **人物職分**：`PersonVocationRules`（君主/政治家/文官/武官/技術者の役割が人事に効くか）。
- **成長/退役/スキル**：`GrowthRules`（経験→能力）・`AdmiralSkillRules`（提督パッシブ）・`AutonomyRules`（自律分散）＝**会戦の将才に効くべき**だが未配線（Tier A 寄りの候補）。

---

## 3. Tier C — 意図的 Core-first（breadcrumb として残す・配線しない）

> CLAUDE.md 既知の重複・将来整理対象／経済はBOMに寄せ集約・背景化。**ここを個別配線するとタイクン化＝終盤ラグ**。観測（J/E オーバーレイ）で見えれば十分。

- **東証33業種＋サブ業種 archetype（#2016-2025）**：`ChemicalRules`/`SteelRules`/`AutoRules`/`SemiconductorRules`/`ShippingRules`/`RetailRules`/`MiningRules`… 計100+。
- **金融市場の銘柄系（#185/#161/#1939…）**：`StockMarketRules`/`BondMarketRules`/`BankRules`/`FuturesMarketRules`/`MonetaryPolicyRules`/`FinancialCrisisRules`… ＝**集約（国庫/債務#163）に留め、個別銘柄シミュは観測どまり**が設計意図。
- **メディア/小売/不動産/サービス/宇宙派生（#2025）**：`FilmStudioRules`/`RealEstateRules`/`HotelRules`/`SpaceRailwayRules`… フレーバー。
- **世界観・遠未来**：`DisclosureLedger`（秘史開示・要演出UI）・儀礼/婚姻/三密 等。

> ＝Tier C は**「実装しない」ではなく「集約・観測で背景化し、個別ループ化しない」**が正。

---

## 4. C1 の結論と推奨

1. **完成に直結する配線は Tier A の十数個**（兵站・諜報・政体駆動・予算の軍事/外交・条約）。これらは「選択が結果を変える」中核体験で、未配線だと 4X として痩せる。
2. **Tier C（経済100+業種・金融銘柄）は配線しない**＝集約と観測で背景化（終盤ラグ回避の設計原則）。
3. **配線の前に必ず C2/C3/C4（因果が効くか）で「既に配線済みと思っている経路」が本当に通っているかを検証**（推移的配線の取りこぼし・途中で切れた連鎖をあぶり出す）。
4. 各配線は **Core 純ロジック追加なし・既存窓口呼び出し・観測層に出す・glossary 追記** を守る。

### 次アクション候補（優先）
- **A-2 軍政の駆動**：`CoupRisk`/`CommandChainRules`/継承・選挙 を年次 Tick へ（このPRの軍政層が初めて「効く」）。
- **A-1 兵站**：`RunMilitarySupplyTick` の実効（補給切れ→損耗→戦闘力）を C3 で検証してから穴埋め。
- **A-3 予算残**：`MilitaryReadinessFactor`/`DiplomacyOpinionBonus` の注入点。

> 注：本監査は直接参照ベースの静的解析。**「直接参照0」＝配線候補**であり、推移的に効くもの・設計上わざと Core-only のものを含む。Tier 分けは「プレイヤー体感への近さ×設計意図」で判断した。
