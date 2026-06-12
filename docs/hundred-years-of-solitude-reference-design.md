# マルケス『百年の孤独』参考設計（EPIC #HYOS）

> 参照元：ガブリエル・ガルシア＝マルケス『百年の孤独』（1967）。
> ある一族が辺境の町を開拓し、7世代・百年のあいだ繁栄・内紛・衰退を繰り返し、
> 最後に「最初の者は木に縛られ、最後の者は蟻に食われる」という運命を閉じる——
> **同じ名前・同じ性格・同じ失敗の世代反復**と**孤独が宿命として流れる一族の物語**。
> 本ドキュメントは、当プロジェクト（Ginei）にとって**役に立つ構造パターン**のみを抽出し、
> EPIC `#HYOS` として issue 化する提案。
> **著作権注意：固有名・文章・キャラクター・固有設定は流用せず、
> 世代システム・家系ロジック・運命アーキタイプの構造パターンのみを参考にする。**

---

## 0. なぜ「百年の孤独」が本システムに役立つか

当プロジェクトは人物ライフサイクル層（LIFE-1〜7）を大量に保有している：

| 既存（人物・世代系） | カバー範囲 |
|---|---|
| `LifecycleRules`/`Calendar`（LIFE-1/2） | 年齢・死亡・加齢。暦の年境界で提督が老いて死ぬ |
| `VacancyRules`（LIFE-2） | 死亡/捕虜で空いた席に後任を補充 |
| `SuccessionRules`/`Organization`（#812） | カリスマ死後の組織存続。制度化ぶんは残り個人カリスマぶんは消える |
| `DynastyRules`/`Regime`（#867） | 王朝サイクル：腐敗→天命喪失→改革/易姓革命で再起動 |
| `GrowthRules`/`Growth`（#537〜543） | 個人の成長曲線（経験→実効能力）。4アーキタイプ |
| `RetirementRules`（#530〜536） | 階級別停年・アップオアアウト・戦時召集 |
| `CareerPipelineRules`（LIFE-5/6/7） | 武/官/技の3系統。卒業年・席次・同窓閥 |
| `Person`/`PersonRules`（#866） | 軍人/文民の適材適所。`ICharacter` 共通窓口 |
| `AdmiralData`（構造化命名） | FullName/ShortName/EpithetName/RegnalSuffix など名前の構造 |
| `DisclosureLedger`（FND-4） | 秘史の連鎖開示。条件成立→前提満たした先行開示が連鎖 |
| `EventEngine`（#116） | 条件発火→通知/選択肢→効果のデータ駆動エンジン |

**しかし、これらは「個人の一生」「組織の継承」「政体の循環」を扱うものであり、
百年の孤独が固有に持つ以下が欠けている**：

| 百年の孤独が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **血統台帳**：誰が誰の子か・何世代目か・同じ名前が何度現れたかを追跡するデータ構造 | `Person` は個人単位。**血縁グラフ・世代深度・名前の出現回数**を永続保持する構造が無い |
| **名前運命アーキタイプ**：同じ名前の継承者は似た能力傾向を持つ（「ホセ・アルカディオ」型＝肉体・情熱、「アウレリャノ」型＝知性・孤独） | `AdmiralData` の命名は構造化されているが、**同名系統に共通の能力傾向バイアス**を与える仕組みが無い |
| **世代忘却**：直前世代が学んだことは次世代が繰り返す。制度記憶は世代交代で減衰する | `Organization.institutionalInterest`（省益）はあるが、**世代境界を跨いだ知識の逓減**はモデルされていない |
| **歴史共鳴検知**：「また同じことが起きている」をシステムが検出し、反復の旗を立てる | 因果連鎖は線形。**パターンの反復を検出してイベントを発火する**メタ機構が無い |
| **孤独の系統的帰結**：一族は繁栄の絶頂でもつながりを失い孤立する。これはランダムな不運でなく構造的な傾向 | `ConsentRules`/`HopeRules` はあるが**血統単位の孤立傾向**を追跡するモデルが無い |

**結論**：百年の孤独は当プロジェクトのライフサイクル層に**「血統」という永続単位**と、
①**血縁グラフ** ②**名前運命アーキタイプ** ③**世代忘却** ④**歴史共鳴検知**
という4つの欠落軸を与える。
特に「同じ名前の者は似た運命を辿る」というメカニズムは、
銀英伝的な「英雄の世代交代」をゲームとして深みのある体験にする。

---

## 1. 役に立つ視点（要約）

百年の孤独の世界観を、**本システムに効く形**で1行ずつ：

