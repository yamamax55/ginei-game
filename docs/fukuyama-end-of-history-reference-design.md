# フクヤマ『歴史の終わり』参考設計（EPIC #FUKU）

> 参照元：フランシス・フクヤマ著『歴史の終わりと最後の人間』（1992）。
> ヘーゲルの承認論を受け継ぎ、**自由民主主義が人類の政治形態の到達点**であるという命題を展開。
> 「歴史の終わり」の後に残る「最後の人間（末人）」という逆説、および歴史を動かしてきた**気概（thymos＝承認欲求）**の二形態を主軸とする。
> 本ドキュメントは、当プロジェクト（Ginei＝帝国 vs 同盟という専制/民主の対立を核に持つ銀河戦略）に**役に立つ視点**だけを抽出し、EPIC `#FUKU` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**政治哲学のメカニクス・構造パターンのみ**を参考にする。

---

## 0. なぜ「歴史の終わり」が本システムに役立つか

当プロジェクトはすでに**政治・社会シミュ層を大量に保有**している：

| 既存（カバー範囲） | カバー内容 |
|---|---|
| `HopeRules`/`Community`（#852-856） | 希望が尽きると末人（`末人フラグ`）が立つ＝フロストパンク的危機 |
| `DynastyRules`/`Regime`（#867） | 正統性・腐敗・天命喪失・革命 |
| `ConsentRules`/`Polity`（#836） | 協力・正統性・非協力で統治不能 |
| `CoupRules`（#215-219） | クーデター（軍部/宮廷/革命）成功率・正統性コスト |
| `SeparationOfPowersRules`（#171） | 権力集中・専制リスク |
| `TOCQ`（トクヴィル）系モジュール | 穏やかな専制・孤立化・多数者専制・平等化圧力 |
| `WarGoalRules`/`CasusBelli`（DIP-3 #192） | 厭戦・戦争目標・講和 |
| `LoyaltyRules`/`BattleAllegianceRules`（#817） | 関ヶ原型・寝返り・実効兵力 |
| `FactionData.ideology`（B-1〜B-4） | 専制/民主などの政体イデオロギー |

**しかし、これらは「制度・物質・合意・希望」という軸でしか政治行動を説明しておらず**、フクヤマが固有に示す以下が**欠けている**：

| フクヤマが固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **気概（thymos）＝承認欲求という第三の動機** | `eros`（物質欲）と `logos`（理性）だけでは説明できない政治行動の動機軸が無い。提督が戦うのは物資のためではなく**承認（名誉・尊厳）のため**という軸 |
| **等認欲求（isothymia）vs 卓越欲求（megalothymia）の分岐** | 等しく認められたい（民主化衝動）と卓越を認められたい（建国/僭主衝動）の二形態が無い。`FactionData.ideology` は政体を分類するが、**その政体を生む欲求の分岐**が無い |
| **承認欠乏 → 急進化・革命の固有経路** | `GovernanceRules.IsUnrest` は物質的不満に基づく。**物質的には豊かでも承認を剥奪された集団が急進化する**経路（ヴァイマル共和国・第三身分の類）が無い |
| **megalothymia 提督の制度的チャネリング** | 偉大な将帥は `AdmiralData.leadership` で表現されるが、**卓越欲求の高い提督が民主制の下で英雄になるか僭主になるかを決める制度要因**が無い |
| **歴史収束圧力（敗北した専制政体への民主化係数）** | `TOCQ-5 EqualityDriftRules` は社会的平等化。**繰り返す敗北・停滞に曝された独裁政体が等認欲求から民主化圧力を受ける**収束動学が無い |

**結論**：フクヤマは当プロジェクトの政治シミュに**「気概（thymos）という承認欲求の軸」**を与える。
帝国（専制・卓越欲求の制度的許容）vs 同盟（民主・等認欲求の制度化）という Ginei の根幹対立が、
単なる「善悪の戦い」でなく**両システムの内発的論理の衝突**として表現できるようになる。
既存 `HopeRules`（末人）・`DynastyRules`（正統性）・`TOCQ` 系（民主の内発的腐食）とは**直交した欲求軸**の追加であり、重複しない。

