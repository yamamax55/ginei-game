# デュマ『モンテ・クリスト伯』参考設計（EPIC #DUMA）

> 参照元：アレクサンドル・デュマ『モンテ・クリスト伯』（1844-46）。
> 無実の罪で14年間投獄された男が、膨大な財宝を手に変名で社交界に再登場し、
> 自分を陥れた三人を段階的に破滅させる長期復讐劇。
> 本ドキュメントは、当プロジェクト（Ginei）にとって**役に立つ視点**だけを抽出し、
> EPIC `#DUMA` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、
> **政治謀略メカニクス／世界観の構造パターンのみ**を参考にする。

---

## 0. なぜ「モンテ・クリスト伯」が本システムに役立つか

当プロジェクトは政治・謀略の**マクロ純ロジックを大量に保有**している：

| 既存（マクロ・抽象） | カバー範囲 |
|---|---|
| `LoyaltyRules`/`Allegiance`（#817） | 諸侯の忠誠・陣営スタンス（戦う/静観/寝返り） |
| `EspionageRules`/`SpyNetwork` | 諜報網の成否・情報収集・破壊工作 |
| `CaptivityRules` | 捕虜化・処断・解放・勧誘 |
| `SecurityRules`/`SecurityApparatus` | 反体制鎮圧・クーデター検知 |
| `DisclosureLedger` （FND-4） | 条件付き連鎖開示（秘史A開示→秘史B前提成立） |
| `DiplomacyRules`/`DiplomacyState`（#189） | 勢力間関係・条約・敵対判定 |
| `FiscalRules`/`MarketRules`/`BankRules` | 財政・市場・信用創造（国家/市場スケール） |
| `Person`/`PersonRules`/`CareerTrack` | 人物能力・役職・出自経路 |
| `GovernmentRegistry`/`OfficeRules` | 任免台帳・役職資格 |
| `EventEngine`（#116） | 条件発火イベント・選択肢・効果 |

**しかし、これらは「勢力・市場・組織」という集合主体の抽象的動作**であり、
モンテ・クリスト伯が固有に描く以下が**欠けている**：

| モンテ・クリスト伯が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **ネームド人物間の個人宿怨**（誰が誰にいつ何をされたか） | `LoyaltyRules` は勢力スタンス。**特定人物→特定人物**への私怨・目的意識が無い |
| **変名・偽装潜入**（敵陣で別人として社交する） | `EspionageRules` はネットワーク単位。**人物が自ら偽身分を纏って活動**する概念が無い |
| **段階的暗謀計画**（舞台を整えてから本命を刺す） | `EventEngine` はトリガー→効果。**人物が著作する多段計画**（舞台A→舞台B→本命Cの依存構造）が無い |
| **財力による個人標的の経済破滅**（無限の富を武器に特定人物を潰す） | 経済ルールは勢力・市場スケール。**個人資産を特定標的の信用に向けた攻撃**手段が無い |
| **腐敗的相互庇護ネットワーク**（共犯者が連鎖的に庇い合い、一人が崩れると全員崩れる） | `DisclosureLedger` は前提連鎖。**積極的な相互庇護・仲間の保護コスト**という動態が無い |
| **失墜後の社会的復権軌跡**（囚人→帰還→蓄積→再登場→地位回復） | `CaptivityRules` は解放まで。**解放後の名誉・地位・影響力の段階的回復弧**が無い |

**結論**：モンテ・クリスト伯は当プロジェクトの謀略・政治層に
**「名指しの怨恨が政治の原動力になる」という人物スケールの駆動力**と、
①個人宿怨 ②変名潜入 ③段階的暗謀 ④財力標的打倒 ⑤腐敗連鎖崩壊
という5つの欠落軸を与える。
**関ヶ原の寝返り（#817）・諜報（#166）・捕虜（#154）に人物の長期動機を接続する蝶番**になる。

---

## 1. 役に立つ視点（要約）

