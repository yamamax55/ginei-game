---
type: reference
tags: [reference]
---

# ギレンの野望 参考設計（EPIC #GIR）

> 参照元：『機動戦士ガンダム ギレンの野望』シリーズ（バンダイ）。兵器の開発→生産、将官の配属と部隊編成、指揮影響圏、拠点制圧、士気/兵站、プロパガンダ、戦略兵器（コロニー落とし級）と複数勢力の総力戦を回す戦略シミュレーション。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略＋戦術艦隊戦）にとって**移植して映える「いいところ」だけ**を抽出し、EPIC `#GIR` として issue 化する提案。
> 著作権注意：固有名・モビルスーツ・キャラクター・文章・固有設定は流用せず、**戦略メカニクス／総力戦の構造パターンのみ**を参考にする。

---

## 0. なぜ「ギレンの野望」が本システムに役立つか

当プロジェクトは戦略・会戦の**純ロジックを大量に保有**している（[CLAUDE.md] 参照）。ギレンの野望が描く要素の多くは**既にカバー済み**：

| 既存（カバー範囲） | ギレンの野望の該当要素 |
|---|---|
| `TechCatalog`/`TechTreeRules`/`ResearchRules`/`ArmamentDesignRules`/`WeaponsRules` | 兵器の研究開発ツリー・性能設計 |
| `ShipyardRules`/`BuildOrder`/`FleetCapRules` | 生産・建艦キュー・**戦力上限** |
| `FleetRoster`/`OrderOfBattle`/`CommandStaffRules` | 部隊編成・将官配属・指揮班 |
| `AtrocityRules`/`WarCrimesRules`/`TribunalRules`/`ImperialBlowbackRules` | 残虐行為の反動・戦犯・法廷 |
| `PlanetSiegeRules`/`GovernanceRules`/`Occupation` | 拠点（コロニー/地球エリア）制圧・占領統治 |
| `DiplomacyRules`/`PoliticsState`/`WarWearinessModifiersRules`/`FreePressRules` | 外交・世論・厭戦・報道 |
| `CaptivityRules`/`PersonFate`/寝返り#817/`EventEngine` | 捕虜・亡命・寝返り・イベント分岐 |
| `isTranscendent`/`TenchijinRules`/`TalentCatalog` | ニュータイプ級の傑出した個（静的） |
| `BoardingActionRules`/`TechDiffusionRules` | 拿捕・技術伝播 |

**しかし、ギレンの野望が固有に持つ以下が欠けている**：

| ギレンの野望が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **指揮影響圏（将官の指揮アウラ）** | 指揮班/指揮容量（`CommandStaffRules`）はあるが、**戦場で将官の周囲の友軍を階級/統率に応じて強化する空間的アウラ**が無い |
| **覚醒（凡将が戦いの中で限界突破＝NTフラッシュ）** | `isTranscendent` は**データ固定フラグ**＝`TenchijinRules` は覚醒**後**の成長のみ。**武勲/練度の閾値を超えて isTranscendent を獲得する過程**が無い |
| **戦略兵器（コロニー落とし級の広域殲滅）** | 戦術超兵器（`FortressMainCannon`）はあるが、**戦略マップで星系規模を一撃で殲滅する兵器の「使用という決断」**が無い（反動の受け皿＝`AtrocityRules` 等は既存） |
| **鹵獲→敵性技術の解析（リバースエンジニアリング）** | 拿捕（`BoardingActionRules`）・技術伝播（`TechDiffusionRules`）はあるが、**鹵獲が自軍の研究を加速する**経路が薄い |
| **兵器系統の開発分岐（艦系統樹＝伸ばす系統の機会費用）** | 技術ツリー（`TechTreeRules`）はあるが、**艦の世代系統（系譜）として伸ばす/捨てる選択**のメタが薄い |
| **指導者の演説・プロパガンダ作戦（一手の戦略アクション）** | 報道/厭戦/支持はあるが、**指導者が一手で全軍士気/徴募/敵厭戦に作用させる能動アクション**が無い |

**結論**：ギレンの野望は当プロジェクトに **①指揮影響圏（将官の存在が戦場を変える）②覚醒（傑物は生まれるだけでなく戦いの中で生まれる）**という2つの強い欠落軸を与え、さらに戦略兵器の決断・鹵獲研究・艦系統・演説で総力戦のテクスチャを供給する。いずれも既存（`CommandStaffRules`/`TenchijinRules`/`AtrocityRules`/`TechTreeRules`/`FreePressRules`）を**作り直さず接続・拡張**する additive。

---

## 1. 役に立つ視点（要約）

ギレンの野望の総力戦観を、**本システムに効く形**で1行ずつ：

