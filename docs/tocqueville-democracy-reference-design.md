# トクヴィル『アメリカのデモクラシー』参考設計（EPIC #TOCQ）

> 参照元：アレクシ・ド・トクヴィル著『アメリカのデモクラシー』（1835/1840）。
> フランス人貴族が新大陸の民主主義を観察した社会科学の古典。**平等化・個人主義・多数者の専制・穏やかな専制**という4つの概念軸が現代政治学の礎となった。
> 本ドキュメントは、当プロジェクト（Ginei＝帝国 vs 同盟という民主制と専制の対立を核に持つ銀河戦略）にとって**役に立つ視点**だけを抽出し、EPIC `TOCQ` として issue 化する提案。
> 著作権注意：固有名・文章・固有設定は流用せず、**政治社会メカニクス／世界観の構造パターンのみ**を参考にする。

---

## 0. なぜ「アメリカのデモクラシー」が本システムに役立つか

当プロジェクトは政治・社会シミュレーション層を**大量に保有**している（[CLAUDE.md] 参照）：

| 既存モジュール | カバー範囲 |
|---|---|
| `SeparationOfPowersRules`（#171） | 三権分立・制度的権力集中リスク（`TyrannyRisk`/`CheckBalance`） |
| `ConstitutionRules`/`Constitution`（#170） | 制約権力・権利→正統性 |
| `ConsentRules`/`Polity`（#836） | 権力は借り物・協力と正統性・統治不能閾値 |
| `CoupRules`（#215-219） | 軍部/宮廷/革命クーデター（hard coup） |
| `CivilianControlRules`（#145） | 文民統制・軍政関係 |
| `Community`/`HopeRules`（#852-856） | 希望・末人（受動化・逃避） |
| `Organization`/`SuccessionRules`（#812） | カリスマ死後の組織存続・制度化 |
| `ReligionRules`（#172-175） | 改宗圧力・異端・聖戦 |
| `NonviolenceRules`/`Movement`（#831） | 非暴力運動・弾圧の可視化 |
| `PowerRules`/`PowerActor`（#164） | 寡頭支配・傀儡 |
| `PartyRules`/`Party`（#159） | 政党・派閥・総裁選 |
| `MinistryRules`/`Ministry`（#158） | 省庁ツリー・縦割り摩擦 |
| `CapitalRules`（#917） | 資本集中（r>g）・格差→反乱 |
| `SeniorityRules`（LIFE-5/6） | 席次vs実力・政体で変わる硬直度 |

**しかし、これらは「制度・権力・武力」を軸とした構造**であり、トクヴィルが固有に描く以下が**欠けている**：

| トクヴィルが固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **多数者の専制（social tyranny）** | `SeparationOfPowersRules.TyrannyRisk` は制度的権力集中。**民主的多数派が社会的圧力で少数意見を封じる** ——「多数派だから正しい」という不文律による意見の同質化——が無い |
| **中間団体・市民結社** | `Party`/`Ministry`/`Organization` は国家・政党・官僚組織。**国家と個人の間の自発的緩衝体**（市民協会・地域団体・業界連合）が無い |
| **民主的個人主義と孤立化** | `ConsentRules.cooperation` と `HopeRules` は協力/希望の量を測る。**平等→個人主義→公的参加撤退→集合行動の弱体化という特定の連鎖スパイラル**が無い |
| **穏やかな専制（soft despotism）** | `CoupRules` は暴力的転換。**暴力なく市民が能動的に集合意志を行政国家へ委任し受動化する**漸進的権威主義が無い |
| **平等化の潮流（equality of conditions）** | `CapitalRules` は富の集中（不平等方向）。**民主的平等化が序列/階級/身分制度を長期的に侵食する歴史的圧力**が無い |

**結論**：トクヴィルは当プロジェクトの政治シミュに**「民主制の内発的腐敗メカニクス」という欠落軸**を与える。帝国＝貴族制 vs 同盟＝民主制という Ginei の核にある対立を「どちらが正しいか」ではなく「それぞれがどう自己崩壊するか」という双方向のダイナミクスとして表現できる。これは既存モジュールへの**additive な接続**であり、新規重複は最小限。

---

## 1. 役に立つ視点（要約）

トクヴィルの観察を、**本システムに効く形**で1行ずつ：

