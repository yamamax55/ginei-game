# 氷と炎の歌（マーティン）参考設計（EPIC #SOIF）

> 参照元：ジョージ・R・R・マーティン『氷と炎の歌』シリーズ（英題 *A Song of Ice and Fire*）。
> 単一の王位をめぐり**複数の主張者が同時に正統性を主張し**、戦争・陰謀・外交・債務が複雑に絡み合う群像劇。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略＋既に大規模な継承/政治/財政純ロジック層）にとって**役に立つ視点だけ**を抽出し、EPIC `#SOIF` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定（ウェスタロス/ドラゴン/家紋等）は流用せず、**継承競合・財政政治・崩壊波及の構造パターンのみ**を参考にする。

---

## 0. なぜ「氷と炎の歌」が本システムに役立つか

当プロジェクトは継承・政治・財政の**純ロジックを広範にカバー**している：

| 既存（カバー範囲） | カバー内容 |
|---|---|
| `SuccessionLawRules`/`SuccessionLaw`（PDX-1 #646） | 継承法の4類型（長子/分割/指名/選挙）・`HeirShare`・`SuccessionCrisisRisk` |
| `Organization`/`SuccessionRules`（#812） | 英雄死後の組織存続・制度化と個人カリスマの引継ぎ |
| `LoyaltyRules`/`BattleAllegianceRules`（#817・#822） | 旗幟・寝返りカスケード・静観・不可逆ロック |
| `PledgeRules`（SGZ-3 #1107） | 個人結盟と盟誓・拘束力・離反ペナルティ |
| `MarriageRules`/`MarriageAlliance`（PDX-2 #647） | 政略結婚・同盟絆・`ClaimInheritance`（請求権継承） |
| `CoupRules`（#215-219） | クーデター：軍部/宮廷/革命・成功率・内戦リスク |
| `BankRules`/`Bank`（#186） | 信用創造・取付け・債務超過 |
| `FiscalRules`/`FiscalState`（#161/163） | 国債/金利/財政悪化→通貨安・税/階級別負担 |
| `VacancyRules`（LIFE-2 #152） | 死亡・捕虜で空いた役職への単一後任補充 |
| `LifecycleRules`/`Calendar`（LIFE-1/2 #151/#152） | 年齢・死亡・席の空き |

**しかし、氷と炎の歌が固有に描く以下が真に欠けている：**

| 氷と炎の歌が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **複数の主張者が同時に正統性を主張し争う** | `SuccessionLawRules` は法的後継者を1人選出するだけ。**3者以上が「私が正統」と主張し、それぞれが勢力を結集して戦う**多極同時フェーズが無い |
| **鉄の銀行モデル：貸し手が返済能力で政治的生死を決する** | `BankRules` は純粋な信用/不払い。**貸し手が意図的に複数陣営へ選択的に融資し、返済能力のある側を存続させ、不払い側の打倒を支援する**政治的融資が無い |
| **継承危機が外部介入・地方割拠・傭兵市場を呼び込む** | `FactionState.IsCollapsing` や `SuccessionCrisisRisk` は危機を**スカラー値**で測るが、危機が長引く間に**外部勢力の侵入・地方豪族の独立宣言・傭兵を雇う資金競争**という盤面の崩壊プロセスが無い |
| **主役の死と語り者の交代** | 提督死亡（`LifecycleRules`）と開示エンジン（`DisclosureLedger`）は別個。**ネームドキャラの死が新しい開示者を能動的に「活性化」する**語り継ぎ接続が無い |

**結論**：氷と炎の歌は当プロジェクトの継承・財政・開示システムに**①請求者並立の競合解決 ②融資政治（戦費の政治武器化）③危機の盤面波及**という3つの欠落軸を与える。いずれも既存モジュールへの additive 接続で実現できる。

---

## 1. 役に立つ視点（要約）

氷と炎の歌の世界観を、**本システムに効く形**で1行ずつ：

