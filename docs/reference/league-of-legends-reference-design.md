---
type: reference
tags: [reference]
---

# League of Legends 参考設計（EPIC #LOL）

> 参照元：『League of Legends』（Riot Games のMOBA）。2チームが中立目標（ドラゴン/バロン）を奪い合い、パワースパイクのタイミングで集団戦を仕掛け、優劣がスノーボールし、最後は拠点を割って勝つ——時間と空間の駆け引きが濃いチーム対戦ゲーム。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略＋戦術艦隊戦）にとって**移植して映える「いいところ」だけ**を抽出し、EPIC `#LOL` として issue 化する提案。
> 著作権注意：固有名・チャンピオン・文章・固有設定は流用せず、**ゲームメカニクス／駆け引きの構造パターンのみ**を参考にする。

---

## 0. なぜ「League of Legends」が本システムに役立つか

当プロジェクトは会戦・戦略の**純ロジックを大量に保有**している（[CLAUDE.md] 参照）。LoL の要素の多くは**既にカバー済み**：

| 既存（カバー範囲） | LoL の該当要素 |
|---|---|
| `FogOfWarRules`/`DetectionRules`/`InformationAsymmetryRules`/`ElectronicWarfareRules`/偵察#119 | 視界・フォグ・索敵・ワード |
| `ActiveCommandRules`#2175／`CommandAuraRules`（GIR-1 予定） | スキル/アクティブ・指揮バフ |
| `ShipCombat.FindPrioritizedEnemyInArc` | フォーカス・ターゲット優先 |
| 配下艦スクリーン／護衛勝利条件 | ピール（要の護衛） |
| 混乱（突撃）/幻惑（石兵八陣）/`ZoneOfControl` | CC（スタン/スロー/拘束） |
| `ArmamentDesignRules`/`WeaponsRules` | 装備ビルド/アイテム |
| `VeterancyRules`（練度XP）/`TalentCatalog` | 成長・育成 |
| SGT-7 出撃コスト編成（#2727） | ドラフト（戦力選択） |

**しかし、LoL が固有に持つ「時間と空間の駆け引き」の以下が欠けている**：

| LoL が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **中立目標の確保で全軍バフ（ドラゴン/バロン）** | 要塞#76-78/拠点はあるが、**確保すると勢力全体に逓増バフが乗る係争中立目標**が無い |
| **パワースパイク/スケーリング曲線（序盤強い/終盤化ける）** | `VeterancyRules` は単調にXPで強くなるだけ＝**兵種/将ごとに序中終盤のパワーカーブが違う「自分の山で仕掛ける」タイミング駆け引き**が無い |
| **スノーボールと賞金首・追い上げ（shutdown/catch-up）** | 殊勲過多の将を狙う動機・劣勢側の粘りが薄い＝**勝ちすぎた将が賞金首になり討てば大功／劣勢でも崩れにくいキャッチアップ**が無い |
| **グローバル介入（テレポ/グローバルult）** | 戦略移動はあるが、**限られた回数で遠方戦線へ即時介入し盤面全体に影響する一手**が無い |
| **スプリットプッシュとマップ圧力（1-3-1）** | 陽動（`FeintRules`）はあるが、**別働隊で手薄な戦線を突き敵の対応を強要する戦略的マクロ圧力**が薄い |

**結論**：LoL は当プロジェクトに **①中立目標の全軍バフ ②パワースパイク曲線 ③スノーボール抑制（賞金首/追い上げ）**という3つの強い欠落軸＝**「時間（いつ仕掛けるか）と空間（どこを取るか）の駆け引き」**を与える。いずれも既存（要塞/`VeterancyRules`/`MeritRecordRules`/`FeintRules`/`CombatModifiers`）を**作り直さず接続・拡張**する additive。

---

## 1. 役に立つ視点（要約）

LoL の駆け引きを、**本システムに効く形**で1行ずつ：

