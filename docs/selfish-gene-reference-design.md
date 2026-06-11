# ドーキンス『利己的な遺伝子』参考設計（EPIC #MEME）

> 参照元：リチャード・ドーキンス『The Selfish Gene（利己的な遺伝子）』（1976）。
> 遺伝子こそが自然選択の単位であり、生物は「遺伝子の乗り物（vehicle）」に過ぎない——という遺伝子中心の進化論。
> さらに**ミーム（meme）＝文化的複製子**の概念を提唱。思想・慣習・ドクトリンは遺伝子と同様に「広まること自体」を目的として複製・競争する。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略＋既に大規模な社会・政治純ロジック層）にとって**役に立つ視点**だけを抽出し、EPIC `#MEME` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**進化論的メカニクス／世界観の構造パターンのみ**を参考にする。

---

## 0. なぜ「利己的な遺伝子」が本システムに役立つか

当プロジェクトは文化・信仰・ゲーム理論の**マクロ純ロジックを大量に保有**している（[CLAUDE.md] 参照）：

| 既存（抽象レベル） | カバー範囲 |
|---|---|
| `CultureRules`/`Culture`（#194） | 同化圧力・分離独立・ナショナリズム・亡命 |
| `ReligionRules`/`Religion`（#172-175） | 改宗圧力・異端・聖戦圧力・社会効果 |
| `GameTheoryRules`/`Move`（#388） | Nash均衡・TitForTat・ZeroSum・囚人のジレンマ |
| `ConsentRules`/`Polity`（#836） | 被統治者の協力・非協力・統治不能 |
| `LoyaltyRules`/`Allegiance`（#817） | 忠誠-調略-旗幟のカスケード |
| `NonviolenceRules`/`Movement`（#831） | 運動イデオロギーの伝播・弾圧の逆効果 |
| `Organization`/`SuccessionRules`（#812） | カリスマ死後の組織存続・制度化 |
| `ResearchRules`/`ResearchProject`（#123-127） | 研究産出・政体偏り |

**しかし、これらは「国家・市場・個人」という主体を単位にした静的均衡モデル**であり、ドーキンスが固有に描く以下が**欠けている**：

| 『利己的な遺伝子』が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **ミーム（文化的複製子）＝伝播力≠有益性** | `CultureRules` は集団文化の同化/抵抗をモデル化するが、**個別のアイデア/ドクトリンが「自己複製子」として感染力・持続性・複製精度パラメータを持ち、宿主に有害でも広まる**という逆選択ダイナミクスが無い |
| **ESS（進化的安定戦略）** | `GameTheoryRules` は Nash（静的均衡）のみ。**まれな突然変異ドクトリンが侵入できるか否かを判定する進化的安定性**、および「戦略の頻度が適応度に応じて変化する複製子動力学（replicator dynamics）」が無い |
| **軍拡競争（Arms Race）の共進化動力学** | `ResearchRules` は研究産出を出力するが、**攻→守→攻の正フィードバックループ＝片方の強化が他方を強化する連鎖**、および資源制限で一方が経済的に崩壊する終局が無い |
| **包括適応度・イデオロギー血縁係数（Hamilton則 r×B>C）** | `DiplomacyRules` は opinon ベースの同盟。**「どれだけイデオロギープログラムを共有しているか」という構造的な関係係数**が無く、説明できない連立安定性のパターンが生じる |
| **ミームの「乗り物」とその搾取** | 組織は自分を維持するミームの乗り物になりうる（カルト・プロパガンダ）。**「宿主に有害なミームが組織を乗っ取るメカニズム」**＝有害ミームの逆選択が無い |

**結論**：『利己的な遺伝子』は当プロジェクトに**①ミーム複製モデル（伝播力≠有益性） ②ESS（進化的安定戦略の動力学） ③軍拡競争の共進化ループ ④包括適応度によるイデオロギー血縁連立**という4つの欠落軸を与える。いずれも既存の文化・信仰・ゲーム理論を**後退させず足す**additive設計。

---

## 1. 役に立つ視点（要約）

本システムに効く形で1行ずつ：

