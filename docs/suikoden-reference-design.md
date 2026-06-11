# 『水滸伝』参考設計（EPIC #SHZ）

> 参照元：施耐庵『水滸伝』（中国古典四大奇書のひとつ・14〜15世紀成立）。腐敗した官吏に追われた英雄たちが梁山泊に集い、108人の義賊集団を形成・膨張させ、最終的に国家の「招安（帰順）」を受け入れて解体していく物語。
> 本ドキュメントは、当プロジェクト（Ginei＝多勢力星間戦略＋社会・政治シミュ層）にとって**役に立つ視点だけ**を抽出し、EPIC `#SHZ` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用しない。**組織論・政治メカニクス／世界観の構造パターンのみ**を参考にする。

---

## 0. なぜ『水滸伝』が本システムに役立つか

当プロジェクトは国家・統治・忠誠に関する**純ロジック層を大量に保有**している：

| 既存（カバー範囲） | 担当モジュール |
|---|---|
| 占領統合・安定度・産出 | `GovernanceRules`/`Province` |
| 被支配者の合意と非協力 | `ConsentRules`/`Polity`（#836） |
| 旗幟・寝返りカスケード | `LoyaltyRules`/`BattleAllegianceRules`（#817） |
| 英雄死後の組織存続 | `SuccessionRules`/`Organization`（#812） |
| 天命喪失・王朝交代 | `DynastyRules`/`Regime`（#867） |
| 捕虜化・処断・登用 | `CaptivityRules`（#154） |
| 政府役職・任免・資格制限 | `OfficeRules`/`GovernmentRegistry`（GOV-1/3） |
| 文民統制・クーデター | `CivilianControlRules`（GOV-4 #145） |
| 政党・選挙・指導者選出 | `PartyRules`/`LeadershipElectionRules`（GOV-6/7） |
| 席次主義 vs 実力主義 | `SeniorityRules`（LIFE-5/6） |
| 希望と末人（非協力の発火） | `HopeRules`/`Community`（#852） |
| 非暴力・道徳の柔術 | `NonviolenceRules`/`Movement`（#831） |
| 多極均衡・同盟圧力 | `BalanceOfPowerRules`（SGZ） |

**しかしこれらはすべて「国家 vs 国家」または「国家 vs 人口」の枠組み**であり、水滸伝が固有に描く以下が**欠けている**：

| 水滸伝が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **非国家武装勢力（反乱組織）の組織動態** | 既存 `FactionData` は国家的勢力を想定。腐敗した国家に追われた個人が集まり武装集団を自律的に形成・維持・解体する**アウトロー勢力のライフサイクルモデル**が無い |
| **対抗的正統性＝腐敗が反乱支持を育てる** | `GovernanceRules.RebelPressure` は反乱リスク確率のみ。**「官の腐敗が高いほど反乱勢力の民衆支持が能動的に成長し、義賊行動（分配・庇護）が正統性を蓄積する」能動的な逆転経路**が無い |
| **招安ジレンマ（co-option 受諾vs拒絶）** | `DiplomacyRules` は国家間条約。**国家が反乱組織に「合法性＋軍事力」を与える代わりに自律性を取り上げる「招安（co-option）」——受諾すると組織の目的が空洞化し自壊する——という特殊な交渉力学**が無い |
| **境遇共有リクルート（状況誘導型入盟）** | `CaptivityRules` は戦場での捕虜→勧誘。**「追放・冤罪・失職という逃げ場なしの状況を作り出して対象者を追い込み入盟させる」平時の非戦闘的リクルート**が無い（水滸伝で最も多用される英雄獲得パターン） |
| **義侠衆議・功績型座次（非国家組織の序列決定）** | `SeniorityRules` は官僚ハンモックナンバー/科挙順。`LeadershipElectionRules` は政党選出。**「入盟時の功績・実力評価→座次固定、重大決断は上位の衆議で」という反乱集団固有の半民主的序列メカニズム**が無い |

