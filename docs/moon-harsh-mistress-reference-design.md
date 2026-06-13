# ハインライン『月は無慈悲な夜の女王』参考設計（EPIC #LUNA）

> 参照元：ロバート・A・ハインライン著『月は無慈悲な夜の女王』（The Moon is a Harsh Mistress）。
> 地球の支配下に置かれた月面植民地が、細胞型地下組織と自覚を持つAI"マイク"の助けを借りて独立革命を成し遂げる物語。
> 本ドキュメントは当プロジェクト（Ginei＝銀英伝風の星間国家戦略）にとって**役に立つ視点**だけを抽出しEPIC `#LUNA` としてissue化する提案。
> 著作権注意：固有名・文章・キャラクター・固有設定は流用せず、**メカニクス／世界観の構造パターンのみ**を参考にする。

---

## 0. なぜ「月は無慈悲な夜の女王」が本システムに役立つか

当プロジェクトは反乱・占領・離脱をカバーする層を**既に複数保有**している：

| 既存（カバー範囲） | カバー |
|---|---|
| `ConsentRules`/`Polity`（#836） | 統治からの非協力・撤退・ `Withdraw` 機能 |
| `LoyaltyRules`/`BattleAllegianceRules`（#817） | 忠誠カスケード・寝返り・静観 |
| `SecurityRules`/`SecurityApparatus`（#166） | 弾圧・クーデター検知・密告 |
| `EspionageRules`/`SpyNetwork`（Wave2） | 諜報ミッション成功率・発覚リスク |
| `NonviolenceRules`/`Movement`（#831） | 非暴力抵抗・支持転換 |
| `OrganizationRules`/`SuccessionRules`（#812） | カリスマ死後の組織存続・制度化 |
| `GovernanceRules`（#109） | 占領統合・安定度・反乱リスク |
| `FiscalRules`/`FiscalState`（#161） | 歳入歳出・補助金（歳出として計上） |
| `ResourceProductionRules`（#93） | 星系の資源産出係数 |
| `WarGoalRules`/`CasusBelli`（DIP-3 #192） | 厭戦・戦争目標 |
| `DynastyRules`/`Regime`（#867） | 政体転換・正統性腐食 |

**しかしこれらは「国家・市場・組織」の抽象モデル**であり、本作が固有に描く以下が**欠けている**：

| 月は無慈悲が固有に持つ視点 | 当プロジェクトでの欠落 |
|---|---|
| **細胞型コンパートメント組織（芋づる防止トポロジー）** | `EspionageRules` はミッション成功率と発覚リスク。**捕捉1件が漏らす接触数をトポロジーで制限する**（各員は3人しか知らない）モデルが無い |
| **宣伝機械（ゼロ限界費用の情報複製）** | `Movement` は支持の増減。**1回の制作が無限複製され全圏域を汚染する**宣伝の規模効果が無い |
| **TANSTAAFL補助金依存サイクル** | `FiscalRules` は補助金を歳出として扱うだけ。**補助→受取側の自立意志の腐食→生産性低下**の依存ループが無い |
| **採掘植民地の資源枯渇曲線** | `ResourceProductionRules` は安定度比例産出係数。**再生不能資源の枯渇カーブ**（採掘が進むほど漸減してゼロへ近づく）が無い |
| **非対称抑止（弱者が持つ決定的脅威）** | `WarGoalRules` は厭戦と戦争目標。**弱者が決定的な一撃能力を持つことで強者が踏み込めなくなる抑止均衡**（脅威信頼性・先制誘因）が無い |
| **解放後の革命組織劣化（解放者→新抑圧者）** | `SuccessionRules` は英雄死後の継承。**革命勝利後に組織が官僚化・内部利益化し当初目的を裏切る**ループが無い |

**結論**：本作は当プロジェクトの反乱・諜報・経済層に、
①細胞組織のトポロジー的秘匿性、
②宣伝の規模効果（ゼロ限界費用）、
③補助依存の経済的腐食、
④資源枯渇の衰退曲線、
⑤非対称抑止、
⑥革命組織の自己腐敗
という6つの欠落軸を与える。星間植民地・独立運動・多極戦略で**最も機能する**テクスチャ。

---

## 1. 役に立つ視点（要約）

本作の世界観を**当プロジェクトに効く形**で1行ずつ：

1. **細胞は3しか知らない**——捕捉の波及を構造で断つ。諜報・地下組織のトポロジーを数値化する。
2. **宣伝はゼロ限界費用**——1本の檄文を複製し続ければやがて全惑星の世論が動く。情報戦の規模の経済。
3. **TANSTAAFL——タダ飯などない**——補助金は短期の友、長期の敵。受取側の自立を腐食し、提供側を枯渇させる。
4. **採掘植民地は食い尽くされる**——抽出型経済の終点は枯渇。星系の衰退曲線がある。
5. **弱者でも棍棒を持てば強者は止まる**——軌道上の石は核より安上がりな抑止手段。非対称抑止理論。
6. **解放者はいつも新たな支配者になる**——革命が成功した瞬間に組織は目的を失い変質する。制度化vs解体のジレンマ。

