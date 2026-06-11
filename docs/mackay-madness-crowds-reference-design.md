# マッカイ『狂気とバブル』参考設計（EPIC #MNIA）

> 参照元：チャールズ・マッカイ著「Extraordinary Popular Delusions and the Madness of Crowds」（1841）。
> チューリップ・バブル・十字軍遠征・魔女狩り・錬金術師の妄想など、**群集が集団で非合理に陥る実例**を横断分析した古典。
> 本ドキュメントは、当プロジェクト（Ginei）にとって**役に立つ視点だけを抽出**し、EPIC `#MNIA` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**群集心理のメカニクス構造のみ**を参考にする。

---

## 0. なぜ「狂気とバブル」が本システムに役立つか

当プロジェクトは経済・社会動態の**マクロ純ロジックを大量に保有**している：

| 既存（カバー範囲） | 備考 |
|---|---|
| `MarketRules`/`Good`/`Market`（#179-182） | 需給均衡価格 `ClearingPrice`・自動均衡 `Tick` |
| `StockMarketRules`/`Company`（#185） | 株価・`CrashRisk`・`FairPrice` |
| SAW-5（#1071 子 issue） | 商品コーナリング動学（意図的な買い占め・バブル崩壊） |
| `HopeRules`/`Community`（#852） | 希望枯渇・信仰ルート（`Faith`：虚構で希望UP）・末人判定 |
| `ReligionRules`/`Religion`（#172-175） | 改宗圧力・異端・聖戦圧力 |
| `LoyaltyRules`/`Allegiance`（#817） | カスケード動態（不動点反復）※ただし会戦旗幟限定 |
| `SecurityRules`/`SecurityApparatus`（#166） | 異論抑圧・クーデター検知・鎮圧支持ペナルティ |
| `DynastyRules`/`Regime`（#867） | 正統性腐食・天命喪失 |
| `ConsentRules`/`Polity`（#836） | 合意撤回・非協力 |
| `EventEngine`（#116） | イベント駆動型帰結 |

**しかしこれらは「合理的均衡」か「段階的侵食」というモデル**であり、マッカイが固有に描く以下が**欠けている**：

| マッカイが固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **信念の感染的伝播**：「良い話」が人から人へ伝染し、懐疑者が沈黙し、誰も止められない集団狂気へ | `HopeRules.Faith` は希望の増減だが**信念の SIR 型伝播**（感染率・接触率・回復率）が無い |
| **バブル価格と基本価値の解離**：マニア時は価格が本来の価値から指数的に逸脱し、ショックで急落 | `MarketRules.ClearingPrice` は合理均衡。**マニア価格乗数**（基本価値からの解離率）が無い |
| **逆張りの孤独と迫害**：群集と反対方向に賭ける有理主体は、ピーク時に社会的・政治的迫害を受け、崩壊後に勝つ | `PersonRules.Effectiveness` は役割適性だが**群集対逆張りの緊張構造**が無い |
| **崩壊後の責任転嫁カスケード**：誰の得にもならなかった狂気が終わると、「犯人探し」が自己増殖する | `DynastyRules.Tick` は腐敗の定常的侵食だが**崩壊イベント起動型の告発カスケード**が無い |
| **経済・政治・宗教を貫く同型性**：チューリップも魔女狩りも十字軍も同じ動学で動く | 各モジュール（市場/宗教/政治）は独立。**横断的な群集動学の共通基盤**が無い |

**結論**：マッカイは当プロジェクトの経済・社会シミュ層に**「集団が非合理に陥り回復する」という時間軸付きの動学モデル**を与える。固有の欠落軸は4つ：**①信念感染モデル・②バブル価格解離・③逆張りの迫害→勝利・④崩壊後告発カスケード**。いずれもクロスドメイン（経済・政治・宗教のいずれにも適用可）。

---

## 1. 役に立つ視点（要約）

マッカイの分析を**本システムに効く形**で1行ずつ：

