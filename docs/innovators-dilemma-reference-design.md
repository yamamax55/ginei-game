# クリステンセン『イノベーションのジレンマ』参考設計（EPIC #1677・INNO）

> 参照元：クレイトン・クリステンセン『イノベーションのジレンマ』（The Innovator's Dilemma）。
> 「なぜ優良企業ほど新興技術に敗れるか」を解いた経営理論の古典——**持続的技術**（既存指標を磨く）と**破壊的技術**（当初は主指標で劣るが別軸で優れ、下位市場から本流を奪う）の二系統、そして「正しい経営判断の積み重ねが敗北を生む」資源配分の重力構造を描く。
> 本ドキュメントは、当プロジェクト（Ginei＝星間国家戦略＋研究/組織/ドクトリンの純ロジック大系）にとって**役に立つ構造パターンだけ**を抽出し、EPIC `#1677` として issue 化した内容を記録する。
> **著作権注意**：書名・固有名・文章・図表・著者固有の事例（ディスクドライブ業界等）は流用しない。**「破壊的イノベーション」という構造パターン／メカニクスのみ**を参考にし、当プロジェクトの語彙（研究/軍事ドクトリン/組織）へ翻訳して実装する。

---

## 0. なぜ「イノベーションのジレンマ」が本システムに役立つか

当プロジェクトは研究・組織の**構造純ロジックを大量に保有**している（[CLAUDE.md] 参照）：

| 既存（構造・抽象） | カバー範囲 |
|---|---|
| `ResearchRules`/`ResearchProject`（#123-127） | 研究産出・分野別効率・政体偏り `IdeologyBias` |
| `Organization`/`SuccessionRules`（#812/#814/#816） | カリスマ/制度化/崩壊・英雄死後の組織存続 |
| `AutonomyRules`/`CommandDoctrine`（#544-550） | 集団依存vs自律分散・創発シナジー・傑物前提 |
| `MeritRankRules`（#900-905） | 戦功→爵位・制度の畏怖・法家の罠（短期最強→長期崩壊） |
| `DynastyRules`/`Regime`（#867） | 腐敗・天命喪失・改革・易姓革命 |
| `MarketRules`/`FiscalRules`（#179-182/161-162） | 需給均衡・国債/税の経済モデル |

**しかし、これらは「研究は加算的に進む」「組織は結束/制度化で測る」という静的・線形の系**であり、クリステンセンが固有に描く以下が**欠けている**：

| クリステンセンが固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **持続的技術 vs 破壊的技術の二系統** | `ResearchRules` は単一指標の加算産出。**当初は劣るが別軸で優れる技術**という二トラック・二トラジェクトリが無い |
| **性能のS字と供給過剰（オーバーシュート）** | 研究は単調に価値を増す前提。**要求水準を超えた性能は無価値化する**という天井が無い |
| **下位市場からの本流奪取（Disruption Crossover）** | 「弱い技術がやがて主力を喰う」という**逆転点**の構造が無い |
| **「正しい経営」ゆえの合理的敗北（資源配分の重力）** | `Organization`（結束/制度化）は組織の存続を測るが、**実績・制度化が高いほど新技術への投資が構造的に細る**という資源配分バイアスが無い |
| **両利きの経営（隔離自律組織の処方）** | `AutonomyRules` はドクトリンの型を持つが、**破壊的技術を育てるための隔離部門**という戦略的処方への接続が無い |
| **勝者の呪い（連勝が次の敗北の種になる）** | `MeritRankRules` の法家の罠は制度腐敗の話。**軍事的成功の蓄積が次世代ドクトリンへの脆弱性を生む**という技術版が無い |

**結論**：クリステンセンは当プロジェクトの研究・組織システムに**「なぜ強い勢力ほど新興技術に負けうるか」という構造的敗北のロジック**を与える。`ResearchRules`（#123）と`Organization`（#812）の交差点——**軍の保守性×研究軌道**——という、既存層が触れていない欠落軸を埋める。プレイヤーの決断は「投資配分」「隔離部門の設置」という高位の2レバーに限定し、敗北（正しさゆえの滅び）は創発（タイクン化回避）。

---

## 1. 役に立つ視点（要約）

クリステンセンの理論を、**本システムに効く形**で1行ずつ：