---

## 2. 取り入れるべきメカニクス（優先度つき・既存への接続）

> 大原則：**既存の `EspionageRules`/`ConsentRules`/`OrganizationRules`/`FiscalRules`/`ResourceProductionRules` を作り直さない**。LUNAはそれらに**欠落軸を追加接続**するだけ（additive）。

### ★★★ 最優先（真の欠落・本作のsignature）

#### LUNA 細胞型コンパートメント組織（捕捉耐性トポロジー）
- **細胞構造モデル**：地下組織メンバー各員が知る接触数を `cellFanout`（既定3）に制限。捕捉1件で漏れる接触数≤`cellFanout`。捕捉連鎖は接触グラフ上のBFS距離で止まる。
- **秘匿度**：`CellNode.secrecy`（0..1）。捕捉・尋問で低下。ゼロで全接触を暴露（`ExposeContacts`）。
- **組織規模vs秘匿性のトレードオフ**：`cellFanout` を増やすと動員力↑だが連鎖脆弱性↑。
- 接続：`EspionageRules.DetectionRisk` に「暴露接触数が多い組織は発覚リスク比例増大」を追加。`SecurityRules.DissentSuppression` の標的が `CellNetwork` になる（芋づるで潰せる範囲が限定される）。
- 新設：`CellNetworkRules`（static）＋`CellNode`（pure data）。**test-first**。

#### LUNA 宣伝機械（ゼロ限界費用・世論操作の規模効果）
- **宣伝力**：`PropagandaState.output`（単位時間に生成できる宣伝コンテンツ量）×`reach`（接触圏域人口比）。複製はゼロ限界費用＝一度制作すれば無限拡散。
- **世論ドリフト**：`targetOpinion`（宣伝が向ける方向）×`driftRate`（reach比例）で `Province.nativeIdeology` や `Polity` の `legitimacy`/`cooperation` を誘導。真偽判定は外部（`EspionageRules` 情報優位で逆宣伝）。
- **規模の逓減**：同一圏域への過剰投下は効果低下（飽和係数 `saturationDecay`）。
- 接続：`Movement.Repress`（可視化弾圧×宣伝reach＝より広域に波及）×`EventEngine`（宣伝イベント）×`GovernanceRules`（安定度目標値に宣伝ドリフトを加味）。
- 新設：`PropagandaRules`（static）＋`PropagandaState`（pure data）。**test-first**。

### ★★ 高（既存の重要な拡張）

#### LUNA TANSTAAFL補助金依存サイクル（補助→自立意志の腐食）
- **依存度**：`SubsidyDependency.dependency`（0..1）。補助金が続くと上昇、停止後は緩やかに低下。
- **効果**：`dependency` が高いほど生産性係数（`GovernanceRules.OutputFactor` に乗算）が低下、かつ補助停止時の安定度ショックが大きい（依存が深いほど離脱コスト↑）。
- **提供側**：補助を払い続けると `FiscalRules.Expenditure` を圧迫し財政悪化→一方的に打ち切れないジレンマ。
- 接続：`FiscalRules.Expenditure`（補助歳出）×`GovernanceRules.OutputFactor`×`ConsentRules.ControlStrength`。
- 新設：`SubsidyRules`（static）＋`SubsidyDependency`（pure data）。**test-first**。

#### LUNA 採掘植民地の資源枯渇曲線（抽出型経済の衰退）
- **枯渇係数**：`DepletionState.remaining`（0..1・初期1）。採掘量に比例して低下。
- **産出逓減**：`ResourceProductionRules.Produce` に `remaining` を乗算＝枯渇が進むと産出が減少してゼロへ収束。
- **枯渇後**：星系が事実上の廃墟化→`GovernanceRules` の安定度極小・反乱リスク急上昇。
- 接続：`ResourceProductionRules`×`GovernanceRules`。`ShipyardRules.ProductionFactor`（造船）にも波及。
- 新設：`DepletionRules`（static）＋`DepletionState`（pure data）。**test-first**。

#### LUNA 非対称抑止（弱者の決定的脅威・軌道投射モデル）
- **抑止力**：`DeterrenceState.strike`（決定的一撃能力。軌道兵器/核/生物兵器に相当）×`credibility`（0..1・先制攻撃されれば消滅→低くなりやすい）。
- **抑止均衡**：強者が弱者へ先制攻撃する場合、弱者の残存 `strike`×`credibility` が強者の許容損害を超えるなら攻撃を抑止。弱者はcredibilityを維持するために公言・証明が必要。
- **抑止の罠**：credibilityを高める＝脅威の公言が先制攻撃の誘因を生む（use-it-or-lose-it問題）。
- 接続：`WarGoalRules.PeaceAcceptance`（抑止力が高い側への講和提案はペナルティ低下）×`DiplomacyRules`×`SecurityRules.CoupRisk`（credibility低下で内部崩壊リスク上昇）。
- 新設：`DeterrenceRules`（static）＋`DeterrenceState`（pure data）。**test-first**。

### ★ 中（既存への軽微な拡張）

