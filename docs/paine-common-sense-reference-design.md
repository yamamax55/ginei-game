# ペイン『コモン・センス』参考設計（EPIC #PNCS）

> 参照元：トマス・ペイン（Thomas Paine）『コモン・センス』（Common Sense, 1776）。
> アメリカ植民地の独立運動を点火した政治パンフレット。平易な文体で王政・世襲の非合理を論駁し、
> 独立を「誰でも分かる常識」として提示。出版から数ヶ月で10万部以上を流通させ世論を転換した。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略）にとって**役に立つ構造パターン**
> だけを抽出し、EPIC `#PNCS` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**世論動学／独立メカニクスの構造パターンのみ**参考。

---

## 0. なぜ「コモン・センス」が本システムに役立つか

当プロジェクトは「正統性の侵食」と「大衆の動き」に関する純ロジックをすでに多数保有している：

| 既存（カバー範囲） | カバー内容 |
|---|---|
| `DynastyRules`/`Regime`（#867） | 腐敗→正統性低下→天命喪失→革命。時間経過で進む |
| `ConsentRules`/`Polity`（#836） | 被支配者の非協力・ボイコット。統治力=戦力+協力×人口 |
| `NonviolenceRules`/`Movement`（#831） | 弾圧の可視化→沈黙の多数を支持へ転換。`PressurePolity` |
| `GovernanceRules`（#109） | 安定度・反乱リスク。`RebelPressure`/`IdeologyModifier` |
| `LoyaltyRules.ResolveCascade`（#817） | 軍事的忠誠の不動点カスケード |
| `DisclosureLedger`（FND-4） | 秘史の連鎖開示。条件→開示→効果 |
| `EventEngine`（#116） | イベント発火→選択肢→効果 |
| アンダーソン #1874 | 出版資本主義が国民を製造（印刷→ナショナリズム・緩慢な過程） |
| アーレント #1513 | 大衆のアトム化→運動の生成（全体主義動態） |
| フェデラリスト #1470 | 派閥の逆説・複合共和制（独立後の制度設計） |

**しかし、これらは「正統性の時間的侵食」「軍事忠誠の切り替え」「憲法設計」を扱い、
コモン・センスが固有に描く以下が欠けている**：

| コモン・センスが固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **プロパガンダ媒体そのものを設計するメカニクス** | `EventEngine` は一発イベント。**一つの著作物が拡散し続け世論に累積効果を与える**回路が無い |
| **正統性の前提論駁**（世襲・君主制の非合理を論証） | `Regime.legitimacy` は腐敗で自然減。**外部からの哲学的攻撃**（前提を問い直す論証）が無い |
| **世論点火・自己加速カスケード**（民政領域） | `LoyaltyRules.ResolveCascade` は会戦中の軍事忠誠。**民政世論の潜在不満→点火→自己加速**が無い |
| **分離独立メカニクス**（属領→新勢力） | 軍事征服による所有変動はある。**感情・理念に駆動された自発的独立宣言**→新勢力生成が無い |
| **平易性（明瞭度）を世論効果の乗数にする** | 情報の非対称（SAW-3）とは別。**論証の明瞭さが拡散速度を規定する**回路が無い |

**結論**：コモン・センスは当プロジェクトの正統性・世論システムに、
**①プロパガンダ媒体の拡散モデル ②正統性の前提攻撃 ③民政世論の点火カスケード ④自発的分離独立**
という4つの欠落軸を与える。これにより「武力でなく理念で銀河が動く」当プロジェクトの王道路線（ALM、SAW 等と同系）を
**起動フェーズ側（革命の点火）から強化**する。

---

## 1. 役に立つ視点（要約）

コモン・センスの世界観を、**本システムに効く形**で1行ずつ：

