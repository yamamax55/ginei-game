# ウェーバー『職業としての政治』参考設計（EPIC #WEBR）

> 参照元：マックス・ウェーバー著『職業としての政治』（Politik als Beruf, 1919）。
> ミュンヘン大学での講義をもとにした政治社会学の古典。**支配の三類型・心情倫理 vs 責任倫理・政治の職業化・ツェーザリズム**という4つの概念が20世紀政治学の礎となった。
> 本ドキュメントは、当プロジェクト（Ginei＝帝国 vs 同盟という権威主義と民主制の対立を核に持つ銀河戦略）にとって**役に立つ視点**だけを抽出し、EPIC `WEBR` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**政治社会メカニクス／世界観の構造パターンのみ**を参考にする。

---

## 0. なぜ「職業としての政治」が本システムに役立つか

当プロジェクトは政治・支配の**動態ロジックを大量に保有**している（[CLAUDE.md] 参照）：

| 既存モジュール | カバー範囲 |
|---|---|
| `Organization`/`SuccessionRules`（#812） | カリスマの日常化・制度化・後継危機 |
| `DynastyRules`/`Regime`（#867） | 正統性サイクル・腐敗・天命喪失・易姓革命 |
| `ConsentRules`/`Polity`（#836） | 権力は借り物・協力と統治不能 |
| `CivilianControlRules`（#145） | 文民統制・クーデターリスク |
| `PartyRules`/`Party`（#159） | 政党・派閥・最小選挙 |
| `LeadershipElectionRules`（#165） | 党首選出（自民党型・派閥推薦票） |
| `CareerPipelineRules`（LIFE-5/6/7） | 武/官/技の出自パイプライン・席次 |
| `PersonRules`/`Person` | 軍人/文民の役割・適材適所 |
| `MinistryRules`/`Ministry`（#158） | 省庁ツリー・省益・縦割り摩擦 |
| `OfficeRules`/`GovernmentRegistry`（GOV-1/3） | 役職・任命・提案権限 |
| `LoyaltyRules`/`Allegiance`（#817） | 忠誠・調略・旗幟の解決 |

**しかし、これらは「制度・権力・動員」の動態を個別に扱う**ものであり、ウェーバーが固有に描く以下が**欠けている**：

| ウェーバーが固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **支配の三類型の統一分類軸** | `Organization`#812はカリスマ→制度化の推移をモデル化。`DynastyRules`は正統性サイクルを扱う。だが**伝統的・カリスマ的・合法的の三者を統一した分類軸**として、現在の政体がどの型に属し、型ごとの**安定プロファイル・継承メカニズム・崩壊モード**が何かを一貫して解決する純ロジックが無い |
| **心情倫理 vs 責任倫理** | `PersonRules`は軍人/文民の役職適性を扱う。`LoyaltyRules`は忠誠解決を扱う。だが**指導者が「原則を守り結果は問わない（心情倫理）」か「結果に責任を持ち原則を折る（責任倫理）」か**という政治的意思決定の倫理軸が全く無い |
| **政治の職業化（召命型 vs 生業型）** | `CareerPipelineRules`は出自経路（士官学校/科挙/有力者/テクノクラート）を扱う。だが**「政治のために生きる（inner calling・理念先行）」vs「政治で食う（生業型・組織優先）」という動機軸**が無い。これが党機械の官僚化と政治腐敗の構造的原因をモデル化できない原因 |
| **ツェーザリズム（人民投票的指導者）** | `LeadershipElectionRules`（#165）は派閥推薦票×党員票の内部選挙（自民党型）をモデル化。だが**大衆への直接訴求によって党機械を迂回するカリスマ的指導者の出現**（ツェーザリズム）という別の選出メカニズムが無い。これは帝国のフェザーン政治や同盟の大衆政治家の動態に直結する |

**結論**：ウェーバーは当プロジェクトの支配・政党・指導者選出ロジックに**「正統性の統一分類軸」「政治倫理の意思決定軸」「党の官僚化メカニクス」「大衆動員型リーダーシップ」**という4つの欠落軸を与える。これらは既存の`Organization`/`DynastyRules`/`ConsentRules`/`LeadershipElectionRules`/`PartyRules`を**後退させずに接続する additive 設計**であり、帝国（伝統的/カリスマ的支配）vs 同盟（合法的/民主的支配）という Ginei の核をより精密に描写できる。

---

## 1. 役に立つ視点（要約）

ウェーバーの論考を、**本システムに効く形**で1行ずつ：

