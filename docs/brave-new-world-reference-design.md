# ハクスリー『すばらしい新世界』参考設計（EPIC #BNW）

> 参照元：ハクスリー『すばらしい新世界』（Aldous Huxley, *Brave New World*, 1932）。遺伝的に階級を固定し、快楽・消費・薬（ソーマ）で人々を自発的に服従させる「幸福なディストピア」。恐怖（オーウェル=1984）でなく**快楽でソフト全体主義を実現する**構造パターンを描く。
> 本ドキュメントは当プロジェクト（Ginei＝銀英伝風の星間国家戦略）にとって役立つ**メカニクスの構造パターン**だけを抽出しEPICとして issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定（ソーマ、アルファ/ベータ名称、フォード崇拝等）は流用せず、**世界観の構造パターンのみ**を参考にする。
> 旧形式関連EPIC：#374（BNW-1〜4・旧風モチーフ形式）— 本設計はその**上位互換**として欠落4軸を追加する（BNW-1〜4との重複は避ける）。

---

## 0. なぜ「すばらしい新世界」が本システムに役立つか

### 既存（CLAUDE.md の純ロジック層）でカバー済みの要素

| 既存モジュール | カバー範囲 |
|---|---|
| `HopeRules/Community`（#852-856） | 希望・絶望・末人発生（苦難が引き起こす） |
| `ConsentRules/Polity`（#836） | 合意・非協力・ボイコット（ガンジー型） |
| `SecurityRules`（#166） | **強制的**な異議抑圧・密告・クーデター検出 |
| `GovernanceRules`（#109） | 安定度・統合度・思想整合・産出係数 |
| `FactionStateRules` | 国家状態の総合（正統性/腐敗/希望/合意） |
| `ResearchRules`（#123-127） | 研究出力・政体偏り（研究と政体の関係） |
| `CareerPipelineRules`（#155-157） | 出自経路（士官学校/科挙/有力者）・席次→階級 |
| `SeniorityRules`（#155-156） | 席次固定の硬直度（`PoliticalRigidity`） |
| `DemographicsRules`（#153） | 有機的人口変動（コホート・依存率） |
| `DisclosureLedger`（FND-4/\#495） | 秘史の**開示**・前提連鎖・真相解放 |
| `ReligionRules`（#172-175） | 宗教統制・改宗圧力・社会効果 |
| `RedistributionRules`（#162-163） | 階級別税・再分配・ClassTension |
| 旧 BNW-EPIC #374（BNW-1〜4） | 風モチーフ：階級固定/薬/操作された幸福/安定vs自由 |

### BNW が固有に持つ視点 × 当プロジェクトでの欠落

| BNW の視点 | 当プロジェクトでの欠落 |
|---|---|
| **快楽的服従（ソーマ効果）**：強制でなく快楽・消費・娯楽で政治的無気力を生む | `SecurityRules`は**強制**抑圧。`HopeRules.Faith`は意味付与（宗教型）。**快楽消費→政治参加意欲の減退→合意↑・活力↓**という独立した回路が無い |
| **安定vs活力の政策選択**：科学・芸術・歴史・宗教を**禁じる**政策で最高安定を実現する | `FactionStateRules.Stability`は現状の測定値。**安定最大化政策（知識統制・役割固定）とその活力コスト**のトレードオフを明示的に設定する政策パラメータが無い |
| **知識統制（禁書・歴史抹消）**：`DisclosureLedger`の逆転 | `DisclosureLedger`は真実を開示する一方通行。**知識カテゴリを政策として封鎖し、短期安定と長期停滞を選ぶ**逆向きの機構が無い |
| **役割分布の意図的設計**：エリートN%・技術者M%・労働者L%という社会構成を政策として設定 | `DemographicsRules`は有機的変動。`CareerPipelineRules`は個人の出自経路。**勢力全体の役割比率を政策目標として管理する**窓口が無い |