1. **一つのパンフレットが世論を転換する**。出版→平易な論証が広まる→潜在的な不満が言語化される→独立支持が臨界を超える。→ 当プロジェクトの「著作物を戦略リソースとして設計する」回路に直結（`DisclosureLedger` とは別の「拡散型媒体」）。
2. **正統性の前提を論駁する**。「世襲は非合理だ」という論証そのものが王権の土台を崩す。→ `DynastyRules` の腐敗ベース衰退に加え、**理性的批判による直接攻撃**の経路を開く。
3. **平易さが伝播速度の乗数になる**。難解な論文でなく誰でも読める平文→識字層全体への一斉浸透。→ 著作物の「明瞭度パラメータ」が拡散係数を決める。
4. **「今こそ分岐点」の緊迫感**。ペインは「いま行動しなければ機会は消える」という不可逆性を強調した。→ タイムプレッシャーのある独立宣言イベント（機会窓口が開閉する）。
5. **潜在不満と論証の掛け算**。不満がない集団には効かない。不満が高い集団では爆発的に広がる。→ `GovernanceRules.RebelPressure` × 媒体効果の積が世論デルタ。
6. **独立=新たな正統性の創出**。単なる反乱でなく「新しい体制の正当な建設」を主張。→ 分離独立の後に `FactionData` を新設し初期正統性を与える。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`DynastyRules`/`ConsentRules`/`NonviolenceRules`/`GovernanceRules` を作り直さない**。
> PNCS はそれらに**欠落軸を足し接続する**だけ（additive）。タイクン化回避＝媒体設計は高位の決断、拡散はエンジン駆動。

### ★★★ 最優先（真の欠落・コモン・センスの signature）

#### PNCS-1 プロパガンダ媒体モデル（`PropagandaWork`/`PropagandaRules`）
- **`PropagandaWork`** (純データ)：`clarity`（明瞭度0..1）×`reach`（到達人口規模）×`grievanceAffinity`（どの不満軸に刺さるか）×`targetPremise`（攻撃する正統性の前提）。
- **`PropagandaRules.SpreadEffect(work, province, grievance, dt)`**：`delta = work.clarity × work.reach × grievanceFactor × dt`。`grievanceFactor = (RebelPressure / max)` を `GovernanceRules` から読む。
- 累積効果：`province.propagandaExposure`（0..1）に蓄積→閾値で `PNCS-3 OpinionIgnitionRules` へ連鎖。
- 接続：`GovernanceRules.RebelPressure`・`EventEngine`（著作物の発行イベント）・`PNCS-2`（前提攻撃）・`PNCS-3`（点火）。

#### PNCS-2 正統性前提攻撃（`LegitimacyPremiseRules`）
- **`LegitimacyPremise` enum**：`{世襲君主制, 神権委任, 征服権, 慣習的権威, 信任契約}`。
- **`LegitimacyPremiseRules.AttackPremise(regime, premise, exposure)`**：`exposure`（PNCS-1 の蓄積量）に比例して `Regime.legitimacy` に直接マイナスを与える（腐敗ベースの自然減とは**別経路**）。
- **`PremiseVulnerability(regime, premise)`**：世襲政体は「世襲君主制」への批判に脆弱（高い）、民主政体には無効（0）。
- 接続：`Regime`/`DynastyRules`（legitimacy源）・`PNCS-1`（exposure入力）・`FactionStateRules`。

### ★★ 高（民政世論の動学・独立メカニクス）

#### PNCS-3 世論点火・自己加速カスケード（`OpinionIgnitionRules`）
- **`OpinionPool`**（勢力/星系単位の政治世論プール）：`sentiment`（独立支持度0..1）・`threshold`（点火臨界）・`momentum`（加速項）。
- **点火動学**：`sentiment < threshold` → 線形緩慢増加；`sentiment ≥ threshold` → `momentum` 正帰還で加速（シグモイド曲線）；飽和近傍で再び鈍化。
- **`Tick(pool, propagandaExposure, dt)`**：PNCS-1 の露出量が高いほど `threshold` が低下（より点火しやすい）。
- 接続：`PNCS-1`（露出入力）・`ConsentRules.Withdraw`（`sentiment` 高で非協力発火）・`NonviolenceRules.IsTriumphant`・`PNCS-4`（独立宣言の発火源）。

