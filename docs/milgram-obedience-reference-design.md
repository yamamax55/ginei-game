# ミルグラム『服従の心理』参考設計（EPIC #MILG）

> 参照元：Stanley Milgram "Obedience to Authority" (1974)。1961〜1963年に行われた「服従実験」とその解析。
> 権威者の命令に従い、普通の市民が他者を傷つけ続ける——「状況の力」が個人の道徳判断を圧倒する構造。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略）にとって**役に立つ視点**だけを抽出し、EPIC `#MILG` として issue 化する提案。
> **著作権注意：固有名・文章・実験手続きの具体的記述は流用せず、社会心理学的メカニクス／世界観の構造パターンのみを参考にする。**

---

## 0. なぜ「服従の心理」が本システムに役立つか

当プロジェクトは服従・権威・抵抗の**純ロジック層を既に保有**している（[CLAUDE.md] 参照）：

| 既存（忠誠・権威・抵抗） | カバー範囲 |
|---|---|
| `LoyaltyRules`/`BattleAllegianceRules`（#817） | 忠誠・調略・寝返りカスケード（ResolveCascade）・静観退き |
| `ConsentRules`/`Polity`（#836） | 権力は借り物・非協力ボイコット・Withdraw |
| SOL-4（#414） | 責任の転嫁・道徳のブレーキ解除・服従度スコア（兵士化EPIC #410 子） |
| `ThoughtlessnessRules`/`BanalityState`（BNAL-1 #1530） | 悪の凡庸性・hierarchyDepth×complianceNorm→moralAgencyFactor |
| `AccountabilityChain`/`WarCrimesRules`（BNAL-4 #1536） | 組織犯罪の責任連鎖・戦後裁判 |
| `Organization`/`SuccessionRules`（#812/#814） | カリスマの日常化・組織継承・制度化度 |
| `OfficeRules`/`GovernmentRegistry`（GOV-1 #142） | 役職・任命・権限の制度構造 |
| `CivilianControlRules`（GOV-4 #145） | 文民統制・クーデターリスク |

**しかし、これらは「服従の状態と帰結」を扱っており**、ミルグラムが固有に解明した以下が**欠けている**：

| ミルグラムが固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **段階的エスカレーション（foot-in-the-door）** | SOL-4の「服従度」は現在の状態変数。**どのように段階的に服従が形成されるか**の動態（小さな服従の累積がコミットメントを固め、撤退コストを上げる過程）が無い |
| **近接性・可視性と服従率** | `LoyaltyRules` は被害者の距離を考慮しない。**被害が視野外（遠距離爆撃・軌道砲撃）では心理コストが下がり従いやすくなる**という非対称が無い |
| **連帯効果（先行抵抗者が抵抗を解放）** | `ResolveCascade` は寝返りの伝播を扱う。しかし**先行して抵抗した者の存在が、孤独な抵抗者の閾値を劇的に下げる**という連帯の解放効果が無い |
| **権威の正当性源泉の構造** | `Organization.institutionalization` は組織の制度化度。しかし「**どの制度的要素**（制服/役職/場所/専門資格）が権威に正当性を与え服従強度を決めるか」の解剖が無い |

**結論**：ミルグラムは当プロジェクトの服従論に**「服従の生成過程」という動態視点**と、
①段階的エスカレーション ②近接性の非対称 ③連帯による抵抗解放 ④権威正当性の構造
という4つの欠落軸を与える。
**軍令連鎖・残虐命令・抵抗運動の臨界点**に最も効くテクスチャを供給し、既存の SOL-4・BNAL-1 と相補的に機能する（重複しない）。

---

## 1. 役に立つ視点（要約）

ミルグラムの知見を、**本システムに効く形**で1行ずつ：

1. **「服従は段階的に形成される」** ＝ 最初の小さな服従がコミットメントを固め、撤退コストを上げ、次の服従を容易にする（foot-in-the-door）。→ 軍令連鎖の最初の一歩が「命令に従う組織文化」を作る動態。
2. **「遠ければ従いやすい」** ＝ 被害者が視野外・実感の外にあるほど心理的抵抗が薄れる。→ 遠距離爆撃・軌道砲撃・経済制裁は直接攻撃より「命令が通りやすい」戦場の非対称。
3. **「一人の抵抗者が全員を解放する」** ＝ 周囲に先行抵抗者が一人いるだけで服従率が激減する（孤独な服従 vs 連帯した抵抗）。→ `ResolveCascade` に「先行不服従者の存在が抵抗閾値を引き下げる」効果を追加。
4. **「権威の正当性は構造的に生産される」** ＝ 制服・役職・場所・専門資格が権威に「正当性の外套」を纏わせ、服従を道徳的に問題なく感じさせる。→ 制度化度・階級差・役職権限が「従わせる力」の源泉になる。
5. **「状況が人を作る（性格論の誤り）」** ＝ 服従するのは「残虐な人格」でなく「服従しやすい状況」にある普通の人間。→ 帝国の腐敗・独裁化が「残虐な艦隊将官」を産む構造的説明。SOL-4・BNAL-1 の lore と接続。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**SOL-4・BNAL-1〜4 を作り直さない・置換しない**。MILG はそれらに**欠落動態を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・ミルグラムの signature）

#### MILG 段階的エスカレーション服従形成（`EscalationCommitmentRules`）

