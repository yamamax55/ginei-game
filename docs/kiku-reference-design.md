# ベネディクト『菊と刀』参考設計（EPIC #KIKU）

> 参照元：ルース・ベネディクト『菊と刀』（Ruth Benedict, *The Chrysanthemum and the Sword*, 1946）。
> 文化人類学者が戦時研究として描いた日本文化の構造分析——**「恥の文化」「義理と人情」「恩の循環」**を核に、社会制御のメカニズムを解剖する。
> 本ドキュメントは、当プロジェクト（銀英伝風の星間国家戦略＋既に大規模な忠誠/文化/内政純ロジック層）にとって**役に立つ視点だけを抽出し**、EPIC `#KIKU` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**文化メカニクス／社会制御の構造パターンのみ**を参考にする。

---

## 0. なぜ「菊と刀」が本システムに役立つか

当プロジェクトは社会制御・忠誠・文化の**マクロ純ロジックを大量に保有**している（[CLAUDE.md] 参照）：

| 既存（カバー範囲） | カバーされている部分 |
|---|---|
| `LoyaltyRules`/`Allegiance`（#817） | 条件付き忠誠・調略による閾値突破・寝返りカスケード |
| `ConsentRules`/`Polity`（#836） | 「権力は借り物」＝協力低下→統治不能 |
| `CultureRules`/`Culture`（#194） | 同化圧力・分離独立リスク・ナショナリズム・亡命 |
| `FactionData.ideology`（B-1〜B-4） | 専制/民主等の政体思想・住民思想との一致度 |
| `PersonRules`/`Person`（#866） | 軍人/文民の役割適性・効力・適材適所 |
| `PledgeRules`（SGZ-3） | 義兄弟型の自発的誓約・拘束力・離反ペナルティ |
| `RankSystem`/`SeniorityRules`（#14/#155/#156） | 階級ラダー・席次主義vs実力主義 |

**しかし、これらは「行動の動機」「行動の閾値」「裏切りの確率」というマクロ均衡**であり、
菊と刀が固有に描く以下が**欠けている**：

| 菊と刀が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **恥の文化＝可視性依存の社会制御** | `ConsentRules`/`LoyaltyRules` は行動変容のマクロ均衡。**見られてこそ恥になる**＝可視性が制裁量を決める回路が無い |
| **義理・恩の負債構造（循環義務）** | `PledgeRules`（SGZ-3）は自発的誓約。**恩恵を受けると義理が蓄積し返済義務が生じる**という強制的負債サイクルが無い |
| **義理と人情の葛藤** | `PersonRules.Effectiveness` は役割適合度。**社会義務と個人感情が衝突したときの行動選択**モデルが無い |
| **名誉の毀損と公的回復** | `LoyaltyRules` は裏切り/忠誠の閾値。**名誉という社会的資源が毀損・回復する動学**（公的行為による復権）が無い |
| **恥/罪の文化軸 = 社会制御「型」の差** | `FactionData.ideology` は政体の種別。**恥（可視性依存）vs 罪（良心依存）という制御メカニズムの違い**が文化型として切り出せていない |

**結論**：菊と刀は当プロジェクトの忠誠/文化層に**「行動が見られるかどうかで結果が変わる」「義理は蓄積する強制的負債」「名誉は社会的資源」**という3つの欠落軸を与える。
そして**帝国（恥の文化＝面子・義理）vs 同盟（罪の文化＝良心・民主的責任）の文化対比**は銀英伝世界の核心テーマと完全に共鳴する。

---

## 1. 役に立つ視点（要約）

菊と刀の世界観を、**本システムに効く形**で1行ずつ：

