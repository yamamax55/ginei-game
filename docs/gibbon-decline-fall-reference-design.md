# ギボン『ローマ帝国衰亡史』参考設計（EPIC #GIB）

> 参照元：エドワード・ギボン『ローマ帝国衰亡史（The History of the Decline and Fall of the Roman Empire）』（1776-1789年）。
> 全6巻にわたる西ローマ・東ローマの3世紀にわたる衰亡の分析——内部腐食・軍の傭兵化・宗教制度の変容・辺境防衛の空洞化を多角的に描く。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略＋既に巨大な社会・政治・軍事純ロジック層）にとって
> **役に立つ視点だけを抽出し**、EPIC `#GIB` として issue 化する提案。
> **著作権注意：固有名・文章・キャラクター・固有設定は流用せず、帝国衰亡のメカニクス／世界観構造パターンのみを参考にする。**

---

## 0. なぜ「ローマ帝国衰亡史」が本システムに役立つか

当プロジェクトは国家の**衰退・崩壊に関わるロジックを広く保有**している：

| 既存（衰退関連） | カバー範囲 |
|---|---|
| `DynastyRules`/`Regime`（#867） | 腐敗→正統性低下→天命喪失→革命（循環的リセット） |
| `FactionState`/`FactionStateRules` | 王朝/統治体/組織/共同体の合成状態（`IsCollapsing`） |
| `CoupRules`（#215-219） | クーデター・成功率・事後正統性 |
| `CivilianControlRules`（#145） | 文民統制・クーデターリスク・軍政関係 |
| `LoyaltyRules`/`Allegiance`（#817） | 諸侯の条件付き忠誠・寝返りカスケード |
| `ReligionRules`（#172-175） | 宗教の社会的影響・改宗圧力・聖戦 |
| `LogisticsRules`（#844） | 版図の連結一体化度（分断で国力低下） |
| `FiscalRules`/`FiscalState`（#163） | 財政・国債・金利・為替・通貨安 |
| `SuccessionLawRules`（#646） | 継承法（長子/分割/指名/選挙） |
| `CareerPipelineRules`（#155-157） | 人材の出自（士官/官僚/技術の三系統） |
| `ConsentRules`/`Polity`（#836） | 権力は協力の束・非協力で統治不能 |
| `HopeRules`/`Community`（#852） | 希望尽きると末人（ロンドン派）が立つ |
| `Organization`/`SuccessionRules`（#812） | カリスマの日常化・英雄死後の組織存続 |

**しかし、これらは個別の「クライシスの瞬間」「循環的な天命サイクル」を扱うものであり、ギボンが固有に描く以下が欠けている：**

| ギボン固有の視点 | 当プロジェクトでの欠落 |
|---|---|
| **世俗的衰退曲線＝ラチェット効果** | `DynastyRules.Tick` は循環・革命でリセット可能。**回復メカニズム自体が劣化する複利衰退**（3世紀にわたる不可逆的進行）が無い |
| **傭兵化・外来軍の並行忠誠（フォエデラティ）** | `Allegiance` は二値（戦う/静観）。**サブリーダーを介する二重忠誠構造**（国家でなく隊長に忠誠を持つ傭兵軍）が無い |
| **パンとサーカス（ポピュリズム補助金の双面効果）** | `WelfareHopeBonus` は希望改善のみ。**短期支持を買う→長期で市民的徳を空洞化**（補助漬けで徴兵・課税・自治が機能不全になる）構造が無い |
| **中核星系の戦略的過剰価値（帝国の心臓喪失）** | `LogisticsRules.CohesionFactor` は全ノード均等。**象徴的中核の喪失→不均衡な正統性崩壊**（ローマ市陥落が軍事価値を超えた崩壊を生む）が無い |
| **二重正統性（国家 vs 宗教制度の権威競合）** | `ReligionRules` は人口への社会効果のみ。**競合する統治機関としての宗教**（国家と正統性を争う制度的主体）が無い |

**結論**：ギボンは本プロジェクトの国家衰退モデルに**「複利的・不可逆な長期衰退軌道」**という次元を加え、さらに①傭兵化した軍の二重忠誠 ②パンとサーカスの市民的空洞化 ③中核喪失の不均衡崩壊 ④宗教の制度的権威競合という4つの欠落軸を与える。銀英伝の**「帝国と同盟の長期的衰退・旧体制の腐食」**という主題とも完全に共鳴する。

---

## 1. 役に立つ視点（要約）

ギボンの世界観を、**本システムに効く形**で1行ずつ：

