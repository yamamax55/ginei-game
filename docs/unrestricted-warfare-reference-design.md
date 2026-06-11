# 『超限戦』参考設計（EPIC #ULW）

> 参照元：喬 良・王 湘穂『超限戦』（1999）。中国人民解放軍大佐2名による戦略論。
> 軍事に限定されない「制限のない戦争」—— 経済・情報・法律・心理・外交など**あらゆる領域を組み合わせた複合打撃**で、軍事的優位者を非軍事的手段で打倒する理論。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略）にとって**役に立つ視点**だけを抽出し、EPIC `#ULW` として issue 化する提案。
> 著作権注意：固有名・文章・固有設定は流用せず、**戦略メカニクス／世界観の構造パターンのみ**を参考にする。

---

## 0. なぜ「超限戦」が本システムに役立つか

当プロジェクトは**戦略×経済×外交の純ロジック層を大量に保有**している：

| 既存（カバー範囲） | カバー内容 |
|---|---|
| `EspionageRules`（#215 系） | 情報収集・妨害・スパイ工作（InfoGain/MissionSuccessChance/DetectionRisk） |
| `CommerceRaidingRules`（#95） | 通商破壊（護衛護送vs掃討の数値解決） |
| `DiplomacyRules`/`TreatyRules` | 外交状態・条約締結・破棄・宣戦布告 |
| `NonviolenceRules`/`ConsentRules` | 非暴力運動・合意の撤回（自陣営内） |
| `WarGoalRules`/`WarWeariness` | 戦争目標・厭戦・和平受諾 |
| `FiscalRules.ExchangeRateFactor` | 財政悪化→通貨安（受動的） |
| `SecurityRules` | 秘密警察・反乱鎮圧・クーデター検知 |

**しかしこれらは個別ドメインの「縦割り」ロジックであり、超限戦が固有に描く以下が欠けている**：

| 超限戦が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **複合打撃（組合せ拳）**：経済＋情報＋法律＋軍事を同時に叩く「統合キャンペーン」 | 各ドメインは独立実行。**複数ドメインを同時に発動し増幅効果を得るエンジン**が無い |
| **法律戦（ローフェア）**：国際法・条約を攻撃的に使い相手の行動空間を収縮させる | `TreatyRules` は条約の定義。**法規範を武器として提訴・制約強要する**ロジックが無い |
| **積極的情報戦・世論戦**：敵の国内合意を崩す意図的偽情報・心理作戦 | `EspionageRules` は**受信**（情報収集）。**敵向けに発信して内部結束を蝕む**回路が無い |
| **閾値以下作戦（グレーゾーン）**：公式の「戦争」を宣言させずに損害を与える否認可能な作戦 | `DiplomacyState` は平時/交戦の2択。**戦争と平和の間のグレーゾーン**が無い |
| **経済的強制のエスカレーション梯子**：通商妨害→制裁→金融封鎖の段階的締め上げ体系 | `CommerceRaidingRules` は孤立した数値解決。**段階的な経済圧力の統合軸**が無い |

**結論**：超限戦は当プロジェクトの戦略レイヤーに**「縦割りドメインを横断する複合打撃」という第三の勝ち筋**を与える。軍事で劣る勢力が非軍事手段の組み合わせで強者を崩す——銀英伝の「フェザーン（商社国家）が武力なく帝国・同盟双方を操る」構図に最もテクスチャが合う。

---

## 1. 役に立つ視点（要約）

超限戦の世界観を、**本システムに効く形**で1行ずつ：

