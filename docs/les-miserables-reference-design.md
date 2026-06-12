# ユーゴー『レ・ミゼラブル』参考設計（EPIC #HUGO）

> 参照元：ヴィクトル・ユーゴー『レ・ミゼラブル』（1862）。19世紀パリを舞台に元服役囚ジャン・ヴァルジャンの贖罪を軸に、民衆の蜂起・貧困の連鎖・法と慈悲の対立を描く社会小説。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略＋既に大規模な社会・政治純ロジック層）にとって**役に立つ視点**だけを抽出し、EPIC `#HUGO` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**メカニクス／世界観の構造パターンのみ**を参考にする。

---

## 0. なぜ「レ・ミゼラブル」が本システムに役立つか

当プロジェクトは社会・政治の**マクロ純ロジックを大量に保有**している（[CLAUDE.md] 参照）：

| 既存（カバー範囲） | カバー内容 |
|---|---|
| `JusticeRules`/`JusticeView`（#918-923） | 功利/ロールズ/リバタリアン/アリストテレス/共通善の5視点 |
| `CoupRules`（#215-219） | 軍部/宮廷/革命クーデターの成功率・後始末 |
| `Movement`/`NonviolenceRules`（#831/#832） | 非暴力抵抗・弾圧の可視化・支持転換 |
| `HopeRules`/`Community`（#852-856） | 希望枯渇→末人（ロンドン派）の立起 |
| `ConsentRules`/`Polity`（#836） | 非協力・ボイコット→統治不能 |
| `SecurityRules`/`SecurityApparatus`（#166） | 反乱抑圧・クーデター検知・弾圧支持ペナルティ |
| `CaptivityRules`（#154） | 捕虜の処断/解放/登用 |
| `FiscalClass`/`RedistributionRules`（#163/#162） | 階級別税負担・累進/逆進と支持変化・階級対立 |

**しかし、これらは「国家・市場・運動」という集合的・マクロ均衡**であり、レ・ミゼラブルが固有に描く以下が**欠けている**：

| レ・ミゼラブルが固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **汚名（スティグマ）と行動による段階的な信頼回復（贖罪弧）** | `CaptivityRules` は捕虜を「解放/処断/登用」するが、元服役囚が長年の行動で信頼を積み上げ社会復帰する**長期の贖罪アーク**が無い。再暴露リスクも無い |
| **民衆武装蜂起・バリケード動態** | `CoupRules`=エリート主導のクーデター、`NonviolenceRules`=非暴力抵抗。市民が武装して街路を封鎖し権力に対峙する**バリケード型の民衆蜂起**（失敗しうる）が無い |
| **貧困の自己強化スパイラル（貧困の罠）** | `FiscalClass`/`RedistributionRules` は階級間分配の集計効果。ファンチーヌ的な「職を失う→売春→さらに追い詰められる」個人・世帯レベルの**下降スパイラル**が無い |
| **恩赦・大赦が正統性に与える不均衡な乗数効果** | `DynastyRules.Reform` は制度的改革で腐敗↓正統性↑。ミリエル司教の銀燭台的な**一回の慈悲が正統性を過剰に押し上げる**恩赦ダイナミクスが無い |
| **執拗追跡と身元暴露リスク** | `SecurityRules` は集合的な反乱抑圧。ジャベール的な**特定個人を長期にわたって追跡**し、隠した過去を暴露するリスク回路が無い |

**結論**：レ・ミゼラブルは当プロジェクトの社会・政治シミュに**「個人の時間軸上の変容」という縦断的視点**と、**①汚名と贖罪 ②市民蜂起 ③貧困の罠 ④恩赦の乗数**という4つの欠落軸を与える。革命とはエリートのクーデターではなく**民衆の絶望と希望が重なった瞬間の爆発**であり、この構造は銀河帝国型専制への抵抗の演算に直接使える。

---

## 1. 役に立つ視点（要約）

レ・ミゼラブルの世界観を、**本システムに効く形**で1行ずつ：

