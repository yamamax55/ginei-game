# アーレント『エルサレムのアイヒマン』参考設計（EPIC #BNAL）

> 参照元：Hannah Arendt "Eichmann in Jerusalem: A Report on the Banality of Evil" (1963)。
> 政治哲学者ハンナ・アーレントによる1961年のアイヒマン裁判傍聴記録と分析。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略）にとって**役に立つ視点**だけを抽出し、EPIC `#BNAL` として issue 化する提案。
> **著作権注意：固有名・文章・キャラクター・固有設定は流用せず、政治哲学的メカニクス／世界観の構造パターンのみを参考にする。**

---

## 0. なぜ「エルサレムのアイヒマン」が本システムに役立つか

当プロジェクトは権力論・統治論の**純ロジック層を大量に保有**している（[CLAUDE.md] 参照）：

| 既存（統治・権力・忠誠） | カバー範囲 |
|---|---|
| `SecurityRules`/`SecurityApparatus`（#166） | 秘密警察・DissentSuppression・RepressionSupportPenalty |
| `LoyaltyRules`/`BattleAllegianceRules`（#817） | 忠誠・調略・寝返りカスケード |
| `Organization`/`SuccessionRules`（#812/#814） | カリスマの日常化・組織継承 |
| `Polity`/`ConsentRules`（#836） | 権力は借り物・非協力ボイコット |
| `Community`/`HopeRules`（#852） | 希望と末人・秩序ルート |
| `Regime`/`DynastyRules`（#867） | 天命・腐敗・正統性サイクル |
| `CaptivityRules`（#154） | 捕虜化・処断・解放・登用 |
| `SeparationOfPowersRules`（#171） | 三権分立・TyrannyRisk |
| `TyrantToolkitRules`（ARIS-5） | 僭主維持術・密告・不信醸成 |
| `CivilianControlRules`（GOV-4 #145） | 文民統制・クーデターリスク |
| `DisclosureLedger`/`DisclosureRules`（FND-4） | lore開示・秘史連鎖 |
| `EventEngine`/`GameEventDef`（#116） | データ駆動イベント |

**しかし、これらは「権力の行使と抵抗」という視点から設計されており**、アーレントが固有に描く以下が**欠けている**：

| アーレントが固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **悪の凡庸性（Banality of Evil）** | `SecurityRules.DissentSuppression` は組織的抵抗の抑制。しかし「ヒエラルキーが思考停止を通じて道徳的主体性を蝕み、普通の人間が組織的加担の歯車になる」メカニズムが無い |
| **複数性（Plurality）と公的領域** | `ConsentRules.Withdraw`（非協力）は集団行動の停止。しかし「多様な視点の共存が政治行為の条件」であり、「それが消滅すること自体が全体主義の入口」という正の条件が無い |
| **全体主義の動態（孤立→恐怖ループ）** | `SecurityRules` の恐怖は組織化された抵抗への抑制。「孤立(atomization)→恐怖(terror)→余剰化(superfluous people)のスパイラル」として個人の社会的絆が切断されるアトム化が無い |
| **組織犯罪の責任連鎖と戦後裁判** | `CaptivityRules` に処断・解放・登用があるが「命令連鎖で責任が分散し誰も全体像を見ない」という組織的加担の責任追及メカニズム（戦争犯罪・戦後裁判）が無い |

**結論**：アーレントは当プロジェクトの権力論に**「悪の組織的産出構造」という視点**と、**①思考停止・道徳的主体性の喪失 ②公的複数性の破壊 ③全体主義の動態スパイラル ④組織犯罪の責任連鎖**という4つの欠落軸を与える。**全体主義への分岐シナリオ・帝国の腐敗エンディング**に最も効くテクスチャを供給する。

---

## 1. 役に立つ視点（要約）

アーレントの洞察を、**本システムに効く形**で1行ずつ：

