# アーレント『全体主義の起源』参考設計（EPIC #TOTL）

> 参照元：ハンナ・アーレント著『全体主義の起源』（1951）。反ユダヤ主義・帝国主義・全体主義の三部作分析。
> 全体主義は**腐敗や暴君から生まれるのでなく、原子化した孤独な大衆から生まれる**——という逆説的診断。
> 本ドキュメントは、当プロジェクト（Ginei）にとって**役に立つ視点**だけを抽出し、EPIC `TOTL` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**社会・政治メカニクスの構造パターンのみ**を参考にする。

---

## 0. なぜ「全体主義の起源」が本システムに役立つか

### 既存カバー範囲

| 既存システム | カバー範囲 |
|---|---|
| `SecurityRules` | 治安弾圧・クーデター検出・弾圧の支持ペナルティ |
| `ConsentRules`/`Polity` | 権力は借り物・協力離脱で統治不能 |
| `Organization`/`SuccessionRules` | 組織の結束・英雄死後の制度化 |
| `DynastyRules`/`Regime` | 正統性崩壊・腐敗・易姓革命 |
| `MovementRules`/`NonviolenceRules` | 可視化された弾圧→支持転換（情報戦の反転） |
| `HopeRules`/`Community` | 希望の枯渇→末人（ロンドン派）の台頭 |
| `CoupRules` | クーデター種別・成功率・軍政関係 |
| `FactionStateRules`/`FactionState` | 国家状態の合成（Regime/Polity/Organization/Community） |
| `EspionageRules` | 情報取得・妨害・発覚リスク |

**しかし、これらは「腐敗・独裁・合意崩壊」という古典的政治不安定のモデル**であり、アーレントが固有に診断した以下が**欠けている**：

### 欠落軸（アーレント固有）

| アーレントが固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **原子化（中間団体の破壊→孤立個人）** | `Polity`/`Organization` は単一団体の内部状態を扱うが、社会全体の「中間団体密度（civil society）」を測る軸がない。破壊が進むほど個人は孤立し全体主義的捕捉率が上がる |
| **テロの原理化（道具でなく目的・自己増殖ループ）** | `SecurityRules.DissentSuppression` は道具的弾圧。テロが自己目的化して弾圧者自身も粛清に呑み込まれるロックオン状態がない |
| **帝国主義の還流（辺境暴力→国内急進化）** | 征服・辺境支配で行使した行政暴力が本国へ還流し政体を急進化させる因果メカニズムがない |
| **余剰性（使い捨て人口の政治心理）** | `HopeRules` で末人を扱うが、「経済的にも政治的にも不要とされた人口」という使い捨て感覚が運動への吸収を高めるループがない |
| **無権利者の創出（国籍剥奪・法外の人口）** | `CaptivityRules` は捕虜化を扱う。組織的に国籍/保護圏を剥奪された無国籍・無権利者クラス（何勢力にも属さない法外人口）がない |

**結論**：アーレントは当プロジェクトの政治・社会シミュ層に**「全体主義への滑り台」という動態的構造**を与える。①**原子化** ②**テロの原理化** ③**帝国主義の還流** ④**余剰性** ⑤**無権利者**という5つの欠落軸。実装することで `FactionStateRules` の崩壊経路が「腐敗型（王朝崩壊・`DynastyRules`）」に加えて**「全体主義型（原子化経由の大衆運動捕捉）」**を獲得する。

---

## 1. 役に立つ視点（要約）

1. **全体主義は悪人からでなく、結びつきを断たれた孤独な大衆から生まれる**。組織的弾圧でなく「中間団体の静かな解体」こそが最も危険。`Organization.cohesion` の単一体版を超えた社会密度を要求する。
2. **帝国主義は全体主義の前段階**。辺境で行使した「人間を消耗品として扱う行政暴力」が本国に還流し、大衆の余剰感覚と結びつく。→ `WarGoalRules` × `FactionStateRules` の因果連結軸。
3. **テロは手段から目的へ移行すると自己増殖する**。独裁者自身も「歴史/自然の法則の執行者」に還元され止められない。→ `SecurityRules` の非線形拡張。
4. **余剰人口（disposable people）は全体主義の原料**。「誰にも必要とされない」感覚が運動への吸収を可能にする。→ `DemographicsRules` × `HopeRules` の接続。
5. **無権利者（国籍なき者）は政治共同体の例外**。権利を持つには所属が必要＝「権利を持つ権利（the right to have rights）」が基礎。→ `CaptivityRules` の法外状態拡張。
6. **権力の本質は結社（人々が共に行動する力）**。弾圧ではなく原子化が真の権力破壊手段。→ `ConsentRules` の基盤に「結社密度」軸を足す。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`SecurityRules`/`ConsentRules`/`Polity`/`Organization`/`FactionStateRules` を作り直さない**。TOTL はそれらに**欠落軸を additive に足す**（接続のみ）。

