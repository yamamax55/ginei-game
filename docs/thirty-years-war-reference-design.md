# ウェッジウッド『三十年戦争』参考設計（EPIC #1415）

> 参照元：C.V.ウェッジウッド『三十年戦争（The Thirty Years War）』（1938）。
> 1618〜1648年、宗教改革後の欧州を席巻した史上最初の「全欧大戦」——宗教・王朝・領邦が複雑に絡み合い、
> 傭兵が大陸を荒廃させながら「戦争が戦争を食って生き延びた」30年間の記録。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略＋既に豊富な軍事・政治・経済純ロジック層）にとって
> **役に立つ視点だけ**を抽出し、EPIC `#TYW` として issue 化する提案。
> **著作権注意：固有名・文章・キャラクター・固有設定は流用せず、歴史構造パターン／メカニクスのみを参考にする。**

---

## 0. なぜ「三十年戦争」が本システムに役立つか

当プロジェクトは傭兵・宗教・補給・戦争終結に関する**多くの純ロジックを既に保有**している：

| 既存（カバー範囲） | 何をカバーするか |
|---|---|
| `MercenaryRules`（MCN-2 #1381） | 傭兵雇用コスト・未払い忠誠低下・敵方提示での裏切りリスク |
| `FoederatiRules`（GIB-2 #1283） | 傭兵隊長の二重忠誠・隊長loyalty低下で部隊丸ごと離反・傭兵依存→市民軍空洞化ループ |
| `ForageRules`（SUN-3 #1128） | 現地調達：占領・通過星系から短期的に自律補給・略奪→安定低下のトレードオフ |
| `SupplyRules`（L-2 #94） | 補給線の接続（二値）・前線枯渇タイマー |
| `WarGoalRules`/`CasusBelli`/`WarWeariness`/`PeaceAcceptance`（Wave 2） | 戦争目標・厭戦・講和受諾（二者間） |
| `ReligionRules`/`Religion`（#172-175） | 改宗圧力・異端・聖戦圧力 |
| `DiplomacyRules`/`DiplomacyState`（DIP-1 #189） | 外交状態遷移・意見・敵対判定 |
| `FrictionRules`（CLZ-1 #1133） | 作戦摩擦：命令深度×補給比×士気→実行成功確率 |
| `DemographicsRules`/`Population` | 人口動態（コホート・出生死亡） |
| `GovernanceRules`/`Province` | 安定度・統合度・不満・反乱圧力 |

**しかし、これらは「戦争がどう始まり・どう解決するか」はカバーするが、
ウェッジウッドが固有に描く「戦争が構造的に自己永続する」メカニズムが欠けている**：

| 三十年戦争が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **コントリビューション制（徴発制）**——軍が通過・占領地の住民から「貢納」を組織的に徴収して自活。軍が動かなければ餓死・解体する構造 | `ForageRules`(SUN-3) は戦術的短期調達。`SupplyRules` は本国補給線。**軍の構造的自活経済（軍が進み続けなければ維持できない動態）が無い** |
| **軍事請負将軍（Kriegsherr）**——将軍が個人の信用で軍を調達・維持し、君主への「債権者」となる。返済は領地・爵位・政治的特権。軍は将軍の私的資本 | `MercenaryRules`(MCN-2) はユニット雇用。`FoederatiRules`(GIB-2) は隊長忠誠。**将軍が君主に対して政治的要求を行使する「軍を担保にした交渉」が無い** |
| **開戦理由の腐食**——宗教的casus belliから始まった戦争が長期化により権力政治・領土獲得へ変容。カトリック仏とプロテスタント瑞が同盟する逆転 | `ReligionRules.HolyWarPressure` は宗教圧力を扱うが、**戦争目的が時間とともに宗教から世俗へドリフトする「目的変容」が無い** |
| **多極講和の協調問題**——三者以上が和平に合意するには全員の最低条件を満たすパッケージが必要。誰か一者が拒否すれば戦争継続。ウェストファリア交渉は5年かかった | `WarGoalRules.PeaceAcceptance` は二者間。**三者以上のパッケージ合意メカニクスが無い** |
| **主権規範の醸成**——ウェストファリアが確立した「領土支配者が宗教を決める」規範。宗教的干渉の正当性が構造的に低下 | `DiplomacyRules` は状態遷移のみ。**外交規範そのものが時間とともに強化され、ある種のcasus belliを「非合法化」する動態が無い** |

