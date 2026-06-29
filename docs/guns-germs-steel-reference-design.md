# ダイアモンド『銃・病原菌・鉄』参考設計（EPIC #1644）

> 参照元：ジャレド・ダイアモンド『銃・病原菌・鉄』（Guns, Germs, and Steel）（1997年）。
> なぜ一部の社会が他を征服したのかを「地理と生態環境」から解明した文明論的考察。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略＋大規模社会・政治ロジック層）に
> とって**役に立つ視点**だけを抽出し、EPIC `#1644` として issue 化する提案。
> 著作権注意：固有名・文章・特定文明の固有設定は流用せず、
> **地理→伝播の構造パターン／メカニクスのみ**を参考にする。
> 特定の人種・民族・実在文明を「優劣」として扱わない（本書の主旨そのものに沿う）。

---

## 0. なぜ「銃・病原菌・鉄」が本システムに役立つか

当プロジェクトは地理・伝播・経済・社会層を大量に保有している（[CLAUDE.md] 参照）：

| 既存（地理・伝播・経済） | カバー範囲 |
|---|---|
| `GalaxyMap`/`Corridor`（Phase C） | 星系グラフ・回廊ネットワーク |
| `GovernanceRules`/`Province`（#109） | 安定度・統合度・産出（安定度比例） |
| `ResearchRules`/`ResearchProject`（#123-127） | 研究産出・技術進歩 |
| `CultureRules`/`Culture`（#194） | 同化・分離独立・ナショナリズム |
| `ColonizationRules`/`ColonyMission`（#129） | 入植・星系開拓 |
| `LogisticsRules`（#844） | 版図の連結成分・一体化度 |
| `EpidemicRules`/`Epidemic`（PEST-1 #2225） | SIR型拡散・感染・回復 |
| `SupplyRules`（#94 L-2） | 補給線・遮断 |
| `ResourceProductionRules`（#93 L-1） | 星系類型×安定度比例の産出 |

**しかし、これらはすべて「勢力内で閉じた均衡」または「単純到達判定」**であり、
本書が固有に描く以下が**欠けている**：

| 本書が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **賦存の地理的非対称**（星系の「持ち物」が位置・近傍・類型から決まる） | `ResourceProductionRules` は全星系一律のOutputFactor。**位置と生態が産出に影響する決定論生成**が無い |
| **伝播の異方性**（東西軸は速く、南北軸は障壁で遅い） | `CultureRules`/`ResearchRules` は勢力内で閉じる。**隣接漏出・回廊の向き・媒質差による拡散係数の違い**が無い |
| **研究・文化の地理的漏出**（先進星系から隣接星系へ技術・文化が自然に流れる） | 研究は各勢力が独立産出。**星系間の知識・文化の自然伝播**が無い |
| **疫病の免疫非対称**（集住・家畜共存の集団は免疫があり、孤立集団へ一方的にショックを与える） | `EpidemicRules`（PEST-1）は拡散するが**集住密度→免疫格差→非対称ショック**のモデルが無い |
| **余剰→拡張燃料の連鎖**（高賦存→食料余剰→人口増→入植拡大という累積的連鎖） | 入植は遠征個別発火。**賦存値が入植の拡散速度を底上げする連鎖ルール**が無い |
| **障壁係数としての地形**（回廊の向き・媒質が技術伝播の速度を変える） | `Corridor` に障壁係数が無い。**孤立によって技術的遅滞が生まれる**モデルが無い |

**結論**：本書は当プロジェクトの地理・伝播・経済シミュに
**「初期条件の不均等がいかに蓄積的優位に転じるか」という第三の戦争軸**を与える。
軍事力でなく地理と生態が生む**差の自己強化**——
①賦存の非対称 ②伝播の異方性 ③研究/文化の漏出 ④疫病免疫の非対称 ⑤余剰→拡散燃料
⑥障壁係数——という6本の欠落軸を、**既存の地理・社会・伝播層に additive で接続する**。

---

## 1. 役に立つ視点（要約）

本書の世界観を、**本システムに効く形**で1行ずつ：

1. **強さは地理＝運**。どの星系に生まれるかが産出・技術速度の初期値を決める。
   賦存は選択でなく位置から決定論的に生成——「有利な宿命でなく有利な初期条件」。
