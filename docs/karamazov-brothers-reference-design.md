# ドストエフスキー『カラマーゾフの兄弟』参考設計（EPIC #DOST）

> 参照元：フョードル・ドストエフスキー著『カラマーゾフの兄弟』（1880年）。
> ドミトリー（情念）・イワン（理性）・アリョーシャ（信仰）の三兄弟と父殺しを軸に、**自由・信仰・道徳・正統性の根源問題**を問う大作。
> 本ドキュメントは当プロジェクト（Ginei＝銀英伝風の星間国家戦略＋既に巨大な社会シミュ純ロジック層）にとって**役に立つ視点**だけを抽出し、EPIC `#DOST` として issue 化する提案。
> **著作権注意：固有名・文章・キャラクター・固有設定は流用せず、統治思想・哲学的メカニクスの構造パターンのみを参考にする。**

---

## 0. なぜ「カラマーゾフの兄弟」が本システムに役立つか

当プロジェクトは社会シミュの**純ロジック層を大量に保有**している（[CLAUDE.md] 参照）：

| 既存（社会・政治シミュ） | カバー範囲 |
|---|---|
| `ConsentRules`/`Polity`（#836） | 権力は借り物・協力の撤退（ボイコット）で統治不能 |
| `HopeRules`/`Community`（#852） | 希望の枯渇→末人→秩序ルート（強権）／信仰ルート（意味の捏造） |
| `DynastyRules`/`Regime`（#867） | 天命と腐敗サイクル・易姓革命（正統性の漸進的崩壊） |
| `ReligionRules`/`Religion`（#172-175） | 改宗圧力・異端・聖戦圧力・社会効果 |
| `FactionStateRules`/`FactionState` | 王朝×統治体×組織×共同体の合成安定度 |
| `JusticeRules`（#918-923） | 功利主義/ロールズ/リバタリアン等の正義観×正統性デルタ |
| `SecurityRules`/`SecurityApparatus`（#166） | 反体制抑圧・クーデター察知・抑圧の支持コスト |

**しかし、これらは「権力の維持・腐敗・崩壊」という**機能的・プロセス的モデル**であり、カラマーゾフが固有に描く以下の構造が欠けている**：

| カラマーゾフが固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **大審問官：民衆は自由を恐れ、パン・奇跡・権威に自発的に服従する** | `ConsentRules` は権力を"借り物"と捉える（民衆が積極的に権威を必要とする自発的服従モデルが無い） |
| **「神がいなければ、すべては許される」＝超越的権威の喪失→道徳的拘束の消滅** | `DynastyRules` は腐敗速度のモデル。超越的権威崩壊後の規範拘束そのものの消滅が無い |
| **神義論トリガー：無辜の者の苦しみが可視化されると正統性が急崩壊する** | `JusticeRules` は正義観の加重和。「なぜ無辜が苦しむのか」という感情的・道徳的爆発のトリガーが無い |
| **三本柱統治（奇跡・神秘・権威）＝自由の代替としての支持調達** | `SecurityRules` は抑圧。「奇跡・神秘・権威」という積極的支持生成の三モードが無い |
| **「積極的愛」vs「観念的愛」＝直接接触ある統治vs抽象宣言だけの統治の効果差** | `GovernanceRules` に統治スタイル（直接接触の有無）が変える産出・統合速度の係数が無い |

**結論**：カラマーゾフは当プロジェクトの社会シミュに**「自由の恐怖という心理軸」**と、**①自発的服従 ②道徳的権威崩壊 ③神義論トリガー ④三本柱統治 ⑤積極的愛**という5つの欠落軸を与える。「権力の源泉と限界」を問う核心部は `ConsentRules`（権力は借り物）と対をなす**「権力は献上される」**という逆方向のモデルとして機能する。

---

## 1. 役に立つ視点（要約）

カラマーゾフの兄弟の世界観を、**本システムに効く形**で1行ずつ：

1. **「人は自由の重さに耐えられず、それを誰かに手渡す」（大審問官）**。希望が崩れた共同体は強権を拒まず*求める*。→ `HopeRules`（末人）の先に「自発的服従」という第三段階を置く。
2. **「神なき世界では道徳拘束が機能しない」（イワン「すべては許される」）**。宗教や超越的イデオロギーが腐食すると、制度や契約の"なぜ守るか"が消滅し腐敗が加速。→ `ReligionRules`崩壊×`DynastyRules`腐敗を**乗算的に連結**。
3. **「無辜の苦しみは説明できない」（神義論）**。統治の正統性は政策の合理性だけでなく、**目に見える無辜者の犠牲への答え**を求められる。→ `JusticeRules.DominantGrievance` の専用トリガーとして機能。
4. **「奇跡・神秘・権威」という三本柱で人心を掌握する（大審問官）**。自由の代わりに確実性・神秘性・絶対服従を与えると一時的に高支持が実現する。→ 正統性補完の三モードとして `SecurityRules`/`ReligionRules` に接続。
5. **「積極的愛（個人への直接の愛）vs観念的愛（抽象的な人類愛）」（ゾシマ長老）**。距離のある統治は抽象宣言のみに終わり実効産出が低い。→ `GovernanceRules.OutputFactor` の統治スタイル係数。
6. **三兄弟の類型（情念/理性/信仰）＝意思決定の三原型**。組織・国家の指導者が情念/理性/信仰のどれを基軸とするかで安定性と可能性が変わる。→ 既存 `CareerPipelineRules` の4アーキタイプに**倫理軸**を加える参照モデル。lore に格納。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`ConsentRules`/`HopeRules`/`DynastyRules`/`ReligionRules`/`JusticeRules`/`SecurityRules` を作り直さない**。DOST はそれらに**欠落軸を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・カラマーゾフの signature）