1. **「継承は法律では決まらない——軍事力が決める」**。正統性の主張は戦争の前置きにすぎず、主張者の数だけ内戦が生まれる。→ `SuccessionLawRules.SuccessionCrisisRisk` が跳ね上がった先の**競合解決メカニクス**が空白。
2. **「債務は必ず返済される——返せない者の敵に銀行が融資する」**。鉄の銀行は単なる貸し手でなく**政治アクター**。貸し手の政治的意図が戦争の勝者を決める。→ `BankRules` に政治的融資選択を追加すると戦費金融が戦略の一軸になる。
3. **「主役が死ぬ——だから物語が動く」**。既存の継承システムは後任を補充するだけ。**死が新しい語り手を目覚めさせ、隠されていた真実を開示する**仕組みが開示エンジン（FND-4）に欠けている。
4. **「空位は主張者を召喚する——そして外部からも入ってくる」**。空席が1人の正当後継者でなく3人の主張者を生む。主張者ごとに諸侯が旗幟を選び、外部勢力が「どちらが勝てるか」を計算して介入する。
5. **「継承戦争は長引くほど戦場が広がる」**。戦乱が続く間に地方豪族が独立を宣言し傭兵が職を得て国境が霞む。→ `FactionState.IsCollapsing` の先に**時間依存の盤面崩壊プロセス**が欲しい。
6. **「義より算段——食糧が尽きれば忠誠も尽きる」**。氷と炎の歌の宴と冬は士気と補給の寓話。`SupplyRules` × `LoyaltyRules` の接続に血肉を与える lore。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：`SuccessionLawRules`/`BankRules`/`LoyaltyRules`/`FactionState` を作り直さない。SOIF はそれらに**欠落軸を足し、接続する**（additive）。

### ★★★ 最優先（真の欠落・氷と炎の歌の signature）

#### SOIF 継承主張者の並立競合（ClaimantNetworkRules）
- **Claimant** データ型：主張者の正統性スコア（法的根拠 × 世論承認 × 軍事後援）と、支持者リスト（`Allegiance`）。
- **並立フェーズ検知**：`SuccessionCrisisRisk` が閾値を超えたとき、単一候補を返さず**Claimant リストを生成**。各 Claimant が `LoyaltyRules.ResolveCascade` の入力になる。
- **競合解決**：軍事実効兵力（`LoyaltyRules.EffectiveStrength`）最大が王位に就く。あるいは外交的和解（最強者が主張を譲る代わりに地位を得る）。
- 接続：`SuccessionLawRules`（クライシスリスク） × `LoyaltyRules`（旗幟カスケード） × `GovernmentRegistry`（役職確定）。
- 純ロジック・test-first。EditMode テスト必須。

#### SOIF 融資政治と戦費債務（DebtLeverageRules）
- **政治的融資**：貸し手（`Bank`）が返済信用度（`FiscalState` × 安定度）だけでなく**政治的優先度**（支持したい勢力）でも融資を選択。不払い側には代わりに**その敵対勢力へ融資**する。
- **レバレッジ効果**：融資を受けた側はターン毎に戦力ボーナス（`FleetPool.Add`）、融資を断られた側は `FleetPool` 上限が低下。
- **不払いトリガー**：`FiscalRules.IsDebtSpiral` の勢力に `BankRules.BankRunRisk` とは別に「融資打ち切り + 敵国支援」フラグが立つ。
- 接続：`BankRules`（信用）× `FiscalRules`（財政）× `FleetPool`（兵力）× `DiplomacyRules`（外交状態）。
- 純ロジック・test-first。EditMode テスト必須。

### ★★ 高（継承危機の時間依存波及）

#### SOIF 継承危機の盤面波及（ClaimantCrisisRules）
- **危機タイマー**：Claimant 並立フェーズが継続するターン数を計上。ターン経過と共に：
  - 地方豪族の独立宣言確率↑（`GovernanceRules.RebelPressure`接続）
  - 外部勢力の介入誘引（`DiplomacyRules.TargetOpinion` に「空位の弱さ」修正子）
  - 傭兵コスト上昇（戦費需要増大→`FiscalRules.InterestRate` リスクプレミアム加算）