2. **東西軸は速く、南北軸は遅い**。同一気候帯を東西に連なる回廊では技術・文化の拡散が速く、
   異なる環境帯を南北に跨ぐ回廊では障壁（温度差・媒質差）がある。→ `Corridor` に軸係数を足す。
3. **先進星系が周囲を引き上げる**。技術・文化のピーク値が高い星系から隣接する星系へ自然漏出——
   勢力をまたいだ「文明の重力場」。→ `ResearchRules`/`CultureRules` を拡散場へ接続。
4. **集住が疫病を育てる**。人口密度の高い集積圏は免疫保有者の割合が高く、
   そこから孤立集団へ疫病が到達すると非対称に崩壊する。→ `EpidemicRules`（PEST-1）に免疫補正を追加。
5. **賦存の優位は拡張力の優位に転化する**。高賦存→余剰→人口→入植資源——累積連鎖が地理的勝者を生む。
   → `ColonizationRules` に賦存由来の拡散燃料ルールを接続。
6. **孤立は遅滞を生む**。障壁で隔たれた星系は技術・文化の流入が薄く、
   独自発展するが世界水準から乖離していく。→ `Corridor` 障壁係数×`DiffusionRules` 減衰。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`GalaxyMap`/`GovernanceRules`/`ResearchRules`/`CultureRules`/`ColonizationRules`/
> `EpidemicRules`/`LogisticsRules` を作り直さない**。
> GGS はそれらに**欠落軸を足し、接続する**だけ（additive）。
> タイクン化回避＝高位の決断→エンジン駆動→創発帰結。

### ★★★ 最優先（真の欠落・本書の signature）

#### GGS-1 賦存の地理的非対称（`Endowment`/`EndowmentRules`）
- **賦存ポテンシャル**：`Endowment`（純データ＝`systemId`/`potential`(0..1)/`ecologyType`）＋
  `EndowmentRules`（static 純ロジック）＝
  `Generate(systemId, position, neighbors, seed)` — 位置・近傍類型・シード値から決定論的に potential を算出、
  `ResourceBonus(endowment)` — 星系産出への乗算係数（`ResourceProductionRules.Produce` に接続）、
  `SpreadAffinity(endowmentA, endowmentB)` — 類似生態圏同士は伝播しやすい（GGS-2 への入力）。
- **シード固定**：`GalaxyMap` 生成時に1度だけ計算し `StarSystem.endowment` として保持。
  後から変わらない——地理は初期条件。
- EditMode テスト必須：同シードで再生成が同一値、potential が位置・近傍に依存することを確認。

#### GGS-2 伝播の異方性（`DiffusionRules`/`DiffusionField`）
- **拡散場**：`DiffusionField`（`fieldType`(技術/文化/疫病)/`level`(各星系の蓄積量)）＋
  `DiffusionRules`（static 純ロジック）＝
  `SpreadCoefficient(corridor, fieldType)` — 回廊の「向き×媒質×障壁係数」から拡散速度を算出、
  `Diffuse(map, field, dt)` — 暦Tick（CalendarDispatcher 日次フック）で隣接漏出を処理、
  `PeakLevel(map, field)` — 全星系最高値（引力源の計算に使用）。
- **軸の向き**：`Corridor.axisAngle`（任意・未設定なら0＝等方）と
  生態圏類似度 `EndowmentRules.SpreadAffinity` の積で `SpreadCoefficient` を決める。
- 接続：`CalendarDispatcher` の日次フック（TIME-6）で `GalaxyView.Update` が回す。
- EditMode テスト必須：同類生態圏回廊が異類より速く拡散すること、障壁係数が減衰することを確認。

### ★★ 高（知識・文化の地理的漏出）

#### GGS-3 研究/文化を拡散場へ接続（漏出・流入）
- **知識の重力**：`DiffusionRules.InboundBonus(systemId, field)` ＝
  隣接する星系の `DiffusionField.level` 最大値から現星系への流入量を計算（ピーク差に比例）。
  これを `ResearchRules.ResearchOutput` / `CultureRules.AssimilationPressure` への修正子として加算。
