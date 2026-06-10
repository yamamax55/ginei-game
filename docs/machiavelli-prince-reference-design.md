# マキャヴェリ『君主論』参考設計（EPIC #MKV）

> 参照元：ニッコロ・マキャヴェリ『君主論』（1513年成立）。新君主が権力を獲得し保持するための技術論。
> 理想論を排し、**政治の「有るがままの現実」**を記述した政治リアリズムの古典。
> 本ドキュメントは、当プロジェクト（Ginei＝社会・政治シミュ層を持つ星間国家戦略）にとって
> **役に立つ視点だけ**を抽出し、EPIC `#MKV` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**政治メカニクス／世界観の構造パターンのみ**を参考にする。

---

## 0. なぜ「君主論」が本システムに役立つか

当プロジェクトは統治・支持・強制力に関する**純ロジックを大量に保有**している：

| 既存（カバー範囲） | 対応モジュール |
|---|---|
| 省支持・安定度・占領統合 | `GovernanceRules`/`Province` |
| 被支配者の協力と撤退（非協力） | `ConsentRules`/`Polity` |
| 抑圧・秘密警察・不満鎮圧 | `SecurityRules`/`SecurityApparatus` |
| クーデター確率・軍政関係 | `CoupRules`/`CivilianControlRules` |
| 忠誠・調略・寝返りカスケード | `LoyaltyRules`/`BattleAllegianceRules` |
| 王朝腐敗・易姓革命 | `DynastyRules`/`Regime` |
| 参謀能力補正（戦闘） | `AdmiralData.staffOfficers`/`CommandStaffRules` |
| 軍事戦略提案（献策） | `CounselRules`（SGZ-2 で設計済み） |

**しかし、これらは「制度・均衡・集計値」の抽象モデル**であり、君主論が固有に描く以下が**欠けている**：

| 君主論が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **新領土の保持戦略（三様）** | `GovernanceRules.OnOccupied` は占領後の統合を均一に扱う。**「旧秩序を駆逐する/植民者を入れる/傀儡政権を置く」という戦略的選択肢と各々のコスト・リスク**が無い |
| **恐怖と憎悪の回廊** | `SecurityRules.RepressionSupportPenalty` は抑圧→支持低下を線形に扱う。**「賢明な強制力は恐れられても憎まれない」対「場当たり的抑圧は憎悪を生む」という非線形の区間モデル**が無い |
| **佞臣問題（情報品質の劣化）** | `AdmiralData.staffOfficers` は戦闘能力補正。`CounselRules`（SGZ-2）は軍事提案。**「おべっか使い参謀が君主に偽情報を届ける → 政策が現実から乖離する」という情報品質軸**が無い |
| **ヴィルトゥーとフォルトゥーナ** | `PersonRules.Effectiveness` は適性×役割一致。**「逆境に立って状況に応じて手法を変える適応力（ヴィルトゥー）」と「外的偶然（フォルトゥーナ）が統治結果を歪める確率的圧力」**の交差モデルが無い |

**結論**：君主論は当プロジェクトの統治ロジックに**「現実政治の技術論」**という視角から、
①**征服地保持の戦略選択肢**、②**恐怖-憎悪の非線形回廊**、③**佞臣による情報劣化**、
④**適応力×偶然の統治修正子**という4つの欠落軸を与える。
`GovernanceRules`（占領）・`SecurityRules`（抑圧）・`LoyaltyRules`（忠誠）への**additive な接続**。

---

## 1. 役に立つ視点（要約）

君主論の世界観を、**本システムに効く形**で1行ずつ：

1. **新領土は旧来のやり方で統治できない** — 既存秩序を壊さず支配しようとする君主は必ず反乱に遭う。→ 征服地に対して明示的な「統治方針の選択」が必要（`GovernanceRules` の拡張）。
2. **恐れられることと憎まれることは別物** — 一貫した強制力は秩序を生み、場当たりの残酷さは憎悪を生む。→ 抑圧の「質」と「量」を分離するモデルが必要（`SecurityRules` に区間判定を追加）。
3. **君主は虚偽の情報に最も傷つく** — 佞臣に囲まれた君主は現実を知らないまま誤った決断を下す。→ 参謀の「直言率」が政策効率に影響する純ロジック（新 `CounselIntegrityRules`）。
4. **ヴィルトゥーはフォルトゥーナの半分を制する** — 適切な手法を状況に合わせて変える能力が、外的偶然を「運命の川のダム」に変える。→ `PersonRules.Effectiveness` に適応力修正子を追加。
5. **民衆の憎悪を避けることが最大の防衛** — 民衆が味方なら外敵も内敵も排除できる。→ `ConsentRules.Polity.suppression` × `CoupRules.CoupRisk` の連接強化。
6. **武装した改革者だけが変革を達成する** — 説得だけに頼る変革者は、潮目が変わると見捨てられる。→ `DynastyRules.Reform` に「武力の有無」を成功確率修正子として接続（既存拡張のみ）。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`GovernanceRules`/`SecurityRules`/`ConsentRules`/`LoyaltyRules` を作り直さない**。MKV はそれらに**欠落軸を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・君主論の signature）

#### MKV 征服地統治の三様（新領土保持戦略）
- **三択モデル**：新たに占領した領土に対して支配者は3つの方針を選べる：
  - **旧秩序駆逐**（旧指導層を排除・高コスト高速統合）：支持低下リスク大だが残存敵対勢力が消える
  - **植民**（自勢力の入植者を送り込み同化を促進）：コスト中・統合速度中・人口生産性に影響
  - **傀儡/間接統治**（旧支配者層を温存し代理統治）：コスト低・統合遅・裏切りリスク高