1. **将官は点でなく面で効く**。名将が戦線にいるだけで周囲の凡庸な部隊が強くなる。→ 銀英伝の「ヤンがいる戦線」の手応え。`CommandStaffRules` の指揮容量に**空間的アウラ**を足す。
2. **傑物は生まれるだけでなく、戦いの中で覚醒する**。無名の士官が修羅場で開眼し、軍神/天才の域へ。→ `isTranscendent` を**獲得しうる**ものにし、`VeterancyRules`/武勲に劇的な到達点を与える。
3. **大量破壊は勝利を買うが、魂を売る**。広域殲滅兵器は戦況を変えるが、残虐の反動が国を蝕む。→ 戦略兵器の**使用という決断**を既存の `AtrocityRules`/`ImperialBlowbackRules` に接続。
4. **奪った敵兵器は次の世代の糧**。鹵獲機の解析が開発を飛躍させる。→ 拿捕（`BoardingActionRules`）に**研究加速**のペイオフを与える。
5. **何を作り、何を捨てるか**。限られた国力で艦系統のどれを伸ばすかが国の形を決める。→ 技術ツリーに**艦系統（系譜）の機会費用**を足す。
6. **言葉は兵器**。指導者の演説が全軍を奮い立たせ、敵を厭戦へ追い込む。→ 報道/厭戦/支持に**能動の一手**を足す。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`CommandStaffRules`／`TenchijinRules`／`VeterancyRules`／`AtrocityRules`＋`WarCrimesRules`＋`ImperialBlowbackRules`／`TechTreeRules`／`FleetCapRules`／`DiplomacyRules`/`FreePressRules` を作り直さない**。GIR はそれらに**欠落軸を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・ギレンの野望の signature）

#### GIR 指揮影響圏（将官の指揮アウラ）
- 将官（士官級以上）が戦場で**指揮影響圏**を張り、圏内の友軍部隊の能力（攻撃/命中/士気維持）を**階級・実効統率に応じて**底上げ。圏の半径は階級で決まり、重複は上位優先。
- 接続：新 `CommandAuraRules`（Core・純ロジック・test-first）＝半径/倍率/重複優先を `RankSystem`×`EffectiveLeadership` から算出。会戦 Game 層が `FleetRegistry`×`CombatModifiers`/`ModifierStack` で実効値に反映。`CommandStaffRules`（指揮容量）とは別軸（空間的バフ）。

#### GIR 覚醒（武勲/練度の閾値を超えた将が限界突破＝isTranscendent を獲得）
- 凡将でも、**武勲・練度・修羅場（劣勢での殊勲等）が閾値を超える**と確率的に**覚醒**し、限界突破（`isTranscendent`）の域へ到達＝以後 `TenchijinRules` の青天井成長に乗る。
- 接続：新 `AwakeningRules`（Core・決定論 roll）＝`VeterancyRules`（練度XP）×`MeritRecordRules`（武勲・TKO-2）×劣勢度 から覚醒確率を出し、`AdmiralData.isTranscendent` を**獲得**させる窓口。`TenchijinRules` は覚醒**後**を担う（二重実装しない）。

### ★★ 高（総力戦のテクスチャ）

#### GIR 戦略兵器と広域殲滅（コロニー落とし級の「使用という決断」）
- 戦略マップで**星系規模を一撃で殲滅/制圧**する戦略兵器。使えば戦況を覆すが、**使用が既存の残虐反動を誘発**（支持低下・厭戦・外交悪化・戦犯訴追）。
- 接続：新 `StrategicWeaponRules`（Core・効果量とコスト/チャージ）＝**使用の判定と戦果**のみ新設。反動は **既存の `AtrocityRules`/`WarCrimesRules`/`ImperialBlowbackRules`/`WarWearinessModifiersRules`/支持#113 へ接続**（反動は二重実装しない）。戦術超兵器 `FortressMainCannon`#77 の戦略版。

#### GIR 鹵獲と敵性技術の解析（拿捕→リバースエンジニアリング→開発加速）
- 敵艦の**拿捕**が自軍の**研究を加速**（敵性技術の解析）。鹵獲した系統の技術前提を一部充足／研究力にボーナス。
- 接続：新 `CapturedTechRules`（Core）＝拿捕量×敵技術水準→研究加速量。`BoardingActionRules`（拿捕）×`TechDiffusionRules`（伝播）×`ResearchRules`/`TechTreeRules`（解禁）×`CaptivityRules` に接続。

### ★ 中（開発の選択・言葉の力・lore）

#### GIR 兵器系統の開発分岐（艦系統樹＝伸ばす系統の機会費用）
- 艦を**世代系統（系譜）**として捉え、限られた国力でどの系統を伸ばす/捨てるかの**機会費用**を作る。
- 接続：新 `ShipLineageRules`（Core）＝系統ノードの世代強化と排他コスト。`TechTreeRules`（作り直さない）×`ShipClass`#80×`HeritageShipNames`/`ShipNameRegistry`×`ShipyardRules` に接続。

#### GIR 指導者の演説・プロパガンダ作戦（一手の戦略アクション）
- 指導者が**演説**を一手として打ち、**全軍士気/徴募/敵厭戦/支持**に作用（成功度は能力・知力・正統性依存）。
- 接続：新 `WarSpeechRules`（Core・決定論 roll）＝演説の効果量。`FreePressRules`×`WarWearinessModifiersRules`×支持#113×3チャネル#908×`NotificationCenter` に接続。