1. **名前は運命を宿す**。同じ名前の者は異なる世代に生まれても同じ傾向を持ち、同じ失敗を繰り返す。→ `AdmiralData` の名前系統に**能力傾向バイアス**を与え、プレイヤーが「また同じ名前の者が…」と感じる設計。
2. **世代は前世代を忘れる**。祖父が命がけで学んだ教訓を孫は知らない。→ **制度記憶の世代間逓減**＝`Organization.institutionalInterest` に世代境界での喪失率を足す。
3. **歴史は螺旋状に繰り返す**。全く同じではないが、同型のパターンが再出現する。→ **歴史共鳴検知**＝過去イベント記録と照合し「このパターンは X 世代前にも起きた」を `EventEngine` で発火。
4. **血統は積み上がる**。家系の評判・功績・汚点は個人を超えて一族に蓄積される。→ **`Lineage` 台帳**＝誰が誰の子か・累計功績・汚点・名前の反復回数を永続保持。
5. **孤独は構造的帰結**。孤立は個人の失敗でなく血統の傾向として現れる。→ `Organization.individualCharisma` の「属人組織は英雄と滅ぶ」と接続＝孤独傾向を持つ血統は制度化が遅れる。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`LifecycleRules`/`Person`/`Organization`/`DynastyRules`/`DisclosureLedger` を作り直さない**。
> HYOS はそれらに**血統という新しい永続単位と4欠落軸を additive に追加する**だけ。

### ★★★ 最優先（真の欠落・百年の孤独の signature）

#### HYOS 血統台帳 `Lineage`/`LineageRules`（基盤）

- `Lineage`（`[Serializable]` 純データ）：`lineageId`/`lineageName`（家名）/`founderPersonId`（始祖）/
  `parentOf`（`Dictionary<string,string>` person→parent）/`generation`（person→世代数）/
  `nameCount`（名前→出現回数）/`merits`（累計功績）/`disgraces`（累計汚点）。
- `LineageRules`（static）：`RegisterBirth`（親から世代深度計算・名前カウント更新）／
  `Generation`（personId→何世代目）／`NameRepeatCount`（name→累計出現）／
  `AccumulateMerit`/`AccumulateDisgrace`（功績/汚点の蓄積）／
  `LegacyFactor`（功績−汚点から0..1の家名補正係数→`PersonRules.Effectiveness`に乗算）。
- **他のHYOS全子issueの基盤**となるデータ構造。test-first（EditMode＋TestHarness）。

#### HYOS 名前運命アーキタイプ `NameFateRules`（signatureメカニクス）

- 「同じ名前（またはその系統キー）の継承者は似た能力傾向を持つ」を実効値パターンで実装。
- `NameArchetype`（`[Serializable]`）：`archetypeKey`（名前系統識別子・「アウレリャノ型」など）/
  `abilityBiases`（`Dictionary<string,float>` ability→修正子）。修正子は正負どちらも可。
- `NameFateRules`（static）：`GetArchetype(lineage, personName)`（lineage.nameCount から
  閾値以上の反復名を検出→対応アーキタイプを返す）／
  `ApplyBias(admiral, archetype)`（修正子を**実効値として**計算・`EffectiveStat`=baseValue＋bias×repeats。
  **基準フィールドは非破壊**・実効値パターンに準拠）。
- 接続：`AdmiralData.Effectivexxx`（能力実効値）×`Lineage.NameRepeatCount`。
  `FleetMovement.GetMobilityFactor`/`ShipCombat.ComputeDamage`/`FleetStrength.TakeDamage` が既に Effectivexxx を読む設計のため、バイアスがそのまま戦闘/移動へ波及。
- EditModeテスト必須：バイアス修正子の計算・archetypeKey 解決・基準値非破壊の確認。

### ★★ 高（世代忘却・共鳴検知）

#### HYOS 世代忘却 `GenerationalAmnesiaRules`

- 世代交代時に制度記憶（`Organization.institutionalInterest`）の一部が失われる。
- `GenerationalAmnesiaRules`（static）：`AmnesiaRate(generation)`（世代ごとに増す逓減率・`AmnesiaParams` 既定 基礎10%/世代＋5%）／
  `ApplyGenerationalDecay(org, lineage)`（最後の世代保有者が死亡した時点で org.institutionalInterest に逓減を適用）／
  `SurvivorBonus`（同世代の生存者が複数いるほど逓減を緩和＝集団記憶の強靭性）。
- 接続：`AnnualLifecycleRules`（暦年次の死亡判定）×`Organization`×`Lineage.generation`。
  「戦争の教訓を知る最後の提督が死んだとき、組織は記憶を失う」。EditModeテスト必須。

#### HYOS 歴史共鳴検知 `CyclicResonanceRules`