#### DOST 自発的服従（「自由vsパン」の心理転換・`VoluntaryServitudeRules`）
- **大審問官テーゼ**：希望崩壊＋高い生存不安のとき、民衆は自由を捨てて権威に服従を**自発的に求める**。これは `ConsentRules` の"協力の撤退"とは逆方向——権威への積極的な委任。
- `VoluntaryServitudeRules`/`ServitudeState`（純ロジック・test-first）：
  - `ServitudeRisk(hope, stability, freedom)`: 希望崩壊＋安定低下＋自由度高の組合せ → 服従転換リスク 0..1
  - `IsServitudeActive(state)`: 服従状態が活性かどうか
  - `ServitudeBonus(state)`: 服従中の短期安定ボーナス（民衆が秩序を支持する）
  - `LongTermFragility(state)`: 自発的服従の長期脆弱性（解放の瞬間に反発爆発 → `Polity.cooperation` 急落）
  - 接続：`ConsentRules`/`Polity`（`ServitudeBonus` が `ControlStrength` に加算） × `HopeRules`/`Community`（希望崩壊が `ServitudeRisk` に供給）

#### DOST 「すべては許される」＝道徳的拘束崩壊（`MoralAnchorRules`）
- **イワンのテーゼ**：超越的権威（神・イデオロギー）が喪失すると、"なぜ規範を守るか"の根拠が消え、腐敗加速・離反コスト消滅・約束の破棄が連鎖する。
- `MoralAnchorRules`（純ロジック・test-first）：
  - `AnchorStrength(religion, regime)`: 宗教強度×政体正統性 → 道徳的権威の強度 0..1
  - `ConstraintFactor(anchor)`: 規範拘束の実効係数 → 腐敗加速倍率・忠誠離反コストへの乗数
  - `IsAnchorCollapsed(anchor)`: 超越的権威の崩壊判定
  - `CollapseShockLoyaltyDelta`: 崩壊時の忠誠急落量
  - 接続：`DynastyRules`/`Regime`（`ConstraintFactor` が `corruption` 増分に乗算） × `ReligionRules`（宗教崩壊が `AnchorStrength` に入力） × `LoyaltyRules`（離反コストへの係数）

### ★★ 高（正統性危機の新しい回路）

#### DOST 神義論トリガー（「無辜の苦しみ」→正統性クリーシス）
- **イワンの反逆**：民間人大量死・子どもの犠牲・不条理な被害が可視化されると、政策の合理性を超えた道徳的問いが噴出し、正統性が急崩壊する。`JusticeRules.DominantGrievance` より即効・感情的。
- 実装方針：`GameEventDef`（#116 EventEngine）の専用カテゴリ `TheodiceaEvent`＋`TheodiceaShockRules`（純ロジック）：
  - `ComputeShock(casualtyCivilian, visibility, regime)`: 無辜の可視被害 → 正統性急落量
  - `IsGriefTriggered(shock, threshold)`: 発火判定
  - 接続：`EventEngine`/`GameEventDef`（イベント型） × `DynastyRules.Regime.legitimacy`（直撃デルタ） × `JusticeRules.DominantGrievance`（不満の支配的正義観を更新）

#### DOST 三本柱統治モード（奇跡・神秘・権威）
- **大審問官の統治技術**：自由の代わりに①**奇跡（Miracle）**＝製造された勝利・神話、②**神秘（Mystery）**＝情報遮断・秘儀性、③**権威（Authority）**＝絶対的上意下達——の三モードで支持を調達する。各モードは短期効果・コスト・副作用が異なる。
- `AuthorityPillarRules`/`AuthorityPillarState`（純ロジック・test-first）：
  - `enum AuthorityPillar { Miracle, Mystery, Authority }`
  - `ActivePillar(state)`: 現在使用中のモード
  - `SupportBonus(pillar, intensity)`: 各柱の支持生成量
  - `SustainCost(pillar, intensity)`: 維持コスト（财政 / 情報コスト / 反乱リスク）
  - `PillarCollapsePenalty(pillar)`: 崩壊時の反動（神話が壊れると信仰ルートより大きいショック）
  - 接続：`SecurityRules`/`SecurityApparatus`（Mystery柱 = 情報遮断の延長） × `ReligionRules`（Miracle柱 = 宗教的演出） × `Polity`（支持デルタ）

