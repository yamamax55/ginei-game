# 『墨子』参考設計（EPIC #MOZI）

> 参照元：墨子（紀元前 470〜391 年頃）著『墨子』。戦国中期の思想家・墨翟とその後継者たちによる
> 「墨家」の集大成。**兼愛（すべての人への等しい配慮）・非攻（侵略戦争の否定）・尚賢（実力主義）・
> 節用（倹約）・防城術（守城の専門技術）**を骨格とする。
> 墨家は思想集団でありながら、非国家的な守城請負組織としても機能した——他国の防衛を依頼されると
> 遠征して城を守った、史上最古級の「専門防衛コントラクター」。
> 本ドキュメントは、当プロジェクト（Ginei）にとって**役に立つ視点だけ**を抽出し、
> EPIC `#MOZI` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**政治/軍事メカニクス／世界観の構造パターンのみ**を参考にする。

---

## 0. なぜ「墨子」が本システムに役立つか

当プロジェクトは統治・軍事・外交に関する**純ロジックを大量に保有**している：

| 既存（カバー範囲） | 対応モジュール |
|---|---|
| 実力主義的昇進（席次 vs 実力） | `SeniorityRules` |
| 軍功授爵（戦功→爵位） | `MeritRankRules`（QIN #900〜905） |
| 人物の適材適所 | `PersonRules`/`Person` |
| 王朝腐敗・正統性・改革 | `DynastyRules`/`Regime` |
| 被支配者の協力と撤退 | `ConsentRules`/`Polity` |
| 占領統合・思想差・安定度 | `GovernanceRules`/`Province` |
| 惑星攻城（攻撃側視点） | `PlanetSiegeRules`/`Planet` |
| 外交状態・条約・宣戦 | `DiplomacyRules`/`DiplomacyState` |
| 組織継続（英雄死後） | `Organization`/`SuccessionRules` |
| 財政効率・税・福祉 | `FiscalRules`/`FiscalState` |
| 法令一貫性（韓非子） | `LegalConsistencyRules`（HFZ） |

**しかし、これらは「国家の制度・均衡・二項対立」の抽象モデル**であり、墨子が固有に描く以下が**欠けている**：

| 墨子が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **非国家の守城専門組織（守城請負）** | 攻城は `PlanetSiegeRules` でカバー。しかし**非国家の防衛専門集団**が契約・派遣されて守城に加勢するモデルがない。兵器・人員・技術を提供する「防衛コントラクター」としての組織類型がない |
| **非攻ドクトリン（自己拘束的非侵略）** | `DiplomacyRules` は外交状態の遷移を扱う。しかし**勢力が自らの教義として侵略を禁じ、その自己拘束が外交信用を生む**回路がない。平和宣言（双方合意）とは違い、一方的な戦略的制約 |
| **兼愛ガバナンス（思想差の減衰）** | `GovernanceRules.IdeologyModifier` は思想差を常にペナルティとして計算。しかし**「すべての民を等しく扱う」統治教義が思想差による占領不満を緩和する**メカニクスがない |
| **尚賢の正統性直結（能力→正統性）** | `SeniorityRules` は昇進ルートの柔軟性。`MeritRankRules` は軍事的報奨。しかし**「支配者の能力が高いほど体制の正統性が安定する」という能力→正統性の直結エンジン**がない。`DynastyRules` の天命は腐敗起点（能力とは無関係） |
| **節用（倹約的統治→財政効率トレードオフ）** | `FiscalRules` は税・支出・債務の均衡。しかし**支配者が儀礼・贅沢を廃して倹約統治することで産出効率は上がるが、貴族・富裕層の合意が下がる**という制度的トレードオフがない |

**結論**：墨子は当プロジェクトの戦略・内政ロジックに**「非国家的な防衛連帯」という組織類型**と、
①**守城専門集団**（攻城防御の欠落軸）、②**非攻ドクトリン**（一方的平和主義の外交価値）、
③**兼愛ガバナンス**（思想差ペナルティの緩和ルート）、④**尚賢の正統性直結**（能力→体制安定）
という4つの真の欠落軸を与える。⑤**節用の財政効率**は既存財政に新たなトレードオフ軸を追加する。
`PlanetSiegeRules`/`DiplomacyRules`/`GovernanceRules`/`DynastyRules`/`FiscalRules` への**additive な接続**。