1. **「悪は怪物や熱狂者を要しない」**＝官僚的服従と思考停止が「普通の人間」を加担者にする。→ `SecurityRules` の弾圧は外的な恐怖だが、**命令ヒエラルキーが内発的な道徳的主体性を蝕む**のは別回路。帝国の腐敗が加速するにつれ、艦隊将校が「命令だから」と非人道的作戦を遂行する動態を表現できる。
2. **「政治行為の条件は複数性（plurality）の保全」**＝各人の不可換な視点が共存する公的領域が民主的正統性の源泉。→ `PluralityRules` が `ConsentRules` の**前提条件**として機能し、全体主義が「同意の機械的調達」ではなく「複数性の破壊」であることを表現する。
3. **「全体主義は孤立→恐怖→余剰化のスパイラル」**＝個人の社会的絆（家族・友人・コミュニティ）を切断してアトム化し、孤立した個人を恐怖で操り、最終的に「余剰な人間」概念を産出する。→ `HopeRules`/`ConsentRules` の構造的破壊ループとして既存モジュールに接続する。
4. **「命令連鎖が責任を分散させる」**＝組織犯罪は「全体を見ている誰もいない」状態で進行する。命令に従ったことは免罪符ではなく、各人の判断能力（moral agency）に応じた責任がある。→ `WarCrimesRules`/`AccountabilityChain` が `CaptivityRules.DefaultDisposition`（政体による処遇傾向）に**組織的加担の程度**という新軸を足す。
5. **「裁判は法の執行であり政治の道具ではない」**＝アーレントが批判したのは「ショーとしての裁判」。正義の実現は法の手続きによる。→ `WarCrimesRules.TrialOutcome` に**政治圧力 vs 法的正統性**の緊張を表現し、世界観 lore（DisclosureLedger）に「正義は法廷で生きる」を開示する。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`SecurityRules`/`LoyaltyRules`/`ConsentRules`/`CaptivityRules` を作り直さない**。BNAL はそれらに**欠落軸を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・アーレントの signature）

#### BNAL 悪の凡庸性（思考停止と道徳的主体性の喪失）
- **`BanalityState{hierarchyDepth: int, complianceNorm: float, moralAgencyFactor: float}`**：
  - `hierarchyDepth`：命令連鎖の深さ（将官→中堅将校→下士官→兵）
  - `complianceNorm`：組織内の服従規範の強さ（0〜1）
  - `moralAgencyFactor`：道徳的主体性（1.0=完全な自律判断、0.0=完全な服従機械）
- **`ThoughtlessnessRules`(static)**：
  - `MoralAgencyFactor(depth, norm)`：階層深さ×服従規範 → 道徳的主体性を計算（深く・強い服従規範ほど低下）
  - `AtrocityRisk(state)`：moral agency が低下した状態での非人道的作戦執行リスク
  - `SystemicCulpability(commandChain)`：指揮連鎖全体での「組織的加担度」
- 接続：`Organization`（組織の制度化 → complianceNorm を決定）、`CivilianControlRules`（文民統制が moral agency を保全）、`SecurityRules`（恐怖が complianceNorm を押し上げる）

#### BNAL 複数性と公的領域
- **`PoliticalSpace{perspectiveDiversity: float, speechFreedom: float, opinionSuppression: float}`**：
  - `perspectiveDiversity`：異なる視点の共存度（0=一元化、1=多様な視点が競存）
  - `speechFreedom`：公的な発言・討議の自由度
  - `opinionSuppression`：意見抑圧の強さ（`SecurityRules` から流入）
- **`PluralityRules`(static)**：
  - `IsTotalitarian(space)`：公的複数性が閾値以下 → 全体主義的状態の判定
  - `ActionCapacity(space)`：pluralityがある程度 → 集合的政治行為の能力を返す
  - `LegitimacyFromPlurality(space)`：多様な視点の参与 → 正統性加算（`Regime.legitimacy` への入力）
  - `AtomizationLevel(suppression, security)`：抑圧×監視 → 個人が孤立しアトム化される程度
- 接続：`ConsentRules`（非協力の前提：pluralityが保たれているときのみ組織的非協力が可能）、`FactionStateRules`（Tick に AtomizationLevel を伝播）、`Regime.legitimacy`（複数性が正統性の源泉）

### ★★ 高（全体主義の動態と法的責任）

#### BNAL 全体主義の動態（孤立・恐怖ループ）
- **`TotalitarianPressure{atomization: float, terror: float, ideologySubstitution: float}`**：
  - `atomization`：社会的絆の切断度（家族・友人・コミュニティの解体）
  - `terror`：組織的恐怖の強度
  - `ideologySubstitution`：「イデオロギーが現実認識を置換」する度合い（現実より教義が優先される）
- **`TotalitarianRules`(static)**：
  - `AtomizationEffect(pressure)`：アトム化が `PluralityRules.ActionCapacity` を削減する係数
  - `TerrorLoopGain(atomization, security)`：アトム化×恐怖 → 恐怖が自己増幅するゲイン係数
  - `IsSuperfluousPopulation(dehumanizationLevel)`：非人間化が閾値を超えると「余剰人口」概念が社会に浸透する
  - `Tick(pressure, securityLevel, dt)`：圧力・安全保障レベル・時間から全体主義圧力を更新
- 接続：`Community`/`HopeRules`（hopeless community → 末人 → 全体主義的動員に脆弱）、`SecurityRules`（恐怖の源泉・DissentSuppression が terror を供給）、`ConsentRules`（Withdraw 能力の喪失＝non-cooperation ができない状態）

#### BNAL 組織犯罪の責任連鎖と戦後裁判
- **`AccountabilityChain{positions: List<(actor, moralAgencyFactor)>}`**：
  - 指揮連鎖内の各ポジションと、その人物の moral agency 水準のリスト