1. **群集は「感染」する**。良い話は口コミで広がり、懐疑者は「時代遅れ」と黙らされ、参加しないことが社会的コストになる。→ 市場・政治・宗教いずれにも適用できる**信念感染モデル**が欲しい。
2. **価格は基本価値から切り離せる**。感染が広がるほど価格は「信じる人の数」に比例し、基本価値とは無関係になる。→ `MarketRules.ClearingPrice` へのマニア乗数オーバーレイ。
3. **逆張りは正しいが孤独で危険**。マニアのピーク時に「おかしい」と言う者は排除される。崩壊後にだけ英雄になる。→ `PersonRules`×マニア強度の**迫害→勝利コスト構造**。
4. **崩壊は誰かのせいになる**。現実への帰還は突然で激烈で、誰もが自分を被害者と信じる。告発は自己増殖する。→ 崩壊イベント起動の**告発カスケードルール**。
5. **狂気は再発する**。世代が変わると「あの失敗は二度と起こらない」と言いながら繰り返す。→ マニア終息後に`ManiaState`をリセットし、再び感染可能な状態に戻す（`recoveredDecay`）。
6. **チューリップと魔女狩りは同じ動学**。感染・ピーク・崩壊・責任転嫁の構造は経済的でも宗教的でも政治的でも同じ。→ 単一の `ManiaRules` が複数ドメインに接続できる設計（接続先を外部から注入）。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**既存の `MarketRules`/`StockMarketRules`/`HopeRules`/`ReligionRules`/`LoyaltyRules` を作り直さない**。MNIA はそれらに**欠落の感染動学を追加し接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・マッカイの signature）

#### MNIA 信念感染モデル — `ManiaState`/`ManiaRules`
- **`ManiaState`（純データ）**：`susceptibleFraction`（感染可能な人口比率）・`infectedFraction`（信念保持者）・`recoveredFraction`（脱信念者＝免疫）・`maniaIntensity`（0→1のピーク指数）。
- **`ManiaRules`（static 純ロジック）**：SIR 型 `Tick(state, transmissionRate, recoveryRate, dt)` ＝感染者が接触して感染拡大→自然回復で免疫獲得→全員が通過するとリセット（`RecoveredDecayRate` で徐々に感染可能へ戻る＝世代交代で再発）。`IsPeak`（`infectedFraction` が最大付近）・`IsCollapsing`（急減フェーズ）・`ManiaIntensity`（ピーク比）。
- **接続先**：`ManiaIntensity` を乗数として `MarketRules.ClearingPrice`×（MNIA-2）・`GovernanceRules.OutputFactor`（マニア中は過剰楽観で産出押し上げ→崩壊で陥没）・`ReligionRules.HolyWarPressure`（宗教マニアの圧力）・`EventEngine`（マニア開始/ピーク/崩壊を各種イベントのトリガーに）。
- EditMode テスト必須（感染率・回復率変化でピーク・収束を確認）。

#### MNIA バブル価格解離 — `BubblePriceRules`
- **`BubblePriceRules`（static 純ロジック）**：`ManiaPrice(basePrice, maniaIntensity, peakMultiplier)` ＝基本価値 × (1 + maniaIntensity × (peakMultiplier−1))。崩壊後は `basePrice` へ収束（オーバーシュートの `CrashDamping`）。
- `DisconnectionRatio`（マニア価格と基本価値の乖離率）＝崩壊イベントのトリガー閾値。
- 接続：**MNIA-1 の `ManiaIntensity` × `MarketRules.ClearingPrice`** のオーバーレイ。SAW-5（意図的コーナリング）とは別系統＝**意図なき群集の非合理**として価格を吊り上げる。EditMode テスト必須。

### ★★ 高（迫害→勝利・責任転嫁）

#### MNIA 逆張り迫害・勝利構造 — `ContraryPositionRules`
- **`ContraryPosition`（純データ）**：`holderId`（逆張り人物）・`betAgainstMania`（マニアに反する立場）・`persecutionAccrued`（迫害累積コスト）・`vindicated`（崩壊後に報われたか）。
- **`ContraryPositionRules`（static）**：`PersecutionPenalty(maniaIntensity)` ＝マニア強度に比例した社会的迫害コスト（`PersonRules.Effectiveness` に修正子を乗算）。`VindicationPayoff(crashRatio, holderId)` ＝崩壊後に蓄積ペナルティを**逆転益**として払い戻す。`IsContrarian(person, state)` ＝マニア信念を保持しない人物か。
- 接続：`PersonRules`（逆張り人物の実効能力が迫害期に下がり崩壊後に跳ね上がる）×`EventEngine`（迫害イベント・勝利通知）。EditMode テスト必須。

