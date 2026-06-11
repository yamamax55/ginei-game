# フーコー『監獄の誕生』参考設計（EPIC #PANO）

> 参照元：ミシェル・フーコー著『監獄の誕生 — 監視と処罰』（1975年）。刑務所の誕生を通じて「近代権力はいかに身体を支配するか」を解剖した権力論の古典。
> **規律権力（pouvoir disciplinaire）** と **パノプティコン（一望監視装置）** を核心概念として、処罰から「訓育」へ、公開処刑から「矯正」へと変容した近代統治の論理を描く。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略＋既に豊富な統治・安全保障の純ロジック層）にとって**役に立つ視点**だけを抽出し、EPIC `#PANO` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**権力論／制度メカニクスの構造パターンのみ**を参考にする。

---

## 0. なぜ「監獄の誕生」が本システムに役立つか

当プロジェクトは統治・安全保障の**マクロ純ロジックを相当量保有**している（[CLAUDE.md] 参照）：

| 既存（統治・安全保障） | カバー範囲 |
|---|---|
| `SecurityRules`/`SecurityApparatus`（#166） | 秘密警察の弾圧・クーデター検知・支持ペナルティ |
| `EspionageRules`（DIP-3周辺） | 任務成功確率・情報収集・サボタージュ効果 |
| `ConsentRules`/`Polity` | 合意・非協力（ボイコット）・実効統治力 |
| `GovernanceRules`/`Province` | 安定度・統合度・反乱圧力・産出倍率 |
| `CivilianControlRules`（#145） | 文民統制・軍政関係・クーデター閾値 |
| `CareerPipelineRules`/`SeniorityRules` | 士官学校・科挙・席次vs実力 |
| `MinistryRules`/`Ministry` | 省庁ツリー・縦割り・省益摩擦 |
| `Organization`/`SuccessionRules` | 組織の結束・制度化・カリスマ日常化 |

**しかし、これらは「権力の使用」であり、フーコーが固有に描く以下が欠けている**：

| フーコーが固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **パノプティコン効果 = 見られている意識による予防的服従** | `SecurityRules.DissentSuppression` は弾圧コスト（事後対処）。**監視インフラの存在自体が服従を先手で生む**（事前抑止）回路が無い |
| **規律訓練 = 訓練強度が「従順で有用な身体」を製造する** | `CareerPipelineRules` は出身経路・席次。**訓練の深さが信頼性（規格化）を生みつつ創意を削る**トレードオフが無い |
| **考課・検査制度 = 定期記録が権力の眼差しを個人に刻む** | `Person` に `examRank`（科挙順位）はあるが、**軍人・官僚を定期的に評価し経歴に記録する制度**と、それが服従行動に与える影響が無い |
| **監視と抵抗の弁証法 = 可視化が地下組織を生む** | `SecurityRules` は弾圧→支持低下の一方向。**高密度監視が表面的服従と地下抵抗を同時に生む**逆説が無い |

**結論**：フーコー『監獄の誕生』は当プロジェクトの統治ロジックに**「権力の作動様式」という次元**を加える。既存の `SecurityRules` が「使う権力」なら、フーコーは**①パノプティコン係数（見せるだけで服従させる抑止）②規律訓練（訓育が従順と能力を製造）③考課記録（記録が眼差しを制度化）④監視と抵抗の弁証法（高密度監視が地下組織を育てる逆説）**という4つの欠落軸を与える。**`SecurityRules`（#166）に理論的基盤と動的側面を付与**するのが核心。

---

## 1. 役に立つ視点（要約）

フーコーの論点を、**本システムに効く形**で1行ずつ：

