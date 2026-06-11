# マキャヴェリ『ディスコルシ（ローマ史論）』参考設計（EPIC #DISC）

> 参照元：ニッコロ・マキャヴェリ『ローマ史論（Discorsi sopra la prima deca di Tito Livio）』（1517年頃成立）。
> 『君主論』の「新君主の権力技術」に対し、**共和政の構造的優位・自由の制度的基盤・腐敗と更新の周期**を論じた政治思想の古典。
> 本ドキュメントは、当プロジェクト（Ginei＝社会・政治シミュ層を持つ星間国家戦略）にとって
> **役に立つ視点だけ**を抽出し、EPIC `#DISC` として issue 化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**政治メカニクス／世界観の構造パターンのみ**を参考にする。

---

## 0. なぜ「ディスコルシ」が本システムに役立つか

当プロジェクトは統治・支持・強制力に関する**純ロジックを大量に保有**している：

| 既存（カバー範囲） | 対応モジュール |
|---|---|
| 省支持・安定度・占領統合 | `GovernanceRules`/`Province` |
| 被支配者の協力と撤退（非協力） | `ConsentRules`/`Polity` |
| 王朝腐敗・易姓革命 | `DynastyRules`/`Regime` |
| 組織の継承・制度化 | `Organization`/`SuccessionRules`（#812） |
| 政党・最小選挙 | `PartyRules`/`Party`（GOV-6） |
| 忠誠・調略・寝返りカスケード | `LoyaltyRules`/`BattleAllegianceRules` |
| 階級別経済負担・再分配 | `RedistributionRules`/`TaxStructure`（#163） |
| 建国の制度化・憲法制定権力 | `LawgiverRules`/ROUS-2 |
| 征服地統治の三様 | `ConquestDispositionRules`/MKV-1 |
| 抑圧の非線形モデル | `CoerciveStyleRules`/MKV-2 |
| 艦隊プール・造船供給 | `FleetPool`/`FleetPoolRules`/`ShipyardRules` |

**しかし、これらは「制度・均衡・集計値」または「君主の権力技術」であり**、ディスコルシが固有に描く以下が**欠けている**：

| ディスコルシが固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **階級対立の制度的チャネル化**（護民官） | `RedistributionRules` は経済格差。`ConsentRules.Withdraw` は受動的離脱。**「貴族vs平民の対立を護民官的機関で制度に吸収し法律を生み出す」能動的チャネル**が無い。欠落は反乱か内戦のどちらかしかない |
| **市民軍と傭兵の忠誠軸**（徴募源泉→離反リスク） | `FleetPool` は兵力量のみ。**徴募の源泉（市民兵=高忠誠 vs 傭兵=逆境で離反）が艦隊品質・離反確率に影響する軸**が無い。MKV は「傭兵問題は既存でカバー」として棄却したが数値軸としては未設 |
| **リノヴァツィオーネ**（共和政の定期的自己更新） | `DynastyRules.Reform` は危機駆動の反応的改革。**腐敗が一定水準に達する前に建国精神へ回帰する予防的刷新のメカニズム**が無い |
| **建国者の自己廃絶テスト**（独裁の正当性判定） | `LawgiverRules`(ROUS-2) は憲法制定の一回性の逆説。**建国後の体制が「制度を築き自己廃絶する共和制型独裁」か「権力を固定する専制への転落」かを評価する軌道判定**が無い |
| **拡大vs保全の共和国活力モデル** | 戦略的選択として星系拡張はあるが、**「拡大しない共和国は内部派閥対立のはけ口を失い停滞する」という活力ダイナミクス**が無い |

**結論**：ディスコルシは当プロジェクトの統治ロジックに**「共和政の内部力学」**という視角から、
①**護民官的機関**（階級対立→法律の生成チャネル）、
②**徴募源泉別の忠誠軸**（市民軍vs傭兵）、
③**リノヴァツィオーネ**（腐敗の予防的刷新）、
④**建国者の軌道判定**（共和制移行か専制固定か）
という4つの欠落軸を与える。
`GovernanceRules`（統治）・`FleetPool`（軍事）・`DynastyRules`（体制）への**additive な接続**。

---

## 1. 役に立つ視点（要約）

ディスコルシの世界観を、**本システムに効く形**で1行ずつ：

