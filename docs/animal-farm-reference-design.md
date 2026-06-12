# オーウェル『動物農場』参考設計（EPIC #FARM）

> 参照元：ジョージ・オーウェル『動物農場』(1945)。農場動物が人間支配者を革命で打倒するが、
> 指導層の豚が徐々に旧支配者と同型の抑圧者になるという寓話。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風星間国家戦略＋既に巨大な政治・社会シミュ層）にとって
> **役に立つ視点だけ**を抽出し、EPIC として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**政治メカニクス／世界観の構造パターンのみ**を参考にする。

---

## 0. なぜ「動物農場」が本システムに役立つか

当プロジェクトは政治・社会シミュの**マクロ純ロジックを大量に保有**している：

| 既存（政治・社会シミュ） | カバー範囲 |
|---|---|
| `Regime`/`DynastyRules`（#867） | 正統性/腐敗/徳 → 天命喪失/改革/易姓革命 |
| `Organization`/`SuccessionRules`（#812/#814） | 結束/制度化/個人カリスマ → 英雄死後の継承 |
| `Polity`/`ConsentRules`（#836） | 権力は借り物・協力/正統性/抑圧 → 非協力で統治不能 |
| `HopeRules`/`Community`（#852） | 希望/末人フラグ・信仰ルート/秩序ルート |
| `SecurityRules`/`SecurityApparatus`（#166） | 秘密警察・抑圧/クーデター検知 |
| `CoupRules`（#215） | 軍部/宮廷/革命クーデター・成功率/後処理正統性 |
| `LoyaltyRules`/`BattleAllegianceRules`（#817） | 条件付き忠誠・寝返り/カスケード |
| `FactionStateRules`/`FactionState` | 上記モジュールの合成層 |
| `EventEngine`/`GameEventDef`（#116） | 条件発火→通知/選択肢→効果 |
| `DisclosureLedger`/`DisclosureRules`（FND-4） | 秘史開示・前提条件連鎖 |

**しかし、これらは権力者の「腐敗」「弾圧」「改革」を抽象スカラーで扱う**。動物農場が固有に描く以下が欠けている：

| 動物農場が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **建国理念の文書化と段階的書き換え**（七つの戒律のドリフト） | `Regime.corruption` は抽象スカラー。**理念を条文として記録し、逸脱度・書き換え試行・発覚コスト**を追う仕組みがない |
| **権力者による言説支配**（スクイーラーの「説明」ループ） | `SecurityRules.DissentSuppression` は物理弾圧。`HopeRules.Faith` は信者の自発的信仰。**権力が言説空間を占拠して規範を語り直す**「プロパガンダの正当化ループ」がない |
| **スケープゴーティング**（不在のスノーボールへの帰属） | `EventEngine` でイベント化は可能だが、「架空/不在の敵を指定→問題を帰属させて支持維持→暴露で崩壊」の純ロジック窓口がない |
| **忠実な支持者ほど搾取される逆説**（ボクサーの消耗） | `ConsentRules` で協力→統治可能だが、「協力度が高い集団がより多く絞られ最終的に使い捨てられる」非対称がない |

**結論**：動物農場は当プロジェクトの抽象的な「腐敗スカラー」と「弾圧パラメータ」に、①理念文書の段階的書き換え ②言説統制・プロパガンダ正当化 ③スケープゴーティング ④忠実者搾取の逆説、という**4つの構造パターン**を与える。`Regime`/`Polity`/`SecurityRules`/`LoyaltyRules` の間を接続する新しい歯として機能する。

---

## 1. 役に立つ視点（要約）

動物農場の構造パターンを、**本システムに効く形**で1行ずつ：