#### MNIA 崩壊後告発カスケード — `AccusationCascadeRules`
- **`AccusationState`（純データ）**：`accusationIntensity`（告発強度 0→1）・`scapegoatIds`（告発対象の人物リスト）・`spreading`（自己増殖中か）。
- **`AccusationCascadeRules`（static）**：`Trigger(crashMagnitude)` ＝崩壊の大きさに比例した告発強度で起動。`Spread(state, dt, panicFactor)` ＝告発が無実の者を巻き込んで増殖（`LoyaltyRules.ResolveCascade` の旗幟カスケードとは別系統＝会戦外の社会的告発）。`Suppress(state, legalStrength)` ＝制度的秩序が強い勢力は告発拡大を抑制。`Burn(victim, state)` ＝告発対象に損害付与→`PersonRules`/`GovernmentRegistry` から除籍可。
- 接続：`DynastyRules.Tick`（崩壊後の正統性さらなる低下）×`SecurityRules`（告発が秘密警察を肥大化させるリスク）×`EventEngine`（「スケープゴート起訴」イベント）。EditMode テスト必須。

### ★ 中（世界観 lore）

#### MNIA（lore）世界観開示データ
- 「銀河国家の歴史も熱狂の繰り返しだった」「唯一の理性は群集に踏み潰され、崩壊後にだけ英雄になる」「免疫は一世代しか続かない」。
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への**lore データ入力**のみ。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 株式市場のバブル・暴落そのもの | **`StockMarketRules.CrashRisk`（#185）がカバー**。MNIA は感染動学の乗算オーバーレイのみ |
| 商品コーナリング（意図的操作） | **SAW-5（#1071 子）がカバー**。MNIA は無意図の群集現象に限定 |
| 通貨改鋳・品位 | **SAW-1（#1072）がカバー** |
| 信仰的熱狂の改宗圧力 | **`ReligionRules.ConversionPressure`（#172）がカバー**。MNIA は感染の速度モデルを接続するのみ |
| 群集の暴動・敗走の伝播（ル・ボン的） | バックログのル・ボン『群衆心理』で扱う。MNIA は長期マニア（週〜年スケール）に特化 |
| 嘘・プロパガンダの流布 | **`EspionageRules`（諜報・情報工作）+ SAW-3（情報の非対称）がカバー**。MNIA は意図しない信念感染 |
| ユニコーン企業・VC バブル（現代金融） | `FiscalRules`/`StockMarketRules` で十分。新 EPIC 化しない |

---

## 3. EPIC #MNIA の子 Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UI へ配線。既存の市場・宗教・政治ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #1617**。GitHub issue 起票済み（#1620〜#1626）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **MNIA-1** | #1620 | 信念感染モデル（`ManiaState`/`ManiaRules`・SIR型伝播・マニア強度・経済/政治/宗教横断） | 新純ロジック。`MarketRules`×`GovernanceRules`×`ReligionRules`×`EventEngine` への乗数接続 |
| **MNIA-2** | #1622 | バブル価格解離（`BubblePriceRules`・マニア価格乗数・崩壊後オーバーシュート） | MNIA-1 × `MarketRules.ClearingPrice` オーバーレイ。SAW-5 との分離＝意図なき群集版 |
| **MNIA-3** | #1624 | 逆張り迫害・勝利構造（`ContraryPositionRules`・迫害コスト→崩壊後逆転） | `PersonRules`（実効能力修正子）×MNIA-1/2×`EventEngine` |
| **MNIA-4** | #1625 | 崩壊後告発カスケード（`AccusationCascadeRules`・スケープゴート自己増殖・制度強度で抑制） | `DynastyRules`×`SecurityRules`×`GovernmentRegistry`×`EventEngine` |
| **MNIA-5** | #1626 | （lore）世界観開示データ（群集の熱狂が帝国を揺るがす・理性の孤独・免疫は一世代） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順

`MNIA-1`（感染モデル＝基盤・他全てが依存）→ `MNIA-2`（価格解離＝最も可視的な経済効果）→ `MNIA-3`（逆張り構造＝人物システムへの接続）→ `MNIA-4`（告発カスケード＝崩壊後の政治効果）→ `MNIA-5`（lore）。

> いずれも既存モジュールを**後退させず乗算・接続**する additive 設計。`ManiaRules` は単一の感染モデルが市場/政治/宗教の3ドメインに接続できる横断設計が要諦。
