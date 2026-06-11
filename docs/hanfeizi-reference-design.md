# 『韓非子』参考設計（EPIC #HFZ）

> 参照元：韓非（紀元前3世紀）著『韓非子』。法家思想の集大成。
> 君主が国家を安定統治するための**「法（fa）・術（shu）・勢（shi）」の三位一体**を論じた政治技術論。
> 本ドキュメントは、当プロジェクト（Ginei＝社会・政治シミュ層を持つ星間国家戦略）にとって
> **役に立つ視点だけ**を抽出し、EPIC `#HFZ` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**政治メカニクス／世界観の構造パターンのみ**を参考にする。

---

## 0. なぜ「韓非子」が本システムに役立つか

当プロジェクトは統治・支持・組織継続に関する**純ロジックを大量に保有**している：

| 既存（カバー範囲） | 対応モジュール |
|---|---|
| 占領統合・安定度・産出 | `GovernanceRules`/`Province` |
| 被支配者の協力と撤退 | `ConsentRules`/`Polity` |
| 抑圧・秘密警察・鎮圧 | `SecurityRules`/`SecurityApparatus` |
| クーデター確率・軍政関係 | `CoupRules`/`CivilianControlRules` |
| 忠誠・調略・寝返りカスケード | `LoyaltyRules`/`BattleAllegianceRules` |
| 王朝腐敗・易姓革命 | `DynastyRules`/`Regime` |
| カリスマの日常化（英雄死後の組織存続） | `OrganizationRules`/`SuccessionRules` |
| 政治情報品質（佞臣問題の供給側） | `CounselIntegrityRules`（MKV-3） |
| 納諫ループ（君主の受容性） | `RemonstranceRules`（JGS-1） |
| 軍功授爵（軍事域の信賞必罰） | `MeritRankRules`（QIN #900-905） |
| 政府役職・任免・資格制限 | `OfficeRules`/`GovernmentRegistry` |
| 臣下の省益・横断摩擦 | `MinistryRules`/`SectionalismFriction` |

**しかし、これらは「個別の制度・均衡・均一な抑圧」の抽象モデル**であり、韓非子が固有に描く以下が**欠けている**：

| 韓非子が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **法の公開性と一貫適用（予測可能性）** | `GovernanceRules.stability` は結果値。**「法が誰にでも同じく適用されるか」という一貫性スコア**——恣意的適用は省益・縁故に乗られ安定を蝕む——がない |
| **参験（大臣の言行照合）** | `CounselIntegrityRules`（MKV-3）は助言の正直さ（供給側）。**君主が臣下に課題を与え、言ったことと実際の結果を突き合わせる主体的な成果検証**（需要側の検証行動）がない |
| **勢（位置の権威）** | `OrganizationRules` は英雄死後の組織存続。**現役の凡庸な君主が、徳に依らず「ポジションの権威」で統治できる——徳のある人物でなくても地位が法を動かす**という制度的レバレッジがない |
| **二柄の均衡（賞と罰の両輪）** | `MeritRankRules`（QIN）は軍功報奨のみ。`SecurityRules` は抑圧のみ。**賞のみ→阿諛追従で国庫枯渇、罰のみ→怨恨で反乱**という均衡ダイナミクスが統治全域でない |
| **大臣の権力集積監視（専横防止）** | `CoupRules` はクーデターの確率と結果。`FeudalRules` は領地諸侯の反乱。**宮廷官僚が徐々に私的追随者・情報独占・財を積み上げ専横に至る過程**の早期警戒がない |

**結論**：韓非子は当プロジェクトの統治ロジックに**「法治主義的統治技術論」**という視角から、
①**法の一貫性（予測可能性が安定を生む）**、②**参験（主体的な成果照合）**、
③**勢（位置の制度的権威）**、④**二柄（賞罰の均衡ダイナミクス）**という4つの真の欠落軸を与える。
加えて⑤**大臣専横の早期警戒**を `CoupRules` への前段として補完する。
`GovernanceRules`・`ConsentRules`・`CoupRules`・`DynastyRules` への**additive な接続**。

> 参考：`MeritRankRules`（QIN #900-905）は韓非子理論を**軍事域だけで先行実装**したもの。
> HFZ はその思想原典として、統治全域（軍事外）へ拡張する位置づけ。