1. **支配の正統性には三類型あり、それぞれ崩壊モードが異なる**——伝統的支配は変化への硬直で崩れ、カリスマ的支配は後継危機で崩れ、合法的支配は官僚主義と正統性の空洞化で崩れる。→ 既存の`Organization`/`DynastyRules`/`ConsentRules`に**統一分類軸**を与え、現在の政体がどの型に属するかを判定し型別の危機モードを予測する。
2. **心情倫理の政治家は「原則」を守るが帰結に責任を持たない**——良い意図で悪い結果をもたらしても自己を正当化する。責任倫理の政治家は「帰結」を見て原則を折る——悪魔とも手を組む。→ 意思決定システムの**倫理軸**として機能し、指導者の行動パターンと政治的帰結を分岐させる。
3. **政治を職業にすると「政治のために生きる者」と「政治で食う者」に分かれる**——後者が多い党機械は組織存続を優先し腐敗する。→ CareerPipelineRulesに「召命型 vs 生業型」の動機軸を追加し党機械の官僚化をモデル化。
4. **大衆民主制はカリスマ的な演説者を選出する（ツェーザリズム）**——党内の慎重な政治家でなく、大衆に直接訴えるデマゴーグを選ぶ。→ LeadershipElectionRulesに人民投票的選出メカニズムを追加し、党機械 vs 大衆動員の緊張を表現。
5. **官僚制は合理化の極北であり「鉄の檻」となる**——合法的支配の帰結として省庁が人間を手段として扱い、政治的意志を実行できなくなる。→ MinistryRules（省益#158）に合法的支配の逆機能係数として接続。
6. **政治の本質は「暴力の独占に基づく倫理的責任」**——権力を持つ者は汚い手段を使っても結果に責任を負う。これが英雄との違い。→ 世界観lore。帝国の「鉄の掌」と同盟の「大義名分」の対比を深める。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`Organization`/`DynastyRules`/`ConsentRules`/`LeadershipElectionRules`/`PartyRules`/`CareerPipelineRules` を作り直さない**。WEBRはそれらに**欠落軸を足し接続する**だけ（additive）。

### ★★★ 最優先（ウェーバーの真の固有 signature）

#### WEBR 支配の三類型（`HerrschaftRules`/`HerrschaftType`）

- **`HerrschaftType` enum**：`{伝統的, カリスマ的, 合法的}`
  - **伝統的支配**（Traditional）：慣習・先例・世襲の権威。変化に抗う硬直性が高い。崩壊モード = 外部変化への適応失敗・他類型の浸食。
  - **カリスマ的支配**（Charismatic）：個人の非凡な資質への帰依。流動性・動員力が高い。崩壊モード = 後継者危機（Veralltäglichung 失敗）。`Organization`#812の「カリスマの日常化」の前段。
  - **合法的支配**（Legal-Rational）：規則・手続き・資格への従属。官僚制。崩壊モード = 正統性の空洞化（手続きは正しいが意志がない）・鉄の檻。`ConsentRules`#836の「権力は借り物」の構造的原因。
- **`HerrschaftRules`**（static・純ロジック・test-first）：
  - `Classify(polity, regime, organization)` → `HerrschaftType`（現在の政体が三類型のどれに最も近いか推定）
  - `CrisisMode(type)` → `HerrschaftCrisis`（崩壊モードの予測：硬直崩壊/後継危機/空洞化）
  - `StabilityProfile(type)` → `HerrschaftProfile`（安定性の特性値：可塑性/動員力/官僚効率）
  - `RoutinizationPressure(type, organization)` → float（カリスマ型は Organization.charisma が高いほど制度化圧力）
  - `LegitimacyDrainRate(type, regime)` → float（正統性低下速度の型別係数）
- 接続：`Organization.charisma/institutionalization`（カリスマ型判定）× `DynastyRules.Regime.legitimacy`（正統性）× `ConsentRules.Polity.cooperation`（合法型の基盤）× `FactionStateRules.IsCollapsing`（崩壊モード検出）。
- **既存を後退させない**：`Organization`#812の Veralltäglichung は「カリスマ型→合法型の遷移」。WEBRは遷移の前後を分類するだけで遷移メカニズムには触れない。

#### WEBR 心情倫理 vs 責任倫理（`PoliticalEthicsRules`）

- **政治的意思決定の倫理軸**：
  - **心情倫理（Gesinnungsethik）**：「原則が正しければ帰結は問わない」。道徳的純粋さを守る。失敗しても「時代が悪い」「民が悪い」と帰結を他に帰す。
  - **責任倫理（Verantwortungsethik）**：「帰結に責任を持つ」。原則を折ってでも最善の結果を目指す。「悪魔とも手を組む」覚悟。