1. **偉大な帝国は外敵に滅ぼされるより内部から腐る**——市民的徳の喪失、財政の過剰拡大、軍の外注が先に起き、その隙を外敵が突く。→ `FactionState.IsCollapsing` に**不可逆の衰退軌道**を加える。
2. **軍の傭兵化は一時しのぎが構造問題になる**——外来軍に頼るほど市民の軍事参加が減り、次の危機にはさらに傭兵が必要になる悪循環。→ `FoederatiRules`（二重忠誠の傭兵）として既存 `Allegiance`×`FactionLoyaltyRules` に追加。
3. **パンとサーカスは市民を「受給者」に変える**——補助金は即時支持を買うが市民的責任感を萎縮させ、課税・徴兵・自治の受容度が下がる。→ `BreadAndCircusRules`（補助金の善悪二面）として `FiscalRules`×`Organization` に接続。
4. **帝国の心臓（首都・聖地）の喪失は数学的損失を超える**——全ノード均等な論理では説明できない崩壊加速が起きる。→ `StrategicWeightRules`（中核星系の重み）として `LogisticsRules` を拡張。
5. **宗教は単なる信仰でなく統治を競う制度になりうる**——教会が課税・教育・司法・外交を担うとき、「国家と教会のどちらが正統か」が政治問題になる。→ `DualLegitimacyRules`（権威の二重構造）として `ReligionRules`×`Regime` に接続。
6. **東西分割は管理効率を高めたが継承問題を永続化した**——行政の分権化（効率）と正統性の分裂（弱体化）はトレードオフ。→ 既存の多勢力・外交システムへの lore 接続。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`DynastyRules`/`FactionState`/`CivilianControlRules`/`ReligionRules`/`LogisticsRules` を作り直さない**。GIB はそれらに**欠落軸を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・ギボンの signature）

#### GIB 世俗的衰退指数とラチェット効果（`DeclineRules`/`DeclineIndex`）
- **`DeclineIndex`**（純データ・非MonoBehaviour）：`FactionState` の複数要素（正統性/合意/結束/希望）が**同時に閾値以下**になると衰退スコアを増分する。
- **ラチェット効果**：衰退スコアが一定以上になると、`DynastyRules.Reform`/`Revolution` の回復効果に**ペナルティ倍率**をかける（回復メカニズム自体が劣化）。
- **不可逆フラグ**：複数指標の同時悪化がN回連続で起きると `IsTerminalDecline=true` を立て、通常の政策効果を半減する。
- 接続：`FactionState`（入力）→`DeclineRules.Tick(state,dt)`→`DeclineIndex`（出力）→`CampaignRules.EffectiveStability` に係数として乗算。

#### GIB 傭兵化・フォエデラティの二重忠誠（`FoederatiRules`/`FoederatiForce`）
- **`FoederatiForce`**（非MonoBehaviour）：`factionId`（雇用側）／`captainLoyalty`（隊長→雇用側への忠誠 0..1）／`strength`（兵力）／`isEngaged`（現在従軍中か）。
- **二重忠誠**：傭兵部隊の戦力は雇用側の軍に加算されるが、`captainLoyalty` が閾値を下回ると `isEngaged=false`（離脱）または敵側への転属（`Defect`）が発火する——`LoyaltyRules.ResolveStance` を隊長に適用した派生形。
- **悪循環**：`FoederatiRules.CivicMilitaryDecay` が傭兵依存度（フォエデラティ兵力÷総兵力）に比例して `Organization.institutionalization` を毎ターン微減させる（傭兵を使うほど市民軍が育たない）。
- 接続：`Allegiance`/`LoyaltyRules`（隊長忠誠の計算）×`CivilianControlRules`（軍部優位リスク）×`BreadAndCircusRules`（パンとサーカスで徴兵力低下→傭兵依存上昇のループ）。

#### GIB パンとサーカス — ポピュリズム補助金の双面効果（`BreadAndCircusRules`）
- **`Subsidize(faction, amount, FactionState, FiscalState)`**：短期効果＝`Polity.cooperation`上昇＋`Community.hope`上昇。長期効果＝`FiscalState`歳出増加＋`Organization.institutionalization`微減（市民的徳の萎縮）。
- **空洞化加速**：補助水準が`SubsidyDependencyThreshold`を超えると `CanTax`（増税合意）と `CanConscript`（徴兵受容）に制限ペナルティをかける——自由な財源・兵力が枯渇する。
- **依存ループ**：`BreadAndCircusRules.DependencyRate` = (累積補助/GDP)——依存率が高いほど補助を切った時の`cooperation`暴落が大きくなる（一度始めると止められない）。
- 接続：`FiscalRules`（歳出）×`Organization`（市民的徳の空洞化）×`ConsentRules`（課税・徴兵の合意）。`FoederatiRules.CivicMilitaryDecay`とループ。

### ★★ 高（マクロ均衡に構造的重みを加える）

#### GIB 中核星系の戦略的重心（`StrategicWeightRules`）
- **`StarSystem.isHeartland`**（bool フラグ）：象徴的・経済的・制度的中核を持つ星系。
- **`StrategicWeightRules.HeartlandLossImpact(faction, lostSystem)`**：中核星系を失った場合、`FactionState.Stability`への追加ペナルティを算出（単純な `LogisticsRules.CohesionFactor` の変動分より大きな値を返す）。「心臓喪失」の不均衡崩壊を定式化。
- **`CohesionWithHeartland(map, faction)`**：既存の `LogisticsRules.CohesionFactor` に中核星系の重みを加味した拡張版（オーバーロード）。
- 接続：`LogisticsRules`（拡張）×`CampaignRules.EffectiveStability`×`DynastyRules.MandateLost`（中核喪失が正統性崩壊を加速）。