1. **「恥の文化」＝社会制御は外部の眼差しが媒介する**。見られている場では規律が保たれ、見られていない場では規律が緩む。→ `ShameRules`（可視性×重大度→制裁量）で諜報/監視が社会統制の直接コストになる。
2. **恩は意図せず受け取られ、義理として返済を強制する**。主君の恩寵→臣下の義理→戦場での献身——この循環が封建制の実動エンジン。→ `GiriRules`（恩負債の蓄積→返済義務）が `LoyaltyRules` の動機層を補強する。
3. **義理と人情は衝突する**。義理を優先すれば人情を裏切り、人情を優先すれば社会から外れる。この葛藤が登場人物を複雑にする。→ `PersonRules.Effectiveness` に義理-人情緊張修正子を追加。
4. **名誉は社会的資源で毀損・回復できる**。公的失敗→名誉損失、公的贖罪行為→回復——極端には自刃による完全回復。→ `HonorRules`（名誉量の動学）が `LoyaltyRules`/`LifecycleRules` に接続する。
5. **恥の文化 vs 罪の文化の差が、同じ行動でも社会的帰結を変える**。監視が厳しい文化圏では可視性修正子が大きく、良心依存文化圏では内的スコアが支配する。→ `CulturalControlType`（恥型/罪型）を `CultureRules` に足し `FactionData.ideology` から解決。
6. **「見られていないなら義務を果たさなくていい」という論理が腐敗を生む**。恥の文化は監視機構が弱れば崩れやすい。→ `SecretRules`（#166）/`GovernanceRules` の腐敗加速修正子として接続。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`CultureRules`#194/`LoyaltyRules`#817/`ConsentRules`#836/`PersonRules`#866 を作り直さない**。KIKU はそれらに**欠落軸を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・菊と刀の signature）

#### KIKU 恥の文化メカニクス = 可視性依存の社会制御（`ShameRules` / `ShameProfile`）

- **コア**：行動 A が制裁を生むかどうかは `visibility(A)` に依存する。
  `shame_pressure = visibility × severity → behavioral_change`。
  見られていない行為は恥を生まない＝監視コストが社会統制コストになる。
- **接続先**：
  - `ConsentRules`（合意低下に可視性修正子を追加）
  - `LoyaltyRules.ResolveStance`（恥圧力が高ければ戦う閾値を下げる）
  - `SecurityRules`#166（監視強度が visibility を上げる）
  - `EventEngine`（恥イベントの発火条件）
- 純ロジック・test-first・EditModeテスト必須

#### KIKU 義理・恩の負債構造（`GiriRules` / `ObligationDebt`）

- **コア**：恩恵(`On`)の受取 → 負債 `obligation_debt` が蓄積。返済が不足すると恥圧力（KIKU-1）が上昇し忠誠が増す・または負い目から行動する。
  `on_received → giri_debt += on × weight; giri_repaid → debt -= repay`
- **封建ループ**：主君の恩寵（昇進/恩賞）→ 臣下の義理積み上げ → 戦場での献身・離反耐性 → さらなる恩寵。
- **接続先**：
  - `LoyaltyRules.BaselineLoyalty`（動的義務係数として追加）
  - `GovernmentRegistry`（patron-client の恩-義理循環）
  - `FleetRoster.AssignAdmiral`（昇進は恩付与イベント）
  - `FactionLoyaltyRules`（腐敗した国家は恩を与える余力が落ちる）
- 純ロジック・test-first・EditModeテスト必須

### ★★ 高（人物層・名誉の動学）

#### KIKU 義理と人情の葛藤エンジン（`GiriNinjoTensionRules`）

- **コア**：人物が抱える義理負債の総量と個人感情（人情スコア）の差 `tension = giri_debt - ninjo_score` が高いと`PersonRules.Effectiveness` に罰を受けるが、義理優先の行動で債務を返済できる。
- **接続先**：
  - `PersonRules.Effectiveness`（葛藤係数修正子）
  - `EventEngine`（義理-人情葛藤イベント＝選択肢 = 義理優先/人情優先）
  - `LoyaltyRules.ResolveCascade`（人情は「局所的な寝返り」要因になりうる）
- 純ロジック・test-first・EditModeテスト必須

#### KIKU 名誉の毀損と公的回復（`HonorRules` / `HonorState`）

- **コア**：名誉 `honor(0..1)` は社会的資源。公的失敗イベントで損耗し、公的贖罪行為（大きな戦果・名誉ある死・恩の完全返済）で回復する。
  `honor` が閾値を下回ると `LoyaltyRules.ResolveStance` の忠誠 → 戦う閾値が変動し、極端な毀損は `LifecycleRules.Kill`（自刃経路）を開く。
- **接続先**：
  - `LoyaltyRules`（名誉損失 → 戦う閾値増加・または絶望的突撃）
  - `LifecycleRules.Kill`（自刃＝名誉の究極回復手段）
  - `FactionLoyaltyRules`（国家の名誉→`BaselineLoyalty` 修正子）
  - `EventEngine`（名誉毀損イベント・回復イベント）
- 純ロジック・test-first・EditModeテスト必須

### ★ 中（文化型の分類と lore）

#### KIKU 恥/罪の文化軸（`CulturalControlType`）= `CultureRules` への文化制御型追加