1. **「全ての手段が兵器になる」**。軍艦だけが武器でない——経済制裁・情報操作・法廷戦術・無関係に見える企業買収も戦争行為。→ 当プロジェクトの**多勢力戦略ゲームに第三・第四の勝ち筋**を開く。
2. **複数ドメインの同時発動が非線形に増幅する**。経済圧力だけでは抵抗できる相手が、同時に情報戦・法律戦をかけられると崩れる。→ `CombatModifiers`・`ModifierStack` の**横断版**が必要。
3. **「閾値以下」が鍵**。相手が「これは戦争だ」と宣言できないレベルに抑えながら損害を与え続ける。→ `DiplomacyState` に**グレーゾーン状態**が必要。
4. **法律は剣にも盾にもなる**。条約・国際法を利用して相手の軍事オプションを封じる（ローフェア）。→ `TreatyRules` に**攻撃的用法**を足す。
5. **情報戦は双方向**。スパイが情報を奪うだけでなく、偽情報を撒いて敵の内部決定を歪める。→ `EspionageRules.InfoGain` の**送信側**が欠落。
6. **弱者の戦略たりうる**。軍事で劣る勢力が、経済・情報・法律の組み合わせで強者を圧倒できるかの非対称評価軸。→ 「戦わずして勝つ」孫子（`SUN-1〜5`）と相補・接続。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`CommerceRaidingRules`/`EspionageRules`/`DiplomacyRules`/`WarGoalRules` を作り直さない**。ULW はそれらに**欠落軸を足し、横断する統合レイヤーを追加**するだけ（additive）。

### ★★★ 最優先（真の欠落・超限戦の signature）

#### ULW 複合打撃ドクトリン（`HybridCampaignRules`）
- **マルチドメイン攻撃の統合エンジン**：`CampaignDomain{軍事,経済,情報,法律,心理}` ×
  強度（0..1）のベクトルとして定義。`HybridCampaignRules.CombinedEffect(strike, target)` =
  軍事効果 × （1 ＋ 情報増幅 ＋ 法律制約 ＋ 経済圧力）＝各ドメインが乗算で増幅。
- **相乗効果の閾値**：2ドメイン以上の同時発動で `SynergyFactor` が加算（単発では出ない）。
- 接続：`CommerceRaidingRules`/`EspionageRules`/`DiplomacyRules`/`WarGoalRules`/`CombatModifiers`
  に**外部コールとして乗る**（各モジュールは不変）。

#### ULW 法律戦（`LawfareRules`）
- **国際法・条約の攻撃的用法**：`LawfareAction{条約提訴, 規則利用, 制約強要, 規範形成}` ＋
  `LawfareRules.ConstraintStrength(action, target)` → 相手が取れる軍事オプションを削る
  （例：特定回廊での兵力展開に法的根拠を失わせる）。
- **コスト**：法律戦の発動には外交資本（`DiplomacyState.opinion`）を消費。高コスト・高信頼国は低コスト。
- 接続：`TreatyRules`（条約の違反判定を `IsAboveThreshold` で利用）・
  `DiplomacyRules.HostileOverride`（法的勝訴が相手の行動制限へ）。

### ★★ 高（マクロ戦略に新しい手段を追加する）

#### ULW 積極的情報戦・世論戦（`PsyOpRules`）
- **敵国内部への心理作戦**：`PsyOpCampaign{target, domain, intensity, channel}` を
  `PsyOpRules.Launch(campaign)` で実行 → `ConsentRules.Polity.cooperation`（敵国民の協力率）を
  低下させ、`HopeRules.Community.hope` を蝕む。
- **`EspionageRules` との分離**：`EspionageRules` は**受信（情報収集）**、
  `PsyOpRules` は**発信（意図的撒布）**——同じ諜報ネットワークを媒体として使うが別ロジック。
- 接続：`EspionageRules.SpyNetwork`（媒体）・`ConsentRules`・`HopeRules`・`EventEngine`（風説イベント）。

#### ULW 閾値以下・グレーゾーン作戦（`GreyZoneRules`）
- **宣戦なき損害**：作戦の「強度スコア」が `WarDeclarationThreshold` を超えないよう
  抑えながら損害を与え続ける。`GreyZoneRules.IsAboveThreshold(op)` → false の間は
  `DiplomacyState` が `交戦` に遷移しない（相手は正式に宣戦できない）。
- **否認可能性**：`AttributionAmbiguity(op)` — 作戦が自勢力に帰属されるかどうかの確率
  （低ければ相手の外交コストが上がる）。
- 接続：`DiplomacyRules.DeclareWar`（閾値超過でトリガー）・`EspionageRules`・`PsyOpRules`・
  `CommerceRaidingRules`（全て `GreyZoneOp` のドメインとして包む）。