モンテ・クリスト伯の世界観を、**本システムに効く形**で1行ずつ：

1. **「腐敗した制度は人を陥れる——しかし制度は武器にもなる」**。
   主人公は法廷・社交・金融という同じ制度を逆用する。
   → `DynastyRules`（腐敗）×`GovernmentRegistry`（役職）×`FiscalRules`（財力）の交差点。

2. **「変名は究極の情報非対称——敵は誰を警戒すべきか知らない」**。
   偽身分は単なる変装でなく、情報優位を構造化する戦略。
   → `EspionageRules` に「人物が自ら情報の主体になる」次元を足す。

3. **「復讐は弾道——撃てば自分も傷つく」**。
   最終章で主人公は復讐の代償を思い知る（副次的被害、自己変容）。
   → `GrievanceRules` の宿怨解消コスト＝世界観EPIC（開示エンジン）のlore。

4. **「腐敗者は互いに庇い合うから強い——一人が崩れると連鎖崩壊する」**。
   三人の仇が互いの罪を隠し合っているため、個別に攻撃しても効かない。
   → `CorruptionNetworkRules`（腐敗連鎖）は関ヶ原の寝返りカスケードの「保護バージョン」。

5. **「長い沈黙こそが最大の準備——待機は無為でなく蓄積」**。
   14年の獄中でアリア神父から語学・剣術・財宝の知識を得る。
   → `RehabilitationRules`（失墜後復権）×`GrowthRules`（成長）。

6. **「財力は武力より精密——ターゲットの信用を削いで孤立させる」**。
   銃や毒より先に、財力で仇の支持基盤・社会的信用を崩す。
   → `EconomicAssaultRules`＝第三の政治武器（武力・外交に続く）。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`LoyaltyRules`/`EspionageRules`/`DisclosureLedger`/`FiscalRules` を作り直さない**。
> DUMA はそれらに**人物スケールの欠落軸を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・モンテ・クリスト伯の signature）

#### DUMA 個人宿怨 `Grievance`/`GrievanceRules`

- **概念**：ネームド人物Aがネームド人物Bに対して保持する追跡可能な宿怨。
  `Grievance`（`targetId` / `cause`{背信/冤罪/財産収奪/名誉毀損} / `severity`0..1 / `ageSinceTurn` / `isSatisfied`）。
- **効果**：`GrievanceRules.PoliticalInfluence(a,b)` → a が b を相手とする外交/任命判断に負のバイアスを与える。
  `IsSatisfied(g)` で宿怨解消（標的の失墜・死亡・謝罪）。
- **接続**：`Person`/`ICharacter`（保持者）×`DiplomacyRules`（関係修正子）×`LoyaltyRules`（忠誠補正）×`EventEngine`（宿怨発火イベント）。
- **なぜ新設**：`LoyaltyRules` は勢力スタンス（戦う/静観/寝返り）。`DiplomacyRules` は勢力間 opinion。
  どちらも **特定人物→特定人物** の私怨を長期追跡する概念が無い。純ロジック・test-first。

#### DUMA 変名・偽装潜入 `AliasProfile`/`InfiltrationRules`

- **概念**：`AliasProfile`（`personId` / `aliasName` / `targetFaction` / `startTurn` / `detectedProbability`）。
  ある `Person` が敵勢力内で別名を名乗り活動する。
- **効果**：`InfiltrationRules.IntelGain(alias)` → 潜入者は `EspionageRules` より高精度の情報を得る。
  `DetectionRisk(alias, apparatus)` → `SecurityApparatus` の能力×在籍期間で発覚確率。
  発覚 → `CaptivityRules.Capture` または撤退（`AliasProfile.Burned`）。
- **接続**：`EspionageRules`（情報収集基盤）×`SecurityRules`（検知）×`CaptivityRules`（発覚後処遇）。
- **なぜ新設**：`EspionageRules` はネットワーク単位（`SpyNetwork.MissionSuccessChance`）。
  **人物が自ら敵陣内で偽身分として動く**という第一人称の潜入概念が無い。