---

## 1. 役に立つ視点（要約）

フクヤマの世界観を、**本システムに効く形**で1行ずつ：

1. **政治行動の動機は物質欲でも理性でもなく「承認欲求（thymos）」**——提督が星系を落とすのは資源のためではなく歴史に名を刻むためであり、民衆が蜂起するのは飢えだけでなく尊厳の剥奪のため。→ 既存の `eros`（`FiscalRules`/`MarketRules`）と `logos`（`EventRules`/政策決定）に**第三軸として thymos を追加**。
2. **等認欲求（isothymia）が民主化衝動を作り、卓越欲求（megalothymia）が建国者/独裁者を作る**——民主革命は「俺も人間だ」という等認欲求の爆発。英雄は「俺は特別だ」という卓越欲求の具現化。→ `ThymosRules` の二軸が、`GovernanceRules` の現体制型（民主/専制）の**発生源**を説明。
3. **承認を奪われた集団は材料に関係なく急進化する**——ヴァイマル共和国のナショナリスト、植民地の民族解放運動。物質的豊かさでは止められない。→ `RecognitionDeficitRules` が `GovernanceRules.IsUnrest` に**承認軸を足す**（材料の不満と独立した経路）。
4. **偉大な将帥（megalothymia 型）は民主制の下で英雄か僭主かの岐路に立つ**——ナポレオン、カエサル。制度がチャネリングできれば英雄、できなければ専制者。→ `AdmiralData` の提督 × `CivilianControlRules`（文民統制）の接続が深まる。
5. **繰り返す敗北に曝された独裁政体は等認欲求の圧力で民主化に収束する傾向がある**——冷戦後の民主化の波。ただし収束は保証されず、逆行（authoritarianバックスライド）もある。→ `HistoricalConvergencePressure` が `FactionState` に係数として効き、帝国の長期的「合理的変容」か「崩壊」かを分岐させる。
6. **「歴史の終わり」の後に残る末人は thymos を持たない**——自由民主主義の勝利が皮肉にも「最後の人間」を量産する逆説。→ 既存 `HopeRules.末人フラグ` の**理論的基盤**として lore に接続（コード新設なし）。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`HopeRules`・`DynastyRules`・`ConsentRules`・`TOCQ` 系を作り直さない**。FUKU はそれらに**欠落軸（thymos）を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・フクヤマの signature）

#### FUKU 気概（thymos）の純ロジック（等認欲求/卓越欲求）

- **`Thymos`(struct・非MB)**：`isothymia`（等認欲求 0..1） / `megalothymia`（卓越欲求 0..1） / `TotalDrive`（合計承認欲求） / `Balance`（iso-mega 差分 > 0 = 民主志向 < 0 = 権威志向）。
- **`ThymosRules`(static・pure logic)**：`Classify(Thymos)`（均衡型/等認支配型/卓越支配型）／`IsothymicFrustration(iso,regime)`（等認欲求と実体制の乖離 → 不満係数）／`MegalothymicFrustration(mega,recognitionGiven)`（卓越欲求への承認が不十分 → 急進化係数）／`ThymicDriveToward`（現在の政体から見て thymos が押す方向 = 民主化/権威化）。
- **接続**：`FactionState` に `factionThymos`（勢力のthymos 分布）を追加。`GovernanceRules.EquilibriumStability` の係数に `ThymicAlignment`（thymos と政体の整合度）を乗算。**基準値非破壊（実効値パターン）**。

#### FUKU 承認欠乏と過激化（`RecognitionDeficitRules`）