#### LUNA 解放後の革命組織劣化（解放者→新抑圧者サイクル）
- **革命後ドリフト**：`Organization.purpose`（0=利己化〜1=使命維持）。勝利後に脅威が消えると `purpose` が逓減し、`institutionalization`（制度化）への投資がなければ `Organization.collapse` へ。
- **組織化のジレンマ**：`InvestInstitution`（制度化投資）は目的維持に有効だが、官僚化で `purpose` も徐々に低下。`Refactor`（中央集権化）は短期安定だが `离反リスク`（bug3）。
- 接続：`SuccessionRules.InvestInstitution`/`Refactor`×`DynastyRules.Tick`（腐敗進行）×`Polity.Withdraw`（使命喪失で民が離れる）。既存拡張（`Organization` に `purpose` フィールド追加）。**test-first**。

#### LUNA（lore）開示データ（TANSTAAFL哲学・自由の代償・個人と集団の緊張）
- 「すべての自由には代償がある」「補助金はいつか請求書になる」「解放組織は勝利で死ぬ」。
- 接続：**コード新設せず** `DisclosureLedger`（FND-4）への**loreデータ入力**。独立革命シナリオの世界観codexとして。

### ❌ 不採用（重複・既存で十分）

| 不採用 | 理由 |
|---|---|
| 非協力・ストライキ・ボイコット | **`ConsentRules.Withdraw`/`Polity.IsUngovernable` が既にカバー** |
| 捕虜・尋問・処断 | **`CaptivityRules`（LIFE-4）が既にカバー** |
| 地下組織の「拠点」物理シミュレーション | マイクロ操作になりタイクン化。`CellNetwork` のトポロジー計算に留める |
| 軌道爆撃の物理計算（弾道・着弾誤差） | 会戦は2D戦術マップ中心。抑止均衡の純ロジック（`DeterrenceRules`）のみで足りる |
| AI自意識・感情モデル | 主旨はAIが「いつ自覚を得るか」ではなく組織モデル。固有設定を流用しない |
| 月面重力・資源採掘の工学設定 | 個別アセット。`DepletionRules` の枯渇曲線で十分 |

---

## 3. EPIC #LUNA の子Issue（採用分のみ・着手順）

> 純ロジックはTestHarness/EditModeで先に固定（test-first）→盤面/UIへ配線。既存モジュールは**接続のみ・重複新設しない**。
> 著作権注意：固有名・文章・キャラは不使用、**メカニクス/世界観構造のみ**参考。

> **EPIC = #2333**。GitHub issue 起票済み（#2335〜#2350）。

| # | issue | タイトル | 接続先 / 主眼 |
|---|---|---|---|
| **LUNA-1** | #2335 | 細胞型コンパートメント地下組織（`CellNetworkRules`/`CellNode`・捕捉耐性トポロジー） | `EspionageRules.DetectionRisk`×`SecurityRules`。芋づる防止のグラフ計算 |
| **LUNA-2** | #2337 | 宣伝機械（`PropagandaRules`/`PropagandaState`・ゼロ限界費用の世論操作・規模効果） | `Movement`×`GovernanceRules`×`EventEngine`。宣伝ドリフト+飽和係数 |
| **LUNA-3** | #2339 | TANSTAAFL補助金依存サイクル（`SubsidyRules`/`SubsidyDependency`・補助→自立意志の腐食） | `FiscalRules.Expenditure`×`GovernanceRules.OutputFactor`×`ConsentRules` |
| **LUNA-4** | #2342 | 採掘植民地の資源枯渇曲線（`DepletionRules`/`DepletionState`・抽出型経済の衰退） | `ResourceProductionRules`×`GovernanceRules`×`ShipyardRules.ProductionFactor` |
| **LUNA-5** | #2344 | 非対称抑止（`DeterrenceRules`/`DeterrenceState`・弱者の決定的脅威・軌道投射モデル） | `WarGoalRules.PeaceAcceptance`×`DiplomacyRules`×`SecurityRules.CoupRisk` |
| **LUNA-6** | #2346 | 解放後の革命組織劣化（`Organization.purpose`フィールド追加・解放者→新抑圧者サイクル） | `SuccessionRules`×`DynastyRules`×`Polity.Withdraw` |
| **LUNA-7** | #2350 | （lore）開示データ（TANSTAAFL哲学・自由の代償・独立革命の世界観codex） | `DisclosureLedger`（FND-4）。コード新設なし |

### 推奨着手順
`LUNA-1`（細胞組織＝最も固有な欠落・諜報層に深さを与える）→ `LUNA-2`（宣伝機械＝情報戦の核）→ `LUNA-3`（TANSTAAFL補助依存＝経済搾取の動学）→ `LUNA-4`（枯渇曲線＝植民地衰退の必然）→ `LUNA-5`（非対称抑止＝独立戦略の切り札）→ `LUNA-6`（革命劣化＝`Organization`拡張・軽微）→ `LUNA-7`（lore・最後）。

> いずれも既存モジュールを**後退させず接続**するadditive設計。独立運動・植民地シナリオ・多極戦略に最も効く。