> 参考：`MeritRankRules`（QIN #900-905）は始皇帝モデルの軍功爵位で一部重複する。
> MOZI はその思想的対抗軸（墨家 vs 秦の法家）として位置づけ、重複域（軍功報奨）は作らない。

---

## 1. 役に立つ視点（要約）

墨子の世界観を、**本システムに効く形**で1行ずつ：

1. **墨家は「防衛コントラクター」だった** — 弱国から守城依頼を受けると戦士・技術者を派遣し城を守った。国家でも傭兵でもない第三類型。→ `PlanetSiegeRules` の**防御側強化の外部供給**としてモデル化できる（攻城はあるが守城支援組織は未実装）。
2. **非攻＝攻撃しないことが最強の外交カードになる** — 侵略しない誓約は信頼を生み、防衛同盟の結成を容易にする。しかし自ら攻撃できない非対称コストを負う。→ `DiplomacyRules` に**自己拘束ドクトリンの外交価値**軸を追加。
3. **兼愛は占領コストを下げる実用主義でもある** — 征服地の民を「自分たちと同じ」として扱う統治は、思想差による反抗を弱める。→ `GovernanceRules.IdeologyModifier` を**ドクトリン側で緩和**する余地を開く。
4. **尚賢は「誰が偉いか」でなく「誰が有能か」で役職を決める** — 有能な支配者への支持は有徳な支配者と同様に正統性を安定させる。→ `DynastyRules`/`Regime` に**能力ルートの正統性源泉**を追加（徳ルートとは別軸）。
5. **節用は「派手な国家を廃す」という政治的コスト** — 官僚・貴族は儀礼と贅沢で地位を示す。簡素統治は生産力を上げるが階級的基盤を削る。→ `FiscalRules` × `ConsentRules` の**倹約ドクトリンのトレードオフ**。
6. **非命（反宿命論）：努力が歴史を変える** — 「運命だから仕方ない」という敗北主義への否定。→ 世界観開示（`DisclosureLedger`）の**宿命論 vs 自由意志**の対立軸として lore 注入。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`PlanetSiegeRules`/`DiplomacyRules`/`GovernanceRules`/`DynastyRules`/`FiscalRules` を作り直さない**。MOZI はそれらに**欠落軸を足し、接続する**だけ（additive）。`SeniorityRules`/`MeritRankRules` は作り直さず接続のみ。

### ★★★ 最優先（真の欠落・墨子の signature）

#### MOZI 守城専門集団（`DefenseGuild`・非国家の守城請負組織）
- **非国家の守城専門組織**：勢力に属さず、または複数勢力と非敵対的に中立な立場で守城支援を行う組織。「巨子」（代表者）が率いる墨家型の構造。
- **守城請負**：`DefenseGuild.contract`（依頼勢力・対象星系）を受け入れると、その星系の防御に `defenseBonus`（制空権回復速度加速・攻城進行阻止力向上）を提供する。
- **組織の自律性**：非国家組織なので `FactionRelations.IsHostile` に依存しない独自の中立条件。`DefenseGuildRules.CanContract`（依頼受理条件：依頼側が守城対象・非侵略性）。
- 接続：`PlanetSiegeRules.Tick` の `defenderSAV` に `DefenseGuild.bonus` を加算。`Organization`（組織継続性）＋`FactionRelations`（中立判定）。**純ロジック・test-first**。