**結論**：ウェッジウッドの三十年戦争は本プロジェクトの戦争・傭兵・外交レイヤーに、
①**コントリビューション制**（軍の自己永続経済）
②**軍事請負将軍**（将軍が君主への債権者）
③**開戦理由の腐食**（宗教→権力政治への目的変容）
④**多極講和協調**（三者以上の包括的和平パッケージ）
という4つの真の欠落軸を与える。
MCN-2（傭兵制）・GIB-2（二重忠誠）・SUN-3（現地調達）と**完全に直交し**、「なぜ戦争は自己永続するか」という問いを閉じる。

---

## 1. 役に立つ視点（要約）

ウェッジウッドの三十年戦争を、**本システムに効く形**で1行ずつ：

1. **「戦争が戦争を養う」＝コントリビューション制**——軍は通過地の民衆から組織的に食料・金銭を徴収して自活。動き続けなければ解体する→戦争の自己永続ロジック。→ 新 `KontributionRules`：抽出と星系安定低下のトレードオフ、停滞で軍崩壊。
2. **将軍は君主の臣下でなく債権者だった**——将軍は私財・借金で軍を組成し、君主に「貸し付け」る。返済できなければ将軍は軍を引き連れて離脱するか、領地をよこせと脅す。→ 新 `KriegsherrRules`：財政的レバレッジが政治的要求に変換される経路。
3. **宗教戦争は途中から権力政治になった**——フランスはプロテスタントを支援しながらカトリックの王国。スウェーデンは宗教的大義より帝国の拡大を追った。長期化すると「何のために戦っているか」が変わる。→ 新 `WarPurposeDriftRules`：戦争継続とともにcasus belli が変容し同盟パターンが逆転。
4. **和平は全員の同意が要る——それが難しい**——一者でも拒否すれば戦争継続。「誰も勝てないが誰も止められない」状態がウェストファリアまで続いた。→ 新 `MultipartyPeaceRules`：三者以上の最低要求を満たすパッケージが見つかるか否か。
5. **「cuius regio, eius religio」——領土が宗教を決める**——ウェストファリア主権原則。宗教的干渉の正当性が低下し、「内政不干渉」規範が生まれた。→ 新 `SovereigntyNormRules`：外交規範の成熟が casus belli の種類・強度を変える。
6. **戦争は農村を荒廃させ、荒廃がさらに戦争を激化させた**——省スが抽出を使い果たすと農民反乱が起き、それを鎮圧するために軍がさらに必要になった。→ 既存 `GovernanceRules.RebelPressure`/`KontributionRules`(TYW-1) の連鎖として表現。コード新設不要。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> **大原則：`MercenaryRules`(MCN-2)・`FoederatiRules`(GIB-2)・`ForageRules`(SUN-3)・`SupplyRules`(L-2)・`WarGoalRules`・`ReligionRules`・`DiplomacyRules` を作り直さない。**
> TYW はそれらに**欠落軸を足し、接続するだけ**（additive）。

---

### ★★★ 最優先（三十年戦争の signature・真の欠落）

#### TYW-1 コントリビューション制 `KontributionRules`——「戦争が戦争を養う」

**問題意識**：三十年戦争の軍隊は本国補給線でも短期略奪でもなく、**通過・占領地の住民から組織的な「貢納」を徴収する制度的抽出**で自活した。この制度の恐ろしさは：軍が動かなければ（＝新たな抽出地が見つからなければ）維持できない構造にある。和平＝給料なし＝軍解体→将軍は和平を望まない。

- **`Kontribution`（純データ）**：`systemId` / `extractingFaction` / `extractionRate`（1ターン当たりの抽出量） / `fatigue`（累積抽出負荷 0..1）/ `isExhausted`（枯渇フラグ）。
- **`KontributionRules`（static）**：
  - `ExtractableAmount(province, fatigue)` — 安定度×(1-fatigue)×`extractionBase` で1ターン分の抽出可能量を返す（`GovernanceRules.OutputFactor` 準拠・基準値非破壊）。
  - `ApplyExtraction(province, amount)` — `GovernanceRules.OnOccupied` 相当の安定低下を累積適用し `fatigue` を上昇。
  - `IsExhausted(province, fatigue, threshold)` — 枯渇判定：threshold（既定0.85）超過で以後の抽出はほぼゼロ（軍が次の地へ移る動機）。
  - `ArmySustainabilityFactor(totalExtracted, armyStrength)` — 抽出量が軍の維持閾値を下回ると`SustainabilityFactor`(0..1)低下→`FrictionRules.FrictionFactor`に乗算（軍が足りなければ計画が崩れる）。
  - `MustAdvancePressure(extractionSurplus, armyStrength)` — 余剰がマイナス（抽出<維持費）の場合に「前進圧力」を返す（AIがこれを受けて次の未抽出地へ動く動機）。
