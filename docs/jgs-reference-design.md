# 『貞観政要』参考設計（EPIC #JGS）

> 参照元：呉競 撰『貞観政要』（唐・玄宗時代に成立）。太宗李世民（唐第2代皇帝）と魏徴ら側近との問答を編集した帝王学の古典。
> 「貞観之治」（唐の最盛期政治）を可能にした**君主の自制・直言する臣・創業と守成の相克**を記録する。
> 本ドキュメントは、当プロジェクト（Ginei＝社会・政治シミュ層を持つ星間国家戦略）にとって
> **役に立つ視点だけ**を抽出し、EPIC `#JGS` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**統治メカニクス／世界観の構造パターンのみ**を参考にする。

---

## 0. なぜ「貞観政要」が本システムに役立つか

当プロジェクトは統治・諫言・人事に関する**純ロジックを大量に保有**している：

| 既存（カバー範囲） | 対応モジュール |
|---|---|
| 佞臣問題・政治情報品質の供給側（直言する意欲） | `CounselIntegrityRules`（MKV-3 設計済み） |
| 軍事献策・提案→採択 | `CounselRules`（SGZ-2 設計済み） |
| 適材適所・役職適性 | `PersonRules`/`OfficeRules`/`VacancyRules` |
| 王朝腐敗・易姓革命サイクル | `DynastyRules`/`Regime` |
| 功績→昇進（秦モデル） | `MeritRankRules`（QIN #900-905 設計済み） |
| 年齢退役・停年・再召集 | `RetirementRules`（#530-536 設計済み） |
| 組織継承・カリスマの日常化 | `SuccessionRules`/`Organization`（#812 設計済み） |

**しかし、これらは「制度・供給側・年齢管理」の抽象モデル**であり、貞観政要が固有に描く以下が**欠けている**：

| 貞観政要が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **納諫の需要側（君主が聞くか）** | MKV-3 `CounselIntegrityRules` は「臣が直言する意欲」（供給側）を扱う。**「君主が実際に受け取るか（受容性スコア）→受け取らないと有能な臣が去る」という需要側フィードバックループ**が無い |
| **創業フェーズ→守成フェーズの遷移** | `DynastyRules` は腐敗の線形蓄積のみ。**「征服期は勇断型指揮官が最適・安定期は文治型が最適」というフェーズ依存の人材適性切替**が無い |
| **成功→慢心の非線形加速** | `DynastyRules.Tick` は腐敗が徳に比例して減速するが線形。**「大きな成功が倦怠・奢侈・自信過剰を加速する」非線形トリガー**が無い |
| **創業功臣の守成期ミスマッチ** | `RetirementRules` は年齢・停年による退役を扱う。**「征服戦で功績を積んだ英雄が平時行政に置かれると機能不全→省益増大/支持喪失のジレンマ」**は無い |

**結論**：貞観政要は当プロジェクトの統治ロジックに**「名君×名臣の相互作用ダイナミクス」**という視角から、
①**納諫の需要側ループ**、②**創業→守成フェーズ遷移**、③**成功慢心の非線形加速**、
④**創業功臣の守成期ジレンマ**という4つの欠落軸を与える。
`DynastyRules`（腐敗）・`PersonRules`（適材）・`LoyaltyRules`（忠誠）への**additive な接続**。

---

## 1. 役に立つ視点（要約）

貞観政要の世界観を、**本システムに効く形**で1行ずつ：

1. **「聞く君主」がシステムを生かす** — 有能な臣が直言しても聞かなければ無効。受容性の低い君主には有能な臣が近寄らなくなる。→ `RemonstranceRules` で受容性スコアとフィードバックループをモデル化（MKV-3 供給側と別系統）。
2. **創業の英雄は守成の荷物になりうる** — 征服期の胆力は安定期の慎重さと相反する。同じ人物が「英雄から障害へ」転化するジレンマが君主を悩ませる。→ `ReignPhaseRules` で創業/守成フェーズを明示的に切り替え、`PersonRules` 適性を相対化する。
3. **大きな成功が倦怠を呼ぶ** — 難関を乗り越えた君主が繁栄の中で気を緩め慢心する。太宗自身が晩年にこの罠に嵌まった。→ `DynastyRules` の慢心加速係数で非線形化。
4. **制度が个人を超える** — 優れた君主一人の判断に頼る統治は脆い。諫言機構を制度化して「聞く仕組み」を保証することが王朝の持続を担う。→ `RemonstranceRules` は個人属性だけでなく制度(`Office`)の有無でも受容性を補強できる。
5. **功臣を尊重しながらも権限を区切る** — 巧みに処遇した太宗は、功臣の体面を守りつつ実権を文治官僚に移した。不器用に扱うと功臣が反乱の核心になる。→ `FounderTransitionRules` で創業功臣の移行コストをモデル化。
6. **守成は創業より難しい** — 「創業は守成に易く、守成は創業に難し（創業難、守成更難）」。→ `ReignPhaseRules` で守成フェーズの安定コストを定式化。既存EPIC（SGZ/MKV/CLZ）が扱う戦争・諜報とは異なる**平時統治の難問**を補完。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`DynastyRules`/`PersonRules`/`LoyaltyRules`/`CounselIntegrityRules` を作り直さない**。JGS はそれらに**欠落軸を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・貞観政要の signature）