1. **盤上には「取る価値のある中立点」がある**。確保すれば全軍が強くなるので、戦力をそこへ吸い寄せる＝会戦に**目的地の引力**を作る。→ 漫然とした殴り合いを「目標を巡る攻防」へ。
2. **強さには時間軸がある**。今弱くても化ける編成、今強いが息切れする編成＝**「自分の山（パワースパイク）で仕掛ける」タイミング**の読み合い。→ `VeterancyRules` に**曲線**を与える。
3. **勝ちは雪だるま式に転がるが、転がりすぎは抑えられる**。突出した将は賞金首として狙われ、劣勢側は粘れる＝**一方的にならない動学**。→ 銀英伝の「ヤンを討てば戦局が変わる」緊張。
4. **遠くの戦線に一手で介入できる**。限られた切り札で盤面全体に圧力＝**マクロの決断**。→ 戦略移動に「即時介入の一手」を足す。
5. **正面でなく手薄を突く**。別働隊のスプリットプッシュが敵の対応を強要＝**兵力を割く/集めるのジレンマ**。→ 戦略AIに多正面圧力を足す。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**視界`FogOfWarRules`/`DetectionRules`・CC（混乱/幻惑/ZOC）・フォーカス`ShipCombat`・装備`ArmamentDesignRules`・ドラフトSGT-7 を作り直さない**。LoL はそれらに**欠落軸を足し、接続する**だけ（additive）。MOBAのマイクロ操作・単一文字キー・課金・ランク/対人は**不採用**。

### ★★★ 最優先（真の欠落・LoL の signature）

#### LOL 中立戦略目標と全軍バフ（ドラゴン/バロン型）
- 盤上に**係争中立目標**（資源星系/古代遺物/中継要衝）を置き、確保した勢力に**逓増する全軍バフ**（攻撃/機動/補給/視界等）を与える。バロン型＝強力だが時限・喪失で剥落。
- 接続：新 `NeutralObjectiveRules`（Core・純ロジック・test-first）＝確保判定/スタック逓増/時限剥落/バフ量。`FortressRegistry`/要塞#76-78・`GalaxyMap`（中立星系）・`CombatModifiers`/`ModifierStack`（バフ適用）・`CommandAuraRules`（GIR-1・全軍適用の相棒）に接続。**戦力を目標へ吸い寄せる引力**。

#### LOL パワースパイク/スケーリング曲線（序盤強い/終盤化ける）
- 兵種/将に**時間軸のパワーカーブ**（序盤型/終盤型/スパイク型）を与え、**「自分の山で仕掛ける」タイミング**の読み合いを作る。会戦内の経過時間×練度で実効戦力が曲線的に変化。
- 接続：新 `ScalingCurveRules`（Core・実効値パターン）＝曲線種別×経過×練度→倍率。`VeterancyRules`（練度）×`ShipClass`#80×`AdmiralArchetypeModifiers`×`CombatModifiers` に接続（`VeterancyRules` の単調成長に曲線を重ねる・二重実装しない）。

#### LOL スノーボールと賞金首・追い上げ（shutdown / catch-up）
- 殊勲を上げ続けた将は**賞金首**となり敵AIの優先目標＝討てば**大功**（武勲/士気の大スイング）。劣勢勢力は**粘り**（士気的キャッチアップ）で一方的展開を抑制。
- 接続：新 `MomentumBountyRules`（Core・決定論）＝武勲/連勝→賞金首度＋劣勢度→追い上げ係数。`MeritRecordRules`（武勲・TKO-2）×`FleetMorale`×`WarWearinessModifiersRules`×`FactionState`（国力差）に接続。**金銭のラバーバンドでなく評判/士気駆動**（人為的補正でなく創発）。

### ★ 中（マクロの一手・多正面圧力・lore）

#### LOL グローバル介入（遠方戦線への即時増援＝戦略リソース）
- 限られた回数の**戦略リソース**で、遠方戦線へ**即時増援/介入**し盤面全体に圧力＝マクロの決断。
- 接続：新 `GlobalInterventionRules`（Core）＝介入コスト/クールダウン/効果。`BattleHandoff`・戦略艦隊移動・采配ゲージ風コスト（SGT-1 と同型の有限資源）に接続。

#### LOL スプリットプッシュとマップ圧力（1-3-1 マクロ）
- 別働隊で**手薄な戦線を突き敵の対応を強要**＝兵力を割く/集めるのジレンマ（戦略AIの多正面圧力）。
- 接続：新 `MapPressureRules`（Core）＝戦線ごとの圧力/対応強要度。戦略AI（`GalaxyView`）×`GalaxyMap` 回廊×`FeintRules`（陽動・戦術版とは別の戦略マクロ層）に接続。