- 過去に起きたイベントパターンと現在の状況が一致した時、「反復の旗」を立て `EventEngine` を発火する。
- `ResonanceRecord`（`[Serializable]`）：`eventTypeKey`/`factionId`/`generation`/`timestamp`。
- `CyclicResonanceRules`（static）：`RecordEvent`（イベント発生時にレコードを積む）／
  `DetectResonance(currentContext, records, windowGenerations)`（同型イベントが過去 N 世代内に発生済みか照合）／
  `ResonanceStrength`（反復回数に応じた0..1スコア＝通知の重みに使う）。
- 接続：`EventEngine.Tick`→`EventRules.IsEligible`→共鳴検知で条件スコアを加重→`NotificationCenter.Push`（category:政治/severity:注意）。
  「この種の内乱は3世代前にも起きた（共鳴度 0.8）」という通知。EditModeテスト必須。

### ★ 中（lore・コード新設なし）

#### HYOS（lore）世界観の開示データ

- 「歴史は繰り返す——宿命の反復」「英雄でさえ最後は孤独に還る」「子は親の轍を踏む」。
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への**lore データ入力**。
  `Lineage.NameRepeatCount` が閾値を超えたとき `DisclosureLedger.TryReveal` を呼ぶ実装。
  CCX-6（世界観codex退避）方針に一貫。

---

### ❌ 不採用（重複・既存で十分・魔術的要素）

| 不採用 | 理由 |
|---|---|
| 魔術的リアリズム要素（幽霊・奇跡・予言の魔術） | ゲームジャンルと乖離・固有の文学的設定。構造パターンのみ採用 |
| 王朝サイクル・制度疲労・腐敗の実装 | **`DynastyRules`/`Regime`#867 が既にカバー**。重複新設しない |
| 個人の成長・老衰の数値モデル | **`GrowthRules`#537/`LifecycleRules` LIFE-1/2 がカバー** |
| 宿命の予言（書かれた未来を読む） | **`DisclosureLedger` で連鎖開示が既に機能**。追加実装は lore 入力で足りる |
| カリスマ死後の組織崩壊 | **`SuccessionRules`/`Organization`#812 が既にカバー** |
| 一族の政治支配・土地制度 | **`FeudalRules`/`GovernmentRegistry` が既にカバー** |
| 家系の地理的孤立（辺境の町） | 戦略マップで `LogisticsRules` が版図の一体化を既にモデル。固有設定は不採用 |

---

## 3. EPIC #HYOS の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。
> 既存ライフサイクル層は**接続のみ・重複新設しない**。
> **著作権注意：固有名・文章・キャラは不使用、メカニクス/世界観構造のみ参考。**

> **EPIC = #2222**。GitHub issue 起票済み（#2226〜#2240）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **HYOS-1** | #2226 | 血統台帳 `Lineage`/`LineageRules`（血縁グラフ・世代計算・名前反復カウント・功績/汚点蓄積） | 基盤。他 HYOS 全issue の前提。`Person`/`LifecycleRules` に血縁軸を追加 |
| **HYOS-2** | #2229 | 名前運命アーキタイプ `NameFateRules`（同名継承者への能力傾向修正子・実効値パターン・基準値非破壊） | `AdmiralData.Effectivexxx`×`Lineage.NameRepeatCount`。戦闘/移動にそのまま波及 |
| **HYOS-3** | #2233 | 世代忘却 `GenerationalAmnesiaRules`（世代交代時の制度記憶喪失率・集団記憶の強靭性） | `Organization`×`AnnualLifecycleRules`×`Lineage.generation` |
| **HYOS-4** | #2237 | 歴史共鳴検知 `CyclicResonanceRules`（反復パターン検出→`EventEngine`発火・共鳴スコア） | `EventEngine`×`NotificationCenter`。メタ発火機構 |
| **HYOS-5** | #2240 | （lore）世界観の開示データ（運命の反復・孤独の宿命・世代の忘却） | `DisclosureLedger`（FND-4）。コード新設なし。`Lineage.NameRepeatCount` 閾値で連鎖開示 |

### 推奨着手順

`HYOS-1`（血統台帳＝基盤データ構造）→ `HYOS-2`（名前運命アーキタイプ＝このEPICのsignature）→
`HYOS-3`（世代忘却＝`Organization` への接続）→ `HYOS-4`（歴史共鳴検知＝EventEngine への接続）→
`HYOS-5`（lore 入力）。

> HYOS-1/2 が最も固有で欠落の大きい signature。
> これ2件だけ着地すれば「同じ名前の者が似た運命を辿る」という体験が成立する。
> HYOS-3/4 はそこに「制度の忘却」と「歴史が繰り返す」という奥行きを足す。
> いずれも既存ライフサイクル層を**後退させず接続**する additive 設計。