1. **「破壊的技術は最初、主要指標で劣る」**。だから優良組織の意思決定プロセスは正しく無視する。→ `ResearchRules` に**別軸性能**を持つ技術系統を足すことで、「弱いのに脅威」という技術が初めて成立する。
2. **「性能は要求水準を超えると無価値になる」**。磨き続けた指標が過剰性能（オーバーシュート）に陥り、別の勝ち筋に隙を作る。→ `MeritRankRules` 法家の罠（短期最強→長期崩壊）の**技術版**。
3. **「資源配分は顧客が握る＝合理的な近視眼」**。実績ある組織ほど、儲かる主力へ資源を吸われ、破壊的技術には回らない。→ `Organization.institutionalization`/`leaderCharisma` から**硬直度**を導出し、投資配分に重力バイアスをかける。
4. **「両利きの経営＝本体から隔離した自律組織だけが新技術を育てられる」**。→ `AutonomyRules.CommandDoctrine`（自律分散）を**戦略的処方**として接続する。
5. **「勝ち続けた組織ほど次の敗北に脆い」**。連勝が硬直を積み、破壊的戦術への耐性を失わせる。→ 会戦係数（#106 `CombatModifiers`）への実効値パターン接続。
6. **「正しさが滅びを呼ぶ」という逆説は、開示・世界観データとしても効く**。→ `DisclosureLedger`（FND-4）への lore 入力。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：`ResearchRules`#123／`Organization`/`SuccessionRules`#812／`AutonomyRules`#544-550／`MeritRankRules`#900 を**作り直さない**。INNO はそれらに**欠落軸を足し、接続する**だけ（additive）。プレイヤーの決断は「投資配分」「隔離部門の設置」の高位2レバーに限定し、タイクン化を避ける。

### ★★★ 最優先（真の欠落・クリステンセンの signature）

#### INNO-1 技術軌道の二系統（持続 vs 破壊）
- **持続的技術**＝既存性能指標を磨く（確実・既存戦力を強化）。**破壊的技術**＝当初は主指標で劣るが別軸（コスト/量産/到達性）で優れ、独自のS字で改善する。
- 接続：新 `TechTrajectoryRules`/`enum TechKind{持続,破壊}`（純ロジック・test-first）。`ResearchProject` に種別＋別軸性能を拡張。`ResearchRules.ResearchOutput`/`Tick` は置換せず、その上に軌道を載せる。

#### INNO-2 性能S字と供給過剰（オーバーシュート）
- 性能はS字（逓増→逓減）で伸び、**戦線の要求水準（demand line）を超えると過剰**になり、追加性能が無価値化する。`MeritRankRules`「法家の罠」の技術版＝短期最強が長期に飲まれる。
- 接続：`TechTrajectoryRules.PerformanceAt`（S字）／`IsOvershoot`（要求超過）。

#### INNO-3 下からの本流奪取（Disruption Crossover）
- 破壊的技術の性能が要求水準に追いつくと、**主指標で劣ったまま本流を奪取**（安さ・別軸で十分になるため）＝逆転点。
- 接続：`TechTrajectoryRules.DisruptionCrossover`（INNO-2 の上に積む）。

### ★★ 高（"正しい経営"の硬直と処方）

#### INNO-4 “正しい経営”ゆえの硬直（IncumbentRigidity・read-only）
- 組織の**実績・制度化が高いほど**、破壊的技術の採択確率が下がる（資源配分が既存顧客＝主力へ向かう重力）。合理的判断の積み重ねが敗北を生む＝プレイヤーが“正しく”振る舞うほど創発的に負ける。
- 接続：`Organization.institutionalization`/`leaderCharisma`＋実績指標から `IncumbentRigidity`（硬直度）を導出（純ロジック・基準値非破壊＝量を読むだけ）。`SuccessionRules` は不参照側（read-only）。

#### INNO-5 資源配分の重力（AllocationGravity）
- 限られた研究予算は**既存主力（高収益部門＝主流ドクトリン/主力艦隊）へ重力的に配分**され、破壊的研究は構造的に痩せる。プレイヤーの配分決断に重力バイアスがかかる（放置＝自動的に持続側へ流れる）。
- 接続：`TechTrajectoryRules.AllocationGravity`（硬直度×実績→破壊側配分の目減り係数）。`FiscalRules`/`CampaignState` の予算、`ResearchRules` の投資先選択。

#### INNO-6 両利きの処方（隔離自律部門）
- 本体の重力下では破壊的研究は完成しない。**独立・自律（`CommandDoctrine.自律分散`）の隔離部門**を立てると、重力バイアスを免れて破壊的技術を育てられる＝唯一の処方。隔離はコスト（本体との結束低下リスク）を伴う。高位の決断＝「隔離部門を設置するか」だけ。
- 接続：`AutonomyRules`/`CommandDoctrine`×`TechTrajectoryRules`。隔離部門の `DoctrineFactor`/`EmergentSynergy` が破壊的研究の重力を打ち消す。

### ★ 中（会戦係数・世界観lore）

#### INNO-7 勝者の呪い（連勝→硬直→破壊的戦術に脆弱）
- **過去の勝利が組織を硬直させ**、次の破壊的戦術（新編制・新兵科）に最も弱くする。連勝した勢力ほど `IncumbentRigidity` が上がり破壊に脆い。破壊的戦術を採れた側が会戦で優位を得る（実効値パターン・基準非破壊）。
- 接続：`AutonomyRules`×`Organization`×#106 `CombatModifiers`／会戦勝敗 `BattleManager`。

