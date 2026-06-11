# タックマン『八月の砲声』参考設計（EPIC #GUN）

> 参照元：バーバラ・タックマン『八月の砲声』（The Guns of August, 1962）。
> 1914年の開戦過程——動員計画の自動性・同盟連鎖・外交的誤算——を描いたノンフィクション。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略）にとって**役に立つ視点**だけを抽出し、EPIC `#GUN` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**政治・外交・動員のメカニズム構造のみ**を参考にする。

---

## 0. なぜ「八月の砲声」が本システムに役立つか

当プロジェクトは外交と戦争の**手続き**を大量に保有している：

| 既存（カバー範囲） | 対応モジュール |
|---|---|
| 外交状態（平時/同盟/不可侵/属国/交戦）と opinion | `DiplomacyRules`/`DiplomacyState`（DIP-1 #189） |
| 条約効果・条約破棄コスト・leverage | `TreatyRules`/`Treaty`（DIP-2 #191） |
| 厭戦・戦争目標の正統性・講和受諾 | `WarGoalRules`/`CasusBelli`（DIP-3 #192） |
| 作戦の政治目標への接続・動員上限スケーラ | `CLZ-2` 政戦連接（CLZ #1133） |
| 作戦実行の摩擦係数（命令→実態の乖離） | `FrictionRules`（CLZ-1 #1133） |
| 個人の旗幟変更（関ヶ原型カスケード） | `LoyaltyRules`/`BattleAllegianceRules`（#817） |
| 諜報・偽情報・情報優位 | `EspionageRules`（#166類似）, `DeceptionRules`（SUN-1） |
| 攻勢終末点（補給距離→戦力効率低下） | `CulminatingPointRules`（SUN-4） |

**しかし、これらは外交を「状態遷移」として、戦争を「バトル解決」として扱う**。
タックマンが描く「開戦の構造的必然性」という視点が**欠けている**：

| 八月の砲声が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **段階的動員のナッシュ均衡トラップ**：相手が動員したら自分も動員しなければ不利＝双方が全面動員に引き込まれる | `DeclareWar` はゼロ秒で宣戦。**動員の段階性・逆転コスト・相手への圧力波及**が無い |
| **後戻り不能点（列車は止められない）**：全面動員に達すると計画実行が政治意思を凌駕し開戦が自動化される | CLZ-2 の動員上限は「量の上限」。**動員水準が閾値を超えると宣戦が事実上確定する**「不可逆性」が無い |
| **同盟連鎖の自動参戦**：AがBを攻撃するとBの全同盟国が引き込まれ、その同盟国のさらなる同盟がと連鎖する | `TreatyRules.Leverage` は1対1の条約コスト。**宣戦→同盟の再帰的引き込み**（戦争が自己拡大する）が無い |
| **作戦計画の硬直性が外交選択肢を消す**：シュリーフェン計画を発動すると中立国侵犯が不可避となり英仏参戦が確定 | `FrictionRules` は「計画通りにいかない」。**計画発動が逆に特定外交オプションを封じる**（計画が政治を拘束）が無い |
| **戦争期間の過少見積もり**：全員が「クリスマスまでに帰れる」と思って突入し、長期消耗に誰も備えていなかった | `WarWeariness` は消耗の蓄積。**期待と実態の乖離**が厭戦を「倍速」で加速させるメカニズムが無い |

**結論**：八月の砲声は当プロジェクトの外交システムに**「戦争は決断でなく構造が起こす」という視点**と、
**①動員段階性 ②同盟連鎖 ③計画硬直 ④不可逆点 ⑤期間過少見積もり**という5つの欠落軸を与える。
既存の DIP-1〜3/CLZ を**後退させず接続するだけ（additive）**。

---

## 1. 役に立つ視点（要約）

八月の砲声の世界観を、**本システムに効く形**で1行ずつ：