---

## 1. 役に立つ視点（要約）

韓非子の世界観を、**本システムに効く形**で1行ずつ：

1. **法は明示・公開・一貫していなければ意味がない** — 法がある名目と実際の適用が乖離すると、官僚は「誰に何を言えばルールを曲げられるか」を学ぶ。→ `GovernanceRules` に法的一貫性修正子を追加。
2. **君主は言葉でなく結果で評価せよ（参験）** — 「私はできる」と言う大臣に仕事を割り振り、後から結果を照合する。乖離が大きければ罰せよ。→ 成果照合の純ロジック `VerificationRules`。
3. **徳のある君主に頼るな、ポジションに頼れ（勢）** — 500年に一度しか生まれない聖王に賭けるシステムは脆弱。誰がその席に座っても法が動く仕組みこそ堅牢。→ 制度的レバレッジが `CoupRules` 耐性を与える。
4. **賞と罰の両方を握れ（二柄）** — 賞だけ与えれば大臣に賞を配る権限を奪われる。罰だけ与えれば恨みを買う。両方を直接握ることで臣下をコントロール。→ 賞罰均衡の均衡ダイナミクス。
5. **大臣が私党・私富・私兵を積む前に動け** — 水が堤を越えてから止めようとしても遅い。蓄積中に抑えよ。→ `CoupRules` への前段としての専横監視。
6. **法家は儒家を批判したが、ともに「安定した統治の形」を求めた** — 韓非子の答えは「制度の自動性」、儒家の答えは「名君の徳」。本プロジェクトは両方を実装できる（`DynastyRules` の天命論と `LegalConsistencyRules` の制度論を共存させる）。→ 思想対立軸 #617〜623・開示エンジンへ。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`GovernanceRules`/`ConsentRules`/`CoupRules`/`DynastyRules`/`MeritRankRules` を作り直さない**。HFZ はそれらに**欠落軸を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・韓非子の signature）

#### HFZ 法の公開性と一貫適用（`LegalRegime` + `LegalConsistencyRules`）
- **法的レジームモデル**：`LegalRegime(factionId, consistency:0..1, publicness:0..1)` — 純データ。
  - `consistency`：実際の適用が例外・縁故・恣意なく均一かの度合い。0=無法地帯、1=完全一貫。
  - `publicness`：法がすべての臣民に公開・周知されている度合い。
- **`LegalConsistencyRules`**（static・純ロジック）：
  - `StabilityModifier(regime)`：一貫性↑ → `GovernanceRules.stability` ボーナス（法が予測可能→安心して農業・生産）
  - `CooperationModifier(regime)`：一貫性↑ → `ConsentRules.Polity.cooperation` ↑（恣意的支配より協力しやすい）
  - `CorruptionAcceleration(regime)`：一貫性↓ → `DynastyRules.Regime.corruption` 加速（例外が慣例化→腐敗スパイラル）
  - `FavorExemptionPenalty(exemptionCount)`：特赦・縁故例外を与えるたびに `consistency` を削る
- 接続：`GovernanceRules.Tick` が `LegalConsistencyRules.StabilityModifier` を係数として参照。`DynastyRules.Tick` が `CorruptionAcceleration` を参照。EditMode テスト必須。

#### HFZ 参験（`VerificationRules`）— 大臣の言行照合
- **参験モデル**（言行一致の検証）：
  - `VerificationTask(officeId, personId, expectedOutcome:string, targetPeriod:float)` — 君主が臣下に課す課題と期待成果。
  - `VerifyOutcome(task, actualScore:0..1)` → `VerificationResult{gapScore, mismatch:bool}` — 実際の成果と期待値の乖離を返す。
  - `OutcomePenaltyTrigger(result)` → `EventEngine` イベント発火（無能発覚/詐称発覚/忠実な臣発見）。
- **MKV-3 `CounselIntegrityRules`（佞臣供給側）との差異**：
  - MKV-3：「助言者は正直に話しているか？」（情報品質の供給側・政体や権威が決める）
  - HFZ 参験：「課された仕事の結果は宣言通りか？」（成果の照合・**君主が主体的に検証する**）
  → 供給側と需要側の両方が揃って初めてエージェント問題が完全にモデル化される。