#### INNO-8 戦略マップ配線＋（lore）開示データ
- 各勢力に**技術軌道の状態**を持たせ、暦境界Tickで持続/破壊S字を進め、逆転点で本流転換イベント。辺境星系（低要求戦線）で破壊的技術が立ち上がる。＋（lore）「正しさが滅びを呼ぶ」「下からの逆転」「勝者の硬直」を `DisclosureLedger`（FND-4）へ lore データ入力。
- 接続：`CampaignState`×`CalendarDispatcher`（暦境界Tick）／`DisclosureLedger`（接続のみ・lore）。

### ❌ 不採用（重複・既存で十分・タイクン化）

| 不採用 | 理由 |
|---|---|
| 研究ポイント蓄積・分野別効率・政体偏り | `ResearchRules`#123-127 が既にカバー（上に軌道層を載せて接続するだけ） |
| 組織の結束/制度化/継承そのもの | `Organization`/`SuccessionRules`#812 が既にカバー（量を読んで硬直度を導出のみ・read-only） |
| 指揮ドクトリンの型（集団依存/自律分散）そのもの | `AutonomyRules`#544-550 が既にカバー（隔離部門の処方として接続） |
| 「短期最強→長期崩壊」の盛者必衰 | `MeritRankRules` 法家の罠#900・`DynastyRules` が既にカバー（技術版として共鳴のみ） |
| 市場/顧客セグメント/利益率の経済モデル化 | `MarketRules`#179・`FiscalRules`#161 が既にカバー（顧客＝戦線の要求水準へ翻訳） |
| 企業マネジメント・人事採用・製品ラインのマイクロ管理 | タイクン化回避 |

---

## 3. EPIC #1677 の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/戦略レイヤーへ配線。既存の研究/組織/ドクトリン層は**接続のみ・重複新設しない**。
> 著作権注意：書名・固有名・文章・固有の事例は不使用、**構造パターン／メカニクスのみ**参考。

> **EPIC = #1677**。GitHub issue 起票済み（#1678〜#1685）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **INNO-1** | #1678 | 技術軌道の二系統（`enum TechKind{持続,破壊}`＋研究の種別・別軸性能を `ResearchProject` 拡張） | 新 `TechTrajectoryRules`。`ResearchRules`/`ResearchProject` |
| **INNO-2** | #1679 | 性能S字と供給過剰（`PerformanceAt`/`IsOvershoot`／要求水準 demand line） | `TechTrajectoryRules` 拡張。`MeritRankRules` 法家の罠の技術版 |
| **INNO-3** | #1680 | 下からの本流奪取（破壊側が要求水準に到達＝`DisruptionCrossover` 逆転点） | `TechTrajectoryRules`（INNO-2の上） |
| **INNO-4** | #1681 | “正しい経営”ゆえの硬直（実績・制度化→`IncumbentRigidity` 導出・read-only） | `Organization.institutionalization`/`leaderCharisma`×`SuccessionRules` |
| **INNO-5** | #1682 | 資源配分の重力（`AllocationGravity`＝硬直度×実績で破壊研究を兵糧攻め） | `TechTrajectoryRules`。`FiscalRules`/`CampaignState`/`ResearchRules` |
| **INNO-6** | #1683 | 両利きの処方（隔離自律部門だけが重力を免れ破壊技術を育てる） | `AutonomyRules`/`CommandDoctrine`×`TechTrajectoryRules` |
| **INNO-7** | #1684 | 勝者の呪い（連勝→硬直→破壊的戦術に脆弱・会戦係数へ） | `AutonomyRules`×`Organization`×#106 `CombatModifiers`／`BattleManager` |
| **INNO-8** | #1685 | 戦略マップ配線＋（lore）開示データ（勢力技術軌道を `CampaignState` へ・暦境界Tick・観測層追従／`DisclosureLedger` lore） | `CampaignState`×`CalendarDispatcher`／`DisclosureLedger` |

### 推奨着手順

`INNO-1 → INNO-2 → INNO-3`（技術軌道の二系統・S字供給過剰・下からの逆転＝技術トラジェクトリの核）→ `INNO-4 → INNO-5`（硬直度導出→資源配分の重力＝敗北メカニズム）→ `INNO-6`（両利きの処方＝唯一の対抗策）→ `INNO-7`（勝者の呪い→会戦係数）→ `INNO-8`（戦略マップ配線＋lore）。

> いずれも既存の研究/組織/ドクトリン層を**後退させず接続**する additive 設計。`ResearchRules`#123 と `Organization`#812 の交差点——**軍の保守性×研究軌道**——を埋める。
