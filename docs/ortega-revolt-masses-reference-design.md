# オルテガ『大衆の反逆』参考設計（EPIC #ORTE）

> 参照元：ホセ・オルテガ・イ・ガセット『大衆の反逆』(1930)。
> 本ドキュメントは当プロジェクト（Ginei＝銀英伝風の星間国家戦略）に**役に立つ構造パターン**だけを抽出した提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用しない。**思想構造・社会メカニクスのパターンのみ**を参考にする。

---

## 0. なぜ「大衆の反逆」が本システムに役立つか

当プロジェクトは社会・政治の**純ロジック層を大量に保有**している（CLAUDE.md 参照）：

| 既存（社会・政治ロジック） | カバー範囲 |
|---|---|
| `HopeRules`/`Community`（#852-856） | 希望が尽きると末人が立つ（フロストパンク的危機） |
| `ConsentRules`/`Polity`（#836/837） | 合意の増減・非協力・支持低下（ガンジー）|
| `DynastyRules`/`Regime`（#867） | 正統性・腐敗・天命喪失・改革（天命サイクル）|
| `PartyRules`/`Party`（GOV-6） | 政党支持・最小選挙・派閥 |
| `LeadershipElectionRules`（GOV-7） | 党首選出（党員票×議員票の加重和） |
| `CivilianControlRules`（GOV-4） | 文民統制・クーデターリスク |
| `PersonRules`/`Person` | 軍人/文民・役割一致効率・6能力 |
| `CareerPipelineRules`（LIFE-5/6/7） | 士官/科挙/テクノクラートの出自パイプライン |
| `OfficeRules`/`GovernmentRegistry` | 役職資格・任命台帳（GOV-1/3） |
| `CrowdRules`/`Crowd`（CRWD#1819） | 群衆化の相転移・被暗示性・感情カスケード |
| `FactionStateRules`/`FactionState` | 国家状態の合成（正統性/合意/結束/希望） |

**しかし、これらは「危機・恐慌・権威」が駆動するロジック**であり、本著が固有に描く以下が**欠けている**：

| 本著が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **市民的自己要求（エリートと大衆の区分）** | `Person` は能力を持つが「自己に要求するか否か」という軸がない。繁栄の中で大衆化が進む動学が無い |
| **繁栄→安住→制度フリーライドの逆説** | `DynastyRules` の腐敗は時間進行だが、**安定・繁栄が大衆化を加速し腐敗速度を上げる**回路が無い |
| **ポピュリズム（大衆が反エリート候補を押し上げる）** | `LeadershipElectionRules` は票計算のみ。**大衆化率×不満→ポピュリスト台頭**の動学が無い |
| **超国家統合体の形成/崩壊** | `DiplomacyRules` は二国間。**複数勢力が主権を委ねる連盟・統合体**の中間層が無い |
| **専門化の野蛮（知識の蛸壺）** | `CareerPipelineRules.TechnocratEffectiveness` は専門職優位だが、**専門家が汎用政治職に就くとペナルティ**になる機能が無い |
| **エリートの責務（noblesse oblige）** | 高い役職・特権に「自己要求の義務」が対置されない。履行不全が統治コストを生む回路が無い |

**結論**：本著は当プロジェクトの政治・統治ロジックに
①**市民的自己要求（civicVirtue）と繁栄の逆説的腐食**、
②**ポピュリズムの動学**、
③**超国家統合体の形成**、
④**専門化ペナルティ**
という4つの欠落軸を与える。
`DynastyRules`（腐敗）× `FactionState`（安定）× `LeadershipElectionRules`（選挙）× `DiplomacyRules`（外交）の各モジュールへ**additive に接続**するだけで動く。

---

## 1. 役に立つ視点（要約）

本著の思想構造を**本システムに効く形**で1行ずつ：