1. **「革命の堕落」は一度の転換でなく、小さな逸脱の積み重ね**。条文が一夜で変わるのではなく毎回「説明」される。→ **ManifestoRules** ＋ **PropagandaRules** で段階的ドリフトを数値化。
2. **建国理念を文書（マニフェスト）に記録すると、逸脱が検知可能になり正統性コストが発生する**。文書があるからこそ「書き換え」がリスクになる。→ `Regime.corruption` × `DynastyRules.LegitimacyConflict` の精密化（条文スケール）。
3. **言説を独占した権力は弾圧なしに正当化できる**。物理弾圧（秘密警察）より安く、発覚しにくい。→ `SecurityRules.DissentSuppression`（物理）と `PropagandaRules`（言説）の二段構えで弾圧コスト構造を精緻化。
4. **架空の敵は連帯の糊として機能する**が、情報が漏れると正統性が崩壊する（`EspionageRules` の逆用）。→ `ScapegoatRules` ＋ 情報統制（FARM-3）が連動する動学。
5. **最も協力的な層が最も搾取される**。`ConsentRules.ControlStrength`（協力度で統治可能）の逆張り——協力度が高いほど搾取強度も上がる非対称。→ `FiscalRules.TaxBurdenPenalty` × 階層別`RedistributionRules` への新しい係数。
6. **革命後の新体制は旧体制と同型化する**。SuccessionRules（制度化）が低いまま覇権が移ると「新しい豚」になる。→ 既存 `Organization`/`Regime` で表現可能、新規不要（lore として `DisclosureLedger` へ）。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**Regime/Polity/SecurityRules を作り直さない**。FARM はそれらに**欠落軸を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・動物農場の signature）

#### FARM 理念文書と段階的書き換え（`ManifestoRules` ＋ `Manifesto`）
- **Manifesto**（純データ）：勢力の建国宣言／革命原則を**条文リスト**（`Article` = id/text/authorityScore）として記録。`baselineSnapshot`（発効時の条文コピー）＋ `currentArticles`（現在条文）。
- **ManifestoRules**（static・純ロジック）：`DriftScore`（現在条文 vs. 基準スナップショットの平均逸脱度 0..1）／`AmendAttempt`（書き換え試行＝支配層の「意図」を渡す。成功コスト＝正統性消費、失敗は即発覚）／`DetectionRisk`（情報統制度 FARM-3 が低いほど書き換えが露見しやすい）／`DriftToLegitimacyPenalty`（乖離が正統性に与える係数）。
- 接続：`Regime.corruption`（腐敗に DriftScore を加算）× `DynastyRules.LegitimacyConflict`（乖離が一定を超えると改革者/反乱発火）× `EventEngine`（書き換え成功/発覚イベント）。

#### FARM 言説統制・プロパガンダ正当化ループ（`PropagandaRules` ＋ `NarrativeControl`）
- **NarrativeControl**（純データ）：`controlLevel`（0..1・言説支配度）＋ `legitimizationCost`（条文逸脱を「説明」して正当化するコスト・資源か正統性消費）。
- **PropagandaRules**（static・純ロジック）：`Legitimize(manifesto,driftScore,control)`（言説統制が高いと正当化でき、正統性ペナルティを一時抑制）／`InformationControlFactor`（統制度で `DetectionRisk` を下げる）／`Backfire`（過度な統制が長期的に希望ドリフトを下げる）。
- 接続：`SecurityRules.DissentSuppression`（物理抑圧）と `PropagandaRules`（言説抑圧）の二段構え。`HopeRules.Faith`（信仰による希望操作）は自発的信仰なので別経路。

### ★★ 高（核の欠落・重要な構造パターン）

#### FARM スケープゴーティング（`ScapegoatRules`）
- **ScapegoatTarget**（純データ）：`targetId`（架空/不在の敵の id。`Person` 参照 or null）＋ `believability`（信憑性 0..1・情報統制度と逆相関）＋ `supportBoostPerCycle`（指定中の支持維持係数）。
- **ScapegoatRules**（static・純ロジック）：`Designate(target,control)`（スケープゴート指定→支持ボーナス）／`Persist(target,espio,control)`（毎ターンの維持コスト＝信憑性低下）／`ExposureRisk(target,espio)`（諜報力が高いほど暴露リスク↑）／`Expose(target)`（発覚→支持ボーナス反転＝正統性崩壊）。
- 接続：`EspionageRules`（暴露リスク）× `PropagandaRules.InformationControlFactor`（統制度でリスク低減）× `EventEngine`（発覚イベント発火）。