**結論**：水滸伝は当プロジェクトに**「国家の裂け目から生まれる反乱組織」**という視角から、①非国家武装勢力の組織モデル、②対抗的正統性、③招安ジレンマ、④境遇共有リクルート、⑤義侠衆議という**5つの欠落軸**を additive に足せる。三国志演義（SGZ）が「国家間の多極動態」を補い、平家物語（HEIK）が「国家崩壊の時間軸」を補ったとすれば、水滸伝は**「国家の外縁で自生する対抗勢力」**というその補集合を埋める。

---

## 1. 役に立つ視点（要約）

水滸伝の世界観を、**本システムに効く形**で1行ずつ：

1. **腐敗した国家が反乱組織を産む**——官の腐敗・弾圧は国家の安定度を下げるだけでなく、「義賊」の正統性を能動的に育てる。→ `DynastyRules.Corruption` × `GovernanceRules.RebelPressure` の**正統性生成の反転経路**。
2. **反乱組織は「国家ではない」ことで独自の凝集力を持つ**——共通の危機・追放体験が仲間を結びつける。正規の役職・法より「義（仲間への義理）」が組織を束ねる。→ 既存 `Organization.cohesion` に**アウトロー固有の結束源泉**を追加。
3. **「招安」は見えない解体装置**——国家に吸収された瞬間、組織が存在した理由（腐敗への抵抗）が消え、義士は官僚化し、凝集力が溶ける。→ **co-option のゲーム理論**的帰結。`DiplomacyRules` に招安交渉モードを追加。
4. **梁山泊型リクルートは「追い込み」から始まる**——対象者を冤罪・裏切り・失職で追い詰め「ここしか居場所がない」状態にして仲間にする。`CaptivityRules` の戦闘版と並ぶ**平時の非戦闘的リクルート**。
5. **功績が座次を決める半民主体制**——貴族的な家格・官歴ではなく、加盟時の功績実績で入盟席次が決まり、大方針は席次上位の衆議で決める。→ `SeniorityRules`/`LeadershipElectionRules` の**非国家版**。
6. **「英雄の無駄死に」が物語の核心**——招安を受け入れた英雄たちは国家のために消耗して次々と死ぬ。制度に飲み込まれた個人英雄のカタストロフ。→ 開示エンジン（FND-4）の lore。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`GovernanceRules`/`ConsentRules`/`LoyaltyRules`/`Organization`/`CaptivityRules`/`DiplomacyRules`/`SeniorityRules` を作り直さない**。SHZ はそれらに**欠落軸を足し、接続するだけ**（additive）。

### ★★★ 最優先（真の欠落・水滸伝の signature）

#### SHZ-1 非国家武装勢力の組織モデル（`RebelOrg`/`OutlawOrganizationRules`）
- **`RebelOrg`**（純データ・`[Serializable]`）：`cohesion`（義による結束0..1）／`counterLegitimacy`（対抗的正統性0..1）／`infiltration`（国家の浸透・スパイ度）／`cooptionPressure`（招安圧力）／`size`（傘下兵力）。
- **`OutlawOrganizationRules`**（static・純ロジック）：`FormationThreshold`（コア人物の「逃げ場なし」係数が閾値を超えると非国家勢力として成立）／`CohesionSource`（状況誘導入盟・義侠行動・共同危機がそれぞれ cohesion に加算）／`DissolutionRisk`（招安受諾・最上位者の死・目的達成で凝集力が崩壊する確率）／`Tick`（時間進行で弾圧圧力が上昇するにつれ counterLegitimacy が育つ）。
- 接続：`FactionData` の **legacyFaction 非依存の追加勢力型**として扱う（コード変更不要）。`GovernanceRules.IsUnrest` の先にこのモデルを配置し、反乱組織が自律的に成立する条件を計算。

