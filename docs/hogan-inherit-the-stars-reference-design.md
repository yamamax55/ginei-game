# ホーガン『星を継ぐもの』参考設計（EPIC #IHER）

> 参照元：ジェイムズ・P・ホーガン『星を継ぐもの』(1977)。月面で発見された5万年前の宇宙服の死体「チャーリー」。
> 互いに無関係な複数の科学的証拠が積み重なり、思いもよらない真実が解き明かされる——**謎→仮説→検証→反証→再仮説**という科学的推理の連鎖。
> 本ドキュメントは、当プロジェクト（Ginei＝星間国家戦略＋すでに大規模な開示エンジン/研究ルール）にとって**役に立つ視点**だけを抽出し、EPIC `#IHER` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**メカニクス／世界観の構造パターンのみ**を参考にする。

---

## 0. なぜ「星を継ぐもの」が本システムに役立つか

### 既存（カバー範囲）

| 既存（類似・関連） | カバー範囲 |
|---|---|
| `DisclosureLedger` / `DisclosureRules` (FND-4 #495) | 秘史開示チェーン（`prerequisites` + `condition` → `TryReveal` → 連鎖） |
| `ResearchRules` / `ResearchProject` (#123-127) | 研究産出・政体偏り `IdeologyBias` |
| `EventEngine` / `GameEventDef` (#116) | 条件発火→効果の離散イベント |
| `ParadigmRules` / `ParadigmCommunityRules` (KUHN-1/5 #1918) | パラダイム体制・コミュニティ合意形成（証拠強度×開放性→受容率） |
| `EspionageRules` / `SpyNetwork` | 諜報・情報収集 |
| `ColonizationRules` / `StarSystem` | 星系の居住可能性・入植 |
| `GovernanceRules` / `Province` (#109) | 内政・安定度 |

### 欠落軸（星を継ぐものが固有に持つ視点 × 当プロジェクトでの欠落）

| 星を継ぐものが固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **証拠断片の重みと信頼性**＝複数の独立した証拠が収束するほど結論の信頼性が上がる | `DisclosureLedger.condition` は真偽二値の条件式。**証拠の重み × 発見源の独立性 → 臨界で開示**という量的蓄積モデルが無い |
| **多分野収束ボーナス**＝生物学・地質学・天文学が同じ結論を指すとき、単一分野より確度が跳ね上がる | `ResearchRules` は研究フィールド別の産出を扱うが、**異なるフィールドからの証拠が収束するシナジー**が無い |
| **遺跡・遺物という物理的証拠源**＝星系に実際に残る先行文明の痕跡（段階的調査・解読） | `StarSystem` には居住可能性・所有者があるが**考古学的遺跡**（調査フェーズ→解読→証拠生成）の概念が無い |
| **消滅文明の記録**＝かつて存在した（が今はない）文明の痕跡が現在の銀河地図と研究に影響する | `FactionData` は生きている勢力のみ。**絶滅した先行文明**（版図・技術水準・滅亡原因→現在への波及）の型が無い |
| **誤仮説への投資コスト**＝途中の仮説が覆されると、その仮説に投資した資源・計画が損失になる | 研究産出や開示は一方向。**信じていた仮説が証拠で覆ったときの更新コスト**（組織の慣性×コミット量）が無い |

**結論**：星を継ぐものは当プロジェクトの開示エンジン(FND-4)と研究ルール(#123)に**「証拠の物理的重み」という次元**を与える。既存の `DisclosureLedger`（条件→開示）は*ゲートの開閉*を扱うが、IHER は*どれだけ証拠が積み上がったか*という量的プロセスを足す。特に **①証拠断片の収束蓄積 ②遺跡という物理的証拠源 ③消滅文明の遺産 ④誤仮説の更新コスト**という4欠落軸が、**秘史解明の醍醐味**をゲームロジックに変換する。クーン#1918（パラダイム体制・コミュニティ合意）とは相補的＝IHERは証拠収集、KUHNはその社会的受容。

---

## 1. 役に立つ視点（要約）

星を継ぐものの世界観を、**本システムに効く形**で1行ずつ：

1. **証拠は重さを持つ**。単一証拠より複数の独立した証拠が積み重なるほど結論が確実になる。→ `DisclosureLedger.condition` の真偽二値に**証拠蓄積の量的モデル**を足す（IHER-1）。
2. **分野をまたいだ収束が最強の証拠**。生物学・地質学・天文学が同じ結論を指すとき、単一専門より強い。→ `ResearchRules.ResearchField` の異分野シナジー（IHER-1 `ConvergenceRules`）。
3. **遺跡は証拠の泉**。星系に残る先行文明の痕跡を調査・解読することで証拠断片が生まれる。→ `StarSystem` に考古遺跡の概念（IHER-2 `Relic`）を追加。
4. **消滅した文明が現在を規定する**。今はない文明の技術・版図が、現在の研究と銀河地理に痕跡を残す。→ `ExtinctCivilization`（IHER-3）が `GalaxyMap` と `ResearchRules` に接続。
5. **間違った仮説への投資は痛い**。仮説が覆されると、それを信じていた勢力は計画と資源を失う。→ `BeliefState`/`BeliefRules`（IHER-4）が組織の慣性と更新コストを定式化。
6. **謎が解けるとき、世界の見え方が変わる**。証拠が臨界を超えた瞬間のゲシュタルト転換。→ `DisclosureLedger.TryReveal` の連鎖（既存）に**証拠臨界**から発火するルートを接続（IHER-1）。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`DisclosureLedger`/`ResearchRules`/`EventEngine`/`ParadigmRules`(KUHN) を作り直さない**。IHER はそれらに**欠落軸を足し、接続する**だけ（additive）。タイクン化回避＝遺跡マイクロ管理なし・高位の証拠蓄積のみ。

### ★★★ 最優先（真の欠落・星を継ぐものの signature）

#### IHER-1 証拠断片と収束信頼性（`EvidenceFragment` / `EvidenceBody` / `EvidenceRules` / `ConvergenceRules`）

- **`EvidenceFragment`**（純データ）：`(topic, sourceField, credibility, weight)`。単一証拠アイテム。`sourceField`=`ResearchField` に対応（宇宙物理/生物学/地質学…）。
- **`EvidenceBody`**：`List<EvidenceFragment>` のトピック別コレクション。
- **`EvidenceRules`**（static・純ロジック・test-first）：
  - `TotalCredibility(body)` ＝ Σ(weight × credibility)
  - `IsConclusive(body, threshold)` → bool（開示トリガー）
  - `FieldDiversity(body)` → 発見源フィールド数
  - `ConvergenceMultiplier(diversity)` → 異分野が揃うほど信頼性を上乗せ（例: 3分野以上で×1.5）
  - `EffectiveCredibility(body)` ＝ `TotalCredibility` × `ConvergenceMultiplier`（実効信頼性・基準非破壊）
- 接続：`DisclosureLedger` の `condition` として `EvidenceRules.IsConclusive` を使用→**証拠の量的蓄積が開示を駆動**する新ルート。KUHN-5 `ParadigmCommunityRules`（社会的受容）とは別層（IHERは物理証拠の蓄積、KUHNは合意形成）。

#### IHER-2 遺跡・遺物の段階調査（`Relic` / `RelicRules`）

- **`Relic`**（`[Serializable]` 純データ）：`systemId`/`linkedCivId`(消滅文明ID・任意)/`surveyPhase`(0=未発見/1=検知/2=調査/3=解読)/`surveyProgress`(0..1)/`evidenceTopics`(解読後に生成する EvidenceFragment のトピック一覧)/`decoded`。
- **`RelicRules`**（static・純ロジック・test-first）：
  - `SurveyTick(relic, surveyPower, dt)` → 進捗加算・フェーズ遷移
  - `CanDecode(relic)` → `surveyPhase == 3`
  - `GenerateEvidence(relic)` → `List<EvidenceFragment>`（解読後に証拠断片を生成）
  - `ResearchBonus(relic)` → 関連`ResearchField`への産出ボーナス（期間限定）
- 接続：`StarSystem`（`relics: List<Relic>`を追加）× `ResearchRules`（`SurveyPower`は研究産出を流用）× IHER-1（解読→`EvidenceFragment`生成）。調査は `CalendarDispatcher`（TIME-6）の日次Tickで進行→`NotificationCenter`へ完了通知（NOTIF-1）。

### ★★ 高（欠落軸を補完・既存に接続）

#### IHER-3 消滅文明の記録（`ExtinctCivilization` / `ExtinctCivRules`）

- **`ExtinctCivilization`**（`[Serializable]` 純データ）：`id`/`civName`/`territorySystemIds`(List<string>・占有していた星系群)/`peakTechTier`(技術水準0..10)/`extinctionEra`(game-seconds)/`extinctionCause`(enum `{戦争,環境,不明}`)。
- **`ExtinctCivRules`**（static・純ロジック・test-first）：
  - `TechHeritage(ec, researchField)` → float（その分野の研究産出倍率・遺産文明の技術水準が高いほど大）
  - `GeoFootprint(ec, map)` → IEnumerable<StarSystem>（版図に属した星系一覧・所有権主張や遺跡配置に使用）
  - `ExtinctionCauseEffect(ec, faction)` → 文明が戦争で滅んだなら軍事諜報にボーナス・環境なら内政安定度に警告など（実効値パターン・基準非破壊）
- 接続：`GalaxyMap`（版図→星系の文化的背景）× `ResearchRules`（遺産ボーナス）× `DisclosureLedger`（消滅文明の真実が秘史として開示される）× IHER-2（版図星系に Relic を自動配置する初期化）。

#### IHER-4 仮説と誤謬コスト（`BeliefState` / `BeliefRules`）

- **`BeliefState`**（`[Serializable]` 純データ）：`factionId`/`topic`/`hypothesis`(string・現在信じている仮説ラベル)/`commitment`(0..1・投資した資源/計画の深さ)/`locked`(反証後は寝返り不可・組織慣性)。
- **`BeliefRules`**（static・純ロジック・test-first）：
  - `IsRefuted(belief, evidenceBody)` → bool（EvidenceBodyが仮説と矛盾するか）
  - `UpdateCost(belief)` → float（`commitment` 比例の更新コスト＝研究産出減/組織混乱期間）
  - `Update(belief, newHypothesis)` → 仮説更新＋`commitment` リセット（基準非破壊）
  - `ShouldLock(belief)` → `commitment > 閾値` かつ `Organization.institutionalization`が高い勢力は`locked`になりやすい（省益#158・制度化#812 接続）
- 接続：IHER-1 `EvidenceRules`（反証判定）× `Organization`（制度化→解除困難）× `EventEngine`（仮説反証イベント発火）× `GovernanceRules`（安定度への影響）。基準値・研究投資は非破壊。

### ★ 中（世界観lore）

#### IHER-5 （lore）世界観の開示データ

- 「現在の銀河は先行文明の廃墟の上に立っている」「ある星系の小惑星帯はかつて惑星だった」「人類（または一勢力）は移民の子孫かもしれない」「宇宙には知的種族がかつていたが今は孤独である」。
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への**loreデータ入力**のみ。IHER-1〜4 の証拠蓄積が条件を満たすと開示が連鎖する。CCX-6（世界観codex退避）の方針に一貫。

### ❌ 不採用（重複・既存で十分・タイクン化）

| 不採用 | 理由 |
|---|---|
| パラダイム転換エンジン（通常科学→異常→危機） | **KUHN-1/2 `ParadigmRules`/`ResolveShift` がカバー**。IHER は証拠収集側のみ担当 |
| 科学者コミュニティの合意形成 | **KUHN-5 `ParadigmCommunityRules` がカバー**（証拠強度×開放性→受容率）|
| 遺跡の3D探索・個別マイクロ管理 | タイクン化。高位の調査フェーズと証拠生成のみ（発掘の詳細を扱わない） |
| 固有の宇宙人種族・種族間外交の実装 | **`DiplomacyRules`/`FactionData`/`FactionRelations` で対応可**。種族特化システムを新設しない |
| 文明分岐・種族特化の能力ツリー | タイクン化。`AdmiralData`/`GrowthRules` への係数で十分 |
| 地質・生物・天文データの詳細な自然科学モデル | スコープ外。証拠断片のソースフィールドラベルで代替 |
| 遺物の売買・経済市場 | **`MarketRules`(#179)/商社FRM#1022 で対応可**。遺物単独の市場を新設しない |

---

## 3. EPIC #IHER の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。既存開示・研究・イベントエンジンは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #2396**（IHER-1〜5 = #2397〜#2401）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **IHER-1** | #2397 | 証拠断片と収束信頼性（`EvidenceFragment`/`EvidenceBody`/`EvidenceRules`/`ConvergenceRules`） | 新 `EvidenceRules`。`DisclosureLedger.condition` の量的蓄積版。異分野収束ボーナス |
| **IHER-2** | #2398 | 遺跡・遺物の段階調査（`Relic`/`RelicRules`：検知→調査→解読→証拠断片生成） | IHER-1 × `StarSystem` × `ResearchRules` × `CalendarDispatcher` |
| **IHER-3** | #2399 | 消滅文明の記録（`ExtinctCivilization`/`ExtinctCivRules`：版図・技術遺産・滅亡原因） | `GalaxyMap` × `ResearchRules` × `DisclosureLedger` × IHER-2初期配置 |
| **IHER-4** | #2400 | 仮説と誤謬コスト（`BeliefState`/`BeliefRules`：反証→更新コスト×コミット量） | IHER-1 × `Organization` × `EventEngine` × `GovernanceRules` |
| **IHER-5** | #2401 | （lore）世界観の開示データ（先行文明/移民史/宇宙の孤独） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順

`IHER-1`（証拠蓄積モデル＝最も基盤・他から参照される）→ `IHER-2`（遺跡＝証拠の物理的源泉）→ `IHER-3`（消滅文明＝遺跡の文脈・研究遺産）→ `IHER-4`（誤仮説コスト＝証拠が揃ってから有効）→ `IHER-5`（lore＝データ入力のみ）。

> いずれも既存 `DisclosureLedger`/`ResearchRules`/`ParadigmRules`(KUHN) を**後退させず接続**する additive 設計。IHER は証拠物理層・KUHN は社会的受容層という役割分担。
