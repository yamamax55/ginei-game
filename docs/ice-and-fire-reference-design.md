# マーティン『氷と炎の歌』参考設計（EPIC #GOT）

> 参照元：マーティン『氷と炎の歌』シリーズ。複数の王位請求者が乱立し、貴族の旗幟が流動する大陸規模の継承戦争。
> 「大義より債務」「冬来る」「主役の死」という三本柱の構造が、当プロジェクトの政治・財政・群像システムに欠落した軸を指し示す。
> 本ドキュメントは当プロジェクト（銀英伝風の星間国家戦略＋既存の大量純ロジック層）にとって**役に立つ視点**だけを抽出し、EPIC `#GOT` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**政治メカニクス／世界観の構造パターンのみ**を参考にする。

---

## 0. なぜ「氷と炎の歌」が本システムに役立つか

当プロジェクトは継承戦争・忠誠・財政の純ロジックを既に大量保有している：

| 既存（カバー範囲） | カバー内容 |
|---|---|
| `SuccessionLawRules`/`SuccessionCrisisRisk`（PDX-1 #646） | 継承法の種別（長子/分割/指名/選挙）・危機リスク値 |
| `MarriageRules`/`ClaimInheritance`（PDX-2 #647） | 政略結婚による請求権継承・同盟絆 |
| `LoyaltyRules`/`Allegiance`（#817 SEKI） | 旗幟解決・忠誠カスケード・静観フリーライダー |
| PIL-6 #1095（継承戦争） | 君主死×継承危機→請求者並立→諸侯の旗幟（関ヶ原カスケードの国家規模化） |
| `FeudalRules`/`VassalRebellionRisk`（#168/#169） | 封建的反乱リスク・徴募貢献 |
| `CaptivityRules`（#154） | 捕虜化・解放・処断・寝返り（CaptiveStatus/Disposition） |
| `BankRules`/`Bank`（#186） | 信用創造・取付け・債務超過 |
| `FiscalRules.IsDebtSpiral`（#161） | 高債務→複利膨張・財政崩壊 |
| `DiplomacyRules`/`TreatyRules.IsBreach`（DIP-2 #191） | 条約破棄の検出 |
| `SuccessionRules`/`Organization`（#812） | カリスマの日常化・英雄死後の組織存続 |

**しかし、「氷と炎の歌」が固有に描く以下が欠けている**：

| 作品が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **鉄の銀行（主権デフォルト→債権者が請求者を資金支援）** | `FiscalRules.IsDebtSpiral` は財政崩壊を検出するだけ。**債権機関が敵請求者を支援する**という第三者介入ルールが無い |
| **長期冬サイクル（多年度の季節性資源ショック・外生的全勢力圧力）** | 季節・気候変動型のマクロ資源ペナルティが存在しない。全勢力を同時に締め付ける**外生的圧力**の軸が皆無 |
| **誓約信頼度の減衰（条約破棄→将来の条約コスト永続増）** | `TreatyRules.IsBreach` は破棄を検出するが**勢力ごとの信頼スコアの蓄積と将来の交渉コストへの反映**が無い |
| **身代金経済（高地位捕虜→財政収益）** | `CaptivityRules.Capture` は身分を捕虜化するが**ランク依存の身代金算定・支払い→財政への還流**が無い |
| **血統請求権の強度（世代距離・競合数による正統性重み）** | `ClaimInheritance` は請求権の存在を継承するが、**N人の請求者が並立するときの相対的正統性**（どちらの請求が「強い」か）の数値化が無い |

**結論**：氷と炎の歌は①**鉄の銀行効果**（財政崩壊が地政学の第三者介入を呼ぶ）、②**長期冬**（政治劇と独立した外生的圧力）、③**誓約信頼度**（約束を破り続けた勢力の外交コスト上昇）、④**身代金経済**（`CaptivityRules` の財政的補完）、⑤**血統請求権の強度**（PIL-6 カスケードの初期重み付け）という5つの欠落軸を与える。PIL-6（継承戦争の旗幟カスケード）とは直交的に機能し、重複しない。

---

## 1. 役に立つ視点（要約）