- **コア**：`enum CulturalControlType { 恥の文化, 罪の文化, 未分化 }`。
  恥の文化 = 可視性修正子が有効（`ShameRules` フル起動）。
  罪の文化 = 良心スコアが可視性を上回る（`ConsentRules` の内的コスト重視）。
- **接続先**：
  - `CultureRules`/`FactionData.ideology`（文化型を文化属性として解決）
  - `ShameRules`（KIKU-1）の可視性修正子のon/off
  - `GovernanceRules.IdeologyModifier`（文化型が統治効率に影響）
  - `FactionData`（`culturalControlType` フィールド追加）
- 純ロジック・test-first・EditModeテスト必須

#### KIKU（lore）世界観の開示データ（恥/義理/名誉の人類普遍性と文化差）

- 恥の文化 vs 罪の文化の文化人類学的対比・義理と人情の葛藤・名誉の回復とその先にあるもの。
- 帝国（恥の文化型・義理の封建制）vs 同盟（罪の文化型・良心の民主主義）という世界観コントラストを秘史/lore データとして焼く。
- **コード新設なし**：`DisclosureLedger`（FND-4）への**lore データ入力**。世界観EPIC（啓蒙/ニーチェ/秘史エンディング）に接続。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 一般的な階層・席次・昇進 | **`RankSystem`/`SeniorityRules`/`CareerPipelineRules` が既にカバー** |
| 自発的誓約・義兄弟型盟誓 | **SGZ-3 `PledgeRules` がカバー**。KIKU は強制的負債のみ |
| 文化的同化・分離独立 | **`CultureRules`#194 がカバー** |
| 合意の撤退・非協力 | **`ConsentRules`#836 がカバー**。KIKUは可視性修正子として接続のみ |
| 宗教と経済の融合 | **SAW-7 がカバー** |
| 政治腐敗のマクロモデル | **`DynastyRules.Tick`/`SecurityRules` がカバー**。KIKU は監視×恥の接続のみ |
| 歴史家の文化比較論を全部実装 | タイクン化回避。高位の抽象（恥/義理/名誉の3軸）のみ足す |

---

## 3. EPIC #KIKU の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。既存の忠誠/文化ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**文化メカニクス/社会制御の構造パターンのみ**参考。

> **EPIC = #1830**。GitHub issue 起票済み（#1832〜#1850）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **KIKU-1** | #1832 | 恥の文化メカニクス＝可視性依存の社会制御（`ShameRules`/`ShameProfile`） | 新 `ShameRules`（可視性×重大度→恥圧力）。`ConsentRules`×`LoyaltyRules`×`SecurityRules`#166 接続 |
| **KIKU-2** | #1835 | 義理・恩の負債構造（`GiriRules`/`ObligationDebt`） | 新 `GiriRules`。恩受取→義理負債蓄積→返済義務→`LoyaltyRules.BaselineLoyalty` 動的係数 |
| **KIKU-3** | #1838 | 義理と人情の葛藤エンジン（`GiriNinjoTensionRules`） | 新 `GiriNinjoTensionRules`。`PersonRules.Effectiveness` 修正子×`EventEngine` 葛藤選択肢 |
| **KIKU-4** | #1841 | 名誉の毀損と公的回復（`HonorRules`/`HonorState`） | 新 `HonorRules`。名誉量(0..1)の動学。`LoyaltyRules`×`LifecycleRules.Kill`（自刃経路）接続 |
| **KIKU-5** | #1846 | 恥/罪の文化軸（`CulturalControlType`）＝`CultureRules`への文化制御型追加 | `CultureRules` 拡張＋`FactionData` フィールド追加。`ShameRules`（KIKU-1）の可視性修正子on/off |
| **KIKU-6** | #1850 | （lore）世界観の開示データ（恥/義理/名誉・帝国=恥/同盟=罪の文化対比） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順

`KIKU-1 → KIKU-2`（恥の可視性モデルと義理負債＝最も固有で欠落の大きい signature）
→ `KIKU-3`（葛藤エンジン＝人物に内的緊張を与える）
→ `KIKU-4`（名誉動学＝生死に繋がる重層化）
→ `KIKU-5`（文化型 enum で帝国/同盟を差別化）
→ `KIKU-6`（lore 入力で世界観と統合）

> いずれも既存の忠誠/文化/内政ロジックを**後退させず接続**する additive 設計。
> 帝国の義理封建制 と 同盟の民主的良心 という文化的二項対立に**実動エンジン**を与える。