1. **ミームは「広まること」を目的に複製競争する**。有益かどうかは関係ない。→ 宣伝・ドクトリン・カルト思想が**勢力を内部から食い荒らすメカニズム**（逆選択）を与える。`CultureRules`/`ReligionRules` では表現できない「有害なのに広まる」ダイナミクスを補完。
2. **進化的安定戦略（ESS）は Nash とは違う**。静的最適ではなく「まれな変異が侵入できない」という動的安定性。→ 複数勢力が取るドクトリンの長期均衡、および「新ドクトリンはなぜ定着するか/しないか」の理論的骨格を既存 `GameTheoryRules` に足す。
3. **軍拡競争は資源を食い尽くして崩壊する**。攻防の相互強化が終わるのは均衡でなく一方の財政破綻。→ `FiscalRules`/`ResearchRules` に「対抗的エスカレーション」の正フィードバックを追加。
4. **Hamilton則（r×B>C）は「共通の設計図を持つ者は損をしても協力する」**。→ `DiplomacyRules`（opinion）に「イデオロギー血縁係数」という構造的な連立安定性の土台を与える。同イデオロギー陣営の連立が維持される理由・崩れる閾値の理論的根拠。
5. **「遺伝子の乗り物」という視点は普遍的**。組織も国家もある思想の乗り物になりうる。→ 開示エンジン（FND-4）への**世界観loreデータ**として機能。宇宙帝国が「ある理念の乗り物」である閾値の問い。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`CultureRules`/`ReligionRules`/`GameTheoryRules`/`DiplomacyRules` を作り直さない**。MEME はそれらに**欠落軸を接続するだけ**（additive）。

### ★★★ 最優先（真の欠落・『利己的な遺伝子』の signature）

#### MEME ミーム複製モデル（伝播力≠有益性・有害ミームの逆選択）
- **`Meme`（純データ）**：`id`/`label`/`fidelity`（複製精度）/`fecundity`（感染力）/`longevity`（持続性）。
- **`MemeRules`（static）**：`SpreadPressure(meme, prevalence)` ＝ fecundity×prevalence×(1-prevalence) − decay×prevalence（ロジスティック感染）。`Compete(memes, mindShare)` ＝ 有限の「mindShare」をミームが奪い合う。`HarmFactor(meme, host)` ＝ 広まっても宿主の生産性/士気を下げる逆選択。
- 核心：**伝播力が高くとも有益性が低いミームが「有害ミーム（memetic parasite）」として蔓延し、勢力内部を食い荒らす**。プロパガンダ・カルト思想・硬直ドクトリンの力学。
- 接続：`Province`/`GovernanceRules.OutputFactor`（産出低下）・`FleetMorale`（士気低下）・`ReligionRules`（宗教ミームとして包含可）・`CultureRules`（文化バルクと対比）。

#### MEME ESS（進化的安定戦略）と勢力ドクトリンの進化的均衡
- **`EssRules`（static）**：`ReplicatorDynamic(strategies, payoffMatrix, frequencies, dt)` ＝ 戦略の頻度が相対適応度に比例して変化する差分方程式。`IsESS(strategy, payoffMatrix)` ＝ まれな突然変異戦略による侵入を拒絶できるか。`HawkDoveEquilibrium(V, C)` ＝ 利得 V・コスト C のHawk-Dove均衡（V/C の比が Hawks の安定割合を決める）。
- 核心：**既存 `GameTheoryRules` の静的Nash に「長期的に勢力がどのドクトリン比率に落ち着くか」の動力学**を追加する。まれな攻撃的/防衛的ドクトリンが定着するか否かの判定。
- 接続：`GameTheoryRules`（既存・委譲）・`ResearchRules`（ドクトリン研究の方向性）・`FactionState`（ドクトリン頻度を国家状態に反映）。

### ★★ 高（マクロ均衡に共進化の遊びを足す）

#### MEME 軍拡競争ダイナミクス（攻防の共進化・資源制限・崩壊リスク）
- **`ArmsRaceState`（純データ）**：`attackLevel`/`defenseLevel`/`investRate`/`degradeRate`。**`ArmsRaceRules`（static）**：`Tick(stateA, stateB, resourcesA, resourcesB, dt)` ＝ 双方が相手の水準に比例して投資を増やす正フィードバック。`OffenseDefenseRatio` ＝ 攻勢優勢/防勢優勢の判定。`CollapseRisk(state, fiscalHealth)` ＝ 軍拡コストが財政キャパを超えると崩壊リスク上昇。`IsRunaway` ＝ 資源制限なしのエスカレーション検出。
- 核心：**「相手が上げたから自分も上げる」が連鎖し、片方が経済的に崩壊して終わる**。均衡でなく崩壊で終わる軍拡競争の史実パターンを盤面に与える。
- 接続：`ResearchRules`（研究速度に乗算）・`FiscalRules.FiscalHealthFactor`（財政→崩壊リスク）・`LogisticsRules`（版図分断で崩壊加速）。

