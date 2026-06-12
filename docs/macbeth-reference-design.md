# シェイクスピア『マクベス』参考設計（EPIC #MACB）

> 参照元：ウィリアム・シェイクスピア作の悲劇『マクベス』（約1606年）。
> スコットランドの武将マクベスが**予言に煽られ主君を殺して王位を奪い、恐怖政治で権力を維持しようとしながら孤立・崩壊していく**悲劇。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略＋既に巨大な政治・権力純ロジック層）にとって**役に立つ視点**だけを抽出し、EPIC `#MACB` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**権力・予言・組織崩壊のメカニクス構造パターンのみ**を参考にする。

---

## 0. なぜ「マクベス」が本システムに役立つか

当プロジェクトは権力・正統性に関する**マクロ純ロジックを大量に保有**している（[CLAUDE.md] 参照）：

| 既存（権力・正統性） | カバー範囲 |
|---|---|
| `CoupRules`（#215-219） | クーデター成功率・粛清・内戦・事後正統性 `PostCoupLegitimacy` |
| `DynastyRules`/`Regime`（#867） | 正統性腐敗・天命喪失・易姓革命 |
| `SecurityRules`/`SecurityApparatus`（#166） | 秘密警察・弾圧・支持ペナルティ |
| `Organization`/`SuccessionRules`（#812/#814） | カリスマ継承・組織崩壊 |
| `CaptivityRules`（#154） | 捕虜化・亡命 `ExileLikelihood` |
| `LoyaltyRules`/`BattleAllegianceRules`（#817/#822） | 旗幟・寝返りカスケード |
| `ConsentRules`/`Polity`（#836） | 合意の剥奪・非協力による統治不能 |
| `DisclosureLedger`（FND-4 #495） | 秘史開示・条件付き真相公開 |

**しかし、これらは「クーデター/弾圧/正統性」という抽象的な状態遷移**であり、マクベスが固有に描く以下が**欠けている**：

| マクベスが固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **予言の罠（条件付き無敵信念→過信→盲点）** | `EventEngine`の`condition`は「現在状態が閾値」だが、**予言を知る者が行動し予言が実現する双方向ループ**・予言への過信による意思決定バイアスが無い |
| **奪権後の恐怖政治ジレンマ**（正統後継者が生存中の奪権） | `CoupRules.PostCoupLegitimacy`はあるが、**正統後継者が生存する間だけ発生する正統性コストの倍増**・恐怖統治と孤立が互いを加速するスパイラルモデルが無い |
| **亡命正統後継者の外国支援獲得と帰還** | `CaptivityRules.ExileLikelihood`の亡命はあるが、**亡命後に外国で正統性主張・同盟獲得・帰還軍形成**する動態が無い |
| **粛清連鎖による組織崩壊のスパイラル** | `SecurityRules.RepressionSupportPenalty`はあるが、**粛清→信頼層縮小→疑心暗鬼→次の粛清→臨界点崩壊**の連鎖モデルが無い |

**結論**：マクベスは当プロジェクトの権力・クーデターシステムに**①予言の自己実現（過信バイアス）②奪権後の恐怖政治スパイラル③亡命後継者の帰還動態④粛清連鎖の臨界点**という4つの欠落軸を与える。最も接続先として効くのは**`CoupRules`（奪権）×`SecurityRules`（恐怖政治）×`DiplomacyRules`（亡命外国支援）**の交差点。

---

## 1. 役に立つ視点（要約）

マクベスの世界観を、**本システムに効く形**で1行ずつ：

