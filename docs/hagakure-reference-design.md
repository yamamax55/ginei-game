# 『葉隠』参考設計（EPIC #HAGK）

> 参照元：山本常朝口述・田代陣基筆録『葉隠』（江戸中期成立）。肥前鍋島藩の武士道書。
> 「武士道とは死ぬことと見つけたり」——死の受容を前提とした義的忠誠・殉死・不動心を描く武士道の古典。
> 本ドキュメントは、当プロジェクト（Ginei＝忠誠・士気・組織存続の純ロジック層を大量保有）にとって**役に立つ視点**だけを抽出し、EPIC `#HAGK` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**武士道メカニクス／世界観の構造パターンのみ**を参考にする。

---

## 0. なぜ「葉隠」が本システムに役立つか

当プロジェクトは忠誠・士気・組織存続の**マクロ純ロジックを大量に保有**している（[CLAUDE.md] 参照）：

| 既存（忠誠・士気・群像） | カバー範囲 |
|---|---|
| `LoyaltyRules/Allegiance`（#817） | 計算的忠誠（利益比較→戦う/静観/寝返り）・カスケード |
| `BattleAllegianceRules` | 会戦中の旗幟遷移・寝返りロック |
| `FleetMorale` | 士気（低下→敗走・`IsRouted`）・`GetMoraleFactor()` |
| `FocusRules`（#872） | 身・口・意の同期→瞬間的最大出力（空海モデル） |
| `Organization/SuccessionRules`（#812） | カリスマ死後の組織存続・制度化投資 |
| `GrowthRules`（#537） | 提督成長・経験→実効能力 |
| `AdmiralData.staffOfficers` | 参謀ボーナス（能力底上げ） |
| `CaptivityRules`（#154） | 捕虜化・処断・登用・`DefaultDisposition` |
| JGS-1 納諫率（#1227） | 君主の受容性→政策改善（需要側） |
| MKV-3 `CounselIntegrityRules`（計画中） | 政治顧問の誠実性→政策精度（供給側） |
| `FactionLoyaltyRules` | 国家状態→忠誠基準値（腐敗が寝返りを生む） |

**しかし、これらは「計算・均衡・合理性」という枠組みで忠誠を解く**。葉隠が固有に描く以下の視点が**欠けている**：

| 葉隠が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **死狂い（死の受容→恐怖除去→絶体絶命での戦闘力維持）** | `FleetMorale.IsRouted` は士気ゼロ=敗走一択。「死を受け入れた戦士は退かない」回路が無い |
| **義的忠誠（計算でなく同一性・文化としての忠誠）** | `LoyaltyRules` は明示的に計算モデル。「裏切りは自己否定」という質的に異なる忠誠構造が無い |
| **殉死（主君死亡時に自己犠牲を選ぶ行動モデル）** | `VacancyRules` は空席補充のみ。主君の死に殉じる（または後退援護で艦隊を犠牲にする）選択肢が無い |
| **不動心（武士道エトスによる逆境耐性・内的動揺の抑制）** | `FleetMorale`/`FactionState.Stability` は全文化圏が同一曲線で揺れる。武士道文化の逆境耐性モデルが無い |

**結論**：葉隠は当プロジェクトの忠誠・士気・群像システムに**「義的忠誠」「死の受容（死狂い）」「殉死」「不動心」という武士道文化4欠落軸**を additive に供給する。接続の核は `LoyaltyRules`×`FleetMorale`×`FleetStrength.BeginRetreat`×`FactionData`。

---

## 1. 役に立つ視点（要約）

葉隠の世界観を、**本システムに効く形**で1行ずつ：