- **接続**：`GovernanceRules`（安定低下）× `ForageRules`(SUN-3)（ForageRulesは戦術的短期。KontributionRulesは戦略的構造的自活＝別系統・重複しない）× `FrictionRules`(CLZ-1)（持続可能性因子を摩擦に乗算）× `StrategyRules`（AIの行動動機）× `ResourceStockpile`（抽出物の流入先）。
- **pure logic・test-first・EditMode テスト必須**。

#### TYW-2 軍事請負将軍 `KriegsherrRules`——将軍が君主への債権者になる

**問題意識**：MCN-2 は「ユニットが雇い主に雇われる」関係。GIB-2 は「隊長の忠誠が部隊行動を左右する」関係。TYW が固有に描くのは**将軍が私財・借金で軍全体を組成し、君主に対して「私はあなたに〇〇額を貸しているので、返済として領地・爵位・権限を寄越せ」と交渉できる政治的債権者**になる構造。将軍への未払いは傭兵の離反だけでなく将軍による政治的脅迫・軍の引き揚げに直結する。

- **`MilitaryContractor`（純データ）**：`commanderId`(ICharacter) / `contractedStrength`（請負兵力規模） / `selfFinancingDebt`（将軍個人が立て替えている累積額） / `politicalDemand`（債権を返すべき政治的要求のリスト＝領地/爵位/自治権のenum） / `employer`（雇用勢力）。
- **`KriegsherrRules`（static）**：
  - `AccrueLeverage(contractor, unpaidTurns, dailyCost)` — 未払い期間に比例して `selfFinancingDebt` を増加させ `politicalDemand` を更新。
  - `ContractorLeverage(contractor)` — `selfFinancingDebt / baseSalaryPerTurn` で「何ターン分の借しがあるか」を返す。数値が大きいほど将軍の交渉力が高い。
  - `DemandThreshold(leverage)` — `leverage` が閾値を超えると `politicalDemand` が具体的な要求（領地割譲・自治権付与）に昇格。
  - `WithdrawalRisk(contractor, sovereignFiscalHealth)` — 財政破綻×高レバレッジで軍の引き揚げリスク（`DefectionRisk` の将軍版。MCN-2 は兵士個人・TYW-2 は将軍丸ごと）。
  - `ResolvePayment(contractor, state)` — 財政で返済 or 政治的要求承認で `selfFinancingDebt` を削減。
- **接続**：`MercenaryRules`(MCN-2)・`FoederatiRules`(GIB-2)（これらは「傭兵ユニット/隊長の忠誠」。TYW-2 は「将軍個人の財務的レバレッジ」——別次元・重複なし）× `FiscalRules`（君主側の支払能力）× `LoyaltyRules.ApplyIntrigue`（高レバレッジ状態は intrigue 上昇とみなして `Allegiance` に流す）× `GovernmentRegistry`（政治的要求を `Office`/`TryAppoint` で解決）。
- **pure logic・test-first・EditMode テスト必須**。

---

### ★★ 高（既存への接続補強・戦争の長期変容を描く）

#### TYW-3 開戦理由の腐食 `WarPurposeDriftRules`——宗教から権力政治へ

**問題意識**：三十年戦争は宗教戦争として始まり、中盤以降は領土と国際的プレゼンス争いになった。カトリックのフランスがプロテスタントのスウェーデンと同盟したことが象徴的だ。既存の `ReligionRules.HolyWarPressure` は宗教圧力を扱うが、「戦争の目的そのものが時間とともに宗教的から世俗的にドリフトする」変容メカニクムが無い。

- **`WarPurposeType`（enum）**：`Religious` / `Dynastic` / `Territorial` / `Survival`。
- **`WarPurposeDriftRules`（static）**：
  - `CurrentPurpose(originalPurpose, warElapsedTurns, warWeariness)` — 経過ターン×厭戦度に比例して `Religious`→`Territorial`→`Survival` へドリフト（`CasusBelliDecayRate` 定数）。
  - `IdeologicalDecoupling(factionA, factionB, driftLevel)` — driftが閾値を超えると「宗教的敵対」が同盟の信頼できる予測子でなくなる（宗教が同じでも敵になれる・宗教が違っても同盟できる）。`FactionRelations.IsHostile` の宗教バイアスを係数で減衰。
  - `PurposeLegitimacyFactor(currentPurpose, originalPurpose)` — 当初目的から外れるほど `GoalLegitimacy`（`WarGoalRules`）が低下。