**結論**：BNW の真の欠落は**4軸**：
1. **快楽的服従（ソーマ効果）** — 1984の恐怖と対をなすソフト統治の独自機構
2. **安定vs活力の政策選択形式化** — 安定最大化政策とその活力コストのトレードオフ
3. **知識統制** — `DisclosureLedger`（開示）の対称的な逆転
4. **役割分布の意図的設計** — 政策として設定できる社会構成管理

これらは**恐怖統治（1984系）・合意（ガンジー系）・希望（フロストパンク系）**とは独立した「快楽型ソフト全体主義」の固有メカニクスであり、当プロジェクトに**統治スタイルの多様性**をもたらす。

---

## 1. 役に立つ視点（本システムに効く形で）

1. **恐怖でなく快楽で服従を引き出す**：強制力ゼロで人々が自発的に服従する「ソフト全体主義」。→ `SecurityRules`（強制）・`ConsentRules`（自発的合意）の中間にある**第三の統治路線**として政策選択肢に深みを与える。
2. **最高の安定は最高の停滞でもある**：科学・芸術・宗教・歴史を禁じることで達成する安定は革新を殺す。→ `ResearchRules`と`GovernanceRules`安定度の間に**根本的なトレードオフ**を形式化する。
3. **幸福の製造は真実の抹消を必要とする**：`DisclosureLedger`が開示する真実を、統治者が封じ込めることで幸福製造が成り立つ。→ 開示エンジンに**「抑圧」の対称性**を与える。
4. **末人は設計される**：`HopeRules`の末人は苦難から生まれるが、BNWの末人は**快楽で意図的に生産される**。→ 統治者の選択肢として末人を「生産する」回路は新軸。
5. **役割固定は政策パラメータ**：`SeniorityRules.PoliticalRigidity`で部分的に表現できるが、BNWは**「政策として設定した社会構成比率」**という次元を加える。
6. **外部の「野蛮」が内部の歪みを照らす**：完全統制社会の外に非統制の世界が残ることで、管理された幸福の代償が可視化される。→ 勢力間の思想・統治スタイル比較・外交（`DiplomacyRules`）への深み。

---

## 2. 取り入れるべきメカニクス（優先度・既存への接続）

> 大原則：旧BNW-EPIC #374（BNW-1〜4）、`HopeRules`/`ConsentRules`/`SecurityRules`/`GovernanceRules` を**作り直さない**。
> BNW-参考はそれらに欠落軸を足し接続するだけ（additive）。タイクン化回避のため係数接続を基本とし、マイクロ操作UIを増やさない。

---

### ★★★ 最優先（ソフト全体主義の核心・真の欠落）

#### BNW-5 快楽的服従ルール（`PacificationRules` / `PacificationState`）

**概要**：快楽・消費・娯楽が政治参加意欲を消す「ソーマ効果」の純ロジック。強制（`SecurityRules`）でも意味付与（`HopeRules.Faith`）でもなく、**快楽そのものが合意を生む**独自回路。

- `PacificationState`：`pleasureLevel`（0..1・快楽/消費水準）、`apathyFactor`（0..1・政治的無気力度）
- `PacificationRules`（static）：
  - `ApathyFromPleasure(pleasureLevel)` → `apathyFactor`（高快楽→高無気力）
  - `CooperationBoost(apathyFactor)` → `Polity.cooperation` への短期加算（快楽→合意↑・非協力リスク↓）
  - `InnovationPenalty(pleasureLevel)` → `ResearchRules` 出力係数（快楽→革新↓）
  - `HopePlateauFactor(apathyFactor)` → `Community.hope` の上昇上限を平板化（末人を安定的に生産）
  - `DissentSuppression(apathyFactor)` → `SecurityRules.DissentSuppression`の「快楽型」版（非強制）
- **接続**：`ConsentRules.Withdraw`（非協力→快楽で抑制）×`HopeRules.UpdateDissent`（末人発火が起きにくい）×`ResearchRules.ResearchOutput`（革新コスト）×`MarketRules`（消費水準→pleasureLevel）
- **実効値パターン**：基準値（消費水準）を上書きせず、係数のみ返す
- **EditMode テスト必須**（TestHarness）

