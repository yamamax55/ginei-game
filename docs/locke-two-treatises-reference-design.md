# ロック『統治二論』参考設計（EPIC #LOCK）

> 参照元：ジョン・ロック『統治二論』（1689）。**自然権（生命・自由・財産）＝政府に先立つ権利、統治者は人民の信託を受けた代理人に過ぎず、信託を侵犯すれば権力は解消される**という近代立憲主義・自由主義の基礎。
> 本ドキュメントは、当プロジェクト（Ginei）にとって**役に立つ視点**だけを抽出し、EPIC `#LOCK` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**メカニクス／世界観の構造パターンのみ**を参考にする。

---

## 0. なぜ「統治二論」が本システムに役立つか

当プロジェクトは政治・統治の**マクロ純ロジックを大量に保有**している（[CLAUDE.md] 参照）：

| 既存（カバー範囲） | カバー内容 |
|---|---|
| `ConsentRules`/`Polity`（#836/#837） | 統治の被支配者合意・非協力・統治不能 |
| `MagnaCartaRules`/`Charter`（#624） | 制約権力・課税同意・抵抗権・慣習法化 |
| `ConstitutionRules`/`Constitution`（#170） | 制約権力・権利→正統性・立憲君主制 |
| `CoupRules`（#215-219） | クーデター成功率・政変タイプ・粛清/内戦 |
| `SeparationOfPowersRules`（#171） | 三権分立・牽制均衡・専制リスク |
| `OWN#1032`（OWN-1〜9） | 資産所有権・家産制→法治・恣意没収 |
| `ColonizationRules`（#129） | 未占有居住可能星系の入植 |
| `WarGoalRules`/`CasusBelli`（DIP-3 #192） | 厭戦・正戦の正統性・講和 |

**しかし、これらは制度・抑制・権力の構造を扱うが、ロックが固有に描く以下が欠けている**：

| ロック固有の視点 | 当プロジェクトでの欠落 |
|---|---|
| **労働財産論**（労働を混ぜた者に財産権が生まれる） | OWN#1032は所有の移転を扱うが、財産権の**起源・創出**が無い。入植地の正統性根拠（なぜその星系を持つ権利があるか）が薄い |
| **先占と共有地（コモンズ）**（未開拓地は人類共有、労働で私有化） | `ColonizationRules`は居住可否だけ見るが、コモンズ vs 先占的私有の**緊張**（封建地からの囲い込みに相当する宇宙版）が無い |
| **信託政府論の解消連鎖**（長い侵犯の積み重ね→信託解消→政府解消） | `MagnaCartaRules.ResistanceTriggered` は抵抗権を持つが、**侵犯の蓄積→段階的崩壊**という因果チェーンが無い。単発でも解消し得る曖昧さがある |
| **フォーレイチャー（権利喪失）**（先に侵犯した者は自らの権利を失う） | `CasusBelli` に自然法的根拠（先に侵犯した側が権利を失う）が無い。`WarGoalRules.GoalLegitimacy` に自然法ボーナスが無い |

**結論**：ロック『統治二論』は当プロジェクトに欠ける**①財産権の起源（労働財産論・コモンズ）②信託解消の因果連鎖③自然法的正戦（フォーレイチャー）**の3軸を与える。既存の `OWN`/`ConsentRules`/`ColonizationRules`/`WarGoalRules` に「どこから来たか・なぜ崩れるか」という哲学的裏打ちを与え、政治崩壊の因果をより精密にする。

---

## 1. 役に立つ視点（要約）

ロック『統治二論』の世界観を、**本システムに効く形**で1行ずつ：

1. **財産は国家が与えるのではなく、労働から生まれる**。入植者の正当権利・収奪の正当性・国有化の不当性の根拠に。→ `OWN#1032`/`ColonizationRules` の哲学的基盤として機能する。
2. **未開拓地はコモンズ（人類共有財）、改良した者が権利を持つ**。入植・開拓ロジックに「先占＋改良量で権利強度が決まる」テクスチャを与える。
3. **統治者は人民の信託を受けた代理人に過ぎない。信託を長期的に侵犯すれば統治は解消される**。腐敗→崩壊の連鎖に「信託侵犯の積み重ね」という因果軸を挿入。`DynastyRules.腐敗` + `ConsentRules.Withdraw` を精密化。
4. **侵略者は自らの権利を失う（フォーレイチャー）**。先に侵犯した側は正当防衛の対象となり、戦争目標が最大正統性を持つ。`WarGoalRules.GoalLegitimacy` に自然法根拠を加える。
5. **財産・自由・生命は切り離せない三権束**（life, liberty, estate）。既存の個別モジュールを「三権束への侵犯」という共通軸で横断的に接続できる。
6. （lore）**「王権神授から自然権へ」は啓蒙の転換点**。`DisclosureLedger` の秘史「神権統治の終焉→自然権の発見」に接続できる。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**ConsentRules/MagnaCartaRules/OWN#1032/CoupRules を作り直さない**。LOCK はそれらに**欠落軸を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・ロックの signature）