- **収束圧力**：危機が長引くほど全主張者の実効支配コストが増え、どこかで譲歩が合理化される（ゲーム理論的均衡）。
- 接続：`GovernanceRules`（反乱圧力）× `DiplomacyRules`（外部介入）× `FiscalRules`（戦費インフレ）。
- 純ロジック・test-first。EditMode テスト必須。

### ★ 中（語り継ぎと lore）

#### SOIF（lore）物語視点の死と交代・大義より債務・冬の寓話
- ネームドキャラ（`Person`）の死亡（`LifecycleRules.Kill`）が、その人物固有の `DisclosureEntry` を**死後開示モード**に切り替える——通常は prerequisites で封じていた開示が、死を条件に解放される。既存の `DisclosureLedger.TryReveal` に条件判定を1種追加する（コード拡張は最小）。
- 「大義より債務が永続する」「空位は外敵を招く」「冬＝補給切れで旗幟が変わる」を `DisclosureLedger` の lore エントリとして入力。
- 接続：`DisclosureLedger`（FND-4）× `LifecycleRules`（死亡トリガー）× `SupplyRules`（補給×忠誠）。コード新設は最小。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| ドラゴン・魔法・超自然要素 | SF/ファンタジー固有設定。**構造のみ参考（著作権原則）** |
| 個別家系図マイクロ管理 | タイクン化回避。`Person` の家族関係は `MarriageAlliance.ClaimInheritance` で足りる |
| 盛者必衰の物語曲線（一族スケール） | **平家物語 #1313 がカバー済み**。重複新設しない |
| 食糧/農業/冬の気候イベント詳細 | `GovernanceRules`/`ResourceProductionRules` で接続。新 EPIC 化しない（係数で十分） |
| 個人レベルの決闘・試練 | 宮本武蔵 #1372 / 決断システム #502 でカバー |
| 誓約と裏切り（客人の権利） | **PledgeRules（SGZ-3 #1107）がカバー済み** |
| 政略結婚の詳細（持参金・継承権移転） | **MarriageRules/MarriageAlliance（PDX-2 #647）がカバー済み** |
| POV群像劇の脚本演出 | タイクン化回避。高位の決断から創発（DisclosureLedger + 列伝#784 で十分） |

---

## 3. EPIC #SOIF の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UI へ配線。既存継承・財政・政治ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**継承競合・融資政治の構造パターンのみ**参考。

> **EPIC = #2246**。GitHub issue 起票済み（#2247〜#2250）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **SOIF-1** | #2247 | 継承主張者の並立競合（`ClaimantNetworkRules`：正統性スコア×軍事×同盟カスケードで決する） | 新 `ClaimantNetworkRules`。`SuccessionLawRules.SuccessionCrisisRisk` が跳ねた後の競合解決。`LoyaltyRules.ResolveCascade` 入力 |
| **SOIF-2** | #2248 | 融資政治と戦費債務（`DebtLeverageRules`：貸し手が政治的意図で融資先を選択・不払い側の敵を支援） | 新 `DebtLeverageRules`。`BankRules`×`FiscalRules`×`FleetPool`×`DiplomacyRules` |
| **SOIF-3** | #2249 | 継承危機の盤面波及（`ClaimantCrisisRules`：危機継続で地方割拠・外部介入・傭兵コスト増大） | 新 `ClaimantCrisisRules`。`GovernanceRules.RebelPressure`×`DiplomacyRules.TargetOpinion`×`FiscalRules.InterestRate` |
| **SOIF-4** | #2250 | (lore) 死後開示トリガー＋世界観データ（`DisclosureLedger` 拡張＋lore エントリ入力） | `DisclosureLedger`×`LifecycleRules.Kill`。コード拡張は最小 |

### 推奨着手順

`SOIF-1`（継承競合＝最も欠落が大きく独自性高い）→ `SOIF-2`（融資政治＝`BankRules` を政治アクターへ昇格）→ `SOIF-3`（危機波及＝1/2の前提を受け盤面効果を完成）→ `SOIF-4`（lore・コード最小）。

> いずれも既存継承/財政/政治ロジックを**後退させず接続**する additive 設計。
