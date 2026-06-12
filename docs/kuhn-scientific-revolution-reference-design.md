# クーン『科学革命の構造』参考設計（EPIC #KUHN）

> 参照元：トーマス・クーン『科学革命の構造』(1962)。
> 科学の進歩は**連続的な真実の蓄積**でなく、「通常科学→異常蓄積→危機→パラダイムシフト→新通常科学」という**断絶的な革命**で起きる——という知識論の転換点。
> 本ドキュメントは、当プロジェクト（Ginei＝星間国家戦略＋技術/ドクトリン体制）にとって**役に立つ視点**だけを抽出し、EPIC `KUHN` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**メカニクス／世界観の構造パターンのみ**を参考にする。

---

## 0. なぜ「科学革命の構造」が本システムに役立つか

### 既存（カバー範囲）

| 既存（類似・関連） | カバー範囲 |
|---|---|
| `ResearchRules` / `ResearchProject` (#123-127) | 線形研究産出・政体偏り `IdeologyBias` |
| `DynastyRules` / `Regime` (#867) | 政治体制サイクル（腐敗→改革→革命） |
| `EventEngine` (#116) | 条件発火→効果の離散イベント |
| `SeniorityRules` (#155/156) | 席次vs実力・世代交代の硬直 |
| `OrganizationRules` / `SuccessionRules` (#812) | カリスマ死後の組織存続・制度化 |
| `GrowthRules` / `Growth` (#537-543) | 個人の経験成長曲線（アーキタイプ別） |
| `CareerPipelineRules` | 人材出自・士官学校/科挙/テクノクラート |
| `LifecycleRules` / `Calendar` (#151/152) | 年齢・死亡・世代交代 |
| `RetirementRules` (#530-536) | 停年・アップオアアウト・戦時召集 |

### 欠落軸（クーンが固有に持つ視点 × 当プロジェクトでの欠落）

| クーンが固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **パラダイム体制**＝技術/ドクトリンが「支配的フレームワーク」の中で動く | `ResearchRules` は線形産出。**支配パラダイムへの適合が効率を決め、外れが創造的破壊になる**回路が無い |
| **通常科学の安定 + 異常蓄積 → 危機 → 革命**という非線形進歩 | 研究も政治変化も線形か離散イベント。**異常が蓄積してから閾値で一気に転換**という動学が無い |
| **パラダイムシフトの創造的破壊**＝前パラダイムの研究が一部陳腐化し、新領域が爆発 | `ResearchRules` には「技術断絶」が無い。突破口が出ると旧投資が無駄になる非対称性が無い |
| **不可通約性**＝旧パラダイム保持者は新パラダイムを「見られない」＝世代交代が必要 | `SeniorityRules` は序列を扱うが、**知識体制の閉鎖性**（旧パラダイム者への採用コスト・死ぬまで変わらない）が無い |
| **科学者共同体の合意形成**＝パラダイム採用は証拠強度 × コミュニティ開放性の社会的プロセス | 研究は勢力単独で産出。**コミュニティ合意によって新技術/ドクトリンが正統化される**仕組みが無い |
| **軍事ドクトリンとしてのパラダイム**＝戦術体系が「正統ドクトリン」を構成し、異端提督が革命を起こす | 陣形/提督能力はあるが**「支配ドクトリン vs 異端革命」という体制レベルの対立軸**が無い |

**結論**：クーン『科学革命の構造』は当プロジェクトの技術・ドクトリン層に**「体制としての知識フレームワーク」**という視点を与える。①通常科学の安定効率 ②異常蓄積→危機→断絶的突破 ③不可通約性＝世代交代の必要 ④コミュニティの合意形成という4欠落軸が、**戦略ゲームの研究・ドクトリン・世代交代を非線形で面白くする**。特に**軍事ドクトリンとしてのパラダイム**は銀英伝の「革新 vs 旧守派」という対立構造に直結する。

---

## 1. 役に立つ視点（要約）

クーンの世界観を、**本システムに効く形**で1行ずつ：

1. **進歩は連続でなく断絶**。通常科学の効率的なパズル解きが、蓄積した異常によって危機を経てパラダイム転換する。→ `ResearchRules` の線形産出に**非線形スパイク**を与え、技術的転換点が戦略上の断層になる。
2. **通常科学の安定は強みでも弱み**。パラダイム内では効率が上がるが、外れ値（異常）を見落とす。→ 支配技術体制に沿って研究すると効率が上がるが、**体制から外れた革新は異端扱いで遅れる**——実効値パターン。
3. **軍事ドクトリンはパラダイム**。「正統戦術」で鍛えた提督は正統との戦いに強いが、革命的新ドクトリンに対応できない。→ `AdmiralData.preferredFormation`（#104得意陣形）を超えた**ドクトリン体制**の概念。
4. **不可通約性＝世代交代が必要**。旧パラダイムの提督/科学者は証拠を見ても転向できない。新パラダイムが広まるには彼らの退役/死が前提条件。→ `LifecycleRules`/`SeniorityRules` に知識政治のコストを与える（既存問題#812カリスマ日常化の技術版）。
5. **コミュニティの合意が正統化**。個人の天才だけでは革命は完成しない。勢力・機関・同僚の採用によってパラダイムが「事実」になる。→ 孤高の革新者が孤立死するか、勢力がドクトリン転換を制度化するかの分岐。
6. **パラダイムシフトはゲシュタルト転換**。世界の「見え方」が変わる体験——`DisclosureLedger` の秘史開示と共鳴。既存 #450 開示EPIC に接続。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`ResearchRules`/`EventEngine`/`SeniorityRules`/`LifecycleRules` を作り直さない**。KUHN はそれらに**欠落軸を足し、接続する**だけ（additive）。タイクン化回避＝研究マイクロなし・パラダイム体制という高位決断のみ。

### ★★★ 最優先（真の欠落・クーンのsignature）

#### KUHN-1 研究パラダイムモデル（`ResearchParadigm` / `ParadigmRules`）
- **パラダイム**は「支配的な研究フレームワーク」：`field`（得意研究領域）＋`efficiency`（通常科学効率倍率）＋`anomalyAccumulation`（異常カウンタ）＋`crisisThreshold`（危機閾値）。
- `NormalScienceBonus(research, paradigm)`：パラダイム内研究に効率ボーナス（通常科学の安定）。
- `AccumulateAnomaly(research, paradigm)`：パラダイムから外れる研究ほど異常が積まれる。
- `IsInCrisis(paradigm)`：閾値超過→危機状態。
- `PathSwitchCost(committedResearch, paradigmAge)`：パラダイム転換の移行コスト（旧研究の一部陳腐化）。
- 接続：`ResearchRules` の非破壊拡張（線形産出は維持）。`FactionState.IdeologyBias` と並走。純ロジック・test-first。

#### KUHN-2 パラダイムシフトと技術断絶（`ResolveShift` / `ShiftEffect`）
- 危機（`IsInCrisis`）が `EventEngine` のトリガになり、**パラダイムシフト**イベントを発火。
- `ResolveShift(oldParadigm, newParadigm)` → `ShiftEffect{gainFields, lossFields, multiplier}`：
  - `gainFields`（突破口領域）：研究産出が爆発的倍増。
  - `lossFields`（陳腐化領域）：積み上げた研究が一部無駄になる（路依存損失）。
- 接続：`EventEngine`（危機→シフトイベント）＋`ResearchRules`（効果適用）。純ロジック・test-first。

### ★★ 高（軍事・社会への展開）

#### KUHN-3 軍事ドクトリンのパラダイム（`DoctrinalParadigm` / `DoctrineParadigmRules`）
- 勢力の「支配ドクトリン」＝現在の優勢な戦術体系（`DoctrinalParadigm{doctrine, orthodoxy, anomalyCarriers}`）。
- `WithinParadigmBonus(admiral, paradigm)`：支配ドクトリンに従う提督は交戦効率UP。
- `AnomalyCarrierScore(admiral)`：高機動×高運営の提督が異端（異常キャリア）＝ドクトリン異常を加速。
- `RevolutionaryEncounterPenalty(admiral, newDoctrine)`：支配ドクトリン保持者が革命的新ドクトリンの敵と戦う際のペナルティ（`CombatModifiers` に入力）。
- 接続：`AdmiralData`（実効値パターン・基準値非破壊）×`CombatModifiers`（#106）×`AdmiralSkillRules`（#137-140）。得意陣形#104の体制レベルへの拡張。純ロジック・test-first。

#### KUHN-4 不可通約性＝世代交代と新パラダイム受容（`IncommensurabilityRules`）
- 旧パラダイム保持者は新パラダイムを採用できない：`AdoptionResistance(seniority, age, paradigmCommitment)` → 0..1（1＝完全拒否）。
- `IsConvertible(person)`：若者・低seniority・テクノクラートは転換しやすい。旧将官は不可。
- `DiffusionRate(communityOpenness, evidenceWeight)`：コミュニティ全体のパラダイム拡散速度。
- **新パラダイムの完全制度化は旧世代の退役/死を待つ**＝`LifecycleRules`/`RetirementRules` との接続で「速度の下限」が定まる。
- 接続：`SeniorityRules`×`LifecycleRules`×`RetirementRules`×`CareerPipelineRules`。純ロジック・test-first。

### ★ 中（コミュニティ合意・世界観lore）

#### KUHN-5 科学者共同体の合意形成（`ParadigmCommunityRules`）
- 新パラダイムの採用は証拠強度 × コミュニティ開放性の**社会的プロセス**：`CommunityAcceptance(evidenceWeight, communityOpenness, dissenterStrength)` → 受容率。
- `CommunityOpenness`：勢力思想・制度化度・`SeniorityRules.PoliticalRigidity` に由来。
- 接続：`FactionState`（思想・制度化）×`PersonRules`（人物の意見形成）×`SeniorityRules.PoliticalRigidity`。改宗圧力（`ReligionRules`）の知識版として類似構造を借用。純ロジック・test-first。

#### KUHN-6 （lore）世界観の開示データ
- 「科学に絶対真理はなく、よりよいパラダイムがある」「銀河文明の歴史はパラダイムシフトの連鎖」「超古代技術は旧パラダイムの遺物＝現代人には不可通約」。
- コード新設なし。`DisclosureLedger`（FND-4）への**lore データ入力**。世界観EPIC（秘史/CAP/超古代文明）に接続。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 政治体制の改革/革命 | **`DynastyRules.Reform`/`Revolution` がカバー**。KUHNは科学/技術/ドクトリンに限る |
| 個人の成長・学習曲線 | **`GrowthRules` がカバー**。KUHNはパラダイム体制レベルのみ |
| 宗教の改宗圧力 | **`ReligionRules` がカバー**。KUHNは類似構造を参照するが実装は既存に委ねる |
| イデオロギーの政体偏り | **`ResearchRules.IdeologyBias` がカバー**。KUHNは異常蓄積の非線形ロジックのみ足す |
| 人材採用・天才発見 | **`PersonRules.BestFor` / `VacancyRules` がカバー** |
| 研究ツリーのマイクロ管理 | **タイクン化回避**。KUHNはパラダイム体制という高位決断のみ |
| 個別技術ツリーの新設 | **#123-127 `ResearchRules` がカバー**。KUHNは体制レイヤーの重ね合わせのみ |

---

## 3. EPIC #KUHN の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→盤面/UIへ配線。既存研究・ドクトリン・世代交代ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #1918**。GitHub issue 起票済み（#1921〜#1929）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **KUHN-1** | #1921 | 研究パラダイムモデル（`ResearchParadigm`/`ParadigmRules`：通常科学効率ボーナス・異常蓄積・危機閾値・転換コスト） | 新純ロジック。`ResearchRules` 線形産出の非破壊拡張。`FactionState.IdeologyBias` 並走 |
| **KUHN-2** | #1922 | パラダイムシフトと技術断絶（危機→`EventEngine`発火→突破口/陳腐化フィールドマップ `ResolveShift`） | `EventEngine`×`ResearchRules` への危機発火・効果接続 |
| **KUHN-3** | #1923 | 軍事ドクトリンのパラダイム（`DoctrinalParadigm`/`DoctrineParadigmRules`：支配ドクトリン内ボーナス・異端キャリア・革命的ドクトリン遭遇ペナルティ） | `AdmiralData`×`CombatModifiers`(#106)×`AdmiralSkillRules` |
| **KUHN-4** | #1925 | 不可通約性＝世代交代と新パラダイム受容（`IncommensurabilityRules`：旧保持者の採用抵抗・退役/死による拡散速度） | `SeniorityRules`×`LifecycleRules`×`RetirementRules`×`CareerPipelineRules` |
| **KUHN-5** | #1927 | 科学者共同体の合意形成（`ParadigmCommunityRules`：証拠強度×開放性→受容率・思想的硬直→転換遅延） | `FactionState`×`PersonRules`×`SeniorityRules.PoliticalRigidity` |
| **KUHN-6** | #1929 | （lore）世界観の開示データ（科学に絶対真理なし・銀河史=パラダイムシフトの連鎖・超古代技術=不可通約の遺物） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`KUHN-1`（コアモデル確立）→ `KUHN-2`（シフトイベント接続）→ `KUHN-3`（軍事ドクトリン＝最もゲームに効く）→ `KUHN-4`（不可通約性＝世代交代加速）→ `KUHN-5`（コミュニティ合意）→ `KUHN-6`（lore）。

> いずれも既存研究・ドクトリン・世代交代ロジックを**後退させず接続**する additive 設計。`ResearchRules`（線形産出）に**体制レベルの非線形ダイナミクス**を重ねる層として機能する。