### ★★★ 最優先（真の欠落・アーレントの signature）

#### TOTL 原子化（`AtomizationRules`/`CivilSociety`）

中間団体（教会・組合・地域結社・政党派閥）の密度を `civilSocietyHealth`（0..1）として定量化。弾圧・体制変化・長期孤立で低下。

- **`civilSocietyHealth` が低いほど `susceptibility`（全体主義的捕捉率）が高まる**：
  - `Organization.cohesion` の初期値係数（低密度＝中間団体が弱い→組織が結束しにくい）
  - `ConsentRules.ControlStrength` の下限引き下げ（孤立個人は非協力を組織化できない）
  - `CoupRules.CoupRisk` の種別分岐（密度低下→大衆運動型クーデター確率上昇）
- `FactionStateRules.Stability` の構成要素に `civilSocietyHealth` を追加。
- 新規：`CivilSociety` struct＋`AtomizationRules`（static, test-first, Core）。

#### TOTL テロの原理化（`TerrorPrincipleRules`）

`SecurityRules` の `repression` レベルが閾値（`TerrorThreshold`）を超えると**テロ原理化状態**へ移行：

- 通常弾圧は `RepressionSupportPenalty` で制御可能（道具的）。
- 原理化後は弾圧コストが消え、**組織内粛清**（`Organization.cohesion` 侵食）＋`DynastyRules.corruption` 加速を呼ぶ。
- **自己増殖ループ**：粛清→恐怖→組織的忠誠の演技→次の粛清（不動点反復に収束しない）。
- 離脱条件：`LoyaltyRules.ResolveCascade` 全員裏切り or 外部勢力制圧 or `FactionState.IsCollapsing`。
- 接続：`SecurityRules`（拡張）×`DynastyRules`×`CoupRules`×`Organization`。
- 新規：`TerrorPrincipleRules`（static, test-first, Core）。

### ★★ 高（戦略・社会シミュへの接続）

#### TOTL 帝国主義の還流（`ImperialBlowbackRules`）

征服・辺境支配で行使した行政暴力が本国へ還流し政体を急進化させる：

- 高い `WarWeariness`（`WarGoalRules`）× 「植民地型占領」期間が閾値超 → `FactionState.Organization.cohesion` を毎ターン侵食。
- `DynastyRules.Regime.corruption` が加速（辺境の行政暴力文化が中央へ）。
- `AtomizationRules.CivilSociety.civilSocietyHealth` の低下も伴う（長期戦争で中間団体解体）。
- 接続：`WarGoalRules`×`DiplomacyRules`×`LogisticsRules`×`FactionStateRules`。
- 新規：`ImperialBlowbackRules`（static, test-first, Core）。

#### TOTL 余剰性（`SuperfluousnessRules`）

「経済的・政治的に不要とされた人口」の規模を `superfluousRatio`（0..1）で計算：

- `DemographicsRules.DependencyRatio` 高（オーナス）＋`FiscalRules.TaxRevenue` 低下（不況）→ `superfluousRatio` 上昇。
- `superfluousRatio` が高いほど：
  - `HopeRules.Community.hope` の底上げ困難（「無駄な存在」感が希望注入を打ち消す）
  - 急進的大衆運動型クーデター（`CoupRules`）の吸収率上昇
  - `AtomizationRules.susceptibility` 係数に乗算（余剰感×孤立＝最高捕捉率）
- 接続：`DemographicsRules`×`FiscalRules`×`HopeRules`×`AtomizationRules`。
- 新規：`SuperfluousnessRules`（static, test-first, Core）。

### ★ 中（拡張・lore）