- 接続：`GovernmentRegistry`（課題割当対象）×`PersonRules.Effectiveness`（期待値の計算）×`EventEngine`（結果イベント）。EditMode テスト必須。

### ★★ 高（マクロ統治に均衡の厚みを足す）

#### HFZ 二柄（`TwoHandlesRules`）— 賞罰の均衡
- **二柄モデル**：賞（rewardFrequency）と罰（punitiveFrequency）の相対バランスを0..1で表す。
  - `TwoHandlesBalance(rewardF, punitiveF)` → `BalanceScore`（中央1.0が最適・両端が逸脱）
  - **罰過多**（罰のみ）→ `ConsentRules.Polity.cooperation` ↓・`CoupRules.CoupRisk` ↑（怨恨型）・`HopeRules.Community.hope` ↓
  - **賞過多**（賞のみ）→ 官僚は賞を目的とした阿諛追従で行動→`CounselIntegrityRules.integrityFactor` ↓・`FiscalRules.PrimaryBalance` 悪化（財政圧力）
  - **均衡**（両方を直接握る）→ 官僚が実績ベースで行動 → `LegalConsistencyRules.consistency` ↑
- 接続：`MeritRankRules`（QIN-2 軍事報奨を軍事外ドメインへ拡張するハブとして機能）×`SecurityRules.DissentSuppression`×`ConsentRules`×`CoupRules`。EditMode テスト必須。

#### HFZ 勢（`PositionalAuthorityRules`）— 位置の権威
- **制度的レバレッジ**：`InstitutionalLeverage(faction)` = 制度強度スコア（0..1）。
  - 高レバレッジ：凡庸な君主でも地位が法を動かす→聖王依存が低い→後継者問題に強い
  - 低レバレッジ：有徳な名君がいる間だけ機能→英雄死去で即崩壊（`OrganizationRules.personalCharisma` と連動）
  - `RulerInsulationFactor(leverage, rulerAbility)` → `CoupRules.CoupRisk` の修正子（高レバレッジなら能力低い君主でもクーデター耐性あり）
  - `LeverageBuildCost(faction)` → 制度構築は時間コストがかかる（`CalendarTick` 経由で徐々に上昇）
- **OrganizationRules（カリスマの日常化）との相補関係**：
  - OrganizationRules：「カリスマ没後に制度が組織を引き継げるか？」
  - PositionalAuthorityRules：「現役の凡庸な君主が徳なしで統治できるか？」
  → 前者は時間軸（継承後）、後者は当代（継承中）を扱い直交する。
- 接続：`CoupRules.WouldCoup`（修正子）×`OrganizationRules.institutionalization`（連動）×`OfficeRules`（役職の制度強度に寄与）。EditMode テスト必須。

### ★ 中（`CoupRules` の前段補完）

#### HFZ 大臣の権力集積監視（`MinisterialConsolidationRules`）
- **集積スコアモデル**：`ConsolidationScore(personId)` = 私的追随者数 + 情報独占度 + 財蓄積 + 職務外の権限流入。
  - 閾値超過 → `CoupRules.CoupRisk` を非線形に急増（線形積み上げが突然臨界を越える）。
  - `PreemptivePurgeCost(person, score)` → 早期排除のコスト（`GovernanceRules.stability` 一時低下 + `LoyaltyRules.BaselineLoyalty` ペナルティ）。
- **`FeudalRules.VassalRebellionRisk`との差異**：
  - FeudalRules：領地諸侯（地域に根ざした土地権力）。
  - MinisterialConsolidationRules：宮廷官僚（役職に根ざした行政権力）。→ 別系統。
- 接続：`GovernmentRegistry`（監視対象の全臣）×`CoupRules.WouldCoup`（閾値超過で急増）×`LoyaltyRules`（排除コスト）×`MinistryRules.institutionalInterest`（省益が集積のベースになる）。EditMode テスト必須。

#### HFZ（lore）世界観の開示データ
- 「法は徳より堅牢である」「聖王を待つな、制度を作れ」「言葉より結果で評価せよ」。
- 儒家の天命論 vs. 法家の制度論という**思想対立軸の一極**として開示エンジンへ入力。
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への**lore データ入力**。思想対立軸 #617〜623 への接続。

