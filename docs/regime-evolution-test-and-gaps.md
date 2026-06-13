# 政体進化（首長制→民主/独裁→下位形態）— テストプロンプトと不足点

> 完成像：勢力は **首長制スタート**から進化し、最終的に **民主主義**（立憲君主制／共和制）か **独裁主義**（共産主義／指導者独裁）へ**分岐**する。
> 本書は (1) この分岐進化を検証するテストプロンプト、(2) 現状の不足点（あぶり出し）、(3) 不足を埋める最小設計、をまとめる。

---

## 0. 結論（先に）

> **状態：§4 の最小設計を実装済**＝`GovernmentForm`／`GovernmentFormRules`（Core・test-first `GovernmentFormRulesTests`）＋`FactionState.governmentForm`＋`GalaxyView.RunRegimeEvolutionTick`（年次・帝国=君主制/同盟=共和制シード→社会シグナルで分岐進化・通知）。形態は軸（軍政/所有）へ `ControlTypeOf`/`OwnershipOf` で橋渡し。**政変による形態転換も実装済**＝`PoliticalUpheavalRules`（`CivilianControlRules.WouldCoup`→`CoupRules`→成功で `FormAfterCoup`＝軍部→指導者独裁/革命→共産主義or共和制/宮廷→不変）を `RunRegimeEvolutionTick` が年次で回す（政変が無ければ緩やかな `NextForm` 進化）。**形態差の常時帰結も一部実装**＝(a) **捕虜処遇** が政体に追従（`FactionControl`→`ControlTypeOf`＝共産化で処断的・民主は解放・`ResolveCaptives`）／(b) **軍人事ドクトリン** が政体で変わる（`WarCollegeDoctrine`→`PromotionDoctrineOf`＝民主=実力主義/専制=学閥主義＝恩賜の軍刀組の効きが政体で変化）。**残＝企業利潤先（`OwnershipOf`→`EnterpriseRules` 国有/私有・要・企業の盤面配線）／任免資格（`DefaultMilitaryOnly`→`OfficeRules`・要・役職システム拡充）／選挙（要・`LeadershipElectionRules` 配線）**。下記は当初のギャップ分析（記録）。

**（当初の不足）政体の「形態」を表す統一型と、形態間の遷移グラフが存在しなかった。** あるのは**軸がバラバラの部品**だけ：

| 軸 | 既存モジュール | カバー範囲 |
|---|---|---|
| 軍政（誰が軍を握る） | `CivilianControlType{文民統制,君主統帥,党軍,軍部優位,未分化}` | 5型あり（**未分化=首長制**）。だが「政体形態」ではなく軍-政の関係軸 |
| 立憲度 | `Constitution`＋`ConstitutionRules.IsConstitutionalMonarchy` | 立憲か否かの判定はある |
| 所有（共産か） | `PropertyRules.Ownership{私有,国有}` | 国有=共産の判定材料 |
| 君主の有無 | `Person.isSovereign`／継承#152 | 君主が居るかは表せる |
| 王朝サイクル | `Regime`/`DynastyRules`（正統性/腐敗/徳・`Reform`/`Revolution`） | **王朝交代（同形態で君主が替わる）**であって形態転換ではない |
| 統治スタイル | `FactionState.inclusiveness`（収奪↔包摂） | 形態と直交の連続値 |
| 政変 | `CoupRules{軍部,宮廷,革命}` | 政変は解決するが**形態を変えない**（`PostCoupLegitimacy` は正統性だけ） |
| 選挙/政党 | `LeadershipElectionRules`/`PartyRules` | 共和制/民主の部品（未配線） |

**＝ユーザーの5形態は「既存の軸の組み合わせ」で表せるが、それを名前付き形態に合成し・遷移させる層が無い。** さらに `FactionData.ideology` は自由文字列、`FactionState` は政体形態フィールドを持たない。これらの軸の大半は C1 監査で **未配線（盤面で効かない）**。

---

## 1. 目標モデル（形態 ↔ 既存軸 の対応）

| 政体形態 | 君主 | 立憲(ruleOfLaw/権利) | 選挙/議会 | 所有 | 軍政(CivilianControl) |
|---|---|---|---|---|---|
| **首長制（出発点）** | 首長＝軍事政治一体 | 低 | なし | — | 未分化 |
| **立憲君主制**（民主） | 君主（象徴・縛られる） | 高 | あり | 私有 | 文民統制 |
| **共和制**（民主） | 君主なし（元首=選出） | 高 | あり | 私有 | 文民統制 |
| **共産主義**（独裁） | なし（党首） | 法は党の下 | 党内のみ | 国有 | 党軍 |
| **指導者独裁**（独裁） | 個人（君主/非君主） | 低（恣意的権力） | 形骸/なし | 私有 | 軍部優位 or 非立憲な君主統帥 |

### 遷移グラフ（進化の分岐）
```
首長制 ──制度化/世襲確立──► 君主制 ──立憲化(Constitution投資)──► 立憲君主制 ──君主廃止──► 共和制   ＝民主ルート
   │                          │
   └──個人への権力集中────────┴──► 指導者独裁                                                    ＝独裁ルート
                              │
                              └──革命(国有化+党軍)──► 共産主義                                     ＝革命ルート
```
- 各遷移のトリガ候補：正統性/腐敗（`DynastyRules`）・合意（`ConsentRules`）・支持/希望（`community.hope`）・立憲投資（`ConstitutionRules`）・クーデター（`CoupRules`）・包摂度（`inclusiveness`）。

---

## 2. テストプロンプト（コピペ可・受け入れ条件付き）