#### LOL（lore）協働と犠牲の倫理
- 「個の犠牲とシナジーが勝利を生む」「役割分担（要・盾・斥候）」のチーム倫理。
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への lore データ入力。

### ❌ 不採用（重複・既存で十分・性質が異なる）

| 不採用 | 理由 |
|---|---|
| 視界・フォグ・索敵・ワード | **`FogOfWarRules`/`DetectionRules`/`InformationAsymmetryRules`/偵察#119 が既存** |
| CC（スタン/スロー/拘束） | **混乱（突撃）/幻惑（石兵八陣）/`ZoneOfControl` が既存**（接続のみ） |
| フォーカス・ターゲット優先 | **`ShipCombat.FindPrioritizedEnemyInArc` が既存** |
| ピール（要の護衛） | **配下艦スクリーン/護衛勝利条件 が既存** |
| ドラフト（ピック&バン） | **SGT-7 出撃コスト編成（#2727）でカバー**。バンは単一プレイには薄い |
| 装備ビルド/アイテム | **`ArmamentDesignRules`/`WeaponsRules` が既存** |
| スキル/アクティブそのもの | **`ActiveCommandRules`#2175／号令SGT-2 が既存** |
| 単一文字キー/マイクロ操作/課金/ランク/対人マッチング | 本作はリアルタイム星間戦略＝性質が異なる（指示どおり不採用） |

---

## 3. EPIC #LOL の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面（会戦/戦略）へ配線。既存ロジックは**接続・拡張のみ・重複新設しない**。
> 著作権注意：固有名・チャンピオンは不使用、**メカニクス/駆け引き構造のみ**参考。

> **EPIC = #2739**。GitHub issue 起票済み（#2740〜#2745）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **LOL-1** | #2740 | 中立戦略目標と全軍バフ（ドラゴン/バロン型の係争中立点） | 新 `NeutralObjectiveRules`。`FortressRegistry`/要塞#76-78×`GalaxyMap`×`CombatModifiers`×`CommandAuraRules`(GIR-1) |
| **LOL-2** | #2741 | パワースパイク/スケーリング曲線（序盤強い/終盤化ける） | 新 `ScalingCurveRules`。`VeterancyRules`×`ShipClass`#80×`AdmiralArchetypeModifiers`×`CombatModifiers` |
| **LOL-3** | #2742 | スノーボールと賞金首・追い上げ（shutdown/catch-up） | 新 `MomentumBountyRules`。`MeritRecordRules`(TKO-2)×`FleetMorale`×`WarWearinessModifiersRules`×`FactionState` |
| **LOL-4** | #2743 | グローバル介入（遠方戦線への即時増援＝戦略リソース） | 新 `GlobalInterventionRules`。`BattleHandoff`×戦略艦隊移動×采配ゲージ型コスト(SGT-1) |
| **LOL-5** | #2744 | スプリットプッシュとマップ圧力（1-3-1 の多正面圧力） | 新 `MapPressureRules`。戦略AI(`GalaxyView`)×`GalaxyMap`回廊×`FeintRules`（戦略マクロ層） |
| **LOL-6** | #2745 | （lore）協働と犠牲の倫理（役割分担で勝つ） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`LOL-1 → LOL-2 → LOL-3`（中立目標→パワースパイク→スノーボール抑制＝LoL の「時間と空間の駆け引き」の核三本）→ `LOL-4`（グローバル介入）→ `LOL-5`（マップ圧力）→ `LOL-6`（lore）。

> いずれも既存の会戦・戦略ロジックを**後退させず接続・拡張**する additive 設計。タイクン化回避（高位の決断→エンジン→創発帰結）・PERF#1117（中立目標/圧力は throttle・有界）。人為的なラバーバンドでなく評判/士気駆動の創発で実装する。

## 関連
- [[sangokushi-taisen-reference-design]] — 会戦の采配ゲージ/号令（LOL-4 の有限資源と相棒）
- [[gihren-no-yabou-reference-design]] — 指揮影響圏/覚醒（LOL-1 の全軍バフ・LOL-2 の成長と相棒）
- [[mahan-reference-design]] — 要衝・チョークポイント（中立目標の戦略的価値）
- [[roadmap]] — §5-2 に本EPICを追記