1. **汚名は人をその過去に縛り続ける——しかし行動が汚名を上書きしうる**。ヴァルジャンの贖罪弧＝烙印を負った人物が長年の善行でスティグマを削り、露見リスクの中で生きる。→ 既存 `CaptivityRules`（捕虜処分）に**長期の贖罪・信頼回復**の縦断次元を足す。
2. **民衆蜂起は「絶望×希望のピーク」で発火し、孤立すると自壊する**。ABCの友の会→6月蜂起＝秘密組織が臨界で街頭へ溢れ、連帯が崩れると敗北。→ クーデター(CoupRules)と非暴力抵抗(NonviolenceRules)の**間**の武装市民蜂起を埋める。
3. **貧困は単なる不足でなく、悪い選択を強いる構造的罠**。ファンチーヌの下降スパイラル＝選択肢の狭窄が選択肢をさらに狭める。→ 個人・世帯単位の**貧困罠ロジック**が集計すると社会不安に接続される。
4. **法の厳格さと一回の慈悲は非対称**。制度的改革より個人的恩赦が正統性を飛躍的に押し上げる（ミリエル効果）。→ 政体が持つ**恩赦権**を執行コストと正統性乗数でモデル化。
5. **追跡者の存在が贖罪弧の緊張を生む**。露見リスクは行動選択に常に影を落とす——ジャベールとヴァルジャンの非対称ゲーム。→ `SecurityRules` 集合抑圧に**個人標的の執拗追跡**を追加。
6. **失敗した革命は次の革命を準備する**。6月蜂起は敗北したが、その記憶が後代の民衆運動のloreになる。→ `DisclosureLedger`（FND-4）への世界観loreデータ。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`CoupRules`/`NonviolenceRules`/`JusticeRules`/`CaptivityRules`/`SecurityRules` を作り直さない**。HUGO はそれらに**欠落軸を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・レ・ミゼラブルの signature）

#### HUGO 汚名と贖罪弧（`StigmaRules`/`PersonalRedemption`）
- **汚名（Stigma）**：元服役囚・脱走兵・裏切り者など、`Person` に「スティグマ種別＋強度」を付与。スティグマが高いほど就職・昇進・同盟に不利。
- **贖罪行動**：善行・奉仕・時間経過で段階的にスティグマを削減する純ロジック（`RedemptionRules.AccrueGoodwill`）。基準値非破壊・実効値パターン。
- **再暴露リスク**：`StigmaRules.ExposureRisk`——隠されたスティグマは `PursuitRules`（HUGO-5）や内部告発イベントで露見しうる。露見時の信頼崩壊を計算。
- 接続：`CaptivityRules`（登用→贖罪弧の起点）×`LifecycleRules`（時間）×`EventEngine`（露見イベント）×`GovernmentRegistry`（スティグマ持ちが高位役職に就けるか）。

#### HUGO 街頭蜂起・バリケード動態（`InsurrectionRules`）
- **蜂起閾値**：地域（星系）の絶望度(`HopeRules.Community.hope` が低い)+民衆武装率×触媒イベントで閾値判定。
- **バリケード**：蜂起勢力が空間（星系内）を一時的に制圧。反乱軍連帯(`solidarity`)が高いほど長続きするが、外部支援なしでは孤立自壊。`SolidarityDecay`が連帯を自然減衰させ、援軍到着で回復。
- **鎮圧コスト**：統治側は兵力×鎮圧効率で反乱を圧倒できるが、正統性ペナルティが発生（`NonviolenceRules.Repress` の武装版）。
- 接続：`HopeRules`（絶望→蜂起）×`NonviolenceRules`（プロテスト→武装化の分岐）×`CoupRules`（エリートが乗っ取ると革命クーデターへ）×`SecurityRules`（鎮圧コスト）×`ConsentRules`（統治不能判定）。

### ★★ 高（個人レベルの下降スパイラル・制度的恩赦）

#### HUGO 貧困の罠・社会的再生産（`PovertyTrapRules`）
- **貧困の自己強化**：`Person` の経済水準が閾値（`PovertyThreshold`）を割ると「貧困スパイラル」状態に遷移。スパイラル中は選択肢が狭まり（役職就任不可・教育不可・徴募のみ）、回復に時間と外部介入が必要。
- **貧困の代際再生産**：貧困スパイラルが続くと次世代（`DemographicsRules` の年少コホート）が高い初期スティグマ（HUGO-1）を持って生まれる確率が上昇。
- 接続：`FiscalClass`/`RedistributionRules`（集計貧困率）×`DemographicsRules`（人口動態）×`LifecycleRules`（世代）×`GovernmentRegistry`（貧困層の任職制約）×`StigmaRules`（貧困→スティグマ付与）。

#### HUGO 恩赦・大赦と正統性乗数（`ClemencyRules`）
- **恩赦執行**：統治者が `PersonalClemency`（個人恩赦）または `Amnesty`（大赦）を執行するコスト（支持者の反発）と報酬（正統性の不均衡な上昇）。
- **慈悲の乗数**：`ClemencyRules.LegitimacyGain`＝`ConsentRules`/`DynastyRules` の通常の改革より高い正統性乗数。ただし反復するほど効果が逓減（ミリエル効果は一回だから強い）。
- **恩赦した人物のスティグマ軽減**（HUGO-1 `StigmaRules` と接続）。
- 接続：`DynastyRules`（正統性）×`ConsentRules`（合意）×`StigmaRules`（HUGO-1）×`EventEngine`（恩赦イベント）。