1. **多数派が事実上の権威となり少数意見が社会的に封殺される**——王への異議申し立てより民主的多数への異議申し立ての方が困難。→ `SeparationOfPowersRules`に制度外の**社会的同質化圧力**を追加。帝国の言論統制と同盟の多数決独裁を両輪で描く。
2. **中間団体（associations）こそ民主制を専制から守る**——国家と個人の間を埋める自発的団体が弱いと、孤立した個人は国家に頼るしかなくなる。→ 新 `CivicAssociation` が `ConsentRules.cooperation` と `OrganizationRules` を媒介。
3. **平等は個人主義を生み、個人主義は孤立を生み、孤立は国家膨張を招く**——「自分さえよければ」という民主的個人主義が公共圏を空洞化させる。→ 孤立化スパイラルが `FactionState` の安定性に係数として効く。
4. **穏やかな専制は暴力なく成立する**——市民が「面倒な決断」を行政国家に委ねるとき、自由は静かに消える。→ `SoftDespotismRules` がクーデションなしに専制を達成するルートを定義。`CoupRules` の**対称系**。
5. **平等化の潮流は止められない歴史的力**——貴族制・身分制は民主的圧力に長期では抗えない。→ `EqualityDriftRules` が `RankSystem`/`SeniorityRules` に圧力係数を与え、帝国の貴族制が長期で侵食される。
6. **慣習・気風（moeurs）は法より深く社会を支える**——制度を輸出しても民主主義は根付かない。→ コード新設せず `DisclosureLedger` への lore 入力。世界観EPIC（啓蒙/秘史）と接続。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**既存の `ConsentRules`/`SeparationOfPowersRules`/`CoupRules`/`Organization` を作り直さない**。TOCQ はそれらに**欠落軸を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・トクヴィルの signature）

#### TOCQ 多数者の専制（`MajorityTyrannyRules`/`MinorityOpinion`）
- **社会的同質化圧力**：多数派支持率が閾値を超えると `minorityVoice`（少数意見の可視性・0..1）が下がる。少数意見が封じられると `innovation`（異端的アイデアの発生）と `resistanceCapacity`（反乱組織化力）が低下。
- `MajorityTyrannyRules`（pure logic）：`MajorityPressure(supportShare)` / `VoiceSupression(pressure, protection)` / `InstitutionalProtection`（三権分立・中間団体が圧力を緩和）/ `TyrannyIndex`（0..1・高いほど少数意見が消える）。
- 接続：`SeparationOfPowersRules.TyrannyRisk`（既存）に`TyrannyIndex` を乗算（制度的リスク×社会的圧力の複合）。`CivicAssociation.independence`（TOCQ-2）が `InstitutionalProtection` を底上げ。既存の制度リスクと分離して social tyranny を別軸で測る。

#### TOCQ 中間団体・市民結社（`CivicAssociation`/`AssociationRules`）
- **`CivicAssociation`**（純データ）：`scope`（星系/勢力/銀河）/ `domain`（経済/政治/文化/宗教）/ `solidarity`（結束0..1）/ `independence`（国家からの独立度0..1）/ `memberCount`。
- **`AssociationRules`**（static・純ロジック）：`FormAssociation`（勢力の統治スタイル `inclusiveness` が低いと困難） / `Atrophy`（締め付け・無関心で弱体化） / `CivicBuffer`（in-scope の結社の合計独立度→ soft despotism 圧力を相殺） / `CollectiveActionBonus`（結社があると `ConsentRules.cooperation` が上乗せ）/ `IsVanguard`（solidarity>0.8 の結社は `NonviolenceRules.Movement` の核になれる）。
- 接続：`ConsentRules.cooperation`（結社が底上げ）× `MajorityTyrannyRules.InstitutionalProtection`（結社が少数意見を守る）× `SoftDespotismRules.AtomizationIndex`（TOCQ-4・結社が弱いほど孤立化進行）。

#### TOCQ 民主的孤立化スパイラル（`AtomizationRules`）
- **スパイラルの定式化**：社会的平等度（`EqualityLevel`・TOCQ-5）が高いほど個人主義傾向 `IndividualismPressure` が高まる → 市民の公的参加意欲 `CivicEngagement`（0..1）が低下 → `CivicEngagement` 低下 = `AssociationRules.Atrophy` に圧力 → 結社弱体化 → `SoftDespotismRules`（TOCQ-4）の入力悪化 → 行政国家膨張 → さらに孤立化（外部性）。
- **`AtomizationRules`**（static・純ロジック）：`IndividualismPressure(equality)` / `CivicEngagement(polity, associations, regime)` / `AtomizationIndex`（孤立化指数0..1） / `SpiralVelocity`（スパイラルの加速・中間団体の弱さ×平等化速度） / `CircuitBreaker`（希望 `Community.hope` > 閾値 or 外敵脅威でスパイラル停止）。
- 接続：`Community.hope`（HopeRules）が `CircuitBreaker` に接続（希望があれば孤立スパイラルが止まる）。`FactionState.Stability` への係数（高孤立化 → 安定度低下）。

### ★★ 高（民主制固有の崩壊モード）

#### TOCQ 穏やかな専制（`SoftDespotismRules`）
- **定義**：暴力的クーデション（`CoupRules`）なしに、市民が**自発的に**集合意志を行政国家へ委任し受動化する過程。「支配するのではなく世話をする」後見国家（tutelary state）。
- **`SoftDespotismRules`**（static・純ロジック）：`PaternalisticReach`（行政の生活介入度0..1） / `CivicPassivity`（市民の受動性 = 1 - CivicEngagement） / `SoftDespotismRisk`（reach × passivity）/ `IsFullySoft`（reach > 0.7 & passivity > 0.7 → 軍事力なしに民主的外見のまま専制達成） / `DemocraticFacade`（形式的選挙/権利が残っているが実質空洞）。`CoupRules` と区別：`IsFullySoft` は `CoupRules.WouldCoup=false` でも成立。
- 接続：`AtomizationRules.AtomizationIndex`（高孤立 → `PaternalisticReach` 拡大） × `MinistryRules` の省益/縦割り（行政膨張の物理的担体） × `CivilianControlRules`（文民統制が逆説的に軍を抑えて行政を際限なく膨張させる）。