1. **自由な社会は内部対立から生まれる** — 元老院vs平民の対立が良き法律を生んだ。和合ではなく、**制度化された対立**が共和政の源泉。→ `ConsentRules` の「非協力」を補完する「制度的対立チャネル」の新設（DISC-1）。
2. **傭兵に頼る国は滅ぶ** — 自らのために戦わない兵士は、形勢が傾いた瞬間に逃げる。市民が戦士であることが共和政の軍事的基盤。→ `FleetPool` に徴募源泉の品質軸を追加（DISC-2）。
3. **共和政は腐敗する。問いは「いつ」ではなく「どう刷新するか」** — 定期的に創設者の精神へ立ち返る刷新（`rinnovazione`）だけが共和政の長命を保証する。→ `DynastyRules` に予防的刷新を追加（DISC-3）。
4. **建国の独裁は目的のためにのみ正当化される** — ロムルスがレムスを殺したのは私益のためでなかった。独裁が「制度建設のための一時的手段」か「権力固定の目的」かを軌道で判定できる。→ `Organization` に軌道評価指標を追加（DISC-4）。
5. **共和国は拡大することで内部活力を保つ** — スパルタは保全を選んで停滞し、ローマは拡大を選んで内部対立をエネルギーに変えた。→ 拡大とFactionState活力の連動（DISC-5）。
6. **民衆の集合判断は君主の判断より誤りが少ない（安定期に限り）** — 個人の天才ではなく制度的多数決が共和政の知的優位。→ `PartyRules`・`ConsentRules` の民主政体スコアに共鳴（lore）。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**`GovernanceRules`/`ConsentRules`/`DynastyRules`/`FleetPool`/`Organization` を作り直さない**。DISC はそれらに**欠落軸を足し、接続する**だけ（additive）。

### ★★★ 最優先（真の欠落・ディスコルシの signature）

#### DISC 護民官的機関（階級対立の制度的チャネル化）
- **制度的対立モデル**：勢力内の「上位層支持（`eliteSupport`）vs 下位層支持（`plebeianSupport`）」の緊張を、**護民官的機関（`TribunalOffice`）** が吸収するかどうかで結果が分岐：
  - **機関あり**：緊張 → 政治的対立 → 立法（`GovernanceRules.Tick` の `equilibriumStability` 補正） → 法の質↑
  - **機関なし**：緊張 → `ConsentRules.Withdraw`（大規模非協力）or `CoupRules.WouldCoup`（クーデター）へ直結
- 接続：新 `TribunalRules`（static・純ロジック・test-first）。`GovernmentRegistry.TryAppoint` で護民官ポストを登録、`ConsentRules`/`CoupRules` の係数修正子として挿入。`RedistributionRules.ClassTension` と連動（緊張が高いほど護民官の価値が大きい）。

#### DISC 市民軍と傭兵の忠誠軸（徴募源泉→離反確率）
- **徴募源泉 `RecruitmentSource{市民,志願,傭兵}`**：`FleetPool`/`FleetPoolRules` に源泉属性を追加：
  - **市民兵**：忠誠ベースライン高（`LoyaltyRules.BaselineLoyalty` 補正↑）、逆境での離反確率低、徴募コスト中
  - **志願兵**：中間。専門性高（攻撃補正↑）、忠誠は状況依存
  - **傭兵**：専門性最高、忠誠ベースライン低、逆境（`FleetMorale.IsRouted` や劣勢）で `BattleAllegianceRules` 経由の静観/撤退確率↑
- 接続：新 `MilitiaLoyaltyRules`（static・純ロジック・test-first）。`FleetPool`/`FleetPoolRules` を source属性付きで拡張、`LoyaltyRules`/`BattleAllegianceRules`/`BattleAllegianceManager` が源泉別係数を読む。

### ★★ 高（既存ロジックへの重要な欠落を補う）

#### DISC リノヴァツィオーネ（共和政の予防的自己刷新）
- **腐敗蓄積モデル**：`Regime.腐敗` が閾値未満でも **`institutionalDecay`（制度疲弊）** が `CalendarDispatcher`（TIME-6）の年次 Tick で蓄積。閾値を超えた瞬間が腐敗爆発ではなく**刷新の好機ウィンドウ**（`RenewalWindow`）：
  - ウィンドウ中に刷新（`Renew()`）を実施 → `institutionalDecay` リセット、正統性↑
  - ウィンドウを見逃す → `DynastyRules` の腐敗サイクルへそのまま流れ込み、危機が加速