- **`PoliticalEthicsType` enum**：`{心情倫理, 責任倫理}`
- **`PoliticalEthicsRules`**（static・純ロジック・test-first）：
  - `DecisionBias(ethicsType)` → float（心情倫理は高道徳コスト行動を忌避・責任倫理は低コスト行動を許容）
  - `LegitimacyCostOnCompromise(ethicsType)` → float（原則を折った時の支持コスト：心情倫理者は大、責任倫理者は小）
  - `OutcomeResponsibility(ethicsType, outcome)` → float（悪結果を指導者が負う割合：責任倫理>心情倫理）
  - `IsParadigmaticLeader(person, ethicsType)` → bool（個人の意思決定パターンから倫理型を推定）
  - `ConvictionConflict(ethicsType, situation)` → bool（原則と結果が真に衝突する状況か）
- 接続：`AdmiralData`/`Person`（指導者の倫理型フィールド追加）× イベントエンジン`EventRules`（倫理軸によって選択肢の評価が変わる）× `ConsentRules.cooperation`（心情倫理指導者は原則を守り支持が高いが適応遅延）× `LoyaltyRules.Allegiance`（責任倫理指導者はより現実的な交渉が可能）。
- **完全に新規**：既存のいずれの純ロジックにも倫理軸が無い。

### ★★ 高（政治腐敗・指導者選出に動機層を追加）

#### WEBR 政治の職業化（`PoliticalVocationRules`）

- **動機軸**：「政治のために生きる」召命型 vs「政治で食う」生業型。
  - **召命型（Vocation）**：理念・使命感・政治そのものへの献身。不利でも理念を主張する。任期を超えて国家を考える。腐敗しにくいが現実適応に遅れる。
  - **生業型（Livelihood）**：政治を職業として利益を得る。組織存続・再選・収入が優先。適応力は高いが腐敗に傾く。党機械を支える。
- **`VocationOrientation` enum**：`{召命型, 生業型}`
- **`PoliticalVocationRules`**（static・純ロジック・test-first）：
  - `OrientationOf(person, partySize, careerTrack)` → `VocationOrientation`（政党規模・出自から傾向推定）
  - `PartyBureaucratizationRate(party)` → float（生業型議員比率が高いほど党が官僚化：組織存続＞理念）
  - `CorruptionSusceptibility(orientation, office)` → float（生業型は高い）
  - `IdeologicalDrift(party, dt)` → float（官僚化した党ほど理念が希薄化し選挙工学に傾く）
  - `ProfessionalPoliticianShare(party)` → float（生業型比率）
  - `MachinePartyIndex(party)` → float（官僚化度・派閥投票×生業型×組織資源から複合）
- 接続：`CareerPipelineRules`（出自が傾向に影響：科挙→生業型傾向/有力者→召命型or生業型）× `PartyRules.Party`（党の官僚化係数として機能）× `LeadershipElectionRules`（生業型議員が多いほど WEBR-4 ツェーザリズムへの反動が強まる）× `DynastyRules.Regime.corruption`（官僚化した党は腐敗加速）。

#### WEBR ツェーザリズム（`PlebiscitaryRules`）

- **選出メカニズムの拡張**：`LeadershipElectionRules`（#165）の内部選挙（派閥票×党員票）に加え、**大衆直接動員**による指導者出現を追加。
  - **ツェーザリスト指導者**：演説・カリスマで大衆支持を直接獲得し、党機械の内部選挙を迂回または圧倒する。
  - **党機械 vs 大衆動員の緊張**：内部選挙では党機械が強いが、外部世論が高ければツェーザリストが強引に候補化される。
- **`PlebisciteResult`**（struct）：`massApproval`（大衆支持率）/ `partyApproval`（党内支持率）/ `hasTwist`（divergence）/ `isCaesarist`（mass > threshold で真）
- **`PlebiscitaryRules`**（static・純ロジック・test-first）：
  - `MassApproval(person, communityHope, regime)` → float（演説・カリスマ・時代の希望から大衆支持率算出。`Community.hope`#852が高い時代はカリスマが輝きやすい）
  - `IsCaesarist(person, threshold)` → bool（大衆支持が党員支持を大きく超えるか）
  - `CaesaristOverride(massApproval, partyApproval)` → bool（大衆支持が圧倒的な場合は党機械を迂回して当選できるか）
  - `DemagogyRisk(massApproval, ethicsType)` → float（ツェーザリスト×心情倫理が重なると衆愚政リスク・ThucydidesRules/TOCQと接続）
  - `AccountabilityGap(isCaesarist)` → float（ツェーザリストは党機械の規律を欠き暴走しやすい）
  - `Elect(partyResult, plebisciteResult)` → `LeaderElection`（内部選挙+人民投票の合成で最終当選者決定）
- 接続：`LeadershipElectionRules`（拡張：内部×外部の合成）× `Community.hope`（希望が高い時代にツェーザリズム出現しやすい）× `HopeRules.末人`（末人が多い時代に逆にツェーザリストが現れる逆転構造）× `PoliticalEthicsRules`（デマゴーグ危険度は心情倫理×大衆動員で最大）× `DiplomacyRules`（ツェーザリスト政府は外交が不安定）。