#### JGS 納諫率・受容性フィードバックループ（需要側の諫言系）
- **受容性スコア** `RulerReceptiveness`（0〜1）：君主が直言をどれほど受け入れる傾向にあるかを表すスコア。
  - 高い（～1.0）：直言を受け入れ、政策精度 → `GovernanceRules.OutputFactor` が向上。有能な臣が留まりやすい。
  - 低い（～0.0）：直言が蹴られ続けると有能な臣に「フラストレーション」が蓄積 → 閾値超過で `VacancyRules` の自発退出。
- **供給側（MKV-3）との分業**：MKV-3 `CounselIntegrityRules` は「臣が直言する意欲・情報品質」＝供給側。JGS-1 は「君主が実際に受け取るか」＝需要側。両者が低いと二重に機能不全。
- **受容性の変化**：成功 → `HubrisfFactor`（JGS-3 参照）が上昇すると受容性が低下（高慢状態）。逆境・失敗 → 受容性が回復するチャンス。`Office` 内に諫言専従ポストがあると受容性に下限を設ける制度的ブレーキ。
- 接続：新 `RemonstranceRules`（static・Core 純ロジック）。`GovernmentRegistry`/`PersonRules` × `FactionState.corruption` × `DynastyRules`（JGS-3）。EditMode テスト必須。

#### JGS 創業→守成フェーズ遷移（統治フェーズ状態機械）
- **フェーズ enum** `ReignPhase { 創業, 守成 }`
  - 創業フェーズ：拡張・戦争行動のコストが低い。高機動/攻撃型人物が政策効率最大。失敗への許容度が高い（勝てば官軍）。
  - 守成フェーズ：拡張コストが増大し安定維持が優先される。文治/行政型人物が政策効率最大。同じ大胆行動がリスクになる。
- **遷移条件** `ShouldTransition`：占領星系の統合率・経過年・安定度の閾値を超えると守成フェーズへ移行。後退（反乱/占領喪失）で創業フェーズへ差し戻し可。
- **PersonRules への波及** `PersonRoleMatchesPhase`：`PersonRules.Effectiveness` に phase 整合性を追加修正子。創業期の英雄が守成期文官職に置かれると Effectiveness が下がる（ミスマッチペナルティ）。
- 接続：新 `ReignPhaseRules`（static・Core 純ロジック）。`DynastyRules` × `PersonRules.Effectiveness` × `GovernanceRules` × `CampaignRules.Tick`。EditMode テスト必須。

### ★★ 高（既存モジュールへの重要な欠落補完）

#### JGS 成功慢心加速と君主の自制（非線形腐敗トリガー）
- **慢心加速係数** `HubrisfFactor`：最近の成功度合い（拡張成功率・高安定期間・勝利連続）を集計し、`DynastyRules.Tick` の腐敗増分に乗算する非線形項。
  - 平和な繁栄が続くほど `HubrisfFactor` が高まり、腐敗の増速が加速する。
  - 逆境・敗北 → 係数が下落し腐敗増速が緩和される（苦難が君主を鍛える）。
- **自制のブレーキ** `SelfRestraintBrake`：`RulerReceptiveness`（JGS-1）が高い君主は `HubrisfFactor` の上昇速度が遅くなる。直言を聞く君主は自身の高慢化を自覚できる。
- 接続：`DynastyRules.Tick` に additive 項として挿入（既存の徳減速と並存）× `RemonstranceRules`（JGS-1）。EditMode テスト必須。