- **勢力をまたぐ漏出**：技術・文化の拡散は `FactionRelations.IsHostile` と独立（壁を越えて流れる）。
  軍事的封鎖は `SupplyRules` を止めるが、`DiffusionField` は遅くなるだけで止まらない。
- **賦存の不利を地理で部分補償**：低賦存でも交差点（多方向の回廊が集まる星系）なら
  漏出が多く入り学習のハブになる——戦略的価値の多様性。
- 接続：`ResearchRules.Tick`（#123）×`CultureRules.Tick`（#194）への additive 修正子。

#### GGS-4 疫病と免疫の非対称（集住→免疫獲得→孤立集団へのショック）
- **免疫蓄積**：`EpidemicRules` に `ImmunityLevel(systemId)` を追加＝
  感染履歴×人口密度（`Province.population` 比例）が高いほど自然回復率↑・再感染率↓。
- **非対称ショック**：孤立星系（`DiffusionField.疫病` が低い＝感染未経験）に疫病が到達したとき
  `GovernanceRules.OutputFactor` / `Province.stability` に大幅ペナルティ（通常の2〜3倍の崩壊速度）。
- **戦略的含意**：密集圏の艦隊は疫病に強く、辺境孤立圏は一撃で崩れる——
  疫病は軍事力を迂回した制圧手段。
- 接続：`EpidemicRules`（PEST-1 #2225）を拡張、`Province`/`GovernanceRules`×免疫係数。
- EditMode テスト必須：高免疫星系の回復率が低免疫を上回ること、非対称ショックの倍率を確認。

### ★ 中（余剰連鎖・障壁係数・世界観開示）

#### GGS-5 賦存→余剰→拡散の連鎖（入植拡散燃料）
- **余剰が入植を後押しする**：`EndowmentRules.SurplusCapacity(endowment, province)` ＝
  potential×OutputFactor から余剰率を算出し、
  `ColonizationRules.Tick` の進捗速度（`ColonizationParams.buildTime`）への係数として渡す——
  高賦存の勢力は入植が速く完成し、銀河をより速く拡大する。
- **低賦存は拡大が鈍い**：逆に貧困環境の勢力は入植が遅れ、
  技術漏出（GGS-3）と余剰不足（GGS-5）が重なって構造的な遅れが生まれる。
- **タイクン化回避**：プレイヤーの判断は「どこへ入植するか」（高賦存星系の選択）のみ。
  速度の差はエンジンが計算する。
- 接続：`ColonizationRules.Tick`（#129）×`EndowmentRules.SurplusCapacity`。

#### GGS-6 伝播障壁としての地形（`Corridor` に障壁係数）
- **障壁係数**：`Corridor.barrierFactor`（float 0..1・既定=1.0＝障壁なし・後方互換）。
  `DiffusionRules.SpreadCoefficient` がこれを乗じて最終拡散速度を算出。
- **孤立の遅滞**：高障壁回廊で繋がれた星系は `DiffusionField.level` が入りにくく、
  技術/文化の流入が薄い——孤立文明は独自だが世界水準から遅れる。
- **GGS-2との関係**：GGS-2 は軸角度×媒質の「方向的係数」、
  GGS-6 は個別回廊の「障壁固定係数」——2つを積む（`DiffusionRules.SpreadCoefficient` で統合）。
- 接続：`Corridor`（既存型に `barrierFactor` フィールド追加・既定値＝後方互換維持）。

#### GGS-7 （lore）地理決定論の開示データ
- **内容**：強さは地理＝運——征服した側の特別な才能ではなく、
  有利な初期条件が累積して現在に至るという世界観認識。
  「孤立は遅滞を生んだが、独自の強みも宿らせた」というバランス視点も盛り込む。
- **接続**：コード新設なし。`DisclosureLedger`（FND-4）への **lore データ入力**のみ。
  `DisclosureEntry`（category=真相/予言）でゲーム内の地理的な「逆転の瞬間」後に解放。

### ❌ 不採用（重複・既存で十分・タイクン化）