#### BNW-6 安定執政パラメータ（`StabilityRegimeRules` / `StabilityRegime`）

**概要**：統治者が「安定を優先する」政策選択を明示的に設定し、その活力コストと正統性リスクを形式化する。BNWの「世界総統が安定のために科学を禁じる」意思決定のモデル化。

- `StabilityRegime` enum：`{自由型, 温和型, 管理型, 安定専制型}`
- `StabilityRegimeRules`（static）：
  - `StabilityBonus(regime)` → `GovernanceRules.EquilibriumStability` への加算
  - `DynamismFactor(regime)` → `ResearchRules.ResearchOutput` 係数（管理型→0.7、安定専制型→0.4）
  - `LegitimacyRisk(regime, duration)` → 長期間の安定専制は `Regime.legitimacy` 侵食（隠蔽限界）
  - `KnowledgeControlEfficiency(regime)` → `KnowledgeControlRules`（BNW-7）の封鎖効率に乗算
  - `CultureOutput(regime)` → 文化的産出係数（芸術・歴史・宗教の活動度）
- **接続**：`FactionStateRules.Tick`（国家状態に安定執政モードを追加）×`ResearchRules`×`GovernanceRules`×`DynastyRules`（正統性長期侵食）
- **EditMode テスト必須**

---

### ★★ 高優先（知識統制・役割設計）

#### BNW-7 知識統制ルール（`KnowledgeControlRules`）

**概要**：`DisclosureLedger`（開示エンジン）の対称的な逆転。統治者が知識カテゴリを封鎖することで短期安定を得るが、諜報による漏洩リスクと革新コストを抱える。

- `KnowledgeControlRules`（static）：
  - `SuppressCategory(ledger, category)` → 指定カテゴリの`DisclosureEntry`を封鎖（`CanReveal`を`false`化）
  - `SuppressCost(category, factionState)` → 封鎖の維持コスト（安定度消費・資源）
  - `LeakRisk(category, espionageLevel)` → 諜報力が高い敵がいると封鎖が破れる確率（`EspionageRules`連動）
  - `InnovationBlock(suppressedCategories)` → 封鎖カテゴリ数に比例した研究出力ペナルティ
  - `LeakConsequence(entry)` → 封鎖が破れると隠蔽失敗による正統性↓＋`EventEngine`への通知
- **接続**：`DisclosureLedger`（逆転の対）×`EspionageRules.MissionSuccessChance`（漏洩判定）×`ResearchRules`（革新ブロック）×`EventEngine`（知識漏洩イベント）
- **実効値パターン**：封鎖状態は外部フラグ。DisclosureLedger の内部を書き換えない
- **EditMode テスト必須**

#### BNW-8 役割分布設計ルール（`DemographicDesignRules`）

**概要**：社会の役割分布（エリート・技術者・労働者）を政策として設計・管理し、実際の分布との乖離が社会的緊張を生む仕組みを形式化。有機的な`DemographicsRules`に政策の意図を追加する。

- `RoleDistribution` struct：`eliteShare`（0..1）、`technicalShare`、`laborShare`（3つで合計1）
- `DemographicDesignRules`（static）：
  - `OptimalDistribution(productionGoals, stabilityRegime)` → 目標生産×安定政策から最適分布を導出
  - `DistributionFit(actual, target)` → 0..1（1＝完全一致）
  - `SocialTension(fit, rigidity)` → 乖離×`SeniorityRules.PoliticalRigidity`が`RedistributionRules.ClassTension`に加算
  - `MobilityBlock(fit)` → 分布乖離が昇進詰まりを生む（`SeniorityRules.MeritOvertakes`への係数）
  - `DesignEfficiency(regime)` → 安定専制型は分布設計の効率↑・自由型は低い（設計が難しい）
