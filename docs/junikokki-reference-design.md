# 小野不由美『十二国記』参考設計（EPIC #JNKK）

> 参照元：小野不由美『十二国記』（講談社）。
> 十二の王国が並立する異世界。王は世襲でなく**徳によって選ばれ**（麒麟が選定）、王が失道に陥れば**国土そのものが荒廃する**——天命が物理現象として顕現する世界観。
> 本ドキュメントは当プロジェクト（Ginei）に**欠けているメカニクス構造のみ**を抽出し、EPIC `#JNKK` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・世界観固有設定（麒麟・蓬莱・托卵など）は流用しない。**天命と統治品質の連動・徳治選出・加速崩壊の構造パターンのみ**を参考にする。

---

## 0. なぜ『十二国記』が本システムに役立つか

当プロジェクトは「天命と易姓革命」を `DynastyRules` として純ロジック化済みである。しかし十二国記が描く**天命の最大の特徴**——「失道すると国土が物理的に荒廃する」という直接連動——は既存システムに存在しない。

### 既存カバー範囲

| 既存モジュール | カバー内容 |
|---|---|
| `DynastyRules`/`Regime` (#867) | 天命概念：腐敗→正統性低下→`MandateLost`→`Reform`/`Revolution` |
| `GovernanceRules`/`Province` (#109) | 安定度・統合度・産出 の per-province 管理 |
| `FactionStateRules`/`FactionState` | 国家状態の合成（正統性×合意×結束×希望） |
| `SuccessionRules`/`Organization` (#812) | 英雄死後の継承・カリスマ日常化 |
| `PersonRules`/`Person` (#866) | 適材適所（軍才/文才による役職適性）による選出 |
| `VacancyRules` (#152) | 欠員補充（tier×若い順） |
| `HopeRules`/`Community` (#852) | 希望と末人（末人発火） |
| `ConsentRules`/`Polity` (#836) | 合意・非協力・統治不能 |

### 十二国記が固有に持つ視点 ×当プロジェクトでの欠落

| 十二国記の構造的特徴 | 当プロジェクトの欠落 |
|---|---|
| **失道の加速崩壊**：王が失道に踏み込むと劣化が指数的に加速し破局的転換点を超える | `DynastyRules.Tick` は線形減衰。閾値超えの **非線形加速** が無い |
| **天命の物理的顕現**：失道→国土に災害・荒廃イベントが発生。抽象数値でなく世界の出来事として顕れる | `MandateLost` フラグが立つが **`Province`へのイベント生成連鎖** が無い |
| **徳治選出機構**：軍才でも家柄でもなく **徳スコア**（統治品質・民心・誠実性の合成）で次代の長が選ばれる | `VacancyRules` は tier+若さ、`PersonRules.BestFor` は役職適性。**徳による選出経路** が無い |
| **超長期制度記憶**：不老の官吏・長寿の制度保持者が王朝を跨いで制度知識を蓄積する | `LifecycleRules` は全人物が老死。**不老/長寿型の制度記憶継続** が無い |
| **天命段階のUI公開**：統治品質が 5段階（繁栄→崩壊）として世界地図に可視化される | `Province.stability` は数値。**段階分類 API と可視化フック** が無い |

**結論**：十二国記は既存の天命概念（`DynastyRules`）に **①加速崩壊の非線形モデル、②Province へのイベント連鎖、③徳治選出の第3経路、④超長期制度記憶、⑤段階可視化 API** という5つの欠落軸を与える。いずれも **`DynastyRules` / `GovernanceRules` / `PersonRules` に additive に接続** するだけで実現できる。

---

## 1. 役に立つ視点（要約）

1. **「天命は物理」**：王の不徳が国土の荒廃として直接顕れる——抽象スコアでなく *出来事* として。→ `GovernanceRules.Tick` × `EventEngine` のイベント生成を天命連動で駆動する。
2. **「徳は軍才・家柄と別軸」**：腕が立つこと・家系が良いことと、善く治めることは別の能力。→ `PersonRules` に **徳スコア第3軸** を与え、`VirtueElectionRules` を分岐として追加。
3. **「失道の加速」**：「少し乱れた」から「崩壊寸前」は連続しない——閾値を越えると坂を転がり落ちる非線形。→ 既存 `DynastyRules` の線形モデルを **`MandateBreachRules` で補完**（基準値は変えない）。
4. **「制度は人より長く生きる」**：長寿の官僚は王朝を跨いで技術・慣例・知恵を蓄積し継承する。→ `LifecycleRules` の例外経路 + `PersonRules.Effectiveness` への制度記憶ボーナス。
5. **「統治品質は見える」**：5 段階の天命状態が世界地図上の色として現れ、プレイヤーはどの星系が危機かを一目で読む。→ `MandateLevelRules`（段階分類 pure logic）→ Game 層の色フック。
6. **「十二国の並立」**：独立した王国が同時に動く多極構造。→ 当プロジェクトの `CampaignState` 多勢力盤面と直接共鳴。追加コード不要。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`DynastyRules`/`GovernanceRules`/`PersonRules` を作り直さない**。JNKK はそれらに **欠落軸を足し、接続する**だけ（additive）。基準値は非破壊。

### ★★★ 最優先（真の欠落・十二国記の signature）

#### JNKK 失道の加速崩壊（`MandateBreachRules`）
- `DynastyRules.Tick` は `腐敗 += rate * (1 - 徳 / maxRate)` の線形。十二国記の核心は **閾値超えで加速が指数的に増す非線形**。
- 新規：`MandateBreachRules`（static・Core・test-first）
  - `BreachThreshold(regime)`：失道開始点（正統性の下限）
  - `BreachDepth(regime)`：閾値からの超過量（0 = 閾値ちょうど、1 = 完全崩壊）
  - `BreachVelocity(depth)`：加速係数＝`base × rate^depth`（既定 base=1.0 / rate=2.0・double-exponential）
  - `IsBreach(regime)`・`ProjectCollapse(regime, yearsPerTick)`（崩壊までの推定年数）
- 接続：`DynastyRules.Tick` を呼ぶ `CampaignRules.Tick` が `BreachDepth > 0` のとき `BreachVelocity` を腐敗増速に乗算（実効値パターン・基準値非破壊）

#### JNKK 天命の物理的顕現（`MandateManifestationRules`）
- 失道が深まると `Province` 安定度を引き下げ、災害イベントを生成する。
- 新規：`MandateManifestationRules`（static・Core・test-first）
  - `StabilityDrain(breachDepth)`：`GovernanceRules.Tick` へ加算する安定度引き下げ量（depth 0.0→drain 0 / depth 0.5→drain 5 / depth 1.0→drain 20 の線形補間）
  - `DisasterEventRate(breachDepth)`：`EventEngine` へ渡す 1ターン当たりのカテゴリ「戦闘」以外の危機イベント発火確率（depth 0.5 超で 5%/ターン、depth 1.0 で 30%）
  - `ManifestationFactor(breachDepth)`：合成係数（0.0 〜 1.0）
- 接続：`GovernanceRules.Tick` に `StabilityDrain` を外部加算入口として追加 → `CampaignRules.Tick` が `MandateBreachRules.BreachDepth` を読んで渡す。`EventEngine.Tick` が `DisasterEventRate` を参照。

### ★★ 高（欠落軸を明確に補完）

#### JNKK 徳治選出機構（`VirtueElectionRules`）
- 現在の選出は「tier + 若さ (`VacancyRules`)」または「役職適性 (`PersonRules.BestFor`)」。**徳スコアによる第3経路**を追加する。
- 新規：`VirtueElectionRules`（static・Core・test-first）
  - `VirtueScore(person)`：統治文才(`CivilAptitude`)×`BaselineLoyalty`(`FactionLoyaltyRules`由来・誠実性代用)×希望貢献(`Person.希望バフの実績` = 将来 `HopeRules` 経由）→ 0..100 の pure 計算（全パラメータ受け渡し・モジュール不参照）
  - `SelectByVirtue(candidates)`：`VirtueScore` 最大を選択（同点は若順）
  - `VirtueGate(person, threshold)`：最低ライン未満を除外（王道型選出の gate）
- 接続：`VacancyRules.FillVacancy` のオプションモード（`SelectionMode.ByVirtue`）として追加。`GovernmentRegistry.TryAppoint` はそのまま使用。既存の tier 経路に**干渉しない**（後方互換）。

#### JNKK 超長期制度記憶（`ImmortalTenureRules`）
- 特定の人物（`Person.isImmortal` フラグ）は `LifecycleRules.ShouldDieOfAge` の対象外。長寿で蓄積した制度知識が実効能力ボーナスになる。
- 新規：`ImmortalTenureRules`（static・Core・test-first）
  - `IsImmortal(person)`：`person.isImmortal` null-safe
  - `InstitutionalMemoryYears(person, currentYear)`：`currentYear - person.birthYear`（上限 `MaxMemoryYears` = 300）
  - `MemoryBonus(person, currentYear)`：`Clamp(years / MaxMemoryYears, 0, 1) × maxBonus`（既定 maxBonus = 20）→ `PersonRules.Effectiveness` の `CivilAptitude` に加算（実効値パターン・基準値非破壊）
  - `IsExemptFromMortality(person)`：`IsImmortal(person)` の結果
- 接続：`LifecycleRules.ShouldDieOfAge` が `IsExemptFromMortality` を先チェック。`PersonRules.Effectiveness` の aptitude 計算に `MemoryBonus` を加算入口追加。`VacancyRules.ClearDeparted` が不老者をスキップ。`Person` に `isImmortal: bool`（既定 false = 後方互換）フィールドを追加。

#### JNKK 天命段階 API（`MandateLevelRules`）
- Province × Regime の組み合わせから天命状態を5段階で返す pure logic。UI 層はこれを読んで色表現する（`MandateLevelRules` 自体は色を知らない）。
- 新規：`MandateLevelRules`（static・Core・test-first）
  - `enum MandateLevel { 繁栄, 安定, 動揺, 荒廃, 崩壊 }`
  - `Evaluate(province, regime)`：安定度×正統性の合成スコア → Level 判定（閾値は const で調整可）
  - `AggregateRealmMandate(provinces, regime)`：勢力全 Province の最頻/最悪を返すサマリ
  - `IsVisiblyUnstable(level)`：動揺以下で true（UI 強調の判定窓口）
- 接続：`GameLayer` の `GalaxyView`（星系色）・`SystemDetailPanel`（安定度バーの色帯）が `Evaluate` を呼ぶ（Game 層から Core を読むだけ、逆参照なし）。

### ★ 中（lore 補完・コード不要）

#### JNKK lore 入力（世界観開示データ）
- 「統治の徳が国土に顕れる」「正統性のない王は選ばれない」「長い歴史の中で制度が人を超える」という世界観の骨子を `DisclosureLedger` エントリとして入力する。
- コード新設なし。`DisclosureLedger`（FND-4）への**データ入力のみ**。CCX-6 世界観 codex 退避方針に一貫。

---

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 天命・正統性の概念そのもの | **`DynastyRules.MandateLost`/`Regime.正統性` がカバー**。JNKKは加速モデルだけ足す |
| 易姓革命・王朝交代 | **`DynastyRules.Revolution` がカバー** |
| 民心・合意 | **`ConsentRules`/`Polity` がカバー** |
| 希望と末人・ロンドン派 | **`HopeRules`/`Community` がカバー** |
| 人物の継承・後任補充 | **`SuccessionRules`/`VacancyRules` がカバー** |
| 麒麟の固有メカ（神獣・選定の演出） | **作品固有の設定**。ゲームメカニクス化しない（著作権・タイクン化回避） |
| 転生・异世界渡航の fixed story | **プロット固有**。純ロジックに汲み取れる構造なし |
| 12国の固有政体名・文化名 | **固有名**。既存 `FactionData.ideology` + `GovernanceRules` で代替 |

---

## 3. EPIC #JNKK の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。既存モジュールは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

**EPIC = #2268**。GitHub issue 起票済み（#2272〜#2290）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **JNKK-1** | #2272 | 失道の加速崩壊（`MandateBreachRules`・閾値超えの非線形 velocity） | `DynastyRules.Tick` の乗算係数として additive 接続 |
| **JNKK-2** | #2275 | 天命の物理的顕現（`MandateManifestationRules`・`StabilityDrain`×`DisasterEventRate`） | `GovernanceRules.Tick` 引き下げ入口 × `EventEngine.Tick` 発火率 |
| **JNKK-3** | #2279 | 徳治選出機構（`VirtueElectionRules`・`VirtueScore`×`SelectByVirtue`） | `VacancyRules.FillVacancy` の第3モード。tier 経路は後方互換 |
| **JNKK-4** | #2283 | 超長期制度記憶（`ImmortalTenureRules`・`isImmortal`×`MemoryBonus`） | `LifecycleRules` 例外 × `PersonRules.Effectiveness` 加算 |
| **JNKK-5** | #2286 | 天命段階 API（`MandateLevelRules`・`enum MandateLevel` × `Evaluate`） | `GalaxyView`/`SystemDetailPanel` の色フックに公開 |
| **JNKK-6** | #2290 | （lore）世界観の開示データ（天命の物理法則・徳治選出・長寿制度） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順

`JNKK-1 → JNKK-2`（失道の非線形崩壊＝最も固有で欠落の大きい signature、物理顕現はその直接帰結）→ `JNKK-3`（徳治選出＝選出経路の第3軸、独立性が高い）→ `JNKK-4`（長寿制度記憶＝人物層の拡張）→ `JNKK-5`（段階 API は 1-4 が整ってから集約）→ `JNKK-6`（lore は任意タイミング）

> いずれも既存 `DynastyRules`/`GovernanceRules`/`PersonRules` を**後退させず接続**する additive 設計。`CampaignRules.Tick` の 1 呼び出し場所で全モジュールが繋がる。