1. **動員は「決断」でなく「反応」になる**。相手が動員したら自分も動員せざるを得ない——ナッシュ均衡トラップが相互確証動員を引き起こす。→ 外交AIの選択肢設計に直結。
2. **後戻り不能点は存在する**。「全面動員=開戦」という閾値を越えると、政治指導者が止めようとしても計画が走り出す。→ `StrategicFleet`/`FleetPool` の動員状態に「不可逆ゾーン」を与える。
3. **同盟は戦争を「局地戦→世界大戦」に自動拡大させる**。一国の行動が連鎖的に別国を引き込む——意図しなかった広域戦争の構造。→ `DiplomacyState.alliances` を「引き込み計算」に使う。
4. **軍事計画が外交空間を食いつぶす**。シュリーフェン計画＝柔軟性ゼロの計画が英仏参戦を確定させた。→ `OrderOfBattle` の作戦ドクトリンが外交オプションを閉じるメカニズム。
5. **短期戦想定が消耗戦を悪化させる**。「6週間で終わる」という計画が、長期消耗に対する備えを根絶した。→ `WarGoalRules.WarWeariness` に「期待乖離倍率」を足す。
6. **戦争の原因は英雄でも悪人でもなく「システム」だった**。どの指導者も止めたかったが止められなかった。→ 世界観EPICとして「構造決定論vs個人意思」の問い（`DisclosureLedger` lore）。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**DIP-1/2/3・CLZ-1/2・SUN-1/4 を作り直さない**。GUN はそれらに**欠落軸を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・八月の砲声の signature）

#### GUN 段階的動員メカニズム（`MobilizationRules` / `MobilizationState`）
- **動員水準**：`MobilizationLevel`（0=平時 / 1=予備動員 / 2=部分動員 / 3=全面動員）を勢力ごとに保持。
- **逆転コストの非対称性**：`MobilizeCost(level, direction)`——上げるコスト < 下げるコスト（逆転は準備の無駄・信用喪失）。
- **相手圧力 `MatchingPressure(myLevel, opponentLevel)`**：敵が水準を上げると自陣営の「動員圧力スコア」が上昇し、AI/プレイヤーに動員を迫る。
- **不可逆点 `IsPointOfNoReturn(state)`**：全面動員（Lv3）に達すると `WarGoalRules.PeaceAcceptance` が激減し、事実上の開戦確定。
- 接続：`FleetPool`（兵力の動員上限）× `DiplomacyRules.DeclareWar`（不可逆点で自動宣戦 or 強制分岐）× `WarGoalRules`（CLZ-2の動員スケーラに水準を入力）。
- 純ロジック test-first。

#### GUN 同盟連鎖自動参戦（`AllianceCascadeRules`）
- **戦争拡大計算 `ExpandWar(belligerents, diplomacy)`**：現在の交戦国セットから各勢力の同盟（`DiplomacyState.alliances`）を再帰的にたどり、参戦 or 条約破棄を `ObligationCost` で比較して拡大を計算。
- **連鎖深度 `CascadeDepth`**：第1次連鎖（直接同盟）/ 第2次連鎖（同盟の同盟）。深度が深いほど引き込みコスト逓増（遠い同盟は義務感薄い）。
- **参戦vs破棄トレードオフ `ObligationCost(treaty, faction)`**：条約破棄コスト（`TreatyRules.Leverage` の既存値）と参戦コスト（消耗予測）を比較——小国は強制的に引き込まれやすい。
- 接続：`DiplomacyState`（既存同盟情報）× `TreatyRules.Leverage`（破棄コスト）× `FactionRelations.IsHostile`（交戦判定更新）。
- 純ロジック test-first。

### ★★ 高（計画の硬直性と戦争拡大の構造）

#### GUN 作戦計画の硬直性（`OperationalPlanRules`）
- **ドクトリン `OperationalDoctrine`**：`FleetRoster`/`OrderOfBattle` に紐づく作戦計画。`IsActivated`（発動済みか）を持つ。
- **発動による外交コスト増 `DiplomaticRestriction(doctrine, action)`**：特定の外交行動（講和打診・第三国への不侵犯条約締結など）が計画発動中はコスト増。計画が政治空間を食いつぶす。
- **逸脱コスト `DeviationCost`**：計画通りに動かないと `FrictionRules.FrictionModifier` に加算（作戦上の非効率）。
- 接続：`OrderOfBattle`/`CommandStaffRules`（計画の保持場所）× `DiplomacyRules`（コスト増の注入先）× `FrictionRules`（逸脱ペナルティ）。CLZ-1 の摩擦と**別系統**（CLZ=実行ギャップ、GUN=政治選択肢の消失）。
- 純ロジック test-first。