### ★★ 高（謀略・政治の動学を豊かにする）

#### DUMA 段階的暗謀計画 `PlotArc`/`PlotRules`

- **概念**：`PlotArc`（`authorId` / `targetId` / `List<PlotStage>` / `currentStage` / `isRevealed`）。
  `PlotStage`（`goal` / `precondition`{条件式} / `action` / `effect` / `disruptionRisk`0..1）。
- **効果**：`PlotRules.CanAdvance(stage, context)` で前条件充足を判定。
  `Advance(arc)` で次段階へ進み効果発火（`EventEngine` 連動可）。
  `TryDisrupt(arc, investigator)` で計画が潰れると `isRevealed=true`。
- **接続**：`GrievanceRules`（動機源）×`EventEngine`（外部トリガー・効果）×`EspionageRules`（情報収集段階）。
- **なぜ新設**：`EventEngine` はトリガー→効果の単発連鎖。
  **人物が著作する多段依存計画**（舞台A→舞台B→本命Cの順序・依存構造）は別概念。

#### DUMA 財力による個人標的の経済破滅 `EconomicAssaultRules`

- **概念**：`EconomicAssaultRules.Strike(assailantFaction, targetPerson, spendAmount)` →
  標的が保有する `FiscalState.primaryBalance` / 信用(`BankRules.CreditScore`)を spendAmount 比で毀損。
- **効果**：標的の政治的後援者が離反（`GrievanceRules.PoliticalInfluence` 連動）、役職剥奪リスク上昇（`OfficeRules`）。
- **接続**：`FiscalRules`（財政毀損） × `BankRules`（信用削減） × `GovernmentRegistry`（役職リスク）。
- **なぜ新設**：既存経済ルールは全て勢力・市場レベルの集合操作。
  **個人資産を特定ネームド標的の信用に向けて打撃する**一対一の財力攻撃手段が無い。

#### DUMA 腐敗的相互庇護ネットワークの連鎖崩壊 `CorruptionNetworkRules`

- **概念**：`CorruptionTie`（`aId` / `bId` / `protectionStrength`0..1）で相互庇護関係を登録。
  `CorruptionNetworkRules.Register`/`Implicate(exposed, implicatee)` /
  `CascadeRisk(tie)` → 庇護者が危うくなると被庇護者の脆弱性が上昇。
  `Collapse(network, seed)` → seed の失墜から連鎖的に崩壊する節点を返す。
- **接続**：`DisclosureLedger`（前提充足連鎖）× `SecurityRules`（検知・粛清）× `GovernmentRegistry`（解任）。
- **なぜ新設**：`DisclosureLedger` は「Aの開示がBの前提を充たす」一方向依存。
  **互いを積極的に庇い合う双方向保護**（保護コスト・保護崩壊の伝播）は別ダイナミクス。

### ★ 中（復権軌跡・世界観lore）

#### DUMA 失墜後の社会的復権弧 `RehabilitationRules`

- **概念**：`RehabilitationRules.Phase(person)` →
  `{Imprisoned / Exiled / Returned / Accumulating / Reentered / Restored}` の6段階。
  各段階への昇格条件（`CanAdvance`）と達成効果（再任・階級回復・宿怨発動可能化）を返す。
- **接続**：`CaptivityRules`（解放→Returned）× `GrowthRules`（蓄積→Accumulating）×
  `CareerTrack`（再登場経路）× `GrievanceRules`（復讐実行の前提条件）。
- **なぜ新設**：`CaptivityRules` は解放まで。`CareerTrack` は通常キャリア。
  **不名誉な失墜→段階的回復→旧地位の再獲得という固有の弧**を追跡するモデルが無い。

#### DUMA （lore）復讐の道徳と孤独の開示データ

