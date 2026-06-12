# トールキン『指輪物語』参考設計（EPIC #LOTR）

> 参照元：J.R.R.トールキン『指輪物語』(The Lord of the Rings)。中つ国の種族連合が「権力そのもの」と戦う叙事詩。
> 「指輪は持つ者を蝕む」「善意の支配は最悪の支配になりうる」「不死者の孤独と去り際の美学」——権力腐蝕・連合の脆さ・文明の尊厳ある退場を描く。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略＋既に巨大な政治/社会シミュ層）にとって**役に立つ視点**だけを抽出し、EPIC として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**権力腐蝕・連合力学・文化継承のメカニクス構造パターンのみ**を参考にする。

---

## 0. なぜ「指輪物語」が本システムに役立つか

当プロジェクトは権力・連合・衰退に関する**マクロ純ロジックを大量に保有**している（CLAUDE.md 参照）：

| 既存（カバー範囲） | カバー内容 |
|---|---|
| `DynastyRules`/`Regime`（#867） | 制度腐敗・天命喪失・易姓革命（王朝レベル） |
| `FactionState`/`FactionStateRules` | 国家状態の合成（正統性/腐敗/徳） |
| `LoyaltyRules`/`Allegiance`（#817） | 関ヶ原型旗幟・寝返りカスケード |
| `BalanceOfPowerRules`（SGZ-1 #1102） | 最強勢力への漸次的連衡（多極均衡圧力） |
| `EspionageRules`/`SpyNetwork` | 諜報収集・妨害工作・情報獲得 |
| `DeceptionRules`（SUN-1 #1125） | 偽情報/陽動で敵AIの行動を歪める（能動的欺瞞） |
| `Organization`/`SuccessionRules`（#812） | カリスマの日常化・継承・組織崩壊 |
| `PersonRules`/`Person`（#866） | 人物の役割適性・実効値パターン |
| `PledgeRules`（SGZ-3 #1102） | 個人間の盟誓・拘束力・離反ペナルティ |
| `DisclosureLedger`（FND-4） | 開示エンジン（lore データ入力） |

**しかし、これらは「制度レベルの腐敗」「漸次的連衡」「能動的欺瞞」であり、指輪物語が固有に描く以下が欠けている**：

| 指輪物語が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **権力そのものが個人を能動的に蝕む** | `DynastyRules.腐敗` は制度疲労（機関レベル）。**個人が権力/強力な役職を持つほど意志が侵食される累積腐蝕圧力**が無い |
| **実存的脅威が発動する緊急同盟** | `BalanceOfPowerRules`(SGZ) は「最強勢力への漸次的カウンターバランス」。**圧倒的外部脅威が伝統的敵対を一時凍結させる閾値トリガー型緊急連合**が無い |
| **同盟の亀裂点＝誘惑イベント** | `LoyaltyRules` は個人の旗幟解決。**連合構成員が敵の力を「自分の善い目的に使えるはず」と考えて離反する誘惑構造**が無い |
| **汚染された神託（被撃破情報ノード）** | `DeceptionRules`(SUN-1) は「こちらが敵を騙す」能動的欺瞞。**自軍の情報収集手段が乗っ取られ、見えているものが敵の見せたいものになる受動的汚染**が無い |
| **尊厳ある文化退場と遺産継承** | `DynastyRules.Revolution`/`Organization.崩壊` は暴力的・混沌的な崩壊。**文明が自覚的に退きながら価値観・制度を後継文明へ手渡す尊厳ある撤退**が無い |

**結論**：指輪物語は当プロジェクトの政治/社会シミュ層に、①**個人権力腐蝕**②**実存的緊急連合**③**連合亀裂の誘惑構造**④**情報ノードの汚染**⑤**尊厳ある文化遺産継承**という5つの欠落軸を与える。そして**銀英伝の核テーマ（「人間とは何か」「権力と意志の闘い」）に最も深く共鳴するテクスチャ**を供給する。

---

## 1. 役に立つ視点（要約）

指輪物語の世界観構造を、**本システムに効く形**で1行ずつ：