#### MOZI 非攻ドクトリン（`NonAggressionDoctrineRules`・自己拘束→外交信用）
- **非侵略宣言**：勢力が「非攻」ドクトリンを採択すると `CanDeclareOffensiveWar=false`（`DiplomacyRules.DeclareWar` をゲート）。
- **外交信用ボーナス**：非攻採択勢力への他勢力の `opinion` 修正子が `+nonAggressionTrustBonus`（既定+15）→ 同盟締結・不可侵条約が成立しやすくなる（`DiplomacyRules.CanProposeAlliance` の判定値が下がる）。
- **内政正統性**：非攻採択は `ConsentRules.Withdraw` の発火閾値を緩和（戦争疲弊が起きにくい→協力維持しやすい）。
- **トレードオフ**：攻撃不可によるスターシステム獲得不能。抑止が信じられなければ攻め込まれるリスク（`FactionRelations` の変化で失効なし→ 敵の判断に委ねる）。
- 接続：`DiplomacyRules`（意見修正子/状態遷移ゲート）×`ConsentRules`（正統性）。**純ロジック・test-first**。

### ★★ 高（既存システムへの有効な欠落補完）

#### MOZI 兼愛ガバナンス（`UniversalCareRules`・思想差の減衰）
- **「思想差ペナルティを薄める」統治ドクトリン**：勢力が兼愛ドクトリンを採択すると、占領統治の `GovernanceRules.IdeologyModifier` のネガティブ幅が `universalCareDampening`（0..1、既定0.4）倍に縮小される（純関数で基準値非破壊）。
- **限界**：ドクトリン採択は `FactionState.inclusiveness`（包摂度）が高い場合のみ有効（搾取型の勢力が「兼愛」と言っても効果なし）。`GovernanceRules.OutputFactor` へは影響しない（生産力は安定度経由のまま）。
- 接続：`GovernanceRules.EquilibriumStability`（純関数の `ideologyMod` 引数に掛けるだけ）×`FactionState.inclusiveness`。**純ロジック・test-first**。

#### MOZI 尚賢の正統性直結（`CompetenceLegitimacyRules`・能力→体制正統性）
- **能力ベースの正統性ルート**：`DynastyRules.Regime` の正統性は現在「腐敗で下がり・改革で戻る」モデル。これに**「支配者の有効能力が高いほど正統性低下を遅らせる」**補正を追加する。
- 純関数 `CompetenceLegitimacyRules.LegitimacyRetentionFactor(leader: Person, regime: Regime)`：`PersonRules.Aptitude(leader, 軍務 or 政務)` に基づく 0.5〜1.5 の倍率→ `DynastyRules.Tick` の腐敗→正統性低下量に乗算（基準計算式は非破壊）。
- **無能な君主は正統性を急速に失い、有能な君主は正統性を長く維持**。`SeniorityRules`（昇進経路）とは別の純ロジック。
- 接続：`DynastyRules`/`Regime` ×`PersonRules`/`Person`（`ICharacter`）×`RankSystem`。**純ロジック・test-first**。

### ★ 中（既存への接続・世界観補完）

#### MOZI 節用の財政効率（`FrugalityDoctrineRules`・簡素統治のトレードオフ）
- **倹約ドクトリン**：勢力が「節用」ドクトリンを採択すると `GovernanceRules.OutputFactor` に `frugalityProductionBonus`（既定+0.1）を加算（行政コスト削減＝生産力向上）。
- **代償**：`FiscalClass.富裕層` の `ClassSupportDelta` に `frugalityElitePenalty`（既定-0.15）を適用（貴族・富裕層は儀礼・地位表示の廃止に不満）。`SeparationOfPowersRules` が弱い勢力（専制）ではクーデターリスク（`CoupRisk`）が増加する副作用も。
- **タイクン化回避**：倹約は新しい建設メニューを生まない。係数の変化だけ（背景的効果）。
- 接続：`GovernanceRules.OutputFactor`×`RedistributionRules.ClassSupportDelta`×`CoupRules.CoupRisk`。**純ロジック・test-first**。

#### MOZI（lore）世界観開示データ
- **コード新設なし**。`DisclosureLedger`（FND-4）への**lore データ入力**：
  - 「非攻＝防衛同盟の信頼の礎」（非攻ドクトリン採択後に解放）
  - 「兼愛帝国の夢と現実」（占領安定化閾値到達で解放）
  - 「非命：宿命を拒む意志と秩序の崩壊」（`DisclosureLedger` の宿命論 vs 自由意志対立軸。世界観EPIC秘史と接続）
  - 「巨子の最後の遠征」（守城専門集団が滅亡するまで城を守った史実をモチーフに）