### ★ 中（既存システムの横断統合）

#### ULW 経済的強制のエスカレーション梯子（`EconomicCoercionRules`）
- **段階的締め上げの統合軸**：`CoercionLevel{通商妨害,部門制裁,金融封鎖,通貨攻撃}` の梯子。
  各段階が前段を前提とし、効果が累積する（通商妨害だけでは弱い→制裁が重なると強い）。
- **報復連鎖**：`RetaliatorySpiral(initiator, target)` — 一方の制裁が報復制裁を呼び、
  やがて双方が経済損害を負う均衡（脱出条件は `PeaceAcceptance` 類似）。
- 接続：`CommerceRaidingRules`（通商妨害＝L1）・PRC-8 制裁（L2）・
  `FiscalRules.ExchangeRateFactor`（L4 通貨攻撃の帰結）。**新規コード最小、既存を梯子で束ねる**。

#### ULW（lore）超限戦世界観の開示データ
- 「戦争の境界が消える」「あらゆる手段が武器になる」「軍事的敗北が戦略的勝利にならない」。
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への**lore データ入力**。
  CCX-6（世界観codex退避）方針に一貫。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 通商破壊そのもの | **`CommerceRaidingRules` #95 が既存**。ULW は梯子（ULW-5）でその上に乗る |
| サイバー攻撃の個別実装 | **`EspionageRules.SabotageEffect` がカバー**。ULW は `PsyOpRules`/`GreyZoneRules` の発動チャネルとして既存を使う |
| 宣戦布告・条約の再実装 | **`DiplomacyRules` がカバー**。ULW は閾値ロジック（ULW-4）を上乗せするだけ |
| 軍事指揮の改変 | **戦術レイヤーは不変**（超限戦は戦術でなく戦略・非軍事領域の議論）。`CombatModifiers` は読むだけ |
| 非国家武装集団の個別実装 | 超限戦の「非国家主体の武器化」は `PsyOpRules`（ULW-3）と `GreyZoneRules`（ULW-4）の組み合わせで表現可能。専用モジュール不要 |

---

## 3. EPIC #ULW の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UI へ配線。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #1370**。GitHub issue 起票済み（#1374〜#1399）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **ULW-1** | #1374 | 複合打撃ドクトリン（`HybridCampaignRules`：多ドメイン同時発動の相乗効果モデル） | `CombatModifiers`×全ドメイン。孫子`SUN-1`と相補 |
| **ULW-2** | #1380 | 法律戦（`LawfareRules`：条約・国際法を攻撃的に使い相手の行動空間を収縮） | `TreatyRules`×`DiplomacyRules`×`WarGoalRules` |
| **ULW-3** | #1386 | 積極的情報戦・世論戦（`PsyOpRules`：敵国内部合意を蝕む偽情報・心理作戦） | `EspionageRules`（媒体）×`ConsentRules`×`HopeRules` |
| **ULW-4** | #1392 | 閾値以下・グレーゾーン作戦（`GreyZoneRules`：宣戦なき損害・否認可能性） | `DiplomacyRules.DeclareWar`×`EspionageRules`×`CommerceRaidingRules` |
| **ULW-5** | #1397 | 経済的強制の梯子（`EconomicCoercionRules`：通商妨害→制裁→金融封鎖の段階統合） | `CommerceRaidingRules`×PRC-8×`FiscalRules` |
| **ULW-6** | #1399 | （lore）超限戦世界観の開示データ（「全ての手段が武器になる」等を `DisclosureLedger` へ） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`ULW-1`（複合打撃＝最も固有な欠落・全体の土台）→ `ULW-2`（法律戦＝軍事以外の第一の刃）→
`ULW-4`（グレーゾーン＝閾値管理の核）→ `ULW-3`（情報戦＝ConsentRules/HopeRules への配線）→
`ULW-5`（経済梯子＝既存束ね・統合）→ `ULW-6`（lore・最後）。

> いずれも既存戦略ロジックを**後退させず横断統合**する additive 設計。フェザーン（#160 商社国家）と非軍事路線（孫子 #1125 系）に最も効く。