#### TOTL 無権利者の創出（`StatelessnessRules`）

組織的な国籍・保護圏剥奪で生じる「無権利者（stateless）」人口クラスを `CaptivityRules` の拡張として実装：

- `CaptiveStatus` に新状態 `stateless`（無国籍・無権利・法外）を追加。
- 無権利者は勢力なし・保護なし・交渉対象外。`CaptivityRules.Recruit` の対象だが `RecruitChance` 最低（アイデンティティ剥奪済み）。
- `GovernanceRules` の安定度計算に「管轄内の無権利者比率→不安定化係数」。
- 接続：`CaptivityRules`（拡張）×`GovernanceRules`×`ColonizationRules`。
- 新規：`StatelessnessRules`（static, test-first, Core）。

#### TOTL（lore）世界観の開示データ

「全体主義は腐敗でなく孤独から生まれる」「権力の本質は結社（共に行動する力）」「悪の凡庸性（命令への服従が悪を生む）」「帝国主義は全体主義の前段階」「権利を持つ権利（the right to have rights）」。

- コード新設なし：`DisclosureLedger`（FND-4）への**lore データ入力**のみ。
- 条件例：`AtomizationRules.civilSocietyHealth < 0.3` かつ `TerrorPrincipleRules.IsLocked` → 「全体主義の閾値」開示解放。世界観EPIC（秘史/啓蒙/天命喪失/エンディング）と連鎖。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 反ユダヤ主義・人種差別の固有設定 | 固有歴史設定（ゲームとして不適切）。一般化した「憎悪の政治利用」は `EventEngine` イベントで十分 |
| プロパガンダによる現実解体の独立ルール | `EspionageRules.InfoGain`×`EventEngine` で代替可能。新モジュール不要 |
| 一党支配制度の詳細設計 | `PartyRules`/`GovernmentRegistry`/`CoupRules` で既にカバー |
| 収容所の物理設置・マイクロ管理 | タイクン化（マイクロ操作）・プレイ体験として不適切。`CaptivityRules.stateless` で十分 |
| 全体主義国家の経済計画 | `FiscalRules`/`ResourceProductionRules` で既にカバー |
| 大衆運動の独立モジュール新設 | `MovementRules`/`NonviolenceRules` が基盤を提供。係数接続で十分 |

---

## 3. EPIC #TOTL の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #1513**。GitHub issue 起票済み（#1516・#1519・#1522・#1524・#1526・#1529）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **TOTL-1** | #1516 | `AtomizationRules`/`CivilSociety` — 中間団体密度の定量化と susceptibility 係数 | `Organization`×`ConsentRules`×`CoupRules`×`FactionStateRules`。全体主義的捕捉率の基盤 |
| **TOTL-2** | #1519 | `TerrorPrincipleRules` — テロの原理化（道具→目的・粛清自己増殖ループ） | `SecurityRules`×`DynastyRules`×`CoupRules`×`Organization` 拡張 |
| **TOTL-3** | #1522 | `ImperialBlowbackRules` — 帝国主義の還流（辺境暴力→国内急進化フィードバック） | `WarGoalRules`×`DiplomacyRules`×`FactionStateRules`×`LogisticsRules` |
| **TOTL-4** | #1524 | `SuperfluousnessRules` — 余剰性（使い捨て人口が運動吸収率を上げる） | `DemographicsRules`×`FiscalRules`×`HopeRules`×`AtomizationRules` |
| **TOTL-5** | #1526 | `StatelessnessRules` — 無権利者の創出（国籍剥奪・法外人口クラス） | `CaptivityRules`拡張×`GovernanceRules`×`ColonizationRules` |
| **TOTL-6** | #1529 | （lore）世界観の開示データ（全体主義は孤独から/権力は結社/悪の凡庸性） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順

`TOTL-1`（AtomizationRules＝基盤・他の全issueが依存）→ `TOTL-2`（テロの原理化＝最もアーレント固有の signature）→ `TOTL-3`（帝国主義還流＝戦略レイヤーとの接続）→ `TOTL-4`（余剰性＝社会シミュとの接続）→ `TOTL-5`/`TOTL-6`（拡張・lore）。

> いずれも既存システムを**後退させず接続**する additive 設計。`FactionStateRules` の崩壊経路に「全体主義型」を追加する。
