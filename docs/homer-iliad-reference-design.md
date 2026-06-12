# ホメロス『イリアス』参考設計（EPIC #ILID）

> 参照元：ホメロス『イリアス』（古代ギリシャ叙事詩、パブリックドメイン）。トロイア戦争を描く——特に**アキレウスの怒り(mēnis)から始まり、パトロクロスの死を経て、アキレウスが戦場に復帰するまでの物語構造**。
> 本ドキュメントは、当プロジェクト（Ginei＝銀英伝風の星間国家戦略）にとって**役に立つ視点**だけを抽出し、EPIC `#ILID` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用しない。**名誉経済・連合戦争・英雄力学の構造パターンのみ**を参考にする。

---

## 0. なぜ「イリアス」が本システムに役立つか

当プロジェクトは名誉・忠誠・戦闘の**個別モジュールを大量に保有**している：

| 既存（カバー範囲） | カバー内容 |
|---|---|
| `HonorRules`/`HonorState`（KIKU-4 #1841） | 個人の名誉量の毀損・回復・自刃閾値 |
| `LoyaltyRules`/`Allegiance`（#817） | 忠誠/調略の閾値判定・寝返りカスケード |
| `BattleAllegianceRules`（#817） | 会戦中の旗幟再評価・静観/寝返り発火 |
| `OrderOfBattle`/`FleetRoster`（#146/#147） | 連合艦隊の梯団ツリー・命令系統 |
| `MeritRankRules`（#900） | 軍功→爵位・インセンティブ士気 |
| `AdmiralSkillRules`/`AdmiralSkill`（#137-140） | 提督スキルの条件付き修正子 |
| `Organization`/`SuccessionRules`（#812） | カリスマ死後の組織存続・制度化 |
| `DisclosureLedger`（FND-4 #495） | 世界観の秘史開示 |

**しかし、これらは「個人の名誉量」「静的な忠誠モデル」「独立した戦闘スキル」**であり、イリアスが固有に描く以下が**欠けている**：

| イリアスが固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **戦利品の名誉的分配問題** | KIKU `HonorRules` は個人の毀損/回復。**連合戦争の共同成果を公平/不公平に分配する集団的ルール**が無い |
| **怒りの公然撤退(mēnis)** | `BattleAllegianceRules` は旗幟(静観/寝返り)。**侮辱による「最強戦力の意図的ストライキ」**（怒り状態→公然撤退→和解条件）という構造が無い |
| **アリステイア（英雄の絶頂）** | `AdmiralSkillRules` は条件付き修正子。**特定の感情的契機で一時的に平常限界を超える「英雄絶頂状態」**（無敵に見える集中）が無い |
| **連合の名誉的権威構造** | `OrderOfBattle` は階級による命令系統。**名誉(timē)が盟主の指揮権の根拠**であり、名誉失墜→諸侯の命令拒否という因果連鎖が無い |
| **英雄の名声遺産(kleos)** | `LifecycleRules` は死亡。**英雄の死後に組織を超えて残る「名声の遺産」**——それが次世代の戦意と正統性を形成する回路が無い |

**結論**：イリアスは当プロジェクトの連合戦争層に**①戦利品の公平分配問題 ②怒り-撤退-和解の戦略的構造 ③英雄の絶頂状態 ④名誉に根ざした指揮権**という4つの欠落軸を与える。特に「最強戦力が論功行賞の失敗で戦わなくなる」という本システムの核心テーマ（銀英伝的な「提督が居なければ勝てない」）と完全に共鳴する。

---

## 1. 役に立つ視点（要約）

イリアスの世界観を、**本システムに効く形**で1行ずつ：

1. **「名誉(timē)は戦場で分配される希少資源」**。戦利品/褒賞は兵力や金銭でなく**名誉の物的表現**であり、不公平な分配は最強戦力を失う。→ 論功行賞の失敗が致命的な戦力喪失を生む連鎖。
2. **「怒り(mēnis)は最強者の戦略的武器」**。アキレウスの撤退は感情爆発でなく、名誉を侮辱した盟主への政治的圧力。→ 最強提督が「戦わない選択」を持つことで指揮系統に緊張が生まれる。
3. **「連合は盟主の名誉的権威で維持される」**。諸侯は名誉ある盟主には従うが、名誉を失った盟主の命令は合法的に無視できる。→ 連合の指揮権は形式的階級でなく名誉に根ざす。
4. **「アリステイアは感情の極限集中が生む一時的神域」**。英雄は平常限界を超える「絶頂状態」に入り、潮流を一人で変える。→ 個人の感情状態が会戦の流れに介入する非線形要素。
5. **「英雄の死は組織を超えて名声(kleos)として残る」**。短命でも栄光の死は次世代の戦意に火を点ける。→ 英雄の死が「失われた指揮官」でなく「継承される動機」になる設計。
6. **「終戦は条件ではなく疲弊と交渉で来る」**。10年の包囲戦＝士気と補給の消耗戦。和解と身代金は武力より外交が解決する。→ `DiplomacyRules`/`WarGoalRules` への長期戦の疲弊モデル接続。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`HonorRules`(KIKU-4)/`LoyaltyRules`/`BattleAllegianceRules`/`OrderOfBattle` を作り直さない**。ILIDはそれらに**欠落軸を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・イリアスの signature）

