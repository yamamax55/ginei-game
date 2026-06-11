# ポリュビオス『歴史』参考設計（EPIC #POLY）

> 参照元：ポリュビオス『歴史』（紀元前2世紀、ギリシア人の目で描いたローマ覇権の分析）。
> **政体循環論（Anacyclosis）・混合政体の安定原理・Tyche（運命）と制度の関係**——「なぜローマだけが世界を制したか」を普遍法則として解析した歴史哲学。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略＋既に巨大な政体/社会シミュ層）にとって**役に立つ視点**だけを抽出し、EPIC `#POLY` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**政体メカニクス／歴史哲学の構造パターンのみ**を参考にする。

---

## 0. なぜ「ポリュビオス『歴史』」が本システムに役立つか

当プロジェクトは政体・社会シミュのロジックを**大量に保有**している（[CLAUDE.md] 参照）：

| 既存（政体・社会シミュ） | カバー範囲 |
|---|---|
| `DynastyRules`/`Regime`（#867） | 正統性/腐敗/徳・天命喪失・改革/革命サイクル |
| `ConstitutionRules`/`Constitution`（Wave2 #170） | 制約権力/権利/立憲君主制フラグ |
| `SeparationOfPowersRules`（Wave2 #171） | 権力分立・抑止均衡・専制リスク・機能停止 |
| `CoupRules`（Wave2 #215-219） | クーデタータイプ別成功率・粛清・内戦 |
| `ConsentRules`/`Polity`（#836） | 被支配者の合意・非協力・統治不能 |
| `FactionStateRules`/`FactionState` | 王朝/統治体/組織/共同体の統合安定度 |
| `LogisticsRules`（#844） | 版図一体化度・分断ペナルティ |
| `GrowthRules`/`Growth`（Wave1 #537-543） | 経験→実効能力・4アーキタイプ |
| `DisclosureLedger`（FND-4） | 秘史/真相/予言の連鎖開示 |

**しかし、これらは「個々の政体・社会要素の単品モデル」**であり、ポリュビオスが固有に描く以下が**欠けている**：

| ポリュビオスが固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **六政体の類型と腐落ベクトル（Anacyclosis）** | `DynastyRules` は王朝中心サイクル（天命）。**王政→僭主制→貴族制→寡頭制→民主制→衆愚制→…の六形態と各形態固有の腐落方向**の型が無い |
| **混合政体の安定指数（Monarchic/Aristocratic/Democratic成分の混合比）** | `SeparationOfPowersRules` は「分立の有無」。**三要素の混合度がそのまま耐腐落レジリエンスになる**という積極的な安定モデルが無い |
| **Tyche（運命）×制度の強靭さ（制度の良さが運命を手懐ける）** | `EventEngine` のイベント効果は制度品質で変調されない。**良い制度は災厄を軽減し、粗悪な制度は幸運すら無駄にする**という制度×確率の連結が無い |
| **普遍史の星系間因果波及（ある地域の事件が全域に波及）** | `LogisticsRules` は物理的連結。**決定的敗北・政変が隣接星系の安定度や忠誠に伝播する歴史的連鎖**のモデルが無い |
| **歴史の教訓（実用的歴史観＝危機を生き延びた知識が制度に蓄積）** | `GrowthRules` は個人成長。**組織・勢力が過去の危機から「制度知識」を蓄え、同型の危機への耐性を高める**機構が無い |

**結論**：ポリュビオスは当プロジェクトの政体シミュに**「政体類型の体系と循環」「混合比という積極的安定モデル」「制度が運命を左右するという哲学」「歴史の記憶が将来を変える機構」**という4つの欠落軸を与える。**既存の Wave2 政体群（`ConstitutionRules`/`SeparationOfPowersRules`/`CoupRules`）を後退させず接続する additive 設計**。

---

## 1. 役に立つ視点（要約）

ポリュビオス『歴史』の世界観を、**本システムに効く形**で1行ずつ：

