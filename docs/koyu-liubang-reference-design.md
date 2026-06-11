# 司馬遼太郎『項羽と劉邦』参考設計（EPIC #KORY）

> 参照元：司馬遼太郎著『項羽と劉邦』。楚漢戦争（紀元前206〜202年）を舞台に、
> **項羽（最強の武力）と劉邦（最大の人望）の決定的非対称**を描く歴史大河。
> 「なぜ天才軍人が敗れ、凡庸な男が天下を取ったか」——人が集まる理由・去る理由という
> 陣営の引力モデルが本作の核心。
> 本ドキュメントは、当プロジェクト（Ginei＝多勢力戦略×社会シミュ）にとって
> **役に立つ視点だけ**を抽出し、EPIC `#KORY` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**メカニクス／世界観の構造パターンのみ**参考。

---

## 0. なぜ『項羽と劉邦』が本システムに役立つか

当プロジェクトは人物・忠誠・組織継続に関する**純ロジックを大量に保有**している：

| 既存（カバー範囲） | 担当モジュール |
|---|---|
| 旗幟・寝返りカスケード | `LoyaltyRules`/`BattleAllegianceRules`（#817） |
| 国家状態↔諸侯の忠誠連結 | `FactionLoyaltyRules`（#817・腐敗→寝返り） |
| 人物・適材適所（軍才/文才） | `PersonRules`/`Person`（#866） |
| 艦隊指揮班3ネームド（参謀補完） | `CommandStaffRules`（#885）・`AdmiralData.staffOfficers` |
| 王朝腐敗・天命喪失 | `DynastyRules`/`Regime`（#867） |
| カリスマの日常化・英雄死後の組織存続 | `SuccessionRules`/`Organization`（#812） |
| 政府役職・任免・資格制限 | `OfficeRules`/`GovernmentRegistry`（GOV-1/3） |
| 軍功授爵（インセンティブ体系） | `MeritRankRules`（#900-905） |
| 捕虜・処断・登用 | `CaptivityRules`（#154） |
| 士気・敗走・回復 | `FleetMorale`（会戦層） |
| 包囲・ZOC減速 | `ZoneOfControl`/`FleetMovement`（#81） |
| 三国志演義：献策・個人盟誓 | SGZ-1〜6（#1103〜#1108）— **人材吸引力を明示的にKORYへ委譲** |

**しかし、これらは「制度・均衡・能力の静的記述」が中心**であり、
『項羽と劉邦』が固有に描く以下が**欠けている**：

| 『項羽と劉邦』が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **声望（陣営の人材磁力）** | `LoyaltyRules` は「忠誠を失う過程」。**才能ある自由エージェントが「どの陣営に就くか」を選ぶ引力モデル**（声望＝才人を呼び寄せる重力場）がない |
| **器量（才人を使える容量）** | `PersonRules.Effectiveness` は本人の適性。**指導者が自分より優秀な部下を活かせるか否か**（=器量）という上位修正子がない |
| **大義名分の競合（外部正統性の共有）** | `DynastyRules.legitimacy` は勢力内部の正統性。**複数勢力が同一の外部権威（義帝・天子）を代弁するという競合構造**がない |
| **背水の陣（撤退不能の覚悟）** | `FleetMovement.BeginRetreat` は退路あり前提。**意図的に退路を断つことで戦闘力を最大化する覚悟コミットメント**がない |
| **四面楚歌（包囲×心理戦の士気崩壊）** | `FleetMorale` は交戦・被弾による低下。**物理包囲に心理的孤立が重なったときの指数的士気崩壊**がない |
| **功臣処遇ジレンマ** | `CaptivityRules` は捕虜処遇。**勝利後の有力功臣をどう扱うか**（厚遇→中央集権の脅威、粛清→支持喪失）という安定化ジレンマがない |

**結論**：『項羽と劉邦』は当プロジェクトの人物・忠誠層に
**「人が集まる理由・去る理由」という陣営引力の動態モデル**という核心欠落を埋め、
さらに①**器量（才人を使える容量）**②**大義名分の競合**③**覚悟コミットメント**
④**心理的包囲崩壊**⑤**功臣処遇ジレンマ**という5軸を補完する。
三国志演義（SGZ）が委譲した「人材吸引力（声望モデル）」の実装先として最も適切。

---

## 1. 役に立つ視点（要約）