- **承認欠乏の固有経路**：物質的豊かさと独立した「承認の剥奪」が蓄積 → `RadicalizationRisk`（急進化リスク）上昇。等認欲求の剥奪（→ 民主革命圧力）と卓越欲求の剥奪（→ 英雄待望・独裁者出現）は方向が逆。
- **`RecognitionDeficitRules`(static)**：`IsothymicDeficit(province, regime_type)`（等認欲求剥奪量 = 民主制外での抑圧度） / `MegalothymicDeficit(admiral, institutional_recognition)`（卓越欲求への承認不足） / `RadicalizationPressure(deficit)`（欠乏→急進化確率 * sigmoid） / `ChronicDeficit`（累積欠乏 = `Province.integration` の逆数 + ideology mismatch）。
- **接続**：`GovernanceRules.IsUnrest` の 2 つ目の独立経路（既存 = 物質不満・新規 = 承認欠乏）。`EventEngine` の条件式に使用可。

### ★★ 高（megalothymia 提督 × 収束動学）

#### FUKU Megalothymia 提督パターン（英雄 vs 僭主の岐路）

- **偉大な将帥は卓越欲求が高い**：`AdmiralData.megalothymia`（卓越欲求係数 0..1）を追加（任意フィールド、0 = 従来動作・後方互換）。
- **チャネリング条件**：`ChannelsMegalothymia(admiral, control_type)` = 民主制 + 強い文民統制 → 英雄として機能（能力ボーナス維持）／民主制 + 弱い文民統制 OR 専制 + 高 megalothymia → 僭主リスク（`CoupRules.CoupRisk` に上乗せ）。
- **接続**：`CivilianControlRules`（`MilitaryMayHoldPoliticalOffice` × megalothymia → 制度が英雄を飼いならすか）×`SuccessionRules`（後継指名で megalothymia 高い人物は正統性争いを起こしやすい）×`AdmiralData.isProtagonist`（GON-6 主人公は通常高 megalothymia）。**基準値非破壊**。

#### FUKU 歴史収束圧力（`HistoricalConvergencePressure`）

- **敗北に曝された権威政体への民主化係数**：繰り返す軍事敗北 + 停滞 + 隣接民主政体の繁栄 → 体制内等認欲求が臨界を超えると民主化圧力が `DynastyRules` の正統性を侵食。
- **`ConvergenceRules`(static)**：`IsothymicConvergencePressure(factionState, militaryFailures, neighborDemocracy)`（敗北数 × 隣接民主度 = 収束圧力 0..1）／`AuthoritarianBackslashRisk`（収束が急ならバックラッシュ → クーデター/弾圧で逆流）／`ConvergenceSpeed`（民主化が進む速度 = 制度の浸透度）。
- **接続**：`DynastyRules.Tick`（天命喪失条件に収束圧力を係数として追加）×`FactionStateRules.Tick`（敗北イベントが承認不満を積み上げる）。**TOCQ-5 `EqualityDriftRules`（社会的平等化）とは軸が異なる（あちらは평等、こちらは承認）**。重複しない。

### ★ 中（承認戦争・lore）

#### FUKU 承認のための戦争（`RecognitionCasusBelli`）

- **戦争の動機に「承認・威信・名誉」を追加**：領土・資源・安全保障だけでない、**承認を求める戦争**（「最強の星系国家と認められたい」「帝国の権威を示したい」）を `WarGoalRules.CasusBelli` の一種として。
- **`RecognitionCasusBelli`(enum 追加)**：`PrestigeWar`（威信・承認獲得）。`WarGoalRules` に `RecognitionGoalLegitimacy`（承認戦争への国民支持 = megalothymia 高い勢力で上昇）を追加。
- **接続**：`WarGoalRules`/`DiplomacyState`×`CasusBelli` の拡張。megalothymia 高い勢力・提督は威信戦争を起こしやすい。

#### FUKU （lore）歴史の終わり・末人の逆説

- **「自由民主制の勝利が最後の人間を量産する」逆説** = 既存 `HopeRules.末人フラグ` の**理論的背景**として lore 入力。
- **帝国 vs 同盟の核のナラティブ**：帝国は megalothymia の解放（英雄の輩出）を約束するが専制に終わり、同盟は isothymia の充足（平等）を約束するが末人に終わる——という**どちらにも完全な答えはない**弁証法的対立。
- **接続**：コード新設せず `DisclosureLedger`（FND-4）への**lore データ入力**。秘史「承認と歴史の終わり」「末人化した銀河と最後の英雄」などの条件発火素材。