- **接続**：`DemographicsRules`（有機的変動の基礎）×`CareerPipelineRules`（出自付与）×`SeniorityRules.PoliticalRigidity`×`RedistributionRules.ClassTension`
- **EditMode テスト必須**

---

### ★ 中優先（世界観lore）

#### BNW-9 （lore）世界観開示データ（`DisclosureLedger` への入力）

**概要**：BNW の思想的帰結を`DisclosureLedger`のデータとして実装。コード新設なし。

- 登録候補：「快楽の檻—自由の自発的放棄」「パンとサーカスの終着点—民主主義の死」「ソフト全体主義—幸福な奴隷制の完成」「野蛮の意味—外部からの問い」
- `SampleDisclosures`のパターンで `DisclosureEntry` を追加し、条件（安定執政専制型×一定期間）で発火
- **接続**：`DisclosureLedger`（FND-4）、`StabilityRegimeRules`（発火条件）。コード新設なし

---

### ❌ 不採用（理由つき）

| 不採用 | 理由 |
|---|---|
| 遺伝子工学・人工子宮・試験管生殖の具体的実装 | SF的ガジェット。構造パターン（役割固定・分布設計）は`CareerPipelineRules`+`DemographicDesignRules`で抽象化 |
| フォード崇拝の具体的宗教体系実装 | `ReligionRules`の一インスタンスとして乗せられる。新規モジュール不要（additive で十分） |
| ソーマの化学的副作用・依存性モデル | `PacificationRules.pleasureLevel`で抽象化。詳細毒性は不要（タイクン化回避） |
| 旧 BNW-1〜4 の再実装 | #374/#380-383 が概念を既に確立。新規は追加4軸のみ（additive） |
| 強制・監視・恐怖統治 | 1984 EPIC (#375/#1638) が対応。BNW-参考は快楽側に専念 |
| 消費の物理的サプライチェーン | `SupplyRules`/`SCM`がカバー。BNWは消費水準→pacification係数の接続のみ |

---

## 3. 子 Issue 表（着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。
> 旧 BNW-1〜4（#380-383）との重複は避け、BNW-5〜9 として追番。
> 著作権注意：固有名・キャラ・文章は不使用。**構造パターンのみ**参考。

> **EPIC = #2293**。GitHub issue 起票済み（#2294〜#2298）。

| # | issue | タイトル | 接続先・主眼 |
|---|---|---|---|
| **BNW-5** | #2294 | 快楽的服従ルール(`PacificationRules`) — 快楽消費→政治的無気力→合意↑・活力↓ | `ConsentRules`×`HopeRules`×`ResearchRules`×`MarketRules` |
| **BNW-6** | #2295 | 安定執政パラメータ(`StabilityRegimeRules`) — 管理度→安定↑・活力↓・長期正統性侵食 | `FactionStateRules`×`ResearchRules`×`GovernanceRules`×`DynastyRules` |
| **BNW-7** | #2296 | 知識統制ルール(`KnowledgeControlRules`) — DisclosureLedger逆転・封鎖→革新ブロック・漏洩リスク | `DisclosureLedger`×`EspionageRules`×`ResearchRules`×`EventEngine` |
| **BNW-8** | #2297 | 役割分布設計(`DemographicDesignRules`) — 政策目標分布×実分布の乖離→階級対立・昇進詰まり | `DemographicsRules`×`CareerPipelineRules`×`SeniorityRules`×`RedistributionRules` |
| **BNW-9** | #2298 | (lore) 世界観開示データ（快楽の檻・ソフト全体主義・野蛮の問い） | `DisclosureLedger`（データ入力のみ・コード新設なし） |

### 推奨着手順

`BNW-5 → BNW-6`（ソフト全体主義の核心：快楽統治×安定執政政策）→ `BNW-7`（知識統制＝DisclosureLedger の逆転）→ `BNW-8`（役割分布設計）→ `BNW-9`（lore）

> いずれも既存モジュールを**後退させず接続**する additive 設計。旧 BNW-1〜4（#380-383）と合わせて「ソフト全体主義」の完全な純ロジック層を構成する。