- **接続**：`WarGoalRules.GoalLegitimacy`（腐食した宗教目的は正当性を削る）× `ReligionRules.HolyWarPressure`（ドリフトが進むほど聖戦圧力が弱まる）× `DiplomacyRules`（意見修正子：宗教一致ボーナスの時間的減衰）× `FactionRelations.IsHostile`（宗教バイアスの弱体化）。
- **pure logic・test-first・EditMode テスト必須**。

#### TYW-4 多極講和の協調問題 `MultipartyPeaceRules`——ウェストファリア型パッケージ交渉

**問題意識**：`WarGoalRules.PeaceAcceptance` は二者間の「甲が乙を許容するか」を扱う。三十年戦争のような三者以上が絡む戦争では**全員が同時に受け入れられるパッケージを見つけることが協調問題**になる。誰かが拒否すれば戦争継続——これが30年かかった理由の一つ。

- **`PeacePackage`（純データ）**：`termsPerFaction`（勢力ごとの要求/譲歩リスト）/ `signatories`（合意予定勢力リスト）。
- **`MultipartyPeaceRules`（static）**：
  - `MinimumAcceptableTerms(faction, warGoal, warWeariness)` — 勢力の「これ以下なら合意しない」最低ライン（`WarGoalRules.PeaceAcceptance` を勢力ごとに評価して返す）。
  - `PackageAcceptability(faction, package)` — 特定勢力がパッケージを受け入れるかどうか（最低ライン以上かチェック）。
  - `IsViablePackage(factions, package)` — 全勢力が受け入れるパッケージかどうか（全員 `PackageAcceptability` を通過）。
  - `IsDeadlocked(factions, candidates)` — どの候補パッケージも `IsViablePackage` を満たさない状態（＝多極膠着）を返す。
  - `NegotiationPressure(faction, deadlockTurns, warWeariness)` — 膠着が長引くほど最低ラインが緩む（厭戦が条件緩和を引き出す）→最終的に解を見つけやすくなる。
- **接続**：`WarGoalRules.PeaceAcceptance`（二者間の延長として機能）× `WarGoalRules.WarWeariness`（条件緩和の入力）× `DiplomacyRules.MakePeace`（パッケージ合意で複数ペアの講和を一括発動）× `CLZ-3 TrinitarianTensionRules`（三位一体の崩壊が `NegotiationPressure` を強める）。
- **pure logic・test-first・EditMode テスト必須**。

---

### ★ 中（世界観の深度・外交規範の動態）

#### TYW-5 主権規範の醸成 `SovereigntyNormRules`——「cuius regio, eius religio」の近代的昇華

**問題意識**：ウェストファリア条約が確立した「領土支配者が宗教を決める＝内政不干渉」原則は、外交の基本ルールそのものを書き換えた。これは `DiplomacyRules` に「規範が時間とともに成熟し、ある種の行動（宗教的干渉）の正当性を低下させる」動態を追加することで表現できる。

- **`SovereigntyNorm`（純データ）**：`maturity`（0..1・条約締結や慣習で上昇）/ `scope`（適用対象：全勢力 or 特定勢力間）。
- **`SovereigntyNormRules`（static）**：
  - `ReligiousInterventionFactor(norm)` — norm.maturity が高いほど宗教的casus belli の `GoalLegitimacy` が低下（宗教で攻める正当性が薄れる）。
  - `NonInterventionPressure(norm, targetFaction)` — 規範が成熟した世界では内政干渉的な同盟参戦への国際世論ペナルティ（`DiplomacyState.opinion` 修正子）。
  - `TerritorialIntegrityBonus(norm, defendingFaction)` — 純粋な領土征服戦争に対する防衛側の `GoalLegitimacy` ボーナス（侵略の正当性低下）。
  - `Mature(norm, treatyEvent)` — 平和条約締結・大国間合意で `maturity` を加算。
- **接続**：`WarGoalRules.CasusBelli`（宗教的casus belli のコスト上昇）× `DiplomacyRules.ActiveDiplomacy`（IsHostile 判定への連絡）× `ReligionRules.HolyWarPressure`（聖戦圧力の外部的抑制）× `TreatyRules`（DIP-2 Wave2）（条約締結で規範成熟）。
- **pure logic・test-first・EditMode テスト必須**。

#### TYW-6 （lore）三十年戦争の世界観開示データ