1. **「予言は行動を決め、行動が予言を実現させる」**。予言を信じた者は予言に依存し、例外条件への盲点を持つ。→ 現在の`EventEngine`に**予言の自己実現ループ**を足す（予言を知っていると行動バイアスが変わる）。
2. **「力で奪った王座は恐怖でしか守れない」**。正統性がゼロの奪権者は弾圧で権力を維持するしかないが、弾圧が孤立を生み孤立が次の脅威を招く。→ `CoupRules`+`SecurityRules`の**奪権後スパイラルモデル**。
3. **「殺すたびに次も殺さなければならなくなる」**。粛清の連鎖は帰還不能点（臨界点）を持ち、臨界点を越えると崩壊が加速する。→ `Organization.cohesion`に**粛清コスト**を加え、連鎖の臨界点を設定。
4. **「逃げた正統後継者は外から正統性を呼び込む」**。亡命した後継者が外国の支援を受けて帰還すると、奪権者の正統性は外部から完全に否定される。→ `CaptivityRules`の亡命を拡張し**外国支援→帰還軍**の動態を加える。
5. **「権力の腐敗は内側から始まる」**。奪権は勝者ではなく腐敗した者の行動であり、腐敗が組織を内部から崩壊させる。→ `DynastyRules.Regime`の腐敗（制度疲労）モデルとの接続。
6. **（lore）「野心＝最初の罪」の世界観**。正当な手段で得られなかったものを力で得ると、維持コストが正当性コストを超えていく。→ `DisclosureLedger`への秘史データ。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`CoupRules`/`SecurityRules`/`DynastyRules`/`CaptivityRules` を作り直さない**。MACB はそれらに**欠落軸を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・マクベスの signature）

#### MACB 予言メカニクス（条件付き無敵信念と過信バイアス）
- **`ProphecyRules`/`Prophecy`**：予言を「知る」アクターは意思決定バイアスを持つ（特定条件を盲点として扱う）。「女から生まれた者には倒せない」→ 女でない者への防衛を怠る。
- **条件付き予言の自己実現**：予言が行動を決め、行動が予言を実現させる双方向ループ。`DisclosureLedger.TryReveal`の連鎖とは別（プレイヤー/アクターが予言を知っている上で行動する）。
- **予言の例外条件**：予言には「例外（ただし〜の場合は除く）」が必ずあり、その例外が最終的に発動する。アクターが例外に気づくと行動修正できる。
- 接続：`EventEngine`（予言イベント発火）×`PersonRules`（意思決定バイアス）×`DisclosureLedger`（例外条件の開示）。**純ロジック新設、test-first 必須**。

#### MACB 奪権後の恐怖政治ジレンマ（正統後継者生存中の特殊コスト）
- **`UsurpationRules`**：正統後継者（正当な権利保持者）が生存している間、奪権者の`Regime.legitimacy`はフロアを持ち回復不能。
- **恐怖統治トレードオフ**：弾圧（`SecurityRules.DissentSuppression`）を強化するたびに`Organization.cohesion`が下落。弾圧しないと反乱リスクが上昇。奪権者はどちらを選んでも悪化するジレンマ。
- **正統後継者消滅で正統性上限解放**：後継者が死亡/捕虜化されると、ジレンマは解消されるが事後の孤立状態は残る。
- 接続：`CoupRules.PostCoupLegitimacy` → `UsurpationRules`として拡張（`CoupOutcome.usurpation`の判定）×`SecurityRules`×`DynastyRules`。**純ロジック、test-first 必須**。

### ★★ 高（欠落軸に実装価値がある）

#### MACB 亡命正統後継者の外国支援獲得と帰還動態
- **`LegitimateClaimantRules`**：亡命（`CaptivityRules.ExileLikelihood`）した後継者が**外国で正統性主張→同盟締結→帰還軍形成**する動態。
- **外国支援ボーナス**：亡命先が（帝位請求者への）外交支援（`DiplomacyRules.SignTreaty`）を行うと、帰還軍の兵力が`BattleHandoff`に上乗せされる。
- **帰還は通常の侵攻と異なる**：帰還軍は占領地で「解放者」として迎えられ、`GovernanceRules.OnOccupied`の統合コストが低い（占領ではなく奪還）。
- 接続：`CaptivityRules`（亡命）×`DiplomacyRules`（外国支援）×`StrategyRules`（帰還侵攻）×`GovernanceRules`（解放効果）。