- 接続：新 `ConquestDispositionRules`（static・純ロジック）。`GovernanceRules.OnOccupied`→`Province` の初期化パラメータを3方針で差別化。`LoyaltyRules.BaselineLoyalty` へ間接統治の裏切りリスクを波及。

#### MKV 恐怖と憎悪の回廊（賢明な強制力の非線形モデル）
- **区間モデル**：抑圧強度 × 実施様式 → 結果が非線形：
  - **低抑圧**：なめられ謀反が増える（`CoupRules.CoupRisk` ↑）
  - **賢明な強制**（一時的・集中的・目的明確）：恐れられるが憎まれない。`SecurityRules.DissentSuppression` ↑ + 支持低下なし
  - **残虐な抑圧**（慢性・無差別）：憎悪を生む。`ConsentRules.Polity.cooperation` 急落 + `CoupRules.WouldCoup` 上昇
- 接続：新 `CoerciveStyleRules`（static・純ロジック）。`SecurityRules`/`ConsentRules`/`CoupRules` への係数修正子として挿入。EditMode テスト必須。

### ★★ 高（既存ロジックへの重要な欠落を補う）

#### MKV 直言参謀と佞臣（情報品質の劣化）
- **情報品質軸**：参謀の「直言率（integrityFactor）」が高いほど政策が現実から乖離しない。直言率は：
  - 君主の権威強度（支持が高いと直言しやすい）
  - 参謀の個人能力（`PersonRules.Aptitude`）
  - 政体の透明性（民主 > 軍閥 > 専制）で変化
- **効果**：低い直言率 → `EventEngine` でのネガティブイベント頻度が上がる（隠蔽された問題が後で爆発）。
- 接続：新 `CounselIntegrityRules`（static・純ロジック）。`GovernmentRegistry`/`OfficeRules` × `PersonRules` × `EventEngine`。
  SGZ-2 `CounselRules`（軍事戦略提案）とは別系統＝政治情報品質の軸。EditMode テスト必須。

#### MKV ヴィルトゥー・フォルトゥーナ（適応力 × 外的偶然）
- **適応力修正子**：`Person.adaptability`（0〜1）。高い人物は状況に合わせて統治方針を変えられる：
  - 高 adaptability：外的イベント圧力（フォルトゥーナ）を「半分」軽減できる（`FactionStateRules.Tick` への係数）
  - 低 adaptability：固定した手法に固執し、状況変化で失敗率が上がる
- 接続：`PersonRules.Effectiveness` に adaptability 項を追加。`EventEngine.IsEligible` でのイベント耐性修正子。基準値非破壊（実効値パターン）。

### ★ 中（世界観lore・既存の接続強化）

#### MKV（lore）世界観の開示データ
- 「現実の政治は道徳的である必要はない」「民衆の憎悪が最大の危機」「ヴィルトゥーの美学」。
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への**lore データ入力**。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 武装した改革者（新モジュール化） | `DynastyRules.Reform` に武力有無の修正子を追加するだけ（新 EPIC 不要）|
| 民衆 vs 貴族の対立軸そのもの | `RedistributionRules`/`TaxStructure`（#163）が階級対立をカバー済み |
| 軍事技術論・傭兵の問題 | `StrategyRules`/`FleetPool`/`ShipyardRules` が傭兵的問題を部分カバー。CLZ-1 が摩擦でカバー |
| 運命論のエンジン化（ランダム性大幅追加） | タイクン化回避・マジックナンバー禁止 |
| 外交の詳細戦術（欺瞞外交・偽条約） | `DiplomacyRules`/`TreatyRules`（DIP-2）と SUN-1 `DeceptionRules` でカバー |

---

## 3. EPIC #MKV の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UI へ配線。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #1138**。GitHub issue 起票済み（#1139〜#1143）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **MKV-1** | #1139 | 征服地統治の三様（旧秩序駆逐/植民/傀儡の3戦略選択＋統合速度・裏切りリスクのトレードオフ） | 新 `ConquestDispositionRules`。`GovernanceRules.OnOccupied`→初期 Province パラメータを3方針で差別化。`LoyaltyRules` 接続 |
| **MKV-2** | #1140 | 恐怖と憎悪の回廊（賢明な強制力 vs 残虐な抑圧の非線形モデル） | 新 `CoerciveStyleRules`。`SecurityRules`/`ConsentRules`/`CoupRules` への係数修正子 |
| **MKV-3** | #1141 | 直言参謀と佞臣（政治情報品質 → 政策の現実乖離度） | 新 `CounselIntegrityRules`。`GovernmentRegistry`×`PersonRules`×`EventEngine`。SGZ-2 `CounselRules` と別系統 |
| **MKV-4** | #1142 | ヴィルトゥー・フォルトゥーナ（統治者の適応力 × 外的偶然の統治修正子） | `PersonRules.Effectiveness` に adaptability 項追加。`EventEngine` 耐性修正子 |
| **MKV-5** | #1143 | （lore）世界観の開示データ（現実主義の秘史・恐怖の論理・状況倫理） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`MKV-1`（征服地統治の三様＝最も欠落が大きく `GovernanceRules` と直接接続）→
`MKV-2`（恐怖と憎悪の回廊＝`SecurityRules`/`CoupRules` の非線形化）→
`MKV-3`（直言参謀＝情報品質の新軸・`EventEngine` 接続）→
`MKV-4`（ヴィルトゥー＝`PersonRules` の適応力拡張）→
`MKV-5`（lore）。

> いずれも既存の統治・支持・強制力ロジックを**後退させず接続**する additive 設計。
> 「新領土の統治」という会戦直後の戦略フェーズに最も効く。