司馬遼太郎の描く楚漢構造を、**本システムに効く形**で1行ずつ：

1. **「人望こそ天下を取る」＝才能ある人物が自発的に集まる勢力が勝つ** — 劉邦は武勇・頭脳とも一流ではなかったが「ここについていけば生涯を捧げる価値がある」と韓信・張良・蕭何が判断した。→ 声望`PrestigeRules` の根拠。当プロジェクトの`PersonRules`（適材適所）に**「どの陣営を選ぶか」の引力を追加**。
2. **器量の非対称が歴史を決める** — 項羽は天才軍人だが自分より優秀な部下を排除した（范増放逐）。劉邦は「自分は負けるが、そいつらを使える」と自覚していた。→ `CapacityRules` で**指導者の器量が参謀・将の実効値を増幅または減衰**させる。
3. **義帝殺害は決定的な失策** — 全勢力が従う名目的権威を殺すことで、劉邦は「正義の側」に立つ大義を独占した。→ `MetaLegitimacyRules`：**外部権威の代弁者競合と、その破壊がもたらす正統性転移**。
4. **背水の陣＝退路を断つ覚悟が限界突破を生む** — 韓信が趙水攻めで意図的に退路を断ち兵に死に物狂いで戦わせた。→ `CommitmentRules`：撤退不能コミットで戦闘強化、敗北は壊滅（既存 `BeginRetreat` に対置する設計）。
5. **四面楚歌＝包囲だけでなく孤立感が魂を砕く** — 項羽は最後まで戦えたが「楚の歌が聞こえる＝故郷の民も敵に回った」という心理的囲い込みで戦意が崩壊した。→ `ZoneOfControl` × `FleetMorale` の交差に**心理的孤立修正子**を接続。
6. **功臣処遇は勝利後の最難問** — 劉邦もまた勝利後に多くの功臣を粛清せざるを得なかった。それが漢帝国の制度的脆弱性を生んだ。→ `MeritRetentionRules`：厚遇→権力集中リスク、粛清→支持喪失、という安定化ジレンマ。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`LoyaltyRules`/`PersonRules`/`DynastyRules`/`FleetMorale` を作り直さない**。
> KORY はそれらに**欠落軸を足し接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・本作の signature）

#### KORY 声望モデル — `PrestigeRules`/`PrestigeState`（陣営の人材磁力）

- **`PrestigeState`**（`Faction` ごとの純データ）：`prestige`（0..100）＋ `reputationEvents`（直近の評判イベント履歴）。
- **`PrestigeRules`**（static・Core・test-first）：
  - `EffectivePrestige(faction, FactionState)`：正統性×結束×声望イベント履歴の合成（基準値非破壊）
  - `TalentAttractionRate(prestige)`：自由エージェント `Person` がこの勢力への参加を「志願」する確率/速度
  - `DefectionPull(sourcePrestige, targetPrestige)`：才人が高声望陣営へ横断する引力（`LoyaltyRules` と乗算）
  - `PrestigeGain/Loss` イベント：勝利/大義名分遵守→UP、虐殺/約束破り→DOWN（`EventEngine` から呼ぶ）
- 接続：`PersonRules.BestFor` がこの引力で選好順位を修正 → `VacancyRules.SelectSuccessor` に影響 → `FleetRoster.AssignAdmiral` に伝播。SGZ-3 `PledgeRules`（誓約）とも接続（誓約先の声望が上がれば誓約の拘束力が強まる）。

#### KORY 器量 — `CapacityRules`/`CapacityTolerance`（才人を活かせる指導者の容量）

- **`CapacityTolerance`**：`AdmiralData`（または `Person`）への追加スカラー `capacityTolerance`（0..1）。未設定は1.0（後方互換）。
  - 高い（≈1.0）：自分より優秀な部下が最大の恩恵をもたらす（劉邦型）
  - 低い（≈0.3）：優秀な部下が力を発揮できず離脱リスクが高まる（項羽型）
- **`CapacityRules`**（static・Core・test-first）：
  - `StaffEffectivenessMultiplier(commander)`：指揮官の器量が `AdmiralData.staffOfficers` の実効値に乗ずる倍率（`CommandStaffRules.EffectiveLeadership` に掛ける）。基準値は非破壊。
  - `RetentionRisk(subordinate, commander)`：部下の能力が指揮官の器量を超える場合に離脱確率を上昇させる（`LoyaltyRules`/`PrestigeRules` に入力）
  - `LeaderPrestigeFromCapacity(tolerance, recruitedTalent)`：優秀な人材を実際に使えている状態が声望UPに繋がる