#### SHZ-2 対抗的正統性（`CounterLegitimacyRules`・腐敗が反乱支持を育てる）
- **腐敗→正統性転換**：支配勢力の `Regime.corruption`（腐敗）× `SecurityRules.DissentSuppression`（弾圧）が高いほど、反乱組織の `counterLegitimacy` が増大する転換係数 `OppresionToLegitimacy`。
- **義賊行動ボーナス**：義賊が分配（物資の民衆配布）・庇護（逃亡者の保護）を行うと `counterLegitimacy` が上昇する `ActOfJusticeBonus`。これが敵国の `ConsentRules.Polity` に `Withdraw` 圧力を与える（腐敗した国の民が支持を転換する）。
- **非線形蓄積**：支配勢力の正統性が高い間は転換が遅く、`MandateLost` 寸前に急加速する S 字型。
- 接続：`DynastyRules.Regime`（腐敗）× `ConsentRules.Polity`（民衆合意）× `GovernanceRules.RebelPressure`。`CampaignRules.Tick` から一緒に回す。

### ★★ 高（現システムに固有の交渉・リクルート動学を足す）

#### SHZ-3 招安ジレンマ（`CooptionRules`・co-option 受諾閾値と吸収後崩壊）
- **招安提案モデル**：`CooptionOffer(terms:{legitimacy, rank, autonomy}, offerorStrength, defenderStrength)`——国家が「正規軍化・指揮権維持・恩赦」を条件に反乱組織を取り込もうとする。
- **受諾閾値**：`AcceptanceThreshold(org: RebelOrg)` = 凝集力（cohesion）が高く counterLegitimacy が大きいほど閾値↑（拒絶しやすい）、cooptionPressure（生存危機）が高いほど閾値↓（受諾しやすい）。
- **吸収後ドリフト**：招安受諾後 `PostCooptionDrift`——国家の腐敗が続いても自分たちが官側になったため counterLegitimacy の生成経路が消える。cohesion が `driftRate` で低下し `DissolutionRisk` が上昇していく。
- 接続：`DiplomacyRules.SignTreaty`（状態遷移の枠組み）× `Organization.Refactor`（急な組織変換のカリスマ離反コスト）× `SuccessionRules`（目的なき継続で英雄が消耗）。

#### SHZ-4 境遇共有リクルート（`CorneringRules`・逃げ場なし係数から入盟へ）
- **`CornerednessScore`**：追放・冤罪・失職・家族の被害といった不可逆的状況要因の合計スコア。高いほど「梁山泊に行くしかない」状態。
- **リクルートチャンス**：`RecruitFromAdversity(target: Person, org: RebelOrg)` = `CornerednessScore` × cohesion × ideology 適合度で確率算出。`CaptivityRules`（戦場捕虜ルート）と並ぶ**平時・非戦闘リクルート経路**。
- **入盟後の結束ボーナス**：追い込まれて入盟した者は高い凝集力貢献（「自分が選んだのではなく追い込まれた」共同体験が強い義理を生む）。
- 接続：`CaptivityRules.RecruitChance`（戦闘捕虜）の非戦闘版補完。`GovernanceRules.OnOccupied`（不安定化で難民発生）が `CornerednessScore` の大量供給源になる。

### ★ 中（既存の seq 拡張・lore）

#### SHZ-5 義侠衆議（`MeritSeatingRules`・功績型座次と集団意思決定）
- **入盟時座次決定**：`MeritSeating(feats, cohesionContribution)` = 功績・実力評価 → 座次（実質 tier）を `SeniorityRules.InitialTier` の非国家版として算出。血筋・学歴でなく**実績**で序列が決まる。
- **衆議決定**：`AssemblyVote(org, decision)` = 座次上位 K 名の多数決。全員参加でなく高功績者が議決権を持つ半民主制。`LeadershipElectionRules` のアウトロー版。
- 接続：`SeniorityRules`（席次本体）+ `LeadershipElectionRules`（投票集計） のパラメータを非国家組織に適用する拡張。