#### PNCS-4 分離独立メカニクス（`SecessionRules`）
- **`SecessionState`**：`independenceSentiment`（PNCS-3 の `sentiment`）・`capability`（自衛兵力）・`opportunity`（宗主勢力の弱体化＝`FactionStateRules.Stability` の逆数）。
- **`CanDeclare(state, polity)`**：`sentiment × capability × opportunity ≥ threshold` で独立宣言可能。
- **`Declare(state, map, registry, newFactionTemplate)`**：新 `FactionData` を生成＋`StarSystem.owner` を新勢力へ変更＋初期正統性を `initialLegitimacy`（独立の大義分）で設定＋`DiplomacyRules.DeclareWar` で旧宗主と交戦状態へ。
- **`GrievanceToSentiment(province, ownerData, dt)`**：未統合・低安定・思想ミスマッチが `independenceSentiment` を積み上げる（`GovernanceRules.IdeologyModifier` 流用）。
- 接続：`GovernanceRules`・`PNCS-3`・`StrategyRules`・`DiplomacyRules`（DIP-1）・`FactionData`。

### ★ 中（lore・世界観開示）

#### PNCS-5（lore）コモン・センスの思想構造 — `DisclosureLedger` への開示データ
- **コード新設なし**。`DisclosureEntry` に以下を追加するデータ作業：
  - 「制度の自明視を問い直す」（世襲はなぜ当然とされてきたか）
  - 「独立の常識性」（今こそ分岐点・機会の窓）
  - 「共和政の可能性」（新しい正統性の雛形）
- 先行開示との連鎖：`PNCS-2` の `LegitimacyPremise.世襲君主制` 攻撃を条件に解放。
- 接続：`DisclosureLedger`（FND-4）・`PNCS-2`。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 印刷・出版業そのものの経済モデル | SAW-4/SCM#982/FRM#1022 でカバー。コモン・センスは「媒体の効果」に特化し経済は扱わない |
| 出版資本主義→国民意識の緩慢な形成 | **アンダーソン #1874 がカバー**。PNCS は緩慢な形成でなく急速な点火に特化 |
| 暗号・情報の非対称 | **孫子#1125・暗号解読#1900・SAW-3 がカバー** |
| 議会政治・投票制度 | **フェデラリスト #1470・`PartyRules`#159 がカバー**（独立後の設計） |
| 一般的な革命メカニクス全般 | `DynastyRules.Revolution` が既存。PNCS は「点火フェーズ」のみ追加 |
| 徴兵・軍建設 | **坂の上の雲 #1430・ハインライン etc.** 別系統 |

---

## 3. EPIC #PNCS の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。
> **既存 `DynastyRules`/`ConsentRules`/`NonviolenceRules`/`GovernanceRules` は作り直さない。additive 設計。**

> **EPIC = #2143**。GitHub issue 起票済み（#2146〜#2162）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **PNCS-1** | #2146 | プロパガンダ媒体モデル（`PropagandaWork`/`PropagandaRules`・明瞭度×不満×拡散） | 新 `PropagandaRules`。`GovernanceRules`×`EventEngine`×PNCS-3 |
| **PNCS-2** | #2150 | 正統性前提攻撃（`LegitimacyPremiseRules`・世襲/神権etc.への論証的攻撃） | `Regime`/`DynastyRules` への直接 legitimacy delta。PNCS-1 の exposure 入力 |
| **PNCS-3** | #2155 | 世論点火・自己加速カスケード（`OpinionIgnitionRules`・閾値→シグモイド加速） | `PNCS-1`×`ConsentRules`×`NonviolenceRules.IsTriumphant`×PNCS-4 |
| **PNCS-4** | #2159 | 分離独立メカニクス（`SecessionRules`・不満蓄積→独立宣言→新勢力生成） | 新 `SecessionRules`。`GovernanceRules`×`StrategyRules`×`DiplomacyRules` |
| **PNCS-5** | #2162 | （lore）コモン・センスの思想構造 — `DisclosureLedger` 開示データ | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`PNCS-1`（媒体モデル＝基盤）→ `PNCS-2`（前提攻撃＝PNCS-1の出力を消費）→ `PNCS-3`（点火カスケード＝PNCS-1/2の先）→ `PNCS-4`（独立宣言＝PNCS-3の帰結）→ `PNCS-5`（lore・コードなし）。

> PNCS-1〜4 はいずれも既存マクロ政治層を**後退させず接続**する additive 設計。
> 「プロパガンダ→前提攻撃→世論点火→独立」の4段階が一連のパイプラインを形成する。