1. **大義より債務** ＝ 「鉄の銀行はその取り分を得る」。財政的信用は武力より確実に王座を揺さぶる。→ `FiscalRules.IsDebtSpiral` を地政学イベントのトリガーに昇格させる。
2. **冬来る** ＝ 政治劇とは独立した外生的脅威が全勢力を均等に締め付け、長期準備をした者だけが生き残る。→ 当プロジェクトの「高位の決断→創発的帰結」パターンの好例：冬に備えるか無視するかが10年後の勢力分布を決定。
3. **主役が死ぬ** ＝ 制度や組織が個人を超えて機能するかが問われる。→ `SuccessionRules`/`Organization`（#812 カリスマの日常化）と完全共鳴。制度化した勢力だけが英雄の死後も戦える。
4. **誓いは風** ＝ 約束を破り続けた者はいつか誰にも信じてもらえなくなる。外交信頼度が消費財になる。→ `DiplomacyRules` に累積スコアの軸を加え、条約締結のコスト構造を変える。
5. **身代金政治** ＝ 高位捕虜は殺すより金に換えるほうが賢い。財政難の勢力が捕虜をレバレッジにする。→ `CaptivityRules` の「解放」経路に財政コネクションを付与。
6. **五王同時並立** ＝ N≥3 の請求者が独立した資源基盤で争う構造。どの請求が「正しいか」は文脈次第。→ PIL-6 が扱う旗幟カスケードに、請求権の相対強度という**初期重み**を与える。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**PIL-6（継承戦争）/PDX-1/PDX-2/LoyaltyRules/FiscalRules/BankRules/CaptivityRules を作り直さない**。GOT はそれらに**欠落軸を足し、接続するだけ**（additive）。

### ★★★ 最優先（GOTの signature・完全な欠落）

#### GOT 鉄の銀行型主権信用（SovereignCreditRules）
- **主権デフォルト** (`FiscalRules.IsDebtSpiral` または `DebtRatio` 閾値超過) → 債権機関（`CreditInstitution`）が**存在する請求者/反乱勢力へ資金支援**（`FundRival`）を提示するオプションを `EventEngine` 経由で発火。
- 支援を受けた請求者は `baseStrength` 増強＋暫定的に `BankRules` 信用を共有。支援者が倒れると債務が継承。
- **財政崩壊が政治・外交の第三者介入に直結**＝武力でなく借金で王座を揺さぶる。
- 接続：`FiscalRules.IsDebtSpiral`/`DebtRatio` × `BankRules` × `LoyaltyRules`/PIL-6 請求者 × `EventEngine`。test-first。

#### GOT 長期冬サイクル（SeasonalCycleRules）
- **季節フェーズ**（春/夏/秋/冬）を `GameClock` のゲーム時間で回す。冬は複数年続きうる（最大 `maxWinterYears`）。
- 冬フェーズ中は `ResourceProductionRules.ProductionFactor` に `winterPenalty`（< 1.0）を全勢力均等適用。長い冬ほど `Community.Hope` が下がり末人リスクが上昇。
- 冬への**準備指数**（食料備蓄 `ResourceStockpile.物資/弾薬` の余剰比率）が高い勢力はペナルティを軽減（`preparedness` 係数）。**高位の決断＝備えるか否か**→創発的帰結。
- 接続：`GameClock`/`CalendarDispatcher` × `ResourceProductionRules` × `GovernanceRules.OutputFactor` × `Community.Hope`/`HopeRules`。test-first。

### ★★ 高（欠落・既存への接続で完結）

#### GOT 誓約信頼度の減衰（TreatyCredibilityRules）
- 勢力ごとに `credibility`（0〜1）を管理。`TreatyRules.IsBreach` が発火するたびに `-breachPenalty` を適用（回復は時間と履行実績）。
- 条約締結のコスト（`OpinionEffect`/`Leverage`）は `credibility` に反比例して増大 → 信頼を失った勢力は外交が詰まる。
- `LoyaltyRules.BaselineLoyalty` に連結：低信頼勢力は傘下諸侯の基準忠誠も低下（「この主君は約束を守らない」）。
- 接続：`DiplomacyRules`/`TreatyRules`（DIP-2）× `LoyaltyRules.BaselineLoyalty` × `FactionState.Stability`。test-first。

#### GOT 身代金経済（RansomRules）
- 捕虜の `PersonRules.rankTier`・`MilitaryAptitude`・`FactionState.Stability`（相手が豊かか）から**身代金算定**（`RansomValue`）。
- `CaptivityRules.Release` の拡張：「解放」に加え「身代金解放」パスを追加 → 支払い側は `FiscalRules.Revenue` にマイナス、受け取り側はプラス。支払い拒否→処断へ（既存パス）。
- 著名捕虜の処断ペナルティ（`ExecutionSupportPenalty` #154 カバー）との組み合わせが政治リスクを形成。
- 接続：`CaptivityRules`（#154） × `FiscalRules.Revenue` × `PersonRules.rankTier`/`ICharacter.BirthYear`（年齢）。test-first。

### ★ 中（PIL-6 の補完・正統性の初期重み）