- 「戦争は技術でなく経済で終わる——誰も払えなくなった時に終わった」
- 「軍は解体する前に次の戦争を見つけねばならない——傭兵将軍の動機」
- 「勝者のいない戦争——全勢力が疲弊して終わる局面がある（#205 ハーモニー的解決とは別）」
- **コード新設なし**。`DisclosureLedger`（FND-4）への lore データ入力。CCX-6（世界観codex退避）方針に一貫。

---

### ❌ 不採用（重複・既存で十分・タイクン化回避）

| 不採用 | 理由 |
|---|---|
| 基本的傭兵雇用・忠誠低下・離反 | **MCN-2 `MercenaryRules`（#1381）が既にカバー**。TYW は追加しない |
| 傭兵隊長の二重忠誠・部隊丸ごと離反 | **GIB-2 `FoederatiRules`（#1283）がカバー**。TYW-2 は「将軍=債権者」次元のみ |
| 戦場の略奪・個別暴力 | `ForageRules`(SUN-3) の安定低下で十分。新規実装はタイクン化 |
| 疫病・飢饉の伝播ルール | `DemographicsRules.Tick`（死亡率カーブ）でカバー範囲内。新規疫病エンジンはタイクン化 |
| 宗教改革・プロテスタント/カトリックの教義差 | 宗教の教義差はゲームで扱う必要なし。`ReligionRules` の汎用圧力で十分 |
| 戦争期間の過少見積もり | **GUN-4 `WarScopeRules`がカバー**（戦争長期化ペナルティ） |
| 同盟連鎖の自動参戦 | **GUN-2 `AllianceCascadeRules`がカバー** |
| 複数星系にまたがる戦闘作戦マイクロ | タイクン化回避。`StrategyRules` の抽象解決で十分 |

---

## 3. EPIC #1415 の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。
> 既存 MCN-2/GIB-2/SUN-3 は**接続のみ・重複新設しない**。
> **著作権注意：固有名・文章・キャラは不使用、歴史的メカニクス/構造のみ参考。**

> **EPIC = #1415**。GitHub issue 起票済み（#1420〜#1429）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **TYW-1** | #1420 | コントリビューション制（`KontributionRules`・軍が占領地の組織的抽出で自活・枯渇で前進圧力） | `GovernanceRules`×`ForageRules`(SUN-3)×`FrictionRules`(CLZ-1)×`StrategyRules` |
| **TYW-2** | #1424 | 軍事請負将軍（`KriegsherrRules`・将軍が個人融資で軍を所有・財務レバレッジ→政治的要求） | `MercenaryRules`(MCN-2)×`FiscalRules`×`LoyaltyRules`×`GovernmentRegistry` |
| **TYW-3** | #1426 | 開戦理由の腐食（`WarPurposeDriftRules`・宗教→権力政治ドリフト・意識的同盟逆転） | `WarGoalRules.GoalLegitimacy`×`ReligionRules.HolyWarPressure`×`DiplomacyRules` |
| **TYW-4** | #1427 | 多極講和の協調問題（`MultipartyPeaceRules`・三者以上の包括パッケージ合意・膠着検知） | `WarGoalRules.PeaceAcceptance`×`DiplomacyRules.MakePeace`×`TrinitarianTensionRules`(CLZ-3) |
| **TYW-5** | #1428 | 主権規範の醸成（`SovereigntyNormRules`・領土主権規範成熟→宗教的干渉の正当性低下） | `WarGoalRules.CasusBelli`×`ReligionRules.HolyWarPressure`×`DiplomacyRules`×`TreatyRules` |
| **TYW-6** | #1429 | （lore）三十年戦争の世界観開示データ（「誰も払えなくなった時に終わる」等を`DisclosureLedger`へ） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順

`TYW-1 → TYW-2`（コントリビューション制＋軍事請負将軍＝三十年戦争の2大 signature）
→ `TYW-3`（開戦理由の腐食＝長期戦の変容を描く・`WarGoalRules`への接続）
→ `TYW-4`（多極講和＝戦争を終わらせる難しさ・`PeaceAcceptance`の多者版）
→ `TYW-5`（主権規範＝外交ルールそのものの変化・波及大）
→ `TYW-6`（lore データ最後に整理）。

> いずれも MCN-2/GIB-2/SUN-3/CLZ-1〜4/GUN-1〜4 を**後退させず接続**する additive 設計。
> TYW の貢献は「戦争の自己永続ロジック」と「多極戦争の終結メカニクス」の2本柱。