#### GUN 戦争期間の過少見積もりと長期化ペナルティ（`WarScopeRules`）
- **期待継続期間 `ExpectedDuration`**：宣戦時の `WarGoalRules` が推定する終戦見込み（動員コスト・相手兵力から計算）。
- **乖離倍率 `ScopeSurpriseFactor(actual, expected)`**：実際の経過 game-time が期待を超えると、`WarWeariness` の蓄積が加速（`WarGoalRules.WarWeariness` への乗数）。
- **短期戦想定インセンティブのトレードオフ**：低コスト開戦は短い期待継続期間→乖離リスク大。長期戦備えは初期コスト大だが乖離リスク小。
- 接続：`WarGoalRules.WarWeariness`（乗数入力）× `FiscalRules`（財政コストの遡及的加算）× `GameClock.elapsedSeconds`（実経過時間参照）。
- 純ロジック test-first。

### ★ 中（盤面への配線・世界観lore）

#### GUN 動員・連鎖の戦略マップ配線
- `GalaxyView` の戦略ループに `MobilizationState` 表示（勢力ごとの動員水準インジケータ）と、`AllianceCascadeRules.ExpandWar` を `StrategyRules` の宣戦トリガーに接続。
- 純ロジック（GUN-1/2）が完成後の盤面配線。

#### GUN（lore）世界観の開示データ
- 「戦争は決断でなく構造が起こした」「誰も止めたかったが誰も止められなかった」。
- コード新設なし。`DisclosureLedger`（FND-4）への**lore データ入力**。

### ❌ 不採用（重複・既存で十分・タイクン化）

| 不採用 | 理由 |
|---|---|
| 動員の戦術マイクロ（鉄道時刻表・輸送能力） | タイクン化回避。`SupplyRules`/`SCM` で十分 |
| 個別指揮官の性格欠陥シミュ（ モルトケの優柔不断等） | `PersonRules.Effectiveness`/`CounselIntegrityRules`（MKV-3）がカバー |
| 攻勢終末点 | **SUN-4 `CulminatingPointRules` がカバー**（GUN は作らない） |
| 戦場の霧（tactical fog） | **SUN-1 `DeceptionRules` がカバー** |
| 個別条約の外交文書生成 | `TreatyRules`（DIP-2）がカバー |
| 厭戦そのもの | `WarGoalRules.WarWeariness`（DIP-3）がカバー。GUN は**乗数のみ追加** |
| 意図的な偽情報・欺瞞作戦 | **SUN-1 `DeceptionRules` がカバー**。GUN の誤認は「構造的誤認」＝諜報精度の問題（`EspionageRules` 接続で十分） |

---

## 3. EPIC #GUN の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面配線。DIP-1/2/3・CLZ-1/2 は**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #1314**。GitHub issue 起票済み（#1316〜#1326）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **GUN-1** | #1316 | 段階的動員メカニズム（`MobilizationRules`/`MobilizationState`・逆転コスト非対称・相手圧力） | 純ロジック。`FleetPool`×`DiplomacyRules`×`WarGoalRules`（CLZ-2入力） |
| **GUN-2** | #1318 | 同盟連鎖自動参戦（`AllianceCascadeRules`・`ExpandWar` 再帰・参戦 vs 条約破棄コスト） | 純ロジック。`DiplomacyState.alliances`×`TreatyRules.Leverage`×`FactionRelations.IsHostile` |
| **GUN-3** | #1320 | 作戦計画の硬直性（`OperationalPlanRules`・発動→外交コスト増・逸脱→摩擦加算） | 純ロジック。`OrderOfBattle`×`DiplomacyRules`×`FrictionRules`（CLZ-1拡張） |
| **GUN-4** | #1323 | 戦争期間の過少見積もり（`WarScopeRules`・乖離倍率→厭戦加速・短期戦インセンティブのトレードオフ） | 純ロジック。`WarGoalRules.WarWeariness`×`FiscalRules`×`GameClock` |
| **GUN-5** | #1326 | 動員・連鎖の戦略マップ配線（動員水準 UI・`AllianceCascadeRules` → `StrategyRules` 宣戦トリガー） | Game 層配線。GUN-1/2 完了後。`GalaxyView`×`NotificationCenter` |

### 推奨着手順

`GUN-1`（段階的動員＝最も固有で欠落の大きい signature）→ `GUN-2`（同盟連鎖＝戦争拡大エンジン）→ `GUN-3`（計画硬直性＝外交への逆流）→ `GUN-4`（期間過少見積もり＝厭戦加速）→ `GUN-5`（盤面配線）。

> いずれも既存 DIP/CLZ を**後退させず接続**する additive 設計。銀英伝の「**星の海の政治劇**」に「構造的開戦メカニズム」という重みを与える。