1. **エリートは自己に要求し、大衆は要求しない**——高い地位は高い義務の後払い（noblesse oblige）。→ 役職者の「自己要求度」が統治の質を決める。`PersonRules`×`OfficeRules` に責務ループ。
2. **繁栄が大衆を育て、大衆が文明を食う**——長い安定は自己要求を麻痺させ、制度を当然のものとして消費する世代を生む。→ 安定期に `civicVirtue` が低下し `Regime` の腐敗速度が上がる（逆説的腐食）。
3. **ポピュリズムは大衆化の政治的帰結**——自己要求の無い大衆は「自分に奉仕する指導者」を選ぶ。→ `LeadershipElectionRules` に大衆化率×不満の係数を乗せ、ポピュリスト台頭リスクを導出。
4. **専門家は自分の分野の天才だが余では野蛮人**——超専門化は汎用政治能力を壊す。→ `TechnicalAptitude` 高×汎用政治職 → `Effectiveness` ペナルティ（`SpecializationPenaltyRules`）。
5. **国民国家を超える秩序だけが混乱を静められる**——「欧州合衆国」の先見＝多国家統合体。→ `SupranationalBodyRules`：複数勢力が主権の一部を委ねる連盟/統合体の純ロジック。
6. **エリートの義務不履行は静かな腐敗**——特権を持ちながら何も要求しない上位層は、危機的でない形で統治を劣化させる。→ `NoblesseObligeRules`：役職者の義務スコアが `GovernmentRegistry` の効率に波及。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**既存の `DynastyRules`/`PartyRules`/`LeadershipElectionRules`/`PersonRules`/`DiplomacyRules` を作り直さない**。ORTE はそれらに**欠落軸を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・本著の signature）

#### ORTE 市民的自己要求と大衆化率（`CivicVirtueRules`/`CivicVirtue`）

- **`CivicVirtue`**（純データ struct）：勢力の「市民的自己要求度」(0..1)。
- 動学：長期安定・高繁栄 → 慣れ → `civicVirtue` 低下；危機・改革・エリート主導の制度投資 → 上昇。
- **大衆化係数 `MassificationFactor`**（1 - civicVirtue）が `DynastyRules.Regime` の腐敗速度 `Tick` に乗る＝繁栄が制度を静かに蝕む。
- 接続：`FactionStateRules.Stability` の修正子・`GovernanceRules.OutputFactor` の係数として連結。

#### ORTE ポピュリズム圧力と統治質の劣化（`PopulismRules`）

- **`PopulistRisk`**：大衆化率 × 不満（`FactionState.IsCollapsing` 前段階）→ ポピュリスト指導者台頭確率。
- **`LeadershipElectionRules` 拡張**：`PopulistScore`（反エリート・カリスマ特化）が大衆票では高評価、議員票では低評価 → ねじれ選挙・ポピュリスト首班の可能性。
- **政策効果ペナルティ `PolicyEfficiencyMultiplier`**：ポピュリスト政権では `GovernanceRules.OutputFactor` × ペナルティ係数（短期支持↑、長期効率↓）。
- 接続：ORTE-1 × `PartyRules` × `LeadershipElectionRules` × `GovernanceRules`。

### ★★ 高（統合体・専門化）

#### ORTE 超国家統合体（`SupranationalBodyRules`/`UnionState`）

- **`UnionState`**（純データ）：参加勢力リスト・統合度 `integrationLevel`(0..1)・共同決議権。
- **メカニクス**：`integrationLevel` 高 → 共同防衛係数・共同市場 `LogisticsRules.CohesionFactor` ボーナス・内部紛争リスク低下。
  逆に `integrationLevel` 低 → 主権摩擦 → 脱退リスク `ExitRisk`。
- 接続：`DiplomacyRules.SignTreaty` の上位層（二国間条約複数→連盟格上げ）× `FactionRelations.IsHostile`（連盟内は非敵対デフォルト）× `LogisticsRules`。

#### ORTE 専門化の野蛮（`SpecializationPenaltyRules`）

- `PersonRules.Effectiveness` の拡張：**`TechnicalAptitude` が高く `CivilAptitude` が低い者が汎用政治・外交・元首職に就くと** `Effectiveness` にペナルティ係数 `SpecialistInOfficepenalty` を乗算。
- **キャリア経路との連動**：`CareerTrack.テクノクラート` 出身者は専門職でのみ高効率＝汎用政治職への配置は損失（`PersonRules.BestFor` が汎用職は専門職出身を回避）。
- 接続：`PersonRules` × `CareerPipelineRules.TechnocratEffectiveness` × `OfficeRules`。