### ❌ 不採用（重複・既存で十分・タイクン化）

| 不採用 | 理由 |
|---|---|
| 末人（last man）モジュールの新設 | **`HopeRules`/`Community`（#852-856）が既にカバー**。FUKU はその**理論的基盤**として接続するだけ |
| 民主化プロセス詳細 UI / 選挙ロジック | **`PartyRules`/`LeadershipElectionRules`（GOV-6/7）がカバー**。詳細選挙は既存 |
| 冷戦の軍拡競争・核抑止 | **既存 `DiplomacyRules`/`WarGoalRules`** の範囲。FUKU は動機軸の追加のみ |
| Hegelian 弁証法の自動歴史進歩エンジン | タイクン化回避。決定論的歴史進歩は面白くない。thymos は「圧力」として係数を与えるだけ |
| 自由民主主義の「正しさ」を直接コード化 | イデオロギー判断をゲームに埋め込まない。thymos は中立的な欲求軸 |
| 全政体を自由民主制に収束させる強制ルール | 収束は圧力（傾向）。バックラッシュ・停滞・崩壊も等確率で選ばれる |

---

## 3. EPIC #FUKU の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。既存政治ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**政治哲学のメカニクス/世界観構造のみ**参考。

> **EPIC = #1877**。GitHub issue 起票済み（#1881〜#1895）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **FUKU-1** | #1881 | `Thymos`/`ThymosRules` 気概の純ロジック（等認欲求 isothymia / 卓越欲求 megalothymia） | 新 `ThymosRules`。`FactionState.factionThymos`×`GovernanceRules.ThymicAlignment` 係数 |
| **FUKU-2** | #1883 | `RecognitionDeficitRules` 承認欠乏と過激化（欠乏蓄積→急進化/革命圧力・物質不満と独立した経路） | `GovernanceRules.IsUnrest` の第2経路。`EventEngine` 条件式。`Province.integration`×ideology mismatch |
| **FUKU-3** | #1886 | Megalothymia 提督パターン（卓越欲求高い提督→英雄 vs 僭主・文民統制でのチャネリング） | `AdmiralData.megalothymia`（任意フィールド）×`CivilianControlRules`×`CoupRules.CoupRisk` |
| **FUKU-4** | #1889 | `ConvergenceRules` 歴史収束圧力（敗北・停滞した権威政体への民主化係数・バックラッシュ逆流） | `DynastyRules.Tick`×`FactionStateRules.Tick`。TOCQ-5（平等化）と独立した「承認」軸 |
| **FUKU-5** | #1892 | `RecognitionCasusBelli` 承認のための戦争（威信・名誉・承認を動機とする戦争目標の追加） | `WarGoalRules`/`CasusBelli` 拡張。megalothymia 高い勢力/提督の威信戦争傾向 |
| **FUKU-6** | #1895 | （lore）歴史の終わり・末人の逆説と帝国 vs 同盟ナラティブ（`DisclosureLedger` 開示データ） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順

`FUKU-1`（thymos の純ロジック基盤を先に固定 = 後続の全 issue がここを参照）→ `FUKU-2`（承認欠乏経路 = 最も具体的な欠落）→ `FUKU-3`（megalothymia 提督 = AdmiralData への接続で会戦に直結）→ `FUKU-4`（収束圧力 = 戦略ゲームプレイへの影響）→ `FUKU-5`（承認戦争 = DiplomacyRules 拡張）→ `FUKU-6`（lore）。

> いずれも既存政治シミュレーション層（`HopeRules`/`DynastyRules`/`ConsentRules`/`TOCQ`）を**後退させず接続**する additive 設計。
> **「帝国は megalothymia の星、同盟は isothymia の星」——この対立に数値的な軸を与えることが FUKU の核心**。