1. **「権力は持つ者を蝕む」＝権力腐蝕は外から来るのではなく内側から育つ**。指輪は悪を作るのではなく、既にある欲望を増幅する。→ `PersonRules` の個人能力に**腐蝕圧力**の次元を足し、長期権力保持が実効値を変質させる。
2. **善意の支配者が最悪の支配者になる**。サルマンは最初は善のために指輪を求めた。→ `ConsentRules`/`CoerciveStyleRules`(MKV) の抑圧モデルに**「理念→腐蝕→専制」のアーク**を供給。
3. **実存的脅威は伝統的敵対を凍結する**。エルフとドワーフが共に戦う理由は力の均衡ではなく「共通の絶滅危機」。→ `BalanceOfPowerRules`(SGZ) の漸次的連衡とは別の**閾値トリガー型緊急同盟**。
4. **連合は強力だが脆い**。最も強い味方が最も危険な離反者になりうる（力を自分の善の目的に使える、という誘惑）。→ `LoyaltyRules` に**連合特有の誘惑亀裂イベント**を追加。
5. **見ることと騙されることは表裏一体**。遠くを見るほど敵に見返される（パランティア効果）。→ `EspionageRules.SpyNetwork` に**汚染ノード・偽報フィード**の次元を追加（SUN-1の能動的欺瞞の**受動的裏面**）。
6. **去り際の美学＝何を残すかが問われる**。エルフが中つ国を去るとき、その文化・知識・精神は人間に手渡される。→ `DynastyRules`/`Organization` に**文明の尊厳ある撤退と遺産継承**を追加。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`DynastyRules`/`LoyaltyRules`/`EspionageRules`/SGZ-1/SUN-1 を作り直さない**。LOTR はそれらに**欠落軸を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・指輪物語の signature）

#### LOTR 個人権力腐蝕圧力（指輪型腐蝕アーク）

- **腐蝕圧力モデル**：個人（`Person`/`AdmiralData`）が高権限ポスト（元帥・統治者・宰相）を長期保持すると**腐蝕圧力**が累積する。圧力が意志値を超えると実効能力が変質（統率↑だが忠誠・合意への感受性↓）。
- 制度腐敗（`DynastyRules.腐敗`）と直交：組織が健全でも個人は腐る。個人が健全でも組織は腐る。
- 「善意で使おうとするほど速く腐る」＝`CombatModifiers` の能力倍率に腐蝕修正子を掛ける（基準値非破壊・実効値パターン）。
- 接続：`PersonRules.Effectiveness`×`FactionState.腐敗`×`DynastyRules`×`CombatModifiers`（#106）。新 `PersonalCorruptionRules`/`CorruptionArc`（純ロジック・test-first）。

#### LOTR 実存的緊急同盟（閾値トリガー型）

- **SGZ-1 との明確な差別化**：`BalanceOfPowerRules` は「最強勢力への漸次的Opinion修正」。実存的緊急同盟は「支配的脅威が閾値（全勢力の合計国力の50%超等）を超えたとき、通常は敵対する勢力ペアが**一時的に非敵対**になる」閾値トリガー型。
- 同盟の条件：共通脅威 + 緊急閾値超過 + 双方の正統性残存（すでに崩壊した勢力は参加できない）。
- 時限性：脅威が消えると関係は元の敵対に戻る（恒久的関係変化ではない）。
- 接続：`DiplomacyRules`×`FactionRelations`×`BalanceOfPowerRules`(SGZ-1)。新 `ExistentialCoalitionRules`（純ロジック・test-first）。

### ★★ 高（連合の内部力学・情報ノードの汚染）

#### LOTR 連合亀裂イベント（誘惑構造）

- 緊急連合の構成員に**誘惑イベント**が発火：「敵の力を自分の善い目的に使えるはずだ」という誘惑→ステルス離反。
- `EventEngine`（#116）の条件トリガーとして実装：構成員の `intrigue`/能力値 + 敵勢力の power 差 → 離反確率。
- 離反時は `LoyaltyRules` の寝返りカスケードと接続（連合一員の離反が他メンバーの動揺を誘発）。
- 接続：`EventEngine`×`LoyaltyRules`×`ExistentialCoalitionRules`(LOTR-2)×`BattleAllegianceRules`。

#### LOTR 汚染された神託（パランティア効果）

- **SUN-1 `DeceptionRules` との明確な差別化**：SUN-1 は「こちらが敵を欺く」能動的欺瞞。パランティア効果は「自軍の情報ノード（SpyNetwork）が乗っ取られ、取得情報が敵の見せたいものに差し替えられる」受動的汚染。
- 汚染ノードは表面上は正常に動作している（発覚しにくい）。汚染情報に基づく行動が裏目に出ることで間接検知。
- 汚染の開示：`DisclosureLedger` に「裏切りの発覚」エントリとして連動可。
- 接続：`EspionageRules.SpyNetwork`×SUN-1 `DeceptionRules`×`EventEngine`。新 `TaintedOracleRules`（純ロジック・test-first）。