#### MACB 粛清連鎖と組織崩壊の臨界点
- **`PurgeSpiralRules`**：粛清1件ごとに`Organization.cohesion`を下落させ、cohesionが閾値を割ると**次の粛清リスクが自動上昇**（疑心暗鬼スパイラル）。
- **臨界点崩壊**：cohesionがゼロになると`SuccessionRules.ResolveSuccession`が失敗し後継者が現れない（組織空洞化）。
- **粛清者の孤立指標**：累積粛清数に比例して`GovernmentRegistry`の有資格者数が減少（粛清した腹心は戻らない）。
- 接続：`SecurityRules`（弾圧）×`Organization`（結束）×`SuccessionRules`（継承失敗）×`GovernmentRegistry`（有資格者数）。**純ロジック、test-first 必須**。

### ★ 中（世界観 lore・接続のみ）

#### MACB（lore）世界観の開示データ
- 「野心で奪った権力は維持コストを際限なく要求する」「予言の例外条件こそが真の結末」「亡命した正統後継者は王国の失われた魂」。
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への**lore データ入力**のみ。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| クーデター本体（成功率・内戦） | **`CoupRules` がカバー**。MACB は奪権後の特殊コストのみ足す |
| 秘密警察・弾圧の基本実装 | **`SecurityRules`/`SecurityApparatus` がカバー**。接続のみ |
| 正統性の一般的な腐敗 | **`DynastyRules.Regime` がカバー**。奪権による特殊コストのみ追加 |
| 亡命の基本実装 | **`CaptivityRules.ExileLikelihood` がカバー**。外国支援獲得のみ追加 |
| 継承一般・組織崩壊一般 | **`SuccessionRules`/`Organization` がカバー**。粛清連鎖の特殊モデルのみ追加 |
| 旗幟・寝返り | **`LoyaltyRules`/`BattleAllegianceRules` がカバー**。不採用 |
| 悲劇のキャラクター心理（罪悪感の UI 表現） | MonoBehaviour/UI 層→Game 層。純ロジックでないため不採用 |

---

## 3. EPIC #MACB の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。既存権力ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #2134**。GitHub issue 起票済み（#2135, #2136, #2140, #2148, #2153）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **MACB-1** | #2135 | `ProphecyRules`/`Prophecy`（予言の自己実現・条件付き無敵信念・過信バイアス） | 新 `ProphecyRules`。`EventEngine`×`PersonRules`×`DisclosureLedger` |
| **MACB-2** | #2136 | `UsurpationRules`（奪権後の恐怖政治ジレンマ・正統後継者生存中の正統性コスト） | `CoupRules.PostCoupLegitimacy` 拡張。`SecurityRules`×`DynastyRules` |
| **MACB-3** | #2140 | `LegitimateClaimantRules`（亡命正統後継者の外国支援獲得・帰還軍・解放効果） | `CaptivityRules`（亡命）拡張。`DiplomacyRules`×`GovernanceRules` |
| **MACB-4** | #2148 | `PurgeSpiralRules`（粛清連鎖と組織崩壊の臨界点・疑心暗鬼スパイラル） | `SecurityRules`×`Organization`×`SuccessionRules`×`GovernmentRegistry` |
| **MACB-5** | #2153 | （lore）世界観の開示データ（予言の罠・奪権者の末路・外部正統性の勝利） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`MACB-1`（予言＝最も固有で欠落の大きい signature）→ `MACB-2`（奪権後スパイラル＝政治エンジンの核）→ `MACB-4`（粛清連鎖＝スパイラルの加速機構）→ `MACB-3`（亡命帰還＝エンドゲームの対抗軸）→ `MACB-5`（lore）。

> いずれも既存の権力・クーデター・組織システムを**後退させず接続**する additive 設計。`CoupRules`（クーデター）×`SecurityRules`（恐怖政治）×`DiplomacyRules`（亡命外国支援）の交差点に最も効く。