- `DynastyRules.Reform`（危機駆動）との違い：これは**危機前の予防的刷新**。正統性が高い間だけ低コストで実施できる（刷新を怠り腐敗した後は `DynastyRules.Reform` の高コスト改革が必要になる）。
- 接続：新 `RinnovazioneRules`（static・純ロジック・test-first）。`Organization`(#812) の `InvestInstitution` / `Regime` の `Tick` に隣接して配置。`CalendarDispatcher.onYear` で `Tick`。

#### DISC 建国者の自己廃絶テスト（体制軌道判定）
- **軌道判定**：`Organization.charisma` が高く `institution` が低い「カリスマ依存体制」に対し、ターンごとに：
  - `InstitutionBuildingRate`（制度投資速度）> `CharismaConcentrationRate`（権力集中速度）→ **共和制移行軌道**：`Organization.institution` 上昇、`Regime.正統性` 安定
  - 逆 → **専制固定軌道**：`DynastyRules.MythPressure` 加速、カリスマ後継危機が深刻化
- ROUS-2 `LawgiverRules`（憲法制定の一回性）と**別軸**：これは「建国後の継続的な制度構築ペース」の評価（静的な一回性 vs 動的な軌道）。接続のみ・重複新設しない。
- 接続：新 `FounderTrajectoryRules`（static・純ロジック・test-first）。`Organization`/`Regime`/`DynastyRules` のデータを読んで評価値 `trajectoryScore(-1.0..1.0)` を返す。`CampaignRules.Tick` が毎ターン `FactionState` 経由で評価。

### ★ 中（活力モデル・世界観lore）

#### DISC 拡大vs保全の活力ダイナミクス
- 版図が固定（拡大ゼロ）の共和政勢力は、内部対立のはけ口を失い `FactionState.Stability` が緩やかに低下（内部派閥の空転ペナルティ）。逆に適度な拡大がある間は内部対立を生産的に向けられる。
- 接続：`CampaignRules.EffectiveStability` に `LogisticsRules.CohesionFactor` × 拡大率（StarSystem占領変化）の修正子を追加（既存 `LogisticsRules` を拡張）。新規モジュール不要・係数のみ追加。

#### DISC（lore）世界観の開示データ
- 「自由な社会は対立から生まれる」「傭兵に頼る国は滅ぶ」「建国の独裁者は自らを不要にすることで正当化される」「腐敗は法律ではなく戻ることで防ぐ」。
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への**lore データ入力**。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 共和政 vs 君主政の政体変換エンジン | `DynastyRules.Revolution`/`Reform` および `Regime` がカバー。DISC は係数修正子として接続のみ |
| 護民官の通常の政府役職化（複雑実装） | `GovernmentRegistry`/`OfficeRules`（GOV-1/3）で既存ポスト追加は可。DISC は**純ロジック層の係数**のみ実装（UI配線はGOV既存経路に委譲） |
| 元老院の独立した意思決定AI | タイクン化回避。護民官的機関の係数として表現 |
| 民主政体の「民衆の集合知」独立エンジン | `PartyRules.RulingParty`/`ConsentRules` で部分カバー。新規は微小なlore貢献 → lore（DISC-6）に収納 |
| 傭兵の雇用・契約管理UI | タイクン化回避。数値軸のみ `MilitiaLoyaltyRules` で表現 |
| ローマ史の具体的な制度名（元老院/護民官/等）の固有名UI | 著作権上は問題ないが固有名依存は避け、抽象的機能として実装 |

---

## 3. EPIC #DISC の子Issue（採用分のみ・着手順）

> 純ロジックは TestHarness/EditMode で先に固定（test-first）→ 盤面/UI へ配線。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #1475**。GitHub issue 起票済み（#1479〜#1500）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **DISC-1** | #1479 | `TribunalRules`/護民官的機関（階級対立の制度チャネル化・上位層vs下位層緊張→政治的対立→立法 or 反乱の分岐） | 新 `TribunalRules`。`GovernmentRegistry`×`ConsentRules`/`CoupRules`/`RedistributionRules.ClassTension` |
| **DISC-2** | #1483 | `MilitiaLoyaltyRules`/市民軍・志願兵・傭兵の忠誠軸（徴募源泉 `RecruitmentSource` → 逆境時の離反確率差） | 新 `MilitiaLoyaltyRules`。`FleetPool`/`FleetPoolRules` に source 属性追加×`LoyaltyRules`/`BattleAllegianceRules` |
| **DISC-3** | #1488 | `RinnovazioneRules`/リノヴァツィオーネ（腐敗蓄積→刷新好機ウィンドウ→予防的自己刷新。危機前の `DynastyRules.Reform` と対） | 新 `RinnovazioneRules`。`Organization`×`Regime`×`CalendarDispatcher`(TIME-6) |
| **DISC-4** | #1493 | `FounderTrajectoryRules`/建国者の自己廃絶テスト（制度投資速度 vs 権力集中速度 → 共和制軌道 or 専制固定軌道） | 新 `FounderTrajectoryRules`。`Organization`/`Regime`/`DynastyRules`/`FactionState`。ROUS-2 と別軸 |
| **DISC-5** | #1497 | 拡大vs保全の活力ダイナミクス（停滞版図→内部派閥空転ペナルティ・適度拡大→活力維持） | `LogisticsRules`×`CampaignRules.EffectiveStability` に修正子追加。新モジュール不要 |
| **DISC-6** | #1500 | （lore）世界観の開示データ（自由は対立から・傭兵国の滅び・建国独裁の正当性・腐敗への処方） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`DISC-1`（護民官的機関＝最も欠落が大きく `ConsentRules`/`CoupRules` に直接接続・共和政の核）→
`DISC-2`（市民軍vs傭兵＝`FleetPool` への徴募軸追加・戦略層の軍事品質に効く）→
`DISC-3`（リノヴァツィオーネ＝`DynastyRules` の予防的補完・腐敗管理の能動化）→
`DISC-4`（建国者の軌道判定＝`Organization`/`FactionState` の評価指標）→
`DISC-5`（活力ダイナミクス＝既存 `LogisticsRules` への係数追加のみ）→
`DISC-6`（lore）。

> いずれも既存の統治・軍事・体制ロジックを**後退させず接続**する additive 設計。
> 「共和政勢力の設計・腐敗・更新」という戦略フェーズの長期ダイナミクスに最も効く。