| 不採用 | 理由 |
|---|---|
| 個別作物・家畜・栽培化の細目シミュ | タイクン化。賦存は1つのポテンシャル値に集約（マイクロ操作回避） |
| ネームド技術者が工法を保持（人の移動で伝播） | PIL-3（#1092）がカバー。こちらは構造スケール・別軸 |
| 研究ツリーの産出そのもの | `ResearchRules`（#123-127）が既存。拡散場を接続するだけ |
| 同化・分離独立・ナショナリズムの新設 | `CultureRules`（#194）が既存。異方拡散場へ接続するだけ |
| 入植・植民の成立進捗ロジック新設 | `ColonizationRules`（#129）が既存。賦存→拡散燃料の接続のみ |
| 版図の到達性・連結成分の新設 | `LogisticsRules`（#844）が既存。「到達」でなく「伝播速度」＝別軸 |
| 人種・民族の優劣付け | 本書の主旨に反する。賦存差は地理＝運として扱う |
| 疫病拡散エンジン本体の新設 | `EpidemicRules`（PEST-1 #2225）が既存。免疫非対称の接続のみ |
| 星系間通商路・輸送システム新設 | `CommerceRaidingRules`（#95 L-3）が既存。拡散場とは別軸 |

---

## 3. EPIC #1644 の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。
> 既存ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・特定文明の固有設定は不使用、**構造パターンのみ**参考。

> **EPIC = #1644**。GitHub issue 起票済み（#1645〜#1651）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **GGS-1** | #1645 | 賦存の地理的非対称（`Endowment`/`EndowmentRules`＝星系の生産性ポテンシャルを位置・類型・近傍から決定論生成・シード固定） | 新 `Endowment`/`EndowmentRules`（Core・test-first）。`GalaxyMap`×`ResourceProductionRules` |
| **GGS-2** | #1646 | 伝播の異方性（`DiffusionRules`/`DiffusionField`＝軸の向き・媒質一致・障壁で拡散係数が変わる拡散場・暦境界Tick） | 新 `DiffusionRules`/`DiffusionField`（Core・test-first）。`CalendarDispatcher`（TIME-6）×`Corridor` |
| **GGS-3** | #1647 | 研究/文化を拡散場へ接続（先進星系から隣接へ漏出・流入＝賦存の不利を地理で部分補償） | `ResearchRules`/`CultureRules` × `DiffusionField.InboundBonus`（additive 修正子） |
| **GGS-4** | #1648 | 疫病と免疫の非対称（`Epidemic`/`EpidemicRules`＝集住が疫病を育て未接触集団へ非対称ショック＝戦わず人口崩壊） | `EpidemicRules`（PEST-1 #2225）拡張。`Province`×`GovernanceRules` 免疫係数 |
| **GGS-5** | #1649 | 賦存→余剰→拡散の連鎖（高賦存→人口余剰→入植の拡散燃料・地理の不利が拡張力の不利へ） | `ColonizationRules.Tick`×`EndowmentRules.SurplusCapacity`（additive 係数） |
| **GGS-6** | #1650 | 伝播障壁としての地形（`Corridor` に障壁係数・既定なし＝後方互換・孤立が遅滞を生む） | `Corridor.barrierFactor` 追加×`DiffusionRules.SpreadCoefficient` |
| **GGS-7** | #1651 | （lore）地理決定論の開示データ（強さは地理＝運／孤立は遅滞／地理は宿命でなく初期条件） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`GGS-1`（賦存生成＝最も固有で欠落の大きい signature・シード固定純ロジック先行）
→ `GGS-2`（拡散場＝賦存があって初めて意味を持つ伝播エンジン）
→ `GGS-3`（研究/文化を拡散場へ接続＝知識が地理を越えて流れる）
→ `GGS-6`（障壁係数＝GGS-2 の拡散場に個別回廊の係数を加える）
→ `GGS-4`（疫病免疫の非対称＝PEST-1 上の additive 拡張）
→ `GGS-5`（余剰連鎖＝賦存+拡散が整ったあとの入植加速）
→ `GGS-7`（lore＝最後に開示エンジンへデータ入力）

> いずれも既存地理・社会・伝播シミュを**後退させず接続**する additive 設計。
> 「初期条件の不均等が累積し、戦わずして帰結を決める」という構造——
> 軍事的天才でなく地理的有利が帝国を生む、という銀英伝のテーマとも共鳴する。