1. **「処罰より訓育」＝身体を作り変える権力**。公開処刑は消え、矯正・訓練・試験が「役に立つ従順な身体」を製造する。→ 既存 `CareerPipelineRules` の「どの経路で育ったか」に**訓練強度の次元**を加える。
2. **パノプティコン = 「見られているかもしれない」が服従を強制する**。監視者がいなくても看守台があれば囚人は自分を律する。→ `SecurityApparatus` の密度が**事前抑止効果**を生む新回路。秘密警察は「使わなくても機能する」。
3. **規律権力は毛細管化する**。軍隊・学校・病院・工場が同一の規律テクノロジーを持つ。→ 省庁・軍・党のすべてが監視インフラを共有する「制度浸透度」の概念。
4. **検査＝可視化の技術**。試験・考課・診断が個人を「ケース（症例）」として記録し、権力の対象に変える。→ 定期考課が人事台帳を充実させ、反乱予兆の検出精度が上がる（`EspionageRules`に経路を与える）。
5. **権力のあるところ必ず抵抗がある**。パノプティコン社会は表面的服従と地下組織を同時に生む。→ 高密度監視→表面的忠誠↑でも地下ネットワーク→クーデター検知コスト↑というトレードオフ。
6. **規律は二刃の剣 = 信頼性↑・自律↓**。規律訓練された部隊は「予測可能で安定的」だが突破口も少ない。→ `AutonomyRules.EmergentSynergy`（傑物前提）との構造的緊張。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`SecurityRules`（#166）/`ConsentRules`/`GovernanceRules` を作り直さない**。PANO はそれらに**欠落軸を接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・フーコーのsignature）

#### PANO パノプティコン係数（可視監視の抑止効果）
- **`SurveillanceState`（純データ）**：`networkDensity`（監視網密度 0..1）/ `visibilityScore`（インフラ可視性）/ `internalizedDiscipline`（自己規律係数）。
- **`PanoptismRules`（static）**：`DeterrentFactor(SurveillanceState)` = `networkDensity × visibilityScore × calibration` → **事前に抑止される反乱圧力**（`GovernanceRules.RebelPressure` に乗算する削減係数）。`InternalizedDiscipline(years, intensity)` = 訓練年数×強度で自己規律が醸成。
- 接続：`SecurityRules.DissentSuppression` コストを `DeterrentFactor` で割引（監視があれば弾圧を使わずに済む）。`GovernanceRules.RebelPressure` への係数。
- **新設**：`SurveillanceState`（Core純データ）＋`PanoptismRules`（static Core）。EditModeテスト必須。

#### PANO 規律訓練と標準化（訓練強度→信頼性↑・創意↓）
- **`DisciplinaryTraining`（純データ）**：`intensity`（0..1 訓練強度）/ `conformityScore`（規格化係数 0..1）/ `roteYears`（訓練年数）。
- **`NormalizationRules`（static）**：`ConformityFactor(training)` = 基礎信頼性修正子（高い → 命令実行確率↑・敗走耐性↑）。`GeniusCost(conformity)` = 高規格化が `AutonomyRules.EmergentSynergy` を減衰（傑物は標準化兵団で突出しにくい）。`NormalizeCareer(person, training)` = `CareerPipelineRules.Stamp` で経路を刻む際に訓練強度を乗せる。
- 接続：`CareerPipelineRules`（士官学校経路への強度パラメータ）×`AutonomyRules.EmergentSynergy`（負の相関）×`FleetMorale.IsRouted`（規律係数が敗走閾値を高める）。
- **新設**：`DisciplinaryTraining`（Core純データ）＋`NormalizationRules`（static Core）。EditModeテスト必須。

### ★★ 高（既存システムへの有意義な拡張）

#### PANO 考課制度（定期記録→人事ファイルの充実→服従の技術化）
- **`PerformanceReview`（純データ）**：`personId` / `reviewYear` / `score`（0..1）/ `reviewerOfficeId` / `flaggedForRisk`（反乱予兆フラグ）。
- **`ExaminationRules`（static）**：`AnnualReview(person, superior)` → `PerformanceReview` を記録。`EffectiveStandingModifier(reviews)` → `SeniorityRules.EffectiveStanding` への補正（昇進に考課を反映）。`PredictDefectionRisk(person, reviews)` → 蓄積記録から反乱予兆を算出（`EspionageRules` の検出精度を高める）。
- 接続：`SeniorityRules`（考課スコアが席次vs実力に追加軸）×`EspionageRules`（人事ファイルの厚さが反乱検出精度に影響）×`GovernmentRegistry`（定期考課を任命台帳に付随）。
- **新設**：`PerformanceReview`（Core純データ）＋`ExaminationRules`（static Core）。EditModeテスト必須。