### ★ 中（文化遺産の継承・lore）

#### LOTR 尊厳ある文化遺産継承（長い敗退）

- 衰退する文明が**自覚的に退きながら価値観・制度・知識を後継文明へ手渡す**。`DynastyRules.Revolution`（暴力的転換）`Organization.崩壊`（混沌的崩壊）とは異なる「第三の終わり方」。
- 遺産継承：衰退勢力の `FactionState.制度化` を後継勢力の `Organization.制度化` へ部分的に転移（制度化が高いほど継承率が高い）。
- 「去り際」の判断：`IsCollapsing` または長期衰退検知時に発火する意思決定イベント→ `DiplomacyRules.MakePeace` / 解体条約 / 保護国化の選択肢。
- 接続：`DynastyRules`×`Organization`/`SuccessionRules`×`DiplomacyRules`×`DisclosureLedger`。新 `CulturalLegacyRules`（純ロジック・test-first）。

#### LOTR（lore）世界観の開示データ

- 「権力は持てば蝕む」「善意の独裁は最悪になりうる」「去り際の美学＝何を残すか」「種族を超えた連帯の脆さと尊さ」。
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への**lore データ入力**。

### ❌ 不採用（重複・既存で十分・タイクン化回避）

| 不採用 | 理由 |
|---|---|
| 神話的英雄・予言・運命の必然性 | 決定論回避。`EventEngine` の確率的条件トリガーで十分 |
| 個人間の盟誓・友情の絆 | **SGZ-3 `PledgeRules` がカバー**。LOTR は連合レベルの亀裂に絞る |
| 地形/環境の障壁（カラドラス山など） | 空間戦術は会戦シーンでカバー済み。戦略マップの地形追加はスコープ外 |
| 指輪の具体的魔法効果（不可視化など） | 固有設定の流用になる。スキル効果は `AdmiralSkillRules`(#137) で十分 |
| 種族固有の能力値テーブル | タイクン化回避（種族別マイクロは入れない。`FactionData`+`PersonRules` で十分） |
| 一神論的悪（サウロンの神学） | 固有設定・世界観の直接流用。lore 以外は不採用 |

---

## 3. EPIC #LOTR の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UI へ配線。既存ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #2186**。GitHub issue 起票済み（#2189〜#2204）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **LOTR-1** | #2189 | 個人権力腐蝕圧力（`PersonalCorruptionRules`・長期権力保持→意志侵食） | 新 `PersonalCorruptionRules`/`CorruptionArc`。`PersonRules`×`CombatModifiers`(#106)×`DynastyRules` |
| **LOTR-2** | #2192 | 実存的緊急同盟（`ExistentialCoalitionRules`・閾値トリガー型・時限非敵対） | 新 `ExistentialCoalitionRules`。`DiplomacyRules`×`FactionRelations`×SGZ-1 `BalanceOfPowerRules` |
| **LOTR-3** | #2195 | 連合亀裂・誘惑イベント（敵の力を善用できるという誘惑→ステルス離反） | `EventEngine`(#116)×`LoyaltyRules`×LOTR-2×`BattleAllegianceRules` |
| **LOTR-4** | #2198 | 汚染された神託（`TaintedOracleRules`・情報ノード乗っ取り・偽報フィード） | 新 `TaintedOracleRules`。`EspionageRules.SpyNetwork`×SUN-1 `DeceptionRules` |
| **LOTR-5** | #2201 | 尊厳ある文化遺産継承（`CulturalLegacyRules`・尊厳ある撤退・制度継承） | 新 `CulturalLegacyRules`。`DynastyRules`×`Organization`×`DiplomacyRules` |
| **LOTR-6** | #2204 | （lore）世界観開示データ（権力腐蝕/連帯の尊さ/去り際） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順

`LOTR-1`（個人権力腐蝕＝最も固有で欠落の大きい signature）→ `LOTR-2`（実存的同盟＝LOTR-1 の集団版）→ `LOTR-3`（連合亀裂＝LOTR-2 の崩壊ダイナミクス）→ `LOTR-4`（汚染された神託＝諜報の裏面・SUN との接続）→ `LOTR-5`（文化遺産継承＝LOTR-1の文明スケール版帰結）→ `LOTR-6`（lore データ投入）。

> いずれも既存政治/社会シミュ層を**後退させず接続**する additive 設計。銀英伝の核テーマ「人間と権力」に直接刺さる。