#### JGS 功臣の処遇ジレンマ（創業英雄の守成期移行コスト）
- **移行コストモデル** `FounderTransitionRules`：
  - `IsFounderMismatch(person, phase)`：創業功臣が守成フェーズの文官職に在る場合に真。
  - `TransitionCost(person, newRole, loyaltyWeight)`：功臣を軍職→文職へ移す際のコスト（本人フラストレーション ＋ 旧部下の支持低下）。転換を無理強いすると `LoyaltyRules` に波及する離反リスク。
  - `LegacyBonus(person)`：うまく処遇して移行できた功臣は「象徴的結束」として組織に正の係数を与える（`Organization.cohesion` 小幅ブースト）。
  - `ForcedRetirementRisk(person, phase)`：守成フェーズで高 `institutionalInterest` の功臣を放置すると `MinistryRules.SectionalismFriction` が増加。
- 接続：新 `FounderTransitionRules`（static・Core 純ロジック）。`ReignPhaseRules`(JGS-2) × `PersonRules` × `LoyaltyRules` × `MinistryRules` × `RetirementRules`（既存の年齢退役と別軸・接続のみ）。EditMode テスト必須。

### ★ 中（世界観 lore・既存の接続強化）

#### JGS（lore）世界観の開示データ
- 「名君と名臣の対話が黄金期を作る」「守成は創業より難し」「成功が慢心を呼び、慢心が滅亡を呼ぶ」。
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への**lore データ入力**。CCX-6 方針に一貫。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 佞臣の供給側（直言する意欲・情報品質） | MKV-3 `CounselIntegrityRules` が既にカバー。JGS は需要側のみ |
| 軍事献策・策略提案 | SGZ-2 `CounselRules` が軍事提案を、MKV-3 が政治提案品質をカバー |
| 賢才の挙用（積極的人材登用） | `PersonRules.BestFor`/`VacancyRules.SelectSuccessor`/`CaptivityRules.Recruit` の組合せでカバー |
| 年齢退役・停年 | `RetirementRules`（#530-536）がカバー。JGS-4 は相互接続のみ（重複新設しない） |
| 朝廷礼制・官制ミクロ | タイクン化回避。`GovernmentRegistry`/`OfficeRules` で十分 |
| 歴史編纂そのもの（国史制度） | 列伝#784/殿堂#785/開示FND-4 が既に史書機能を担う |

---

## 3. EPIC #JGS の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UI へ配線。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #1226**。GitHub issue 起票済み（#1227〜#1231）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **JGS-1** | #1227 | 納諫率・受容性ループ（君主が聞くか＝需要側。高フラストレーションで有能な臣が離脱） | 新 `RemonstranceRules`。`GovernmentRegistry`×`PersonRules`×`FactionState`。MKV-3 供給側と別系統 |
| **JGS-2** | #1228 | 創業→守成フェーズ遷移（フェーズ状態機械＋PersonRules 整合修正子） | 新 `ReignPhaseRules`。`DynastyRules`×`PersonRules.Effectiveness`×`GovernanceRules` |
| **JGS-3** | #1229 | 慢心加速と君主の自制（成功→非線形腐敗加速。受容性が高いと減衰） | `DynastyRules.Tick` 拡張 + `RemonstranceRules` ブレーキ |
| **JGS-4** | #1230 | 功臣の処遇ジレンマ（創業英雄の守成期ミスマッチ→移行コスト・省益増大） | 新 `FounderTransitionRules`。`ReignPhaseRules`×`PersonRules`×`LoyaltyRules`×`MinistryRules` |
| **JGS-5** | #1231 | （lore）世界観の開示データ（守成の難しさ・名君と名臣の相互作用の成功例） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`JGS-1`（納諫ループ＝最も欠落が大きく MKV-3 供給側と対になる signature）→
`JGS-2`（創業→守成フェーズ＝PersonRules 適性を相対化する骨格）→
`JGS-3`（慢心加速＝JGS-1 と JGS-2 に依存して完成）→
`JGS-4`（功臣ジレンマ＝JGS-2 フェーズ + `RetirementRules` 接続）→
`JGS-5`（lore）。

> いずれも既存の統治・人事・腐敗ロジックを**後退させず接続**する additive 設計。
> 「創業後の平時統治」という戦略フェーズに最も効く（SGZ/MKV/CLZ が扱う戦争・征服の補完）。