#### ILID 戦利品の名誉的分配（`SpoilsRules`/`SpoilsLedger`）

- **コア**：連合が共同で獲得した戦果（撃破戦力・占領星系・鹵獲資源）を**名誉分配として割り当てる**。
  - `SpoilsRules.Distribute(pool, contributors) → allocation`（貢献度比例の公平分配）
  - `SpoilsRules.HonorDelta(allocation, expectation) → float`（期待との差分＝名誉増減）
  - 差分が `HonorRules.Damage`（KIKU-4）へ流れる。
- **接続**：`HonorRules`(KIKU-4) / `FleetRoster`(貢献者確認) / `MeritRankRules`(軍功評価と連動) / `EventEngine`(不公平分配イベント発火)。
- 純ロジック・test-first・EditModeテスト必須。

#### ILID 怒りと公然撤退（`WrathRules`/`WrathState`）

- **コア**：`HonorState.honor` が `wrathThreshold` 以下かつ侮辱主体が連合盟主 → **怒り状態(wrath)**。
  - `WrathState { isWrathed: bool; sourceId: string; demandsMet: bool }`
  - `WrathRules.TriggerWrath(honor, insultSource) → WrathState`（閾値判定）
  - `WrathRules.IsWithdrawn(wrath) → bool`（怒り中は `IsFighting=false`＝戦わない）
  - `WrathRules.TryReconcile(wrath, offer) → WrathState`（補償提示→和解評価・`RollWrath(offer)`）
  - `WrathRules.ReconcileChance(offer, originalInsult) → float`（補償の充足度で和解確率）
- **接続**：`HonorRules`(KIKU-4) / `BattleAllegianceRules`(IsFightingゲート) / `LoyaltyRules`(wrath=loyalty_floor) / `EventEngine`(和解提示イベント)。
- **後方互換**：`WrathState` 未割当なら従来動作（wrath無し）。
- 純ロジック・test-first・EditModeテスト必須。

### ★★ 高（連合戦争の非線形要素）

#### ILID アリステイア（英雄の絶頂状態）（`AristeiaMomentRules`/`AristeiaState`）

- **コア**：特定の感情的契機（盟友の戦死・怒り解除直後・絶望的劣勢）で**一時的超強化状態**に入る。
  - `AristeiaState { active: bool; durationRemaining: float; trigger: AristeiaTrigger }`
  - `AristeiaMomentRules.CanTrigger(admiral, battleContext) → bool`
  - `AristeiaMomentRules.AttackMultiplier(state) → float`（既定1.5：攻撃超強化）
  - `AristeiaMomentRules.MoraleAuraRadius(state) → float`（周囲の友軍士気向上）
  - `AristeiaMomentRules.Tick(state, dt) → AristeiaState`（持続時間減衰）
- `AdmiralSkillRules`の延長（スキル効果の一種）だが、**通常スキルが「条件付き修正子」なのに対し、アリステイアは「時間限定・感情起因・一時的な別次元強化」**という差分。
- **接続**：`AdmiralSkillRules`/`AdmiralData` / `FleetMorale` / `ShipCombat.ComputeDamage`(乗算) / `EventEngine`(アリステイア発火イベント)。
- 純ロジック・test-first・EditModeテスト必須。

#### ILID 連合の名誉的指揮権（`CoalitionCommandRules`）

- **コア**：連合盟主の `HonorState.honor` が**諸侯の信頼閾値**を下回ると命令拒否が可能になる。
  - `CoalitionCommandRules.CommandAuthority(leaderHonor, params) → float`（0..1）
  - `CoalitionCommandRules.ObedienceChance(commandAuthority, vassal) → float`（諸侯が命令に従う確率）
  - `CoalitionCommandRules.CanRefuse(commandAuthority, vassalHonor) → bool`（名誉ある諸侯ほど拒否権を持つ）
- `OrderOfBattle` は**階級**による命令系統。`CoalitionCommandRules` は**名誉(timē)**による権威系統——両者は並立（階級が十分でも名誉が低ければ拒否される）。
- **接続**：`HonorRules`(KIKU-4) / `OrderOfBattle` / `LoyaltyRules`(拒否の旗幟反映) / `BattleAllegianceRules`。
- 純ロジック・test-first・EditModeテスト必須。