- 接続：`CommandStaffRules`（指揮班の実効能力に乗算）× `PrestigeRules`（声望フィードバック）× `LoyaltyRules`（離脱引き金）。

### ★★★ 最優先（構造の核・既存に真に欠けている）

#### KORY 大義名分の競合 — `MetaLegitimacyRules`/`MetaAuthority`（義帝問題）

- **`MetaAuthority`**（純データ・`[Serializable]`）：`authorityName`/`isAlive`/`holderFactionId`（代弁を主張する勢力）。
- **`MetaLegitimacyRules`**（static・Core・test-first）：
  - `ClaimStrength(faction, authority)`：勢力がどれだけ権威の「正当な代弁者」かを表すスコア（0..1）。義帝殺害や約束破りで激減。
  - `LegitimacyTransfer(killer, victim, authority)`：権威の代弁者を殺した場合の正統性転移（殺した側が大きく失い、反対側に吸収）
  - `PropagateLegitimacyShift`：正統性転移を `FactionLoyaltyRules.ApplyBaseline`/`DynastyRules.legitimacy` へ伝播
  - `VacancyBonus(faction, authority)`：権威が無力化されている間、最も高い `ClaimStrength` を持つ勢力が人望ボーナス
- 接続：`DynastyRules.Regime`（内部正統性に加算）× `PrestigeRules`（声望ボーナス）× `LoyaltyRules.ResolveStance`（中立諸侯の態度決定に影響）。

### ★★ 高（既存を後退させず覚悟・心理を追加）

#### KORY 背水の陣 — `CommitmentRules`（撤退不能コミットメント）

- **`CommitmentRules`**（static・Core・test-first）：
  - `CommitToNonRetreat(fleet)`：退路を断つ宣言。`FleetStrength` に `committed` フラグ。
  - `CombatBonus(committed)`：コミット中の攻撃・士気低下耐性ボーナス（`ShipCombat.ComputeDamage`/`FleetMorale` に修正子）
  - `AnnihilationOnDefeat`：コミット中に `strength=0` になると `BeginRetreat` でなく壊滅（ユニット永久除去）
  - `BreachCommitment(fleet)`：コミット解除（信用低下ペナルティ = `PrestigeRules.PrestigeLoss`）
- 接続：`FleetStrength`（`committed` フラグ保持）× `CombatModifiers`（実効値パターンで基準値非破壊）× `PrestigeRules`（コミット遂行で声望UP、離脱で声望DOWN）。

#### KORY 四面楚歌 — `PsychologicalSiegeMorale`（包囲×心理戦の士気崩壊加速）

- **`PsychologicalSiegeMorale`**（static・Core・test-first）：
  - `IsPsychologicallyBesieged(fleet, registry, FactionRelations)`：全方位を敵ZOCに包まれかつ `PrestigeState.prestige` が一定以下（孤立判定）
  - `SiegeMoraleDecayMultiplier(besieged)`：包囲判定中は通常の士気低下レートに乗数（`FleetMorale` が参照）
  - 包囲から脱出または増援到達で解除
- 接続：`ZoneOfControl.HostileIntensityAt`（包囲度）× `PrestigeRules`（孤立の判定）× `FleetMorale.GetMoraleFactor`（低下加速）。実効値パターン（基準値非破壊）。

### ★ 中（勝利後の構造的課題）

#### KORY 功臣処遇ジレンマ — `MeritRetentionRules`（功臣の安定化ジレンマ）

- **`MeritRetentionRules`**（static・Core・test-first）：
  - `VassalPowerConcentration(roster, campaign)`：有力功臣の合計実力 vs 中央勢力の比較（`FleetRoster`/`GovernmentRegistry` から算出）
  - `CentralizationThreat(concentration)`：功臣が強大化した場合の中央権力侵食リスク（`CoupRules.CoupRisk` に加算）
  - `PurgeSupport(purgedPerson, prestige)`：功臣粛清が声望に与える打撃（`PrestigeRules.PrestigeLoss`）
  - `RetentionPolicy(enum { 厚遇, 転封, 解除, 粛清 })`：政策選択に応じた安定度×声望の複合帰結
