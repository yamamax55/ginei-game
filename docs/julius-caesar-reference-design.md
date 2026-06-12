# シェイクスピア『ジュリアス・シーザー』参考設計（EPIC #CAES）

> 参照元：ウィリアム・シェイクスピア『ジュリアス・シーザー』（Julius Caesar, c.1599）。
> 独裁者暗殺の陰謀・演説による世論の即時逆転・三頭政治の瓦解——「共和制 vs 独裁」の古典的政治劇。
> 本ドキュメントは当プロジェクト（Ginei＝銀英伝風の星間国家戦略）に**役立つ政治・権力の構造パターンのみ**を抽出する。
> 著作権注意：固有名・文章・キャラクター・固有設定は一切流用しない。**メカニクス／世界観の構造パターンのみ**参考。
>
> ※ `docs/caesar-gallic-wars-reference-design.md`（軍事遠征記）とは別作品・別視点。

---

## 0. なぜ「ジュリアス・シーザー」が本システムに役立つか

### 既存（マクロ・抽象）— カバー済み

| 既存モジュール | カバー範囲 |
|---|---|
| `CoupRules` (#215-219) | クーデター成功率・逮捕・正統性喪失 |
| `LoyaltyRules`/`Allegiance` (#817) | 関ヶ原型旗幟・寝返りカスケード |
| `Organization`/`SuccessionRules` (#812) | 英雄死後の組織存続・制度化 |
| `DynastyRules`/`Regime` (#867) | 天命喪失・腐敗・革命・王朝交代 |
| `ConsentRules`/`Polity` (#836) | 合意・非協力・統治不能 |
| `NonviolenceRules`/`Movement` (#831) | 可視化された弾圧→支持転換 |
| `SecurityRules`/`SecurityApparatus` (#166) | 密告・クーデター検知・抑圧 |
| `EspionageRules` | 諜報ネットワーク・任務成功率 |
| `GameTheoryRules` (#388) | Nash均衡・囚人のジレンマ（2プレイヤー） |
| `DiplomacyRules` (#189) | 二国間同盟・外交状態・宣戦 |
| `CivilianControlRules` (#145) | 文民統制・クーデターリスク |
| `ConstitutionRules`/`Constitution` (#170) | 制約権力・立憲主義 |
| `PowerRules`/`PowerActor` (#164) | 実権・影の支配者・傀儡判定 |
| `OfficeRules`/`GovernmentRegistry` (#142/144) | 役職・任命・元老院型議会制 |

### シェイクスピア『ジュリアス・シーザー』が固有に持つ視点 × 当プロジェクトでの欠落

| 作品固有の視点 | 当プロジェクトでの欠落 |
|---|---|
| **演説による即時の世論逆転**（葬儀演説：敵対した群衆を数分で反転させる「弁士の技」） | `ConsentRules` は緩慢なドリフト、`NonviolenceRules` は弾圧の可視化。**技能×文脈で即時に世論が変わる演説のゲームロジック**が無い |
| **陰謀の拡大・解体動学**（2人→60人に拡大するほど能力は増えるが露見リスクも増す） | `EspionageRules` は諜報任務の個別解決。**秘密結社が成員を増やしながら能力と露見リスクをトレードオフする内部動学**が無い |
| **N≥3 連合の崩壊**（三頭政治：どの2勢力も残りの1を排除するインセンティブを持ち、必然的に瓦解） | `DiplomacyRules` は二国間。**シャプレー値に基づく3者以上の連立離脱インセンティブの定量化**が無い |
| **ポピュリズムの制度バイパス**（大衆人気が元老院の拒否権を合法的に無力化する「制度の形骸化」） | `ConstitutionRules.ConstrainedAuthority` は形式的制約。**民衆動員で合法的に制度を空洞化するポピュリスト動学**が無い |
| **不可逆コミットメント**（退路を断つことで脅威に信頼性を付与するルビコン型戦略） | `EventEngine` は非繰り返しイベント。**退路遮断→脅威信頼性上昇**の純ロジックが無い |

**結論**：この作品は当プロジェクトの権力/統治層に対し、
①演説の政治力学 ②陰謀の動学 ③多者連合の不安定性 ④ポピュリズムの制度侵食 ⑤不可逆コミットメント
という5つの欠落軸を与える。いずれも **`CoupRules`/`DiplomacyRules`/`ConsentRules` の隙間**を埋める補完（additive）であり、既存モジュールを後退させない。

---

## 1. 役に立つ視点（要約）

本作の世界観を**本システムに効く形**で1行ずつ：

1. **演説は制度を超える武器**。一人の弁士が群衆を支配し、民心を短時間で反転させる。→ `ConsentRules` の緩慢ドリフトと `EventEngine` の単発イベントの間にある「技能駆動の即時世論操作」を追加。
2. **陰謀は規模と露見リスクのトレードオフを内包する**。成員を増やすほど能力は上がるが秘密は漏れやすくなる。→ `CoupRules` の**前段**となる陰謀招集フェーズを純ロジック化し test-first で固定。
3. **三者連合は二者連合に必然的に収束する**。どの3者も2対1圧力から逃れられない。→ `GameTheoryRules` を2プレイヤーから N プレイヤー連合へ拡張。
4. **大衆人気は制度的拒否権を空洞化する**。違法でも非協力でもなく「合法的な形骸化」——これがポピュリズムの核心。→ `ConstitutionRules` の制約権力に人気駆動の侵食を追加。
5. **退路を断つことが交渉力に転化する**（ルビコン型コミットメント）。不可逆を宣言することで相手の計算を変える。→ `GameTheoryRules.TitForTat` に不可逆コミットの信頼性ゲームを接続。
6. **高潔な動機から生まれる悲劇的誤算**——善意の政変が逆に体制を破壊する逆説。→ `DisclosureLedger` への世界観 lore 入力（コード新設なし）。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`CoupRules`/`DiplomacyRules`/`GameTheoryRules`/`ConsentRules` を作り直さない**。
> CAES はそれらの**隙間を欠落軸で補完**するだけ（additive）。

### ★★★ 最優先（真の欠落・本作の signature）

#### CAES-1：RhetoricRules — 演説の政治力学

- **技能モデル**：`speakerSkill`（0..100）×`audienceEmotionalState`（感情変動係数）×`contextTiming`（危機中・平時・追悼等）→ `opinionShift`（世論の即時変動量・signed）
- **フレーミング効果**：同じ事実でも「英雄の意志」vs「暴君の野望」という提示順序・言葉で聴衆の解釈が変わる。`FramingBias` が `audienceReceptivity` を乗算し最終 `opinionShift` を増減
- **戦略的運用**：高スキルの雄弁家が演説を「行動」として行使。一回のスピーチが `ConsentRules.cooperation` や `PartyRules.support` を急変させる（緩慢ドリフトの高速版）
- 接続先：`ConsentRules.cooperation`（ドリフト加速）／`PartyRules.support`（急変動）／`EventEngine`（演説イベントに技能ゲートを追加）／`MovementRules`（支持転換の高速路）
- 純ロジック新設 → **EditMode テスト必須**

#### CAES-2：ConspiracyRules — 陰謀の拡大・解体動学

- **データ構造**：`Conspiracy { size, cohesion, capability, detectionRisk, locked }`。`locked = true` なら解体不可（実行確定フェーズ）
- **成員招集**：`Recruit(person)` → `capability ↑`・`cohesion ↓`・`detectionRisk ↑`（規模の二乗に比例。60人の陰謀は2人の30倍以上危ない）
- **露見判定**：`IsExposed(conspiracy, securityStrength)` = `detectionRisk × size² / cohesion > threshold`
- **実行移行**：`Execute(conspiracy)` → `CoupRules.Resolve` へ渡す（能力値を補正として注入）
- **崩壊パス**：`IsExposed` が true → `BurstConspire(conspiracy)` = 首謀者逮捕・支持激減
- 接続先：`EspionageRules`（諜報で陰謀を検知）×`SecurityRules.CoupDetectionChance`（露見閾値）×`CoupRules`（実行）
- 純ロジック新設 → **EditMode テスト必須**

### ★★ 高優先（マクロ政治の動的拡張）

#### CAES-3：CoalitionStabilityRules — N≥3 連合の崩壊動学

- **シャプレー値**：N 勢力の連合において各勢力の限界貢献度 `ShapleyValue(coalition, faction)` を計算（N≤4 の近似で十分）
- **2対1圧力**：`TwoVsOnePressure(coalition)` = 最小シャプレー値の勢力への「排除合意形成確率」。差が大きいほど圧力上昇
- **安定指数**：`StabilityIndex(coalition)` = シャプレー値の分散の逆数（均等分配ほど安定）
- **崩壊トリガー**：圧力が閾値 `ShiftThreshold` を超えると `DiplomacyRules.BreakTreaty` を自動発火してN体連合をN-1体＋1体（離脱）に分解
- 接続先：`DiplomacyRules.SignTreaty`（二国間→N国間連立に拡張）×`GameTheoryRules.NashEquilibrium`×`FactionRelations.IsHostile`（多勢力敵対判定）
- 純ロジック新設 → **EditMode テスト必須**

#### CAES-4：PopulistDelegitimationRules — ポピュリズムの制度バイパス

- **制度空洞化モデル**：勢力の `popularSupport`（民衆支持率 0..1）が `institutionalVetoThreshold` を超えると `ConstitutionRules.ConstrainedAuthority` の実効制約を `bypassFactor`（0..1）で減殺
- **差別化**：`CoupRules`（違法・実力行使）・`ConsentRules.Withdraw`（被治者側の非協力）とは別——**合法的形式を踏みながら制度を形骸化**する経路
- **侵食ダイナミクス**：`BypassScore = Clamp((support - threshold) / (1 - threshold), 0, 1)` × `bypassStrength`。継続すると `Constitution.legitimacyModifier` が劣化し制度が恒久的に弱体化
- 接続先：`ConstitutionRules.ConstrainedAuthority`×`PartyRules.support`×`OfficeRules.CanHold`（高人気勢力の資格チェックをバイパス）
- 純ロジック新設 → **EditMode テスト必須**

### ★ 中優先

#### CAES-5：CommitmentRules — 不可逆コミットメント（ルビコン型）

- **橋焼き戦略**：`BurnBridges(action, cost)` で退路を宣言的に断つ → `ThreatCredibility = f(irreversibilityScore × observability)` が上昇
- **相手の行動変化**：高信頼性の脅威はナッシュ均衡自体を変化させる。`GameTheoryRules.Payoff` に信頼性係数を掛け込み相手の期待利得を書き換える
- **コスト**：`BurnBridges` は irreversible cost を先払い。不履行時の信頼性ペナルティは `DiplomacyRules.opinion` へ直撃（口だけの脅威より高い誓約コストが信頼性を保証）
- 接続先：`GameTheoryRules`（均衡変化）×`CoupRules`（クーデター前宣言の信頼性）×`DiplomacyRules.BreakTreaty`（条約破棄コスト前払い）
- 純ロジック新設 → **EditMode テスト必須**

#### CAES-6：(lore) DisclosureLedger — 英雄の亡霊・共和制の終焉

- 「善意の政変者が共和制を壊す」（悲劇的誤算）
- 「制度は英雄の死後を超えて続くか——カリスマの日常化の失敗例」
- 「不死者の孤独と歴史の繰り返し」（#812 組織存続テーマの鏡像）
- 接続先：**コード新設なし**。`DisclosureLedger`（FND-4 #495）への lore データ入力のみ。`CCX-6` 世界観 Codex 方針に一貫

### ❌ 不採用（重複・既存でカバー）

| 不採用 | 理由 |
|---|---|
| クーデター本体 | **`CoupRules` (#215-219) が既にカバー**。CAES-2 はその前段（陰謀招集フェーズ）のみ追加 |
| 軍の個人忠誠（私兵化・軍の帝国化） | **`LoyaltyRules`/`Allegiance` (#817) + `CivilianControlRules` (#145) がカバー** |
| 政変後の国制再設計 | **`ConstitutionRules` (#170) がカバー** |
| 英雄死後の組織崩壊と継承 | **`SuccessionRules`/`Organization` (#812) がカバー**。CAES-6 lore で接続のみ |
| 元老院式議会の議事手続き | **`OfficeRules`/`GovernmentRegistry` (#142/144) がカバー** |
| 暗殺の個人スキル（剣技・護衛突破） | 当プロジェクトは純政治ロジック層が主眼。戦術レベルの暗殺スキルは対象外 |

---

## 3. EPIC #CAES の子Issue（採用分・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UI へ配線。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #2207**。GitHub issue 起票済み（#2210〜#2218）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **CAES-1** | #2210 | 演説の政治力学（`RhetoricRules`：技能×文脈→世論即時変動） | `ConsentRules`×`PartyRules`×`EventEngine` |
| **CAES-2** | #2212 | 陰謀の拡大・解体（`ConspiracyRules`：成員招集→能力↑×露見リスク↑） | `EspionageRules`×`SecurityRules`×`CoupRules` |
| **CAES-3** | #2214 | N≥3 連合の崩壊（`CoalitionStabilityRules`：シャプレー値×2対1圧力） | `DiplomacyRules`×`GameTheoryRules`×`FactionRelations` |
| **CAES-4** | #2216 | ポピュリズムの制度バイパス（`PopulistDelegitimationRules`：人気→制度空洞化） | `ConstitutionRules`×`PartyRules`×`OfficeRules` |
| **CAES-5** | #2217 | 不可逆コミットメント（`CommitmentRules`：橋焼き→脅威信頼性） | `GameTheoryRules`×`CoupRules`×`DiplomacyRules` |
| **CAES-6** | #2218 | (lore) 英雄の亡霊・共和制の終焉（`DisclosureLedger` への lore 入力） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順

`CAES-2`（陰謀の拡大＝`CoupRules` 前段・本作の最大 signature）
→ `CAES-1`（演説＝陰謀が成った後の正統化・世論工作に直結）
→ `CAES-3`（多者連合の安定性＝戦略マップで即使える `GameTheory` 拡張）
→ `CAES-4`（ポピュリスト動学＝制度×人気の長期侵食）
→ `CAES-5`（コミットメント理論＝`GameTheoryRules` の拡張として最後に統合）
→ `CAES-6`（lore。コード不要でいつでも追加可）

> いずれも既存の権力/統治モジュールを**後退させず接続**する additive 設計。
> `CoupRules`（#215-219）・`DiplomacyRules`（#189）・`GameTheoryRules`（#388）に最も効く。