#### GIR（lore）総力戦の開示データ
- 「総力戦は国民全員を動員する」「指導者の野望と国の運命」「大量破壊の倫理」。
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への lore データ入力。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 兵器の研究開発→生産パイプライン本体 | **`TechCatalog`/`TechTreeRules`/`ResearchRules`/`ArmamentDesignRules`/`WeaponsRules`/`ShipyardRules` が既存**（GIR-5 は艦系統メタを足すだけ） |
| 戦力上限・部隊数キャップ | **`FleetCapRules` が既存** |
| 残虐行為の反動・戦犯・法廷 | **`AtrocityRules`/`WarCrimesRules`/`TribunalRules`/`ImperialBlowbackRules` が既存**（GIR-3 は戦略兵器の使用でこれらを**誘発**するだけ） |
| 捕虜・亡命・寝返り・イベント分岐 | **`CaptivityRules`/`PersonFate`/寝返り#817/`EventEngine` が既存** |
| 外交・世論・厭戦・自由報道 | **`DiplomacyRules`/`PoliticsState`/`WarWearinessModifiersRules`/`FreePressRules` が既存**（GIR-6 は演説アクションを足すだけ） |
| 傑出した個（静的な強さ） | **`isTranscendent`/`TenchijinRules`/`TalentCatalog` が既存**（GIR-2 は「獲得＝覚醒」過程を足す） |
| 拠点制圧・占領統治 | **`PlanetSiegeRules`/`GovernanceRules`/`Occupation` が既存** |
| ターン制/ヘックス/原作キャラ・機体固有名 | 本作はリアルタイム星間戦略＝合わない／著作権で固有名は不使用 |

---

## 3. EPIC #GIR の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面（会戦/戦略）へ配線。既存ロジックは**接続・拡張のみ・重複新設しない**。
> 著作権注意：固有名・機体・キャラは不使用、**戦略メカニクス/総力戦構造のみ**参考。

> **EPIC = #2730**。GitHub issue 起票済み（#2731〜#2737）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **GIR-1** | #2731 | 指揮影響圏（将官の指揮アウラ＝圏内の友軍を階級/統率で強化） | 新 `CommandAuraRules`。`RankSystem`×`EffectiveLeadership`×`CombatModifiers`／`CommandStaffRules` と別軸 |
| **GIR-2** | #2732 | 覚醒（武勲/練度/修羅場の閾値超えで limit-break＝isTranscendent を獲得） | 新 `AwakeningRules`。`VeterancyRules`×`MeritRecordRules`(TKO-2)→`AdmiralData.isTranscendent`／覚醒後は `TenchijinRules` |
| **GIR-3** | #2733 | 戦略兵器と広域殲滅（コロニー落とし級の「使用という決断」） | 新 `StrategicWeaponRules`（戦果のみ）。反動は `AtrocityRules`/`WarCrimesRules`/`ImperialBlowbackRules`/支持#113 へ接続。`FortressMainCannon` の戦略版 |
| **GIR-4** | #2734 | 鹵獲と敵性技術の解析（拿捕→リバースエンジニアリング→開発加速） | 新 `CapturedTechRules`。`BoardingActionRules`×`TechDiffusionRules`×`ResearchRules`/`TechTreeRules`×`CaptivityRules` |
| **GIR-5** | #2735 | 兵器系統の開発分岐（艦系統樹＝伸ばす系統の機会費用・世代艦） | 新 `ShipLineageRules`。`TechTreeRules`（作り直さない）×`ShipClass`#80×`HeritageShipNames`×`ShipyardRules` |
| **GIR-6** | #2736 | 指導者の演説・プロパガンダ作戦（一手で全軍士気/徴募/敵厭戦） | 新 `WarSpeechRules`。`FreePressRules`×`WarWearinessModifiersRules`×支持#113×3チャネル#908 |
| **GIR-7** | #2737 | （lore）総力戦と指導者の野望・大量破壊の倫理 | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`GIR-1 → GIR-2`（指揮影響圏→覚醒＝ギレンの野望の最も固有で欠落の大きい signature・銀英伝の将官の手応え）→ `GIR-3`（戦略兵器の決断＝既存の残虐反動に接続）→ `GIR-4`（鹵獲研究）→ `GIR-6`（演説）→ `GIR-5`（艦系統）→ `GIR-7`（lore）。

> いずれも既存の戦略・会戦ロジックを**後退させず接続・拡張**する additive 設計。タイクン化回避（高位の決断→エンジン→創発帰結）・PERF#1117（指揮影響圏は throttle スキャン）。

## 関連
- [[almagest-reference-design]] — 兵器カスタム/戦略UIシェル
- [[taikou-risshiden-reference-design]] — 武勲→昇進（覚醒の素地）
- [[sangokushi-taisen-reference-design]] — 会戦の号令/采配（指揮影響圏と相補）
- [[roadmap]] — §5-2 に本EPICを追記