- 「復讐は正義を産むか、それとも復讐者自身を壊すか」→ `DisclosureLedger` lore
- 「長い暗謀の果ての孤独——目標達成後に残るもの」→ カリスマの日常化（#812）との共鳴
- 「犠牲者の無辜（副次的被害）——計画が意図せず巻き込む者」→ 倫理的選択`EventChoice` への素材
- **コード新設なし**。`DisclosureLedger` + `SampleDisclosures` へのデータ入力。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 新たな諜報ネットワーク実装 | **`EspionageRules`/`SpyNetwork`（#166）がカバー**。DUMA-2 は人物スケールの上乗せのみ |
| 新たな捕虜・投獄システム | **`CaptivityRules`（#154）がカバー**。DUMA-6 は解放後の弧だけ追加 |
| 新たな勢力間外交 | **`DiplomacyRules`（#189）がカバー**。DUMA は人物スケールの修正子を足すだけ |
| 新たな腐敗/天命喪失システム | **`DynastyRules`/`Regime`（#867）がカバー** |
| 刑務所システムの新規実装 | 舞台として示唆するだけ。**`CaptivityRules` の延長で足りる** |
| 変装・衣装・美容の実装 | **マイクロ操作**。`AliasProfile` の論理に内包（外見はゲームに不要） |
| 貴族社会のプロトコル詳細 | **タイクン化回避**。`OfficeRules`/`RankSystem` の既存枠内で動く |

---

## 3. EPIC #DUMA の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。
> 既存謀略・政治ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #2139**。GitHub issue 起票済み（#2144〜#2167）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **DUMA-1** | #2144 | 個人宿怨 `Grievance`/`GrievanceRules`（ネームド人物間の私怨・政治動機への長期影響） | `Person`×`DiplomacyRules`×`LoyaltyRules`×`EventEngine` |
| **DUMA-2** | #2149 | 変名・偽装潜入 `AliasProfile`/`InfiltrationRules`（人物が敵陣で別名活動・発覚リスク） | `EspionageRules`×`SecurityRules`×`CaptivityRules` |
| **DUMA-3** | #2154 | 段階的暗謀計画 `PlotArc`/`PlotRules`（人物著作の多段依存計画・破壊可能・`EventEngine`連動） | `GrievanceRules`×`EventEngine`×`EspionageRules` |
| **DUMA-4** | #2158 | 財力による個人標的経済打倒 `EconomicAssaultRules`（個人資産→標的の信用・財政を毀損） | `FiscalRules`×`BankRules`×`GovernmentRegistry` |
| **DUMA-5** | #2163 | 腐敗的相互庇護と連鎖崩壊 `CorruptionTie`/`CorruptionNetworkRules`（共犯保護網→崩壊連鎖） | `DisclosureLedger`×`SecurityRules`×`GovernmentRegistry` |
| **DUMA-6** | #2166 | 失墜後の社会的復権弧 `RehabilitationRules`（囚人→帰還→蓄積→再登場→地位回復の6段階） | `CaptivityRules`×`GrowthRules`×`CareerTrack`×`GrievanceRules` |
| **DUMA-7** | #2167 | （lore）復讐の道徳と孤独の開示データ（正義 vs 怨恨・副次的被害・長期暗謀の代償） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順

`DUMA-1`（宿怨＝全体の動機源、最も欠落が大きく関ヶ原#817に直結）
→ `DUMA-2`（変名潜入＝宿怨を持つ者が使う具体的武器）
→ `DUMA-3`（暗謀計画＝宿怨×潜入の多段統合）
→ `DUMA-4`（財力打倒＝第三の武器）
→ `DUMA-5`（腐敗崩壊＝`DisclosureLedger`との融合）
→ `DUMA-6`（復権弧＝逆向きの軌跡・`CaptivityRules`後の延長）
→ `DUMA-7`（lore入力・コード不要）

> いずれも既存謀略・政治ロジックを**後退させず接続**する additive 設計。
> 関ヶ原（#817）・諜報（#166）・捕虜（#154）に**人物の長期動機という蝶番**を追加する。
