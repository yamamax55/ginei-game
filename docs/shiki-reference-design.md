# 司馬遷『史記』参考設計（EPIC #SJI）

> 参照元：司馬遷『史記』（前漢時代・BC91年頃成立）。中国二十四史の筆頭にして、五体（本紀/表/書/世家/列伝）で天子から刺客まで網羅した**多視点歴史叙述**の大著。
> 史書とは「誰が覇権を握り、なぜ王朝が滅び、いかに英雄は死んだか」を記録する——**正統性の台帳**であると同時に**敗者の証言**でもある。
> 本ドキュメントは、当プロジェクト（Ginei）に欠けている「多極外交の動学」「勝利後の功臣숙清」「個人名声の社会的蓄積」「プレイ履歴の史書構造化」という4欠落軸を抽出し、EPIC `SJI` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**世界観の構造パターン・メカニクスのみ**を参考にする。

---

## 0. なぜ「史記」が本システムに役立つか

### 既存の純ロジック層（カバー範囲）

| 既存モジュール | カバー範囲 |
|---|---|
| `DynastyRules`/`Regime` (#867) | 天命と易姓革命・腐敗→正統性低下→王朝交代 |
| `DiplomacyRules`/`DiplomacyState` (#189 DIP-1) | 外交状態遷移（宣戦/同盟/不可侵）・opinion修正子 |
| `LoyaltyRules`/`Allegiance` (#817 SEKI-1〜3) | 旗幟・条件付き忠誠・寝返りカスケード |
| `SuccessionRules`/`Organization` (#812) | カリスマの日常化・制度化・継承 |
| `EspionageRules`/`SpyNetwork` (#215-219) | 諜報ミッション・情報収集・妨害 |
| `PersonRules`/`Person` (#866) | 人物・軍才/文才・適材適所・役職効率 |
| `LifecycleRules`/`VacancyRules` (#151/152) | 年齢・死亡・後任補充 |
| `GovernmentRegistry`/`OfficeRules` (#142/144) | 役職・任命・資格チェック |
| `CoupRules` (#215-219) | クーデター（下から上への転覆） |
| JGS-4 `FounderTransitionRules` (#1230) | 功臣の処遇ジレンマ（守成期の役職ミスマッチ） |
| ALM-5 王道値/覇道値 (#1059) | 勢力の評判メタ層（対外的印象） |
| 列伝 CAT2-4 (#785) / 殿堂 CAT2-3 (#784) | 提督・艦隊の年代記・顕彰 |
| `MarketRules`/`BankRules` (#179-186) | 市場均衡・信用創造（貨殖列伝の経済は大半カバー） |

### 史記が固有に持つ視点 × 当プロジェクトでの欠落

| 史記が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **縦横家・合従連衡**（蘇秦と張儀）＝多極均衡における「弁士の説得コスト」と「多対一合従 vs 一対多連衡」 | `DiplomacyRules` は宣戦/和平の**状態遷移**。多国が協調して一強を牽制するか各個撃破されるかの**交渉動学**と**多極同盟の形成コスト**が無い |
| **功臣숙청・兎死狗烹**（韓信の悲劇）＝勝利後に功臣が「脅威」になり除去される | JGS-4 は守成期の「役職ミスマッチ」だが、**能動的な숙청（功臣排除）リスク**＝強大化した功臣が君主に除去される `PurgeRules` が無い（`CoupRules` は逆方向=下からの反乱） |
| **個人の余威・名声**（廉頗の「余威」）＝過去の実績が現在の政治・採用・外交に影響 | ALM-5 は勢力レベルの評判。**個人レベルの名声スコア**（実績→社会的資本→採用/忠誠/外交での優位）が `Person` に無い |
| **本紀/世家/列伝の三層史書構造**＝覇権年代記・諸侯家系録・個人伝記を三階層で記録 | 列伝#785は提督伝記だが、**「覇権変遷の年代記（本紀）」と「諸侯家系の世代記（世家）」** がプレイ履歴から自動生成される史書構造が無い |

**結論**：史記は当プロジェクトに「**①縦横家の交渉動学 ②勝利後の功臣숙청 ③個人名声の蓄積 ④本紀/世家/列伝のプレイ史書構造**」という4欠落軸を与える。貞観政要 #1226（納諫・創業守成）を補完する「勝者の危機」側面を担う。

---

## 1. 役に立つ視点（要約）

史記の世界観を**本システムに効く形**で1行ずつ：

1. **「戦場で決まる前に、外交で決まる」＝縦横家の弁術**。合従が成れば一強は孤立し、連衡が成れば多数は瓦解する。→ 銀河の勢力均衡を決めるのは艦隊戦だけではなく**外交弁術のコスト計算**。
2. **「兎死狗烹・鳥尽弓蔵」＝功臣は勝利で不要になる**。創業の功臣が最強になると、次は彼が脅威になる。→ `SuccessionRules`/`Organization` の逆サイド＝**勝利後の숙청リスク**。
3. **廉頗の余威＝名声は消えない**。老将が弱体化しても「かつての廉頗の勇名」だけで敵が動けなくなる。→ **個人名声スコア**（Renown）が外交・採用・旗幟に効く。
4. **歴史は多視点**。本紀で「正統な天子」と書かれた者が、列伝で「暴君」と描かれる。→ **史書の層構造**は「誰の目から見た歴史か」を複数記録し、正統性の多角性を体現。
5. **太史公の受難＝真実を書く勇気**。宮刑を受けながらも史記を完成させた司馬遷。→ 開示エンジン `DisclosureLedger` のlore：「正史」と「秘史」の二重記録、歴史改竄への抵抗。
6. **貨殖列伝の「物極必反」**。価格が頂点に達すれば必ず反転する、という商機の周期観。→ `MarketRules`の転換点検出（SAWとの接続・経済系で最小追加）。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`DiplomacyRules`/`LoyaltyRules`/`Organization`/`MarketRules`/`PersonRules` を作り直さない**。SJI はそれらに**欠落軸を追加し接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・史記の signature）

#### SJI 縦横家・合従連衡モデル（多極均衡の同盟動学）

- **合従（縦）**：多数の小国が一強国に対して連帯する。説得コスト×利益計算×「一強が崩れれば利益が大きい」という連帯誘因。
- **連衡（横）**：一強国が各国を個別に懐柔し、合従を崩す。個別条件提示×「隣国が合従しないなら自分も合従しても無駄」というナッシュ崩壊。
- 純ロジック：`AllianceFormationRules`（新設）— `PersuasionCost`（説得コスト＝power差×ideology差）／`FormHorizontalAlliance`（合従：目標国以外の全勢力の協調判定）／`FormVerticalAlliance`（連衡：強国が一国ずつ個別条件提示）／`AllyCohesion`（合従の結束＝構成国が多いほど瓦解しやすい）／`TryDissolveAlliance`（連衡により合従を崩す）。
- 接続：`DiplomacyRules`/`DiplomacyState`/`FactionRelations` × `GalaxyMap`（近接優先）× `LoyaltyRules.ResolveStance` の多勢力版。

#### SJI 功臣숙청・兎死狗烹（`PurgeRules` 新設）

- 「功臣が大きくなりすぎると君主の脅威になる」＝勝利後の숙청リスク。強大な功臣（高 `merit`/高 `rankTier`）→君主の恐怖心上昇→숙청判定→組織の再編。
- JGS-4 `FounderTransitionRules` との差：JGS-4 は「守成期の役職ミスマッチと移行コスト」、SJI-2 は「君主が功臣を**能動的に除去**する숙청判定」（`CoupRules` の逆方向）。
- 純ロジック：`PurgeRules`（新設）— `ThreatScore(merit,rank,support)`（功臣の脅威度）／`PurgeFear(regime,threat)`（君主の恐怖心）／`ShouldPurge(fear,threshold)`（숙청判定）／`PurgeOutcome{処断,幽閉,放逐}`（結末）／`PurgeLegitimacyPenalty`（忠義者の숙청は正統性と支持を削る）。
- 接続：`Organization`（創業後フェーズ → 脅威上昇）×`GovernmentRegistry`（役職剥奪）×`CaptivityRules.Execute`（処断経路）×`LoyaltyRules`（「君主が功臣を殺す」→忠誠低下）×`DynastyRules.Regime`（正統性コスト）。

### ★★ 高（既存に個人スケールの顔を足す）

#### SJI 個人名声・余威（`ReputationRules`/`Renown`）

- **廉頗の余威**＝老将の名声だけで敵が動けなくなる。個人 `Person` が実績から「名声スコア（Renown）」を蓄積し、現在の能力が落ちても余威が残る。
- 純ロジック：`ReputationRules`（新設）— `Renown`（名声スコア 0..∞）／`GainRenown(event)`（会戦勝利/功臣昇進/外交成功で積算）／`DecayRenown(dt)`（不活動で緩やかに減衰）／`RenownEffect`（採用確率ボーナス・外交 `PersuasionCost` 減少・旗幟 `BaselineLoyalty` 押し上げ・敵の臆病化＝ZOC強化）。
- ALM-5「王道/覇道値」との差：ALM-5 は**勢力の評判メタ層**。SJI-3 は**個人 `Person` の名声スコア**（廉頗個人の威名が采配や交渉に効く）。
- 接続：`PersonRules`/`Person` × `VacancyRules.SelectSuccessor`（名声高い人物が優先候補）× `DiplomacyRules`（外交コスト軽減）× `FactionLoyaltyRules`（余威が `BaselineLoyalty` を押し上げる）。

### ★ 中（史書構造・lore接続）

#### SJI 本紀/世家/列伝の史書構造（プレイ履歴→自動史書編纂）

- **本紀**（覇権年代記）：誰がいつ銀河の覇権を握り・失ったか = キャンペーンの正統年表。
- **世家**（諸侯家系録）：諸侯家系が何代・何系統続いたか = `Organization` の多世代記録。
- **列伝**（個人伝記）：既存 CAT2-4 (#785) の列伝と連携し、本紀・世家の文脈に埋め込む。
- 薄い新設：`ChronicleEntry`（`[Serializable]`・年代/種別{本紀/世家/列伝}/内容）＋`ChronicleRules`（static・`RecordSuccession`/`RecordDynastyChange`/`RecordHeroEntry`＝`AnnualLifecycleRules`/`DynastyRules`/列伝CAT2-4 が呼ぶ）。`CampaignSaveData`（FND-2）に `chronicle: List<ChronicleEntry>` を追加して永続化。
- 接続：`CampaignSaveData`/`CampaignSerializer`（FND-2）× 列伝 CAT2-4 (#785) × 殿堂 CAT2-3 (#784) × `DynastyRules`（易姓革命で本紀に1行）。

#### SJI（lore）太史公の史観・「正史」と「秘史」の二重記録

- 真実を書くために宮刑を受けた司馬遷。「正史」（権力が承認した歴史）vs「秘史」（権力が隠した真実）の緊張が史記の核心。
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への**loreデータ入力**。CCX-6（世界観codex退避）方針に一貫。「正史」は `DynastyRules`/`Regime.legitimacy` が高いほど強化され、「秘史」が開示されると正統性が揺らぐというイベント連鎖。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 天命・易姓革命 | **`DynastyRules`/`Regime`（#867）が完全カバー** |
| 守成期の功臣処遇（役職ミスマッチ） | **JGS-4 `FounderTransitionRules`（#1230）がカバー**。SJI-2は숙청リスクで別層 |
| 貨殖列伝の市場均衡・銀行信用 | **`MarketRules`/`BankRules`（#179-186）がカバー**。SAW EPIC で商機の顔も整備済み |
| 刺客列伝の個人義侠アクション | **`EspionageRules`（諜報ミッション）がカバー**。個別EPIC化不要 |
| 物極必反（価格反転タイミング） | SAW-4/5（空間裁定/コーナリング）が近い。MarketRulesへの軽微な追加はSAW実装時に吸収可 |
| クーデター（下からの転覆） | **`CoupRules`（#215-219）がカバー**。SJI-2は逆方向（上から下への숙청）で別 |

---

## 3. EPIC #SJI の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **SJI-1** | #1284 | 縦横家・合従連衡モデル（多極均衡の説得コスト×同盟形成/崩壊動学）| 新 `AllianceFormationRules`。`DiplomacyRules`×`GalaxyMap`×`LoyaltyRules` |
| **SJI-2** | #1287 | 功臣숙청・兎死狗烹（`PurgeRules`新設＝勝利後の功臣排除リスク）| 新 `PurgeRules`。`Organization`×`CaptivityRules.Execute`×`DynastyRules` |
| **SJI-3** | #1290 | 個人名声・余威（`ReputationRules`/`Renown`＝廉頗型の社会的蓄積）| 新 `ReputationRules`。`Person`×`VacancyRules`×`DiplomacyRules`×`FactionLoyaltyRules` |
| **SJI-4** | #1292 | 本紀/世家/列伝の史書構造（プレイ履歴→自動史書編纂）| 薄い `ChronicleRules`/`ChronicleEntry`。列伝#785/殿堂#784/`CampaignSaveData` |
| **SJI-5** | #1294 | (lore) 太史公の史観・「正史」と「秘史」の二重記録 | `DisclosureLedger`（FND-4）へのloreデータ入力。コード新設なし |

### 推奨着手順

`SJI-1`（縦横家＝多極均衡の核＋外交戦略の動機付け）→ `SJI-2`（兎死狗烹＝勝利後の組織崩壊トリガー＋SJI-1の連衡成功後の帰結と接続）→ `SJI-3`（名声＝SJI-1の説得コスト削減・SJI-2の余威残留に自然接続）→ `SJI-4`（史書＝SJI-1〜3の出来事を記録する器）→ `SJI-5`（lore）。

> いずれも既存モジュールを**後退させず接続**する additive 設計。貞観政要 #1226（納諫・守成）の「名君」側に対し、SJI は「功臣の숙청・多極外交・名声」という**覇権の危機側面**を補完する。