1. **六政体は類型論であり循環論である**。王政・貴族制・民主制はそれぞれ固有の腐落経路で僭主制・寡頭制・衆愚制へと墜ち、衆愚制の混乱が強い指導者を呼び王政へ回帰する。→ `DynastyRules` の天命サイクルを「6類型の腐落ベクトル」で補強。
2. **最も安定した政体は三要素を混合した政体**。ローマの執政官（君主的）・元老院（貴族的）・民会（民主的）の三層は互いを牽制しつつ欠点を相殺する。→ `SeparationOfPowersRules` の「分立」に**「混合比」という積極的安定**を接合。
3. **Tyche（運命）は制度の堅牢さが手懐ける**。偶然のショックが深刻な打撃になるか軽微な揺らぎで済むかは、その国家の制度品質による。→ `EventEngine` のイベント効果に**制度レジリエンス係数**を接続。
4. **歴史は「有用な知識」の蓄積である**。将来のリーダーが過去の危機から学べるよう記録する——ポリュビオスが『歴史』を書いた動機。→ `InstitutionalMemoryRules` で組織が「危機の記憶」を蓄え将来の同型危機を軽減。
5. **普遍史＝事件は孤立しない**。紀元前220年以降の地中海史は有機的に連結し、一地方の事件が全域に波及した。→ 銀河グラフ上で決定的事件が隣接星系へ不安定化波及（`UniversalHistoryRules`）。
6. **政体論は世界観の秘史**。「なぜ帝国は腐る運命にあるのか」「混合政体だけが永続するか」——ポリュビオスの問いは銀英伝の核と直結する。→ `DisclosureLedger` の開示データとして既存 lore に接続。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`DynastyRules`/`ConstitutionRules`/`SeparationOfPowersRules`/`CoupRules` を作り直さない**。POLY はそれらに**欠落軸を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・ポリュビオスの signature）

#### POLY 六政体類型と腐落ベクトル（Anacyclosis ＝ 政体循環論）
- **`RegimeForm`** enum：`{王政, 僭主制, 貴族制, 寡頭制, 民主制, 衆愚制}`
- **腐落ベクトル**：王政→僭主制 / 貴族制→寡頭制 / 民主制→衆愚制（各「良形態」→「腐落形態」）、衆愚制→王政（循環の回帰）
- **`AnacyclosisRules`**（pure logic, test-first）：`CurrentForm(regime, polity, constitution)` で既存データから現在形態を推定 / `DegenerationPressure(form, regime)` で各形態の腐落圧力算出 / `NextForm(form)` で腐落先を返す / `IsCorrupt(form)` で良形態/腐落形態を区別
- 接続：`DynastyRules.Regime`（腐敗値→腐落圧力に寄与）×`Polity.consent`（民主/衆愚の閾値）×`ConstitutionRules`（Wave2）の推定ソース

#### POLY 混合政体の安定指数（Monarchic/Aristocratic/Democratic 成分の混合比→耐腐落）
- **`MixedConstitutionRules`**（pure logic, test-first）：三要素の比率（0..1 で各成分・合計1.0）を入力 / `MixIndex` = 三成分のシャノン情報量ベースの混合度（0=単一支配、1=完全均等）/ `DegenerationResistance(mixIndex)` = 腐落圧力への乗算係数（高混合→低乗算→腐落遅延） / `RomeProto` = 執政官1/3・元老院1/3・民会1/3 の参照値
- 接続：`AnacyclosisRules`（POLY-1）×`SeparationOfPowersRules`（Wave2・分立の有無を成分推定に利用）×`FactionStateRules.Stability`（混合指数→安定度へ寄与）

### ★★ 高（運命と制度・星系間連鎖）

#### POLY 運命耐性（制度品質 ×Tyche = イベント効果の変調）
- **`TycheRules`**（pure logic, test-first）：`InstitutionalResilience(factionState, mixIndex)` = 安定度・混合指数・正統性から 0..1 の耐性スコアを算出 / `ModifyEventSeverity(severity, resilience)` = 高耐性→悪イベントの影響を減衰・好イベントを増幅 / `ModifyEventSeverity` は乗算係数で基準効果は非破壊
- 接続：`EventEngine`/`EventRules`（POLY-3 の係数を `ApplyChoice` 時に乗算）×`FactionStateRules`（耐性の入力源）×`MixedConstitutionRules`（POLY-2 の `mixIndex` を入力）

#### POLY 普遍史の因果波及（星系間の事件連鎖）
- **`UniversalHistoryRules`**（pure logic, test-first）：`RippleStrength(eventType)` = 事件の種類（決定的敗北/政変/大規模叛乱など）に基づく波及強度 / `InstabilityRipple(epicenterId, galaxyMap, ownerMap, strength)` = 起点から BFS でグラフ距離に反比例する安定度ペナルティを隣接星系へ返す / `DecayFactor(distance)` = 距離による減衰（距離1=0.6, 2=0.3, 3=0.1, 4以上=0）
- 接続：`GalaxyMap`（グラフ距離）×`GovernanceRules`/`Province`（安定度ペナルティの受け手）×`CampaignRules.Tick`（毎ターン波及を畳む）。`LogisticsRules` の物理連結とは別系統