### ★ 中（責務・lore）

#### ORTE エリートの責務と役職降格圧力（`NoblesseObligeRules`）

- **`ObligationScore`**：役職者の「自己要求度」（能力適性×役割一致×実績）。
- 不履行（`ObligationScore` 低下）→ `DynastyRules.Regime.corruption` の蓄積速度 + α・支持(`ConsentRules`) の微低下。
- 高い`ObligationScore` → `FactionStateRules.Stability` に小ボーナス（エリートが義務を果たすと秩序が安定する）。
- 接続：`PersonRules` × `OfficeRules` × `GovernmentRegistry`。

#### ORTE（lore）開示データ — 繁栄と文明の自己消費・超国家統合の先見

- コード新設なし。`DisclosureLedger`（FND-4）への lore データ入力のみ。
- 内容：「繁栄が大衆を育て大衆が文明を食う（逆説的腐食）」「技術は遺産、主体なき者には廃墟になる」「国民国家の枠を超えた秩序だけが混乱を静める」

### ❌ 不採用（重複・既存で十分・タイクン化）

| 不採用 | 理由 |
|---|---|
| 群衆心理・被暗示性・感情カスケード | **CRWD #1819 がカバー**（Le Bon 群衆論）。ORTE は政治・制度の構造的動学であり群衆心理とは別 |
| 末人フラグの再設計 | **`HopeRules.Community.UpdateDissent`（#852）が既にカバー**。ORTE は末人でなく大衆化の「安定期の静かな腐食」 |
| 選挙システムの全面再設計 | **`LeadershipElectionRules`（GOV-7）を作り直さない**。ポピュリスト係数のみ additive に追加 |
| 個別政策の実施・法制・投票マイクロ | タイクン化回避。高位の決断+エンジン駆動の帰結モデルに徹する |
| 固有名・国家名・哲学用語の実装 | 著作権注意＋実装不要。構造のみ抽出 |

---

## 3. EPIC #ORTE の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。
> 既存の政治・統治ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**思想構造・メカニクスのみ**参考。

> **EPIC = #1839**。GitHub issue 起票済み（#1842〜#1861）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **ORTE-1** | #1842 | 市民的自己要求と大衆化率（`CivicVirtueRules`/`CivicVirtue`）＝繁栄が腐敗速度を上げる逆説 | `DynastyRules.Regime` 腐敗係数 × `FactionStateRules` 修正子 |
| **ORTE-2** | #1847 | ポピュリズム圧力と統治質の劣化（`PopulismRules`）＝大衆化率×不満→反エリート選挙 | ORTE-1 × `LeadershipElectionRules` × `PartyRules` × `GovernanceRules` |
| **ORTE-3** | #1852 | 超国家統合体（`SupranationalBodyRules`/`UnionState`）＝連盟の形成・共同防衛・脱退リスク | `DiplomacyRules` 上位層 × `FactionRelations` × `LogisticsRules` |
| **ORTE-4** | #1857 | 専門化の野蛮（`SpecializationPenaltyRules`）＝テクノクラートが汎用政治職に就くと損失 | `PersonRules.Effectiveness` 拡張 × `CareerPipelineRules` × `OfficeRules` |
| **ORTE-5** | #1860 | エリートの責務と役職降格圧力（`NoblesseObligeRules`）＝義務不履行→腐敗加速・支持低下 | `PersonRules` × `OfficeRules` × `GovernmentRegistry` × `DynastyRules` |
| **ORTE-6** | #1861 | （lore）開示データ — 繁栄の逆説・超国家統合の先見（コード新設なし） | `DisclosureLedger`（FND-4）lore 入力のみ |

### 推奨着手順

`ORTE-1`（大衆化の基礎データ構造）→ `ORTE-2`（大衆化の政治的帰結＝ポピュリズム。ORTE-1 に依存）→ `ORTE-3`（超国家統合。独立して進められる）→ `ORTE-4`（専門化ペナルティ。独立）→ `ORTE-5`（責務ループ。ORTE-1/4 に依存）→ `ORTE-6`（lore。いつでも可）。

> いずれも既存政治・統治ロジックを**後退させず接続**する additive 設計。`FactionState` の安定ループに最も効く。
