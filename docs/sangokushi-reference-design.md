# 三国志演義 参考設計（EPIC #SGZ）

> 参照元：羅貫中『三国志演義』（歴史小説）。後漢末〜三国鼎立〜晋による統一を描く群雄割拠の群像劇。
> 戦争・謀略・義兄弟の誓いと離反・軍師の智謀が錯綜する「多極動態の実験場」——星間国家戦略ゲームに接合できる構造パターンが豊富。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用しない。**メカニクス／世界観の構造パターンのみ**参考にする。

---

## 0. なぜ「三国志演義」が本システムに役立つか

当プロジェクトは多極戦略・人物系の**純ロジック層を大量に保有**している：

| 既存（マクロ・抽象） | カバー範囲 |
|---|---|
| `DiplomacyRules`/`DiplomacyState`（#189） | 外交状態・opinion・条約・敵対判定 |
| `LoyaltyRules`/`Allegiance`（#817） | 諸侯の旗幟・カスケード・静観・寝返り |
| `BattleAllegianceRules` | 会戦中の陣営転換 |
| `EspionageRules`/`SpyNetwork` | 諜報ミッション・情報収集・妨害・捕虜 |
| `Person`/`PersonRules`（#866） | 軍人/文民・役職・適材適所 |
| `AdmiralData.staffOfficers` | 参謀3名・能力底上げ（受動的） |
| `GameTheoryRules`（#388） | 囚人のジレンマ・Nash均衡・ゼロサム |
| `SuccessionRules`/`Organization`（#812） | 英雄死後の組織存続・カリスマの日常化 |
| `FactionStateRules`/`FactionState` | 国家状態・腐敗・合意・正統性 |
| `SupplyRules`/`ResourceProductionRules` | 補給線・資源生産 |

**しかし、これらは存在するが接続が薄い・または視点が欠けている**：

| 三国志演義が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **多極均衡（天下三分の計）** ＝弱者が結合して強者を抑止する「力の均衡」 | `GameTheoryRules.NashEquilibrium` はゲーム抽象。**「最強勢力が台頭すると弱小諸侯が連衡する」均衡圧力**が無い。`DiplomacyRules` は個別条約のみで力学的バランスを見ない |
| **軍師の献策・採択ループ** ＝参謀が具体的な策を進言し君主が採る/断る→帰結が変わる | `staffOfficers` は能力加算のみ（受動）。**「策を提案→採択確率×状況評価→帰結修正子」の能動的献策ループ**が無い |
| **個人結盟と離反（義兄弟型）** ＝特定人物間の誓約が政治・軍事を超えた拘束力を持ち、破綻が劇的カスケードを起こす | `LoyaltyRules` は**勢力レベル**の忠誠。**Person-to-Person の誓約・拘束力・裏切りペナルティ**が無い |
| **離間の計** ＝諜報で敵の同盟国間の信頼を意図的に破壊する | `EspionageRules` の `SabotageEffect` は軍事/経済妨害。**「敵A-B間の opinion を悪化させ同盟を崩す」標的型 opinion 工作**が無い |
| **屯田制（軍事農業拠点）** ＝占領地で軍が自給農業→補給線への依存を断つ | `SupplyRules.IsSupplied` は本国依存。**「占領地の軍が自活して補給線喪失に耐える」軍事農業植民地**が無い |

**結論**：三国志演義は当プロジェクトに**「多極動態の力学」**という視点を与える。既存の外交/諜報/忠誠/人物層に欠ける**5つの接続**を additive に足せる。

---

## 1. 役に立つ視点（要約）

1. **「弱者連衡で強者を抑える」＝多極均衡の本質**。2強対立（帝国vs同盟）だけでなく3以上の勢力が共存するとき、最強勢力が台頭するほど残りが自然に結束する。→ `DiplomacyRules` に力学的均衡圧力を足す。
2. **軍師の智謀は「策の提案→採択→帰結」のループ**。参謀が受動的な能力加算にとどまらず、具体的な戦略提案（献策）を出し、君主の知力×信頼でそれを採択するか決まり、採択した策が会戦や外交の修正子になる。→ `AdmiralData.staffOfficers` に能動的な献策エンジンを足す。
3. **個人の誓約は勢力の壁を超える**。義兄弟の誓いは、陣営が変わっても人物同士の拘束力が残る（あるいは破れた時に政治的激震を起こす）。→ `Person` 間の `Pledge`（誓約）データを足し、`LoyaltyRules` に接続。
4. **離間の計＝外交を壊す諜報**。軍事/経済の妨害と並ぶ第3の諜報目標として「敵同盟の信頼破壊」を追加。→ `EspionageRules` を拡張し、標的勢力ペアの `DiplomacyState.opinion` を操作する使命を足す。
5. **屯田制＝補給線の弱点を制度で埋める**。兵站が戦略を縛る（`SupplyRules`）中で、軍が自活できる軍事農業植民地は前線投射力を大幅に拡大する。→ `SupplyRules` に `MilitaryColony` を接続。
6. **「覇王と仁王」の分岐**＝制度的暴力で征服する路線と、仁徳で民心を集める路線。→ 既存 `FactionState.inclusiveness`（収奪↔包摂）と `ALM-王道/覇道軸` への接続（コード新設不要）。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`DiplomacyRules`/`LoyaltyRules`/`EspionageRules`/`SupplyRules`/`AdmiralData` を作り直さない**。SGZ はそれらに欠落軸を足し接続するだけ（additive）。