#### MEME 包括適応度・イデオロギー血縁係数（Hamilton則と連立安定性）
- **`IdeologicalRelatednessRules`（static）**：`RelatednessCoefficient(factionA, factionB, context)` ＝ 共有イデオロギー・政体・文化の重みつき重複（0-1）。`HamiltonCondition(r, benefit, cost)` ＝ r×B > C のとき協力が選択される。`AllianceStabilityScore(factions)` ＝ 全ペアのHamilton条件の充足度から連立全体の安定性を導出。`IsVulnerableToDefection(faction, alliance)` ＝ 相対的に血縁係数が低い勢力が真っ先に離脱する。
- 核心：**`DiplomacyRules`（opinion動向）に「なぜこの連立は損をしても崩れないか/なぜ崩れるか」の構造的な根拠**を与える。政体が似ていればopinionが低くても連立が保たれる理由、逆に近く見えて実は離反しやすいペアの検出。
- 接続：`DiplomacyRules`（opinion修正子）・`LoyaltyRules`（旗幟決定の追加係数）・`FactionStateRules`（国家状態の近さ）。

### ★ 中（世界観lore）

#### MEME（lore）世界観の開示データ「遺伝子視点の宇宙史」
- 「国家・軍隊・王朝は、あるイデオロギー/設計図の乗り物に過ぎないのか？」という問い。
- 開示連鎖：「遺伝子中心の視点」→「ミーム中心の視点」→「乗り物を超えた自律知性は可能か」。
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への**loreデータ入力のみ**。世界観EPIC（秘史/ID vs 進化論/ニーチェ/啓蒙）への接続。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 文化的同化・民族同化の再実装 | **`CultureRules` がカバー**。MEMEは「個別アイデアの複製」のみ追加 |
| 宗教の伝播・改宗圧力の再実装 | **`ReligionRules` がカバー**。宗教ミームは `MemeRules` への接続のみ |
| 静的ゲーム理論の再実装 | **`GameTheoryRules`（Nash/TitForTat）がカバー**。ESSは動的部分のみ追加 |
| 研究システムの全面再設計 | **`ResearchRules` がカバー**。軍拡競争は係数として接続するだけ |
| 外交・条約メカニクスの再実装 | **`DiplomacyRules`/`TreatyRules` がカバー**。血縁係数は修正子として接続 |
| 生物進化シミュレーション（種の進化・遺伝子プール） | ゲームの主題外（宇宙戦略）。個別生物の進化は採用しない。**構造パターンのみ**転用 |
| ドクトリン「遺伝子」の個別管理 | タイクン化回避。マイクロ管理でなく係数で効かせる |

---

## 3. EPIC #MEME の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。既存ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #1836**。GitHub issue 起票済み（#1843〜#1863）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **MEME-1** | #1843 | `MemeRules`/`Meme` — ミーム複製モデル（fidelity/fecundity/longevity・ミームプール競争・有害ミームの逆選択） | 新 `MemeRules`/`Meme`。`Province`×`GovernanceRules`。有害伝播の逆選択 |
| **MEME-2** | #1848 | `EssRules` — ESS（進化的安定戦略）と勢力ドクトリンの進化的均衡（Hawk-Dove・replicator dynamics） | `GameTheoryRules` に動力学を追加。`ResearchRules`/`FactionState` 接続 |
| **MEME-3** | #1855 | `ArmsRaceRules` — 軍拡競争ダイナミクス（攻防の共進化・資源制限・財政崩壊リスク） | `ResearchRules`×`FiscalRules.FiscalHealthFactor`×`LogisticsRules` |
| **MEME-4** | #1859 | `IdeologicalRelatednessRules` — 包括適応度・イデオロギー血縁係数（Hamilton則 r×B>C と連立安定性） | `DiplomacyRules`（opinion修正子）×`LoyaltyRules`×`FactionStateRules` |
| **MEME-5** | #1863 | （lore）世界観の開示データ「遺伝子視点の宇宙史」（乗り物・ミームの逆支配・超越の問い） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順

`MEME-1`（ミーム複製モデル＝最も固有で欠落の大きいsignature・純ロジック基盤）→ `MEME-2`（ESS＝GameTheoryRulesへの動力学追加）→ `MEME-3`（軍拡競争＝ResearchRules/FiscalRulesへの係数追加）→ `MEME-4`（Hamilton則＝DiplomacyRulesへの構造的土台）→ `MEME-5`（lore・コード不要）。

> いずれも既存の文化・信仰・ゲーム理論・外交を**後退させず接続**する additive 設計。