> **R1（政体進化が分岐するか）**
> 「政体が **首長制スタート**から **民主主義（立憲君主制/共和制）** か **独裁主義（共産主義/指導者独裁）** へ分岐進化するかをテスト観点に、次をあぶり出す：(1) **政体形態を表す単一の型**と「現在どの形態か」の判定があるか、(2) 形態間の**合法な遷移と発火条件**（制度化/立憲化/民主化/独裁化/革命）があるか、(3) 各形態が統治で**実際に違う挙動**になるか（任免の資格・所有の利潤先・クーデターリスク・支持/正統性・選挙の有無）。配線/新設はその後、承認の上で最小に。」

補助プロンプト：
- **R2**：「`首長制(未分化)→君主制→立憲君主制→共和制` の民主ルートが条件付きで進むかをテスト観点に、立憲化（`ConstitutionRules` 投資）と君主廃止（`Person.isSovereign` 解除＋選挙`LeadershipElectionRules`）が形態を実際に変えるか確認」
- **R3**：「`君主制/指導者独裁→共産主義` の革命ルートで、`PropertyRules.Ownership` が国有へ・`CivilianControlType` が党軍へ・君主が消えるかをテスト観点に、革命が形態を変えるか確認（`DynastyRules.Revolution` は王朝交代であって形態転換でない点も pin）」
- **R4**：「各形態が**選択として意味を持つか**（G2）＝立憲君主制と指導者独裁で、軍人事の文民統制・クーデターリスク・捕虜処遇・税/支持 が有意に違うかをテスト」

---

## 3. 不足点（あぶり出し）

1. **`GovernmentForm` 統一型が無い**：首長制/立憲君主制/共和制/共産主義/指導者独裁 を表す enum が存在しない。
2. **現在形態の判定（合成）が無い**：軸（君主有無×立憲×所有×選挙×軍政）→名前付き形態 の分類器が無い（`IsConstitutionalMonarchy` の単発判定のみ）。
3. **形態遷移グラフが無い**：合法遷移と発火条件（制度化/立憲化/民主化/独裁化/革命）が定義されていない。`DynastyRules.Revolution`＝**王朝交代（同形態）**、`CoupRules`＝政変だが**形態不変**。
4. **`FactionState` が政体形態を持たない**：`inclusiveness` はあるが形態フィールドが無い＝勢力が「今どの政体か」を保持・進化させられない。
5. **出発点=首長制が未モデル**：`未分化` は `CivilianControlType` 値としてあるが、キャンペーン開始時に勢力を首長制で起こし進化させる機構が無い。
6. **軸の大半が未配線**（C1 Tier A）：`CivilianControlRules`/`CoupRules`/`ConsentRules`/`ConstitutionRules`/`PropertyRules`/`LeadershipElectionRules`/`PartyRules` が盤面で効いていない＝形態を作っても帰結が出ない。
7. **`FactionData.ideology` が自由文字列**：型安全な政体分類に使えない（"専制/民主/共産" 等の運用と不整合の恐れ）。

---

## 4. 不足を埋める最小設計（提案・Core 純ロジック中心）

> 既存の軸を**壊さず合成・遷移させる薄い層**を1枚足す（タイクン化回避＝形態は少数の enum、遷移は条件付き離散イベント）。

1. **`enum GovernmentForm { 首長制, 君主制, 立憲君主制, 共和制, 共産主義, 指導者独裁 }`**（Core）。
2. **`GovernmentFormRules`（static・唯一の窓口）**：
   - `Classify(sovereignPresent, Constitution, Ownership, hasElections, CivilianControlType) → GovernmentForm`（軸→形態の合成）。
   - `Axes(GovernmentForm) → 既定の軸セット`（形態→各軸の既定値＝`Apply` の出所）。
   - `CanTransition(from, to) → bool`（§1 のグラフ）＋`TransitionTrigger(from, to, Regime/Polity/Community/inclusiveness) → bool`（発火条件）。
   - `Apply(FactionState, GovernmentForm)`：形態に合わせて軸（君主/立憲/所有/軍政型）を設定＝**形態が統治に効く起点**。
3. **`FactionState.governmentForm`（既定 首長制）**＋`CivilianControlType` 由来軸の保持。
4. **配線**：`GalaxyView` 年次 Tick に `RunRegimeEvolutionTick`＝各勢力で `TransitionTrigger` 成立なら `CanTransition` 内で形態を進め `Apply`＋通知（政治カテゴリ）。`CoupRules`/`DynastyRules` の帰結を遷移トリガに接続（クーデター成功→指導者独裁/革命→共産 等）。
5. **既存窓口へ接続**（C1 Tier A と同時に効く）：`CivilianControlRules`（任免・クーデターリスク）・`OfficeRules`（資格）・`PropertyRules`（利潤先）・`CaptivityRules`（処遇）・選挙`LeadershipElectionRules` が `governmentForm` を読む。
6. **観測**：`CampaignObserverOverlay`/`CoreStateInspector` に現在の政体形態を表示＋glossary 追記。

> ＝**形態は「軸の合成ビュー」**として持ち（二重管理しない）、遷移は条件付き離散イベント。これで「首長制→民主/独裁→下位形態」の分岐進化が、既存の軍政・立憲・所有・クーデターの帰結に乗って回る。

---

## 5. 推奨着手

- まず **R1 を検証テスト**として実装（現状＝形態型なし・遷移なしを pin）。
- 次に §4 の `GovernmentForm`＋`GovernmentFormRules`（Core・test-first）を新設し、`Classify`/`CanTransition`/`Apply` を EditMode で固定。
- 配線（年次 Tick＋既存窓口接続）は C1 Tier A の軍政駆動（CoupRules/CivilianControl）と**同時に行う**と、政体の選択がクーデター・人事・所有・支持に一気に効く。
