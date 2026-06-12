# ダンテ『神曲』参考設計（EPIC #DANT）

> 参照元：ダンテ・アリギエーリ『神曲（Divina Commedia）』（1308–1321）。地獄篇(Inferno)・煉獄篇(Purgatorio)・天国篇(Paradiso)の三部構成の巡礼的叙事詩。**罪の分類・応報対称性（contrapasso）・段階的浄化・愛の秩序原理**という政治思想の構造を持つ。
> 本ドキュメントは当プロジェクトにとって**役に立つ構造パターン**だけを抽出する。
> 著作権注意：固有名・文章・キャラクター・固有設定は一切流用せず、**政治メカニクス／世界観の構造パターンのみ**を参考にする。

---

## 0. なぜ「神曲」が本システムに役立つか

### 既存カバー範囲

| 既存モジュール | カバー範囲 |
|---|---|
| `JusticeRules`/`JusticeView` (#918-923) | 5つの正義観×行動→正統性デルタ |
| `DynastyRules`/`Regime` (#867) | 腐敗→正統性喪失→改革/易姓革命サイクル |
| `HopeRules`/`Community` (#852-856) | 希望/絶望・末人（ロンドン派）発火 |
| `Organization`/`SuccessionRules` (#812) | 英雄死後の組織存続・制度化 |
| `CoupRules` (#215-219) | クーデター型・成功確率・後始末 |
| `ConsentRules`/`Polity` (#836-838) | 被支配者の合意・非協力・統治不能 |
| `ReligionRules` (#172-175) | 改宗圧力・異端・聖戦 |
| `CultureRules`/`Culture` (#194) | 同化圧力・分離独立・ナショナリズム |
| `FactionStateRules` | 社会シミュ層の統合（Stability/IsCollapsing） |
| `DisclosureLedger` (FND-4 #495) | 秘史の連鎖開示エンジン |

### 神曲が固有に持つ視点 × 当プロジェクトでの欠落

| 神曲の固有視点 | 当プロジェクトでの欠落 |
|---|---|
| **応報対称性（contrapasso）** — 罰の形が罪の形を映す | `DynastyRules.MandateLost` は天命喪失を出すが、**失墜の形（軍事崩壊/財政破綻/情報クーデター/外交孤立）が支配の型（軍事過膨張/収奪/欺罔/孤立）と対応する**写像がない |
| **道徳地形 — 罪の累積パターンが帰結型を決める** | `JusticeRules` は単一行動の正統性増減を評価するが、**累積的な支配パターンを罪の類型に分類し帰結モードを決定する**層がない |
| **煉獄 — 能動的・段階的な贖罪** | `GovernanceRules.integration` は時間で回復するが、**特定の行動（謝罪/補償/制度改革）を経て正統性を能動的に取り戻す**パスがない |
| **愛の引力 — 徳ある存在は征服なしに人を引き寄せる** | `CultureRules.AssimilationPressure` は占領後の同化。**高い正統性×徳×希望が征服なしに近隣民意を引き寄せる**メカニクスがない |
| **案内役の知識ゲート — 領域を変えれば案内役も変わる** | `PersonRules.Aptitude` は役職適性の計算。**危機種別ごとに専門案内役の有無が政策オプションの存否を決める**ゲートがない |

**結論**：神曲は当プロジェクトの政治シミュに**①応報対称性 ②道徳地形（分類入力層） ③段階的贖罪 ④愛の引力 ⑤知識ゲート**の5軸を与える。すべてadditive（既存を後退させない）。中心的価値は**「どう支配したかが、どう滅ぶかを決める（contrapasso）」**と**「滅んでも行いで復権できる（煉獄）」**の2本。

---

## 1. 役に立つ視点

1. **「失墜の形は支配の形を映す」** — 軍事的過膨張は軍事的に崩れ、収奪支配は財政で尽き、欺罔外交は孤立で滅び、背信は内部裏切りで終わる。→ `DynastyRules`×`CoupRules` に応報の型を与える（DANT-1/2）。
2. **「罪は深度ある3層に分類される」** — 過度（節制の失敗）< 暴力 < 欺罔 < 背信の深度構造が帰結の重さを決める。→ 帰結型を決める `MoralTopographyRules` の設計根拠（DANT-2）。
3. **「煉獄は能動的な回復を要求する」** — 時間だけでは昇れない。具体的な行いが要る。→ 接収補償/制度改革/外交修復という**行動条件**付きの正統性回復（DANT-3）。
4. **「愛（徳ある統治）は星を引き寄せる」** — 征服でなく「あの国のようになりたい」という求心力が世界を動かす。→ 高正統性×高希望の勢力が近隣民意を引く `AspirationRules`（DANT-4）。
5. **「領域を変えれば案内役も変える」** — 武将は経済改革を、経済官僚は軍事再建を、それぞれ単独で案内できない。→ 危機種別に専門家の有無が政策オプションを絞る（DANT-5）。
6. **「旅は層状の開示」** — 地獄→煉獄→天国を通じて真理が段階的に開示される。→ `DisclosureLedger` (FND-4) へのloreデータ入力（コード新設なし、DANT-6）。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`DynastyRules`/`JusticeRules`/`CoupRules`/`ConsentRules` を作り直さない**。DANT はそれらに**欠落軸を追加し接続する**（additive）。

### ★★★ 最優先（神曲の signature・真の欠落）

#### DANT 応報対称性 `ContrapassoRules` — 失墜の形は支配の形を映す

- `enum FallPattern { 軍事崩壊, 財政崩壊, 情報クーデター, 外交孤立, 内部背信 }`
- `ContrapassoRules.PredictFallPattern(DomainFlaw) → FallPattern`：支配の型→失墜モードの写像
- `ContrapassoRules.ApplyContrapasso(FactionState, FallPattern)`：失墜の初期化処理（崩壊パスの分岐入力）
- 接続：`DynastyRules.MandateLost` 発火時に `FallPattern` を付与 → `CoupRules.CoupType` / `FiscalRules` / `DiplomacyRules` の崩壊パスを型で分岐させる
- 純ロジック・EditModeテスト必須

#### DANT 道徳地形 `MoralTopographyRules` — 支配パターンを罪の類型に分類する（DANT-1の入力層）

- `enum DomainFlaw { 節制過度, 暴力, 欺罔, 背信 }`（深度順・contrapasaoの4水準）
- `MoralTopographyRules.Classify(FactionState) → DomainFlaw`：`Regime`/`Polity`/`FiscalState` の指標群から主要な欠陥類型を判定
- `MoralTopographyRules.SeverityTier(DomainFlaw) → int(1..3)`：帰結の深刻度
- 接続：`FactionStateRules.Tick` 後に評価 → `ContrapassoRules` の入力 / `JusticeRules` の `LegitimacyDelta` と直交（単一行動評価 vs 累積パターン分類）
- 純ロジック・EditModeテスト必須

#### DANT 煉獄/段階的贖罪 `RedemptionRules` — 行動条件付きで正統性を回復する

- `PenanceState { penalty: DomainFlaw, completedActs: HashSet<PenanceAct>, legitimacyGained: float }`
- `enum PenanceAct { 公開謝罪, 資産補償, 制度改革, 外交修復, 軍縮宣言 }`
- `RedemptionRules.RequiredActs(DomainFlaw) → IReadOnlyList<PenanceAct>`：罪の類型に対応する必須行為セット
- `RedemptionRules.Commit(PenanceState, PenanceAct)`：行為の実行・進捗反映
- `RedemptionRules.IsRedeemed(PenanceState) → bool`：完了判定（必須アクト全達成）
- `RedemptionRules.LegitimacyBonus(PenanceState) → float`：回復量（完了度比例）
- 接続：`Regime.legitimacy` 加算 / `Organization.cohesion` の下限確保 / `DynastyRules.Reform`（制度的）と相互補完（贖罪=道徳的回復）
- 純ロジック・EditModeテスト必須

### ★★ 高（徳の引力）

#### DANT 愛の引力 `AspirationRules` — 徳ある統治が征服なしに近隣民心を引き寄せる

- `AspirationRules.AttractionFactor(source: FactionState, targetProvince: Province) → float (0..1)`：source の `Stability`×`Regime.legitimacy`×`Community.hope` の幾何平均で引力強度を算出
- `AspirationRules.ApplyPull(source, target, dt)`：target省の `nativeIdeology` を source 思想方向へ微シフト（`GovernanceRules.IdeologyModifier` に相乗）
- `AspirationRules.DiplomaticInitiative(source: FactionState) → float`：引力が閾値を超えると `DiplomacyRules.CanProposeAlliance` の成功確率に加算
- 接続：`GovernanceRules`/`HopeRules`/`Regime`/`DiplomacyRules` と連結。`CultureRules.AssimilationPressure`（占領後の同化）とは別経路（征服なしの引力）
- 純ロジック・EditModeテスト必須

### ★ 中（案内役ゲート）

#### DANT 危機案内役ゲート `CrisisGuideRules` — 危機種別に専門案内役の有無が政策オプションを変える

- `enum CrisisKind { 軍事危機, 財政危機, 政治危機, 外交危機 }`
- `CrisisGuideRules.RequiredDomain(CrisisKind) → PersonDomain`：必要な専門能力（`PersonRules.MilitaryAptitude`/`CivilAptitude`/政治任用）
- `CrisisGuideRules.HasGuide(CrisisKind, IEnumerable<Person>) → bool`：適任案内役の在席確認
- `CrisisGuideRules.OptionCount(CrisisKind, persons) → int`：案内役有無で政策オプション数を変える（案内役なし=縮退）
- 接続：`PersonRules`/`OfficeRules`/`GovernmentRegistry` / `EventEngine.EventChoice` の選択肢生成ロジックと連結
- 純ロジック・EditModeテスト必須

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 宗教的天国/救済論そのもの | `ReligionRules`/`HopeRules` がカバー。loreのみ（DANT-6） |
| 地獄的永続懲罰 | `CoupRules`/`LifecycleRules`（処断・死亡）がカバー |
| 案内役の人格・固有関係性 | 固有キャラ設定不使用。構造パターンのみ（DANT-5） |
| 神学的宇宙論・天球配置 | 2D宇宙ゲームとして既存宇宙地図があり不適 |
| 詩的言語・詩節構造 | ゲームメカニクスに不適。lore文体のみ参考（DANT-6） |
| 三界「旅」構造そのもの | `DisclosureLedger` の連鎖開示で十分。新規旅システム不要 |
| 地獄の地形・地理的詳細 | タイクン化の温床。背景loreのみ |

---

## 3. 子Issue表（DANT）＋推奨着手順

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。既存政治・社会シミュは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **DANT-1** | #2203 | 応報対称性 `ContrapassoRules` — 失墜の形は支配の形を映す | 新 `ContrapassoRules`/`FallPattern`。`DynastyRules.MandateLost`×`CoupRules.CoupType` に失墜型を分岐入力 |
| **DANT-2** | #2205 | 道徳地形 `MoralTopographyRules` — 支配パターンを罪の類型に分類する（DANT-1入力層） | 新 `DomainFlaw`/`MoralTopographyRules`。`FactionStateRules`/`DynastyRules` から累積パターンを分類 |
| **DANT-3** | #2208 | 煉獄/段階的贖罪 `RedemptionRules` — 行動条件付きで正統性を回復する | 新 `PenanceState`/`RedemptionRules`。`Regime.legitimacy`×`Organization`×`DynastyRules.Reform` に接続 |
| **DANT-4** | #2211 | 愛の引力 `AspirationRules` — 徳ある統治が征服なしに近隣民心を引き寄せる | 新 `AspirationRules`。`GovernanceRules`×`HopeRules`×`Regime`×`DiplomacyRules` に接続 |
| **DANT-5** | #2213 | 危機案内役ゲート `CrisisGuideRules` — 危機種別に専門案内役の有無が政策オプションを変える | 新 `CrisisGuideRules`/`CrisisKind`。`PersonRules`×`OfficeRules`×`EventEngine` に接続 |
| **DANT-6** | #2215 | （lore）開示エンジンへの世界観データ — 判決構造・贖罪ルート・愛の宇宙論 | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`DANT-2`（道徳地形＝分類入力層を先に確立）→ `DANT-1`（応報対称性＝失墜分岐を接続）→ `DANT-3`（贖罪＝回復路）→ `DANT-4`（愛の引力＝平和的拡張力）→ `DANT-5`（案内役ゲート）→ `DANT-6`（lore）。

> いずれも既存政治・社会シミュを**後退させず接続**するadditive設計。`DynastyRules`/`JusticeRules` に「応報」と「贖罪」の軸を与え、徳ある統治が外交的求心力を生む回路を完成させる。