- 接続：`DisclosureLedger`（FND-4）×`EventEngine`（#116）。CCX-6 世界観 codex 退避方針に一貫。

### ❌ 不採用（重複・既存で十分・タイクン化）

| 不採用 | 理由 |
|---|---|
| 墨家の軍功報奨・爵位体系 | **`MeritRankRules`（QIN #900-905）が既にカバー**。軍功→爵位のモデルは重複新設しない |
| 墨家の席次 vs 実力の昇進ルール | **`SeniorityRules`/`CareerPipelineRules` がカバー**。MOZI は接続のみ |
| 墨家の守城兵器（投石機等）の実装 | 武器カスタムは **ALM-8 技術ツリー配線**（`ResearchRules`）で処理。武器の個別実装は新設しない |
| 「尚同」（上下一統）の指令摩擦低下 | **`MinistryRules.SectionalismFriction`** の逆数として接続可能だが、韓非子の「法的一貫性（HFZ-1）」と実質重複。新EPIC化しない |
| 天志・明鬼（神罰・怨霊）の恐怖統治 | **`ReligionRules`/`SecurityRules`** の組み合わせで実現可。新規モジュール不要 |
| 兼愛的な税制（全民均等課税） | **`RedistributionRules`/`TaxStructure`** の既存パラメータで実現可。累進/逆進は既存 |
| 星系間の相互防衛条約の詳細実装 | **`DiplomacyRules.TreatyRules`（DIP-2 #191）** が既にカバー予定。重複新設しない |

---

## 3. EPIC #MOZI の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。
> 既存のシステムは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #1551**。子issue #1555〜#1569（MOZI-1〜6）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **MOZI-1** | #1555 | 守城専門集団（`DefenseGuild`・`DefenseGuildRules`・非国家の防衛請負組織） | `PlanetSiegeRules.Tick`（`defenderSAV` 加算）×`Organization`×`FactionRelations` |
| **MOZI-2** | #1560 | 非攻ドクトリン（`NonAggressionDoctrineRules`・自己拘束→外交信用・攻撃不可のトレードオフ） | `DiplomacyRules`（意見修正子/状態遷移ゲート）×`ConsentRules` |
| **MOZI-3** | #1563 | 兼愛ガバナンス（`UniversalCareRules`・思想差ペナルティの減衰ドクトリン） | `GovernanceRules.EquilibriumStability`×`FactionState.inclusiveness` |
| **MOZI-4** | #1565 | 尚賢の正統性直結（`CompetenceLegitimacyRules`・能力→体制正統性の保全倍率） | `DynastyRules`/`Regime`×`PersonRules`/`Person`×`RankSystem` |
| **MOZI-5** | #1567 | 節用の財政効率（`FrugalityDoctrineRules`・倹約ドクトリン→産出↑・貴族合意↓） | `GovernanceRules.OutputFactor`×`RedistributionRules`×`CoupRules` |
| **MOZI-6** | #1569 | （lore）世界観開示データ（非攻の信頼基盤／兼愛帝国の夢と現実／非命と宿命対立） | `DisclosureLedger`（FND-4）×`EventEngine`（#116）。コード新設なし |

### 推奨着手順
`MOZI-1`（守城専門集団＝最も固有で未実装の守備側軸・test-first で基盤固め）→ `MOZI-2`（非攻ドクトリン＝外交信用の新軸・`DiplomacyRules` 接続）→ `MOZI-3 → MOZI-4`（内政/統治の補完軸）→ `MOZI-5`（財政トレードオフ）→ `MOZI-6`（lore・コード新設なし・最後）。

> いずれも既存モジュールを**後退させず additive に接続**する設計。
> `PlanetSiegeRules`（攻城）への防御側補完・`DiplomacyRules`（外交）への一方的平和主義軸が最大の貢献。