- **コミットメントの累積**：命令への服従が `escalationStep` として積み上がり、`commitmentBias(steps, decayRate)` で次の命令への抵抗を低下させる。
- **撤退コストの上昇**：服従した回数が多いほど「今さら止められない」認知的不協和が強まる（escalation）。
- **接続**：`LoyaltyRules.ResolveStance` の入力に `ObedienceBonus(escalation)` を渡す。SOL-4の「服従度」は状態変数、MILG-1は**どう形成されるか**の動態（直交）。
- **純ロジック test-first**。

#### MILG 権威の正当性源泉（`AuthorityLegitimacyRules`）

- **権威正当性の構造**：制度化度（`Organization.institutionalization`）× 階級差（`rankDistance`）× 資格/役職威信（`credentialPrestige`）→ `commandComplianceBonus`。
- **正当性が高い権威ほど服従率が上がる**（制服・役職・場所が権威を「自然なもの」にする）。
- **接続**：`OfficeRules`/`Organization` を読取専用で参照し、MILG-1の `escalationStep` に乗算する基礎値を提供。
- **純ロジック test-first**。

### ★★ 高（服従構造の重要な軸）

#### MILG 近接性・可視性係数（`ObedienceProximityRules`）

- **VisibilityFactor**：被害者との物理的距離・視認性（`VisibilityType{直視,同室,隣室,遠距離,軌道外}`）→ 心理コスト（`moralCostFactor: 0..1`）。
- **近いほど従いにくく・遠いほど従いやすい**：遠距離爆撃/軌道砲撃は接触戦より心理コストが低い。
- **接続**：`GovernanceRules`（住民への被害の可視性→安定度への影響）× `LoyaltyRules`（命令への服従率修正子）。会戦の距離係数とも接続可（将来）。
- **純ロジック test-first**。

#### MILG 連帯効果・先行抵抗者の解放（`SolidarityResistanceRules`）

- **DefectorSpillover**：先行して服従拒否した者（静観/寝返り）の数 × 可視性 → 周囲の抵抗閾値の低下量（`spilloverStrength`）。
- **一人の抵抗者が孤独な服従を解放する**：`LoyaltyRules.ResolveCascade` の閾値計算に先行抵抗者カウントを追加。
- **接続**：`ResolveCascade`（既存・#817）を拡張する。孤立した抵抗と連帯した抵抗の非線形差を表現。
- **純ロジック test-first**。

### ★ 中（lore・世界観接続）

#### MILG （lore）「服従の解剖学」— DisclosureLedger 開示データ

- **開示の核**：「状況が人を作る」「一人の抵抗者が全員を解放する」「権威は正当性という衣を纏う」。
- **接続**：`DisclosureLedger`（FND-4）への lore データ入力。BNAL-5（悪の凡庸性）の相補面（帰結ではなく**生成メカニズム**側）。コード新設なし。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 責任の転嫁・道徳のブレーキ解除（服従度スコア） | **SOL-4 (#414) が既にカバー**。「服従度の効果」はそちらに任せる |
| 悪の凡庸性・moralAgencyFactor | **BNAL-1 (#1530) が既にカバー**。hierarchyDepth×complianceNorm→moralAgencyFactorはそちら |
| 組織犯罪の責任連鎖・戦後裁判 | **BNAL-4 (#1536) が既にカバー** |
| 実験の個人心理・被験者の葛藤スコア | タイクン化（マイクロ操作）になる。集団・組織レベルの係数のみ |
| 物理的な拘束・強制（電気ショック等の固有設定） | 著作権・倫理の観点から不使用 |
| 独立した「服従度」属性の新設 | SOL-4が既に持つ。重複して新設しない |

---

## 3. EPIC #MILG の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 戦略/会戦配線。
> SOL-4・BNAL-1〜4 は**接続のみ・重複新設しない**。
> **著作権注意：固有名・文章・実験手続きの具体的記述は不使用、構造パターンのみ参考。**

> **EPIC = #1845**。GitHub issue 起票済み（#1851〜#1864）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **MILG-1** | #1851 | `EscalationCommitmentRules` — 段階的エスカレーション服従形成（foot-in-the-door・commitmentBias・escalationStep累積） | `LoyaltyRules.ResolveStance` への入力 `ObedienceBonus`。SOL-4の「服従度状態」の形成過程 |
| **MILG-2** | #1856 | `AuthorityLegitimacyRules` — 権威の正当性源泉（制度化度×階級差×資格威信→commandComplianceBonus） | `Organization.institutionalization`×`OfficeRules.requiredTier` 読取。MILG-1基礎値 |
| **MILG-3** | #1858 | `ObedienceProximityRules` — 近接性・可視性係数（VisibilityType・moralCostFactor・遠距離砲撃の心理コスト低下） | `GovernanceRules`×`LoyaltyRules` 修正子。会戦距離係数（将来） |
| **MILG-4** | #1862 | `SolidarityResistanceRules` — 連帯効果・先行抵抗者（DefectorSpillover・resistanceTipping・ResolveCascade拡張） | `LoyaltyRules.ResolveCascade`（#817）拡張 |
| **MILG-5** | #1864 | （lore）「服従の解剖学」DisclosureLedger 開示データ — 段階エスカレーション・連帯・権威の正当性（コード新設なし） | `DisclosureLedger`（FND-4）。BNAL-5の相補面（生成メカニズム側） |

### 推奨着手順

`MILG-2`（権威正当性の基礎値を確立）→ `MILG-1`（エスカレーション＝最も固有な signature）→ `MILG-3`（近接性＝戦場に直接効く）→ `MILG-4`（連帯効果＝ResolveCascade拡張）→ `MILG-5`（lore）。

> SOL-4（#414）・BNAL-1（#1530）・`LoyaltyRules.ResolveCascade`（#817）は**接続のみ・後退させない**（additive設計）。