### ★ 中（追跡・監視・lore）

#### HUGO 執拗追跡と身元暴露リスク（`PursuitRules`）
- **追跡指定**：国家が特定 `Person` を「追跡対象」に設定。対象はすべての行動に`ExposureRisk`修正が乗り、高位役職・公開行動・越境で露見確率が上昇。
- **追跡強度**：`SecurityApparatus.PursuitIntensity`（追跡リソース）が高いほど露見確率大。対象が大赦を受けると追跡解除。
- 接続：`SecurityRules`（装置）×`StigmaRules`（HUGO-1・露見で再顕現）×`EspionageRules`（情報収集）×`ClemencyRules`（HUGO-4・恩赦で解除）。

#### HUGO（lore）世界観の開示データ（法と慈悲・贖罪・革命の記憶）
- 「法は万人に平等に降りかかるが、慈悲はその法を超える瞬間がある」。
- 「失敗した蜂起は次の革命を育てる——敗北の歴史が民衆の記憶に残る」。
- 「贖罪とは自己証明の連鎖であり、一度の露見がすべてを無効にしうる」。
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への**lore データ入力**のみ。CCX-6 方針に一貫。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 司法制度・裁判システムの実装 | **`JusticeRules`（#918-923）が5つの正義観で十分カバー**。審判プロセスの手続き実装は「マイクロ操作」になりタイクン化 |
| 警察/官僚機構の組織図 | **`SecurityRules`/`GovernmentRegistry`/`MinistryRules` が既にカバー**。新機構を立てない |
| 教会・宗教の経済的側面 | **`ReligionRules`（#172-175）＋SAW-7（宗教×経済）がカバー**。重複新設しない |
| 非暴力デモ・ストライキ | **`NonviolenceRules`/`Movement`（#831/#832）がカバー** |
| 階級闘争の集計モデル | **`FiscalClass`/`RedistributionRules`（#163/#162）がカバー**。HUGO は個人レベルの罠のみ |
| 亡命・망命のメカニクス | **`CultureRules.ExileLikelihood`（#194）がカバー** |
| 政治犯・検閲・プレス弾圧 | **`SecurityRules.DissentSuppression` がカバー** |

---

## 3. EPIC #2137 の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。既存社会シミュは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #2137**。GitHub issue 起票済み（#2141〜#2164）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **HUGO-1** | #2141 | 汚名と贖罪弧（`StigmaRules`/`PersonalRedemption`）スティグマ付与・善行による段階的削減・再暴露 | 新 `StigmaRules`。`CaptivityRules` 登用→贖罪の起点。`EventEngine` で露見イベント |
| **HUGO-2** | #2145 | 街頭蜂起・バリケード動態（`InsurrectionRules`）蜂起閾値・連帯崩壊・鎮圧コスト | 新 `InsurrectionRules`。`HopeRules`×`NonviolenceRules`×`CoupRules` の中間 |
| **HUGO-3** | #2152 | 貧困の罠・社会的再生産（`PovertyTrapRules`）下降スパイラル・代際再生産 | 新 `PovertyTrapRules`。`FiscalClass`×`DemographicsRules`×`StigmaRules`（HUGO-1）|
| **HUGO-4** | #2157 | 恩赦・大赦と正統性乗数（`ClemencyRules`）個人恩赦/大赦・慈悲の逓減乗数 | 新 `ClemencyRules`。`DynastyRules`×`ConsentRules`×`StigmaRules`（HUGO-1）|
| **HUGO-5** | #2160 | 執拗追跡と身元暴露リスク（`PursuitRules`）追跡指定・露見確率・大赦で解除 | 新 `PursuitRules`。`SecurityRules`×`StigmaRules`（HUGO-1）×`ClemencyRules`（HUGO-4）|
| **HUGO-6** | #2164 | （lore）世界観開示データ（法と慈悲・贖罪・失敗した革命の記憶） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`HUGO-1`（汚名と贖罪弧＝基盤。HUGO-2/3/4/5 が依存）→ `HUGO-2`（街頭蜂起＝最も固有で欠落の大きい signature）→ `HUGO-4`（恩赦乗数＝HUGO-1/2 の対抗軸）→ `HUGO-3`（貧困罠＝HUGO-1 の下流）→ `HUGO-5`（追跡＝HUGO-1/4 の応用）→ `HUGO-6`（lore）。

> いずれも既存社会シミュを**後退させず接続**する additive 設計。`HopeRules`/`ConsentRules`/`NonviolenceRules`/`CoupRules`/`SecurityRules` が「群衆の行動」を点描する中、HUGO は「**個人の縦断的変容と民衆の爆発点**」という欠落次元を足す。