### ★★★ 最優先（真の欠落・三国志演義の signature）

#### SGZ 多極均衡・勢力均衡圧力（BalanceOfPowerRules）
- **ヘゲモン閾値**：勢力 X の支配力（星系数×安定度）が全勢力合計の `hegemonyThreshold`（既定 0.45）を超えたとき `IsHegemonic(x)` = true。
- **均衡圧力**：非覇権勢力の全ペアに対して `DiplomacyRules.TargetOpinion` に `coalitionBonus` を加算（弱者同士が自然に近づく）。
- **連衡トリガー**：覇権判定＋非敵対ペアの opinion が `allianceThreshold` を超えると `EventEngine` に「均衡同盟提案」を push。
- 接続：`CampaignRules.Tick` で `BalanceOfPowerRules.Tick` を回す／`DiplomacyRules`×`FactionStateRules`（`EffectiveStability` を強さ指標に使用）。
- 純ロジック・test-first（`GameTheoryRules.NashEquilibrium` の星系版）。

#### SGZ 献策システム（CounselRules / Stratagem）
- **Counsel（純データ）**：`advisorId`/`targetId`/`stratagemType`（enum: 奇襲/挟撃/離間/撤退誘導/持久/外交）/`confidence`（0..1）。
- **採択確率**：`AdoptionChance(counsel, rulerIntelligence, trustLevel)` ＝ `intelligence × (0.5 + 0.5 × trust) × confidence`（上限1）。
- **採択結果修正子**：`StratagemOutcome(type)` → 会戦ダメージ倍率 or `DiplomacyState.opinion` 変化 or 士気変化（`CombatModifiers` に合流）。
- **不採択でも情報価値**：`IgnoredCounsel` は `DisclosureLedger` に失敗loreとして積む（「臥龍の策を退けた」）。
- 接続：`AdmiralData.staffOfficers`（既存参謀が能動的に献策）×`EventEngine`（策提案イベント）×`PersonRules.CivilAptitude`（軍師の知力）。
- 純ロジック・test-first。

### ★★ 高（多極動態の核心を補完する）

#### SGZ 個人結盟と盟誓（PledgeRules）
- **Pledge（純データ）**：`personA`/`personB`/`pledgeType`（enum: 義兄弟/主従/不可侵/密約）/`strength`（0..1）/`locked`（誓約成立で不可逆）。
- **拘束力**：`BondFactor(pledge)` → `LoyaltyRules.ResolveStance` に追加修正子（誓約相手側への stanceは良化）。
- **離反ペナルティ**：`BetrayalPenalty(pledge)` → `FactionState.Polity.legitimacy` 削減＋`EventEngine` に「誓約破棄」イベント push。
- **誓約検出**：`IsBetrayed(pledge, action)` ＝ 誓約相手を攻撃/裏切った場合 true。
- 接続：`Person`/`LoyaltyRules`/`BattleAllegianceRules`（会戦中の person 行動が pledge を破るか判定）。
- 純ロジック・test-first。

#### SGZ 離間の計（EspionageRules 拡張 — opinion 工作ミッション）
- **EstrangementMission（純データ）**：`spyNetwork`/`targetFactionA`/`targetFactionB`/`intensity`（工作強度）。
- **成功確率**：既存 `EspionageRules.MissionSuccessChance` を流用（`DetectionRisk` も既存）。
- **opinion 効果**：成功 → `DiplomacyRules.DriftOpinion(A, B, -delta)` → `BreakTreaty` リスク。
- **反間リスク**：成功でも `CounterMissionChance` で工作がバレ、逆に自勢力の信頼を損なう。
- 接続：`EspionageRules`（既存窓口を拡張）×`DiplomacyState.opinion`×`DiplomacyRules.BreakTreaty`。既存の `SabotageEffect` とは別ターゲット（軍事/経済 vs 外交 opinion）。
- 純ロジック・test-first。