1. **「武士道とは死ぬことと見つけたり」＝死の受容が戦闘の前提** → 死を受け入れた提督・艦隊は士気ゼロでも退かない「死狂い」状態へ入る。士気と直交する新軸。
2. **義的忠誠 vs 利的忠誠** → `LoyaltyRules` の計算モデルと並立する、同一性・文化に根ざした忠誠係数。武士道エトスを持つ勢力は壊れにくいが、一旦壊れると回復しない（浪人化）。
3. **殉死（主君に殉じる）** → 主君の死に際して、武士道エトスの強い副将・参謀が「残って戦い続ける」「後退援護として艦隊を犠牲にする」を選ぶ行動モデル。`VacancyRules` の補充に殉死行動分岐を足す。
4. **不動心（逆境に揺さぶられない）** → 武士道エトス係数が高い勢力は、敗戦・喪失・士気低下イベントへの感応係数が低い。創発的安定性。
5. **陰での奉公（見えない忠義）** → 認識を求めない奉公こそ真の武士道。評価されない隠れた有効性を持つ。→ lore に回す（参謀ボーナスが既にカバー）。
6. **「武士は一言」＝約束の絶対性** → 武士道エトスを持つ勢力の外交約束は破りにくい（`DiplomacyRules` 接続・lore）。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`LoyaltyRules`/`FleetMorale`/`Organization` を作り直さない**。HAGK はそれらに**欠落軸を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・葉隠の signature）

#### HAGK 死狂い（`DeathResolveRules`）
- **死の受容状態**：士気ゼロ到達時、通常は敗走（`FleetMorale.IsRouted`→`BeginRetreat`）。しかし武士道エトスを持つ提督/艦隊は「死狂い」状態に入り、**最小限の戦闘力（`deathResolveCombatFactor` 既定0.7）を維持して戦い続ける**。
- **実効値パターン**：基準能力は変えず、死狂い係数を実効値計算に組み込む（`ShipCombat.ComputeDamage` ×係数）。
- **発動条件**：`FleetMorale.IsRouted` AND `HasWarriorEthos`（HAGK-2 参照）AND 残存兵力 > 閾値。発動すると `IsInDeathResolve` フラグ、敵の最強部隊へ自動的に向かう。
- 接続：`FleetMorale`（IsRouted 割り込み）×`FleetStrength.BeginRetreat`（抑制）×`ShipCombat.ComputeDamage`（係数）×`FleetAI`（行動変化）。
- 純ロジック新設：`DeathResolveRules`/`DeathResolveState`（Core・test-first）。

#### HAGK 武士道エトス（`WarriorEthosRules`）
- **エトスマーカー**：`FactionData` に `martialEthos`（0..1、既定0）＋`AdmiralData` に `deathResolveProne`（true/false）を足す。
- **義的忠誠モデル**：`WarriorEthosRules.EthosLoyaltyModifier`：エトスが高いほど忠誠基準値が高いが（義的忠誠）、一度裏切られると回復しない（`LoyaltyRules.BaselineLoyalty` 修正子）。閾値型：高エトス→高デフォルト/裏切り時の壊滅的低下を `BattleAllegianceRules` へ伝達。
- **死狂い・殉死のゲート**：`WarriorEthosRules.CanEnterDeathResolve`/`CanPerformJunshi`（エトス値 ≥ 閾値 AND 条件充足）。
- 接続：`FactionData`（フィールド追加）×`LoyaltyRules.BaselineLoyalty`×`FactionLoyaltyRules`×`BattleAllegianceRules`×HAGK-1×HAGK-3。
- 純ロジック新設：`WarriorEthosRules`（Core・test-first）。

### ★★ 高（義的忠誠の行動的帰結）

#### HAGK 殉死（`JunshiRules`）
- **主君死亡→殉死判定**：旗艦が退却（`BeginRetreat`）または喪失したとき、副提督・参謀（`FleetUnitData.viceCommander`/`chiefOfStaff`）の `martialEthos` と `deathResolveProne` により**「後退援護殉死」**を判定：自艦隊を敵最大集団に特攻させ、撤退部隊の逃走経路を開く（`SetReverseDestination` で主要退路方向への移動＋自分は反転攻撃）。
- **実効値パターン**：殉死は基準の loyalty/strength フィールドを書き換えず、`JunshiRules.ShouldPerformJunshi(admiral, ethosLevel, roll)` が boolean を返す（決定論的 roll を受け取る設計）。
- **会戦終了後**：殉死した艦隊は `FleetStrength.BeginRetreat(withEffects:true)` で最後の演出を出したのち退場。
- 接続：`VacancyRules`（主君死亡検知）×`CommandStaffRules`（副提督/参謀）×`FleetMovement.SetReverseDestination`×`BattleManager`（会戦結果記録）。
- 純ロジック新設：`JunshiRules`（Core・test-first）。