#### PANO 監視と抵抗の弁証法（高密度監視が地下組織を育てる逆説）
- **`UndergroundNetwork`（純データ）**：`factionId` / `systemId` / `strength`（地下勢力 0..1）/ `detectionDifficulty`（0..1 = 潜伏の深さ）。
- **`ResistanceRules`（static）**：`UndergroundGrowth(surveillance, repression)` = 監視密度と弾圧強度が高いほど地下組織の成長速度が上がる。`DetectionDifficulty(underground, securityApparatus)` = 潜伏深度 vs 秘密警察のクーデター検知確率を下げる係数。`SurfaceCompliance(surveillance)` = 表面的服従率（反乱圧力の表出を抑制するが消滅しない）。
- 接続：`SecurityRules.CoupDetectionChance` に `DetectionDifficulty` を乗算。`ConsentRules.Withdraw`（非協力）の表面抑制と地下蓄積の分離。`PanoptismRules.DeterrentFactor` と `ResistanceRules.UndergroundGrowth` が逆相関する動的緊張。
- **新設**：`UndergroundNetwork`（Core純データ）＋`ResistanceRules`（static Core）。EditModeテスト必須。

### ★ 中（世界観lore・開示データ）

#### PANO（lore）世界観の開示データ（規律社会の反動・地下組織・主体の生産）
- 「規律権力は消えない——処罰から訓育へ形を変えるだけ」「見られることで主体は作られる」「高密度監視の社会が必ず地下を育てる」。
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への**lore データ入力**。秘史/天井CAP世界観EPIC（秘密警察的社会の長期的逆説）に接続。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 秘密警察の実装そのもの | **`SecurityRules`/`SecurityApparatus`（#166）が既にカバー** |
| クーデターの成功確率 | **`CivilianControlRules.WouldCoup`（#145）がカバー** |
| 弾圧コスト→支持低下 | **`SecurityRules.RepressionSupportPenalty`がカバー** |
| 合意の撤退（ボイコット） | **`ConsentRules.Withdraw`（#836）がカバー** |
| 支配の3類型（カリスマ/伝統/合法） | **これはウェーバーの概念。backlog未処理の「職業としての政治」に委ねる** |
| 教育制度の内容そのもの（カリキュラム等） | タイクン化回避。訓練強度の係数のみで十分 |
| 監視カメラ・デジタル監視のUI | シーン/UI配線は対象外。純ロジックのみ |

---

## 3. EPIC #PANO の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。既存統治ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**権力論メカニクス/構造パターンのみ**参考。

> **EPIC = #1506**。GitHub issue 起票済み（#1507〜#1509, #1512, #1515）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **PANO-1** | #1507 | パノプティコン係数（`SurveillanceState`・`PanoptismRules`）= 監視インフラ密度→事前抑止効果 | `SecurityRules.DissentSuppression`コスト割引 / `GovernanceRules.RebelPressure` 削減係数 |
| **PANO-2** | #1508 | 規律訓練と標準化（`DisciplinaryTraining`・`NormalizationRules`）= 訓練強度→信頼性↑・`AutonomyRules.EmergentSynergy`↓ | `CareerPipelineRules`×`AutonomyRules`×`FleetMorale` |
| **PANO-3** | #1509 | 考課制度（`PerformanceReview`・`ExaminationRules`）= 定期記録→昇進反映・反乱予兆検出 | `SeniorityRules.EffectiveStanding`補正 / `EspionageRules`精度向上 |
| **PANO-4** | #1512 | 監視と抵抗の弁証法（`UndergroundNetwork`・`ResistanceRules`）= 高密度監視→地下組織成長の逆説 | `SecurityRules.CoupDetectionChance`低下係数 / `PanoptismRules`との動的緊張 |
| **PANO-5** | #1515 | （lore）世界観の開示データ（規律社会の逆説・「見られることで主体は作られる」・地下組織の必然性） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順

`PANO-1`（パノプティコン係数＝最も固有で欠落の大きいsignature）→ `PANO-2`（規律訓練＝訓育の機械的効果）→ `PANO-4`（監視と抵抗の弁証法＝動的緊張・PANO-1の逆説として自然な次ステップ）→ `PANO-3`（考課制度＝既存`SeniorityRules`への接続）→ `PANO-5`（lore・コード新設なし）。

> いずれも既存統治・安全保障ロジックを**後退させず接続**する additive 設計。`SecurityRules`（#166）の**理論基盤と動的拡張**として最も効く。