- 接続：`CaptivityRules.DefaultDisposition`（処遇方針）× `PrestigeRules`（声望帰結）× `CoupRules.CoupRisk`（反乱リスク）。

#### KORY（lore）世界観の開示データ

- 「器量の逆説＝自分より優秀な者を使える者が最後に勝つ」「人望は武勇より強い」「天下を取った後こそ最難問が始まる」。
- **コード新設せず** `DisclosureLedger`（FND-4）への**lore データ入力**。CCX-6（世界観codex退避）方針に一貫。

### ❌ 不採用（重複・既存で十分・タイクン化回避）

| 不採用 | 理由 |
|---|---|
| 武将の数値比較ゲーム（武力/知力） | `AdmiralData` の6能力で**既にカバー**。重複新設しない |
| 一騎討ち・個人英雄戦闘 | SGZ でも除外。タイクン化（マイクロ個人戦）を招く |
| 楚漢の具体的地名・地形の再現 | `GalaxyMap` で**既にカバー**。世界観固有名を流用しない |
| 献策システム | **SGZ-2 `CounselRules` がカバー** |
| 個人盟誓（義兄弟） | **SGZ-3 `PledgeRules` がカバー** |
| 物量の数値操作（多い方が強い） | `FleetStrength`/`BattleManager` で既に実装。重複しない |
| 四面楚歌の歌演出（サウンド） | 会戦演出・シーン依存。純ロジックではない |
| 刺客・暗殺（鴻門の会の失敗） | `CaptivityRules`/`EspionageRules` で接続可能。KORY は「機会を使わなかった政治的理由」の純ロジックには踏み込まない |

---

## 3. EPIC #KORY の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。
> 既存モジュールは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #1404**。GitHub issue 起票済み（#1406〜#1425）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **KORY-1** | #1406 | 声望モデル（`PrestigeRules`/`PrestigeState`）— 陣営の人材磁力 | 新 `PrestigeRules`/`PrestigeState`。`PersonRules`×`VacancyRules`×SGZ-3 `PledgeRules` |
| **KORY-2** | #1409 | 器量（`CapacityRules`/`CapacityTolerance`）— 指導者が才人を活かせる容量 | 新 `CapacityRules`。`CommandStaffRules`×`LoyaltyRules`×`PrestigeRules` |
| **KORY-3** | #1411 | 大義名分の競合（`MetaLegitimacyRules`/`MetaAuthority`）— 義帝問題・外部権威の代弁競合 | 新 `MetaLegitimacyRules`。`DynastyRules`×`PrestigeRules`×`LoyaltyRules` |
| **KORY-4** | #1414 | 背水の陣（`CommitmentRules`）— 撤退不能コミットで戦闘力最大化・敗北は壊滅 | 新 `CommitmentRules`。`FleetStrength`×`CombatModifiers`×`PrestigeRules` |
| **KORY-5** | #1419 | 四面楚歌（`PsychologicalSiegeMorale`）— 物理包囲×心理孤立の士気崩壊加速 | 新 `PsychologicalSiegeMorale`。`ZoneOfControl`×`FleetMorale`×`PrestigeRules` |
| **KORY-6** | #1422 | 功臣処遇ジレンマ（`MeritRetentionRules`）— 勝利後の有力功臣の厚遇/転封/粛清の安定化帰結 | 新 `MeritRetentionRules`。`CaptivityRules`×`CoupRules`×`PrestigeRules` |
| **KORY-7** | #1425 | （lore）世界観の開示データ（器量の逆説・人望=戦わずして勝つ・功臣処遇ジレンマ） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順

`KORY-1`（声望モデル＝全KORY子issueの共通基盤）→ `KORY-2`（器量＝声望を増幅/減衰する乗数）→ `KORY-3`（大義名分＝声望の構造的源泉）→ `KORY-4`（背水の陣＝覚悟コミット）→ `KORY-5`（四面楚歌＝包囲士気崩壊）→ `KORY-6`（功臣処遇＝勝利後の安定問題）→ `KORY-7`（lore）。

KORY-1 が基盤なので先行実装を強く推奨。KORY-4/5 は会戦層への配線も伴うため後半。

> いずれも既存モジュールを**後退させず接続**する additive 設計。
> `PersonRules`/`LoyaltyRules`/`FleetMorale` を破壊しない。