#### TOCQ 平等化の潮流と身分侵食（`EqualityDriftRules`）
- **平等化圧力**：民主的政体の統治期間が長いほど `EqualityLevel`（社会的平等度0..1）が上昇 → `SeniorityRules.PoliticalRigidity`（席次の硬直度）に下方圧力 → `RankSystem` の階級メリットが長期で減衰。
- **`EqualityDriftRules`**（static・純ロジック）：`EqualityLevel(polity, regime, years)` / `AristocraticDecay(equality, rankTier)` （上位tierほど侵食される）/ `MeritocracyBias(equality)`（平等化が進むほど能力主義傾向 `SeniorityRules.MeritOvertakes` を強化） / `BacklashRisk`（平等化速度が速すぎると保守反動 → `DynastyRules.Revolution` リスク）。
- 接続：帝国（専制・貴族制）は `EqualityLevel` が低く固定され、内外の民主化圧力に曝されると `BacklashRisk` が蓄積する。**Ginei の帝国 vs 同盟の核心テンションの数値化**。

### ★ 中（世界観lore・コード新設なし）

#### TOCQ（lore）民主主義の世界観開示データ
- 「多数者の専制は王権の専制より深く浸透する」「中間団体なき民主制は専制へ向かう」「平等への愛は自由への愛を追い越す」。
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への**lore データ入力**。秘史「民主化の逆説」「帝国の終わりは外征ではなく内側の空洞化から」などの条件発火イベント素材。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 三権分立・制度的チェックアンドバランス | `SeparationOfPowersRules`（#171）がカバー |
| 連邦制 vs 中央集権（制度論） | `MinistryRules`/`OrderOfBattle` に薄く接続するだけ（新EPIC化しない） |
| 陪審制・法の支配 | ゲームスコープ外（Star Wars的スペオペに陪審は不要） |
| 新聞・言論の自由（個別） | `EspionageRules`（情報）＋`EventEngine`（風説）＋今後の情報戦EPICで対応 |
| 宗教と民主制（詳細） | `ReligionRules`（#172-175）がカバー |
| 民主的魂の不安（短期主義・浮き足だち） | `HopeRules`の末人概念で近似可能。新EPIC化しない |
| プロテスタント精神と資本主義（ウェーバー的議論） | SAWエピックとReligionRulesで対応済み |

---

## 3. EPIC #TOCQ の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UIへ配線。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #1472**。GitHub issue 起票済み（#1478〜#1501）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **TOCQ-1** | #1478 | `MajorityTyrannyRules`/`MinorityOpinion` 多数者の専制（社会的同質化圧力・少数意見の封殺） | `SeparationOfPowersRules.TyrannyRisk` × 結社（TOCQ-2）×社会的圧力軸 |
| **TOCQ-2** | #1482 | `CivicAssociation`/`AssociationRules` 中間団体・市民結社（国家と個人の間の自発的緩衝体） | `ConsentRules.cooperation` 底上げ × `MajorityTyrannyRules.InstitutionalProtection` × soft despotism 相殺 |
| **TOCQ-3** | #1486 | `AtomizationRules` 民主的孤立化スパイラル（平等→個人主義→公的参加撤退→集合行動弱体化） | `Community.hope`（CircuitBreaker）× `FactionState.Stability` 係数 |
| **TOCQ-4** | #1492 | `SoftDespotismRules` 穏やかな専制（行政後見国家・暴力なき受動化・CoupRulesとの対称系） | `AtomizationRules.AtomizationIndex` × `MinistryRules` × `CivilianControlRules` |
| **TOCQ-5** | #1498 | `EqualityDriftRules` 平等化の潮流と身分侵食（民主化圧力が階級/序列に与える長期係数） | `SeniorityRules.PoliticalRigidity` × `RankSystem` × `DynastyRules.Revolution`（BacklashRisk） |
| **TOCQ-6** | #1501 | （lore）民主主義の世界観開示データ（多数者専制の逆説/中間団体/帝国の内側からの空洞化） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`TOCQ-1 → TOCQ-2`（多数者専制と中間団体は表裏一体・signature の核）→ `TOCQ-3`（孤立化スパイラルは両者の接続）→ `TOCQ-4`（穏やかな専制はスパイラルの帰結）→ `TOCQ-5`（平等化圧力＝帝国/同盟対立の数値化）→ `TOCQ-6`（lore）。

> いずれも既存政治シミュレーション層を**後退させず接続**する additive 設計。`CoupRules`（hard coup）の**対称系としての soft despotism**が核。帝国＝貴族制専制 と 同盟＝民主制の両方が**内発的崩壊メカニクスを持つ**ことで Ginei の双方向テンションが深まる。