### ❌ 不採用（重複・既存で十分・後退防止）

| 不採用 | 理由 |
|---|---|
| 五蠹（農民・兵士のみ有益という役割価値観） | `PersonRules.Aptitude`/`CareerPipelineRules.TrackRole` が役割適性・役職一致をカバー済み。タイクン化回避のため過剰な役割制裁モデルは新設しない |
| 刑名之学の全体実装（言葉と現実の一致理論） | 参験（HFZ-2）が実用的コアを抽出済み。全体実装は重複かつオーバースペック |
| 反儒教アーキテクチャの組み込み | `DynastyRules`（天命＝儒家論理）は後退させない。法家↔儒家の対立は開示loreで扱う（HFZ-5） |
| 縦横家批判・説客の弊害の新システム | MKV-3 `CounselIntegrityRules`（情報品質）・JGS-1 `RemonstranceRules`（納諫ループ）がカバー済み |
| 諸侯間外交の技術論 | `DiplomacyRules`（DIP-1〜3 #189〜#192）でカバー済み |
| 耕戦主義の動員システム | `FleetPool`/`ShipyardRules`/`ResourceProductionRules` でカバー済み。QIN-2 `IncentiveMoraleBonus` と重複 |
| 法の整備 UI/編集画面 | タイクン化回避（高位の決断→エンジン駆動）。UIは `FactionStateRules.Tick` 経由で数値が動くのみ |

---

## 3. EPIC #HFZ の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UI へ配線。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #1331**。GitHub issue 起票済み（#1332〜#1337）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **HFZ-1** | #1332 | 法の公開性と一貫適用（`LegalRegime`・一貫性スコア→安定/腐敗加速/協力係数） | 新 `LegalConsistencyRules`+`LegalRegime`。`GovernanceRules.Tick`/`DynastyRules.Tick`/`ConsentRules.Polity` への係数接続 |
| **HFZ-2** | #1333 | 参験（`VerificationRules`・大臣の言行照合→成果乖離→イベント） | 新 `VerificationRules`。`GovernmentRegistry`×`PersonRules.Effectiveness`×`EventEngine`。MKV-3 `CounselIntegrityRules` と相補（供給側vs需要側） |
| **HFZ-3** | #1334 | 二柄の均衡（`TwoHandlesRules`・賞罰バランス→怨恨型クーデター/阿諛追従/整合行動） | 新 `TwoHandlesRules`。`MeritRankRules`（QIN）拡張×`SecurityRules`×`ConsentRules`×`CoupRules` への係数修正子 |
| **HFZ-4** | #1335 | 勢・位置の権威（`PositionalAuthorityRules`・制度的レバレッジ→凡庸な君主のクーデター耐性） | 新 `PositionalAuthorityRules`。`CoupRules.WouldCoup`（修正子）×`OrganizationRules.institutionalization`（相補）×`OfficeRules` |
| **HFZ-5** | #1336 | 大臣の権力集積監視（`MinisterialConsolidationRules`・集積スコア→クーデターリスク急増） | 新 `MinisterialConsolidationRules`。`GovernmentRegistry`×`CoupRules`×`LoyaltyRules`×`MinistryRules.institutionalInterest`。FeudalRules（領地）と別系統 |
| **HFZ-6** | #1337 | （lore）世界観の開示データ（法vs徳・制度の自動性・思想対立軸への接続） | `DisclosureLedger`（FND-4）。コード新設なし。思想対立軸 #617〜623 接続 |

### 推奨着手順
`HFZ-1`（法の一貫性＝最も根幹・`GovernanceRules`/`DynastyRules` と直接接続）→
`HFZ-2`（参験＝成果照合・`EventEngine` でゲームプレイに現れる）→
`HFZ-3`（二柄＝賞罰均衡・既存QINの軍事報奨と接続して全域化）→
`HFZ-4`（勢＝制度的レバレッジ・`CoupRules`耐性として機能）→
`HFZ-5`（大臣専横監視＝`CoupRules`の前段補完）→
`HFZ-6`（lore）。

> いずれも既存の統治・支持・クーデター・腐敗ロジックを**後退させず接続**する additive 設計。
> QIN（始皇帝モデル）の思想原典として軍事域を統治全域へ拡張する位置づけ。