#### FARM 忠実な支持者ほど搾取される逆説（`LoyalistExtractionRules`）
- **LoyalistExtractionRules**（static・純ロジック）：`ExtractionMultiplier(cooperationLevel)`（協力度が高い集団への搾取率の係数＝`cooperationLevel` が高いほど 1.0 を超えて税/動員が重くなる・`MaxExtractionRatio` で上限）／`AttritionRisk`（搾取が一定ライン超で最忠実な層が消耗→`Community.hope` 急落）／`BreakingPoint`（消耗が閾値を超えると離反トリガー＝`ConsentRules.Withdraw` へ連鎖）。
- 接続：`ConsentRules`（協力度が出所）× `FiscalRules.TaxBurdenPenalty`（税負担ペナルティの係数として乗算）× `RedistributionRules`（低所得者ほど忠実で搾取される累積不平等）。

### ★ 中（世界観 lore・コード新設なし）

#### FARM （lore）世界観の開示データ
- 「革命は一度の出来事でなく段階的な腐敗過程」「スローガンが逆転するとき制度が死ぬ」「打倒した支配と同型化した瞬間が革命の終わり」。
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への**lore データ入力**。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 革命そのものの実装 | **`CoupRules`（#215）/`DynastyRules.Revolution`** で既にカバー |
| 秘密警察・恐怖政治 | **`SecurityRules`/`SecurityApparatus`（#166）** が既にカバー |
| 多数決・選挙の書き換え | **`PartyRules`（GOV-6）/`LeadershipElectionRules`（GOV-7）** で既にカバー |
| クーデター後の正統性再建 | **`CoupRules.PostCoupLegitimacy`（#215）** で既にカバー |
| 農業経済・食料生産 | **`ResourceProductionRules`（#93）** の延長で対応可能。新EPICとしない |
| 世代交代・後継者 | **`VacancyRules`/`SuccessionRules`（#812/#152）** で既にカバー |

---

## 3. EPIC #FARM の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。
> 既存 Regime/Polity/SecurityRules は**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **FARM-1** | #2220 | `ManifestoRules`＋`Manifesto`（理念文書の条文化・DriftScore・書き換えコスト） | `Regime.corruption`×`DynastyRules.LegitimacyConflict`×`EventEngine` |
| **FARM-2** | #2221 | `PropagandaRules`＋`NarrativeControl`（言説統制・プロパガンダ正当化ループ） | `SecurityRules.DissentSuppression`×FARM-1の`DetectionRisk`×`HopeRules` |
| **FARM-3** | #2224 | `ScapegoatRules`（架空/不在の敵指定→支持維持→暴露で正統性崩壊） | `EspionageRules`×FARM-2の`InformationControlFactor`×`EventEngine` |
| **FARM-4** | #2227 | `LoyalistExtractionRules`（忠実な支持者ほど搾取される「ボクサーのパラドックス」） | `ConsentRules`×`FiscalRules.TaxBurdenPenalty`×`RedistributionRules` |
| **FARM-5** | #2230 | `IdeologicalDriftRules`（DriftScore→`FactionStateRules`/`CampaignRules`への統合） | FARM-1×FARM-2を`FactionState`/`CampaignState`合成層へ配線 |
| **FARM-6** | #2234 | （lore）世界観の開示データ — 革命の堕落・スローガンの逆転・新支配と旧支配の同型化 | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`FARM-1`（ManifestoRules＝最も固有で欠落の大きい signature）→ `FARM-2`（PropagandaRules＝FARM-1のDetectionRisk連動）→ `FARM-3`（ScapegoatRules＝情報統制と交差）→ `FARM-4`（LoyalistExtraction＝FiscalRules接続・独立性が高い）→ `FARM-5`（統合配線）→ `FARM-6`（lore）。

> いずれも既存 Regime/Polity/Security を**後退させず接続**する additive 設計。腐敗した帝国・末期王朝・革命後の体制に最も効く。