#### GIB 二重正統性（国家 vs 宗教の権威競合）（`DualLegitimacyRules`）
- **`InstitutionalRivalry`**（非MonoBehaviour）：`rivalType`（`enum{国家優位,宗教優位,協調,競合}` ）／`tension`（緊張度 0..1）／`concordatLevel`（妥協度）。
- **`DualLegitimacyRules.Tick(rivalry, regime, religion, dt)`**：宗教制度が強く国家が弱いとき `tension` 上昇→高 `tension` で `Regime.legitimacy` から一部が宗教側へ「流出」し実効正統性が低下。
- **解決経路**：`Caesaropapism`（国家が宗教を吸収=tension解消・宗教効果減）／`Theocracy`（宗教が国家を吸収=tension解消・世俗統治力減）／`Concordat`（negotiated split=tension維持・双方が制限的に機能）。
- 接続：`ReligionRules`（宗教制度の強度）×`Regime`/`DynastyRules`（正統性の流出先）×`Organization`（教会が制度化投資の代替主体になる）。

### ★ 中（世界観lore・コード新設なし）

#### GIB（lore）衰亡の開示データ（`DisclosureLedger`）
- 「偉大な国は外敵に滅ぼされるより内から腐る」「市民的徳の喪失が軍事的敗北に先行する」「補助で育てた受給者は帝国を守れない」「宗教的多元主義は求心力にも離心力にもなる」。
- **コード新設なし**：`DisclosureLedger`（FND-4）への**lore データ入力**として記述。CCX-6（世界観codex）方針に一貫。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| クーデター・軍部台頭の新モデル | **`CoupRules`（#215-219）＋`CivilianControlRules`（GOV-4）が既にカバー** |
| 財政崩壊・通貨改鋳の新モデル | **`FiscalRules`/`FiscalState`（#163）＋`CoinageRules`（SAW-1）が既にカバー** |
| 継承法（指名/長子/養子縁組） | **`SuccessionLawRules`（#646）が既にカバー。指名＋適性=養子縁組の近似** |
| 東西分割の行政モデル | **多勢力 `FactionData`＋`DiplomacyRules`＋`OrderOfBattle` の組合せで表現可能** |
| 詳細な歴史的地政学（ライン/ドナウ国境）の新実装 | `LogisticsRules`の`CohesionFactor`＋`StrategicWeightRules`（GIB-4）で十分 |
| 個人スケールの歴史家AIキャラクター（ギボン自身） | キャラ固有設定＝著作権リスク。lore扱いで十分 |

---

## 3. EPIC #GIB の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。既存衰退ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #1275**。GitHub issue 起票済み（#1279〜#1293）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **GIB-1** | #1279 | 世俗的衰退指数とラチェット効果（`DeclineRules`/`DeclineIndex`） | `FactionState`→衰退スコア→回復ペナルティ。`CampaignRules.EffectiveStability`に乗算 |
| **GIB-2** | #1283 | 傭兵化・フォエデラティの二重忠誠構造（`FoederatiRules`/`FoederatiForce`） | `Allegiance`/`LoyaltyRules`×`CivilianControlRules`。傭兵依存→`Organization`空洞化ループ |
| **GIB-3** | #1286 | パンとサーカス — ポピュリズム補助金の双面効果（`BreadAndCircusRules`） | `FiscalRules`×`Organization`×`ConsentRules`。課税・徴兵合意のペナルティ |
| **GIB-4** | #1288 | 中核星系の戦略的重心（`StrategicWeightRules`） | `LogisticsRules`拡張×`CampaignRules.EffectiveStability`。中核喪失の不均衡崩壊 |
| **GIB-5** | #1291 | 二重正統性（国家 vs 宗教の権威競合）（`DualLegitimacyRules`/`InstitutionalRivalry`） | `ReligionRules`×`Regime`×`Organization`。解決経路3択（国家優位/宗教優位/協定） |
| **GIB-6** | #1293 | （lore）衰亡の開示データ（市民的徳の喪失・補助の空洞化・中核喪失） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`GIB-1`（衰退ラチェット＝最も基盤的・他の全 GIB をこの上に乗せる）→ `GIB-2`（傭兵化＝軍の外注はギボンの最大テーマ）→ `GIB-3`（パンとサーカス＝民間側の空洞化＝GIB-2とループ形成）→ `GIB-4`（戦略的重心＝地政学次元の補完）→ `GIB-5`（二重正統性＝`ReligionRules`との接続）→ `GIB-6`（lore整備）。

> いずれも既存衰退・社会・軍事ロジックを**後退させず接続**する additive 設計。銀英伝の「帝国と同盟の長期的腐食」に最も効く。