### ★ 中（武士道エトスの逆境耐性・世界観lore）

#### HAGK 不動心（`EquanimityRules`）
- **逆境耐性係数**：武士道エトス係数が高い勢力は、負の事象（敗戦・旗艦退却・被占領）による `FleetMorale` 低下量・`FactionState.Stability` 変動量を `EquanimityRules.DampeningFactor(martialEthos)` で減衰させる（`MoveTowards` より遅い降下曲線）。
- **正の事象への鈍感さ**：逆境耐性は「正の出来事にも鈍感」という対称性を持つ（武士道的寡黙＝喜ばない）。`EquanimityRules.UpsideDampening` で対称実装。
- 接続：`FleetMorale`（低下量修正）×`FactionState`（安定度変動修正）×`WarriorEthosRules`（martialEthos 参照）×`EventEngine`（負の事象フック）。
- 純ロジック新設：`EquanimityRules`（Core・test-first）。

#### HAGK（lore）世界観の開示データ
- 「義的忠誠は計算を超える」「死を前提とした行動の誠実性」「殉死の美学と組織の損失」「不動心と創造的逆境への変換」。
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への**loreデータ入力**。CCX-6（世界観codex退避）方針に一貫。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 諫言ロジック（直言→受容/拒絶→帰結） | **JGS-1（納諫率）がカバー**（需要側）・**MKV-3（CounselIntegrityRules）がカバー**（供給側） |
| 創業守成フェーズ遷移 | **JGS-2 がカバー** |
| 個人の成長・修行曲線 | **`GrowthRules`（#537）がカバー** |
| 武士の技法・拍子・間合い | **宮本武蔵五輪書（#1372）がカバー** |
| 身体の同期・武・禅 | **`FocusRules`（#872・空海三密）がカバー** |
| 武家の相続・家督 | **`SuccessionLawRules`（#646）がカバー** |
| 武士コードの外交制約 | **DIP-2/DIP-3 が接続先**・義約束はlore |
| 階級・序列システム | **`RankSystem`/`SeniorityRules` がカバー** |

---

## 3. EPIC #HAGK の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→盤面/UIへ配線。既存忠誠・士気ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **HAGK-1** | #1926 | 死狂い（`DeathResolveRules`）— 死の受容→絶体絶命での戦闘力維持 | 新 `DeathResolveRules`/`DeathResolveState`。`FleetMorale.IsRouted` 割り込み×`ShipCombat.ComputeDamage` 係数 |
| **HAGK-2** | #1928 | 武士道エトス（`WarriorEthosRules`）— 義的忠誠・文化係数・エトスマーカー | 新 `WarriorEthosRules`。`FactionData.martialEthos`×`LoyaltyRules.BaselineLoyalty`×`BattleAllegianceRules` |
| **HAGK-3** | #1930 | 殉死（`JunshiRules`）— 主君退却/喪失時の自己犠牲・後退援護特攻 | 新 `JunshiRules`。`VacancyRules` 主君死亡検知×`CommandStaffRules`×`FleetMovement` |
| **HAGK-4** | #1931 | 不動心（`EquanimityRules`）— 武士道エトスによる逆境耐性係数 | 新 `EquanimityRules`。`FleetMorale`/`FactionState.Stability` の変動減衰×`EventEngine` 負フック |
| **HAGK-5** | #1932 | （lore）世界観の開示データ（義的忠誠・殉死の美学・不動心と創造的逆境） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`HAGK-2 → HAGK-1`（エトスマーカーが基盤→死狂いはその上に乗る）→ `HAGK-3`（殉死＝主君死亡時の義的行動で最も物語的）→ `HAGK-4`（不動心＝逆境耐性で最も広範に効く）→ `HAGK-5`（lore）。

> いずれも既存忠誠・士気・組織システムを**後退させず接続**する additive 設計。武士道エトスを持つ勢力（接続：`FactionData.martialEthos`）に最も効く。