### ★ 中（統治スタイル・lore）

#### DOST 「積極的愛」vs「観念的愛」＝直接統治係数
- **ゾシマ長老の教え**：抽象的な人類愛は実際には誰も助けない。個々の人間への直接の接触・手当てがあって初めて愛（統治）は機能する。政策立案者が現場から乖離していると産出・統合が落ちる。
- 実装方針：`GovernanceRules.OutputFactor` に統治スタイル係数を追加：
  - `DirectGovernanceBonus(govStyle, province)`: 直接統治スタイル（高ゾーン省益摩擦の逆・`MinistryRules.SectionalismFriction` の逆数的補正）
  - 省益（縦割り）が高い＝観念的愛→産出低下、直接統治＝積極的愛→統合速度UP
  - 接続：`GovernanceRules.OutputFactor`/`Province.integration` × `MinistryRules.SectionalismFriction`（省益が間接性の代理指標）

#### DOST （lore）世界観の開示データ（大審問官の問い・三兄弟類型・神の不在）
- 「大審問官の問い」「神なき世界の道徳」「三兄弟の人間原型（情念/理性/信仰）」「積極的愛の倫理」。
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への**lore データ入力**。CCX-6（世界観codex）方針に一貫。

### ❌ 不採用（重複・対象外）

| 不採用 | 理由 |
|---|---|
| 小説固有の筋書き（父殺し・裁判・容疑） | プロットは参照しない。メカニクスの構造のみ |
| 三兄弟をキャラクター類型として `AdmiralData` に直接転写 | `CareerPipelineRules` の4アーキタイプ（首席型/在野俊英型/老練型/叩き上げ）で代替。lore のみ |
| ロシア正教の固有神学・修道院システム | `ReligionRules` で既にカバー |
| 恋愛・家族ドラマ（グルーシェニカ等） | 対象外 |
| 「神の存在証明」の哲学議論をゲームメカニクスに | lore のみ。存在論的議論を数値化しない |
| 「哲人王」モデルとの重複 | `ARIS-2`（アリストテレス#1491）が`PublicGoodnessRules`をカバー |
| 監視・密告システム | `SecurityRules`/`BNAL`（全体主義）が既にカバー |

---

## 3. EPIC #DOST の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。既存社会シミュは**接続のみ・重複新設しない**。
> **著作権注意：固有名・文章・キャラは不使用、統治思想・哲学メカニクスの構造のみ参考。**

> **EPIC = #2138**。GitHub issue 起票済み（#2142〜#2165）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **DOST-1** | #2142 | `VoluntaryServitudeRules`/`ServitudeState` — 自発的服従（大審問官：自由vsパン・ServitudeRisk・ServitudeBonus・LongTermFragility） | 新純ロジック。`ConsentRules`/`HopeRules` 接続。EditMode テスト必須 |
| **DOST-2** | #2147 | `MoralAnchorRules` — 「すべては許される」道徳的拘束崩壊（AnchorStrength・ConstraintFactor・CollapseShock） | 新純ロジック。`DynastyRules`/`ReligionRules`/`LoyaltyRules` 接続。EditMode テスト必須 |
| **DOST-3** | #2151 | `TheodiceaShockRules` — 神義論トリガー（無辜の可視被害→正統性クリーシス・ComputeShock・EventEngine接続） | 新純ロジック。`EventEngine`/`DynastyRules`/`JusticeRules` 接続。EditMode テスト必須 |
| **DOST-4** | #2156 | `AuthorityPillarRules`/`AuthorityPillarState` — 三本柱統治（奇跡・神秘・権威：SupportBonus・SustainCost・PillarCollapsePenalty） | 新純ロジック。`SecurityRules`/`ReligionRules`/`Polity` 接続。EditMode テスト必須 |
| **DOST-5** | #2161 | `GovernanceRules.DirectGovernanceBonus` — 積極的愛係数（直接統治スタイル×省益摩擦の逆数で産出/統合速度補正） | `GovernanceRules.OutputFactor` 拡張。`MinistryRules.SectionalismFriction` 接続。EditMode テスト必須 |
| **DOST-6** | #2165 | （lore）世界観の開示データ（大審問官の問い/神の不在と規範崩壊/三兄弟の原型/積極的愛の倫理） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`DOST-1`（自発的服従＝最も固有で `ConsentRules` と対をなす signature）→ `DOST-2`（道徳的拘束崩壊＝イワンの命題の実装）→ `DOST-3`（神義論トリガー＝正統性危機の新回路）→ `DOST-4`（三本柱統治モード）→ `DOST-5`（積極的愛係数＝GovernanceRules 拡張）→ `DOST-6`（lore）。

> いずれも既存社会シミュ純ロジックを**後退させず接続**する additive 設計。`ConsentRules`（権力は借り物）に**「権力は献上される」という逆方向のモデル**を補完するのが核心。