### ★ 中（世界観lore・コード新設なし）

#### WEBR lore 支配の三類型・責任倫理・鉄の檻

- 「カリスマの死後、情熱は制度の中で冷める」「原則だけで政治をやると罪のない者を傷つける」「官僚機械は人間を手段として扱い、その目的を忘れる」「暴力の独占を手にした者だけが政治的責任を問われる」
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への**lore データ入力**。帝国の正統性喪失（合法的支配の空洞化）や英雄の死後（カリスマ日常化）のイベント素材。

### ❌ 不採用（重複・既存でカバー済み）

| 不採用 | 理由 |
|---|---|
| カリスマの日常化（Veralltäglichung）そのもの | **`Organization`/`SuccessionRules`（#812）が既にカバー**。HerrschaftRulesは前段の分類のみ |
| 易姓革命・正統性の失墜 | **`DynastyRules`/`Regime`（#867）がカバー済み** |
| 統治への同意・非協力 | **`ConsentRules`/`Polity`（#836）がカバー済み** |
| 文民統制・クーデター | **`CivilianControlRules`（#145）がカバー済み** |
| 省庁の縦割り・省益 | **`MinistryRules`/`Ministry`（#158）がカバー済み**。「鉄の檻」は合法的支配の係数としてloreで扱う |
| 選挙制度・議院内閣制そのもの | **`LeadershipElectionRules`（#165）/`PartyRules`（#159）がカバー済み**。PlebiscitaryRulesは拡張のみ |
| プロテスタント倫理と資本主義精神 | 別ウェーバー作品（プロ倫）のテーマ。バックログ「ウェーバー『プロ倫』＋官僚制論」で対応 |
| 社会科学方法論（理念型・価値自由） | ゲームメカニクス化困難・スコープ外 |

---

## 3. EPIC #WEBR の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。既存支配・政治ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**政治社会メカニクス/世界観構造のみ**参考。

> **EPIC = #1520**。GitHub issue 起票済み（#1525・#1528・#1531・#1533・#1534）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **WEBR-1** | #1525 | `HerrschaftRules`/`HerrschaftType` — 支配の三類型分類（伝統的/カリスマ的/合法的 × 安定プロファイル・崩壊モード） | `Organization.charisma`×`DynastyRules`×`ConsentRules`。純ロジック新設・EditModeテスト必須 |
| **WEBR-2** | #1528 | `PoliticalEthicsRules`/`PoliticalEthicsType` — 心情倫理 vs 責任倫理（政治的意思決定の倫理軸 × 帰結責任・原則コスト） | `AdmiralData`/`Person`（倫理型フィールド）×`EventRules`×`ConsentRules`。純ロジック新設・EditModeテスト必須 |
| **WEBR-3** | #1531 | `PoliticalVocationRules`/`VocationOrientation` — 政治の職業化（召命型 vs 生業型 × 党機械の官僚化・腐敗傾性） | `CareerPipelineRules`×`PartyRules`×`LeadershipElectionRules`。純ロジック新設・EditModeテスト必須 |
| **WEBR-4** | #1533 | `PlebiscitaryRules` — ツェーザリズムと人民投票的指導者（大衆直接動員 × `LeadershipElectionRules`の内部選挙との合成） | `LeadershipElectionRules`拡張×`Community.hope`×`PoliticalEthicsRules`×`DiplomacyRules`。純ロジック新設・EditModeテスト必須 |
| **WEBR-5** | #1534 | （lore）世界観開示データ — 支配の三類型・責任倫理・鉄の檻・政治の暗闘 | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`WEBR-1`（三類型分類＝ウェーバーの理論的核心。既存の Organization/DynastyRules/ConsentRules に統一軸を与える）→ `WEBR-2`（心情倫理 vs 責任倫理＝最もユニークな欠落軸。指導者の意思決定を二分する）→ `WEBR-3`（職業化＝WEBR-2の構造的原因。党機械の官僚化に動機層を与える）→ `WEBR-4`（ツェーザリズム＝WEBR-3の帰結としての大衆動員。LeadershipElectionRulesの拡張）→ `WEBR-5`（lore＝コード不要・いつでも可）。

> いずれも既存の政治・支配シミュレーション層を**後退させず接続**する additive 設計。`Organization`#812（カリスマの日常化）・`DynastyRules`#867（易姓革命）・`LeadershipElectionRules`#165（内部党選挙）に**ウェーバーの統一理論的背骨**を与える。帝国（伝統的/カリスマ的支配）vs 同盟（合法的/民主的支配）というGineiの核対立がより精密に描写できる。