#### SHZ-6 （lore）秘史開示データ（義の叛乱・招安の悲劇・英雄の無駄死に）
- 「腐敗した国家が義の反乱を産む」「招安は見えない解体装置」「英雄は制度に飲み込まれて死ぬ」——歴史の冷酷な反復。
- 接続：**コード新設なし**。`DisclosureLedger`（FND-4 #495）への lore データ入力のみ。世界観 CODEX #743 の「転換エンジン・天命サイクル」群と接続。

---

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 武装集団の軍事組織一般（梯団・陣形・提督能力） | `OrderOfBattle`/`FleetRoster`/`AdmiralData` が**既にカバー**。SHZ は組織形成ロジックのみ |
| 義兄弟型個人誓約 | `LoyaltyRules` + **SGZ（Person-to-Person の `Pledge`）**がカバー。重複新設しない |
| 多極均衡・同盟圧力 | **`BalanceOfPowerRules`（SGZ）が既にカバー** |
| 旗幟・寝返りカスケード（会戦） | **`LoyaltyRules`/`BattleAllegianceRules`（#817）が既にカバー** |
| 国家崩壊の速い上昇→脆い頂点 | **`HEIK`（平家物語）がカバー**。SHZ はその補集合（外縁からの自生）に特化 |
| 捕虜・処断・勧誘（戦場ルート） | **`CaptivityRules`（#154）がカバー**。SHZ-4 は平時リクルートのみ足す |
| 招安後の正規軍事作戦 | `StrategyRules`/`BattleManager` が通常通り処理する |

---

## 3. EPIC #SHZ の子 Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面・UI へ配線。既存モジュールは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #1354**。GitHub issue 起票済み（#1357〜#1364）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **SHZ-1** | #1357 | 非国家武装勢力の組織モデル（`RebelOrg`/`OutlawOrganizationRules`・形成閾値/結束源/解体リスク） | `GovernanceRules.IsUnrest` → 自律的なアウトロー勢力成立ロジック |
| **SHZ-2** | #1358 | 対抗的正統性（`CounterLegitimacyRules`・腐敗×弾圧→義賊支持の能動的成長） | `DynastyRules.Regime`×`ConsentRules.Polity`×`GovernanceRules.RebelPressure` |
| **SHZ-3** | #1359 | 招安ジレンマ（`CooptionRules`・co-option 受諾閾値・吸収後 cohesion ドリフト→解体） | `DiplomacyRules.SignTreaty`×`Organization.Refactor`×`SuccessionRules` |
| **SHZ-4** | #1360 | 境遇共有リクルート（`CorneringRules`・`CornerednessScore`→平時の非戦闘入盟） | `CaptivityRules`（非戦闘補完）×`GovernanceRules.OnOccupied`（難民供給） |
| **SHZ-5** | #1362 | 義侠衆議（`MeritSeatingRules`・功績型座次＋集団意思決定・`SeniorityRules` 非国家版） | `SeniorityRules`/`LeadershipElectionRules` の非国家組織への拡張 |
| **SHZ-6** | #1364 | （lore）秘史開示データ（義の叛乱・招安の悲劇・英雄の無駄死に） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順

`SHZ-1 → SHZ-2`（非国家組織の基盤＋対抗的正統性＝水滸伝の signature）→ `SHZ-3`（招安ジレンマ＝co-option の動学）→ `SHZ-4`（境遇リクルート＝入盟経路の補完）→ `SHZ-5`（義侠衆議＝組織序列の拡張）→ `SHZ-6`（lore 入力）。

> **SHZ-1/2 は test-first 必須**（`OutlawOrganizationRules`/`CounterLegitimacyRules` は純ロジック・TestHarness で担保）。SHZ-3/4 も同様。SHZ-6 はコード変更なし。
> SGZ（三国志演義）・HEIK（平家物語）と**補集合**の関係——国家の外縁・裂け目から自生する反乱組織という視角で、既存国家シミュを立体化する。