#### GOT 血統請求権の強度（ClaimStrengthRules）
- `ClaimInheritance`（PDX-2）が継承した「請求権の存在」に**強度**（lineage distance, competing claim count, legitimacy backing）を付与。
- `ClaimStrength = baseLegitimacy / (1 + competingClaims) × distanceFactor`（世代が遠いほど弱い）。
- PIL-6 の旗幟カスケード（`LoyaltyRules.ResolveCascade`）に、諸侯が旗幟を選ぶ際の**初期重み**として注入 → 正統性の強い請求者のほうが諸侯を集めやすい。
- 接続：`MarriageRules.ClaimInheritance`（PDX-2） × `SuccessionLawRules.SuccessionCrisisRisk`（PDX-1） × `LoyaltyRules`（SEKI）× PIL-6 #1095。test-first。

#### GOT（lore）世界観の開示データ
- 「大義より債務＝鉄の銀行が歴史を動かす」「冬来る＝長期準備vs政治劇の二律背反」「主役の死＝制度を作れた者だけが続く」。
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への**lore データ入力**。CCX-6 方針に一貫。

### ❌ 不採用（既存でカバー・タイクン化回避）

| 不採用 | 理由 |
|---|---|
| 継承危機の発生そのもの（危機リスク） | **`SuccessionLawRules.SuccessionCrisisRisk`（PDX-1）がカバー** |
| 政略結婚と請求権継承 | **`MarriageRules.ClaimInheritance`（PDX-2）がカバー** |
| 旗幟カスケード・諸侯の寝返り | **PIL-6 #1095（継承戦争）＋SEKI #817 がカバー** |
| 封建的反乱リスク | **`FeudalRules.VassalRebellionRisk`（#168/#169）がカバー** |
| 捕虜化・処断・寝返りのロジック | **`CaptivityRules`（#154）がカバー**（GOT-4 は財政接続のみ） |
| カリスマ英雄の死と組織存続 | **`SuccessionRules`/`Organization`（#812）がカバー** |
| 陰謀・スパイ・暗殺 | **`EspionageRules`（#166/STH）がカバー** |
| 宮廷派閥・内部勢力 | **`PartyRules`/`Ministry`（GOV-5/6）がカバー** |
| クーデター・軍事クーデター | **`CoupRules`（#215-219）がカバー** |
| 伝染病による人口ショック | タイクン化回避＝マクロ係数のみ（新EPIC化しない） |
| 長城型の固定防衛ラインの詳細実装 | **`GalaxyMap.Corridor`×`IsFtlBlocked` で代替十分**。新実装は重複 |
| ドラゴン等の超兵器 | 世界観が違う。惑星攻城 `SiegeArena`/超兵器（#755 PB-6）で代替可 |

---

## 3. EPIC #GOT の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→盤面/UIへ配線。既存システムは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #2231**。GitHub issue 起票済み（#2235/#2238/#2241/#2243/#2244/#2245）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **GOT-1** | #2235 | 鉄の銀行型主権信用（`SovereignCreditRules`：主権デフォルト→債権機関が請求者を資金支援） | `FiscalRules.IsDebtSpiral`×`BankRules`×`LoyaltyRules`/PIL-6×`EventEngine` |
| **GOT-2** | #2238 | 長期冬サイクル（`SeasonalCycleRules`：春/夏/秋/冬の多年度フェーズ・冬は全勢力の生産にペナルティ・備え指数で軽減） | `GameClock`/`CalendarDispatcher`×`ResourceProductionRules`×`Community.Hope`/`HopeRules` |
| **GOT-3** | #2241 | 誓約信頼度の減衰（`TreatyCredibilityRules`：条約破棄→信頼スコア累積低下→将来の外交コスト増） | `DiplomacyRules`/`TreatyRules`(DIP-2)×`LoyaltyRules.BaselineLoyalty`×`FactionState.Stability` |
| **GOT-4** | #2243 | 身代金経済（`RansomRules`：高地位捕虜→ランク依存の身代金算定→財政収益） | `CaptivityRules`(#154)×`FiscalRules.Revenue`×`PersonRules.rankTier`/`ICharacter` |
| **GOT-5** | #2244 | 血統請求権の強度（`ClaimStrengthRules`：lineage distance×competing count→正統性重み→PIL-6 カスケード初期重み付け） | `MarriageRules.ClaimInheritance`(PDX-2)×`SuccessionLawRules`(PDX-1)×`LoyaltyRules`/PIL-6 |
| **GOT-6** | #2245 | （lore）世界観の開示データ（大義より債務/冬来る/主役の死） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`GOT-1`（鉄の銀行＝最も GOT 固有で欠落の大きい signature）→ `GOT-2`（長期冬＝外生的圧力の柱）→ `GOT-3`（誓約信頼度＝外交システムの補完）→ `GOT-4`（身代金＝捕虜経済の補完）→ `GOT-5`（請求権強度＝PIL-6 の初期条件整備）→ `GOT-6`（lore 入力）。

> GOT-1/2/3/4/5 はすべて既存モジュールへの**additive 接続**＝後退しない。PIL-6（継承戦争カスケード）とは直交して機能する。