### ★ 中（名声遺産・世界観lore）

#### ILID 英雄の名声遺産（`KleosRules`/`KleosRecord`）

- **コア**：提督の死後に「名声(kleos)」が組織の結束と後継世代の戦意に影響を与える。
  - `KleosRecord { admiralId: string; kleos: float }`（死亡時点で確定）
  - `KleosRules.AccrueKleos(admiral, battleHistory) → float`（戦功×英雄的死で加算）
  - `KleosRules.OrganizationCohesionBonus(record, org) → float`（創設者クレオスが組織の結束を底上げ）
  - `KleosRules.MoraleBonusOnRemembrance(record) → float`（記念日/crisis時に士気援護）
- `SuccessionRules`（カリスマの日常化 #812）の**感情的/物語的側面の補強**。制度化は引継ぎ率、クレオスは感情的遺産として並立。
- **接続**：`Organization`/`SuccessionRules`(#812) / `LifecycleRules`(死亡時) / `DisclosureLedger`(FND-4・開示データ)。
- 純ロジック・test-first・EditModeテスト必須。

#### ILID（lore）世界観の開示データ

- 「連合は名誉で維持され、論功行賞の失敗で崩れる」「英雄時代の終焉＝個人の絶頂から制度へ」「短命の栄光 vs 長命の忘却＝kleos vs nostos」。
- **コード新設せず** `DisclosureLedger`（FND-4）への**lore データ入力**。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 名誉状態(HonorState)の基盤 | **KIKU-4 `HonorRules`/`HonorState` が既にカバー**。ILIDは分配+怒り+クレオスの差分のみ足す |
| 個人の義理負債・恥の可視性 | **KIKU-1〜3 がカバー**。接続のみ |
| 捕虜・身代金 | **`CaptivityRules`(#154) がカバー**。接続のみ |
| 外交条約・戦争目標の計算 | **`TreatyRules`/`WarGoalRules`(DIP-2/3)がカバー** |
| 神の直接介入（運命/予言） | **`EventEngine`/`DisclosureLedger` でカバー**。新規ルール不要 |
| 英雄個人の決闘（1対1） | 現状の会戦モデル（部隊単位）と設計思想が異なる。将来の戦術演出層（別EPIC） |
| 個別武器・防具の固有パラメータ | タイクン化（マイクロ操作）回避のため不採用 |

---

## 3. EPIC #ILID の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。既存の `HonorRules`/`LoyaltyRules`/`OrderOfBattle` は**後退させず接続のみ**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #2190**。GitHub issue 起票済み（#2193〜#2209）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **ILID-1** | #2193 | 戦利品の名誉的分配（`SpoilsRules`/`SpoilsLedger`・連合戦果の公平/不公平分配→`HonorRules`差分入力） | `HonorRules`(KIKU-4) / `FleetRoster` / `MeritRankRules` |
| **ILID-2** | #2196 | 怒りと公然撤退（`WrathRules`/`WrathState`・名誉剥奪→ストライキ・和解ルール） | `HonorRules`(KIKU-4) / `BattleAllegianceRules` / `LoyaltyRules` |
| **ILID-3** | #2199 | アリステイア（`AristeiaMomentRules`/`AristeiaState`・感情起因の一時超強化） | `AdmiralSkillRules` / `FleetMorale` / `ShipCombat.ComputeDamage` |
| **ILID-4** | #2202 | 連合の名誉的指揮権（`CoalitionCommandRules`・盟主の名誉低下→諸侯の命令拒否） | `HonorRules`(KIKU-4) / `OrderOfBattle` / `LoyaltyRules` |
| **ILID-5** | #2206 | 英雄の名声遺産（`KleosRules`/`KleosRecord`・死後に組織と後継世代へ伝播） | `Organization`/`SuccessionRules`(#812) / `LifecycleRules` / `DisclosureLedger` |
| **ILID-6** | #2209 | （lore）世界観の開示データ（連合の脆さ/英雄時代の終焉/kleos vs nostos） | `DisclosureLedger`(FND-4)。コード新設なし |

### 推奨着手順

`ILID-1 → ILID-2`（分配→怒り：イリアスの signature＝論功行賞の失敗が最強戦力を失う連鎖）→ `ILID-4`（名誉的指揮権：ILID-1/2 の帰結として連合が揺らぐ上位視点）→ `ILID-3`（アリステイア：戦闘層への接続）→ `ILID-5`（クレオス：長期の名声遺産）→ `ILID-6`（lore）。

> いずれも `HonorRules`(KIKU-4)/`LoyaltyRules`/`BattleAllegianceRules`/`OrderOfBattle` を**後退させず additive に接続**する設計。銀英伝の「提督が戦わなければ勝てない」という核心に最も効く連合戦争テクスチャを補強する。