- **`WarCrimesRules`(static)**：
  - `IndividualCulpability(chain, positionIndex)`：指揮連鎖内の位置と moral agency から個人の加担責任を算出
  - `CanClaimObedience(moralAgencyFactor)`：moral agency が十分に高い場合、「命令に従った」は免罪にならない
  - `TrialOutcome(culpability, politicalPressure)`：責任の重さ×政治圧力 → 裁判結果（処断/無罪/政治決着）
  - `PostWarJustice(chainResults)`：戦後の集団的正義処理（勝者の論理 vs 法的正統性の緊張）
- 接続：`CaptivityRules`（処断・解放の判定に組織的加担度を追加）、`OrderOfBattle`（指揮連鎖の構造的データ）、`LoyaltyRules`（忠誠≠免罪の明示）

### ★ 中（lore・世界観開示）

#### BNAL lore — DisclosureLedger 開示データ
- コード新設なし
- entries（世界観の構造パターンとして開示）：
  - 「悪に怪物は要らない（思考停止が加担を生む）」：全体主義傾向が閾値を超えた時に開示
  - 「複数性の喪失が政治を終わらせる」：`PluralityRules.IsTotalitarian` 成立の時に開示
  - 「正義は法廷で生きる（組織犯罪の個人責任）」：戦後裁判イベント解決後に開示
- 接続：`DisclosureLedger`/`DisclosureRules`（FND-4）、世界観 EPIC（全体主義への分岐シナリオ）

---

## 3. EPIC #BNAL の子 Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UI へ配線。既存権力論ロジックは**接続のみ・重複新設しない**。
> **著作権注意：固有名・文章・キャラクターは不使用。メカニクス・世界観構造のみを参考にする。**

> **EPIC = #1527**。GitHub issue 起票済み（#1530〜#1537）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **BNAL-1** | #1530 | `ThoughtlessnessRules`/`BanalityState` — 悪の凡庸性（hierarchyDepth×complianceNorm→moralAgencyFactor・AtrocityRisk・SystemicCulpability） | 新純ロジック。`Organization`×`CivilianControlRules`×`SecurityRules` 接続。EditMode テスト必須 |
| **BNAL-2** | #1532 | `PluralityRules`/`PoliticalSpace` — 複数性と公的領域（perspectiveDiversity・IsTotalitarian・ActionCapacity・AtomizationLevel） | 新純ロジック。`ConsentRules`前提拡張×`FactionStateRules`×`Regime.legitimacy`。EditMode テスト必須 |
| **BNAL-3** | #1535 | `TotalitarianRules`/`TotalitarianPressure` — 全体主義の動態（atomization・terror・ideologySubstitution・Tick・TerrorLoopGain） | 新純ロジック。`HopeRules`/`ConsentRules`/`SecurityRules` 接続。EditMode テスト必須 |
| **BNAL-4** | #1536 | `AccountabilityChain`/`WarCrimesRules` — 組織犯罪の責任連鎖と戦後裁判（IndividualCulpability・CanClaimObedience・TrialOutcome） | 新純ロジック。`CaptivityRules`拡張×`OrderOfBattle`×`LoyaltyRules`。EditMode テスト必須 |
| **BNAL-5** | #1537 | （lore）DisclosureLedger 開示データ — 悪の凡庸性・複数性の喪失・法廷正義 | コード新設なし。`DisclosureLedger`/`DisclosureRules` データ入力。条件接続を設計書に記述 |

### 推奨着手順
`BNAL-1`（思考停止・道徳的主体性＝最も固有な signature）→ `BNAL-2`（複数性＝全体主義の入口条件）→ `BNAL-3`（動態スパイラル＝BNAL-1/2 の合成ループ）→ `BNAL-4`（責任連鎖＝`CaptivityRules` 拡張）→ `BNAL-5`（lore）。

> いずれも既存の権力論・忠誠・合意ロジックを**後退させず接続**する additive 設計。帝国の腐敗エンディング・全体主義への分岐シナリオに最も効く。

### ❌ 不採用

| 不採用 | 理由 |
|---|---|
| アイヒマン個人の心理・内面モデル | ゲームは構造論で動く。Person心理層は未実装かつ追加不要 |
| 具体的な法廷手続き詳細（証拠・弁護・量刑プロシージャ） | `EventEngine`/`CaptivityRules` の既存で十分。新EPIC化しない |
| 全体主義の政治体制区分（スターリン主義 vs ナチズム等）の再現 | 固有設定問題。ゲームは抽象メカニクスで表現する |
| 「ユダヤ人評議会/協力者」問題の再現 | `LoyaltyRules`（忠誠・寝返り）の既存が十分。詳細化は作品固有 |
| 全体主義の歴史的起源論（帝国主義からの連続）の個別実装 | 抽象化すると BNAL-2/3 に吸収される。別 EPIC 化は不要 |