### ★ 中（兵站戦略の深化）

#### SGZ 屯田制・軍事農業植民地（MilitaryColonyRules）
- **MilitaryColony（純データ）**：`systemId`/`ownerFaction`/`productivity`（0..1）/`garrisonStrength`/`established`（設置ターン）。
- **生産**：`LocalYield(colony, dt)` → 占領地で `ResourceStockpile` に物資産出（`ResourceProductionRules.Produce` を呼ぶ）。
- **補給自立**：`SupplyIndependence(colony)` ＝ colony の productivity が閾値を超えたら近傍艦隊を `IsSupplied=true` 扱い（本国補給線喪失でも稼動）。
- **リスク**：`AttackRisk(colony, enemyStrength)` → 農耕兵は守備力低（兵站の脆弱性）。
- 接続：`SupplyRules.IsSupplied` の例外経路として `MilitaryColonyRules.CoversByColony` を呼ぶ／`ResourceProductionRules`/`FleetPool`。
- 純ロジック・test-first。

#### SGZ（lore）多極世界観の開示データ
- 「天下三分の平衡は本質的に不安定——第4の力（統一者）が現れる前の均衡の詩学」
- 「覇者と仁者の分岐——制度的暴力で征服するか民心を集めるか。勝利はするが後世に何を残すか」
- コード新設せず `DisclosureLedger`（FND-4）への **lore データ入力**。

### ❌ 不採用（重複・既存で十分・タイクン化）

| 不採用 | 理由 |
|---|---|
| 個別武将の武力・知力数値ゲーム | `AdmiralData` の6能力で**既にカバー**。ゲーム的な「武力vs知力」比較の再実装はしない |
| 大規模バトルの一騎討ち | `BattleManager` の RTS 会戦と**方向性が異なる**。タイクン化（マイクロ個人戦）を招く |
| 連環の計（火計・水計）の物理演出 | 戦術演出。純ロジックではなくシーン依存＝今フェーズの対象外 |
| 人材吸引力（声望モデル） | バックログ「司馬遼太郎『項羽と劉邦』」の着眼点と重複→そちらで対応 |
| 諸葛亮型「軍師が全権掌握」の国家 | `GovernmentRegistry`/`OfficeRules` が**既にカバー**。重複新設しない |
| 呉蜀同盟の固定化 | `DiplomacyRules` 既存。SGZ はその動因（均衡圧力）を足すだけ |

---

## 3. EPIC #SGZ の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。
> **重複新設しない**＝`DiplomacyRules`/`LoyaltyRules`/`EspionageRules`/`SupplyRules` は接続のみ。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #1102**。GitHub issue 起票済み（#1103〜#1108）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **SGZ-1** | #1103 | 多極均衡・勢力均衡圧力（BalanceOfPowerRules＝最強勢力が台頭すると弱小諸侯が連衡する） | `CampaignRules.Tick`×`DiplomacyRules.TargetOpinion`×`FactionStateRules` |
| **SGZ-2** | #1104 | 献策システム（CounselRules＝参謀が策を提案→君主が採択→帰結修正子） | `AdmiralData.staffOfficers`×`EventEngine`×`CombatModifiers` |
| **SGZ-3** | #1105 | 個人結盟と盟誓（PledgeRules＝義兄弟型誓約・拘束力・離反ペナルティ） | `Person`×`LoyaltyRules`×`BattleAllegianceRules` |
| **SGZ-4** | #1106 | 離間の計（EspionageRules 拡張＝標的勢力ペアの opinion 工作・同盟崩壊） | `EspionageRules`×`DiplomacyState.opinion`×`DiplomacyRules.BreakTreaty` |
| **SGZ-5** | #1107 | 屯田制・軍事農業植民地（MilitaryColonyRules＝占領地自給で補給線依存を断つ） | `SupplyRules`×`ResourceProductionRules`×`FleetPool` |
| **SGZ-6** | #1108 | （lore）多極世界観の開示データ（天下三分の均衡詩学・覇者と仁者の分岐） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`SGZ-1`（均衡圧力＝多極世界の土台）→ `SGZ-2`（献策ループ＝智謀の機能化）→ `SGZ-3`（誓約システム＝個人関係の構造化）→ `SGZ-4`（離間の計＝諜報の外交兵器化）→ `SGZ-5`（屯田制＝兵站深化）→ `SGZ-6`（lore）。

> SGZ-1・SGZ-2 が他 EPIC の下地になる（多極均衡圧力は SAW の通貨戦争・PIL の継承戦争とも接合）。