### ★ 中（制度的記憶・世界観 lore）

#### POLY 歴史の教訓（実用的歴史観＝制度知識の蓄積）
- **`InstitutionalMemoryRules`** + **`InstitutionalMemory`**（pure logic, test-first）：`InstitutionalMemory`（`lessonCounts: Dictionary<CrisisType, int>`・直列化可）/ `LearnFromCrisis(memory, crisisType)` で危機種別の経験カウントを加算 / `LessonBonus(memory, crisisType)` = log スケールで上限付き係数（1回目大, 2回目中, 以降漸減・`MemoryParams` 調整可）/ `ApplyLesson(eventContext, memory)` を `TycheRules`（POLY-3）の resilience 入力に追加する副入力経路
- 接続：`GrowthRules`/`Growth`（個人成長と対称な組織学習）×`Organization`/`SuccessionRules`（組織の制度化＝記憶の持続性）×`EventEngine`（危機イベントが学習トリガー）

#### POLY（lore）政体論世界観の開示データ
- コード新設なし。`DisclosureLedger`（FND-4）への**lore データ入力**：
  - 断片「帝国はなぜ腐る運命にあるか」→ 真相「六政体の循環——いかなる良政体も腐落を逃れない」→ 予言「混合政体のみが循環を遅らせる」
- 接続：`DisclosureLedger` + `SampleDisclosures`（既存開示連鎖への追記）

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| ローマ共和制そのものの詳細実装（元老院議員数・民会投票手続き） | タイクン化回避。`GovernmentRegistry`/`Party`/`OfficeRules` が既にカバー。ポリュビオスの構造のみ活かす |
| 軍制の詳細（軍団編成・百人隊・マニプルス） | 艦隊編成 `OrderOfBattle`/`FleetRoster`/`CommandStaffRules` が既にカバー |
| 歴史記述スタイル・史料批判の方法論 | ゲームメカニクスに変換できない記述レベルの話 |
| 地政学的分析（地中海の形状→ローマの有利） | `LogisticsRules` が版図一体化でカバー。新規不要 |
| 三権分立そのもの | `SeparationOfPowersRules`（Wave2）が既にカバー。POLY-2 は「混合比」という補完のみ |

---

## 3. EPIC #POLY の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。既存政体ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #1441**。GitHub issue 起票済み（#1442, #1445, #1448, #1451, #1454, #1458）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **POLY-1** | #1442 | 六政体類型と腐落ベクトル（`AnacyclosisRules`・`RegimeForm` enum・六形態の循環） | `DynastyRules`/`Regime` × `Polity` × `ConstitutionRules`（Wave2） |
| **POLY-2** | #1445 | 混合政体の安定指数（`MixedConstitutionRules`・三成分混合比→腐落抵抗・シャノン混合度） | POLY-1 × `SeparationOfPowersRules`（Wave2）× `FactionStateRules` |
| **POLY-3** | #1448 | 運命耐性（`TycheRules`・制度品質×Tyche係数→`EventEngine` イベント効果変調） | `EventEngine`/`EventRules` × `FactionStateRules` × POLY-2 `mixIndex` |
| **POLY-4** | #1451 | 普遍史の因果波及（`UniversalHistoryRules`・星系間の事件連鎖・距離減衰） | `GalaxyMap` × `GovernanceRules`/`Province` × `CampaignRules.Tick` |
| **POLY-5** | #1454 | 歴史の教訓（`InstitutionalMemoryRules`/`InstitutionalMemory`・危機学習→制度知識蓄積） | `GrowthRules`（個人と対称）× `Organization`/`SuccessionRules` × `EventEngine` |
| **POLY-6** | #1458 | （lore）政体論世界観の開示データ（六政体循環・混合政体の知恵を `DisclosureLedger` へ） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`POLY-1 → POLY-2`（六政体＋混合指数＝最も固有で欠落の大きい signature・密結合）→ `POLY-3`（運命耐性＝POLY-2 の mixIndex を入力に使う）→ `POLY-4`（普遍史波及＝独立して着手可）→ `POLY-5`（歴史の教訓＝POLY-3 の resilience 入力に追加）→ `POLY-6`（lore データ＝コード不要・いつでも可）。

> いずれも既存政体・社会シミュを**後退させず接続**する additive 設計。Wave2 の `ConstitutionRules`/`SeparationOfPowersRules` が配線された後に最も威力を発揮する。