#### LOCK-1 労働財産論と先占権（`PropertyOriginRules`）
- **コモンズ判定**：`IsCommon(province)` = 未改良・無占有の星系/惑星。コモンズは誰でも入植可（`ColonizationRules.CanColonize` に接続）。
- **先占・改良スコア**：`ClaimStrength(province, faction)` = 入植進捗・建設投資・居住年数に比例した権利強度（0..1）。
- **没収の不当性係数**：高 ClaimStrength の土地を力で奪うと `LegitimacyDelta` がペナルティ → `GovernanceRules.OnOccupied` の初期安定度をさらに低下。
- 接続：`OWN#1032`（所有権の根拠に労働軸を加算）、`ColonizationRules`（未開拓スター系＝コモンズ）、`GovernanceRules`（占領正統性）。

#### LOCK-2 信託解消連鎖（`TrustMandateRules`）
- **信託侵犯カウント**：統治者の侵犯行為（課税同意なし・財産没収・言論抑圧・軍の民への向け直し・条約破棄）を `ViolationRecord` として蓄積。
- **解消閾値**：`TrustDissolved(state)` = 蓄積点 ≥ `dissolveThreshold` または 単一の重大侵犯（既存 `MagnaCartaRules.ResistanceTriggered` に連動）。
- **段階的効果**：侵犯蓄積中→正統性低下 / 解消後→`ConsentRules.Withdraw` の発動コスト低下 + `CoupRules.CoupSuccessChance` 上昇（反乱側が正統化される）。
- 接続：`ConsentRules`/`MagnaCartaRules`/`DynastyRules.腐敗`/`CoupRules.WouldCoup`。

### ★★ 高（自然法的正戦への接続）

#### LOCK-3 フォーレイチャーと自然法的正戦（`ForfeitureRules`）
- **先侵犯の記録**：`RecordAggression(faction, violationType)` = 無宣告攻撃・財産没収・条約破棄などを記録（`DiplomacyRules` が発火）。
- **フォーレイチャー判定**：`IsAggressor(faction)` + `ForfeitureCasusBelli(attacker, victim)` = 自然法的正戦根拠を返す。
- **正統性ボーナス**：`WarGoalRules.GoalLegitimacy` に自然法ボーナス（最大+0.3）= 相手が先に侵犯した戦争は「最大正統性」。厭戦蓄積を遅くする副次効果。
- 接続：`WarGoalRules`/`CasusBelli`（DIP-3 #192）、`DiplomacyRules`（#189 DIP-1）。

### ★ 中（世界観データ）

#### LOCK-4 （lore）自然権・信託政府の啓蒙的開示
- 「財産は労働から生まれる」「統治者は信託代理人」「人民に抵抗権がある」をゲーム内啓蒙秘史として `DisclosureLedger` へ入力。
- 条件：`TrustMandateRules.TrustDissolved` 発生、または `PropertyOriginRules.ClaimStrength` が初めて剥奪される政変など。
- 接続：`DisclosureLedger`（FND-4）。**コード新設なし**。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 立憲君主制・権利章典の実装 | `ConstitutionRules`/`Constitution`（#170）と `MagnaCartaRules`（#624）が既にカバー |
| 課税同意の新設 | `MagnaCartaRules.TaxRequiresConsent`（#624）が既にカバー |
| 抵抗権の新設 | `MagnaCartaRules.ResistanceTriggered`（#624）が既にカバー |
| 三権分立の新設 | `SeparationOfPowersRules`（#171）が既にカバー |
| 政党・選挙の新設 | `PartyRules`（GOV-6）が既にカバー |
| クーデターの新設 | `CoupRules`（#215-219）が既にカバー |
| 家産制→法治の具体実装 | `OWN#1032`（OWN-1〜9）が設計済み。LOCK は根拠・起源を加えるだけ |
| 自然状態の具体シミュ | タイクン化回避。コモンズ判定（LOCK-1）で哲学的要点は代替できる |

---

## 3. EPIC #LOCK の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UI へ配線。既存政治ロジックは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

**EPIC = #1444**。GitHub issue 起票済み（#1447〜#1455）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **LOCK-1** | #1447 | 労働財産論と先占権（`PropertyOriginRules`・コモンズ/ClaimStrength） | `OWN#1032`×`ColonizationRules`×`GovernanceRules` |
| **LOCK-2** | #1450 | 信託解消連鎖（`TrustMandateRules`・侵犯蓄積→信託解消→反乱正当化） | `ConsentRules`×`MagnaCartaRules`×`DynastyRules`×`CoupRules` |
| **LOCK-3** | #1452 | フォーレイチャーと自然法的正戦（`ForfeitureRules`・先侵犯記録→GoalLegitimacy+0.3） | `WarGoalRules`/`CasusBelli`（#192）×`DiplomacyRules`（#189） |
| **LOCK-4** | #1455 | （lore）自然権・信託政府の啓蒙的開示 | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`LOCK-1 → LOCK-2`（財産論と信託論は表裏一体＝財産への侵犯が信託解消の主因）→ `LOCK-3`（フォーレイチャー＝財産侵犯→正戦の自然法回路）→ `LOCK-4`（lore はいつでも可）。

> いずれも既存政治・財産ロジックを**後退させず接続**する additive 設計。政治崩壊の因果精密化と正戦根拠の自然法化が主眼。
